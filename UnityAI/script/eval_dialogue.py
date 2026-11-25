"""Run with python -m script.eval_dialogue --compare-few-shot."""
import argparse
import json
import os
import re
import sys
import time
from pathlib import Path

from script.dialogue_loader import DialogueStore
from script.dialogue_grading import grade_dialogue, finalize_dialogue_grade, precheck
from script.rules import ruins_intent_by_rules, rule_errors
from script.model import EnglishModel, MODEL_DIR

MIN_PASS_RATE = float(os.environ.get("EVAL_MIN_PASS", "0.85"))
DETERMINISTIC_TAGS = {'precheck', 'rule', 'no_false_positive'}
GRAMMAR_EXPECT_KEYS = {'grammar_min', 'grammar_max', 'max_errors', 'must_quote_contains'}
HERE = Path(__file__).parent
EXPECT_KEYS = {
    "meaning_min", "meaning_max", "grammar_min", "grammar_max", "max_errors",
    "must_quote_contains", "passed", "total", "model_called", "reward_eligible",
    "total_max", "has_better_answer", "relevance",
}


def norm(s):
    return " ".join(re.sub(r"[^\w\s]", "", s.casefold()).split())


def check(case, data, called):
    e, f = case["expect"], []
    if "relevance" in e and data.get("relevance") != e["relevance"]:
        f.append(f"relevance={data.get('relevance')}")
    s = data["scores"]
    for key, val in (("meaning_min", s["meaning"]), ("grammar_min", s["grammar"])):
        if key in e and val < e[key]: f.append(f"{key}: got {val}")
    for key, val in (("meaning_max", s["meaning"]), ("grammar_max", s["grammar"])):
        if key in e and val > e[key]: f.append(f"{key}: got {val}")
    if "max_errors" in e and len(data["errors"]) > e["max_errors"]: f.append("báo lỗi oan")
    for q in e.get("must_quote_contains", []):
        if not any(q in x["quote"] for x in data["errors"]): f.append(f"bỏ sót lỗi '{q}'")
    if "passed" in e and data["passed"] != e["passed"]: f.append(f"passed={data['passed']}")
    if "total" in e and data["total_score"] != e["total"]: f.append(f"total={data['total_score']}")
    if "model_called" in e and called != e["model_called"]: f.append(f"model_called={called}")
    if "reward_eligible" in e and data["reward_eligible"] != e["reward_eligible"]:
        f.append(f"reward_eligible={data['reward_eligible']}")
    if 'total_max' in e and data['total_score'] > e['total_max']:
        f.append(f"total_max: got {data['total_score']}")
    if 'has_better_answer' in e:
        better = data.get('better_answer')
        has = bool(better and not rule_errors(better))
        if has != e['has_better_answer']: f.append(f'has_better_answer={has}')
    return f


def check_leakage(store, cases):
    shots = {norm(x["answer"]) for c in store.by_node.values() for x in c.few_shot}
    leaked = [c["id"] for c in cases if norm(c["answer"]) in shots]
    if leaked:
        raise ValueError(f"Ca test trùng few-shot: {leaked}")
    if not cases:
        raise ValueError("Bộ đánh giá rỗng")
    ids = set()
    for case in cases:
        if not isinstance(case.get("id"), str) or not case["id"] or case["id"] in ids:
            raise ValueError(f"ID ca đánh giá thiếu hoặc trùng: {case.get('id')}")
        ids.add(case["id"])
        if not isinstance(case.get("answer"), str):
            raise ValueError(f"{case['id']}: answer phải là chuỗi")
        expect = case.get("expect")
        if not isinstance(expect, dict) or not expect or set(expect) - EXPECT_KEYS:
            raise ValueError(f"{case['id']}: expect rỗng hoặc có key không được hỗ trợ")
        for key, value in expect.items():
            if key in {"passed", "model_called", "reward_eligible", "has_better_answer"}:
                valid = type(value) is bool
            elif key == "relevance":
                valid = isinstance(value, str) and value in {"story", "scene", "off"}
            elif key == "must_quote_contains":
                valid = isinstance(value, list) and all(isinstance(q, str) and q for q in value)
            else:
                maximum = 10 if key in {"total", "total_max"} else 3 if key == "max_errors" else 5
                valid = type(value) is int and 0 <= value <= maximum
            if not valid:
                raise ValueError(f"{case['id']}: giá trị expect.{key} không hợp lệ")
        for field in ("meaning", "grammar"):
            if expect.get(field + "_min", 0) > expect.get(field + "_max", 5):
                raise ValueError(f"{case['id']}: min > max cho {field}")
        if store.get(case["node_id"]) is None:
            raise ValueError(f"Không tìm thấy node_id: {case['node_id']}")


def summarize(results):
    eligible = [r for r in results if r.get('tag') != 'known_gap']
    ok = sum(not r["fails"] for r in eligible)
    model_results = [r for r in eligible if r["model_called"]]
    by_tag = {}
    for row in results:
        tag = row.get("tag", "legacy")
        group = by_tag.setdefault(tag, {"passed": 0, "total": 0})
        group["total"] += 1
        group["passed"] += not row["fails"]
    for group in by_tag.values():
        group["pass_rate"] = group["passed"] / group["total"]
    return {"passed": ok, "total": len(eligible), "pass_rate": ok / len(eligible) if eligible else None,
            "known_gap_cases": len(results) - len(eligible),
            "deterministic_failed": sum(bool(r['fails']) for r in eligible if r.get('tag') in DETERMINISTIC_TAGS),
            "by_tag": by_tag,
            "model_cases": len(model_results),
            "model_pass_rate": (sum(not r["fails"] for r in model_results) / len(model_results)
                                if model_results else None),
            "false_positives": sum("báo lỗi oan" in r["fails"] for r in results),
            "false_negatives": sum(any("bỏ sót lỗi" in f for f in r["fails"]) for r in results),
            "review_required": sum(r["status"] == "review_required" for r in results),
            "average_seconds": round(sum(r["seconds"] for r in results) / len(results), 2)}


def deterministic_case(ctx, case):
    """Grammar-only labels use fake meaning=5; meaning/reward labels aren't scored.

    Precheck labels are checked in full, since their complete result is deterministic.
    This path never calls inference and reports model_called=False honestly.
    """
    data = precheck(case['answer'])
    if case.get('tag') == 'precheck':
        return data, ['precheck did not reject'] if data is None else check(case, data, False)
    if data is None:
        data = finalize_dialogue_grade(ctx, case['answer'], 'story', 5, 'Đánh giá luật, không chấm nghĩa.',
                                       None, rule_errors(case['answer']))
    scoped = {**case, 'expect': {k: v for k, v in case['expect'].items() if k in GRAMMAR_EXPECT_KEYS}}
    return data, check(scoped, data, False)


def meets_threshold(summary):
    if summary['deterministic_failed']: return False
    rate = summary['model_pass_rate']
    return rate is None or rate >= MIN_PASS_RATE


def evaluate(ai, store, cases):
    results, times = [], []
    for case in cases:
        ctx = store.get(case["node_id"])
        case = {**case, 'tag': case.get('tag', 'precheck' if case['expect'].get('model_called') is False else 'legacy')}
        t0 = time.perf_counter()
        called = False
        try:
            if case.get('tag') in DETERMINISTIC_TAGS | {'known_gap'}:
                data, fails = deterministic_case(ctx, case)
                status = 'validated' if data is not None else 'error'
            else:
                called = precheck(case['answer']) is None
                status, data, called = grade_dialogue(ai, ctx, case["answer"])
                fails = ["review_required"] if data is None else check(case, data, called)
        except Exception as error:
            status, data = "error", None
            fails = [f"exception: {error}"]
        dt = time.perf_counter() - t0
        results.append({"id": case["id"], "status": status, "fails": fails, "seconds": round(dt, 2),
                        "node_id": case["node_id"], "tag": case.get("tag", "legacy"),
                        "model_called": called, "data": data})
        times.append(dt)
        print(("OK  " if not fails else "FAIL"), case["id"], fails or "", flush=True)

    ok = sum(not r["fails"] for r in results)
    rate = ok / len(results)
    print(f"\nĐạt {ok}/{len(results)} = {rate:.0%} | TB {sum(times)/len(times):.1f}s/ca")
    summary = summarize(results)
    for tag, group in summary['by_tag'].items():
        suffix = ' (excluded)' if tag == 'known_gap' else ''
        print(f"{tag}: {group['passed']}/{group['total']} = {group['pass_rate']:.1%}{suffix}", flush=True)
    print(json.dumps(summary, ensure_ascii=False), flush=True)
    return {"summary": summary, "results": results}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", type=Path, default=MODEL_DIR)
    parser.add_argument("--cases", type=Path, default=HERE / "eval_dialogue_cases.json")
    parser.add_argument("--output", type=Path, default=HERE / "eval_dialogue_report.json")
    parser.add_argument("--compare-few-shot", action="store_true")
    parser.add_argument("--case-id", action="append", help="Run selected IDs (repeatable)")
    parser.add_argument("--tag", action="append", help="Run selected categories (repeatable)")
    parser.add_argument('--deterministic', action='store_true', help='Run deterministic tags without loading the model')
    args = parser.parse_args()
    if not 0 <= MIN_PASS_RATE <= 1:
        parser.error("EVAL_MIN_PASS phải nằm trong 0..1")
    store = DialogueStore()
    cases = json.loads(args.cases.read_text(encoding="utf-8"))
    check_leakage(store, cases)
    if args.case_id:
        unknown = set(args.case_id) - {c["id"] for c in cases}
        if unknown:
            parser.error(f"Không tìm thấy case ID: {sorted(unknown)}")
        cases = [c for c in cases if c["id"] in args.case_id]
    if args.tag:
        cases = [c for c in cases if c.get("tag", "legacy") in args.tag]
    if args.deterministic:
        cases = [c for c in cases if c.get('tag') in DETERMINISTIC_TAGS]
    if not cases:
        parser.error("Không có ca đánh giá khớp bộ lọc")
    needs_model = any(c.get('tag') not in DETERMINISTIC_TAGS | {'known_gap'}
                      and precheck(c['answer']) is None for c in cases)
    ai = EnglishModel(args.model_dir) if needs_model else None
    modes = [False, True] if args.compare_few_shot else [os.environ.get("DISABLE_FEW_SHOT") != "1"]
    reports = {}
    original = os.environ.get("DISABLE_FEW_SHOT")
    try:
        for enabled in modes:
            os.environ["DISABLE_FEW_SHOT"] = "0" if enabled else "1"
            label = "with_few_shot" if enabled else "without_few_shot"
            print(label, flush=True)
            reports[label] = evaluate(ai, store, cases)
            args.output.write_text(json.dumps(reports, ensure_ascii=False, indent=2), encoding="utf-8")
    finally:
        if original is None:
            os.environ.pop("DISABLE_FEW_SHOT", None)
        else:
            os.environ["DISABLE_FEW_SHOT"] = original

    intent_ok = True
    for c in json.loads((HERE / "eval_intent_cases.json").read_text("utf-8")):
        got = ruins_intent_by_rules(c["text"])
        intent_ok = intent_ok and got == c["expect"]
        print(("OK  " if got == c["expect"] else "FAIL"), "intent", c["text"], got)
    # The enabled mode is the production criterion; baseline is a comparison.
    production = reports.get("with_few_shot", next(iter(reports.values())))
    return 0 if meets_threshold(production["summary"]) and intent_ok else 1


if __name__ == "__main__":
    sys.exit(main())

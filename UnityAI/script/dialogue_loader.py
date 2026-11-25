"""Nạp hội thoại + cấu hình chấm, dựng ngữ cảnh cho từng câu hỏi."""
import json
import os
from dataclasses import dataclass
from pathlib import Path

from script.dialogue_grading import validate_dialogue_grade
from script.rules import TENSES

DIALOGUE_DIR = Path(os.environ.get(
    "DIALOGUE_DIR", Path(__file__).resolve().parent.parent / "config" / "dialogues"))
HISTORY_LINES = 3          # số câu NPC liền trước đưa vào prompt


@dataclass(frozen=True)
class QuestionCtx:
    dialogue_id: str
    node_id: str
    question: str
    history: tuple          # tối đa HISTORY_LINES câu NPC trước câu hỏi
    scene_facts: str
    expected_intent: str
    expect_hint_vi: str
    references: tuple       # đáp án tham khảo (đưa vào prompt + dùng làm suggested_answer)
    few_shot: tuple         # tuple[dict] gồm answer + output
    min_words: int
    pass_total: int
    reward_total: int
    max_attempts: int | None
    target_forms: dict
    story_examples: tuple
    scene_examples: tuple
    off_examples: tuple
    scene_cap: int | None
    scene_hint_vi: str
    strict_form: bool = False
    exercise: dict | None = None
    known_facts: tuple = ()


def check_few_shots(ctx: QuestionCtx) -> None:
    for i, ex in enumerate(ctx.few_shot, 1):
        tag = f"{ctx.node_id} few_shot[{i}]"
        try:
            if (not isinstance(ex, dict) or set(ex) != {"answer", "output"}
                    or not isinstance(ex["answer"], str) or not ex["answer"].strip()):
                raise ValueError("few-shot phải gồm answer không rỗng và output")
            validate_dialogue_grade(ex["output"], ex["answer"])
            relevance, meaning = ex["output"]["relevance"], ex["output"]["meaning"]
            if relevance == "off" and meaning != 0:
                raise ValueError("off relevance requires meaning 0")
            if relevance == "scene" and not 1 <= meaning <= (ctx.scene_cap or 5):
                raise ValueError("scene meaning must be within 1 and the scene cap")
            if relevance == "story" and meaning < 1:
                raise ValueError("story relevance requires meaning at least 1")
        except ValueError as e:
            raise ValueError(f"{tag}: {e}") from e


class DialogueStore:
    def __init__(self, directory: Path = DIALOGUE_DIR):
        self.by_node: dict[str, QuestionCtx] = {}
        self.dialogues: dict[str, dict] = {}
        if not Path(directory).is_dir():
            raise FileNotFoundError(f"Không tìm thấy thư mục hội thoại: {directory}")
        for path in sorted(Path(directory).glob("*.json")):
            if path.name.endswith(".grading.json"):
                continue
            grading_path = path.with_name(path.stem + ".grading.json")
            if not grading_path.exists():
                raise FileNotFoundError(f"Thiếu file chấm: {grading_path.name}")
            dlg = json.loads(path.read_text(encoding="utf-8"))
            grading = json.loads(grading_path.read_text(encoding="utf-8"))
            self._load(dlg, grading)
        if not self.by_node:
            raise ValueError(f"Không có câu hỏi hội thoại trong {directory}")

    def _load(self, dlg: dict, grading: dict) -> None:
        did = dlg["id"]
        if did in self.dialogues:
            raise ValueError(f"Trùng dialogue_id: {did}")
        if grading.get("dialogue_id") != did:
            raise ValueError(f"dialogue_id không khớp: {did}")
        self.dialogues[did] = dlg
        defaults = {"min_words": 3, "pass_total": 6, "reward_total": 8,
                    "max_attempts": 3, **grading.get("defaults", {})}
        spoken: list[str] = []                       # các câu NPC đã nói, theo thứ tự
        for node in dlg["dialogues"]:
            if node["type"] == "question":
                if node["id"] in self.by_node:
                    raise ValueError(f"Trùng node_id: {node['id']}")
                g = grading["questions"].get(node["id"])
                if g is None:
                    raise ValueError(f"Thiếu cấu hình chấm cho {node['id']}")
                exercise = None
                if g.get('assessment_mode') == 'temple_exercise':
                    exercise = next((ex for ex in dlg.get('exercises', [])
                                     if ex.get('id') == g.get('exercise_id') == node.get('exercise_id')
                                     and ex.get('source_dialogue_id') == node['id']), None)
                    if exercise is None:
                        raise ValueError(f"{node['id']}: missing or mismatched temple exercise")
                    exercise = dict(exercise)
                    if 'report_model_instruction' in g:
                        exercise['evaluation'] = {**exercise['evaluation'], 'model_instruction_en': g['report_model_instruction']}
                cfg = {k: g.get(k, defaults[k]) for k in
                       ("min_words", "pass_total", "reward_total", "max_attempts")}
                for key, value in cfg.items():
                    if exercise is not None and key == 'max_attempts' and value is None and exercise['retry_policy']['mode'] == 'until_pass':
                        continue
                    if exercise is not None and key == 'min_words' and type(value) is int and value == 0:
                        continue
                    if type(value) is not int or value < 1:
                        raise ValueError(f"{node['id']}: {key} phải là số nguyên dương")
                if not 1 <= cfg["pass_total"] <= cfg["reward_total"] <= 10:
                    raise ValueError(f"{node['id']}: ngưỡng điểm phải thỏa 1 <= pass <= reward <= 10")
                target = g.get('target_forms')
                if (not isinstance(target, dict)
                        or set(target) != {'formula', 'accepted_tenses', 'hint_vi'}
                        or not isinstance(target['formula'], str) or not target['formula'].strip()
                        or not isinstance(target['hint_vi'], str) or not target['hint_vi'].strip()
                        or not isinstance(target['accepted_tenses'], list) or not target['accepted_tenses']
                        or any(not isinstance(t, str) or t not in TENSES for t in target['accepted_tenses'])):
                    raise ValueError(f"{node['id']}: invalid target_forms (formula, accepted_tenses, hint_vi)")
                strict = g.get('strict_form', defaults.get('strict_form', False))
                if type(strict) is not bool:
                    raise ValueError(f"{node['id']}: strict_form must be bool")
                for key in ("expected_intent", "expect_hint_vi"):
                    if not isinstance(g.get(key), str) or not g[key].strip():
                        raise ValueError(f"{node['id']}: thiếu {key}")
                if not isinstance(g.get("references"), list) or not g["references"] or any(
                        not isinstance(r, str) or not r.strip() for r in g["references"]):
                    raise ValueError(f"{node['id']}: references phải có câu trả lời mẫu")
                if not isinstance(g.get("few_shot", []), list):
                    raise ValueError(f"{node['id']}: few_shot phải là danh sách")
                examples = {}
                for key in ("story_examples", "scene_examples", "off_examples"):
                    values = g.get(key, [] if key == "scene_examples" else None)
                    if (not isinstance(values, list)
                            or (key != "scene_examples" and not values)
                            or any(not isinstance(v, str) or not v.strip() for v in values)):
                        raise ValueError(f"{node['id']}: invalid {key}")
                    examples[key] = tuple(values)
                scene_cap = g.get("scene_cap")
                if scene_cap is not None and (type(scene_cap) is not int or not 1 <= scene_cap <= 5):
                    raise ValueError(f"{node['id']}: scene_cap must be null or an integer 1-5")
                scene_hint = g.get("scene_hint_vi", "")
                if not isinstance(scene_hint, str):
                    raise ValueError(f"{node['id']}: scene_hint_vi must be a string")
                if scene_cap is not None and (not examples["scene_examples"] or not scene_hint.strip()):
                    raise ValueError(f"{node['id']}: scene_cap requires scene_examples and scene_hint_vi")
                ctx = QuestionCtx(
                    dialogue_id=did, node_id=node["id"], question=node["dialogue_en"],
                    history=tuple(spoken[-HISTORY_LINES:]),
                    scene_facts=grading.get("scene_facts", ""),
                    expected_intent=g["expected_intent"],
                    expect_hint_vi=g.get("expect_hint_vi", ""),
                    references=tuple(g.get("references", [])),
                    few_shot=tuple(g.get("few_shot", [])),
                    target_forms=target, strict_form=strict,
                    scene_cap=scene_cap, scene_hint_vi=scene_hint, **examples,
                    exercise=exercise, known_facts=tuple(dlg.get('known_facts', [])),
                    **cfg,
                )
                check_few_shots(ctx)
                self.by_node[node["id"]] = ctx
            spoken.append(node["dialogue_en"])       # thêm SAU khi xử lý để history không chứa chính câu hỏi

    def get(self, node_id: str) -> QuestionCtx | None:
        return self.by_node.get(node_id)

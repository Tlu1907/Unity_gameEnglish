import threading
from collections import OrderedDict

from flask import Flask, jsonify, request
from prometheus_flask_exporter import PrometheusMetrics

from script.model_gate import ModelGate
from script.model import EnglishModel, MODEL_DIR
from script.dialogue_loader import DialogueStore
from script.dialogue_grading import precheck, grade_with_model, sanitize_answer
from script.dialogue_intent import INTENT_SYSTEM, validate_intent
from script.rules import ruins_intent_by_rules
from script.temple_report import grade_report, report_precheck


app = Flask(__name__)
app.json.ensure_ascii = False
metrics = PrometheusMetrics(
    app,
    buckets=(0.1, 0.5, 1, 2.5, 5, 10, 20, 40, 80, 160)
)

# Nạp model một lần khi khởi động server.
dialogues = DialogueStore()
ai = EnglishModel(MODEL_DIR)

# Only one request may use the shared model at a time.
gate = ModelGate()

attempts = OrderedDict()
attempts_mu = threading.Lock()
inflight = set()
MAX_ATTEMPT_KEYS = 2000
MAX_DIALOGUE_ANSWER_CHARS = 300


@app.get("/health")
def health():
    return jsonify(
        status="ok",
        model_loaded=True,
        dialogues_loaded=len(dialogues.by_node),
        message="Flask va model ONNX dang hoat dong"
    )


@app.post("/grade-answer")
def grade_answer():
    payload = request.get_json(silent=True)
    if not isinstance(payload, dict):
        return jsonify(status="error", message="Can gui JSON object."), 400
    return grade_dialogue_answer(payload)


def exhausted_response(ctx):
    return jsonify(
        status="exhausted", node_id=ctx.node_id, attempts_left=0,
        passed=False, can_award_reward=False,
        suggested_answer=ctx.references[0] if ctx.references else None,
    ), 409


def grade_dialogue_answer(body):
    node_id, answer, session_id = (body.get(k) for k in ("node_id", "answer", "session_id"))
    if (not isinstance(node_id, str) or not node_id.strip()
            or not isinstance(session_id, str) or not session_id.strip()
            or not isinstance(answer, str)):
        return jsonify(status="error", message="node_id, session_id va answer phai la chuoi."), 400
    ctx = dialogues.get(node_id)
    if ctx is None:
        return jsonify(status="error", message="Khong tim thay node_id."), 404
    maximum = 1500 if ctx.exercise is not None and ctx.max_attempts is None else MAX_DIALOGUE_ANSWER_CHARS
    if len(answer) > maximum:
        return jsonify(status="error", message=f"Cau tra loi toi da {maximum} ky tu."), 400
    answer = answer.strip()
    if ctx.exercise is not None and not answer:
        return jsonify(status="error", message="Hãy nhập câu trả lời; lượt trống không tính."), 400
    key = (session_id.strip(), node_id)
    # Reserve the key before inference: concurrent requests cannot spend the
    # same attempt or claim the same reward. Failures release the reservation.
    with attempts_mu:
        rec = attempts.get(key, {"count": 0, "rewarded": False})
        if ctx.max_attempts is not None and rec["count"] >= ctx.max_attempts:
            return exhausted_response(ctx)
        if key in inflight:
            return jsonify(status="busy", can_award_reward=False), 503
        inflight.add(key)
    try:
        data = precheck(answer)
        if data is None:
            with gate.use(timeout=30) as got:
                if not got:
                    return jsonify(status="busy", can_award_reward=False), 503
                status, data = grade_with_model(ai, ctx, answer)
            if status != "validated":
                return jsonify(status="review_required", node_id=node_id,
                               data=None, can_award_reward=False), 200
        with attempts_mu:
            rec = dict(attempts.get(key, {"count": 0, "rewarded": False}))
            reward = bool(data["reward_eligible"]) and not rec["rewarded"]
            rec["count"] += 1
            rec["rewarded"] = rec["rewarded"] or reward
            attempts[key] = rec
            attempts.move_to_end(key)
            # Never evict a record while another request is using it.
            for old_key in list(attempts):
                if len(attempts) <= MAX_ATTEMPT_KEYS:
                    break
                if old_key not in inflight:
                    del attempts[old_key]
        left = ctx.max_attempts - rec["count"] if ctx.max_attempts is not None else -1
        response = dict(status="validated", node_id=node_id, data=data,
                        attempt=rec["count"], attempts_left=left,
                        passed=data["passed"], can_award_reward=reward,
                        unlimited_attempts=ctx.max_attempts is None)
        if left == 0 and not data["passed"] and ctx.references:
            response["suggested_answer"] = ctx.references[0]
        return jsonify(response), 200
    except Exception:
        app.logger.exception("Loi khi cham hoi thoai")
        return jsonify(status="error", message="Khong the cham bai. Luot nay chua tinh diem.",
                       can_award_reward=False), 500
    finally:
        with attempts_mu:
            inflight.discard(key)


@app.post("/grade-temple-report")
def grade_temple_report():
    body = request.get_json(silent=True)
    if not isinstance(body, dict) or body.get('node_id') != 'temple_09_report':
        return jsonify(status='error', passed=False, feedback_vi='Invalid temple report node.'), 400
    answer = body.get('answer')
    if not isinstance(answer, str) or not answer.strip() or len(answer) > 1500:
        return jsonify(status='error', passed=False, feedback_vi='Report must contain 1-1500 characters.'), 400
    early = report_precheck(answer)
    if early is not None:
        return jsonify(early)
    try:
        with gate.use(timeout=30) as got:
            if not got:
                return jsonify(status='busy', passed=False, feedback_vi='Bộ chấm đang bận. Hãy thử lại.'), 503
            result = grade_report(ai, answer)
        return jsonify(result)
    except Exception:
        app.logger.exception('Temple report grading failed')
        return jsonify(status='error', passed=False, feedback_vi='Không chấm được báo cáo. Hãy thử lại; bài không bị tính sai.'), 500


@app.post("/detect-intent")
def detect_intent():
    body = request.get_json(silent=True)
    text = body.get("text") if isinstance(body, dict) else None
    if not isinstance(text, str) or not text.strip() or len(text) > MAX_DIALOGUE_ANSWER_CHARS:
        return jsonify(status="error", message="text phai la chuoi 1-300 ky tu."), 400
    verdict, source = ruins_intent_by_rules(text), "rule"
    if verdict == "maybe":
        source = "model"
        verdict = "no"
        try:
            with gate.use(timeout=30) as got:
                if got:
                    safe_text = sanitize_answer(text).replace("<text>", "").replace("</text>", "")
                    result = ai.request_json(
                        INTENT_SYSTEM, f"<text>{safe_text}</text>",
                        validate_intent, max_new_tokens=30, sample=False,
                    )
                    if result["status"] == "validated" and result["data"]["asks_ruins_location"]:
                        verdict = "yes"
        except Exception:
            app.logger.exception("Loi khi nhan dien y dinh")
    return jsonify(status="ok", intent="ask_next_destination_location",
                   target_location="ancient_ruins", triggered=verdict == "yes", source=source)


if __name__ == "__main__":

    app.run(
        host="127.0.0.1",
        port=8000,
        debug=False,
        use_reloader=False,
        threaded=True
    )

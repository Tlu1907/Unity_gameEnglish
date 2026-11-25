# UnityAI

Flask API for A2 English NPC dialogue assessment and ruins-location intent detection.

## Model

Default: **Qwen3-4B-Instruct-2507, ONNX INT4 (CPU)**, approximately 2.57 GB.

- Original: https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507
- Community ONNX export: https://huggingface.co/robinsmits/qwen3-4b-instruct-2507-onnx-int4
- Pinned revision: `97d8690faf0cad28279dd10334a67c79f673a57f`
- `model.onnx.data` SHA256: `0562b2ddc15a9b812496bb709f72402c44fc8fbf2e8bc9f8f8ec25864a1411be`

## Windows / PowerShell

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt -r requirements-download.txt
.\.venv\Scripts\python.exe -X utf8 -m script.download_model
.\.venv\Scripts\python.exe -X utf8 app.py
```

API: `http://127.0.0.1:8000`. Use `-X utf8` to print Vietnamese correctly in Windows terminals.

The existing local `.venv` inherits the Anaconda installation's packages; a fresh
environment created with the commands above is isolated.

## Docker

Download the model first, then run:

```powershell
docker compose -f config/compose.yaml up --build -d
```

Compose mounts the Qwen3 model read-only. No model weights are included in the image.

## Previous model

Previous model files are retained. To select them for a local server:

```powershell
$env:MODEL_DIR = "$PWD/models/qwen-onnx"
.\.venv\Scripts\python.exe -X utf8 app.py
```

Remove the override to use Qwen3 again: `Remove-Item Env:MODEL_DIR`.

## Dialogue assessment

Run commands from `UnityAI`. Shared inference is in `script/model.py`; the exercise
generation/grading branch was removed by the project owner before this refactor.

`POST /grade-answer`:

```json
{"session_id":"client-session","node_id":"ranger_question_02_player_purpose","answer":"I am agent. i am working my task"}
```

The model judges **meaning only** (0–5). Python rules determine grammar (0–5),
errors and the corrected sentence. Unknown grammar errors receive no penalty.
Rules cover be/do agreement, progressive forms, articles before roles, plurals
after numbers, perfect participles, will + base, past markers and he/she/it forms.
See [AI_DIALOGUE_GRADING_SPEC.md](AI_DIALOGUE_GRADING_SPEC.md) for the full contract.

The response retains status, node_id, attempt, attempts_left, passed,
can_award_reward and data. Data retains its existing fields and adds:

- `better_answer`: a model-written learning sample, or null if filtered. It may add details.
- `form_hint_vi`: a target-form learning hint, or null.
- Errors retain quote/explanation_vi/replacement and add rule_id/tense.

`corrected_answer` applies only rule fixes to the original text. Display **Cần sửa**
separately from **Gợi ý câu hay hơn** (`better_answer`). A sample is hidden if it is
empty, unchanged ignoring case, over 30 words, Vietnamese, or violates a known rule.
It is not checked for minimal edits or factual additions.

Grammar is 5/4/3/2 for 0/1/2/3 errors, capped at 3 below min_words. Defaults:
pass total >=6 and meaning >=3; reward total >=8 and meaning >=4.
Only the outer `can_award_reward` authorizes a reward, once per retained
(session_id, node_id). There are three attempts. Empty/Vietnamese/gibberish replies
receive 0/10 and consume an attempt without acquiring the model lock. Inputs over
300 characters or with missing/wrong types return 400. Busy (503),
review_required and server errors consume no attempt. Exhaustion returns 409
with a reference answer.

State is in RAM, retaining 2,000 keys; restart/eviction resets it. Keep one
Gunicorn worker. Session IDs are client-supplied, not authentication.

## Configuration

Each `config/dialogues/X.json` requires `X.grading.json`. Override the directory
with DIALOGUE_DIR if needed. Each question requires expected_intent, expect_hint_vi,
references, and target_forms with formula/accepted_tenses/hint_vi.
strict_form defaults to false and can be overridden per question.

Target mismatches add a hint without an error. In strict mode they also cap grammar
at 4. Tense detection is heuristic. Each ranger question has six meaning-only
few-shot examples: scores 1–5 with two meaning-5 examples containing grammar errors.
Examples never come from the evaluation dataset.

The local irregular verb CSV is loaded at import and included in the Docker image.
Morphology and curated noun/action tables live in script/verbs.py and
script/grammar_rules.py. No NLP packages are needed.

## Hidden location branch

After `ranger_09_next_destination`, Unity may call
`POST /detect-intent` with `{"text":"Where are the ancient ruins?"}`.
The response contains status, intent, target_location, triggered and source.
Rules handle clear cases; ambiguous text uses the shared model. Busy/error
fallback is triggered=false. No grading attempts are consumed. Unity enforces
conversation position; the endpoint does not track it.

## Tests and evaluation

```powershell
# No model loading
.\.venv\Scripts\python.exe -m pip install -r requirements-dev.txt
.\.venv\Scripts\python.exe -B -X utf8 -m pytest -q

# Deterministic dataset tags only, no model loading
.\.venv\Scripts\python.exe -B -X utf8 -m script.eval_dialogue --deterministic --output script/eval_dialogue_deterministic_report.json

# Full evaluation with the real model
.\.venv\Scripts\python.exe -B -X utf8 -m script.eval_dialogue --output script/eval_dialogue_rules_meaning_report.json
```

Optional flags: --case-id (repeatable), --tag (repeatable), --compare-few-shot,
--cases, --model-dir and --output. DISABLE_FEW_SHOT=1 disables inference examples.

The 79-case set includes known gaps. Precheck/rule/no_false_positive tags run
without inference and must pass 100%. For rule/no_false_positive, only grammar/error
labels are assessed with a fake meaning=5; those cases do not claim to measure LLM
meaning. Precheck labels are checked in full. known_gap cases retain their failed
coverage expectations but are excluded from accuracy. Other cases use real inference;
their model-dependent pass rate must meet EVAL_MIN_PASS (default 0.85).
The report includes per-tag results, deterministic failures, model pass rate,
known gaps, false positives, missed errors, review-required counts and timings.

Missing prepositions (working my task), unsupported spelling/word forms
(childrens, comed), some noun-subject agreement and infinitive constructions
(to finding) are documented gaps. Add conservative rules and tests to expand coverage.

Existing eval_single_question_report.json and eval_dialogue_expanded_smoke_report.json
are historical outputs from the previous model-grammar design, not current accuracy.

## Current validation (2026-11-23)

357 pure Python tests pass. The deterministic dataset report passes 23/23 and
all five intent rule checks pass. The full 79-case real-model run includes 7 known
gaps excluded from accuracy. Against the current labels, model-dependent cases
pass 37/49 (75.5%); combined eligible cases pass 60/72 (83.3%). The model gate
therefore fails the 85% threshold. There are nine meaning-label failures and
three review_required results from invalid/empty feedback. No false-positive
grammar flags were observed under the labeled checks.

See script/eval_dialogue_rules_meaning_report.json and
script/eval_dialogue_deterministic_report.json. The full report retains its
original run summary and original failures. Its validation_notes records the
post-run synchronization of two labels (the two-word identity answer and the
agent case's explicitly requested grammar/sample checks), plus refreshed form
hints. No model meaning scores were changed or regenerated. The model run's
average was 34.06 seconds per case, including deterministic cases without inference.
These results establish the rule/meaning separation, not production-ready
meaning assessment accuracy.

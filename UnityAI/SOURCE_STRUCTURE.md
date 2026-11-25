# UnityAI source structure

UnityAI grades contextual A2 English replies to NPCs and detects requests for the ancient ruins location. It uses Qwen3-4B-Instruct-2507 ONNX INT4 through onnxruntime-genai.

## Modules

| File | Responsibility |
| --- | --- |
| app.py | Flask routes, attempt tracking, one-time reward flags and shared model initialization |
| script/model.py | ONNX model loading, ChatML prompts, decoding, JSON parsing and validation retry |
| script/model_gate.py | Exclusive model lock with a request acquisition timeout |
| script/dialogue_loader.py | Load dialogue/settings pairs, validate examples, build question context |
| script/dialogue_grading.py | Meaning-only prompt, prechecks, sample filtering and pure scoring |
| script/rules.py | ASCII folding, language checks and location intent; exports grammar rules |
| script/grammar_rules.py | Ten conservative grammar rule families and heuristic tense detection |
| script/verbs.py | Local CSV loader and small inflection/action tables |
| config/irregular_verbs.csv | Common irregular base/past/participle forms |
| tests/ | Parametrized rule, scoring, contract, configuration and evaluator tests |
| script/dialogue_intent.py | Intent prompt and response validator |
| script/test_dialogue.py | Regression tests using mocked inference |
| script/eval_dialogue.py | Real-model evaluation, category filters and few-shot comparison |
| script/download_model.py | Download the pinned model export |
| config/dialogues/ | NPC dialogue content and per-question grading settings |

## API

- GET /health: model initialization and number of loaded question nodes.
- POST /grade-answer: accepts node_id, session_id and answer.
- POST /detect-intent: accepts text and returns whether it requests the ruins location.
- GET /metrics: Prometheus metrics.

## Grading flow

1. Validate the request and reserve its (session_id, node_id) key against concurrent submissions.
2. Precheck empty, Vietnamese or unusable replies without acquiring the model lock.
3. Acquire ModelGate for up to 30 seconds when inference is needed.
4. Build a prompt with scene facts, three preceding NPC lines, expected intent, references and question-specific examples.
5. Decode meaning-only JSON greedily with a 160-token limit. Retry once if parsing or validation fails.
6. Python runs the grammar registry, retaining at most three position-sorted, non-overlapping fixes.
7. Pure scoring combines model meaning (0-5) with rule grammar (0-5), corrected text, feedback and optional sample/form hint.
8. Update attempts and award permission only after a validated result.

Grammar is exactly 5/4/3/2 for zero/one/two/three detected errors. Short answers are capped at 3. min_words defaults to 3; the identity question uses 2. Target-form mismatch only adds a hint unless strict_form=true (grammar capped at 4). The LLM cannot return grammar or errors.

corrected_answer applies only detected fixes; better_answer is a separate sample that may add details. Samples are hidden if empty, unchanged, too long, Vietnamese or rejected by known grammar rules. UI guidance lives in AI_DIALOGUE_GRADING_SPEC.md.

Default passing requires total >=6 and meaning >=3. Reward eligibility requires total >=8 and meaning >=4. Each question allows three attempts. Busy, review-required and failed requests do not consume attempts. Empty or unusable replies do consume attempts.

Attempt and reward records live in process RAM, retaining up to 2,000 keys. Restart or eviction resets them. Use one Gunicorn worker; session IDs are supplied by the client. Unity must enforce conversation order for the hidden location branch.

## Validation

Run from UnityAI:

```powershell
.\.venv\Scripts\python.exe -B -X utf8 -m pytest -q
.\.venv\Scripts\python.exe -X utf8 -m script.eval_dialogue --compare-few-shot
```

Regression tests never load the model. The evaluator contains 79 cases and rejects prompt/example leakage. Precheck/rule/no_false_positive tags run without inference and must pass 100%; rule tags check only grammar/error labels using fake meaning. known_gap cases are reported but excluded from accuracy. Other cases must meet a model-dependent pass rate of 0.85 (EVAL_MIN_PASS override). See README.md for commands and historical report limitations.

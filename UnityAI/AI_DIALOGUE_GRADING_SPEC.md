# Dialogue grading contract

This specification describes the current NPC dialogue API. The exercise APIs were
removed at the project owner's earlier request and are not restored by this refactor.
Shared inference lives in script/model.py (formerly script/test_model.py).

POST /grade-answer accepts node_id, session_id, answer. Existing response fields:
status, node_id, attempt, attempts_left, passed, can_award_reward, data.
Data includes original_answer, errors, scores (meaning and grammar),
corrected_answer, feedback_vi, total_score, max_score, passed, reward_eligible,
rule_override. Errors include quote, explanation_vi, replacement.

The target design assigns grammar entirely to conservative Python rules and meaning
only to the LLM. Unknown grammar errors receive no penalty. Passing requires total
at least pass_total and meaning >=3; reward eligibility requires reward_total and
meaning >=4. Defaults are 6, 8 and three attempts. Precheck rejects unusable input
without inference. Busy, review_required and server failures consume no attempt.

Model validation retries once; startup must reject malformed grading configuration.
Session/attempt/reward records are process-local. Use one server worker.

## Unity response contract

The request, outer response and existing data fields remain unchanged. Data adds
optional better_answer (string or null) and form_hint_vi (string or null).
Each error retains quote, explanation_vi, replacement and adds rule_id and tense.
No form mismatch is reported as an error. With strict_form=false it has no score
effect; with strict_form=true grammar is capped at 4 for a target-form mismatch.

The UI should show **Cần sửa** (errors and corrected_answer) separately from
**Gợi ý câu hay hơn** (better_answer). The former applies only detected rule fixes
to the player's text. The latter is a learning sample and may add a small detail.
Hide the sample section when better_answer is null. Display form_hint_vi as a
separate learning hint. Continue to award using outer can_award_reward, not the
answer-quality flag data.reward_eligible.

The LLM returns only meaning (integer 0-5), feedback_vi (nonempty Vietnamese
meaning feedback), and optional better_answer (string). It cannot supply errors
or grammar scores. Python validates this schema and retries once on failure.

Grammar is 5/4/3/2 for 0/1/2/3 detected errors, capped at 3 below min_words.
The feedback joins meaning feedback, the first fixed rule explanation prefixed
with Cần sửa, and length/meaning notes. Corrected text is replaced right to left.
Precheck results remain 0/10 and include both new fields as null.

## Rule and configuration contract

Rules are frozen registry entries with id, tense, find and a fixed Vietnamese
explanation of the correct rule. Return at most three errors, sorted by position,
with exact original quotes and no overlapping spans. Uncovered errors do not
reduce grammar; this is intentional. Invariant verb forms, noun compounds and
ambiguous words are skipped conservatively. Missing prepositions (such as
working my task) are a known gap.

Each question requires target_forms with nonempty formula, accepted_tenses and
hint_vi. Accepted tense names are present_simple, present_continuous, past_simple,
present_perfect and future_simple. strict_form defaults to false and can be
overridden per question. Startup rejects malformed targets, missing question
settings and malformed few-shot outputs. Tense detection is a surface heuristic,
not a parser; hints are nonbinding by default.

## Testing and evaluation

Steps 1-7 use pure Python tests with mocked inference. The evaluator uses a
separate labeled dataset and rejects overlap with inference examples. Deterministic
precheck/rule/no_false_positive tags must pass 100%; known_gap cases are reported
but excluded from accuracy. Other tags use EVAL_MIN_PASS (default 0.85).
Historical reports predate this architecture and do not measure current accuracy.

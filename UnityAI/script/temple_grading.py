"""Temple score adapter: model meaning plus authored, mandatory exercise requirements."""
import re

from script.dialogue_grading import (GRADING_SYSTEM_DIALOGUE, build_user_prompt,
                                    finalize_dialogue_grade, validate_dialogue_grade, zero_result)
from script.dialogue_grading import apply_fixes
from script.model import OutputError
from script.rules import rule_errors
from script.temple_report import grade_report, split_sentences


def normalize(text):
    return ' '.join(re.sub(r'[^\w\s]', '', text.lower()).split())


def check_local(exercise, answer):
    evaluation = exercise['evaluation']
    normalized = normalize(answer)
    if evaluation['method'] == 'keyword_any':
        return any(re.search(r'\b' + re.escape(word) + r'\b', normalized)
                   for word in evaluation['any_required_tokens'])
    if '?' in answer or len(split_sentences(answer)) != 1:
        return False
    rules = evaluation['rule_fallback']
    tokens = set(normalized.split())
    if any(normalize(word) in tokens for word in rules.get('forbidden_tokens', [])):
        return False
    if normalized in {normalize(value) for value in exercise['accepted_answers']}:
        return True
    return (all(word in tokens for word in rules['all_required_tokens'])
            and re.fullmatch(rules['full_match_pattern'], normalized) is not None)


def score_report(ctx, answer, result):
    data = result.get('data')
    if data is None:
        return zero_result(answer, result['feedback_vi'])
    checks = data['sentence_checks']
    roles = sum(check['role_met'] for check in checks)
    meaning = 0 if data['relevance'] == 'off' else min(2, roles) if data['relevance'] == 'scene' else min(5, roles + 2)
    if not data['meaning_clear'] or data['plot_contradictions']:
        meaning = min(meaning, 2)
    errors = rule_errors(answer)
    sentences = split_sentences(answer)
    grammar_notes = []
    # A model may detect a tense/form issue outside the conservative rule registry.
    # Keep those findings visible, and do not charge again for a rule fix in the same sentence.
    model_issue_sentences = set()
    grammar_codes = {'grammar', 'article', 'punctuation', 'minor_article', 'minor_punctuation',
                     'verb_form', 'agreement', 'subject_verb_agreement', 'missing_aux',
                     'past_simple', 'present_perfect', 'future_simple', 'tense', 'word_order'}
    for error in data['errors']:
        grammar_notes.append(f"Câu {error['sentence_number']}: {error['message_vi']}")
        if error['code'].lower() in grammar_codes or re.search(
                r'grammar|tense|agreement|aux|participle|article|punctuation|verb|plural', error['code'], re.I):
            model_issue_sentences.add(error['sentence_number'])
    missing_tenses = set()
    for check in checks:
        if not check['required_tense_met']:
            number = check['sentence_number']
            missing_tenses.add(number)
            grammar_notes.append(f"Câu {number}: chưa dùng đúng thì bắt buộc. " +
                                 (check['feedback_vi'].strip() or ctx.target_forms['hint_vi']))
    rule_sentences = {i + 1 for i, sentence in enumerate(sentences)
                      if any(error['quote'] in sentence for error in errors)}
    additional_issues = (model_issue_sentences | missing_tenses) - rule_sentences
    grammar = max(0, 5 - len(errors) - len(additional_issues))
    return dict(relevance=data['relevance'], original_answer=answer, corrected_answer=apply_fixes(answer, errors),
                errors=errors, scores={'meaning': meaning, 'grammar': grammar}, total_score=meaning + grammar,
                max_score=10, passed=result['passed'] and meaning + grammar >= ctx.pass_total,
                reward_eligible=False, rule_override=bool(errors or additional_issues), better_answer=None,
                grammar_notes=grammar_notes, grammar_not_assessed=False,
                form_hint_vi=ctx.target_forms['hint_vi'] if missing_tenses else None,
                feedback_vi=result['feedback_vi'], report_checks=data)


def grade_exercise(ai, ctx, answer):
    exercise = ctx.exercise
    if exercise['evaluation']['method'] == 'rules_then_model':
        result = grade_report(ai, answer, ctx)
        if result['status'] != 'validated':
            return result['status'], None
        return 'validated', score_report(ctx, answer, result)

    def validator(data):
        try:
            validate_dialogue_grade(data, answer)
        except ValueError as error:
            raise OutputError(str(error)) from error

    system = GRADING_SYSTEM_DIALOGUE + (
        '\nThis is an authored temple EXERCISE. Judge meaning against its task and reading text. '
        'A correct transformation or a supported short answer about the portal/crater is story-related. '
        'Do not judge grammar: Python separately enforces the mandatory form. '
        'For keyword_any, the whole word portal OR crater fully answers the question, even without a full sentence.')
    prompt = build_user_prompt(ctx, answer) + '\nExercise: ' + exercise['prompt_en']
    if exercise.get('reading_text_en'):
        prompt += '\nReading text: ' + exercise['reading_text_en']
    result = ai.request_json(system, prompt, validator, max_new_tokens=200, sample=False)
    if result['status'] != 'validated' or result.get('data') is None:
        return 'review_required', None
    model = result['data']
    reading = exercise['evaluation']['method'] == 'keyword_any'
    grade = finalize_dialogue_grade(ctx, answer, model['relevance'], model['meaning'], model['feedback_vi'],
                                    model.get('better_answer'), [] if reading else rule_errors(answer))
    if reading:
        grade['scores']['grammar'] = 5
        grade['total_score'] = grade['scores']['meaning'] + 5
        grade['form_hint_vi'] = None
        grade['grammar_not_assessed'] = True
        grade['feedback_vi'] += ' Bài đọc hiểu này không trừ điểm ngữ pháp; chỉ cần từ portal hoặc crater đúng theo bản khắc.'
    meets_task = check_local(exercise, answer)
    grade['passed'] = meets_task and grade['scores']['meaning'] >= 3 and grade['total_score'] >= ctx.pass_total
    grade['reward_eligible'] = False
    grade['exercise_requirement_met'] = meets_task
    if not meets_task:
        grade['feedback_vi'] += ' ' + ctx.expect_hint_vi
        grade['grammar_notes'].append('Chưa đáp ứng yêu cầu bài tập: ' + ctx.expect_hint_vi)
    return 'validated', grade

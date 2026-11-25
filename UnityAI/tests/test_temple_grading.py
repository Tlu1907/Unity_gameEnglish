from unittest.mock import Mock

import pytest

from script.dialogue_loader import DialogueStore
from script.dialogue_grading import grade_with_model
from script.temple_grading import check_local


@pytest.fixture
def store():
    return DialogueStore()


def meaning_model(meaning=5, relevance='story'):
    model = Mock()
    model.request_json.return_value = {'status': 'validated', 'data': {
        'relevance': relevance, 'meaning': meaning, 'feedback_vi': 'Ý trả lời rõ ràng.'}}
    return model


def test_dialogue_pair_loads_all_temple_questions_and_keeps_ranger_settings(store):
    assert len(store.by_node) == 7
    for node in ('temple_03_first_stone', 'temple_05_second_stone', 'temple_07_third_stone'):
        assert store.get(node).max_attempts == 2
        assert store.get(node).exercise
    report = store.get('temple_09_report')
    assert report.max_attempts is None and report.min_words == 0
    assert len(report.known_facts) == 7
    assert store.get('ranger_question_02_player_purpose').max_attempts == 3
    assert store.get('ranger_question_02_player_purpose').exercise is None


@pytest.mark.parametrize('node,answer,passed', [
    ('temple_03_first_stone', 'The guardians closed the portal.', True),
    ('temple_03_first_stone', 'The guardians have closed the portal.', False),
    ('temple_03_first_stone', 'Did the guardians closed the portal?', False),
    ('temple_03_first_stone', 'The guardians did not close the portal.', False),
    ('temple_03_first_stone', 'guardians portal closed', False),
    ('temple_05_second_stone', 'The creatures have destroyed many trees.', True),
    ('temple_05_second_stone', 'The creatures has destroyed many trees.', False),
    ('temple_05_second_stone', 'The creatures destroyed many trees.', False),
    ('temple_07_third_stone', 'portal', True),
    ('temple_07_third_stone', 'The crater.', True),
    ('temple_07_third_stone', 'portals', False),
])
def test_model_scores_cannot_bypass_exercise_requirements(store, node, answer, passed):
    ai = meaning_model()
    status, data = grade_with_model(ai, store.get(node), answer)
    assert status == 'validated' and data['passed'] is passed
    assert data['max_score'] == 10 and not data['reward_eligible']
    ai.request_json.assert_called_once()


def test_reading_does_not_penalize_grammar_or_short_answers(store):
    _, data = grade_with_model(meaning_model(), store.get('temple_07_third_stone'), 'portal')
    assert data['scores'] == {'meaning': 5, 'grammar': 5}
    assert data['errors'] == []


def test_report_scoring_uses_semantic_roles_and_has_no_attempt_cap(store):
    ctx = store.get('temple_09_report')
    ai = Mock()
    verdict = {'relevance': 'story', 'meaning_clear': True, 'plot_contradictions': [], 'errors': [],
               'feedback_vi': 'Đủ ba ý.', 'sentence_checks': [
                   {'sentence_number': i, 'role_met': True, 'required_tense_met': True, 'feedback_vi': ''} for i in (1, 2, 3)]}
    ai.request_json.return_value = {'status': 'validated', 'data': verdict}
    status, grade = grade_with_model(ai, ctx, ctx.references[0])
    assert status == 'validated' and grade['passed'] and grade['total_score'] == 10
    assert ctx.max_attempts is None
    verdict['sentence_checks'][2]['role_met'] = False
    assert not grade_with_model(ai, ctx, ctx.references[0])[1]['passed']


def test_bad_sentence_count_never_calls_model(store):
    ai = Mock()
    status, data = grade_with_model(ai, store.get('temple_09_report'), 'I went to the temple.')
    assert status == 'validated' and not data['passed'] and data['total_score'] == 0
    ai.request_json.assert_not_called()


def test_report_model_tense_finding_is_visible_and_reduces_grammar(store):
    ctx = store.get('temple_09_report')
    ai = Mock()
    verdict = {'relevance': 'story', 'meaning_clear': True, 'plot_contradictions': [], 'errors': [],
               'feedback_vi': 'Sửa thì của câu thứ hai.', 'sentence_checks': [
                   {'sentence_number': i, 'role_met': True, 'required_tense_met': i != 2, 'feedback_vi': 'Cần hiện tại hoàn thành.' if i == 2 else ''} for i in (1, 2, 3)]}
    ai.request_json.return_value = {'status': 'validated', 'data': verdict}
    answer = 'I saw a blue light. The creatures destroyed many trees. I will go to the crater and close the portal.'
    _, grade = grade_with_model(ai, ctx, answer)
    assert grade['errors'] == []
    assert grade['scores']['grammar'] == 4
    assert any('Câu 2' in note for note in grade['grammar_notes'])
    assert not grade['passed']


def test_report_model_grammar_error_is_not_lost_or_double_counted(store):
    ctx = store.get('temple_09_report')
    ai = Mock()
    verdict = {'relevance': 'story', 'meaning_clear': True, 'plot_contradictions': [],
               'errors': [{'sentence_number': 2, 'code': 'wrong_participle', 'message_vi': 'Sau have cần destroyed.'}],
               'feedback_vi': 'Câu thứ hai cần sửa.', 'sentence_checks': [
                   {'sentence_number': i, 'role_met': True, 'required_tense_met': i != 2, 'feedback_vi': ''} for i in (1, 2, 3)]}
    ai.request_json.return_value = {'status': 'validated', 'data': verdict}
    answer = 'I saw a blue light. The creatures have destroy many trees. I will go to the crater and close the portal.'
    _, grade = grade_with_model(ai, ctx, answer)
    assert grade['scores']['grammar'] == 4
    assert grade['errors'] and grade['grammar_notes']
    assert 'have destroyed' in grade['corrected_answer']
    assert not grade['passed']


def test_busy_or_invalid_model_output_has_no_score(store):
    ai = Mock()
    ai.request_json.return_value = {'status': 'review_required', 'data': None}
    assert grade_with_model(ai, store.get('temple_03_first_stone'), 'The guardians closed the portal.') == ('review_required', None)

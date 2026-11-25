from copy import deepcopy
from unittest.mock import Mock
from contextlib import contextmanager
import importlib.util
from pathlib import Path

import pytest

from script.model import OutputError
from script.temple_report import (finalize_report, grade_report, load_task,
                                 report_precheck, split_sentences, validate_report)


@pytest.fixture
def verdict():
    return {'relevance': 'story', 'meaning_clear': True, 'plot_contradictions': [], 'errors': [],
            'feedback_vi': 'Bạn đã báo cáo đủ ba ý.',
            'sentence_checks': [{'sentence_number': i, 'role_met': True,
                                 'required_tense_met': True, 'feedback_vi': ''} for i in (1, 2, 3)]}


def test_temple_reference_and_alternative_are_three_sentences():
    content, task = load_task()
    assert len(content['exercises']) == 4
    for key in ('reference_answer_en', 'alternative_example_en'):
        assert len(split_sentences(task[key])) == 3
        assert report_precheck(task[key]) is None


@pytest.mark.parametrize('text,count', [
    ('I saw light. They have hurt trees. I will close the portal', 3),
    ('I saw light... They have hurt trees! I will go?', 3),
    ('I saw light\nThey have hurt trees\nI will go', 1),
    ('Dr. Alex saw 2.5 marks. They have hurt trees. I will go.', 3),
])
def test_sentence_count(text, count):
    assert len(split_sentences(text)) == count


@pytest.mark.parametrize('text', ['', 'One sentence.', 'One. Two. Three. Four.', 'Tôi đi đến đền. Tôi thấy ánh sáng. Tôi đóng cổng.'])
def test_prechecks_skip_model(text):
    ai = Mock()
    result = grade_report(ai, text)
    assert result['status'] == 'validated' and not result['passed']
    ai.request_json.assert_not_called()


def test_success_uses_validated_roles_not_model_pass_flag(verdict):
    assert finalize_report(verdict)['passed']
    with pytest.raises(OutputError):
        validate_report({**verdict, 'passed': True})


@pytest.mark.parametrize('field,value', [('relevance', 'scene'), ('relevance', 'off'),
                                       ('meaning_clear', False), ('plot_contradictions', ['Cổng không nằm ở đền.'])])
def test_pass_requires_every_condition(verdict, field, value):
    verdict[field] = value
    assert not finalize_report(verdict)['passed']


@pytest.mark.parametrize('field', ['role_met', 'required_tense_met'])
def test_all_sentences_must_meet_role_and_tense(verdict, field):
    verdict['sentence_checks'][1][field] = False
    assert not finalize_report(verdict)['passed']


@pytest.mark.parametrize('bad', ['boolean', 'duplicate', 'missing', 'invalid_error'])
def test_malformed_verdict_is_not_scored(verdict, bad):
    data = deepcopy(verdict)
    if bad == 'boolean': data['sentence_checks'][0]['role_met'] = 1
    if bad == 'duplicate': data['sentence_checks'][2]['sentence_number'] = 1
    if bad == 'missing': del data['meaning_clear']
    if bad == 'invalid_error': data['errors'] = [{'sentence_number': True, 'code': 'x', 'message_vi': 'x'}]
    with pytest.raises(OutputError): validate_report(data)


def test_model_receives_evidence_and_report_contract(verdict):
    _, task = load_task()
    ai = Mock()
    ai.request_json.return_value = {'status': 'validated', 'data': verdict}
    result = grade_report(ai, task['alternative_example_en'])
    assert result['passed']
    payload = ai.request_json.call_args.args[1]
    assert payload['known_facts'] and len(payload['answer_outline']) == 3
    ai.request_json.return_value = {'status': 'review_required', 'data': None}
    assert grade_report(ai, task['reference_answer_en'])['status'] == 'review_required'


@pytest.fixture
def api(monkeypatch):
    import script.model
    model = Mock()
    monkeypatch.setattr(script.model, 'EnglishModel', lambda directory: model)
    path = Path(__file__).resolve().parents[1] / 'app.py'
    spec = importlib.util.spec_from_file_location('temple_test_app', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.app.test_client(), module, model


def test_api_report_success_and_precheck_do_not_load_real_model(api, verdict):
    client, module, model = api
    response = client.post('/grade-temple-report', json={'node_id': 'temple_09_report', 'answer': 'Only one sentence.'})
    assert response.status_code == 200 and not response.json['passed']
    model.request_json.assert_not_called()
    model.request_json.return_value = {'status': 'validated', 'data': verdict}
    _, task = load_task()
    response = client.post('/grade-temple-report', json={'node_id': 'temple_09_report', 'answer': task['reference_answer_en']})
    assert response.status_code == 200 and response.json['passed']
    assert 'can_award_reward' not in response.json


@pytest.mark.parametrize('payload', [None, {}, {'node_id': 'ranger_question_01_player_identity', 'answer': 'Hi.'},
                                     {'node_id': 'temple_09_report', 'answer': 'x' * 1501}])
def test_api_rejects_invalid_request(api, payload):
    client, _, model = api
    assert client.post('/grade-temple-report', json=payload).status_code == 400
    model.request_json.assert_not_called()


def test_api_busy_and_invalid_results_do_not_mark_report_wrong(api):
    client, module, model = api
    _, task = load_task()
    body = {'node_id': 'temple_09_report', 'answer': task['reference_answer_en']}
    @contextmanager
    def unavailable(**kwargs):
        yield False
    module.gate.use = unavailable
    response = client.post('/grade-temple-report', json=body)
    assert response.status_code == 503 and response.json['status'] == 'busy'
    model.request_json.assert_not_called()


@pytest.mark.parametrize('node', ['temple_03_first_stone', 'temple_05_second_stone', 'temple_07_third_stone'])
def test_shared_grade_api_limits_stones_to_two_attempts(api, node):
    client, _, model = api
    model.request_json.return_value = {'status': 'validated', 'data': {
        'relevance': 'story', 'meaning': 5, 'feedback_vi': 'Cần làm đúng bài.'}}
    body = {'node_id': node, 'session_id': 'temple-limited', 'answer': 'This is the wrong answer.'}
    for remaining in (1, 0):
        response = client.post('/grade-answer', json=body)
        assert response.status_code == 200 and not response.json['passed']
        assert response.json['attempts_left'] == remaining
    response = client.post('/grade-answer', json=body)
    assert response.status_code == 409 and response.json['status'] == 'exhausted'
    model.request_json.assert_called()


def test_shared_grade_api_allows_report_revisions_until_pass(api, verdict):
    client, _, model = api
    model.request_json.return_value = {'status': 'validated', 'data': verdict}
    verdict['sentence_checks'][2]['role_met'] = False
    _, task = load_task()
    body = {'node_id': 'temple_09_report', 'session_id': 'temple-unlimited', 'answer': task['reference_answer_en']}
    for attempt in range(1, 5):
        response = client.post('/grade-answer', json=body)
        assert response.status_code == 200 and not response.json['passed']
        assert response.json['attempt'] == attempt
        assert response.json['unlimited_attempts'] and response.json['attempts_left'] == -1
    verdict['sentence_checks'][2]['role_met'] = True
    response = client.post('/grade-answer', json=body)
    assert response.json['passed'] and response.json['data']['total_score'] == 10


def test_shared_grade_api_does_not_consume_temple_attempt_for_service_failure_or_empty_input(api):
    client, module, model = api
    body = {'node_id': 'temple_03_first_stone', 'session_id': 'temple-failure', 'answer': ''}
    assert client.post('/grade-answer', json=body).status_code == 400
    body['answer'] = 'The guardians closed the portal.'
    model.request_json.return_value = {'status': 'review_required', 'data': None}
    assert client.post('/grade-answer', json=body).json['status'] == 'review_required'
    assert not module.attempts
    model.request_json.return_value = {'status': 'validated', 'data': {
        'relevance': 'story', 'meaning': 5, 'feedback_vi': 'Đúng nội dung.'}}
    response = client.post('/grade-answer', json=body)
    assert response.json['attempt'] == 1 and response.json['passed']


def test_ranger_api_keeps_both_user_reported_corrections_in_response(api):
    client, _, model = api
    model.request_json.return_value = {'status': 'validated', 'data': {
        'relevance': 'story', 'meaning': 5, 'feedback_vi': 'Bạn đang nói về sinh vật.'}}
    response = client.post('/grade-answer', json={
        'node_id': 'ranger_question_02_player_purpose', 'session_id': 'grammar-regression',
        'answer': 'The creatures have destroy animal'})
    assert response.status_code == 200
    data = response.json['data']
    assert data['scores']['grammar'] == 3
    assert len(data['errors']) == 2
    assert data['corrected_answer'] == 'The creatures have destroyed an animal'

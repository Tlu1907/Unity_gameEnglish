import json
from unittest.mock import Mock
import pytest
from script.dialogue_grading import validate_dialogue_grade, grade_with_model, GRADING_SYSTEM_DIALOGUE
from script.dialogue_loader import DialogueStore
from script.rules import rule_errors


@pytest.mark.parametrize('value', [
    {}, {'meaning': True, 'feedback_vi': 'ok'}, {'meaning': 6, 'feedback_vi': 'ok'},
    {'meaning': 2, 'feedback_vi': ''}, {'meaning': 2.5, 'feedback_vi': 'ok'},
    {'meaning': 2, 'feedback_vi': 'ok', 'errors': []},
    {'meaning': 2, 'feedback_vi': 'ok', 'grammar': 5},
    {'meaning': 2, 'feedback_vi': 'ok', 'better_answer': None},
])
def test_invalid_meaning_schema(value):
    with pytest.raises(ValueError): validate_dialogue_grade(value)


@pytest.mark.parametrize('better', [{}, {'better_answer': ''}, {'better_answer': 'I am an explorer.'}])
def test_optional_sample(better):
    validate_dialogue_grade({'meaning': 5, 'feedback_vi': 'Bạn nói rõ mục đích.', **better})


def test_model_cannot_set_grammar():
    ctx = DialogueStore().get('ranger_question_01_player_identity')
    ai = Mock()
    ai.request_json.return_value = {'status': 'validated', 'data': {
        'meaning': 5, 'feedback_vi': 'Bạn giới thiệu rõ vai trò.', 'better_answer': 'I am a wizard from the forest.'}}
    status, data = grade_with_model(ai, ctx, 'I are a wizard.')
    assert status == 'validated'
    assert data['errors'] == rule_errors('I are a wizard.')
    assert data['scores'] == {'meaning': 5, 'grammar': 4}
    assert data['corrected_answer'] == 'I am a wizard.'
    assert ai.request_json.call_args.kwargs['max_new_tokens'] == 160
    assert 'Score only MEANING' in GRADING_SYSTEM_DIALOGUE


def test_few_shots_are_meaning_only():
    for ctx in DialogueStore().by_node.values():
        assert {1, 2, 3, 4, 5} <= {s['output']['meaning'] for s in ctx.few_shot}
        assert sum(s['output']['meaning'] == 5 and bool(rule_errors(s['answer'])) for s in ctx.few_shot) >= 2
        assert all('ngữ pháp' not in s['output']['feedback_vi'].lower() for s in ctx.few_shot)

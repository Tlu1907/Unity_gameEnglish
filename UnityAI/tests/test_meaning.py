import json
from unittest.mock import Mock
import pytest
from script.dialogue_grading import validate_dialogue_grade, grade_with_model, GRADING_SYSTEM_DIALOGUE
from script.dialogue_loader import DialogueStore
from script.rules import rule_errors


VALID = {'relevance': 'story', 'meaning': 2, 'feedback_vi': 'ok'}


@pytest.mark.parametrize('value', [
    {}, {'meaning': 2, 'feedback_vi': 'ok'},                      # missing relevance
    {'relevance': 'story', 'feedback_vi': 'ok'},                  # missing meaning
    {**VALID, 'relevance': 'sideways'}, {**VALID, 'relevance': None}, {**VALID, 'relevance': True},
    {**VALID, 'meaning': True}, {**VALID, 'meaning': 6}, {**VALID, 'meaning': -1}, {**VALID, 'meaning': 2.5},
    {**VALID, 'feedback_vi': ''}, {**VALID, 'feedback_vi': '   '}, {**VALID, 'feedback_vi': None},
    {**VALID, 'errors': []},
    {**VALID, 'grammar': 5},
    {**VALID, 'better_answer': None},
])
def test_invalid_meaning_schema(value):
    with pytest.raises(ValueError): validate_dialogue_grade(value)


@pytest.mark.parametrize('relevance', ['story', 'scene', 'off'])
@pytest.mark.parametrize('better', [{}, {'better_answer': ''}, {'better_answer': 'I am an explorer.'}])
def test_optional_sample(relevance, better):
    validate_dialogue_grade({'relevance': relevance, 'meaning': 5, 'feedback_vi': 'Bạn nói rõ mục đích.', **better})


def test_model_cannot_set_grammar():
    ctx = DialogueStore().get('ranger_question_01_player_identity')
    ai = Mock()
    ai.request_json.return_value = {'status': 'validated', 'data': {
        'relevance': 'story', 'meaning': 5, 'feedback_vi': 'Bạn giới thiệu rõ vai trò.',
        'better_answer': 'I am a wizard from the forest.'}}
    status, data = grade_with_model(ai, ctx, 'I are a wizard.')
    assert status == 'validated'
    assert data['errors'] == rule_errors('I are a wizard.')
    assert data['scores'] == {'meaning': 5, 'grammar': 4}
    assert data['corrected_answer'] == 'I am a wizard.'
    assert data['relevance'] == 'story'
    assert ai.request_json.call_args.kwargs['max_new_tokens'] == 160
    assert ai.request_json.call_args.kwargs['sample'] is False
    # The prompt tells the model to leave grammar to Python.
    assert 'Do NOT judge or mention grammar' in GRADING_SYSTEM_DIALOGUE


def test_model_output_with_grammar_or_errors_is_rejected_by_validator():
    for extra in ({'grammar': 5}, {'errors': []}, {'scores': {'grammar': 5}}):
        with pytest.raises(ValueError):
            validate_dialogue_grade({**VALID, **extra})


def test_few_shots_are_meaning_only():
    for ctx in DialogueStore().by_node.values():
        rels = {s['output']['relevance'] for s in ctx.few_shot}
        assert {'story', 'off'} <= rels, ctx.node_id
        if ctx.scene_cap is not None:
            assert 'scene' in rels, ctx.node_id
        for s in ctx.few_shot:
            out = s['output']
            if out['relevance'] == 'off':
                assert out['meaning'] == 0
            if out['relevance'] == 'scene':
                assert 1 <= out['meaning'] <= (ctx.scene_cap or 5)
            if out['relevance'] == 'story':
                assert out['meaning'] >= 1
            assert 'ngữ pháp' not in out['feedback_vi'].lower()
        assert any(s['output']['meaning'] == 5 for s in ctx.few_shot), ctx.node_id
    # Ranger questions teach that a grammar mistake must not lower the meaning score.
    for node_id, ctx in DialogueStore().by_node.items():
        if node_id.startswith('ranger_'):
            assert any(s['output']['meaning'] == 5 and bool(rule_errors(s['answer'])) for s in ctx.few_shot), node_id
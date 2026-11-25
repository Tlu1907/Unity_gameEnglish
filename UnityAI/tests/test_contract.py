import json
from pathlib import Path
from unittest.mock import patch
import pytest
from script.dialogue_loader import DialogueStore
from script.dialogue_grading import finalize_dialogue_grade
from script.rules import rule_errors


def test_dialogue_response_optional_fields():
    with patch('script.model.EnglishModel'):
        import app
    app.attempts.clear()
    node = 'ranger_question_02_player_purpose'
    ctx = app.dialogues.get(node)
    data = finalize_dialogue_grade(ctx, 'I am agent.', 5, 'Bạn nêu rõ vai trò.',
                                   'I am an agent exploring this forest.', rule_errors('I am agent.'))
    with patch.object(app, 'grade_with_model', return_value=('validated', data)):
        response = app.app.test_client().post('/grade-answer', json={
            'node_id': node, 'session_id': 'contract-test', 'answer': 'I am agent.'})
    assert response.status_code == 200
    assert {'status', 'node_id', 'attempt', 'attempts_left', 'passed', 'can_award_reward', 'data'} <= response.json.keys()
    result = response.json['data']
    assert {'original_answer', 'errors', 'scores', 'corrected_answer', 'feedback_vi', 'total_score',
            'max_score', 'passed', 'reward_eligible', 'rule_override', 'better_answer', 'form_hint_vi'} <= result.keys()
    assert set(result['scores']) == {'meaning', 'grammar'}
    assert result['errors'][0]['rule_id'] == 'article.role_noun'


@pytest.mark.parametrize('bad', [None, {}, [], {'answer': '', 'output': {}},
                               {'answer': 'I am a guide.', 'output': {'meaning': True, 'feedback_vi': 'ok'}}])
def test_startup_rejects_bad_examples(bad):
    root = Path('config/dialogues')
    dlg = json.loads((root/'map2_forest_ranger.json').read_text(encoding='utf-8'))
    cfg = json.loads((root/'map2_forest_ranger.grading.json').read_text(encoding='utf-8'))
    cfg['questions']['ranger_question_01_player_identity']['few_shot'] = [bad]
    store = DialogueStore.__new__(DialogueStore)
    store.by_node, store.dialogues = {}, {}
    with pytest.raises(ValueError, match='few_shot'):
        store._load(dlg, cfg)

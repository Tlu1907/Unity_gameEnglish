import pytest
from script.rules import detect_tenses
from script.dialogue_loader import DialogueStore


@pytest.mark.parametrize('answer,expected', [
    ('I am looking for a dog.', {'present_continuous'}),
    ('I have found it.', {'present_perfect'}),
    ('I will go home.', {'future_simple'}),
    ('I went home.', {'past_simple'}),
    ('I walked home.', {'past_simple'}),
    ('I am a wizard.', {'present_simple'}),
    ('I need to help the animals.', {'present_simple'}),
    ('I know they came from a cave.', {'present_simple', 'past_simple'}),
    ('I went home and I am looking for him.', {'past_simple', 'present_continuous'}),
    ("I'm exploring.", {'present_continuous'}),
    ('I have seen a cursed seed.', {'present_perfect'}),
])
def test_detection(answer, expected):
    assert detect_tenses(answer) == expected


@pytest.mark.parametrize('bad', [None, {}, {'formula': 'I am', 'accepted_tenses': [], 'hint_vi': 'hint'},
                                {'formula': 'I am', 'accepted_tenses': ['invented'], 'hint_vi': 'hint'}])
def test_missing_target_fields_fail(bad):
    import json
    from pathlib import Path
    folder = Path('config/dialogues')
    dlg = json.loads((folder/'map2_forest_ranger.json').read_text(encoding='utf-8'))
    cfg = json.loads((folder/'map2_forest_ranger.grading.json').read_text(encoding='utf-8'))
    cfg['questions']['ranger_question_01_player_identity']['target_forms'] = bad
    store = DialogueStore.__new__(DialogueStore)
    store.dialogues, store.by_node = {}, {}
    with pytest.raises(ValueError, match='target_forms'):
        store._load(dlg, cfg)

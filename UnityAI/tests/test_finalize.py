from dataclasses import replace
from types import SimpleNamespace
import pytest
from script.dialogue_loader import DialogueStore
from script.dialogue_grading import finalize_dialogue_grade, apply_fixes, zero_result, check_better_answer, validate_dialogue_grade
from script.rules import rule_errors


@pytest.fixture
def ctx():
    return DialogueStore().get('ranger_question_02_player_purpose')


def finish(ctx, answer, meaning=5, better=None, relevance="story"):
    return finalize_dialogue_grade(ctx, answer, relevance, meaning, 'Ý của bạn rõ ràng.', better, rule_errors(answer))


@pytest.mark.parametrize('answer,n,grammar', [
    ('I am looking for my dog.', 0, 5),
    ('I are looking for my dog.', 1, 4),
    ('I are looking for two monster.', 2, 3),
    ("I are looking for two monster. He don't know.", 3, 2),
])
def test_error_mapping(ctx, answer, n, grammar):
    result = finish(ctx, answer)
    assert len(result['errors']) == n
    assert result['scores']['grammar'] == grammar
    assert result['rule_override'] == bool(n)
    assert ('Cần sửa:' in result['feedback_vi']) == bool(n)


def test_min_words(ctx):
    result = finish(ctx, 'Exploring.')
    assert result['scores']['grammar'] == 3
    assert 'đầy đủ hơn' in result['feedback_vi']


def test_short_identity_contraction_is_long_enough():
    identity = DialogueStore().get('ranger_question_01_player_identity')
    assert finish(identity, "I'm Alex.")['scores']['grammar'] == 5


@pytest.mark.parametrize('strict,grammar', [(False, 5), (True, 4)])
def test_strict_form(ctx, strict, grammar):
    result = finish(replace(ctx, strict_form=strict), 'I went to help the animals.')
    assert result['scores']['grammar'] == grammar
    assert result['errors'] == []
    assert result['form_hint_vi'] == ctx.target_forms['hint_vi']


@pytest.mark.parametrize('meaning,passed,reward', [(2, False, False), (3, True, False), (4, True, True), (5, True, True)])
def test_meaning_thresholds(ctx, meaning, passed, reward):
    result = finish(ctx, 'I am exploring the forest.', meaning)
    assert result['passed'] is passed
    assert result['reward_eligible'] is reward
    if meaning <= 2: assert ctx.expect_hint_vi in result['feedback_vi']


def test_custom_total_thresholds(ctx):
    hard = replace(ctx, pass_total=9, reward_total=10)
    result = finish(hard, 'I are exploring the forest.', 4)
    assert result['total_score'] == 8
    assert not result['passed'] and not result['reward_eligible']


def test_fixes_repeated_quotes(ctx):
    answer = 'I are here. I are lost.'
    errors = rule_errors(answer)
    assert apply_fixes(answer, errors) == 'I am here. I am lost.'
    result = finish(ctx, 'I am agent. i am working my task')
    assert result['corrected_answer'] == 'I am an agent. i am working my task'
    assert 'Danh từ đếm được số ít' in result['feedback_vi']


def test_fixes_the_erroneous_occurrence_only(ctx):
    answer = 'I go home daily. Yesterday I go home.'
    assert finish(ctx, answer)['corrected_answer'] == 'I go home daily. Yesterday I went home.'


def test_zero_result_shape():
    result = zero_result('', 'Nhập câu trả lời.')
    assert result['better_answer'] is None
    assert result['form_hint_vi'] is None
    assert result['total_score'] == 0


@pytest.mark.parametrize('better', [None, '', ' I AM EXPLORING. ', 'word ' * 31,
                                  'Tôi đang tìm một con chó.', 'I are a wizard.'])
def test_better_filter(better):
    assert check_better_answer(better, 'I am exploring.') is None


def test_better_can_add_ideas(ctx):
    sample = 'I am exploring the forest to help my village.'
    result = finish(ctx, 'I exploring.', better=sample)
    assert result['corrected_answer'] == 'I am exploring.'
    assert result['better_answer'] == sample


@pytest.fixture
def fake_ctx():
    return SimpleNamespace(scene_cap=3, scene_hint_vi="SCENE_HINT", expect_hint_vi="EXPECT_HINT",
                           min_words=3, target_forms={"accepted_tenses": ["present_continuous"],
                                                     "hint_vi": "FORM_HINT"},
                           strict_form=False, pass_total=6, reward_total=8)


@pytest.mark.parametrize("relevance,cap,meaning,expected,passed,reward", [
    ("off", 3, 5, 0, False, False),
    ("scene", 3, 5, 3, True, False),
    ("scene", None, 5, 5, True, True),
    ("story", 3, 0, 1, False, False),
    ("scene", 3, 0, 1, False, False),
    ("story", 3, 5, 5, True, True),
])
def test_relevance_scoring(fake_ctx, relevance, cap, meaning, expected, passed, reward):
    fake_ctx.scene_cap = cap
    result = finish(fake_ctx, "I am exploring the forest.", meaning, relevance=relevance)
    assert result["relevance"] == relevance
    assert result["scores"] == {"meaning": expected, "grammar": 5}
    assert result["total_score"] == expected + 5
    assert result["passed"] is passed
    assert result["reward_eligible"] is reward


def test_scene_hint_replaces_expect_hint(fake_ctx):
    result = finish(fake_ctx, "I am exploring the forest.", 1, relevance="scene")
    assert fake_ctx.scene_hint_vi in result["feedback_vi"]
    assert fake_ctx.expect_hint_vi not in result["feedback_vi"]
    fake_ctx.scene_hint_vi = ""
    assert fake_ctx.expect_hint_vi in finish(
        fake_ctx, "I am exploring the forest.", 1, relevance="scene")["feedback_vi"]


@pytest.mark.parametrize("relevance", ["unknown", "", None, [], True])
def test_invalid_relevance(relevance):
    with pytest.raises(ValueError):
        validate_dialogue_grade({"relevance": relevance, "meaning": 5, "feedback_vi": "T?t."})


def test_relevance_without_optional_better_answer():
    validate_dialogue_grade({"relevance": "story", "meaning": 5, "feedback_vi": "T?t."})
    assert zero_result("", "Nh?p c?u tr? l?i.")["relevance"] is None


@pytest.mark.parametrize("change", [
    {"meaning": True}, {"meaning": 6}, {"meaning": -1}, {"meaning": 1.5},
    {"feedback_vi": " "}, {"feedback_vi": None}, {"better_answer": None},
    {"grammar": 5},
])
def test_relevance_schema_rejects_invalid_fields(change):
    with pytest.raises(ValueError):
        validate_dialogue_grade({"relevance": "story", "meaning": 5,
                                 "feedback_vi": "T?t.", **change})


def test_user_reported_creatures_sentence_has_two_explicit_fixes(ctx):
    answer = 'The creatures have destroy animal'
    result = finish(ctx, answer)
    assert result['scores']['grammar'] == 3
    assert len(result['errors']) == 2
    assert result['corrected_answer'] == 'The creatures have destroyed an animal'
    assert {e['quote'] for e in result['errors']} == {'The creatures have destroy', 'animal'}
    assert all(e['replacement'] and e['explanation_vi'] for e in result['errors'])


@pytest.mark.parametrize('answer,corrected', [
    ('The creatures has destroyed many trees.', 'The creatures have destroyed many trees.'),
    ('I have go to the forest.', 'I have gone to the forest.'),
    ('They comes from the north.', 'They come from the north.'),
    ("He don't knows the answer.", "He doesn't know the answer."),
    ('The animals is running.', 'The animals are running.'),
    ('I have destroy a tree.', 'I have destroyed a tree.'),
])
def test_expanded_rules_produce_visible_minimal_fixes(ctx, answer, corrected):
    result = finish(ctx, answer)
    assert result['scores']['grammar'] < 5
    assert result['errors']
    assert result['corrected_answer'] == corrected


def test_every_non_edit_penalty_has_a_displayable_reason(ctx):
    short = finish(ctx, 'Exploring.')
    strict = finish(replace(ctx, strict_form=True), 'I went to help the animals.')
    for result in (short, strict):
        assert result['scores']['grammar'] < 5
        assert result['grammar_notes']
        assert all(isinstance(note, str) and note.strip() for note in result['grammar_notes'])


def test_relevance_is_required():
    with pytest.raises(ValueError):
        validate_dialogue_grade({"meaning": 5, "feedback_vi": "T?t."})

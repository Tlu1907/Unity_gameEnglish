import pytest
from script.grammar_rules import RULES
from script.rules import rule_errors
from script.verbs import IRREGULAR, ing_form, past_form

POSITIVES = {
    'article.bare_object': [('I saw animal.', 'an animal'), ('They destroyed portal.', 'a portal'), ('I found sword.', 'a sword')],
    'be.agreement': [('I are a wizard.', 'I am'), ('She am ready.', 'She is'), ('They is strong.', 'They are')],
    'do.agreement': [("He don't know.", "He doesn't"), ('She do not fight.', "She doesn't"), ("I doesn't know.", "I don't")],
    'pres_cont.bare_verb': [('I am go home.', 'I am going'), ('They are run away.', 'They are running'), ('He is hide here.', 'He is hiding')],
    'pres_cont.missing_aux': [('I looking for my dog.', 'I am looking'), ('They coming home.', 'They are coming'), ('She running away.', 'She is running')],
    'article.role_noun': [('I am agent.', 'I am an agent'), ('He is hunter.', 'He is a hunter'), ('She is engineer.', 'She is an engineer')],
    'plural.after_number': [('two monster.', 'two monsters'), ('several city.', 'several cities'), ('many fox.', 'many foxes')],
    'pres_perf.wrong_participle': [('I have went home.', 'I have gone'), ('He has saw it.', 'He has seen'), ('I has found it.', 'I have found'), ('She have hidden here.', 'She has hidden')],
    'future.will_not_base': [('I will goes home.', 'will go'), ('He will running away.', 'will run'), ('They will went home.', 'will go')],
    'past.v1_with_past_marker': [('Yesterday I go home.', 'I went'), ('I see him last night.', 'I saw'), ('Two days ago they find it.', 'they found')],
    'sv.third_person_base': [('She go home.', 'She goes'), ('He try hard.', 'He tries'), ('It watch us.', 'It watches')],
}
NEGATIVES = {
    'article.bare_object': ['I saw an animal.', 'I found sword fragments.', 'They have destroyed many trees.', 'I saw animal tracks.'],
    'be.agreement': ['I am here.', 'She is here.', 'They are strong.'],
    'do.agreement': ["He doesn't know.", "I don't fight.", 'They do not run.'],
    'pres_cont.bare_verb': ['I am free.', 'I am close to home.', 'I am here to help you.', 'What I do is help people.', 'They are open to ideas.', 'They are cross with me.', 'They are read every day.', 'They are run by the villagers.'],
    'pres_cont.missing_aux': ['I sing every day.', 'I bring a sword.', 'I ring the bell.', 'It is something.', 'I am looking.'],
    'article.role_noun': ['I am an agent.', 'I am guard captain.', "I'm Alex.", 'i am alex'],
    'plural.after_number': ['two monsters.', 'two stone bridges.', 'many animal tracks.'],
    'pres_perf.wrong_participle': ['I have gone home.', 'She has seen it.', 'I have a sword.', 'I have read it.', 'I insist that he have a sword.', 'They have come here.', 'The portal has become brighter.', 'I have work to do.'],
    'future.will_not_base': ['I will go home.', 'He will be ready.', 'They will read it.', 'I will saw the wood.', 'I will found a village.'],
    'past.v1_with_past_marker': ['Yesterday I went home.', 'I go home every day.', 'Yesterday I will go home.', 'Last night I let it go.', 'Yesterday I went home, but now I go to school.', 'I know he went home yesterday.'],
    'sv.third_person_base': ['Let it go.', 'I see her run.', 'I hear it sing.', 'He goes home.', 'She cut it yesterday.', 'She makes it go.', 'I am letting it go.', 'She helps it go.'],
}
CASES = [(rule.id, answer, fix) for rule in RULES for answer, fix in POSITIVES[rule.id]]
COMMON = ['I am here to help you.', 'What I do is help people.', 'Let it go.', 'I sing every day.', "I'm Alex.", 'i am alex', 'They are strong.']


@pytest.mark.parametrize('rule_id,answer,fix', CASES)
def test_rule_positive(rule_id, answer, fix):
    rule = next(r for r in RULES if r.id == rule_id)
    errors = rule.find(answer)
    assert any(e['replacement'] == fix for e in errors)
    assert all(answer[e['_start']:e['_end']] == e['quote'] for e in errors)
    assert rule.explain_vi


@pytest.mark.parametrize('rule_id,answer', [(r.id, a) for r in RULES for a in NEGATIVES[r.id] + COMMON])
def test_rule_negative(rule_id, answer):
    assert next(r for r in RULES if r.id == rule_id).find(answer) == []


def test_registry_quality_gate():
    assert len(RULES) == len({r.id for r in RULES}) == 11
    assert all(len(POSITIVES[r.id]) >= 3 and len(NEGATIVES[r.id]) >= 3 for r in RULES)
    assert len(IRREGULAR) >= 145


@pytest.mark.parametrize('verb,expected', [('run', 'running'), ('come', 'coming'), ('hide', 'hiding'), ('lie', 'lying'), ('see', 'seeing'), ('swim', 'swimming'), ('split', 'splitting'), ('forget', 'forgetting'), ('prefer', 'preferring'), ('open', 'opening'), ('panic', 'panicking')])
def test_ing(verb, expected):
    assert ing_form(verb) == expected


@pytest.mark.parametrize('verb,expected', [('plan', 'planned'), ('prefer', 'preferred'), ('carry', 'carried'), ('save', 'saved'), ('help', 'helped')])
def test_regular_past(verb, expected):
    assert past_form(verb) == expected


def test_sorted_overlap_and_combined_fix():
    answer = 'I are go home. She have went away. two monster.'
    errors = rule_errors(answer)
    assert len(errors) == 3
    assert [e['replacement'] for e in errors] == ['I am going', 'She has gone', 'two monsters']
    assert all(set(e) == {'quote', 'replacement', 'explanation_vi', 'rule_id', 'tense'} for e in errors)


@pytest.mark.parametrize('answer', ['My name is Nguyễn Thế Luân.', 'Đức.', "I'm Alex.", 'i am alex, a traveler from another village'])
def test_required_clean_examples(answer):
    assert rule_errors(answer) == []

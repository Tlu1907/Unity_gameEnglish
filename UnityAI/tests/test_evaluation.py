import json
from pathlib import Path
from unittest.mock import patch
import pytest
from script.dialogue_loader import DialogueStore
from script.dialogue_grading import finalize_dialogue_grade
from script.eval_dialogue import (check, check_leakage, deterministic_case, summarize,
                                  meets_threshold, main, DETERMINISTIC_TAGS, GRAMMAR_EXPECT_KEYS)

CASES = json.loads(Path('script/eval_dialogue_cases.json').read_text(encoding='utf-8'))


@pytest.mark.parametrize('case', [c for c in CASES if c.get('tag') in DETERMINISTIC_TAGS], ids=lambda c: c['id'])
def test_deterministic_dataset_without_model(case):
    data, fails = deterministic_case(DialogueStore().get(case['node_id']), case)
    assert fails == []


@pytest.mark.parametrize('case', [c for c in CASES if c.get('tag') != 'known_gap'], ids=lambda c: c['id'])
def test_all_supported_grammar_labels_without_model(case):
    data, fails = deterministic_case(DialogueStore().get(case['node_id']), case)
    assert fails == []


def test_known_gaps_do_not_reduce_rate():
    rows = [dict(id='ok', fails=[], model_called=True, seconds=0, status='validated', tag='correct'),
            dict(id='gap', fails=['missing rule'], model_called=False, seconds=0, status='validated', tag='known_gap')]
    summary = summarize(rows)
    assert summary['pass_rate'] == 1
    assert summary['known_gap_cases'] == 1
    assert meets_threshold(summary)


def test_gate_rejects_deterministic_failure_and_model_rate():
    assert not meets_threshold({'deterministic_failed': 1, 'model_pass_rate': 1})
    assert not meets_threshold({'deterministic_failed': 0, 'model_pass_rate': 0.84})
    assert meets_threshold({'deterministic_failed': 0, 'model_pass_rate': 0.85})


def test_offline_cli_never_constructs_model(tmp_path):
    output = tmp_path/'report.json'
    with patch('sys.argv', ['eval', '--deterministic', '--output', str(output)]), patch(
            'script.eval_dialogue.EnglishModel', side_effect=AssertionError('model loaded')):
        assert main() == 0
    summary = json.loads(output.read_text(encoding='utf-8'))['with_few_shot']['summary']
    assert summary['deterministic_failed'] == 0
    assert summary['model_cases'] == 0


@pytest.mark.parametrize('tag,model_rate,det_failed', [('rule', 1, 1), ('correct', 0.5, 0)])
def test_cli_failure_exit(tmp_path, tag, model_rate, det_failed):
    report = {'summary': {'deterministic_failed': det_failed, 'model_pass_rate': model_rate}}
    with patch('sys.argv', ['eval', '--deterministic', '--output', str(tmp_path/'report.json')]), patch(
            'script.eval_dialogue.evaluate', return_value=report):
        assert main() == 1


def test_new_expectations():
    ctx = DialogueStore().get('ranger_question_02_player_purpose')
    data = finalize_dialogue_grade(ctx, 'I exploring.', 'story', 5, 'Bạn nói rõ hoạt động.',
                                   'I am exploring the forest.', [])
    assert check({'expect': {'total_max': 10, 'has_better_answer': True}}, data, True) == []
    assert check({'expect': {'total_max': 0}}, data, True)


def test_no_prompt_leakage():
    check_leakage(DialogueStore(), CASES)
import pytest
from script.rules import ascii_fold, word_count, looks_vietnamese
from script.dialogue_grading import precheck


@pytest.mark.parametrize('text,expected', [('Đức.', 'Duc.'), ('Nguyễn Thế Luân', 'Nguyen The Luan'), ('ĐẶNG', 'DANG')])
def test_ascii_fold(text, expected):
    assert ascii_fold(text) == expected


def test_names():
    assert word_count('Đức.') == 1
    assert precheck('Đức.') is None
    assert not looks_vietnamese('My name is Nguyễn Thế Luân.')
    assert not looks_vietnamese('Luân')
    assert precheck("I'm Đức.") is None

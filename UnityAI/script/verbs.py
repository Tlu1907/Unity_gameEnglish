"""Small local verb tables; conservative English inflections, no NLP dependency."""
import csv
import re
from pathlib import Path


def load_irregular_verbs(path=None):
    path = Path(path) if path else Path(__file__).resolve().parents[1] / 'config/irregular_verbs.csv'
    with path.open(encoding='utf-8', newline='') as stream:
        reader = csv.DictReader(stream)
        if reader.fieldnames != ['base', 'past', 'pp']:
            raise ValueError('irregular_verbs.csv requires base,past,pp columns')
        rows = list(reader)
    if any(not all(row.get(k, '').isalpha() for k in ('base', 'past', 'pp')) for row in rows):
        raise ValueError('Invalid irregular verb row')
    result = {row['base']: (row['past'], row['pp']) for row in rows}
    if len(result) != len(rows):
        raise ValueError('Duplicate irregular verb base')
    return result


IRREGULAR = load_irregular_verbs()
ACTION_VERBS = frozenset('go come look search find explore walk run try help fight travel collect gather protect rescue save chase escape climb carry cross visit repair follow hunt investigate return enter leave hide swim teach build bring take give write read speak eat drink drive ride watch work play plant open'.split())
REGULAR = frozenset('look search explore walk try help travel collect gather protect rescue save chase escape climb carry cross visit repair follow hunt investigate return enter watch work play plant open live want need call ask attack check study talk listen learn move stop plan prefer clean'.split())
REGULAR = REGULAR | frozenset('destroy appear damage discover close mark burn report wait notice examine describe'.split())
ACTION_VERBS = ACTION_VERBS | frozenset('destroy appear damage discover mark burn report wait notice examine describe'.split())
BASES = frozenset(IRREGULAR) | REGULAR
# Explicit doubled endings avoid guessing stress in multisyllabic words.
DOUBLING = frozenset('begin forget forbid prefer regret refer admit occur quit upset'.split())


def doubles_final(base):
    if base in DOUBLING: return True
    # A single vowel group approximates a monosyllable; multisyllabic final
    # stress is deliberately handled by the small explicit table above.
    return (len(re.findall(r'[aeiouy]+', base)) == 1
            and bool(re.search(r'[^aeiou][aeiou][^aeiouwxy]$', base)))


def ing_form(base):
    if base == 'be': return 'being'
    if base.endswith('ie'): return base[:-2] + 'ying'
    if doubles_final(base): return base + base[-1] + 'ing'
    if base.endswith('c'): return base + 'king'
    if base.endswith('e') and not base.endswith(('ee', 'ye', 'oe')): return base[:-1] + 'ing'
    if base == 'travel': return 'travelling'
    return base + 'ing'


def third_person(base):
    if base == 'be': return 'is'
    if base == 'have': return 'has'
    if base.endswith('y') and base[-2] not in 'aeiou': return base[:-1] + 'ies'
    if base.endswith(('s', 'sh', 'ch', 'x', 'z', 'o')): return base + 'es'
    return base + 's'


def past_form(base):
    if base in IRREGULAR: return IRREGULAR[base][0]
    if base.endswith('e'): return base + 'd'
    if base.endswith('c'): return base + 'ked'
    if base.endswith('y') and base[-2] not in 'aeiou': return base[:-1] + 'ied'
    if doubles_final(base): return base + base[-1] + 'ed'
    return base + 'ed'


ING_TO_BASE = {ing_form(v): v for v in BASES} | {'traveling': 'travel'}
PAST_TO_BASE = {past_form(v): v for v in BASES if v != 'be'}
PP_TO_BASE = {forms[1]: v for v, forms in IRREGULAR.items()} | {past_form(v): v for v in REGULAR}

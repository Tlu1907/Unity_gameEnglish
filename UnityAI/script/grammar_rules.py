"""Conservative rule registry. Unknown/ambiguous constructions receive no penalty.

Only the first three non-overlapping matches are reported. No preposition rule:
e.g. 'working my task' remains an accepted known gap.
"""
import re
from dataclasses import dataclass
from typing import Callable
from script.verbs import (ACTION_VERBS, BASES, IRREGULAR, ING_TO_BASE,
                          PAST_TO_BASE, PP_TO_BASE, ing_form, past_form, third_person)

SUBJECT = r'(?:I|you|we|they|he|she|it)'


def choices(words):
    return '(?:' + '|'.join(re.escape(w) for w in sorted(words, key=lambda x: (-len(x), x))) + ')'


def be_for(subject):
    s = subject.lower()
    return 'am' if s == 'i' else 'is' if s in {'he', 'she', 'it'} else 'are'


def same_case(word, replacement):
    return replacement.upper() if word.isupper() and len(word) > 1 else replacement.capitalize() if word[:1].isupper() else replacement


def candidate(match, replacement):
    return {'quote': match.group(), 'replacement': replacement,
            '_start': match.start(), '_end': match.end()}


def be_agreement(answer):
    out = []
    for m in re.finditer(r'\b(' + SUBJECT + r')\s+(am|is|are)\b', answer, re.I):
        aux = be_for(m[1])
        if aux != m[2].lower(): out.append(candidate(m, f'{m[1]} {aux}'))
    for m in re.finditer(r'\b(the\s+(?:creatures|monsters|guardians|animals|trees|stones|ranger|guardian|portal))\s+(am|is|are)\b', answer, re.I):
        aux = 'are' if m[1].lower().split()[-1] in {'creatures', 'monsters', 'guardians', 'animals', 'trees', 'stones'} else 'is'
        if aux != m[2].lower(): out.append(candidate(m, f'{m[1]} {aux}'))
    return out


def do_agreement(answer):
    out = []
    pattern = r"\b(" + SUBJECT + r")\s+(don't|dont|do not|doesn't|doesnt|does not)\b"
    for m in re.finditer(pattern, answer, re.I):
        singular = m[1].lower() in {'he', 'she', 'it'}
        if singular != m[2].lower().startswith('does'):
            out.append(candidate(m, m[1] + (" doesn't" if singular else " don't")))
    reverse = {third_person(base): base for base in BASES if third_person(base) != base}
    reverse.update({past_form(base): base for base in BASES if past_form(base) != base})
    # Ambiguous words can themselves be base verbs (saw wood, found a village).
    for word in {'saw', 'found', 'fell', 'rose', 'left', 'wound', 'bore', 'bound', 'ground'}:
        reverse.pop(word, None)
    combined = []
    for m in re.finditer(pattern + r'\s+(' + choices(set(reverse) | set(BASES)) + r')\b', answer, re.I):
        aux = "doesn't" if m[1].lower() in {'he', 'she', 'it'} else "don't"
        base = reverse.get(m[3].lower(), m[3].lower())
        if (m[1].lower() in {'he', 'she', 'it'}) != m[2].lower().startswith('does') or base != m[3].lower():
            combined.append(candidate(m, f'{m[1]} {aux} {base}'))
    for m in re.finditer(r"\b((?:do|does|did)(?:\s+not)?|don't|doesn't|didn't)\s+(" + choices(reverse) + r')\b', answer, re.I):
        if m[1].lower() in {'do', 'does', 'did'} and m[2].lower() in {'is', 'was', 'were'}:
            continue  # Copular clauses: "What I do is help people", "All I did was wait".
        out.append(candidate(m, m[1] + ' ' + reverse[m[2].lower()]))
    return combined + out


def bare_progressive(answer):
    out = []
    # open/cross are adjectives; read is an invariant passive participle.
    safe_actions = ACTION_VERBS - {'open', 'cross', 'read'}
    pattern = r'\b(' + SUBJECT + r')\s+(am|is|are)\s+(' + choices(safe_actions) + r')\b'
    for m in re.finditer(pattern, answer, re.I):
        if m[3].lower() == 'run' and re.match(r'\s+(?:by\b|[a-z]+ly\b)', answer[m.end():], re.I):
            continue
        out.append(candidate(m, f'{m[1]} {be_for(m[1])} {ing_form(m[3].lower())}'))
    return out


def missing_aux(answer):
    forbidden = {'sing', 'bring', 'ring', 'thing', 'king', 'nothing', 'something', 'anything', 'everything', 'morning', 'evening'}
    pattern = r'\b(' + SUBJECT + r')\s+(' + choices(set(ING_TO_BASE) - forbidden) + r')\b'
    return [candidate(m, f'{m[1]} {be_for(m[1])} {m[2]}')
            for m in re.finditer(pattern, answer, re.I)]


ROLES = frozenset('agent hunter student traveler traveller wizard hero adventurer archer explorer knight soldier warrior ranger farmer teacher doctor guard mage thief scout engineer artist'.split())


def role_article(answer):
    pattern = (r'\b((?:I\s+am|he\s+is|she\s+is|I\x27m|he\x27s|she\x27s))\s+('
               + choices(ROLES) + r')\b(?=\s*(?:[.,!?;:]|$|\b(?:from|in|and|who|with|of|for|on)\b))')
    return [candidate(m, f'{m[1]} {"an" if m[2][0].lower() in "aeiou" else "a"} {m[2]}')
            for m in re.finditer(pattern, answer, re.I)]


def object_article(answer):
    # Require a complete bare object, not a noun modifier (animal tracks, sword attacks).
    verbs = 'destroy destroyed destroying find found finding see saw seeing protect protected protecting rescue rescued rescuing carry carried carrying buy bought buying'
    nouns = 'animal monster creature guardian portal sword shield dog cat bridge tower tree book map'
    pattern = (r'\b' + choices(verbs.split()) + r'\s+(' + choices(nouns.split())
               + r')\b(?=\s*(?:[.!?;:]|$|\b(?:near|from|in|on|at)\b))')
    out = []
    for match in re.finditer(pattern, answer, re.I):
        noun = match[1]
        out.append({'quote': noun, 'replacement': ('an ' if noun[0].lower() in 'aeiou' else 'a ') + noun,
                    '_start': match.start(1), '_end': match.end(1)})
    return out


NOUNS = frozenset('monster creature tree animal bridge tent stone statue cave horse goat mountain entrance wolf fox city village enemy fairy berry box church bush watch hero sword shield dog cat guard tower river road path seed portal ruin dragon bird goblin flower book map coin'.split())
NUMBERS = 'two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty many several'.split()


def plural(noun):
    if noun == 'wolf': return 'wolves'
    if noun.endswith('y') and noun[-2] not in 'aeiou': return noun[:-1] + 'ies'
    if noun.endswith(('s', 'sh', 'ch', 'x', 'z')): return noun + 'es'
    if noun == 'hero': return 'heroes'
    return noun + 's'


def plural_number(answer):
    # Restrict optional modifiers; never infer plurals for arbitrary noun compounds.
    pattern = (r'\b(' + choices(NUMBERS) + r'|(?:[2-9]|1[0-9]|20))\s+'
               r'((?:(?:lost|hidden|old|large|small|dark|ancient|wild|new)\s+){0,2})('
               + choices(NOUNS) + r')\b(?=\s*(?:[.,!?;:]|$|\b(?:and|or|in|on|near|from|with|for|by|at|to|are|were|have|came|come)\b))')
    return [candidate(m, f'{m[1]} {m[2]}{same_case(m[3], plural(m[3].lower()))}')
            for m in re.finditer(pattern, answer, re.I)]


def perfect_participle(answer):
    out = []
    # Agreement only when a known verbal participle follows: 'I have a sword' is possession.
    forms = set(PP_TO_BASE) | set(PAST_TO_BASE)
    pattern = r'\b(' + SUBJECT + r')\s+(have|has)\s+(' + choices(forms) + r')\b'
    for m in re.finditer(pattern, answer, re.I):
        aux = 'has' if m[1].lower() in {'he', 'she', 'it'} else 'have'
        v = m[3].lower()
        base = PAST_TO_BASE.get(v)
        pp = IRREGULAR[base][1] if base in IRREGULAR else v
        # 'have read/cut/...' is already a valid participle.
        if v in PP_TO_BASE: pp = v
        if aux != m[2].lower() or pp != v:
            out.append(candidate(m, f'{m[1]} {aux} {same_case(m[3], pp)}'))
    # Past+participle errors without explicit pronoun (e.g. 'the ranger has went').
    wrong = {past: pp for base, (past, pp) in IRREGULAR.items()
             if past != pp and past not in PP_TO_BASE}
    for m in re.finditer(r'\b(have|has)\s+(' + choices(wrong) + r')\b', answer, re.I):
        out.append(candidate(m, f'{m[1]} {same_case(m[2], wrong[m[2].lower()])}'))
    # These bases cannot be bare object nouns after have/has in these exercises.
    safe_bases = {'go', 'come', 'see', 'eat', 'take', 'give', 'write', 'speak', 'know', 'break',
                  'choose', 'hide', 'become', 'be', 'begin', 'destroy', 'appear', 'discover'}
    for m in re.finditer(r'\b(' + SUBJECT + r')\s+(have|has)\s+(' + choices(safe_bases) + r')\b', answer, re.I):
        base = m[3].lower()
        pp = IRREGULAR[base][1] if base in IRREGULAR else past_form(base)
        aux = 'has' if m[1].lower() in {'he', 'she', 'it'} else 'have'
        if aux != m[2].lower() or pp != base:
            out.append(candidate(m, f'{m[1]} {aux} {same_case(m[3], pp)}'))
    for m in re.finditer(r'\b(the\s+(?:creatures|monsters|guardians|animals|trees|stones|ranger|guardian|portal))\s+(have|has)\s+(' + choices(forms | safe_bases) + r')\b', answer, re.I):
        aux = 'have' if m[1].lower().split()[-1] in {'creatures', 'monsters', 'guardians', 'animals', 'trees', 'stones'} else 'has'
        verb = m[3].lower()
        base = PAST_TO_BASE.get(verb, verb)
        pp = verb if verb in PP_TO_BASE else IRREGULAR[base][1] if base in IRREGULAR else past_form(base)
        if aux != m[2].lower() or pp != verb:
            out.append(candidate(m, f'{m[1]} {aux} {same_case(m[3], pp)}'))
    for m in re.finditer(r'\b(' + SUBJECT + r')\s+(has|have)\b', answer, re.I):
        aux = 'has' if m[1].lower() in {'he', 'she', 'it'} else 'have'
        # Subjunctives such as 'I insist that he have ...' can be correct.
        prefix = re.split(r'[.!?;]', answer[:m.start()])[-1]
        if re.search(r'\b(?:insist|suggest|demand|require|important|essential)\b', prefix, re.I):
            continue
        if aux != m[2].lower(): out.append(candidate(m, f'{m[1]} {aux}'))
    return out


def future_base(answer):
    reverse = {}
    for base in sorted(BASES - {'be'}):
        for v in (third_person(base), ing_form(base), past_form(base)):
            if v not in BASES: reverse[v] = base
    # These past forms also have independent base-verb meanings (saw wood,
    # found a village, fell a tree). Leave them unpenalized when ambiguous.
    for word in {'saw', 'found', 'ground', 'rose', 'fell', 'wound', 'bore', 'bound', 'left'}:
        reverse.pop(word, None)
    reverse.update({'was': 'be', 'were': 'be', 'being': 'be', 'is': 'be'})
    return [candidate(m, m[1] + ' ' + reverse[m[2].lower()])
            for m in re.finditer(r'\b(will)\s+(' + choices(reverse) + r')\b', answer, re.I)]


PAST_MARKER = re.compile(r'\b(?:yesterday|last\s+(?:night|week|month|year)|(?:\d+|one|two|three|a|an)\s+(?:days?|hours?|weeks?|months?|years?)\s+ago)\b', re.I)


def past_marker(answer):
    out = []
    for clause in re.finditer(r'(?:(?![,;.!?\n]|\b(?:and|but|because|while|although)\b).)+', answer, re.I):
        if not PAST_MARKER.search(clause.group()): continue
        pattern = r'\b(' + SUBJECT + r')\s+(' + choices(BASES - {'be'}) + r')\b'
        for m in re.finditer(pattern, clause.group(), re.I):
            if causative(clause.group(), m.start()): continue
            if m[2].lower() in {'know', 'think', 'say'} and re.match(r'\s+(?:that\s+)?' + SUBJECT + r'\b', clause.group()[m.end():], re.I):
                continue  # don't move a reporting/knowledge verb into the past
            base = m[2].lower()
            if past_form(base) == base: continue
            e = candidate(m, m[1] + ' ' + past_form(base))
            e['_start'] += clause.start(); e['_end'] += clause.start()
            out.append(e)
    return out


def causative(answer, start):
    return bool(re.search(r'\b(?:let|lets|letting|make|makes|made|making|help|helps|helped|helping|see|sees|saw|seen|seeing|watch|watches|watched|watching|hear|hears|heard|hearing|have|has|had|having)\s+$', answer[:start], re.I))


def third_base(answer):
    out = []
    # 'read', 'put', etc. can already be past; 'he cut it' must not be penalized.
    verbs = {v for v in BASES - {'be', 'have'} if past_form(v) != v}
    for m in re.finditer(r'\b(he|she|it)\s+(' + choices(verbs) + r')\b', answer, re.I):
        if not causative(answer, m.start()):
            out.append(candidate(m, f'{m[1]} {third_person(m[2].lower())}'))
    reverse = {third_person(base): base for base in BASES - {'be'} if third_person(base) != base}
    for m in re.finditer(r'\b(I|you|we|they)\s+(' + choices(reverse) + r')\b', answer, re.I):
        out.append(candidate(m, m[1] + ' ' + reverse[m[2].lower()]))
    return out


@dataclass(frozen=True)
class Rule:
    id: str
    tense: str
    find: Callable[[str], list[dict]]
    explain_vi: str


# More specific combined fixes win overlapping matches, then results sort by position.
RULES = (
    Rule('pres_cont.bare_verb', 'present_continuous', bare_progressive, 'Hiện tại tiếp diễn dùng chủ ngữ + am/is/are + V-ing; I đi với am, he/she/it với is, you/we/they với are.'),
    Rule('pres_perf.wrong_participle', 'present_perfect', perfect_participle, 'Hiện tại hoàn thành dùng have/has + V3; he/she/it đi với has, I/you/we/they với have.'),
    Rule('past.v1_with_past_marker', 'past_simple', past_marker, 'Với mốc quá khứ đã kết thúc như yesterday hoặc last week, dùng động từ quá khứ V2.'),
    Rule('be.agreement', 'present_simple', be_agreement, 'Động từ be: I đi với am; he/she/it đi với is; you/we/they đi với are.'),
    Rule('do.agreement', 'present_simple', do_agreement, "Phủ định hiện tại đơn dùng he/she/it + doesn't, I/you/we/they + don't, rồi động từ nguyên mẫu."),
    Rule('pres_cont.missing_aux', 'present_continuous', missing_aux, 'Hiện tại tiếp diễn cần am/is/are trước V-ing; I dùng am, he/she/it dùng is, you/we/they dùng are.'),
    Rule('article.role_noun', 'general', role_article, 'Danh từ đếm được số ít chỉ vai trò cần a/an; dùng an trước âm nguyên âm, a trước âm phụ âm.'),
    Rule('article.bare_object', 'general', object_article, 'Danh từ đếm được số ít làm tân ngữ cần từ hạn định như a/an. Ví dụ: an animal; nếu nói chung nhiều con vật, có thể dùng animals.'),
    Rule('plural.after_number', 'general', plural_number, 'Sau số đếm từ hai trở lên, many hoặc several, danh từ đếm được phải ở dạng số nhiều.'),
    Rule('future.will_not_base', 'future_simple', future_base, 'Tương lai đơn dùng will + động từ nguyên mẫu, không thêm -s, -ing hoặc dạng quá khứ.'),
    Rule('sv.third_person_base', 'present_simple', third_base, 'Hiện tại đơn khẳng định với he/she/it dùng động từ thêm -s/-es, không dùng động từ nguyên mẫu.'),
)


def rule_error_spans(answer):
    """Internal source locations; public errors expose only the five contract keys."""
    selected = []
    for rule in RULES:
        for e in rule.find(answer):
            a, b = e['_start'], e['_end']
            if any(a < old['_end'] and old['_start'] < b for old in selected): continue
            selected.append({**e, 'explanation_vi': rule.explain_vi, 'rule_id': rule.id, 'tense': rule.tense})
    selected.sort(key=lambda e: e['_start'])
    return selected[:3]


def rule_errors(answer):
    return [{k: v for k, v in e.items() if not k.startswith('_')} for e in rule_error_spans(answer)]



TENSES = frozenset({'present_simple', 'present_continuous', 'past_simple', 'present_perfect', 'future_simple'})


def detect_tenses(answer) -> set[str]:
    """Surface heuristic, not a parser. Recognizes known verb forms, not intent.

    Masks recognized auxiliary phrases so 'has seen' isn't also called past.
    Invariant past/base forms such as 'cut' are ambiguous and skipped. Unmatched
    clauses default to present_simple; a mixed answer may return multiple tenses.
    """
    found, covered = set(), []
    patterns = [
        ('present_continuous', r"\b(?:am|is|are|I'm|he's|she's|it's|we're|they're|you're)\s+(?:not\s+)?" + choices(ING_TO_BASE) + r'\b'),
        ('present_perfect', r"\b(?:have|has|haven't|hasn't|I've|you've|we've|they've|he's|she's|it's)\s+(?:not\s+)?" + choices(PP_TO_BASE) + r'\b'),
        ('future_simple', r"\b(?:will|won't|I'll|he'll|she'll|it'll|we'll|they'll|you'll)\s+(?:not\s+)?" + choices(BASES) + r'\b'),
    ]
    for tense, pattern in patterns:
        for m in re.finditer(pattern, answer, re.I):
            found.add(tense); covered.append(m.span())
    masked = list(answer)
    for a, b in covered: masked[a:b] = ' ' * (b-a)
    rest = ''.join(masked)
    pasts = {v for v in PAST_TO_BASE if v not in BASES} | {'was', 'were', 'did'}
    # Require a subject for generic -ed to avoid treating 'a cursed seed' as past.
    past_pattern = (r'\b' + choices(pasts) + r'\b|\b' + SUBJECT
                    + r'\s+(?:[a-z]+ed)\b')
    past_matches = [m for m in re.finditer(past_pattern, rest, re.I)
                    if m.group().split()[-1].lower() not in BASES]
    if past_matches: found.add('past_simple')
    for m in past_matches: masked[m.start():m.end()] = ' ' * len(m.group())
    leftover = ''.join(masked)
    if re.search(r'\b' + SUBJECT + r"\s+(?:am|is|are|do|does|don't|doesn't|" + choices(BASES | {third_person(v) for v in BASES}) + r')\b', leftover, re.I):
        found.add('present_simple')
    return found or {'present_simple'}

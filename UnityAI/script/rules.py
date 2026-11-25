"""Language checks, dialogue grammar fixes and ruins location intent."""
import re
import unicodedata


def ascii_fold(s: str) -> str:
    """Fold Vietnamese names without splitting a word at its accents."""
    s = s.replace("đ", "d").replace("Đ", "D")
    return "".join(c for c in unicodedata.normalize("NFD", s)
                   if not unicodedata.combining(c))

# ---------- nhận diện câu không chấm được ----------
VI_DIACRITICS = re.compile(
    "[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]", re.I)
VI_WORDS = {"toi", "la", "khong", "ban", "minh", "dang", "cua", "nhung", "duoc"}
EN_FUNCTION = {"i", "am", "is", "are", "the", "a", "an", "my", "name", "you", "they",
               "we", "it", "he", "she", "not", "no", "yes", "think", "know", "do",
               "don't", "to", "from", "in", "for", "and", "this", "that", "of", "with"}


def word_count(text: str) -> int:
    return len(re.findall(r"[A-Za-z']+", ascii_fold(text)))


def looks_vietnamese(text: str) -> bool:
    """Heuristic, KHÔNG phải bộ nhận diện ngôn ngữ. Câu có từ chức năng tiếng Anh thì coi là tiếng Anh
    (tránh chặn nhầm 'My name is Nguyễn Thế Luân')."""
    tokens = re.findall(r"\w+(?:'\w+)?", text, re.UNICODE)
    if not tokens:
        return False
    lower = [t.lower() for t in tokens]
    # Vietnamese "an" can coincide with the English article. Accent-heavy
    # sentences remain Vietnamese; isolated names and English introductions pass.
    dia = sum(bool(VI_DIACRITICS.search(t)) for t in tokens)
    if len(tokens) >= 3 and dia / len(tokens) > 0.5:
        return True
    if any(t in EN_FUNCTION or t in {"i'm", "i've", "we're", "they're", "you're", "he's", "she's"} for t in lower):
        return False
    dia = sum(bool(VI_DIACRITICS.search(t)) for t in tokens)
    vi = sum(t in VI_WORDS for t in lower)
    return (len(tokens) >= 2 and dia / len(tokens) > 0.3) or vi >= 2


def looks_gibberish(words: list[str], text: str) -> bool:
    if not words:
        return False
    with_vowel = sum(bool(re.search(r"[aeiouy]", w, re.I)) for w in words)
    return with_vowel / len(words) < 0.5 or bool(re.search(r"(.)\1{4,}", text))


from script.grammar_rules import Rule, RULES, TENSES, detect_tenses, rule_errors


_RUINS = re.compile(r"\b(ruins?|temple|ancient (?:place|site)|old stones?)\b", re.I)
_WHERE = re.compile(
    r"\b(where|which way|how to (?:get|go|find|reach)|how (?:can|do|could|should) (?:i|we) (?:get|go|find|reach)|"
    r"the way|directions?|on the map|mark)\b", re.I)
_REF = re.compile(r"\b(it|there|that place|the place)\b", re.I)
_ORIGIN = re.compile(r"\b(creatures?|monsters?)\b.*\b(come from|came from|origin)\b", re.I)


def ruins_intent_by_rules(text: str) -> str:
    """Trả 'yes' | 'no' | 'maybe'. 'maybe' nghĩa là cần hỏi model."""
    t = text.strip()
    if _ORIGIN.search(t):
        return "no"                      # hỏi nguồn gốc sinh vật KHÔNG kích hoạt nhánh ẩn
    if re.search(r"\b(?:don't|do not|never|not)\b", t, re.I) and _RUINS.search(t):
        return "maybe"
    if _RUINS.search(t) and _WHERE.search(t):
        if re.match(r"^(?:where\b|which way\b|how\b|can\b|could\b|would\b|"
                    r"please\b|show\b|tell\b|mark\b|give\b|do you know\b)", t, re.I):
            return "yes"
        return "maybe"  # A statement mentioning a location is not an explicit request.
    if _RUINS.search(t) or (_WHERE.search(t) and _REF.search(t)):
        return "maybe"
    return "no"

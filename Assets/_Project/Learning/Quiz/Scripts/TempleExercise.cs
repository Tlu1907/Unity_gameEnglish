using System;
using System.Text.RegularExpressions;

/// <summary>Authored temple exercises. The first three never require network inference.</summary>
[Serializable]
public sealed class TempleExercise
{
    public string id, type, source_dialogue_id, prompt_en, reading_text_en, reference_answer_en;
    public string[] accepted_answers;
    public string feedback_correct_en, feedback_after_second_incorrect_en;
    public Hint hint_after_first_incorrect;
    public Evaluation evaluation;
    public Retry retry_policy;
    [Serializable] public sealed class Hint { public string en, vi; }
    [Serializable] public sealed class Retry { public string mode; public int max_attempts; }
    [Serializable] public sealed class Evaluation
    {
        public string method;
        public string[] any_required_tokens;
        public Fallback rule_fallback;
    }
    [Serializable] public sealed class Fallback
    {
        public string full_match_pattern;
        public string[] all_required_tokens, forbidden_tokens;
    }
    public bool IsReport => evaluation?.method == "rules_then_model";
    public bool IsValid => !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(source_dialogue_id) &&
        !string.IsNullOrWhiteSpace(prompt_en) && !string.IsNullOrWhiteSpace(reference_answer_en) &&
        retry_policy != null && (IsReport && retry_policy.mode == "until_pass" ||
        retry_policy.mode == "limited" && retry_policy.max_attempts > 0 &&
        (evaluation?.method == "keyword_any" && evaluation.any_required_tokens?.Length > 0 ||
        evaluation?.method == "exact_then_rules" && accepted_answers?.Length > 0 &&
        !string.IsNullOrWhiteSpace(evaluation.rule_fallback?.full_match_pattern)));

    public static string Normalize(string text) => Regex.Replace(
        Regex.Replace((text ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}\s]", ""), @"\s+", " ").Trim();
    private static bool HasWord(string text, string word) => Regex.IsMatch(text,
        @"(?<!\w)" + Regex.Escape(word.ToLowerInvariant()) + @"(?!\w)");
    public bool CheckLocal(string answer)
    {
        if (!IsValid || IsReport || string.IsNullOrWhiteSpace(answer)) return false;
        string normalized = Normalize(answer);
        if (evaluation.method == "keyword_any")
            return Array.Exists(evaluation.any_required_tokens, word => HasWord(normalized, word));
        // Reject questions and multiple sentences before punctuation is discarded.
        if (answer.Contains("?") || Regex.IsMatch(answer.Trim(), @"[.!]+\s*\S")) return false;
        var fallback = evaluation.rule_fallback;
        if (fallback.forbidden_tokens != null && Array.Exists(fallback.forbidden_tokens,
            word => HasWord(normalized, Normalize(word)))) return false;
        if (Array.Exists(accepted_answers, accepted => Normalize(accepted) == normalized)) return true;
        if (fallback.all_required_tokens != null && !Array.TrueForAll(fallback.all_required_tokens,
            word => HasWord(normalized, word))) return false;
        return Regex.IsMatch(normalized, fallback.full_match_pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}

[Serializable]
public sealed class TempleReportResponse
{
    public string status, feedback_vi;
    public bool passed;
}

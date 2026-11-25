using System;
using System.Text;

/// <summary>JSON field names mirror UnityAI; unknown optional fields remain compatible.</summary>
[Serializable]
public sealed class UnityAiDialogueResponse
{
    public string status, node_id, message, suggested_answer;
    public int attempt, attempts_left;
    public bool passed, can_award_reward, unlimited_attempts;
    public Grade data;
    [Serializable] public sealed class Scores { public int meaning, grammar; }
    [Serializable] public sealed class Error
    {
        public string quote, explanation_vi, replacement, rule_id, tense;
    }
    [Serializable] public sealed class Grade
    {
        public string original_answer, corrected_answer, feedback_vi, better_answer, form_hint_vi;
        public Error[] errors;
        public string[] grammar_notes;
        public Scores scores;
        public int total_score, max_score;
        public bool passed, reward_eligible, rule_override;
        public bool grammar_not_assessed;
    }
    public bool IsGraded => status == "validated" && data != null && data.scores != null;
    public bool IsExhausted => status == "exhausted" || IsGraded && !unlimited_attempts && attempts_left <= 0;
    public bool CanContinue => IsExhausted || IsGraded && passed;

    public string FeedbackText()
    {
        var text = new StringBuilder();
        if (IsGraded)
        {
            text.Append("ĐIỂM: ").Append(data.total_score).Append('/').Append(data.max_score)
                .Append("  ·  Ý nghĩa: ").Append(data.scores.meaning).Append("/5  ·  Ngữ pháp: ");
            if (data.grammar_not_assessed) text.AppendLine("không chấm (không trừ điểm)");
            else text.Append(data.scores.grammar).AppendLine("/5");
            text.AppendLine(data.feedback_vi);
            text.AppendLine("\nCẦN SỬA");
            bool hasDetails = false;
            if (data.errors != null)
            {
                foreach (var error in data.errors)
                {
                    if (error == null) continue;
                    if (!string.IsNullOrWhiteSpace(error.quote) && !string.IsNullOrWhiteSpace(error.replacement))
                    {
                        text.Append(error.quote).Append(" → ").AppendLine(error.replacement);
                        hasDetails = true;
                    }
                    if (!string.IsNullOrWhiteSpace(error.explanation_vi))
                    { text.AppendLine(error.explanation_vi); hasDetails = true; }
                }
            }
            if (data.grammar_notes != null)
                foreach (string note in data.grammar_notes)
                    if (!string.IsNullOrWhiteSpace(note)) { text.Append("• ").AppendLine(note); hasDetails = true; }
            if (!string.IsNullOrWhiteSpace(data.corrected_answer) &&
                !string.Equals(data.original_answer, data.corrected_answer, StringComparison.Ordinal))
            {
                text.Append("Câu đã sửa: ").AppendLine(data.corrected_answer);
                hasDetails = true;
            }
            if (!hasDetails)
            {
                if (data.grammar_not_assessed) text.AppendLine("Bài đọc hiểu này không đánh giá ngữ pháp.");
                else if (data.scores.grammar < 5 || data.rule_override)
                {
                    text.AppendLine("Điểm ngữ pháp chưa tối đa. Xem nhận xét và yêu cầu cấu trúc của bài:");
                    text.AppendLine(!string.IsNullOrWhiteSpace(data.form_hint_vi) ? data.form_hint_vi : data.feedback_vi);
                }
                else text.AppendLine("Chưa có lỗi cụ thể được bộ chấm đánh dấu.");
            }
            if (!string.IsNullOrWhiteSpace(data.better_answer))
                text.AppendLine("\nGỢI Ý CÂU HAY HƠN").AppendLine(data.better_answer);
            if (!string.IsNullOrWhiteSpace(data.form_hint_vi))
                text.Append('\n').AppendLine(data.form_hint_vi);
            if (unlimited_attempts) text.Append("\nKhông giới hạn lượt sửa bài.");
            else text.Append("\nCòn ").Append(attempts_left).Append(" lượt trả lời.");
        }
        if (IsExhausted && !passed && !string.IsNullOrWhiteSpace(suggested_answer))
            text.AppendLine("\n\nĐÁP ÁN THAM KHẢO").Append(suggested_answer);
        return text.ToString();
    }
}

[Serializable]
public sealed class UnityAiIntentResponse
{
    public string status, intent, target_location, source;
    public bool triggered;
}

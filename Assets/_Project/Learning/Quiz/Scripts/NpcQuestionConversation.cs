using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Reusable local JSON dialogue: introduction, consent, question sequence, completion.</summary>
[Serializable]
public sealed class NpcQuestionConversation
{
    public string id;
    public string speaker;
    public Dialogue introduction;
    public Consent consent;
    public Question[] questions;
    public Completion completion;
    [Serializable] public class Dialogue
    {
        public string dialogue_en, dialogue_vi;
        public ResearchDocument.Vocabulary[] vocabulary;
        public string grammar;
    }
    [Serializable] public sealed class Consent : Dialogue { public Choice[] choices; }
    [Serializable] public sealed class Choice
    {
        public string id, label_en, label_vi, reply_en, reply_vi;
        public bool loop;
        public ResearchDocument.Vocabulary[] vocabulary;
        public string grammar;
    }
    [Serializable] public sealed class Completion
    {
        public string[] reward_items;
        public string after_en, after_vi;
        public ResearchDocument.Vocabulary[] vocabulary;
        public string grammar;
    }
    [Serializable] public sealed class Question : Dialogue
    {
        public string id, type, skill, correct;
        public string[] distractors;
        public string explanation_en, explanation_vi, wrong_hint_en, wrong_hint_vi, after_en, after_vi;
        public string[] ShuffledAnswers()
        {
            var result = new string[distractors.Length + 1];
            result[0] = correct;
            Array.Copy(distractors, 0, result, 1, distractors.Length);
            for (int i = result.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                string swap = result[i]; result[i] = result[j]; result[j] = swap;
            }
            return result;
        }
    }
    public static bool TryParse(string json, string expectedId, out NpcQuestionConversation data)
    {
        data = null;
        try
        {
            var parsed = JsonUtility.FromJson<NpcQuestionConversation>(json);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.id) || (expectedId != null && parsed.id != expectedId) || string.IsNullOrWhiteSpace(parsed.speaker) ||
                parsed.introduction == null || string.IsNullOrWhiteSpace(parsed.introduction.dialogue_en) ||
                parsed.questions == null || (parsed.questions.Length == 0 && parsed.id != "q2_archaeologist") || parsed.completion == null ||
                string.IsNullOrWhiteSpace(parsed.completion.after_en)) return false;
            if (parsed.consent != null)
            {
                if (string.IsNullOrWhiteSpace(parsed.consent.dialogue_en) || parsed.consent.choices == null || parsed.consent.choices.Length != 2) return false;
                var consentIds = new HashSet<string>();
                foreach (var choice in parsed.consent.choices)
                    if (choice == null || (choice.id != "yes" && choice.id != "no") || !consentIds.Add(choice.id) ||
                        string.IsNullOrWhiteSpace(choice.label_en) || string.IsNullOrWhiteSpace(choice.reply_en)) return false;
            }
            var ids = new HashSet<string>();
            foreach (var question in parsed.questions)
            {
                if (question == null || string.IsNullOrWhiteSpace(question.id) || !ids.Add(question.id) ||
                    question.type != "multiple_choice" || string.IsNullOrWhiteSpace(question.skill) ||
                    string.IsNullOrWhiteSpace(question.dialogue_en) || string.IsNullOrWhiteSpace(question.correct) ||
                    question.distractors == null || question.distractors.Length < 1 || question.distractors.Length > 3 ||
                    string.IsNullOrWhiteSpace(question.explanation_en) || string.IsNullOrWhiteSpace(question.wrong_hint_en) ||
                    string.IsNullOrWhiteSpace(question.after_en)) return false;
                var options = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { question.correct.Trim() };
                foreach (string word in question.distractors)
                    if (string.IsNullOrWhiteSpace(word) || !options.Add(word.Trim())) return false;
            }
            data = parsed;
            return true;
        }
        catch (ArgumentException) { return false; }
    }
}

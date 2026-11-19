using System;
using UnityEngine;

/// <summary>Authored story beats with optional local JSON question sequences. No network or generated answers.</summary>
[CreateAssetMenu(menuName = "EarthDefender/Story Beat", fileName = "StoryBeat")]
public sealed class StoryBeatData : ScriptableObject
{
    public string beatId;
    [Tooltip("Optional local JSON conversation, including all questions. Drag a Data/Questions/Map1/Json TextAsset here.")]
    public TextAsset conversationJson;
    public string speaker;
    public bool requiresConsent;
    [TextArea] public string introEnglish;
    [TextArea] public string introVietnamese;
    [TextArea] public string questionEnglish;
    [TextArea] public string questionVietnamese;
    public string correct;
    public string[] distractors;
    [TextArea] public string wrongHint;
    [TextArea] public string explanation;
    [TextArea] public string afterEnglish;
    [TextArea] public string afterVietnamese;

    public bool IsValid
    {
        get
        {
            if (conversationJson != null) return TryGetConversation(out _);
            if (string.IsNullOrWhiteSpace(beatId) || string.IsNullOrWhiteSpace(correct) ||
                distractors == null || distractors.Length < 1 || distractors.Length > 3 ||
                string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(introEnglish) ||
                string.IsNullOrWhiteSpace(introVietnamese) || string.IsNullOrWhiteSpace(questionEnglish) ||
                string.IsNullOrWhiteSpace(questionVietnamese) || string.IsNullOrWhiteSpace(wrongHint) ||
                string.IsNullOrWhiteSpace(explanation) || string.IsNullOrWhiteSpace(afterEnglish) ||
                string.IsNullOrWhiteSpace(afterVietnamese)) return false;
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) { correct };
            foreach (string word in distractors)
                if (string.IsNullOrWhiteSpace(word) || !seen.Add(word)) return false;
            return true;
        }
    }

    public string[] ShuffledAnswers()
    {
        if (!IsValid) throw new InvalidOperationException("Invalid authored story beat: " + beatId);
        var answers = new string[distractors.Length + 1];
        answers[0] = correct;
        Array.Copy(distractors, 0, answers, 1, distractors.Length);
        for (int i = answers.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            string swap = answers[i]; answers[i] = answers[j]; answers[j] = swap;
        }
        return answers;
    }

    public bool TryGetConversation(out NpcQuestionConversation conversation)
    {
        conversation = null;
        return conversationJson != null && NpcQuestionConversation.TryParse(conversationJson.text, beatId, out conversation);
    }

    public static StoryBeatData Load(string id)
    {
        var data = Resources.Load<StoryBeatData>("Story/" + id);
        if (data != null && data.beatId == id && data.IsValid) return data;
        Debug.LogError("Missing or invalid story data in Data/Questions/Map1/Resources/Story: " + id);
        return null;
    }

}

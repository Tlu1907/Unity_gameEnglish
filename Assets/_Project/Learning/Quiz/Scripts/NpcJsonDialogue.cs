using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>The authored NPC JSON schema, independent of backend grading settings.</summary>
[Serializable]
public sealed class NpcJsonDialogue
{
    public string id, speaker;
    public Node[] dialogues;
    public Node[] conditional_dialogues;
    public TempleExercise[] exercises;

    [Serializable] public sealed class Vocabulary { public string word, meaning; }
    [Serializable] public sealed class Grammar { public string name_en, formula, example_en, note_vi; }
    [Serializable] public sealed class Trigger
    {
        public string player_intent, target_location;
        public bool requires_explicit_question;
    }
    [Serializable] public sealed class Node
    {
        public string id, type, dialogue_en, available_after, exercise_id, next_id;
        public bool wait_for_player_response, hidden_by_default;
        public Vocabulary[] vocabulary;
        public Grammar[] grammar;
        public Trigger trigger;
        public bool IsQuestion => type == "question";
        public string LearningText()
        {
            var text = new StringBuilder("TỪ VỰNG\n");
            if (vocabulary != null)
                foreach (var word in vocabulary)
                    if (word != null) text.Append(word.word).Append(": ").AppendLine(word.meaning);
            text.Append("\nNGỮ PHÁP\n");
            if (grammar != null)
                foreach (var rule in grammar)
                    if (rule != null)
                        text.AppendLine(rule.name_en).AppendLine(rule.formula)
                            .AppendLine(rule.example_en).AppendLine(rule.note_vi).AppendLine();
            return text.ToString().TrimEnd();
        }
    }

    public static bool TryParse(string json, out NpcJsonDialogue data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var parsed = JsonUtility.FromJson<NpcJsonDialogue>(json);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.id) || string.IsNullOrWhiteSpace(parsed.speaker) ||
                parsed.dialogues == null || parsed.dialogues.Length == 0) return false;
            var ids = new HashSet<string>();
            foreach (var node in parsed.dialogues)
                if (!ValidNode(node, ids) || (node.type != "dialogue" && node.type != "question") ||
                    node.wait_for_player_response != node.IsQuestion) return false;
            if (parsed.exercises != null)
            {
                var exerciseIds = new HashSet<string>();
                foreach (var exercise in parsed.exercises)
                    if (exercise == null || !exercise.IsValid || !exerciseIds.Add(exercise.id) ||
                        !Array.Exists(parsed.dialogues, node => node.id == exercise.source_dialogue_id && node.exercise_id == exercise.id)) return false;
                foreach (var node in parsed.dialogues)
                    if ((!string.IsNullOrEmpty(node.next_id) && !ids.Contains(node.next_id)) ||
                        (node.IsQuestion && !exerciseIds.Contains(node.exercise_id))) return false;
            }
            var spokenIds = new HashSet<string>(ids);
            if (parsed.conditional_dialogues != null)
                foreach (var node in parsed.conditional_dialogues)
                    if (!ValidNode(node, ids) || node.type != "conditional_dialogue" ||
                        !node.hidden_by_default || node.wait_for_player_response ||
                        string.IsNullOrEmpty(node.available_after) || !spokenIds.Contains(node.available_after) ||
                        node.trigger == null || string.IsNullOrWhiteSpace(node.trigger.player_intent) ||
                        string.IsNullOrWhiteSpace(node.trigger.target_location)) return false;
            data = parsed;
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    private static bool ValidNode(Node node, HashSet<string> ids) => node != null &&
        !string.IsNullOrWhiteSpace(node.id) && ids.Add(node.id) && !string.IsNullOrWhiteSpace(node.dialogue_en);

    public TempleExercise ExerciseFor(Node node) => exercises == null || node == null ? null
        : Array.Find(exercises, exercise => exercise.id == node.exercise_id);

    public Node FindFollowUp(string intent, string target, int reachedIndex)
    {
        if (conditional_dialogues == null) return null;
        foreach (var node in conditional_dialogues)
        {
            if (node.trigger.player_intent != intent || node.trigger.target_location != target) continue;
            for (int i = 0; i <= reachedIndex && i < dialogues.Length; i++)
                if (dialogues[i].id == node.available_after) return node;
        }
        return null;
    }
}

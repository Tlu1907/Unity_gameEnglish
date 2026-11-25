using System;
using UnityEngine;

/// <summary>One conversation. Playback progress belongs to the view, never this asset.</summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "EarthDefender/Dialogue Data")]
public sealed class DialogueData : ScriptableObject
{
    [Serializable]
    public sealed class Line
    {
        [TextArea(2, 5)] public string english;
        [TextArea(2, 5)] public string vietnamese;
        [TextArea(2, 5)] public string vocabulary;
        [TextArea(2, 5)] public string grammar;
    }

    public string speakerName;
    public Sprite portrait;
    public string continuePrompt = "Nhấn C để tiếp tục";
    public string closePrompt = "Nhấn C để đóng";
    public Line[] lines = Array.Empty<Line>();
    [Tooltip("Optional authored NPC conversation JSON. Uses UnityAI for question nodes.")]
    public TextAsset conversationJson;
    public bool IsValid => lines != null && lines.Length > 0 &&
        Array.TrueForAll(lines, line => line != null &&
            !string.IsNullOrWhiteSpace(line.english) && !string.IsNullOrWhiteSpace(line.vietnamese));
}

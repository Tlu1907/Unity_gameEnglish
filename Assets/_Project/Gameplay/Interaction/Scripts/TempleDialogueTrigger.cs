using UnityEngine;

/// <summary>The temple entrance uses the player's existing E / Talk button.</summary>
public sealed class TempleDialogueTrigger : MonoBehaviour
{
    public DialogueData dialogue;
    public NpcJsonDialoguePanel panel;
    public SchematicMinimap minimap;
    public bool IsAvailable => isActiveAndEnabled && dialogue != null && panel != null && !panel.IsOpen;
    public string SpeakerName => dialogue != null ? dialogue.speakerName : "Temple Guardian";
    public string LastReport { get; private set; }
    public bool TryTalk()
    {
        if (!IsAvailable || !GameplayState.CanAct) return false;
        return panel.TryOpen(dialogue, completed =>
        {
            if (this == null || !completed || string.IsNullOrWhiteSpace(panel.CompletedTempleReport)) return;
            LastReport = panel.CompletedTempleReport;
            PlayerPrefs.SetString("EarthDefender.TempleReport.v1", LastReport);
            PlayerPrefs.Save();
            if (minimap != null) minimap.SetObjectiveMode(false, "Trở lại kiểm lâm để báo cáo, sau đó đến cổng trong hố đất.");
        });
    }
}

using System.Collections;
using UnityEngine;

/// <summary>The temple entrance uses the player's existing E / Talk button.</summary>
public sealed class TempleDialogueTrigger : MonoBehaviour
{
    public DialogueData dialogue;
    public NpcJsonDialoguePanel panel;
    public SchematicMinimap minimap;
    [SerializeField] private Sprite rewardCrystal;
    private bool rewardInFlight;
    public bool IsAvailable => isActiveAndEnabled && dialogue != null && panel != null && !panel.IsOpen
        && StoryProgress.CanTalkTo(dialogue);
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
            BeginCrystalReward();
            if (minimap != null) minimap.SetObjectiveMode(false, "Trở lại kiểm lâm để báo cáo, sau đó đến cổng trong hố đất.");
        });
    }

    private void BeginCrystalReward()
    {
        if (rewardInFlight) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        var attack = player != null ? player.GetComponent<PlayerAttack>() : null;
        if (attack == null || attack.HasTempleBlessing) return;
        if (rewardCrystal == null)
        {
            Debug.LogWarning("Temple reward crystal sprite is missing.", this);
            return;
        }
        StartCoroutine(AbsorbCrystal(attack));
    }

    private IEnumerator AbsorbCrystal(PlayerAttack player)
    {
        rewardInFlight = true;
        var crystal = new GameObject("Temple Reward Crystal");
        var visual = crystal.AddComponent<SpriteRenderer>();
        visual.sprite = rewardCrystal;
        var playerVisual = player.GetComponentInChildren<SpriteRenderer>();
        if (playerVisual != null) visual.sortingLayerID = playerVisual.sortingLayerID;
        visual.sortingOrder = 32760;
        float scale = .9f / Mathf.Max(.01f, rewardCrystal.bounds.size.y);
        Vector3 start = transform.position + Vector3.up * 1.6f;
        crystal.transform.position = start;
        try
        {
            const float duration = 1.5f;
            float elapsed = 0;
            while (elapsed < duration)
            {
                if (player == null || player.GetComponent<Health>().IsDead) yield break;
                if (!GameplayState.CanAct) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                // Pause above the entrance briefly, then home toward the moving player.
                float flight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, 1, progress));
                Vector3 target = player.transform.position + Vector3.up * .5f;
                crystal.transform.position = Vector3.Lerp(start, target, flight)
                    + Vector3.up * Mathf.Sin(flight * Mathf.PI) * .6f;
                crystal.transform.localScale = Vector3.one * scale
                    * (1 + Mathf.Sin(progress * Mathf.PI) * .2f) * Mathf.Lerp(1, .1f, flight);
                yield return null;
            }
            if (player != null) player.GrantTempleBlessing();
        }
        finally
        {
            if (crystal != null) Destroy(crystal);
            rewardInFlight = false;
        }
    }
}

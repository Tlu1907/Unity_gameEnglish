#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class StoryTestingMenu
{
    [MenuItem("Tools/Story Testing/Skip Tower (Play Mode)")]
    private static void SkipTower()
    {
        if (!Application.isPlaying) return;
        if (!GameplayState.CanAct)
        {
            Debug.LogWarning("Close the current dialogue/quiz and resume gameplay before skipping the tower.");
            return;
        }
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (inventory == null)
        {
            Debug.LogWarning("Story test: no active Player with PlayerInventory was found.");
            return;
        }
        StoryProgress.Complete("q1_spirit");
        inventory.SetEquipped(true);
        StoryProgress.TryClearTowerEnemies();
        StoryProgress.TryReceiveArchaeologistSignal();
        foreach (var enemy in Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include))
            if (enemy.EncounterMode == EnemyEncounterMode.MirrorAmbush)
                enemy.gameObject.SetActive(false);
        Debug.Log("Tower skipped for this Play session. Go to the archaeologist, accept the mission and finish his dialogue, then touch the lab.");
    }

    [MenuItem("Tools/Story Testing/Skip Tower (Play Mode)", true)]
    private static bool CanSkipTower() => Application.isPlaying;
}
#endif

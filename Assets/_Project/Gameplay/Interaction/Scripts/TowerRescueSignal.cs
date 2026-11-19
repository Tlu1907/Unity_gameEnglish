using System.Collections.Generic;
using UnityEngine;

/// <summary>Linh's radio message opens automatically after the tower enemies are defeated.</summary>
[DisallowMultipleComponent]
public sealed class TowerRescueSignal : MonoBehaviour
{
    [Tooltip("All of these enemies must die. If empty, use this scene's tower-ambush enemies.")]
    [SerializeField] private EnemyBrain[] enemiesToDefeat;
    [SerializeField] private DialogueData linhRescueDialogue;
    [SerializeField] private NPCIntroDialogue linhDialogue;
    private readonly HashSet<EnemyBrain> remaining = new HashSet<EnemyBrain>();
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
    private bool initialized, configured, pending, openedThisVisit;
    private bool announcedAutomatically;
    public int RemainingEnemies => remaining.Count;

    private void Awake() => Initialize();
    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (enemiesToDefeat == null || enemiesToDefeat.Length == 0)
        {
            var group = new List<EnemyBrain>();
            foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (enemy.gameObject.scene == gameObject.scene && enemy.EncounterMode == EnemyEncounterMode.MirrorAmbush) group.Add(enemy);
            enemiesToDefeat = group.ToArray();
        }
        configured = enemiesToDefeat.Length > 0;
        foreach (var enemy in enemiesToDefeat)
        {
            if (enemy == null) { configured = false; continue; }
            if (!remaining.Add(enemy)) continue;
            enemy.Defeated += OnEnemyDefeated;
            Health health = enemy.GetComponent<Health>();
            if (health != null && health.IsDead) remaining.Remove(enemy);
        }
        UpdateAreaProgress();
    }
    private void OnEnemyDefeated(EnemyBrain enemy)
    {
        remaining.Remove(enemy);
        UpdateAreaProgress();
    }
    private void UpdateAreaProgress()
    {
        if (configured && remaining.Count == 0) StoryProgress.TryClearTowerEnemies();
    }
    private void OnTriggerEnter2D(Collider2D other) => Register(other);
    private void OnTriggerStay2D(Collider2D other) => Register(other);
    private void Register(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerInventory>() != null) contacts.Add(other);
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        contacts.Remove(other);
        if (contacts.Count == 0) openedThisVisit = false;
    }
    private void Update()
    {
        UpdateAreaProgress();
        contacts.RemoveWhere(contact => contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy);
        if (contacts.Count == 0) openedThisVisit = false;
        // Announce once anywhere; a cancelled message can be replayed at the tower.
        if (announcedAutomatically && contacts.Count == 0) return;
        if (pending || openedThisVisit || !configured || remaining.Count != 0 || !StoryProgress.HasClearedTowerEnemies ||
            StoryProgress.HasReceivedArchaeologistSignal || !GameplayState.CanAct) return;
        if (linhRescueDialogue == null || !linhRescueDialogue.IsValid) return;
        if (linhDialogue == null) linhDialogue = FindAnyObjectByType<NPCIntroDialogue>(FindObjectsInactive.Include);
        if (linhDialogue == null) return;
        pending = openedThisVisit = true;
        if (!linhDialogue.TryShowDialogue(linhRescueDialogue, OnMessageClosed)) pending = openedThisVisit = false;
        else announcedAutomatically = true;
    }
    private void OnMessageClosed(bool readToEnd)
    {
        if (this == null) return;
        pending = false;
        if (readToEnd) StoryProgress.TryReceiveArchaeologistSignal();
    }
    private void OnDisable() { contacts.Clear(); openedThisVisit = false; }
    private void OnDestroy()
    {
        if (enemiesToDefeat == null) return;
        foreach (var enemy in enemiesToDefeat) if (enemy != null) enemy.Defeated -= OnEnemyDefeated;
    }
}

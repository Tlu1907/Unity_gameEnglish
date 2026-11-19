using System.Collections.Generic;
using UnityEngine;

/// <summary>Contact with the tower starts its story. The tower stays in the map after completion.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TowerDialogueTrigger : MonoBehaviour
{
    [SerializeField] private string beatId = "q1_spirit";
    [SerializeField] private StoryQuizPanel dialoguePanel;
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
    private PlayerInventory rewardRecipient;
    private bool openedThisVisit;
    private bool pending;
    private bool rewardGranted;
    private float retryAt;
    public bool IsCompleted => rewardGranted || StoryProgress.IsCompleted(beatId);

    private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;
    private void OnTriggerEnter2D(Collider2D other) => Register(other);
    private void OnTriggerStay2D(Collider2D other) => Register(other);
    private void Register(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerInventory>() == null) return;
        contacts.Add(other);
        TryStartDialogue();
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        contacts.Remove(other);
        if (contacts.Count == 0) openedThisVisit = false;
    }
    private void Update()
    {
        contacts.RemoveWhere(contact => contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy);
        if (contacts.Count == 0) { openedThisVisit = false; return; }
        TryStartDialogue();
    }
    private void TryStartDialogue()
    {
        if (pending || openedThisVisit || IsCompleted || !GameplayState.CanAct || Time.unscaledTime < retryAt) return;
        rewardRecipient = null;
        foreach (Collider2D contact in contacts)
        {
            if (contact == null) continue;
            rewardRecipient = contact.GetComponentInParent<PlayerInventory>();
            if (rewardRecipient != null) break;
        }
        if (rewardRecipient == null || !StoryProgress.CanStart(beatId)) return;
        StoryBeatData beat = StoryBeatData.Load(beatId);
        StoryQuizPanel panel = dialoguePanel != null ? dialoguePanel : StoryQuizPanel.FindOrCreate();
        if (beat == null || panel == null) { retryAt = Time.unscaledTime + 1f; return; }
        openedThisVisit = pending = true;
        if (!panel.TryOpen(beat, Complete))
        {
            openedThisVisit = pending = false;
            retryAt = Time.unscaledTime + .5f;
        }
    }
    private void Complete(bool correct)
    {
        if (this == null) return;
        pending = false;
        if (!correct || rewardGranted) return;
        rewardGranted = true;
        if (rewardRecipient != null) rewardRecipient.SetEquipped(true);
    }
    private void OnDisable()
    {
        contacts.Clear();
        openedThisVisit = false;
    }
}

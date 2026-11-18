using System.Collections.Generic;
using UnityEngine;

public sealed class NPCDialogueTrigger : MonoBehaviour
{
    [SerializeField] private NPCIntroDialogue dialogueUI;
    [SerializeField] private DialogueData dialogueData;
    [SerializeField] private bool onlyOnce = true;

    private readonly HashSet<Collider2D> contacts =
        new HashSet<Collider2D>();

    private bool played;


    private void Start()
    {
        if (dialogueUI == null || dialogueData == null || !dialogueData.IsValid)
        {
            Debug.LogWarning("NPCDialogueTrigger: assign Dialogue UI and Dialogue Data with non-empty English and Vietnamese lines.", this);
            enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMovement>() != null)
            contacts.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        contacts.Remove(other);


    
    }

    public bool CanTalk => isActiveAndEnabled && !(onlyOnce && played) && dialogueUI != null && dialogueData != null;
    public string SpeakerName => dialogueData != null ? dialogueData.speakerName : "NPC";
    public bool TryTalk()
    {
        if (!CanTalk || !GameplayState.CanAct || dialogueUI.IsOpen) return false;

        dialogueUI.ShowDialogue(dialogueData);

        if (dialogueUI.IsOpen)
        {
            played = true;

            return true;
        }
        return false;
    }

    private void OnDisable()
    {
        contacts.Clear();

    }
}


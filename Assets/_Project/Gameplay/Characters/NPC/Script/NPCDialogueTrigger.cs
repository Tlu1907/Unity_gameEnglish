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
    private bool activatedThisVisit;

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

        if (contacts.Count == 0)
            activatedThisVisit = false;
    }

    private void Update()
    {
        contacts.RemoveWhere(c =>
            c == null || !c.enabled || !c.gameObject.activeInHierarchy);

        if (contacts.Count == 0)
        {
            activatedThisVisit = false;
            return;
        }

        if (activatedThisVisit || (onlyOnce && played) ||
            !GameplayState.CanAct || dialogueUI == null ||
            dialogueData == null)
            return;

        dialogueUI.ShowDialogue(dialogueData);

        if (dialogueUI.IsOpen)
        {
            played = true;
            activatedThisVisit = true;
        }
    }

    private void OnDisable()
    {
        contacts.Clear();
        activatedThisVisit = false;
    }
}

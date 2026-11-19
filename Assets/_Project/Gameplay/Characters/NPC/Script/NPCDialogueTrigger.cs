using System.Collections.Generic;
using UnityEngine;

public sealed class NPCDialogueTrigger : MonoBehaviour
{
    [SerializeField] private NPCIntroDialogue dialogueUI;
    [SerializeField] private DialogueData dialogueData;
    [SerializeField] private bool onlyOnce = true;
    [SerializeField] private string beatId;
    [SerializeField] private StoryQuizPanel dialoguePanel;
    private StoryBeatData storyBeat;
    private bool pending;
    public bool IsMissionNpc => !string.IsNullOrEmpty(beatId);
    public string MissionBeatId => beatId;
    private StoryBeatData Beat
    {
        get
        {
            if (storyBeat == null || storyBeat.beatId != beatId) storyBeat = StoryBeatData.Load(beatId);
            return storyBeat;
        }
    }

    private readonly HashSet<Collider2D> contacts =
        new HashSet<Collider2D>();

    private bool played;


    private void Start()
    {
        if (!string.IsNullOrEmpty(beatId))
        {
            if (Beat == null || !Beat.IsValid) { Debug.LogWarning("NPC story beat is missing or invalid: " + beatId, this); enabled = false; }
            return;
        }
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

    public bool CanTalk => isActiveAndEnabled && !pending && !(onlyOnce && played) &&
        (!string.IsNullOrEmpty(beatId) ? StoryProgress.CanStart(beatId) : dialogueUI != null && dialogueData != null);
    public string SpeakerName => !string.IsNullOrEmpty(beatId) ? Beat != null ? Beat.speaker : "NPC" : dialogueData != null ? dialogueData.speakerName : "NPC";
    public bool TryTalk()
    {
        if (!CanTalk || !GameplayState.CanAct) return false;
        if (!string.IsNullOrEmpty(beatId))
        {
            if (dialoguePanel == null) dialoguePanel = StoryQuizPanel.FindOrCreate();
            if (dialoguePanel == null || Beat == null) return false;
            pending = true;
            if (dialoguePanel.TryOpen(Beat, completed => { pending = false; if (completed) played = true; })) return true;
            pending = false;
            return false;
        }
        if (dialogueUI.IsOpen) return false;

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
        if (pending && dialoguePanel != null && dialoguePanel.IsOpen) dialoguePanel.Close();
        pending = false;
        contacts.Clear();

    }
}


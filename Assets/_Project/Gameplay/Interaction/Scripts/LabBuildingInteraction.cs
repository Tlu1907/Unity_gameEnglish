using System.Collections.Generic;
using UnityEngine;

/// <summary>Contact with the solid lab opens its scored document in the existing story UI.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class LabBuildingInteraction : MonoBehaviour
{
    [SerializeField] private TextAsset questionData;
    [SerializeField] private StoryQuizPanel dialoguePanel;
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
    private bool pending, openedThisVisit;
    private float retryAt;
    private ResearchDocument.Document document;
    [SerializeField, TextArea] private string interactionStatus;
    public string InteractionStatus => interactionStatus;
    private void Reset()
    {
#if UNITY_EDITOR
        questionData = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/_Project/Data/Questions/Map1/Json/research_document_questions_en.json");
#endif
        // Keep the building's existing solid collider shape and trigger setting.
    }
    private void OnCollisionEnter2D(Collision2D collision) => Register(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => Register(collision.collider);
    private void OnCollisionExit2D(Collision2D collision) => Exit(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => Register(other);
    private void OnTriggerStay2D(Collider2D other) => Register(other);
    private void OnTriggerExit2D(Collider2D other) => Exit(other);
    private void Register(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerInventory>() == null) return;
        contacts.Add(other);
        TryOpen();
    }
    private void Exit(Collider2D other)
    {
        contacts.Remove(other);
        if (contacts.Count == 0) openedThisVisit = false;
    }
    private void Update()
    {
        contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (contacts.Count == 0) { openedThisVisit = false; interactionStatus = "Waiting for Player to touch the lab collider."; return; }
        TryOpen();
    }
    public void TryOpen()
    {
        if (!GameplayState.CanAct) interactionStatus = "Gameplay is currently " + GameplayState.Current;
        if (!isActiveAndEnabled || pending || openedThisVisit || StoryProgress.HasDecodedResearchDocuments ||
            !GameplayState.CanAct || Time.unscaledTime < retryAt) return;
        if (!StoryProgress.TryEnterResearchRoom())
        {
            interactionStatus = "Finish the archaeologist dialogue: accept the mission and close its final page first.";
            return;
        }
        if (!ResearchDocument.TryParse(questionData, out document))
        {
            Debug.LogWarning("LabBuildingInteraction: assign the five-question research document JSON.", this);
            interactionStatus = "Question Data is missing or invalid.";
            retryAt = Time.unscaledTime + 5;
            return;
        }
        var panel = dialoguePanel != null ? dialoguePanel : StoryQuizPanel.FindOrCreate();
        if (panel == null) { interactionStatus = "No StoryQuizPanel found."; retryAt = Time.unscaledTime + 2; return; }
        openedThisVisit = pending = true;
        if (!panel.TryOpenResearchDocument(document, Complete))
        {
            openedThisVisit = pending = false;
            retryAt = Time.unscaledTime + 1;
            interactionStatus = "Story UI could not open. Check StoryQuizPanel references and enabled state.";
        }
        else interactionStatus = "Research dialogue open.";
    }
    private void Complete(int correct)
    {
        if (this == null) return;
        pending = false;
        if (correct < 0) return;
        StoryProgress.TryCompleteResearchDocument(correct,
            string.IsNullOrWhiteSpace(document.destinationVietnamese) ? document.destination : document.destinationVietnamese);
    }
    private void OnDisable() { contacts.Clear(); openedThisVisit = false; }
}

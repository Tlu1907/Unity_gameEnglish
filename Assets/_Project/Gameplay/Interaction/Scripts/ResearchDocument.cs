using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Five single-attempt questions. Results are committed only after the final page closes.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ResearchDocument : MonoBehaviour, IInteractable
{
    [Serializable] public sealed class Vocabulary
    {
        public string word;
        public string meaning;
    }
    [Serializable] public sealed class Question
    {
        public string text;
        public string vietnamese;
        public string[] answers;
        public int correctIndex;
        public string explanation;
        public string explanationVietnamese;
        public Vocabulary[] vocabulary;
        public string grammar;
        public string grammarId, groupId;
    }
    [Serializable] public sealed class Document
    {
        public bool showImage;
        public string title, introduction, destination;
        public string introductionVietnamese, destinationVietnamese;
        public Vocabulary[] introductionVocabulary, destinationVocabulary;
        public string introductionGrammar, destinationGrammar;
        public string introductionGrammarId, introductionGroupId;
        public string destinationGrammarId, destinationGroupId;
        public Question[] questions;
    }
    [SerializeField] private TextAsset questionData;
    [SerializeField] private Sprite documentSprite;
    [SerializeField] private ResearchDocumentView documentView;
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
    private Document data;
    private int index, correct;
    private bool open, answered, completed;
    private float previousTimeScale;
    public Document Content => data;
    public int QuestionIndex => index;
    public bool HasAnswered => answered;
    public int CorrectAnswers => correct;
    public bool IsOpen => open;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        var visual = GetComponent<SpriteRenderer>();
        if (visual == null) visual = gameObject.AddComponent<SpriteRenderer>();
        if (documentSprite != null) visual.sprite = documentSprite;
        else if (visual.sprite == null)
        {
            visual.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 4);
            visual.color = new Color(1f, .92f, .7f);
        }
        visual.sortingOrder = 10;
    }
    private void OnTriggerEnter2D(Collider2D other) => Register(other);
    private void OnTriggerStay2D(Collider2D other) => Register(other);
    private void Register(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerInventory>() != null) contacts.Add(other);
    }
    private void OnTriggerExit2D(Collider2D other) => contacts.Remove(other);
    private void Update()
    {
        contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (open && (GameplayState.Current != GameplayMode.Dialogue || Input.GetKeyDown(KeyCode.Escape))) Close();
        else if (!open && contacts.Count > 0 && Input.GetKeyDown(KeyCode.E)) Interact();
    }
    public void Interact()
    {
        if (open || completed || StoryProgress.HasDecodedResearchDocuments || !GameplayState.CanAct) return;
        if (!StoryProgress.TryEnterResearchRoom()) return;
        if (!TryReadData()) return;
        if (documentView == null) documentView = FindAnyObjectByType<ResearchDocumentView>(FindObjectsInactive.Include);
        if (documentView == null || !documentView.IsConfigured)
        {
            Debug.LogWarning("ResearchDocument: create and assign a ResearchDocumentView in Edit Mode.", this);
            return;
        }
        if (!GameplayState.BeginDialogue(this)) return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0;
        index = correct = 0;
        answered = false;
        open = true;
        documentView.Show(this);
    }
    private bool TryReadData()
    {
        if (TryParse(questionData, out data)) return true;
        return InvalidData();
    }
    public static bool TryParse(TextAsset source, out Document document)
    {
        try { document = source != null ? JsonUtility.FromJson<Document>(source.text) : null; }
        catch (ArgumentException) { document = null; }
        if (document == null || document.questions == null || document.questions.Length != 5 ||
            string.IsNullOrWhiteSpace(document.title) || string.IsNullOrWhiteSpace(document.destination)) return false;
        foreach (var q in document.questions)
        {
            if (q == null || string.IsNullOrWhiteSpace(q.text) || q.answers == null || q.answers.Length < 2 ||
                q.answers.Length > 4 || q.correctIndex < 0 || q.correctIndex >= q.answers.Length ||
                string.IsNullOrWhiteSpace(q.explanation)) return false;
            var choices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string choice in q.answers)
                if (string.IsNullOrWhiteSpace(choice) || !choices.Add(choice.Trim())) return false;
        }
        return true;
    }
    private bool InvalidData()
    {
        Debug.LogError("ResearchDocument requires valid JSON with exactly five questions and a destination.", this);
        return false;
    }
    public void Choose(int choice)
    {
        if (!open || answered || index >= 5 || GameplayState.Current != GameplayMode.Dialogue) return;
        var q = data.questions[index];
        if (choice < 0 || choice >= q.answers.Length) return;
        if (choice == q.correctIndex) correct++;
        answered = true;
        documentView?.Refresh();
    }
    public void Advance()
    {
        if (!open || !answered || index >= 5) return;
        index++;
        answered = false;
        documentView.Show(this);
    }
    public void Finish()
    {
        if (!open || index != 5 || GameplayState.Current != GameplayMode.Dialogue) return;
        // Restore gameplay before the ambush signal, so enemies never attack behind the document.
        Close();
        completed = StoryProgress.TryCompleteResearchDocument(correct, data.destination);
    }
    public void Close()
    {
        if (!open) return;
        open = false;
        documentView?.Hide();
        Time.timeScale = previousTimeScale;
        GameplayState.EndDialogue(this);
    }
    private void OnDisable() { Close(); contacts.Clear(); }
}

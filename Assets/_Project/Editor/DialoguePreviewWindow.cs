using UnityEditor;
using UnityEngine;

/// <summary>Play Mode previews using the actual authored dialogue UI and JSON data.</summary>
public sealed class DialoguePreviewWindow : EditorWindow
{
    private static readonly string[] Sources = { "Tower", "Archaeologist", "Research Document" };
    private static readonly string[] Files = { "map1_spirit_questions_en.json", "map1_archaeologist_dialogue.json", "research_document_questions_en.json" };
    private static readonly string[] Pages = { "Introduction", "Consent", "Consent Reply", "Question", "Correct Feedback", "Wrong Feedback", "Completion" };
    [SerializeField] private int sourceIndex;
    [SerializeField] private int pageIndex;
    [SerializeField] private int entryIndex = 1;
    private string status;

    [MenuItem("Tools/Story Testing/Dialogue Preview")]
    public static void Open() => GetWindow<DialoguePreviewWindow>("Dialogue Preview");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Quick Dialogue Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Preview any JSON page without completing missions. Uses the scene's existing dialogue layout. Preview does not add learning notes or complete story missions.", MessageType.Info);
        sourceIndex = EditorGUILayout.Popup("Dialogue", sourceIndex, Sources);
        pageIndex = EditorGUILayout.Popup("Page", pageIndex, Pages);
        entryIndex = Mathf.Max(1, EditorGUILayout.IntField("Question / reply number", entryIndex));
        EditorGUILayout.HelpBox("Numbers start at 1. Consent and replies apply to NPC dialogue; research documents have introduction, questions and completion.", MessageType.None);
        if (!EditorApplication.isPlaying)
        {
            if (GUILayout.Button("Enter Play Mode")) EditorApplication.isPlaying = true;
            return;
        }
        if (GUILayout.Button("Show Selected Page")) ShowPage();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Previous Question")) { entryIndex = Mathf.Max(1, entryIndex - 1); pageIndex = 3; ShowPage(); }
            if (GUILayout.Button("Next Question")) { entryIndex++; pageIndex = 3; ShowPage(); }
        }
        if (GUILayout.Button("Close Preview"))
            foreach (var panel in Object.FindObjectsByType<StoryQuizPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (panel.IsOpen) panel.Close();
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
    }

    private void ShowPage()
    {
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Questions/Map1/Json/" + Files[sourceIndex]);
        if (source == null) { status = "Dialogue JSON was not found."; return; }
        string requestedPage = Pages[pageIndex];
        string notice = "";
        if (sourceIndex != 2 && NpcQuestionConversation.TryParse(source.text, null, out var data))
        {
            if (pageIndex >= 3 && pageIndex <= 5 && data.questions.Length == 0)
            {
                requestedPage = "Introduction";
                notice = " This dialogue has no questions; showing its introduction instead.";
            }
            else if ((pageIndex == 1 || pageIndex == 2) && data.consent == null)
            {
                requestedPage = "Introduction";
                notice = " This dialogue has no consent page; showing its introduction instead.";
            }
        }
        // Close through each view's lifecycle so its state ownership and time scale are released.
        foreach (var journal in Object.FindObjectsByType<StoryClueJournal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            journal.Close();
        foreach (var intro in Object.FindObjectsByType<NPCIntroDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (intro.IsOpen) intro.Close();
        foreach (var existing in Object.FindObjectsByType<StoryQuizPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (existing.IsOpen) existing.Close();
        if (QuizController.Instance != null) QuizController.Instance.CloseQuestion();
        GameplayState.Resume();
        var panel = StoryQuizPanel.FindOrCreate();
        bool shown = panel != null && panel.PreviewPage(source, sourceIndex == 2, requestedPage, entryIndex - 1);
        status = shown ? "Showing " + Sources[sourceIndex] + " / " + requestedPage + "." + notice
            : "Cannot open preview. Gameplay state: " + GameplayState.Current + ". Check the Console for missing UI references or JSON errors.";
    }
}

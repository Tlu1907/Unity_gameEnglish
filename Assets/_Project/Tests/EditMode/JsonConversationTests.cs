using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class JsonConversationTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject root;
    private float timeScale;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static object Static(string type, string method, params object[] args) => Runtime(type).GetMethod(method).Invoke(null, args);
    private static TextAsset Source => UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Questions/Map1/Json/map1_spirit_questions_en.json");
    private static bool Completed => (bool)Static("StoryProgress", "IsCompleted", "q1_spirit");
    private Component Panel()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/Prefabs/Resources/StoryDialogue.prefab");
        var copy = UnityEngine.Object.Instantiate(prefab, root.transform);
        var panel = copy.GetComponentInChildren(Runtime("StoryQuizPanel"), true);
        Call(panel, "OnEnable");
        return panel;
    }
    [SetUp] public void SetUp()
    {
        timeScale = Time.timeScale;
        Static("StoryProgress", "Reset"); Static("GameplayState", "Reset");
        Static("LearningJournal", "Reset");
        root = new GameObject("JSON conversation tests");
    }
    [TearDown] public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        Static("StoryProgress", "Reset"); Static("GameplayState", "Reset");
        Static("LearningJournal", "Reset");
        Time.timeScale = timeScale;
    }
    private static object Parse(string json, string id)
    {
        object[] args = { json, id, null };
        bool valid = (bool)Runtime("NpcQuestionConversation").GetMethod("TryParse").Invoke(null, args);
        return valid ? args[2] : null;
    }
    private static object[] Questions(object conversation)
    {
        Array source = (Array)Field(conversation, "questions");
        var result = new object[source.Length]; source.CopyTo(result, 0); return result;
    }
    private static int CorrectIndex(Component panel)
    {
        int index = (int)Field(panel, "questionIndex");
        object question = Questions(Field(panel, "conversation"))[index];
        return Array.IndexOf((string[])Field(panel, "answers"), (string)Field(question, "correct"));
    }
    private static void Agree(Component panel)
    { Call(panel, "Advance"); Call(panel, "Choose", 0); Call(panel, "Advance"); }

    [Test] public void JournalRecordsSeenPagesWithoutTextWidgetAndDeduplicatesReplaysAcrossMaps()
    {
        Component panel = Panel();
        // The saved prefab need not contain a Vocab_Gammar widget for recording to work.
        Assert.That(Call(panel, "TryOpenConversation", Source, null, null), Is.True);
        var maps = (System.Collections.IList)Runtime("LearningJournal").GetProperty("Maps").GetValue(null);
        Assert.That(maps.Count, Is.EqualTo(1));
        var map = maps[0];
        var grammar = (System.Collections.IList)map.GetType().GetProperty("Grammar").GetValue(map);
        var words = (System.Collections.IList)map.GetType().GetProperty("Vocabulary").GetValue(map);
        Assert.That(grammar.Count, Is.EqualTo(1));
        Assert.That(words, Does.Contain("spirit: linh hồn"));
        Assert.That(words, Does.Not.Contain("blade: lưỡi kiếm"), "Unseen question pages must not be recorded.");
        Call(panel, "Close");
        int count = words.Count;
        Assert.That(Call(panel, "TryOpenConversation", Source, null, null), Is.True);
        Assert.That(words.Count, Is.EqualTo(count));
        Assert.That(grammar.Count, Is.EqualTo(1));
        Call(panel, "Close");
        Static("LearningJournal", "Record", "map2", "Màn 2 · Rừng", null, "There is + N");
        Assert.That(maps.Count, Is.EqualTo(2));
        string book = (string)Static("LearningJournal", "BuildBook");
        Assert.That(book, Does.Contain("NGỮ PHÁP"));
        Assert.That(book, Does.Contain("TỪ VỰNG"));
        Assert.That(book, Does.Contain("Màn 2 · Rừng"));
        Assert.That(book, Does.Contain("spirit: linh hồn"));
        Static("LearningJournal", "Reset");
        Assert.That(maps.Count, Is.Zero);
    }

    [Test] public void AuthoredJournalPaginatesLongContentAndClampsNavigation()
    {
        var panel = Panel();
        var journal = panel.transform.parent.GetComponent(Runtime("StoryClueJournal"));
        Call(journal, "OnEnable");
        string longFormula = string.Join("\n", new string[90]).Replace("\n", "A long grammar explanation for pagination.\n");
        Static("LearningJournal", "Record", "map1", "Màn 1", null, longFormula);
        Assert.That(Call(panel, "TryOpenConversation", Source, null, null), Is.True);
        Call(panel, "Close");
        Call(journal, "Toggle");
        int pages = (int)journal.GetType().GetProperty("PageCount").GetValue(journal);
        Assert.That(pages, Is.GreaterThan(2));
        Call(journal, "NextPage");
        var wordsText = (Component)Field(journal, "entries");
        var grammarText = (Component)Field(journal, "grammarEntries");
        string Content(Component text) => (string)text.GetType().GetProperty("text").GetValue(text);
        Assert.That(wordsText, Is.Not.SameAs(grammarText));
        Assert.That(Content(wordsText), Does.Contain("TỪ VỰNG\n\n"));
        Assert.That(Content(wordsText), Does.Contain("spirit: linh hồn\n"));
        Assert.That(Content(wordsText), Does.Not.Contain("NGỮ PHÁP"));
        Assert.That(Content(grammarText), Does.Contain("NGỮ PHÁP\n\n"));
        Assert.That(Content(grammarText), Does.Not.Contain("spirit: linh hồn"));
        for (int i = 0; i < pages + 2; i++) Call(journal, "NextPage");
        Assert.That(journal.GetType().GetProperty("CurrentPage").GetValue(journal), Is.EqualTo(pages));
        Assert.That(Content(wordsText), Is.Empty, "Do not repeat vocabulary on extra grammar pages.");
        for (int i = 0; i < pages + 2; i++) Call(journal, "PreviousPage");
        Assert.That(journal.GetType().GetProperty("CurrentPage").GetValue(journal), Is.EqualTo(1));
        Assert.That(Field(journal, "pageLabel"), Is.Not.Null);
        Assert.That(Field(journal, "nextButton"), Is.Not.Null);
        Assert.That(Field(journal, "previousButton"), Is.Not.Null);
        Call(journal, "Close");
        Call(journal, "OnDisable");
    }

    [TestCase("map1_spirit_questions_en.json")]
    [TestCase("map1_archaeologist_dialogue.json")]
    public void LearningNotesFollowConsentRepliesQuestionsAndCompletion(string filename)
    {
        Component panel = Panel();
        var textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
        var notesObject = new GameObject("Vocab_Gammar", typeof(RectTransform), textType);
        notesObject.transform.SetParent(panel.transform, false);
        notesObject.SetActive(false);
        var text = notesObject.GetComponent(textType);
        string Notes() => (string)textType.GetProperty("text").GetValue(text);
        var source = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/_Project/Data/Questions/Map1/Json/" + filename);
        Assert.That(Call(panel, "TryOpenConversation", source, null, null), Is.True);
        var conversation = Field(panel, "conversation");
        void AssertNotes(object page)
        {
            // Learning fields on Question/Consent are inherited from Dialogue.
            Assert.That(Notes(), Does.Contain((string)Field(page, "grammar")));
            Array words = (Array)Field(page, "vocabulary");
            Assert.That(Notes(), Does.Contain((string)Field(words.GetValue(0), "word")));
            Assert.That(notesObject.activeSelf, Is.True);
        }
        AssertNotes(Field(conversation, "introduction"));
        Call(panel, "Advance"); AssertNotes(Field(conversation, "consent"));
        var choices = (Array)Field(Field(conversation, "consent"), "choices");
        Call(panel, "Choose", 1); AssertNotes(choices.GetValue(1));
        Call(panel, "Advance"); AssertNotes(Field(conversation, "consent"));
        Call(panel, "Choose", 0); AssertNotes(choices.GetValue(0));
        Call(panel, "Advance");
        foreach (var question in Questions(conversation))
        {
            AssertNotes(question);
            Call(panel, "Choose", CorrectIndex(panel));
            if (Field(panel, "step").ToString() != "Success") Call(panel, "Advance");
        }
        AssertNotes(Field(conversation, "completion"));
        Call(panel, "Close");
    }

    [Test] public void JsonPreservesAllSixAnswersAndVariableChoiceCountsAndRejectsInvalidData()
    {
        Assert.That(Source, Is.Not.Null);
        object parsed = Parse(Source.text, "q1_spirit");
        Assert.That(parsed, Is.Not.Null);
        object[] questions = Questions(parsed);
        Assert.That(questions, Has.Length.EqualTo(6));
        string[] expected = { "sword", "shield", "am", "is", "are", "The gate is closed." };
        for (int i = 0; i < questions.Length; i++)
        {
            Assert.That(Field(questions[i], "correct"), Is.EqualTo(expected[i]));
            string[] shuffled = (string[])Call(questions[i], "ShuffledAnswers");
            Assert.That(shuffled, Has.Length.EqualTo(i < 2 ? 4 : 3));
            Assert.That(shuffled, Does.Contain(expected[i]));
        }
        Assert.That(Parse(Source.text, "wrong_npc"), Is.Null);
        Assert.That(Parse("{bad json}", "q1_spirit"), Is.Null);
        Assert.That(Parse(Source.text.Replace("\"correct\": \"sword\"", "\"correct\": \"key\""), "q1_spirit"), Is.Null);
    }

    [Test] public void ConsentLoopAndSixQuestionsAwardOnlyAfterFullCompletion()
    {
        Component panel = Panel();
        int callbacks = 0; bool awarded = false;
        var beat = (ScriptableObject)Static("StoryBeatData", "Load", "q1_spirit");
        Assert.That(Call(panel, "TryOpen", beat, (Action<bool>)(correct => { callbacks++; awarded = correct; }), null), Is.True);
        Call(panel, "Advance"); Call(panel, "Choose", 1);
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("ConsentReply"));
        Call(panel, "Advance");
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Consent"));
        Call(panel, "Choose", 0); Call(panel, "Advance");
        for (int i = 0; i < 6; i++)
        {
            Assert.That(Field(panel, "questionIndex"), Is.EqualTo(i));
            int correct = CorrectIndex(panel);
            Call(panel, "Choose", (correct + 1) % ((string[])Field(panel, "answers")).Length);
            Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Question"));
            Call(panel, "Choose", correct);
            Assert.That(callbacks, Is.Zero); Assert.That(Completed, Is.False);
            if (i < 5) { Assert.That(Field(panel, "step").ToString(), Is.EqualTo("QuestionFeedback")); Call(panel, "Advance"); }
        }
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Success"));
        Call(panel, "Close"); Call(panel, "Close");
        Assert.That(callbacks, Is.EqualTo(1)); Assert.That(awarded, Is.True); Assert.That(Completed, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(timeScale));
    }

    [Test] public void PartialCompletionDoesNotEquipOrAdvanceTheStory()
    {
        Component panel = Panel(); bool rewarded = true;
        Assert.That(Call(panel, "TryOpen", Static("StoryBeatData", "Load", "q1_spirit"), (Action<bool>)(value => rewarded = value), null), Is.True);
        Agree(panel); Call(panel, "Choose", CorrectIndex(panel)); Call(panel, "Close");
        Assert.That(rewarded, Is.False); Assert.That(Completed, Is.False);
    }

    [Test] public void OtherNpcsReuseTheSameViewWithoutMapOneProgressKeys()
    {
        Component panel = Panel(); bool completed = false;
        var json = new TextAsset(Source.text.Replace("q1_spirit", "npc_custom"));
        try
        {
            Assert.That(Call(panel, "TryOpenConversation", json, (Action<bool>)(value => completed = value), null), Is.True);
            Agree(panel);
            for (int i = 0; i < 6; i++) { Call(panel, "Choose", CorrectIndex(panel)); if (i < 5) Call(panel, "Advance"); }
            Call(panel, "Close");
            Assert.That(completed, Is.True); Assert.That(Completed, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(json); }
    }

    [Test] public void AuthoredFrameMatchesLinhAndKeepsChoicesSeparate()
    {
        var preview = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sence_1/Scene1.unity");
        try
        {
            Component linh = null;
            foreach (var go in preview.GetRootGameObjects())
            { linh = go.GetComponentInChildren(Runtime("NPCIntroDialogue"), true); if (linh != null) break; }
            var panel = Panel();
            var expected = ((GameObject)Field(linh, "dialoguePanel")).GetComponent<RectTransform>();
            var actual = (RectTransform)panel.transform;
            Assert.That(actual.anchorMin, Is.EqualTo(expected.anchorMin));
            Assert.That(actual.anchorMax, Is.EqualTo(expected.anchorMax));
            Assert.That(actual.sizeDelta, Is.EqualTo(expected.sizeDelta));
            Assert.That(panel.transform.Find("Choices"), Is.Not.Null);
            Assert.That(Field(panel, "advanceButton"), Is.Null); Assert.That(Field(panel, "closeButton"), Is.Null);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
    }
}

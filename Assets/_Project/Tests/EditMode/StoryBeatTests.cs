using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StoryBeatTests
{
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject root;
    private float originalTimeScale;
    private readonly System.Collections.Generic.List<ScriptableObject> copies = new System.Collections.Generic.List<ScriptableObject>();
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Instance).Invoke(target, args);
    private static object Static(string type, string method, params object[] args) =>
        Runtime(type).GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
    private static object Field(object target, string name) => target.GetType().GetField(name, Instance).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Instance).SetValue(target, value);
    private static bool Completed(string id) => (bool)Static("StoryProgress", "IsCompleted", id);
    private static string State => Runtime("GameplayState").GetProperty("Current").GetValue(null).ToString();
    private ScriptableObject Beat(string id)
    {
        var copy = UnityEngine.Object.Instantiate((ScriptableObject)Static("StoryBeatData", "Load", id));
        Set(copy, "conversationJson", null); // Keep legacy fallback coverage separate from the JSON sequence tests.
        copies.Add(copy);
        return copy;
    }

    [SetUp]
    public void SetUp()
    {
        originalTimeScale = Time.timeScale;
        Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
        root = new GameObject("Story tests", typeof(RectTransform));
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        foreach (var copy in copies) UnityEngine.Object.DestroyImmediate(copy);
        copies.Clear();
        Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
        Time.timeScale = originalTimeScale;
    }

    private Component Panel()
    {
        var go = new GameObject("Story panel", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        go.SetActive(false);
        Component panel = go.AddComponent(Runtime("StoryQuizPanel"));
        Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
        Component Text(string name)
        {
            var text = new GameObject(name, typeof(RectTransform), textType);
            text.transform.SetParent(go.transform, false);
            return text.GetComponent(textType);
        }
        Call(panel, "Configure", Text("Name"), Text("English"), Text("Vietnamese"), Text("Prompt"));
        return panel;
    }

    [TestCase("q1_spirit", "sword", "key", "chest", "shield")]
    [TestCase("q2_archaeologist", "bridge", "school", "bank", "hospital")]
    public void AuthoredQuestionsValidateAndShuffleWithoutChangingAnswers(string id, string correct, string a, string b, string c)
    {
        var beat = Beat(id);
        Assert.That(beat, Is.Not.Null);
        Assert.That(Field(beat, "correct"), Is.EqualTo(correct));
        for (int i = 0; i < 20; i++)
            CollectionAssert.AreEquivalent(new[] { correct, a, b, c }, (string[])Call(beat, "ShuffledAnswers"));
        var invalid = UnityEngine.Object.Instantiate(beat);
        try
        {
            Set(invalid, "correct", "");
            Assert.That(invalid.GetType().GetProperty("IsValid").GetValue(invalid), Is.False);
            Set(invalid, "correct", correct);
            Set(invalid, "distractors", new[] { a, a, b });
            Assert.That(invalid.GetType().GetProperty("IsValid").GetValue(invalid), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(invalid); }
    }

    [Test]
    public void StoryOrderCannotBeSkippedOrCompletedTwice()
    {
        Assert.That(Static("StoryProgress", "Complete", "q2_archaeologist"), Is.False);
        Assert.That(Static("StoryProgress", "Complete", "q1_spirit"), Is.True);
        Assert.That(Static("StoryProgress", "Complete", "q1_spirit"), Is.False);
        Assert.That(Static("StoryProgress", "Complete", "q2_archaeologist"), Is.False);
        Static("StoryProgress", "TryClearTowerEnemies");
        Static("StoryProgress", "TryReceiveArchaeologistSignal");
        Assert.That(Static("StoryProgress", "Complete", "q2_archaeologist"), Is.True);
        Assert.That(Static("StoryProgress", "Complete", "q2_archaeologist"), Is.False);
        Assert.That(Runtime("StoryProgress").GetProperty("Clues").GetValue(null), Has.Count.EqualTo(3));
    }

    [TestCase("q1_spirit", "sword")]
    [TestCase("q2_archaeologist", "bridge")]
    public void DataFolderAssetsLoadWithValidChoices(string id, string expected)
    {
        var fallback = (ScriptableObject)Runtime("StoryBeatData").GetMethod("Load",
            BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { id });
        try
        {
            Assert.That(fallback.GetType().GetProperty("IsValid").GetValue(fallback), Is.True);
            Assert.That(Field(fallback, "correct"), Is.EqualTo(expected));
        }
        finally { /* Loaded assets remain owned by Unity. */ }
    }

    [Test]
    public void ConsentDeclinesAndWrongAnswersAllowRetryThenRewardOnlyOnceOnClose()
    {
        Component panel = Panel();
        int completions = 0; bool awarded = false;
        Time.timeScale = .75f;
        Assert.That(Call(panel, "TryOpen", Beat("q1_spirit"), (Action<bool>)(passed =>
        { completions++; awarded = passed; }), null), Is.True);
        Assert.That(State, Is.EqualTo("Dialogue"));
        Assert.That(Time.timeScale, Is.Zero);
        Call(panel, "Advance");
        for (int i = 0; i < 3; i++) Call(panel, "Choose", 1);
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Consent"));
        Assert.That(Completed("q1_spirit"), Is.False);
        Call(panel, "Choose", 0);
        string[] answers = (string[])Field(panel, "answers");
        int correctIndex = Array.IndexOf(answers, "sword");
        Call(panel, "Choose", (correctIndex + 1) % 4);
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Question"));
        Assert.That(completions, Is.Zero);
        Call(panel, "Choose", correctIndex);
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Success"));
        Call(panel, "Close"); Call(panel, "Close");
        Assert.That(awarded, Is.True); Assert.That(completions, Is.EqualTo(1));
        Assert.That(Completed("q1_spirit"), Is.True);
        Assert.That(State, Is.EqualTo("Playing")); Assert.That(Time.timeScale, Is.EqualTo(.75f));
    }

    [Test]
    public void CancellingOrDisablingDoesNotRewardAndRestoresTime()
    {
        Component panel = Panel();
        int callbacks = 0;
        Action<bool> completion = passed => { Assert.That(passed, Is.False); callbacks++; };
        Time.timeScale = .6f;
        Assert.That(Call(panel, "TryOpen", Beat("q1_spirit"), completion, null), Is.True);
        Call(panel, "Advance"); Call(panel, "Choose", 0);
        panel.gameObject.SetActive(false);
        // EditMode does not dispatch runtime MonoBehaviour lifecycle callbacks.
        Call(panel, "OnDisable");
        Assert.That(callbacks, Is.EqualTo(1)); Assert.That(Completed("q1_spirit"), Is.False);
        Assert.That(State, Is.EqualTo("Playing")); Assert.That(Time.timeScale, Is.EqualTo(.6f));
        Assert.That(Call(panel, "TryOpen", Beat("q1_spirit"), completion, null), Is.True);
        Call(panel, "Close");
        Assert.That(callbacks, Is.EqualTo(2));
    }

    [Test]
    public void ArchaeologistUnlocksAfterSpiritAndCompletesWithBridge()
    {
        Component panel = Panel();
        var beat = Beat("q2_archaeologist");
        Assert.That(Call(panel, "TryOpen", beat, null, null), Is.False);
        Static("StoryProgress", "Complete", "q1_spirit");
        Static("StoryProgress", "TryClearTowerEnemies");
        Static("StoryProgress", "TryReceiveArchaeologistSignal");
        bool passed = false;
        Assert.That(Call(panel, "TryOpen", beat, (Action<bool>)(result => passed = result), null), Is.True);
        Call(panel, "Advance");
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Question"));
        Call(panel, "Choose", Array.IndexOf((string[])Field(panel, "answers"), "bridge"));
        Call(panel, "Advance");
        Assert.That(passed, Is.True); Assert.That(Completed("q2_archaeologist"), Is.True);
    }

    [Test]
    public void SceneDialogueFrameWorksEvenWhenOriginalCanvasIsInactive()
    {
        var preview = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(
            "Assets/_Project/Scenes/Sence_1/Scene1.unity");
        GameObject canvasObject = null;
        try
        {
            Component template = null;
            foreach (GameObject go in preview.GetRootGameObjects())
            {
                template = go.GetComponentInChildren(Runtime("NPCIntroDialogue"), true);
                if (template != null) break;
            }
            Assert.That(template, Is.Not.Null);
            template.gameObject.SetActive(false);
            var panel = (Component)Call(template, "CreateStoryPanel");
            Assert.That(panel, Is.Not.Null);
            canvasObject = panel.transform.parent.gameObject;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, root.scene);
            Assert.That(Call(panel, "TryOpen", Beat("q1_spirit"), null, null), Is.True);
            Call(panel, "Advance"); Call(panel, "Choose", 0);

            Canvas.ForceUpdateCanvases();
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Question"));
            Assert.That(Field(panel, "englishText"), Is.Not.Null);
            Assert.That(Field(panel, "vietnameseText"), Is.Not.Null);
            Assert.That(canvasObject.GetComponentsInChildren(Runtime("StoryClueJournal"), true), Has.Length.EqualTo(1));
            Call(panel, "Close");
            Assert.That(Completed("q1_spirit"), Is.False);
        }
        finally
        {
            if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}

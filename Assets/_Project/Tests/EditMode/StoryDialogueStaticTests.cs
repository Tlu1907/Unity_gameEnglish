using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StoryDialogueStaticTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject root;
    private float originalTimeScale;
    private ScriptableObject fallback;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static object Value(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static void Static(string type, string method) => Runtime(type).GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    private static void Click(object button) => ((UnityEngine.Events.UnityEvent)Value(button, "onClick")).Invoke();

    [SetUp]
    public void SetUp()
    {
        originalTimeScale = Time.timeScale;
        Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
        root = new GameObject("Static dialogue tests", typeof(RectTransform));
    }
    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        if (fallback != null) UnityEngine.Object.DestroyImmediate(fallback);
        Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
        Time.timeScale = originalTimeScale;
    }
    private Component Panel()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/Prefabs/Resources/StoryDialogue.prefab");
        Assert.That(prefab, Is.Not.Null);
        GameObject copy = UnityEngine.Object.Instantiate(prefab, root.transform);
        return copy.GetComponentInChildren(Runtime("StoryQuizPanel"), true);
    }
    private ScriptableObject Beat()
    {
        if (fallback == null)
        {
            fallback = UnityEngine.Object.Instantiate((ScriptableObject)Runtime("StoryBeatData").GetMethod("Load").Invoke(null, new object[] { "q1_spirit" }));
            fallback.GetType().GetField("conversationJson").SetValue(fallback, null);
        }
        return fallback;
    }

    [Test]
    public void SavedPrefabContainsEditableControlsAndJournalReferences()
    {
        Component panel = Panel();
        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.gameObject.activeSelf, Is.False);
        foreach (string field in new[] { "nameText", "englishText", "vietnameseText", "promptText" })
            Assert.That(Field(panel, field), Is.Not.Null, field);
        Assert.That(Field(panel, "advanceButton"), Is.Null);
        Assert.That(Field(panel, "closeButton"), Is.Null);
        Assert.That((Array)Field(panel, "buttons"), Has.Length.EqualTo(4));
        Assert.That(panel.transform.Find("CustomActions"), Is.Not.Null);
        Component journal = panel.transform.parent.GetComponent(Runtime("StoryClueJournal"));
        foreach (string field in new[] { "notebook", "entries", "buttonLabel", "toggleButton" })
            Assert.That(Field(journal, field), Is.Not.Null, field);
        Call(journal, "OnEnable");
        Click(Field(journal, "toggleButton"));
        Assert.That(((GameObject)Field(journal, "notebook")).activeSelf, Is.True);
        Click(Field(journal, "toggleButton"));
        Assert.That(((GameObject)Field(journal, "notebook")).activeSelf, Is.False);
        Call(journal, "OnDisable");
    }

    [Test]
    public void PhysicalButtonsAdvanceExactlyOnceAndPreserveAuthoredLayoutAndExtraChildren()
    {
        Component panel = Panel();
        var rect = (RectTransform)panel.transform;
        Vector2 min = new Vector2(.12f, .08f), max = new Vector2(.88f, .54f);
        rect.anchorMin = min; rect.anchorMax = max;
        var custom = new GameObject("Extra user interaction", typeof(RectTransform));
        custom.transform.SetParent(panel.transform.Find("CustomActions"), false);
        int childCount = panel.GetComponentsInChildren<Transform>(true).Length;
        // EditMode does not dispatch runtime lifecycle events. Rebinding must be idempotent.
        Call(panel, "OnEnable"); Call(panel, "OnEnable");
        int completions = 0; bool awarded = false;
        Assert.That(Call(panel, "TryOpen", Beat(), (Action<bool>)(passed => { completions++; awarded = passed; }), null), Is.True);
        Call(panel, "Advance");
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Consent"));
        Array buttons = (Array)Field(panel, "buttons");
        Click(buttons.GetValue(0));
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Question"));
        int correct = Array.IndexOf((string[])Field(panel, "answers"), "sword");
        Click(buttons.GetValue((correct + 1) % 4));
        Assert.That(Field(panel, "step").ToString(), Is.EqualTo("Question"));
        Click(buttons.GetValue(correct));
        Call(panel, "Advance");
        Assert.That(awarded, Is.True); Assert.That(completions, Is.EqualTo(1));
        Assert.That(rect.anchorMin, Is.EqualTo(min)); Assert.That(rect.anchorMax, Is.EqualTo(max));
        Assert.That(panel.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));
        Assert.That(custom.activeSelf, Is.True);
    }

    [Test]
    public void KeyboardCloseCancelsWithoutRewardAndPreservesCustomClickListeners()
    {
        Component panel = Panel();
        Call(panel, "OnEnable");
        int customClicks = 0, completions = 0;
        object customButton = ((Array)Field(panel, "buttons")).GetValue(0);
        var action = new UnityEngine.Events.UnityAction(() => customClicks++);
        ((UnityEngine.Events.UnityEvent)Value(customButton, "onClick")).AddListener(action);
        Action<bool> completion = correct => { Assert.That(correct, Is.False); completions++; };
        for (int visit = 0; visit < 2; visit++)
        {
            Call(panel, "OnEnable");
            Assert.That(Call(panel, "TryOpen", Beat(), completion, null), Is.True);
            Click(customButton);
            Call(panel, "Close");
            Call(panel, "OnDisable");
        }
        Assert.That(customClicks, Is.EqualTo(2)); Assert.That(completions, Is.EqualTo(2));
        Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));
    }

    [Test]
    public void SceneContainsExactlyOneSavedStaticDialogue()
    {
        var preview = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(
            "Assets/_Project/Scenes/Sence_1/Scene1.unity");
        try
        {
            int count = 0;
            foreach (GameObject go in preview.GetRootGameObjects())
                count += go.GetComponentsInChildren(Runtime("StoryQuizPanel"), true).Length;
            Assert.That(count, Is.EqualTo(1));
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
    }
}

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class LabBuildingDialogueTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject root;
    private float previousScale;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static void Reset(string type) => Runtime(type).GetMethod("Reset").Invoke(null, null);
    [SetUp] public void SetUp()
    {
        previousScale = Time.timeScale;
        Reset("GameplayState"); Reset("StoryProgress");
        root = new GameObject("Lab tests");
    }
    [TearDown] public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        Reset("GameplayState"); Reset("StoryProgress");
        Time.timeScale = previousScale;
    }
    private Component Open(Action<int> callback, bool advance = true)
    {
        var json = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Questions/Map1/Json/research_document_questions_en.json");
        object[] args = { json, null };
        Assert.That(Runtime("ResearchDocument").GetMethod("TryParse").Invoke(null, args), Is.True);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/Prefabs/Resources/StoryDialogue.prefab");
        var copy = UnityEngine.Object.Instantiate(prefab, root.transform);
        var panel = copy.GetComponentInChildren(Runtime("StoryQuizPanel"), true);
        var notes = new GameObject("Vocab_gammar", typeof(RectTransform),
            Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true));
        notes.transform.SetParent(panel.transform, false);
        notes.SetActive(false);
        Assert.That(Call(panel, "TryOpenResearchDocument", args[1], callback), Is.True);
        if (advance) Call(panel, "Advance");
        return panel;
    }
    [Test] public void LearningNotesFollowIntroductionQuestionsAndDestinationWithoutChangingLayout()
    {
        var panel = Open(_ => { }, false);
        var text = (Component)Field(panel, "vocabGrammarText");
        var rect = (RectTransform)text.transform;
        var position = rect.anchoredPosition;
        string Notes() => (string)text.GetType().GetProperty("text").GetValue(text);
        Assert.That(text.gameObject.activeSelf, Is.True);
        Assert.That(Notes(), Does.Contain("This is + danh từ"));
        Assert.That(Notes(), Does.Contain("laboratory: phòng nghiên cứu"));
        var portrait = (UnityEngine.UI.Image)Field(panel, "portrait");
        Assert.That(portrait.gameObject.activeSelf, Is.False);
        string[] grammar = { "use … to + V", "can + V", "keep + V-ing", "Do not + V", "am/is/are + V-ing" };
        Call(panel, "Advance");
        for (int i = 0; i < 5; i++)
        {
            Assert.That(Notes(), Does.Contain(grammar[i]));
            Call(panel, "Choose", 0);
            if (i < 4)
            {
                Assert.That(Notes(), Does.Contain(grammar[i]), "Keep question notes during feedback.");
                Call(panel, "Advance");
            }
        }
        Assert.That(Notes(), Does.Contain("V nguyên mẫu: câu mệnh lệnh."));
        Assert.That(Notes(), Does.Contain("destination: điểm đến"));
        Assert.That(rect.anchoredPosition, Is.EqualTo(position));
        Call(panel, "Close");
        // EditMode tests dispatch runtime cleanup explicitly.
        Call(panel, "OnDisable");
        Assert.That(Notes(), Is.Empty);
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void FiveSingleAttemptQuestionsReturnActualScoreWithoutRetrying(int wrong)
    {
        int score = -99;
        var panel = Open(value => score = value);
        for (int i = 0; i < 5; i++)
        {
            int correct = (int)Field(Field(panel, "eventQuestion"), "correctIndex");
            string[] answers = (string[])Field(panel, "answers");
            int choice = i < wrong ? (correct + 1) % answers.Length : correct;
            Call(panel, "Choose", choice);
            Call(panel, "Choose", correct); // Repeated clicks cannot change the score.
            if (i < 4) Call(panel, "Advance");
        }
        Assert.That(score, Is.EqualTo(-99), "Score must wait for the result screen to close.");
        Call(panel, "Advance");
        Assert.That(score, Is.EqualTo(5 - wrong));
        Assert.That(Time.timeScale, Is.EqualTo(previousScale));
    }
    [Test] public void ClosingBeforeFiveAnswersCancelsWithoutCompletingResearch()
    {
        int score = -99;
        var panel = Open(value => score = value);
        Call(panel, "Choose", 0);
        Call(panel, "Close");
        Assert.That(score, Is.EqualTo(-1));
        Assert.That(Runtime("StoryProgress").GetProperty("HasDecodedResearchDocuments").GetValue(null), Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(previousScale));
    }

    [Test] public void ArchaeologistDialogueOnlyAcceptsMissionAfterConsentAndCompletion()
    {
        var progress = Runtime("StoryProgress");
        progress.GetMethod("Complete").Invoke(null, new object[] { "q1_spirit" });
        progress.GetMethod("TryClearTowerEnemies").Invoke(null, null);
        progress.GetMethod("TryReceiveArchaeologistSignal").Invoke(null, null);
        var beat = Runtime("StoryBeatData").GetMethod("Load").Invoke(null, new object[] { "q2_archaeologist" });
        Assert.That(beat, Is.Not.Null);
        Assert.That(Field(beat, "conversationJson"), Is.Not.Null);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/Prefabs/Resources/StoryDialogue.prefab");
        var copy = UnityEngine.Object.Instantiate(prefab, root.transform);
        var panel = copy.GetComponentInChildren(Runtime("StoryQuizPanel"), true);
        bool passed = false;
        Assert.That(Call(panel, "TryOpen", beat, (Action<bool>)(value => passed = value), null), Is.True);
        Call(panel, "Advance"); // Introduction -> consent.
        Call(panel, "Choose", 1); // Refuse; mission stays locked.
        Call(panel, "Advance"); // Loop back to consent.
        Assert.That(progress.GetMethod("IsCompleted").Invoke(null, new object[] { "q2_archaeologist" }), Is.False);
        Call(panel, "Choose", 0);
        Call(panel, "Advance"); // Accept -> completion, without indexing an empty question list.
        Assert.That(passed, Is.False);
        Call(panel, "Advance");
        Assert.That(passed, Is.True);
        Assert.That(progress.GetMethod("TryEnterResearchRoom").Invoke(null, null), Is.True);
    }
}

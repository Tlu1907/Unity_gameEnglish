using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Contract and UI progression tests; no real LLM or HTTP server needed.</summary>
public sealed class NpcJsonDialogueTests
{
    private const string JsonPath = "Assets/_Project/Data/Questions/Map2/map2_forest_ranger_dialogue_en.json";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private GameObject root;
    private float previousScale;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    private static object Value(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static object Parse(string json)
    {
        object[] args = { json, null };
        bool valid = (bool)Runtime("NpcJsonDialogue").GetMethod("TryParse").Invoke(null, args);
        return valid ? args[1] : null;
    }
    private static object Response(string json) => Runtime("UnityAiDialogueClient").GetMethod("ParseGrade").Invoke(null, new object[] { json });
    [SetUp] public void SetUp()
    {
        Runtime("StoryProgress").GetMethod("Reset").Invoke(null, null);
        Runtime("GameplayState").GetMethod("Reset").Invoke(null, null);
        Runtime("LearningJournal").GetMethod("Reset").Invoke(null, null);
        previousScale = Time.timeScale;
        root = new GameObject("JSON dialogue tests", typeof(RectTransform), typeof(Canvas));
    }
    [TearDown] public void TearDown()
    {
        Runtime("StoryProgress").GetMethod("Reset").Invoke(null, null);
        UnityEngine.Object.DestroyImmediate(root);
        Time.timeScale = previousScale;
        Runtime("GameplayState").GetMethod("Reset").Invoke(null, null);
        Runtime("LearningJournal").GetMethod("Reset").Invoke(null, null);
    }

    [Test] public void RangerSchemaLoadsAndAllQuestionIdsExistInBackend()
    {
        object parsed = Parse(File.ReadAllText(JsonPath));
        Assert.That(parsed, Is.Not.Null);
        Array nodes = (Array)Runtime("NpcJsonDialogue").GetField("dialogues").GetValue(parsed);
        string settings = File.ReadAllText("UnityAI/config/dialogues/map2_forest_ranger.grading.json");
        int questions = 0;
        foreach (object node in nodes)
            if ((bool)Value(node, "IsQuestion"))
            { questions++; StringAssert.Contains("\"" + node.GetType().GetField("id").GetValue(node) + "\"", settings); }
        Assert.That(questions, Is.EqualTo(3));
        StringAssert.Contains("kiểm lâm", ((string)Call(nodes.GetValue(0), "LearningText")).ToLowerInvariant());
    }

    [TestCase("")]
    [TestCase("{}")]
    [TestCase("{\"id\":\"x\",\"speaker\":\"NPC\",\"dialogues\":[{\"id\":\"q\",\"type\":\"question\",\"dialogue_en\":\"Who?\"}]}")]
    [TestCase("{\"id\":\"x\",\"speaker\":\"NPC\",\"dialogues\":[{\"id\":\"a\",\"type\":\"dialogue\",\"dialogue_en\":\"Hi.\"},{\"id\":\"a\",\"type\":\"dialogue\",\"dialogue_en\":\"Again.\"}]}")]
    public void MalformedConversationIsRejected(string json) => Assert.That(Parse(json), Is.Null);

    [Test] public void HiddenBranchRequiresReachedMissionAndMatchingIntent()
    {
        object parsed = Parse(File.ReadAllText(JsonPath));
        Assert.That(Call(parsed, "FindFollowUp", "ask_next_destination_location", "ancient_ruins", 0), Is.Null);
        Assert.That(Call(parsed, "FindFollowUp", "creature_origin", "ancient_ruins", 100), Is.Null);
        object node = Call(parsed, "FindFollowUp", "ask_next_destination_location", "ancient_ruins", 100);
        Assert.That(node, Is.Not.Null);
        Assert.That(node.GetType().GetField("id").GetValue(node), Is.EqualTo("ranger_hidden_01_mark_ruins_on_map"));
    }

    [TestCase("{\"status\":\"validated\",\"passed\":true,\"attempts_left\":2,\"data\":{\"scores\":{\"meaning\":5,\"grammar\":4}}}", true)]
    [TestCase("{\"status\":\"validated\",\"passed\":false,\"attempts_left\":2,\"data\":{\"scores\":{\"meaning\":2,\"grammar\":4}}}", false)]
    [TestCase("{\"status\":\"exhausted\",\"attempts_left\":0}", true)]
    [TestCase("{\"status\":\"review_required\"}", false)]
    [TestCase("not JSON", false)]
    public void OnlyPassedOrExhaustedResponsesUnlockProgress(string json, bool expected)
        => Assert.That(Value(Response(json), "CanContinue"), Is.EqualTo(expected));

    [Test] public void UnlimitedReportDoesNotUnlockAfterAFailedAnswer()
    {
        var result = Response("{\"status\":\"validated\",\"passed\":false,\"attempts_left\":-1,\"unlimited_attempts\":true,\"data\":{\"scores\":{\"meaning\":2,\"grammar\":3}}}");
        Assert.That(Value(result, "IsExhausted"), Is.False);
        Assert.That(Value(result, "CanContinue"), Is.False);
        Assert.That((string)Call(result, "FeedbackText"), Does.Contain("Không giới hạn lượt sửa bài"));
    }

    [Test] public void NeededCorrectionsShowsBothFixesFromTheReportedCreaturesSentence()
    {
        var result = Response("{\"status\":\"validated\",\"attempts_left\":1,\"data\":{\"original_answer\":\"The creatures have destroy animal\",\"corrected_answer\":\"The creatures have destroyed an animal\",\"scores\":{\"meaning\":5,\"grammar\":3},\"total_score\":8,\"max_score\":10,\"errors\":[{\"quote\":\"have destroy\",\"replacement\":\"have destroyed\",\"explanation_vi\":\"Use V3.\"},{\"quote\":\"animal\",\"replacement\":\"an animal\",\"explanation_vi\":\"Add an article.\"}]}}");
        string text = (string)Call(result, "FeedbackText");
        Assert.That(text, Does.Contain("have destroy → have destroyed"));
        Assert.That(text, Does.Contain("animal → an animal"));
        Assert.That(text, Does.Contain("The creatures have destroyed an animal"));
        Assert.That(text, Does.Not.Contain("Chưa có lỗi cụ thể"));
    }

    [Test] public void NonEditGrammarPenaltyShowsItsReasonInsideNeededCorrections()
    {
        var result = Response("{\"status\":\"validated\",\"attempts_left\":1,\"data\":{\"scores\":{\"meaning\":5,\"grammar\":4},\"grammar_notes\":[\"Câu 2: cần hiện tại hoàn thành.\"]}}");
        string text = (string)Call(result, "FeedbackText");
        Assert.That(text.Substring(text.IndexOf("CẦN SỬA")), Does.Contain("Câu 2: cần hiện tại hoàn thành."));
        Assert.That(text, Does.Not.Contain("Chưa có lỗi cụ thể"));
    }

    [Test] public void LegacyResponseWithReducedGrammarCannotClaimNoCorrectionNeeded()
    {
        var result = Response("{\"status\":\"validated\",\"attempts_left\":1,\"data\":{\"scores\":{\"meaning\":5,\"grammar\":4},\"feedback_vi\":\"Use have + V3.\",\"errors\":[]}}");
        string text = (string)Call(result, "FeedbackText");
        Assert.That(text.Substring(text.IndexOf("CẦN SỬA")), Does.Contain("Use have + V3."));
        Assert.That(text, Does.Not.Contain("Chưa có lỗi cụ thể"));
    }

    [Test] public void FeedbackSeparatesRuleCorrectionsFromLearningSampleAndPreservesUnicode()
    {
        var response = Response("{\"status\":\"validated\",\"passed\":true,\"attempts_left\":2,\"data\":{\"scores\":{\"meaning\":5,\"grammar\":4},\"total_score\":9,\"max_score\":10,\"feedback_vi\":\"Bạn nói rõ vai trò.\",\"corrected_answer\":\"I am an agent.\",\"better_answer\":\"I am an agent exploring the forest.\",\"errors\":[{\"quote\":\"am agent\",\"replacement\":\"am an agent\",\"explanation_vi\":\"Danh từ đếm được số ít cần a/an.\"}]}}");
        string text = (string)Call(response, "FeedbackText");
        StringAssert.Contains("Bạn nói rõ vai trò.", text);
        Assert.That(text.IndexOf("CẦN SỬA", StringComparison.Ordinal), Is.LessThan(text.IndexOf("GỢI Ý CÂU HAY HƠN", StringComparison.Ordinal)));
        StringAssert.Contains("Câu đã sửa: I am an agent.", text);
    }

    private GameObject Child(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent ?? root.transform, false); return go;
    }
    private Component AddUi(string name, string type, string assembly, Transform parent = null)
        => Child(name, parent).AddComponent(Assembly.Load(assembly).GetType(type, true));
    private Component Text(string name, Transform parent = null)
    {
        var text = AddUi(name, "TMPro.TextMeshProUGUI", "Unity.TextMeshPro", parent);
        var font = AssetDatabase.LoadAssetAtPath("Assets/_Project/UI/Art/HUD/HUD_Font.asset", Assembly.Load("Unity.TextMeshPro").GetType("TMPro.TMP_FontAsset"));
        text.GetType().GetProperty("font").SetValue(text, font);
        return text;
    }

    [Test] public void CompletedRangerPagesAddMapTwoNotesAndOnlyRecordTheHiddenBranchWhenRead()
    {
        var journal = Runtime("LearningJournal");
        journal.GetMethod("Record").Invoke(null, new object[] { "map1", "Map 1", null, "Earlier lesson" });
        string Book() => (string)journal.GetMethod("BuildBook").Invoke(null, null);
        string before = Book();
        var flow = root.AddComponent(Runtime("NpcJsonDialoguePanel"));
        var panel = Child("Panel");
        Set(flow, "client", root.AddComponent(Runtime("UnityAiDialogueClient")));
        Set(flow, "dialoguePanel", panel);
        Set(flow, "englishText", Text("English", panel.transform));
        Set(flow, "answerInput", AddUi("Answer", "TMPro.TMP_InputField", "Unity.TextMeshPro", panel.transform));
        Set(flow, "submitButton", AddUi("Submit", "UnityEngine.UI.Button", "UnityEngine.UI", panel.transform));
        Set(flow, "continueButton", AddUi("Next", "UnityEngine.UI.Button", "UnityEngine.UI", panel.transform));
        Set(flow, "typewriterEffect", false);
        var data = ScriptableObject.CreateInstance(Runtime("DialogueData"));
        var source = new TextAsset(File.ReadAllText(JsonPath));
        try
        {
            Set(data, "conversationJson", source);
            Assert.That(Call(flow, "TryOpen", data, null), Is.True);
            Call(flow, "Close");
            Assert.That(Book(), Is.EqualTo(before), "Cancelling the first page must not learn unseen content.");
            for (int replay = 0; replay < 2; replay++)
            {
                if (replay > 0)
                {
                    Assert.That(Call(flow, "TryOpen", data, null), Is.False, "Completed NPC conversations must stay closed.");
                    Runtime("StoryProgress").GetMethod("Reset").Invoke(null, null);
                }
                Assert.That(Call(flow, "TryOpen", data, null), Is.True);
                for (int step = 0; step < 30 && (bool)Value(flow, "IsOpen"); step++)
                {
                    Set(flow, "grade", Response("{\"status\":\"validated\",\"passed\":true,\"data\":{\"scores\":{\"meaning\":5,\"grammar\":5}}}"));
                    Call(flow, "Advance");
                }
                Assert.That(Value(flow, "IsOpen"), Is.False);
                string book = Book();
                Assert.That(book, Does.Contain("Earlier lesson"));
                Assert.That(book, Does.Contain("Map 2"));
                Assert.That(book, Does.Contain("Present Perfect"));
                Assert.That(book, Does.Contain("Past Simple"));
                Assert.That(book, Does.Contain("since + mốc thời gian"));
                Assert.That(book, Does.Not.Contain("mark it on your map"));
                if (replay == 0) before = book;
                else Assert.That(book, Is.EqualTo(before), "Replaying must not duplicate notes.");
            }
            Assert.That(Call(flow, "TryOpen", data, null), Is.False);
            Runtime("StoryProgress").GetMethod("Reset").Invoke(null, null);
            Assert.That(Call(flow, "TryOpen", data, null), Is.True);
            var parsed = Parse(source.text);
            var hidden = ((Array)Runtime("NpcJsonDialogue").GetField("conditional_dialogues").GetValue(parsed)).GetValue(0);
            Set(flow, "hiddenNode", hidden);
            Call(flow, "Advance");
            Assert.That(Book(), Does.Contain("mark it on your map"));
        }
        finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(data); }
    }

    [Test] public void UiStopsAtQuestionsRestoresTimeAndKeepsSessionAcrossReopen()
    {
        var owner = root.AddComponent(Runtime("NPCIntroDialogue"));
        var flow = root.AddComponent(Runtime("NpcJsonDialoguePanel"));
        var client = root.AddComponent(Runtime("UnityAiDialogueClient"));
        var panel = Child("Panel");
        Set(flow, "client", client); Set(flow, "dialoguePanel", panel);
        Set(flow, "englishText", Text("English", panel.transform));
        var input = AddUi("Answer", "TMPro.TMP_InputField", "Unity.TextMeshPro", panel.transform);
        Set(flow, "answerInput", input);
        Set(flow, "submitButton", AddUi("Submit", "UnityEngine.UI.Button", "UnityEngine.UI", panel.transform));
        Set(flow, "continueButton", AddUi("Next", "UnityEngine.UI.Button", "UnityEngine.UI", panel.transform));
        var data = ScriptableObject.CreateInstance(Runtime("DialogueData"));
        var source = new TextAsset(File.ReadAllText(JsonPath));
        try
        {
            Set(data, "conversationJson", source);
            Time.timeScale = .65f;
            Assert.That(Call(owner, "TryShowDialogue", data, null), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            string start = (string)Value(flow, "CurrentNodeId");
            Call(flow, "Advance"); // finish typewriter
            Call(flow, "Advance"); // first question
            string question = (string)Value(flow, "CurrentNodeId");
            Assert.That(question, Is.EqualTo("ranger_question_01_player_identity"));
            Call(flow, "Advance"); Call(flow, "Advance");
            Assert.That(Value(flow, "CurrentNodeId"), Is.EqualTo(question), "No answer must not skip a question.");
            Set(flow, "grade", Response("{\"status\":\"validated\",\"passed\":true,\"attempts_left\":2,\"data\":{\"scores\":{\"meaning\":5,\"grammar\":5}}}"));
            Call(flow, "Advance");
            Assert.That(Value(flow, "CurrentNodeId"), Is.Not.EqualTo(question));
            string session = (string)Value(client, "SessionId");
            Call(owner, "Close");
            Assert.That(Time.timeScale, Is.EqualTo(.65f));
            Assert.That(Call(owner, "TryShowDialogue", data, null), Is.True);
            Assert.That(Value(flow, "CurrentNodeId"), Is.EqualTo(start));
            Assert.That(Value(client, "SessionId"), Is.EqualTo(session));
            Call(owner, "Close");
        }
        finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(data); }
    }
}

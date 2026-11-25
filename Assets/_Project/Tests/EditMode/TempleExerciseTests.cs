using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class TempleExerciseTests
{
    [Test]
    public void TempleLocationIsHiddenUntilAskedAndClearsAfterTempleCompletion()
    {
        var progress = Runtime("StoryProgress");
        bool Visible() => (bool)progress.GetProperty("ShowTempleLocation").GetValue(null);
        Assert.That(Visible(), Is.False);
        progress.GetMethod("CompleteConversation").Invoke(null, new object[] { "map2_forest_ranger" });
        Assert.That(Visible(), Is.False, "Finishing the ranger without asking directions must not reveal the temple.");
        progress.GetMethod("MarkTempleLocation").Invoke(null, null);
        Assert.That(Visible(), Is.True);
        progress.GetMethod("CompleteConversation").Invoke(null, new object[] { "map2_ancient_temple" });
        Assert.That(Visible(), Is.False);
        progress.GetMethod("Reset").Invoke(null, null);
        Assert.That(progress.GetProperty("HasMarkedTempleLocation").GetValue(null), Is.False);
    }

    [Test]
    public void TempleRequiresRangerAndCompletedConversationsCannotRestart()
    {
        var progress = Runtime("StoryProgress");
        bool CanStart(string id) => (bool)progress.GetMethod("CanStartConversation").Invoke(null, new object[] { id });
        bool Complete(string id) => (bool)progress.GetMethod("CompleteConversation").Invoke(null, new object[] { id });
        Assert.That(CanStart("map2_ancient_temple"), Is.False);
        Assert.That(Complete("map2_ancient_temple"), Is.False);
        Assert.That(CanStart("map2_forest_ranger"), Is.True);
        Assert.That(Complete("map2_forest_ranger"), Is.True);
        Assert.That(CanStart("map2_forest_ranger"), Is.False);
        Assert.That(CanStart("map2_ancient_temple"), Is.True);
        Assert.That(Complete("map2_ancient_temple"), Is.True);
        Assert.That(CanStart("map2_ancient_temple"), Is.False);
        progress.GetMethod("Reset").Invoke(null, null);
        Assert.That(CanStart("map2_forest_ranger"), Is.True);
        Assert.That(CanStart("map2_ancient_temple"), Is.False);
    }

    [Test]
    public void CrystalBlessingRestoresHealthAndArmorSetsDamageAndCannotBeClaimedTwice()
    {
        var player = new GameObject("Temple reward test player");
        try
        {
            var health = player.AddComponent(Runtime("Health"));
            var inventory = player.AddComponent(Runtime("PlayerInventory"));
            var attack = player.AddComponent(Runtime("PlayerAttack"));
            inventory.GetType().GetMethod("SetEquipped").Invoke(inventory, new object[] { true });
            int Armor() => (int)health.GetType().GetProperty("Armor").GetValue(health);
            int before = Armor();
            health.GetType().GetField("armor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 25);
            health.GetType().GetMethod("Configure").Invoke(health, new object[] { 200, 60 });
            var grant = attack.GetType().GetMethod("GrantTempleBlessing");
            Assert.That(grant.Invoke(attack, null), Is.True);
            Assert.That(health.GetType().GetProperty("Current").GetValue(health), Is.EqualTo(200));
            Assert.That(Armor(), Is.EqualTo(before + 200));
            Assert.That(health.GetType().GetProperty("MaximumArmor").GetValue(health), Is.EqualTo(300));
            Assert.That(attack.GetType().GetProperty("Damage").GetValue(attack), Is.EqualTo(70));
            health.GetType().GetMethod("Configure").Invoke(health, new object[] { 200, 80 });
            Assert.That(grant.Invoke(attack, null), Is.False);
            Assert.That(health.GetType().GetProperty("Current").GetValue(health), Is.EqualTo(80));
            Assert.That(Armor(), Is.EqualTo(before + 200));
            inventory.GetType().GetMethod("SetEquipped").Invoke(inventory, new object[] { false });
            Assert.That(Armor(), Is.EqualTo(200), "Unequipping gear must preserve the crystal armor.");
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void TempleRewardUsesTheAuthoredGoldenCrystalSprite()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Scence_2/Scence2.unity");
        try
        {
            Component trigger = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                trigger = root.GetComponentInChildren(Runtime("TempleDialogueTrigger"), true);
                if (trigger != null) break;
            }
            Assert.That(trigger, Is.Not.Null);
            var sprite = (Sprite)Runtime("TempleDialogueTrigger")
                .GetField("rewardCrystal", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trigger);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.name, Is.EqualTo("Items_56"));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private object[] exercises;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    [SetUp] public void Load()
    {
        Runtime("StoryProgress").GetMethod("Reset").Invoke(null, null);
        object[] args = { File.ReadAllText("Assets/_Project/Data/Questions/Map2/map2_ancient_temple_dialogue_en.json"), null };
        Assert.That(Runtime("NpcJsonDialogue").GetMethod("TryParse").Invoke(null, args), Is.True);
        var values = (Array)Runtime("NpcJsonDialogue").GetField("exercises").GetValue(args[1]);
        exercises = new object[values.Length]; values.CopyTo(exercises, 0);
        Assert.That(exercises.Length, Is.EqualTo(4));
    }
    private bool Check(int index, string answer) => (bool)Runtime("TempleExercise").GetMethod("CheckLocal").Invoke(exercises[index], new object[] { answer });
    [Test] public void AllAuthoredTransformationAnswersPassWithoutInference()
    {
        for (int i = 0; i < 2; i++)
            foreach (string answer in (string[])Runtime("TempleExercise").GetField("accepted_answers").GetValue(exercises[i]))
                Assert.That(Check(i, answer), Is.True, answer);
    }
    [TestCase(0, "Did the guardians closed the portal?")]
    [TestCase(0, "The guardians have closed the portal.")]
    [TestCase(0, "The guardians closed the portal. They left.")]
    [TestCase(0, "The guardians did not close the portal.")]
    [TestCase(0, "portal closed guardians")]
    [TestCase(1, "The creatures has destroyed many trees.")]
    [TestCase(1, "The creatures have not destroyed many trees.")]
    [TestCase(1, "Have the creatures destroyed many trees?")]
    [TestCase(1, "The creatures destroyed many trees.")]
    public void WrongTenseNegativeQuestionsAndKeywordListsAreRejected(int index, string answer)
        => Assert.That(Check(index, answer), Is.False);
    [TestCase("The portal.", true)]
    [TestCase("From the crater", true)]
    [TestCase("They come from the PORTAL in the crater!", true)]
    [TestCase("portals", false)]
    [TestCase("craters", false)]
    [TestCase("a temple", false)]
    public void ReadingRequiresAWholeKeyword(string answer, bool expected) => Assert.That(Check(2, answer), Is.EqualTo(expected));
    [Test] public void ReportHasUnlimitedRevisionAndIsNotLocallyMarkedCorrect()
    {
        Assert.That(Runtime("TempleExercise").GetProperty("IsReport").GetValue(exercises[3]), Is.True);
        Assert.That(Check(3, "I saw light. They have destroyed trees. I will go."), Is.False);
    }
    [Test] public void TempleSceneUiUsesServerResultsAndKeepsEvidenceAvailableForTheReport()
    {
        Runtime("StoryProgress").GetMethod("CompleteConversation").Invoke(null, new object[] { "map2_forest_ranger" });
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, flags).Invoke(target, args);
        object Field(object target, string name) => target.GetType().GetField(name, flags).GetValue(target);
        void Set(object target, string name, object value) => target.GetType().GetField(name, flags).SetValue(target, value);
        var preview = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Scence_2/Scence2.unity");
        GameObject copy = null;
        float scale = Time.timeScale;
        Runtime("GameplayState").GetMethod("Reset").Invoke(null, null);
        Runtime("LearningJournal").GetMethod("Reset").Invoke(null, null);
        try
        {
            var source = Array.Find(preview.GetRootGameObjects(), root => root.name == "TempleDialogueCanvas");
            Assert.That(source, Is.Not.Null);
            copy = UnityEngine.Object.Instantiate(source);
            var flow = copy.GetComponent(Runtime("NpcJsonDialoguePanel"));
            Set(flow, "typewriterEffect", false);
            var data = AssetDatabase.LoadAssetAtPath("Assets/_Project/Data/Dialogues/TempleGuardianDialogue.asset", Runtime("DialogueData"));
            Assert.That(Call(flow, "TryOpen", data, null), Is.True);
            string Node() => (string)flow.GetType().GetProperty("CurrentNodeId").GetValue(flow);
            var input = Field(flow, "answerInput");
            void Answer(string answer)
            {
                input.GetType().GetProperty("text").SetValue(input, answer);
                var exercise = flow.GetType().GetProperty("Exercise", flags).GetValue(flow);
                var state = Call(flow, "StateFor", exercise);
                Set(state, "answer", answer);
                int attempt = (int)Field(state, "attempts") + 1;
                bool correct = (bool)Call(exercise, "CheckLocal", answer);
                string json = "{\"status\":\"validated\",\"passed\":" + (correct ? "true" : "false") +
                    ",\"attempt\":" + attempt + ",\"attempts_left\":" + (2 - attempt) +
                    ",\"data\":{\"scores\":{\"meaning\":5,\"grammar\":5},\"total_score\":10,\"max_score\":10}}";
                var result = Runtime("UnityAiDialogueClient").GetMethod("ParseGrade").Invoke(null, new object[] { json });
                Call(flow, "HandleExerciseResult", exercise, result);
            }
            Call(flow, "Advance"); Call(flow, "Advance");
            Assert.That(Node(), Is.EqualTo("temple_03_first_stone"));
            Answer("wrong"); Call(flow, "Advance");
            Assert.That(Node(), Is.EqualTo("temple_03_first_stone"));
            Call(flow, "Close");
            Assert.That(Call(flow, "TryOpen", data, null), Is.True);
            Call(flow, "Advance"); Call(flow, "Advance");
            Answer("still wrong"); Call(flow, "Advance");
            Assert.That(Node(), Is.EqualTo("temple_04_first_revelation"), "Closing must not reset the two-attempt limit.");
            Call(flow, "Advance"); Answer("The creatures have destroyed many trees."); Call(flow, "Advance");
            Call(flow, "Advance");
            Assert.That(Node(), Is.EqualTo("temple_07_third_stone"));
            Assert.That((string)((Component)Field(flow, "englishText")).GetType().GetProperty("text").GetValue(Field(flow, "englishText")), Does.Contain("It opened a week ago"));
            Answer("The crater."); Call(flow, "Advance"); Call(flow, "Advance");
            Assert.That(Node(), Is.EqualTo("temple_09_report"));
            Assert.That(input.GetType().GetProperty("characterLimit").GetValue(input), Is.EqualTo(1500));
            var hints = Field(flow, "learningHints");
            Assert.That((string)hints.GetType().GetProperty("text").GetValue(hints), Does.Contain("The guardians closed the portal long ago"));
            Call(flow, "Advance"); Assert.That(Node(), Is.EqualTo("temple_09_report"));
            Call(flow, "ShowReferenceAnswer");
            var feedback = Field(flow, "feedbackText");
            Assert.That((string)feedback.GetType().GetProperty("text").GetValue(feedback), Does.Contain("I will go to the crater"));
            Call(flow, "Close");
        }
        finally
        {
            if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            EditorSceneManager.ClosePreviewScene(preview);
            Runtime("GameplayState").GetMethod("Reset").Invoke(null, null);
            Runtime("LearningJournal").GetMethod("Reset").Invoke(null, null);
            Runtime("StoryProgress").GetMethod("Reset").Invoke(null, null);
            Time.timeScale = scale;
        }
    }
}

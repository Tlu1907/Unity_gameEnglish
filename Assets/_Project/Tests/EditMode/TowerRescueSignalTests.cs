using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TowerRescueSignalTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject root;
    private Component signal, dialogue, first, second;
    private Collider2D player;
    private float timeScale;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static object Static(string type, string method, params object[] args) => Runtime(type).GetMethod(method).Invoke(null, args);
    private static void Field(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Value(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static bool Received => (bool)Runtime("StoryProgress").GetProperty("HasReceivedArchaeologistSignal").GetValue(null);
    private GameObject Object(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
    private Component Enemy(string name)
    {
        var brain = Object(name).AddComponent(Runtime("EnemyBrain"));
        Call(brain, "EnsureComponents"); Call(brain, "OnEnable");
        return brain;
    }
    private static void Kill(Component enemy) => Call(enemy.GetComponent(Runtime("Health")), "TakeDamage", 100);
    private void Near() => Call(signal, "OnTriggerEnter2D", player);
    private void Tick() => Call(signal, "Update");
    private void Read()
    {
        for (int i = 0; i < 4; i++) Call(dialogue, "Advance");
    }
    [SetUp] public void SetUp()
    {
        timeScale = Time.timeScale; Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
        root = new GameObject("Rescue signal tests");
        first = Enemy("Tower enemy A"); second = Enemy("Tower enemy B");
        var canvas = Object("Linh Canvas"); canvas.AddComponent<Canvas>();
        dialogue = canvas.AddComponent(Runtime("NPCIntroDialogue"));
        Field(dialogue, "playOnStart", false); Field(dialogue, "typewriterEffect", false);
        var panel = Object("Linh dialogue frame"); panel.transform.SetParent(canvas.transform);
        Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
        Component Text(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), textType); go.transform.SetParent(panel.transform);
            return go.GetComponent(textType);
        }
        Field(dialogue, "dialoguePanel", panel); Field(dialogue, "englishText", Text("English"));
        Field(dialogue, "vietnameseText", Text("Vietnamese")); Field(dialogue, "npcNameText", Text("Speaker"));
        signal = Object("Tower").AddComponent(Runtime("TowerRescueSignal"));
        Array enemies = Array.CreateInstance(Runtime("EnemyBrain"), 2); enemies.SetValue(first, 0); enemies.SetValue(second, 1);
        Field(signal, "enemiesToDefeat", enemies); Field(signal, "linhDialogue", dialogue);
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Data/Dialogues/Linh_ArchaeologistSOS.asset");
        Assert.That(data, Is.Not.Null);
        Field(signal, "linhRescueDialogue", data); Call(signal, "Initialize");
        var actor = Object("Player"); actor.AddComponent(Runtime("PlayerInventory")); player = actor.AddComponent<BoxCollider2D>();
    }
    [TearDown] public void TearDown()
    {
        Call(dialogue, "Close"); Call(signal, "OnDestroy");
        UnityEngine.Object.DestroyImmediate(root); Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
        Time.timeScale = timeScale;
    }
    [Test] public void WholeEnemyGroupAutomaticallyOpensMessageWithoutApproachingTower()
    {
        Static("StoryProgress", "Complete", "q1_spirit");
        Kill(first); Tick();
        Assert.That(Value(signal, "RemainingEnemies"), Is.EqualTo(1)); Assert.That(Value(dialogue, "IsOpen"), Is.False);
        Kill(second); Tick();
        Assert.That(Value(dialogue, "IsOpen"), Is.True); Assert.That(Time.timeScale, Is.Zero);
        Assert.That(Static("StoryProgress", "CanStart", "q2_archaeologist"), Is.False);
        Read();
        Assert.That(Received, Is.True); Assert.That(Static("StoryProgress", "CanStart", "q2_archaeologist"), Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(timeScale));
    }
    [Test] public void DisabledOrMissingEnemiesAreNotCountedAsDefeated()
    {
        Static("StoryProgress", "Complete", "q1_spirit");
        first.gameObject.SetActive(false); Kill(second); Near(); Tick();
        Assert.That(Value(signal, "RemainingEnemies"), Is.EqualTo(1)); Assert.That(Value(dialogue, "IsOpen"), Is.False);
    }
    [Test] public void CancellingCanRetryOnNextVisitAndCompletedMessageNeverRepeats()
    {
        Static("StoryProgress", "Complete", "q1_spirit"); Kill(first); Kill(second);
        Tick(); Call(dialogue, "Close"); Tick();
        Assert.That(Received, Is.False); Assert.That(Value(dialogue, "IsOpen"), Is.False);
        Call(signal, "OnTriggerExit2D", player); Near(); Tick(); Read();
        Assert.That(Received, Is.True);
        Call(signal, "OnTriggerExit2D", player); Near(); Tick();
        Assert.That(Value(dialogue, "IsOpen"), Is.False);
        var clues = (System.Collections.ICollection)Runtime("StoryProgress").GetProperty("Clues").GetValue(null);
        Assert.That(clues.Count, Is.EqualTo(2));
    }
    [Test] public void MessageWaitsForTowerEquipmentBeatAndWorksWithInitiallyHiddenLinhCanvas()
    {
        Kill(first); Kill(second); Tick();
        Assert.That(Value(dialogue, "IsOpen"), Is.False);
        dialogue.gameObject.SetActive(false);
        Static("StoryProgress", "Complete", "q1_spirit"); Tick();
        Assert.That(Value(dialogue, "IsOpen"), Is.True); Assert.That(dialogue.gameObject.activeSelf, Is.True);
        Read(); Assert.That(Received, Is.True);
    }
    [Test] public void AutomaticMessageWaitsUntilGameplayResumes()
    {
        Static("StoryProgress", "Complete", "q1_spirit");
        Kill(first); Kill(second);
        Static("GameplayState", "Pause"); Tick();
        Assert.That(Value(dialogue, "IsOpen"), Is.False);
        Static("GameplayState", "Resume"); Tick();
        Assert.That(Value(dialogue, "IsOpen"), Is.True);
        Read(); Tick();
        Assert.That(Received, Is.True);
        Assert.That(Value(dialogue, "IsOpen"), Is.False);
    }
    [Test] public void SceneUsesTheAssignedTwoEnemiesAndLinhDialogueAsset()
    {
        var preview = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sence_1/Scene1.unity");
        try
        {
            Component source = null;
            foreach (var go in preview.GetRootGameObjects()) { source = go.GetComponent(Runtime("TowerRescueSignal")); if (source != null) break; }
            Assert.That(source, Is.Not.Null);
            Array enemies = (Array)source.GetType().GetField("enemiesToDefeat", Flags).GetValue(source);
            Assert.That(enemies, Has.Length.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { "Monster (4)", "Monster (5)" }, new[] { ((Component)enemies.GetValue(0)).name, ((Component)enemies.GetValue(1)).name });
            Assert.That(source.GetType().GetField("linhDialogue", Flags).GetValue(source), Is.Not.Null);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
    }
}

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TowerDialogueTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject root;
    private Component tower, panel, inventory;
    private Collider2D contact;
    private float timeScale;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static void Reset(string type) => Runtime(type).GetMethod("Reset").Invoke(null, null);
    private static object Value(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private GameObject Object(string name)
    { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
    [SetUp] public void SetUp()
    {
        timeScale = Time.timeScale; Reset("GameplayState"); Reset("StoryProgress");
        root = new GameObject("Tower tests");
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/Prefabs/Resources/StoryDialogue.prefab");
        var view = UnityEngine.Object.Instantiate(prefab, root.transform);
        panel = view.GetComponentInChildren(Runtime("StoryQuizPanel"), true);
        Call(panel, "OnEnable");
        tower = Object("Tower").AddComponent(Runtime("TowerDialogueTrigger"));
        tower.GetType().GetField("dialoguePanel", Flags).SetValue(tower, panel);
        var player = Object("Player");
        player.AddComponent(Runtime("Health"));
        inventory = player.AddComponent(Runtime("PlayerInventory"));
        contact = player.AddComponent<BoxCollider2D>();
    }
    [TearDown] public void TearDown()
    {
        Call(panel, "Close"); UnityEngine.Object.DestroyImmediate(root);
        Reset("GameplayState"); Reset("StoryProgress"); Time.timeScale = timeScale;
    }
    [Test] public void ContactWithoutMovementOpensAndOnlyAFullExitAllowsRetry()
    {
        var child = Object("Second player collider"); child.transform.SetParent(inventory.transform);
        Collider2D second = child.AddComponent<CircleCollider2D>();
        Call(tower, "OnTriggerEnter2D", contact);
        Assert.That(Value(panel, "IsOpen"), Is.True);
        Call(tower, "OnTriggerEnter2D", second); Call(panel, "Close");
        Call(tower, "OnTriggerExit2D", contact); Call(tower, "OnTriggerStay2D", second);
        Assert.That(Value(panel, "IsOpen"), Is.False);
        Call(tower, "OnTriggerExit2D", second); Call(tower, "OnTriggerEnter2D", contact);
        Assert.That(Value(panel, "IsOpen"), Is.True);
    }
    [Test] public void NonPlayerContactsNeverOpenDialogue()
    {
        Collider2D other = Object("Other actor").AddComponent<BoxCollider2D>();
        Call(tower, "OnTriggerEnter2D", other); Call(tower, "Update");
        Assert.That(Value(panel, "IsOpen"), Is.False);
    }
    [Test] public void CompletingAllQuestionsEquipsTheContactPlayerAndKeepsTowerStanding()
    {
        Component bystander = Object("Bystander inventory").AddComponent(Runtime("PlayerInventory"));
        Call(tower, "OnTriggerEnter2D", contact);
        Call(panel, "Advance"); Call(panel, "Choose", 0); Call(panel, "Advance");
        Array questions = (Array)Field(Field(panel, "conversation"), "questions");
        for (int i = 0; i < questions.Length; i++)
        {
            string correct = (string)Field(questions.GetValue(i), "correct");
            Call(panel, "Choose", Array.IndexOf((string[])Field(panel, "answers"), correct));
            if (i < questions.Length - 1) Call(panel, "Advance");
        }
        Call(panel, "Close");
        Assert.That(Value(inventory, "HasWeapon"), Is.True);
        Assert.That(Value(bystander, "HasWeapon"), Is.False);
        Assert.That(Value(tower, "IsCompleted"), Is.True);
        Assert.That(tower.gameObject.activeSelf, Is.True);
        Call(tower, "OnTriggerExit2D", contact); Call(tower, "OnTriggerEnter2D", contact);
        Assert.That(Value(panel, "IsOpen"), Is.False);
    }
    [Test] public void SceneTowerHasTriggerAndEmptyVisualWithoutChestScripts()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sence_1/Scene1.unity");
        try
        {
            GameObject target = null;
            foreach (var go in scene.GetRootGameObjects()) if (go.name == "Tower") target = go;
            Assert.That(target, Is.Not.Null);
            Assert.That(target.GetComponent(Runtime("TowerDialogueTrigger")), Is.Not.Null);
            Assert.That(target.GetComponent<BoxCollider2D>().isTrigger, Is.True);
            Assert.That(target.GetComponent(Runtime("Chest")), Is.Null);
            Assert.That(target.GetComponent(Runtime("ChestQuizInteraction")), Is.Null);
            Assert.That(target.GetComponent(Runtime("MirrorTreasureItem")), Is.Null);
            var renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(1));
            Assert.That(renderers[0].enabled, Is.True);
            Assert.That(target.transform.Find("Visual"), Is.Not.Null);
            Assert.That(target.GetComponent(Runtime("TowerCollisionBounds")), Is.Not.Null);
            var solid = target.transform.Find("Collision").GetComponent<BoxCollider2D>();
            Assert.That(solid.isTrigger, Is.False);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
}

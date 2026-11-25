using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CityExitPortalTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject root;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static object Static(string type, string method, params object[] args) => Runtime(type).GetMethod(method).Invoke(null, args);
    private static object Value(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
    private Component Enemy(string mode)
    {
        var brain = Child(mode).AddComponent(Runtime("EnemyBrain"));
        brain.GetType().GetField("encounterMode", Flags).SetValue(brain, Enum.Parse(Runtime("EnemyEncounterMode"), mode));
        Call(brain, "EnsureComponents");
        Call(brain, "OnEnable");
        Call(brain, "InitializeEncounter");
        return brain;
    }
    private static void DecodeResearch()
    {
        Static("StoryProgress", "Complete", "q1_spirit");
        Static("StoryProgress", "TryClearTowerEnemies");
        Static("StoryProgress", "TryReceiveArchaeologistSignal");
        Static("StoryProgress", "Complete", "q2_archaeologist");
        Static("StoryProgress", "TryEnterResearchRoom");
        Assert.That(Static("StoryProgress", "TryCompleteResearchDocument", 5, "Next area"), Is.True);
    }
    [SetUp] public void SetUp()
    {
        Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
        root = new GameObject("Portal tests");
    }
    [TearDown] public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
    }
    [Test] public void PortalWaitsForResearchAndAllLabDeathsButIgnoresOtherEnemyGroups()
    {
        var first = Enemy("LaboratoryAmbush");
        var second = Enemy("LaboratoryAmbush");
        Enemy("Normal");
        var go = Child("Items_50");
        var renderer = go.AddComponent<SpriteRenderer>();
        var portal = go.AddComponent(Runtime("CityExitPortal"));
        if (go.GetComponent<Collider2D>() == null) Call(portal, "Awake");
        Call(portal, "OnEnable"); Call(portal, "Start");
        Assert.That(renderer.enabled, Is.False);
        Assert.That(go.activeSelf, Is.True, "The controller must keep receiving progress/death events.");
        Assert.That(Value(portal, "RemainingEnemies"), Is.EqualTo(2));
        DecodeResearch();
        first.gameObject.SetActive(false);
        Assert.That(Value(portal, "RemainingEnemies"), Is.EqualTo(2), "Disabling is not defeating an enemy.");
        first.gameObject.SetActive(true);
        Call(first.GetComponent(Runtime("Health")), "TakeDamage", 10000);
        Assert.That(Value(portal, "IsUnlocked"), Is.False);
        Call(second.GetComponent(Runtime("Health")), "TakeDamage", 10000);
        Assert.That(Value(portal, "IsUnlocked"), Is.True);
        Assert.That(renderer.enabled, Is.True);
        Assert.That(go.GetComponent<Collider2D>().enabled, Is.True);
    }
    [Test] public void NoLabEnemiesDoesNotUnlockPortalEvenAfterResearch()
    {
        var portal = Child("Items_50").AddComponent(Runtime("CityExitPortal"));
        if (portal.GetComponent<Collider2D>() == null) Call(portal, "Awake");
        Call(portal, "OnEnable"); Call(portal, "Start");
        DecodeResearch();
        Assert.That(Value(portal, "IsUnlocked"), Is.False);
    }
}

public sealed class MainMenuFlowTests
{
    private const string ProfileKey = "EarthDefender.PlayerProfile.v1";
    private const string NameKey = "EarthDefender.PlayerName";
    private string oldProfile, oldName;
    private bool hadProfile, hadName;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Profile(string method, params object[] args) => Runtime("PlayerProfileStore")
        .GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
    private static string Identity(string property) => (string)Runtime("PlayerProfileStore").GetProperty(property).GetValue(null);

    [SetUp] public void SetUp()
    {
        hadProfile = PlayerPrefs.HasKey(ProfileKey); oldProfile = PlayerPrefs.GetString(ProfileKey);
        hadName = PlayerPrefs.HasKey(NameKey); oldName = PlayerPrefs.GetString(NameKey);
        PlayerPrefs.DeleteKey(ProfileKey); PlayerPrefs.DeleteKey(NameKey);
        Profile("ResetCache");
    }
    [TearDown] public void TearDown()
    {
        if (hadProfile) PlayerPrefs.SetString(ProfileKey, oldProfile); else PlayerPrefs.DeleteKey(ProfileKey);
        if (hadName) PlayerPrefs.SetString(NameKey, oldName); else PlayerPrefs.DeleteKey(NameKey);
        PlayerPrefs.Save(); Profile("ResetCache");
    }
    [Test] public void NameIsRequiredAndIdentitySurvivesRenameAndReload()
    {
        Assert.That(Identity("PlayerName"), Is.Empty);
        Assert.That(Profile("SetName", "  \t "), Is.False);
        Assert.That(Profile("SetName", "  Nguyễn   An  "), Is.True);
        string id = Identity("PlayerId");
        Assert.That(Guid.TryParse(id, out _), Is.True);
        Profile("ResetCache");
        Assert.That(Identity("PlayerName"), Is.EqualTo("Nguyễn An"));
        Assert.That(Identity("PlayerId"), Is.EqualTo(id));
        Profile("SetName", "Bình");
        Assert.That(Identity("PlayerId"), Is.EqualTo(id));
        Assert.That(((string)Profile("NormalizeName", new string('x', 30))).Length, Is.EqualTo(20));
    }
    [Test] public void ExistingMenuNameMigratesWithoutChangingIdentity()
    {
        PlayerPrefs.SetString(NameKey, "Linh");
        Assert.That(Identity("PlayerName"), Is.EqualTo("Linh"));
        string id = Identity("PlayerId");
        Profile("ResetCache");
        Assert.That(Identity("PlayerId"), Is.EqualTo(id));
    }
    [Test] public void BuildStartsAtMenuAndCityPortalTargetsEnabledForestScene()
    {
        var scenes = UnityEditor.EditorBuildSettings.scenes;
        var enabled = Array.FindAll(scenes, scene => scene.enabled);
        Assert.That(enabled.Length, Is.GreaterThanOrEqualTo(3));
        Assert.That(enabled[0].path, Is.EqualTo("Assets/_Project/Scenes/MainMenu.unity"));
        Assert.That(enabled[1].path, Is.EqualTo("Assets/_Project/Scenes/Sence_1/Scene1_Merged.unity"));
        Assert.That(enabled[2].path, Is.EqualTo("Assets/_Project/Scenes/Scence_2/Scence2.unity"));
        foreach (var scene in enabled) Assert.That(System.IO.File.Exists(scene.path), Is.True, scene.path);
        var city = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(enabled[1].path);
        try
        {
            Component portal = null;
            foreach (var root in city.GetRootGameObjects())
            {
                portal = root.GetComponentInChildren(Runtime("CityExitPortal"), true);
                if (portal != null) break;
            }
            Assert.That(portal, Is.Not.Null);
            Assert.That(portal.GetType().GetField("destination").GetValue(portal), Is.EqualTo(enabled[2].path));
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(city); }
    }
}

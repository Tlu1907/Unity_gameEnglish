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

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ResearchDocumentTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static Type TypeOf(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags).Invoke(value, args);
    private static object Static(string type, string name, params object[] args) => TypeOf(type).GetMethod(name).Invoke(null, args);
    private static object Value(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    [SetUp] public void SetUp() { Static("GameplayState", "Reset"); Static("StoryProgress", "Reset"); }
    [TearDown] public void TearDown() { Static("GameplayState", "Reset"); Static("StoryProgress", "Reset"); }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void LaboratoryDifficultyScalesAndOnlyFourOrMoreMistakesAreLethal(int wrong)
    {
        Static("StoryProgress", "Complete", "q1_spirit");
        Static("StoryProgress", "TryClearTowerEnemies");
        Static("StoryProgress", "TryReceiveArchaeologistSignal");
        Static("StoryProgress", "Complete", "q2_archaeologist");
        Static("StoryProgress", "TryEnterResearchRoom");
        Assert.That(Static("StoryProgress", "TryCompleteResearchDocument", 5 - wrong, "Next destination"), Is.True);
        Assert.That(Static("StoryProgress", "TryCompleteResearchDocument", 5, "Overwrite"), Is.False);
        var enemy = new GameObject("Lab enemy");
        var player = new GameObject("Player target");
        try
        {
            var brain = enemy.AddComponent(TypeOf("EnemyBrain"));
            Call(brain, "EnsureComponents");
            TypeOf("EnemyBrain").GetField("encounterMode", Flags).SetValue(brain, Enum.Parse(TypeOf("EnemyEncounterMode"), "LaboratoryAmbush"));
            Call(brain, "InitializeEncounter");
            var health = enemy.GetComponent(TypeOf("Health"));
            Assert.That(Value(health, "Maximum"), Is.EqualTo(100 + wrong * 50));
            Assert.That(Value(health, "IsInvulnerable"), Is.EqualTo(wrong > 3));
            Call(health, "TakeDamage", 50);
            Assert.That(Value(health, "Current"), Is.EqualTo(100 + wrong * 50 - (wrong > 3 ? 0 : 50)));
            var target = player.AddComponent(TypeOf("Health"));
            Call(target, "SetArmorEquipped", true);
            Assert.That(Call(enemy.GetComponent(TypeOf("EnemyAttack")), "TryAttack", target), Is.True);
            Assert.That(Value(target, "IsDead"), Is.EqualTo(wrong > 3));
        }
        finally { UnityEngine.Object.DestroyImmediate(enemy); UnityEngine.Object.DestroyImmediate(player); }
    }
}
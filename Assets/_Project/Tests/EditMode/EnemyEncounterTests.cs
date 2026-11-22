using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyEncounterTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject root;
    private static Type Runtime(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static object Static(string type, string method, params object[] args) => Runtime(type).GetMethod(method, BindingFlags.Static | BindingFlags.Public).Invoke(null, args);
    private static void Field(object target, string name, object value) => Runtime("EnemyBrain").GetField(name, Flags).SetValue(target, value);
    private static object Value(object target, string property) => target.GetType().GetProperty(property, Flags).GetValue(target);
    private GameObject Object(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform);
        return go;
    }
    private Component Brain(string mode, GameObject player = null, GameObject npc = null)
    {
        GameObject enemy = Object("Encounter enemy");
        enemy.AddComponent<SpriteRenderer>(); enemy.AddComponent<BoxCollider2D>();
        Component brain = enemy.AddComponent(Runtime("EnemyBrain"));
        Field(brain, "encounterMode", Enum.Parse(Runtime("EnemyEncounterMode"), mode));
        Field(brain, "player", player);
        Field(brain, "archaeologistToReveal", npc);
        Call(brain, "EnsureComponents");
        Field(brain, "movement", enemy.GetComponent(Runtime("GridMovement")));
        Call(brain, "OnEnable");
        return brain;
    }
    private static void UnlockArchaeologist()
    {
        Static("StoryProgress", "Complete", "q1_spirit");
        Static("StoryProgress", "TryClearTowerEnemies");
        Static("StoryProgress", "TryReceiveArchaeologistSignal");
        Static("StoryProgress", "Complete", "q2_archaeologist");
    }
    [SetUp]
    public void SetUp()
    {
        Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
        root = new GameObject("Enemy encounter tests");
    }
    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        Static("GameplayState", "Reset"); Static("StoryProgress", "Reset");
    }

    [Test]
    public void LaboratoryEnemiesMoveAndAttackFasterWithoutChangingNormalEnemies()
    {
        var lab = Brain("LaboratoryAmbush");
        var normal = Brain("Normal");
        var labAttack = lab.GetComponent(Runtime("EnemyAttack"));
        var normalAttack = normal.GetComponent(Runtime("EnemyAttack"));
        Call(labAttack, "Awake"); Call(normalAttack, "Awake");
        Assert.That(Value(lab, "EffectiveChaseSpeed"), Is.EqualTo(3.5f));
        Assert.That(Value(normal, "EffectiveChaseSpeed"), Is.EqualTo(2f));
        Assert.That(Value(labAttack, "AttackInterval"), Is.EqualTo(.8f));
        Assert.That(Value(normalAttack, "AttackInterval"), Is.EqualTo(1.5f));
        Call(lab, "InitializeEncounter");
        Assert.That(Value(lab, "PursuesPlayerAfterTrigger"), Is.False);
        UnlockArchaeologist();
        Static("StoryProgress", "TryEnterResearchRoom");
        Static("StoryProgress", "TryCompleteResearchDocument", 5, "Next destination");
        Assert.That(Value(lab, "PursuesPlayerAfterTrigger"), Is.True);
    }

    [Test]
    public void MirrorEnemyWaitsForStoryRewardThenAttacksAndKeepsPursuing()
    {
        GameObject player = Object("Test player");
        Component inventory = player.AddComponent(Runtime("PlayerInventory"));
        Component health = player.AddComponent(Runtime("Health"));
        Call(health, "Configure", 200, 200);
        Component brain = Brain("MirrorAmbush", player);
        Call(brain, "InitializeEncounter");
        Component attack = brain.GetComponent(Runtime("EnemyAttack"));
        Assert.That(Value(brain, "IsEncounterActive"), Is.False);
        Assert.That(Call(attack, "TryAttack", health), Is.False);
        Call(inventory, "SetEquipped", true);
        Call(brain, "RefreshEncounter");
        Assert.That(Value(brain, "IsEncounterActive"), Is.False, "Equipment alone must not skip the mirror conversation.");
        Static("StoryProgress", "Complete", "q1_spirit");
        Call(inventory, "SetEquipped", false);
        Call(brain, "RefreshEncounter");
        Assert.That(Value(brain, "IsEncounterActive"), Is.False, "Beat completion alone must not precede the actual reward.");
        Call(inventory, "SetEquipped", true);
        Call(brain, "RefreshEncounter");
        Assert.That(Value(brain, "IsEncounterActive"), Is.True);
        Assert.That(Value(brain, "PursuesPlayerAfterTrigger"), Is.True);
        Assert.That(Call(attack, "TryAttack", health), Is.True);
        Assert.That(Value(health, "Armor"), Is.EqualTo(70));
        Call(inventory, "SetEquipped", false);
        Call(brain, "RefreshEncounter");
        Assert.That(Value(brain, "IsEncounterActive"), Is.True, "The activated ambush must not reset on unequip.");
    }

    [Test]
    public void GuardDeathRevealsArchaeologistAtSpawnPointOnlyOnce()
    {
        GameObject npc = Object("Archaeologist");
        Component brain = Brain("ArchaeologistGuard", null, npc);
        GameObject spawn = Object("NPC spawn"); spawn.transform.position = new Vector3(3, 2, 0);
        Field(brain, "archaeologistSpawnPoint", spawn.transform);
        Call(brain, "InitializeEncounter");
        Assert.That(npc.activeSelf, Is.False);
        Assert.That(Value(brain, "IsEncounterActive"), Is.True);
        Component health = brain.GetComponent(Runtime("Health"));
        Call(health, "TakeDamage", 100);
        Assert.That(npc.activeSelf, Is.True);
        Assert.That(npc.transform.position, Is.EqualTo(spawn.transform.position));
        Assert.That(brain.gameObject.activeSelf, Is.False);
        npc.transform.position = new Vector3(8, 8, 0);
        Call(brain, "Die");
        Assert.That(npc.transform.position, Is.EqualTo(new Vector3(8, 8, 0)));
        Assert.That(Static("StoryProgress", "IsCompleted", "q2_archaeologist"), Is.False,
            "Rescuing the NPC must still require answering his question.");
    }

    [Test]
    public void LabEnemyRequiresRoomEntryAndSuccessfulDecodingAndRestoresOriginalVisibility()
    {
        Component brain = Brain("LaboratoryAmbush");
        GameObject disabledChild = Object("Originally disabled collider");
        disabledChild.transform.SetParent(brain.transform);
        var disabledCollider = disabledChild.AddComponent<BoxCollider2D>(); disabledCollider.enabled = false;
        Call(brain, "InitializeEncounter");
        Assert.That(brain.GetComponent<SpriteRenderer>().enabled, Is.False);
        Assert.That(brain.GetComponent<BoxCollider2D>().enabled, Is.False);
        Assert.That(Value(brain, "IsEncounterActive"), Is.False);
        Assert.That(Static("StoryProgress", "TryEnterResearchRoom"), Is.False);
        Assert.That(Static("StoryProgress", "TryDecodeResearchDocuments"), Is.False);
        UnlockArchaeologist();
        Assert.That(Static("StoryProgress", "TryDecodeResearchDocuments"), Is.False);
        Assert.That(Static("StoryProgress", "TryEnterResearchRoom"), Is.True);
        Call(brain, "RefreshEncounter");
        Assert.That(Value(brain, "IsEncounterActive"), Is.False, "Room entry is not successful decoding.");
        Assert.That(Static("StoryProgress", "TryDecodeResearchDocuments"), Is.True);
        Assert.That(Static("StoryProgress", "TryDecodeResearchDocuments"), Is.False);
        Call(brain, "RefreshEncounter");
        Assert.That(Value(brain, "IsEncounterActive"), Is.True);
        Assert.That(brain.GetComponent<SpriteRenderer>().enabled, Is.True);
        Assert.That(brain.GetComponent<BoxCollider2D>().enabled, Is.True);
        Assert.That(disabledCollider.enabled, Is.False);
    }

    [Test]
    public void LabEnemyCreatedAfterDecodingStartsVisibleAndSessionResetClearsLabFlags()
    {
        UnlockArchaeologist();
        Static("StoryProgress", "TryEnterResearchRoom"); Static("StoryProgress", "TryDecodeResearchDocuments");
        Component brain = Brain("LaboratoryAmbush");
        Call(brain, "InitializeEncounter");
        Assert.That(Value(brain, "IsEncounterActive"), Is.True);
        Assert.That(brain.GetComponent<SpriteRenderer>().enabled, Is.True);
        Static("StoryProgress", "Reset");
        Assert.That(Runtime("StoryProgress").GetProperty("HasEnteredResearchRoom").GetValue(null), Is.False);
        Assert.That(Runtime("StoryProgress").GetProperty("HasDecodedResearchDocuments").GetValue(null), Is.False);
    }

    [Test]
    public void SceneAndLabPrefabsHaveTheExpectedEncounterBindings()
    {
        var preview = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(
            "Assets/_Project/Scenes/Sence_1/Scene1.unity");
        try
        {
            Component mirrorEnemy = null, guard = null;
            foreach (GameObject go in preview.GetRootGameObjects())
            {
                if (go.name == "Monster (4)") mirrorEnemy = go.GetComponent(Runtime("EnemyBrain"));
                if (go.name == "Monster (3)") guard = go.GetComponent(Runtime("EnemyBrain"));
            }
            Assert.That(mirrorEnemy, Is.Not.Null); Assert.That(guard, Is.Not.Null);
            Assert.That(Value(mirrorEnemy, "EncounterMode").ToString(), Is.EqualTo("MirrorAmbush"));
            Assert.That(Value(guard, "EncounterMode").ToString(), Is.EqualTo("ArchaeologistGuard"));
            var npc = (GameObject)Runtime("EnemyBrain").GetField("archaeologistToReveal", Flags).GetValue(guard);
            Assert.That(npc, Is.Not.Null); Assert.That(npc.name, Is.EqualTo("ScientistNPC"));
            var monsterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Gameplay/Characters/Enemies/Prefabs/Monsters/Monster_Laboratory.prefab");
            Assert.That(Value(monsterPrefab.GetComponent(Runtime("EnemyBrain")), "EncounterMode").ToString(), Is.EqualTo("LaboratoryAmbush"));
            var roomPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Gameplay/Interaction/Prefabs/Research/ResearchRoomStoryTrigger.prefab");
            Assert.That(roomPrefab.GetComponent<BoxCollider2D>().isTrigger, Is.True);
            Assert.That(roomPrefab.GetComponent(Runtime("ResearchRoomStoryTrigger")), Is.Not.Null);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
    }
}

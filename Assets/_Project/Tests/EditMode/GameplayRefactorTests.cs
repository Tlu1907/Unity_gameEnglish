using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

// Reflection keeps tests independent of the predefined Assembly-CSharp assembly.
// No runtime asmdef/package changes are needed just to expose gameplay to tests.
public sealed class GameplayRefactorTests
{
    private GameObject root;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type RuntimeType(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static object Static(string type, string method, params object[] args) =>
        RuntimeType(type).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static object Value(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static void Field(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private Component Add(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform);
        return go.AddComponent(RuntimeType(name));
    }
    private static string State => RuntimeType("GameplayState").GetProperty("Current").GetValue(null).ToString();
    [SetUp]
    public void SetUp()
    {
        Static("GameplayState", "Reset");
        Static("QuizController", "ResetStatics");
        root = new GameObject("Refactor tests");
    }
    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        Static("QuizController", "ResetStatics");
        Static("GameplayState", "Reset");
    }

    [Test]
    public void PhysicsPlayerUsesVelocityAndStopsOnReleasePauseAndInvalidGround()
    {
        Component motor = Add("GridMovement");
        var texture = new Texture2D(20, 20);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 20, 20), Vector2.one * .5f, 1f);
        try
        {
            var background = new GameObject("MapBackground");
            background.transform.SetParent(root.transform);
            var renderer = background.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            Field(motor, "walkableBackground", renderer);
            Call(motor, "EnablePlayerPhysics");
            Call(motor, "OnEnable");
            var body = motor.GetComponent<Rigidbody2D>();
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(body.gravityScale, Is.EqualTo(0f));
            Call(motor, "SetMovementInput", Vector3.right, 4f);
            Call(motor, "FixedUpdate");
            Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(4f, 0f)));
            Assert.That(Call(motor, "TryMove", Vector3.up, 4f, false), Is.EqualTo(false));
            Call(motor, "SetMovementInput", Vector3.zero, 4f);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Call(motor, "SetMovementInput", Vector3.down, 4f);
            Call(motor, "FixedUpdate");
            Static("GameplayState", "Pause");
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Static("GameplayState", "Resume");
            body.position = new Vector2(9.99f, 0f);
            Call(motor, "SetMovementInput", Vector3.right, 4f);
            Call(motor, "FixedUpdate");
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
    [Test]
    public void EmptyGroundUsesBackgroundBoundsButPaintedGroundKeepsHolesBlocked()
    {
        Component motor = Add("GridMovement");
        var texture = new Texture2D(4, 4);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 1f);
        var tile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            var background = new GameObject("MapBackground");
            background.transform.SetParent(root.transform);
            var renderer = background.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            Field(motor, "walkableBackground", renderer);
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(root.transform);
            var ground = new GameObject("Ground", typeof(Tilemap));
            ground.transform.SetParent(grid.transform);
            var tilemap = ground.GetComponent<Tilemap>();
            Field(motor, "groundTilemap", tilemap);
            Assert.That(Call(motor, "HasGround", new Vector3(.5f, .5f, 9f)), Is.EqualTo(true));
            Assert.That(Call(motor, "HasGround", new Vector3(3f, 0f, 0f)), Is.EqualTo(false));
            tilemap.SetTile(Vector3Int.zero, tile);
            Assert.That(Call(motor, "HasGround", new Vector3(.5f, .5f, 0f)), Is.EqualTo(true));
            Assert.That(Call(motor, "HasGround", new Vector3(1.5f, .5f, 0f)), Is.EqualTo(false));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tile);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void EquipmentChangesUseInventoryAndNotifyOnlyOnChanges()
    {
        Component inventory = Add("PlayerInventory");
        int changes = 0;
        inventory.GetType().GetEvent("Changed").AddEventHandler(inventory, (Action)(() => changes++));
        Call(inventory, "SetEquipped", true);
        Call(inventory, "SetEquipped", true);
        Assert.That(Value(inventory, "HasWeapon"), Is.EqualTo(true));
        Assert.That(changes, Is.EqualTo(1));
        Call(inventory, "SetEquipped", false);
        Assert.That(Value(inventory, "HasWeapon"), Is.EqualTo(false));
        Assert.That(changes, Is.EqualTo(2));
    }

    [Test]
    public void BlockedGridRequestFacesCardinalDirectionWithoutStartingWalk()
    {
        Component visuals = Add("CharacterAnimation");
        Component motor = visuals.gameObject.AddComponent(RuntimeType("GridMovement"));
        Call(motor, "Awake");
        Assert.That(Call(motor, "TryMove", new Vector3(1f, 1f, 0f), 5f, false), Is.EqualTo(false));
        Assert.That(Value(motor, "IsMoving"), Is.EqualTo(false));
        Assert.That(visuals.GetType().GetField("facing", Flags).GetValue(visuals), Is.EqualTo(Vector2.up));
        Call(visuals, "Face", Vector3.zero);
        Assert.That(visuals.GetType().GetField("facing", Flags).GetValue(visuals), Is.EqualTo(Vector2.up));
        Assert.That(visuals.GetType().GetField("running", Flags).GetValue(visuals), Is.EqualTo(false));
    }

    [Test]
    public void MigrationCopiesLegacyValuesOnceAndKeepsNewComponentEdits()
    {
        Component player = Add("PlayerMovement");
        Field(player, "speed", 6.25f);
        Field(player, "maxHealth", 150);
        Field(player, "currentHealth", 90);
        Assert.That(Call(player, "Migrate", 1000), Is.EqualTo(true));
        Component health = player.GetComponent(RuntimeType("Health"));
        Assert.That(Value(health, "Maximum"), Is.EqualTo(150));
        Assert.That(Value(health, "Current"), Is.EqualTo(90));
        Component input = player.GetComponent(RuntimeType("PlayerInputReader"));
        Assert.That(input.GetType().GetField("speed", Flags).GetValue(input), Is.EqualTo(6.25f));
        Call(health, "Configure", 300, 250);
        Field(input, "speed", 8f);
        Assert.That(Call(player, "Migrate", 1000), Is.EqualTo(false));
        Assert.That(Value(health, "Current"), Is.EqualTo(250));
        Assert.That(input.GetType().GetField("speed", Flags).GetValue(input), Is.EqualTo(8f));
        Assert.That(player.GetComponents(RuntimeType("GridMovement")).Length, Is.EqualTo(1));
    }

    [Test]
    public void DamageIsBlockedDuringQuizAndDeathOccursOnce()
    {
        Component health = Add("Health");
        Call(health, "Configure", 100, 80);
        int deaths = 0;
        health.GetType().GetEvent("Died").AddEventHandler(health, (Action)(() => deaths++));
        object owner = new object();
        Static("GameplayState", "BeginQuiz", owner);
        Call(health, "TakeDamage", 500);
        Assert.That(Value(health, "Current"), Is.EqualTo(80));
        Static("GameplayState", "EndQuiz", owner);
        Call(health, "TakeDamage", 30);
        Assert.That(Value(health, "Current"), Is.EqualTo(50));
        Call(health, "Heal", 500);
        Assert.That(Value(health, "Current"), Is.EqualTo(100));
        Call(health, "TakeDamage", 500);
        Call(health, "TakeDamage", 500);
        Assert.That(Value(health, "Current"), Is.EqualTo(0));
        Assert.That(deaths, Is.EqualTo(1));
    }

    [Test]
    public void QuizRestoresPauseAndNeverRevivesGameOver()
    {
        object owner = new object();
        Static("GameplayState", "Pause");
        Assert.That(Static("GameplayState", "BeginQuiz", owner), Is.EqualTo(true));
        Static("GameplayState", "EndQuiz", new object());
        Assert.That(State, Is.EqualTo("Quiz"));
        Static("GameplayState", "EndQuiz", owner);
        Assert.That(State, Is.EqualTo("Paused"));
        Static("GameplayState", "Resume");
        Static("GameplayState", "BeginQuiz", owner);
        Static("GameplayState", "GameOver");
        Static("GameplayState", "EndQuiz", owner);
        Assert.That(State, Is.EqualTo("GameOver"));
    }

    [Test]
    public void DirectChestQuizKeepsClosedOnWrongAndSwapsSpriteOnlyOnCorrectAnswer()
    {
        var question = ScriptableObject.CreateInstance(RuntimeType("QuestionData"));
        var texture = new Texture2D(4, 4);
        Sprite closed = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
        Sprite opened = Sprite.Create(texture, new Rect(2, 0, 2, 2), Vector2.one * .5f);
        try
        {
            Field(question, "id", "chest_test");
            Field(question, "answers", new[] { "wrong", "correct" });
            Field(question, "correctIndex", 1);
            Component panel = Add("QuizPanel");
            Component controller = Add("QuizController");
            Field(controller, "panel", panel.gameObject);
            Call(controller, "Awake");
            Component chest = Add("Chest");
            var visual = new GameObject("visual");
            visual.transform.SetParent(chest.transform);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = closed;
            var collider = chest.gameObject.AddComponent<BoxCollider2D>();
            // Leave the renderer unassigned to exercise discovery on a child Visual.
            Field(chest, "openedSprite", opened);
            Component interaction = chest.gameObject.AddComponent(RuntimeType("ChestQuizInteraction"));
            Field(interaction, "question", question);
            Field(interaction, "controller", controller);
            Field(interaction, "retryDelay", 0f);
            int rewards = 0;
            chest.GetType().GetEvent("Opened").AddEventHandler(chest, (Action)(() => rewards++));
            Call(chest, "Interact");
            Assert.That(State, Is.EqualTo("Quiz"));
            Call(controller, "SubmitAnswer", 0);
            Assert.That(Value(chest, "IsOpened"), Is.EqualTo(false));
            Assert.That(renderer.sprite, Is.SameAs(closed));
            Call(chest, "Interact");
            Call(controller, "CloseQuestion");
            Assert.That(Value(chest, "IsOpened"), Is.EqualTo(false));
            Call(chest, "Interact");
            Call(controller, "SubmitAnswer", 1);
            Assert.That(Value(chest, "IsOpened"), Is.EqualTo(true));
            Assert.That(renderer.sprite, Is.SameAs(opened));
            Assert.That(collider.enabled, Is.EqualTo(false));
            Call(chest, "Interact");
            Call(chest, "Open");
            Assert.That(rewards, Is.EqualTo(1));
            Assert.That(State, Is.EqualTo("Playing"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(question);
            UnityEngine.Object.DestroyImmediate(closed);
            UnityEngine.Object.DestroyImmediate(opened);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void WrongAndCancelledQuizKeepChestLockedAndCorrectRewardsOnlyOnce()
    {
        var question = ScriptableObject.CreateInstance(RuntimeType("QuestionData"));
        try
        {
            Field(question, "id", "test");
            Field(question, "answers", new[] { "wrong", "correct" });
            Field(question, "correctIndex", 1);
            Component panel = Add("QuizPanel");
            Component controller = Add("QuizController");
            Field(controller, "panel", panel.gameObject);
            Call(controller, "Awake");
            Component chest = Add("Chest");
            Component trigger = Add("QuizTrigger");
            Field(trigger, "question", question);
            Field(trigger, "relatedChest", chest);
            Field(trigger, "controller", controller);
            Call(trigger, "ConfigureLinks", true);
            int rewards = 0;
            chest.GetType().GetEvent("Opened").AddEventHandler(chest, (Action)(() => rewards++));
            Call(chest, "Open");
            Assert.That(Value(chest, "IsOpened"), Is.EqualTo(false));
            Call(trigger, "Interact");
            Call(controller, "SubmitAnswer", 0);
            Assert.That(Value(chest, "IsLocked"), Is.EqualTo(true));
            Assert.That(State, Is.EqualTo("Playing"));
            Call(trigger, "Interact");
            Call(controller, "CloseQuestion");
            Assert.That(Value(chest, "IsLocked"), Is.EqualTo(true));
            Call(trigger, "Interact");
            Call(controller, "SubmitAnswer", 1);
            Call(controller, "SubmitAnswer", 1);
            Call(chest, "Open");
            Assert.That(Value(chest, "IsOpened"), Is.EqualTo(true));
            Assert.That(rewards, Is.EqualTo(1));
            Assert.That(State, Is.EqualTo("Playing"));
        }
        finally { UnityEngine.Object.DestroyImmediate(question); }
    }
}


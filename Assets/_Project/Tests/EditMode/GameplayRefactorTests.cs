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
    public void PlayerSwingHitsOnlyForwardOncePerMonsterAndRespectsCooldown()
    {
        Component attack = Add("PlayerAttack");
        Component visuals = attack.gameObject.AddComponent(RuntimeType("CharacterAnimation"));
        Component inventory = attack.gameObject.AddComponent(RuntimeType("PlayerInventory"));
        Call(visuals, "Face", Vector3.right);
        Component front = Add("EnemyBrain");
        Component back = Add("EnemyBrain");
        Call(front, "EnsureComponents");
        Call(back, "EnsureComponents");
        Call(front, "OnDisable");
        Call(front, "OnEnable");
        front.transform.position = Vector3.right;
        back.transform.position = Vector3.left;
        front.gameObject.AddComponent<BoxCollider2D>();
        back.gameObject.AddComponent<BoxCollider2D>();
        var child = new GameObject("Extra monster collider");
        child.transform.SetParent(front.transform, false);
        child.AddComponent<BoxCollider2D>();
        Component frontHealth = front.GetComponent(RuntimeType("Health"));
        Component backHealth = back.GetComponent(RuntimeType("Health"));
        Physics2D.SyncTransforms();
        Assert.That(Call(attack, "TryAttack"), Is.EqualTo(true));
        Assert.That(Value(frontHealth, "Current"), Is.EqualTo(75));
        Assert.That(Value(backHealth, "Current"), Is.EqualTo(100));
        Assert.That(Call(attack, "TryAttack"), Is.EqualTo(false));
        Assert.That(Value(frontHealth, "Current"), Is.EqualTo(75));
        Call(inventory, "SetEquipped", true);
        Field(attack, "cooldown", 0f);
        Call(attack, "TryAttack");
        Assert.That(Value(frontHealth, "Current"), Is.EqualTo(25));
        Field(attack, "cooldown", 0f);
        Call(attack, "TryAttack");
        Assert.That(Value(frontHealth, "IsDead"), Is.EqualTo(true));
        Assert.That(front.gameObject.activeSelf, Is.EqualTo(false));
    }

    [Test]
    public void PlayerCannotAttackThroughWallsOrDuringDialogue()
    {
        Component attack = Add("PlayerAttack");
        Component motor = attack.gameObject.AddComponent(RuntimeType("GridMovement"));
        Field(motor, "obstacleMask", (LayerMask)(1 << 6));
        Component enemy = Add("EnemyBrain");
        Call(enemy, "EnsureComponents");
        enemy.transform.position = Vector3.down;
        enemy.gameObject.AddComponent<BoxCollider2D>();
        Component health = enemy.GetComponent(RuntimeType("Health"));
        var wall = new GameObject("Wall");
        wall.transform.SetParent(root.transform);
        wall.layer = 6;
        wall.transform.position = Vector3.down * .5f;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1, .1f);
        Physics2D.SyncTransforms();
        Call(attack, "TryAttack");
        Assert.That(Value(health, "Current"), Is.EqualTo(100));
        wall.SetActive(false);
        Physics2D.SyncTransforms();
        Field(attack, "cooldown", 0f);
        Static("GameplayState", "BeginDialogue", attack);
        Assert.That(Call(attack, "TryAttack"), Is.EqualTo(false));
        Assert.That(Value(health, "Current"), Is.EqualTo(100));
    }

    [Test]
    public void MonsterAttackDealsThirtyAndCannotBypassCooldownByReenteringRange()
    {
        Component attack = Add("EnemyAttack");
        Component health = Add("Health");
        Assert.That(Call(attack, "TryAttack", health), Is.EqualTo(true));
        Assert.That(Value(health, "Current"), Is.EqualTo(170));
        Assert.That(attack.GetType().GetField("cooldown", Flags).GetValue(attack), Is.EqualTo(1.5f));
        Assert.That(Call(attack, "TryAttack", health), Is.EqualTo(false));
        health.transform.position = Vector3.right * 10;
        Field(attack, "cooldown", 0f);
        Assert.That(Call(attack, "TryAttack", health), Is.EqualTo(false));
        health.transform.position = Vector3.zero;
        Call(health, "SetArmorEquipped", true);
        Assert.That(Call(attack, "TryAttack", health), Is.EqualTo(true));
        Assert.That(Value(health, "Armor"), Is.EqualTo(70));
        Assert.That(Value(health, "Current"), Is.EqualTo(170));
        Field(attack, "cooldown", 0f);
        Static("GameplayState", "Pause");
        Assert.That(Call(attack, "TryAttack", health), Is.EqualTo(false));
    }

    [Test]
    public void MonsterPatrolContainsExactlyTwentyFiveCells()
    {
        // Use an inactive object to inspect the patrol boundary without initializing gameplay.
        var go = new GameObject("Patrol boundary");
        go.transform.SetParent(root.transform);
        go.SetActive(false);
        Component brain = go.AddComponent(RuntimeType("EnemyBrain"));
        int admitted = 0;
        for (int x = -4; x <= 4; x++)
            for (int y = -4; y <= 4; y++)
                if ((bool)Call(brain, "IsInsidePatrolArea", new Vector2Int(x, y))) admitted++;
        Assert.That(admitted, Is.EqualTo(25));
        Assert.That(Call(brain, "IsInsidePatrolArea", new Vector2Int(3, 0)), Is.EqualTo(false));
    }

    [Test]
    public void ArmorAbsorbsDamageBeforeHealthAndPassesOverflow()
    {
        Component health = Add("Health");
        Assert.That(Value(health, "Armor"), Is.EqualTo(0));
        Call(health, "SetArmorEquipped", true);
        Assert.That(Value(health, "Armor"), Is.EqualTo(100));
        Call(health, "TakeDamage", 30);
        Assert.That(Value(health, "Armor"), Is.EqualTo(70));
        Assert.That(Value(health, "Current"), Is.EqualTo(200));
        Call(health, "SetArmorEquipped", true);
        Assert.That(Value(health, "Armor"), Is.EqualTo(70));
        Call(health, "TakeDamage", 90);
        Assert.That(Value(health, "Armor"), Is.EqualTo(0));
        Assert.That(Value(health, "Current"), Is.EqualTo(180));
    }

    [Test]
    public void InventoryEquipmentGrantsAndRemovesArmor()
    {
        Component inventory = Add("PlayerInventory");
        Component health = inventory.gameObject.AddComponent(RuntimeType("Health"));
        Call(inventory, "SetEquipped", true);
        Assert.That(Value(health, "Armor"), Is.EqualTo(100));
        Call(inventory, "SetEquipped", false);
        Assert.That(Value(health, "Armor"), Is.EqualTo(0));
    }

    [Test]
    public void FlaskSessionKeepsGradeVisibleAndCompletesOnlyOnceOnClose()
    {
        float originalTimeScale = Time.timeScale;
        var go = new GameObject("Flask quiz");
        go.transform.SetParent(root.transform);
        go.SetActive(false);
        Component panel = go.AddComponent(RuntimeType("FlaskEnglishQuizPanel"));
        Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
        var status = new GameObject("Status", typeof(RectTransform), textType);
        var result = new GameObject("Result", typeof(RectTransform), textType);
        status.transform.SetParent(go.transform);
        result.transform.SetParent(go.transform);
        Field(panel, "statusText", status.GetComponent(textType));
        Field(panel, "resultText", result.GetComponent(textType));
        int completions = 0;
        bool passed = false;
        Field(panel, "completed", (Action<bool>)(value => { completions++; passed = value; }));
        try
        {
            Assert.That(Static("FlaskEnglishQuizPanel", "IsReviewRequired", "{\"status\":\"review_required\"}"), Is.EqualTo(true));
            Static("GameplayState", "BeginQuiz", panel);
            Field(panel, "<IsSessionOpen>k__BackingField", true);
            Field(panel, "previousTimeScale", .75f);
            Time.timeScale = 0f;
            Call(panel, "ReadGrade", "{\"status\":\"validated\",\"can_award_reward\":true,\"data\":{\"total_score\":8,\"max_score\":10,\"feedback_vi\":\"Good\",\"corrected_answer\":\"Answer\"}}");
            Assert.That(completions, Is.EqualTo(0));
            Assert.That(State, Is.EqualTo("Quiz"));
            Assert.That(Value(panel, "CanAwardReward"), Is.EqualTo(true));
            Call(panel, "OnDisable");
            Call(panel, "OnDisable");
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(passed, Is.EqualTo(true));
            Assert.That(State, Is.EqualTo("Playing"));
            Assert.That(Time.timeScale, Is.EqualTo(.75f));
        }
        finally { Time.timeScale = originalTimeScale; }
    }

    [Test]
    public void IntroDialogueAdvancesSevenPairsAndRestoresTimeOnCloseOrDisable()
    {
        float originalTimeScale = Time.timeScale;
        Component dialogue = Add("NPCIntroDialogue");
        dialogue.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var panel = new GameObject("DialoguePanel");
        panel.transform.SetParent(dialogue.transform);
        Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
        var english = new GameObject("English", typeof(RectTransform), textType);
        var vietnamese = new GameObject("Vietnamese", typeof(RectTransform), textType);
        english.transform.SetParent(panel.transform);
        vietnamese.transform.SetParent(panel.transform);
        Component englishText = english.GetComponent(textType);
        Component vietnameseText = vietnamese.GetComponent(textType);
        Field(dialogue, "dialoguePanel", panel);
        Field(dialogue, "englishText", englishText);
        Field(dialogue, "vietnameseText", vietnameseText);
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
            "Assets/_Project/Data/Dialogues/Linh_CityIntro.asset");
        Assert.That(data, Is.Not.Null);
        Field(dialogue, "dialogue", data);
        try
        {
            Time.timeScale = .75f;
            Call(dialogue, "Open");
            Assert.That(State, Is.EqualTo("Dialogue"));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(Value(englishText, "text"), Is.EqualTo("This is Linh. I work for the National Security Bureau."));
            Assert.That(Value(vietnameseText, "text"), Is.EqualTo("Tôi là Linh. Tôi làm việc cho Cục An ninh Quốc gia."));
            Assert.That(Value(dialogue, "IsTyping"), Is.EqualTo(true));
            Assert.That(Value(englishText, "maxVisibleCharacters"), Is.EqualTo(0));
            Call(dialogue, "RevealText", .1f);
            Assert.That(Value(englishText, "maxVisibleCharacters"), Is.EqualTo(4));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Call(dialogue, "Advance");
            Assert.That(Value(dialogue, "IsTyping"), Is.EqualTo(false));
            Assert.That(Value(englishText, "text"), Is.EqualTo("This is Linh. I work for the National Security Bureau."));
            Assert.That(Value(englishText, "maxVisibleCharacters"), Is.EqualTo(int.MaxValue));
            Assert.That(Static("GameplayState", "BeginQuiz", new object()), Is.EqualTo(false));
            Static("GameplayState", "EndDialogue", new object());
            Assert.That(State, Is.EqualTo("Dialogue"));
            string previous = (string)Value(englishText, "text");
            for (int i = 0; i < 6; i++)
            {
                Call(dialogue, "Advance");
                Assert.That(Value(dialogue, "IsTyping"), Is.EqualTo(true));
                Call(dialogue, "RevealText", 100f);
                Assert.That(Value(dialogue, "IsTyping"), Is.EqualTo(false));
                string current = (string)Value(englishText, "text");
                Assert.That(current, Is.Not.EqualTo(previous));
                Assert.That(Value(vietnameseText, "text"), Is.Not.Empty);
                previous = current;
                Assert.That(Value(dialogue, "IsOpen"), Is.EqualTo(true));
            }
            Assert.That(previous, Is.EqualTo("Go to the park and check the object. Take any items you find inside."));
            Call(dialogue, "Advance");
            Assert.That(panel.activeSelf, Is.EqualTo(false));
            Assert.That(State, Is.EqualTo("Playing"));
            Assert.That(Time.timeScale, Is.EqualTo(.75f));
            Call(dialogue, "Open");
            Static("GameplayState", "GameOver");
            Call(dialogue, "OnDisable");
            Assert.That(State, Is.EqualTo("GameOver"));
            Assert.That(Time.timeScale, Is.EqualTo(.75f));
        }
        finally { Call(dialogue, "Close"); Time.timeScale = originalTimeScale; }
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
            Field(interaction, "useFlaskQuiz", false);
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


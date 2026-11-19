using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TowerCollisionTests
{
    private Scene scene;
    private GameObject root;
    private Sprite sprite;
    private Texture2D texture;
    private Component fit;
    private SpriteRenderer visual;
    private SimulationMode2D oldSimulationMode;
    [SetUp] public void SetUp()
    {
        oldSimulationMode = Physics2D.simulationMode;
        scene = Application.isPlaying
            ? SceneManager.CreateScene("Tower collision test " + Guid.NewGuid(), new CreateSceneParameters(LocalPhysicsMode.Physics2D))
            : UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        root = new GameObject("Tower"); SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(10, 5, 0); root.transform.localScale = Vector3.one * .57574f;
        var go = new GameObject("Visual"); go.transform.SetParent(root.transform, false);
        go.transform.localPosition = new Vector3(3, 2, 0);
        texture = new Texture2D(128, 256);
        sprite = Sprite.Create(texture, new Rect(0, 0, 128, 256), new Vector2(.5f, .5f), 32);
        visual = go.AddComponent<SpriteRenderer>(); visual.sprite = sprite;
        Type type = Assembly.Load("Assembly-CSharp").GetType("TowerCollisionBounds", true);
        fit = root.AddComponent(type);
        type.GetMethod("RefreshColliders").Invoke(fit, null);
        Physics2D.SyncTransforms();
    }
    [TearDown] public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture);
        Physics2D.simulationMode = oldSimulationMode;
        if (scene.IsValid())
        {
            if (Application.isPlaying) SceneManager.UnloadSceneAsync(scene);
            else UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    [Test] public void OffsetTallSpriteGetsSolidAtItsFootAndLargerInteractionArea()
    {
        var trigger = root.GetComponent<BoxCollider2D>();
        var solid = root.transform.Find("Collision").GetComponent<BoxCollider2D>();
        Assert.That(trigger.isTrigger, Is.True); Assert.That(solid.isTrigger, Is.False);
        Assert.That(solid.bounds.min.y, Is.EqualTo(visual.bounds.min.y).Within(.001f));
        Assert.That(solid.bounds.center.x, Is.EqualTo(visual.bounds.center.x).Within(.001f));
        Assert.That(trigger.bounds.min.y, Is.LessThan(solid.bounds.min.y - .5f));
        Assert.That(trigger.bounds.max.y, Is.GreaterThan(solid.bounds.max.y + .5f));
    }
    [Test] public void PhysicsStopsPlayerAtBaseWhilePlayerOverlapsDialogueTrigger()
    {
        if (!Application.isPlaying) Assert.Ignore("Run this isolated Physics2D simulation in Play Mode.");
        Physics2D.simulationMode = SimulationMode2D.Script;
        var solid = root.transform.Find("Collision").GetComponent<BoxCollider2D>();
        var trigger = root.GetComponent<BoxCollider2D>();
        var player = new GameObject("Physics player"); SceneManager.MoveGameObjectToScene(player, scene);
        GameObject dialogue = null;
        Component panel = null;
        try
        {
            var assembly = Assembly.Load("Assembly-CSharp");
            assembly.GetType("GameplayState").GetMethod("Reset").Invoke(null, null);
            assembly.GetType("StoryProgress").GetMethod("Reset").Invoke(null, null);
            dialogue = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("StoryDialogue"));
            SceneManager.MoveGameObjectToScene(dialogue, scene);
            panel = dialogue.GetComponentInChildren(assembly.GetType("StoryQuizPanel"), true);
            var tower = root.AddComponent(assembly.GetType("TowerDialogueTrigger"));
            tower.GetType().GetField("dialoguePanel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tower, panel);
            player.AddComponent(assembly.GetType("PlayerInventory"));
            player.transform.position = new Vector3(solid.bounds.center.x, solid.bounds.min.y - 2, 0);
            var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = player.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.4f, .4f);
            Physics2D.SyncTransforms();
            var physics = scene.GetPhysicsScene2D();
            for (int i = 0; i < 100; i++) { body.linearVelocity = Vector2.up * 3; physics.Simulate(.02f); }
            Assert.That(collider.bounds.max.y, Is.LessThanOrEqualTo(solid.bounds.min.y + .05f));
            Assert.That(trigger.Distance(collider).isOverlapped, Is.True,
                "The player must reach the dialogue trigger before the solid base blocks movement.");
            Assert.That(panel.GetType().GetProperty("IsOpen").GetValue(panel), Is.True,
                "A real Physics2D trigger callback must open the tower conversation without invoking it manually.");
        }
        finally
        {
            if (panel != null) panel.GetType().GetMethod("Close").Invoke(panel, null);
            if (dialogue != null) UnityEngine.Object.DestroyImmediate(dialogue);
            UnityEngine.Object.DestroyImmediate(player);
        }
    }
}

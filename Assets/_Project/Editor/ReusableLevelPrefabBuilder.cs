using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>Exports a reusable shell from a snapshot; never restructures or saves over the source scene.</summary>
[InitializeOnLoad]
public static class ReusableLevelPrefabBuilder
{
    public const string Output = "Assets/_Project/Scenes/Sence_1/Prefabs/ReusableLevelTemplate.prefab";
    private const string Source = "Assets/_Project/Scenes/Sence_1/Scene1_Merged.unity";
    private const string Request = "Library/ReusableLevelPrefabBuilder.request";
    private const string Result = "Library/ReusableLevelPrefabBuilder.result";
    private static bool running;
    static ReusableLevelPrefabBuilder() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action;
        try { action = File.ReadAllText(Request).Trim(); File.Delete(Request); }
        catch (IOException) { return; }
        if (action == "verify") VerifySaved(); else Export();
    }

    [MenuItem("Tools/Levels/Export Reusable Level Template from Map 1")]
    public static void Export()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        Scene preview = default;
        const string snapshot = "Assets/_Project/Scenes/Sence_1/Prefabs/ReusableLevelSnapshot.unity";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            AssetDatabase.Refresh();
            Scene loaded = SceneManager.GetSceneByPath(Source);
            string sourcePath = Source;
            if (loaded.IsValid() && loaded.isLoaded)
            {
                // Includes unsaved layout edits without modifying the original scene.
                if (!EditorSceneManager.SaveScene(loaded, snapshot, true)) throw new IOException("Could not snapshot Map 1.");
                sourcePath = snapshot;
            }
            preview = EditorSceneManager.OpenPreviewScene(sourcePath);
            var originalRoots = preview.GetRootGameObjects();
            foreach (var original in originalRoots)
                if (PrefabUtility.IsAnyPrefabInstanceRoot(original))
                    PrefabUtility.UnpackPrefabInstance(original, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var root = new GameObject("ReusableLevelTemplate");
            SceneManager.MoveGameObjectToScene(root, preview);
            foreach (var original in originalRoots) original.transform.SetParent(root.transform, true);
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate != null && PrefabUtility.IsAnyPrefabInstanceRoot(candidate.gameObject))
                    PrefabUtility.UnpackPrefabInstance(candidate.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var player = root.GetComponentsInChildren<PlayerMovement>(true).FirstOrDefault();
            var camera = root.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (player == null || camera == null) throw new InvalidOperationException("Map 1 needs a Player and Main Camera.");
            var ui = root.transform.Find("UI");
            if (ui == null) throw new InvalidOperationException("Map 1 UI group was not found.");

            // Keep functional systems without copying Map 1's chests, traps or quest state.
            var systems = new GameObject("Systems"); systems.transform.SetParent(root.transform, false);
            var shared = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(component => component is QuizController || component is LevelSessionActions || component is EventSystem).ToArray();
            foreach (var component in shared)
            {
                if (component == null || component.transform.IsChildOf(ui) || component.transform.IsChildOf(player.transform)) continue;
                Transform container = component.transform;
                if (component is QuizController)
                    while (container.parent != null && container.parent != root.transform && container.parent.name != "Systems") container = container.parent;
                if (container != systems.transform) container.SetParent(systems.transform, true);
            }
            // Preserve the authored 2D lighting, wherever it was grouped in Map 1.
            var lighting = new GameObject("Lighting"); lighting.transform.SetParent(root.transform, false);
            foreach (var light in root.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))
                if (!light.transform.IsChildOf(player.transform) && !light.transform.IsChildOf(ui)) light.transform.SetParent(lighting.transform, true);

            var keep = new HashSet<Transform> { player.transform, camera.transform, ui, systems.transform, lighting.transform };
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (!keep.Contains(child)) Object.DestroyImmediate(child.gameObject);

            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
                if (!collider.transform.IsChildOf(player.transform)) Object.DestroyImmediate(collider);
            foreach (var mission in root.GetComponentsInChildren<LevelObjectives>(true)) Object.DestroyImmediate(mission);
            foreach (var mission in root.GetComponentsInChildren<ObjectiveUI>(true)) Object.DestroyImmediate(mission);
            foreach (var intro in root.GetComponentsInChildren<NPCIntroDialogue>(true))
            {
                var settings = new SerializedObject(intro);
                settings.FindProperty("playOnStart").boolValue = true;
                var panel = settings.FindProperty("dialoguePanel").objectReferenceValue as GameObject;
                if (panel != null) panel.SetActive(false);
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var story in root.GetComponentsInChildren<StoryQuizPanel>(true)) story.gameObject.SetActive(false);
            foreach (var journal in root.GetComponentsInChildren<StoryClueJournal>(true))
            {
                var settings = new SerializedObject(journal);
                var notebook = settings.FindProperty("notebook").objectReferenceValue as GameObject;
                if (notebook != null) notebook.SetActive(false);
            }

            var terrainObject = new GameObject("Terrain"); terrainObject.transform.SetParent(root.transform, false);
            var background = new GameObject("MapBackground", typeof(SpriteRenderer)); background.transform.SetParent(terrainObject.transform, false);
            var backgroundRenderer = background.GetComponent<SpriteRenderer>();
            backgroundRenderer.sortingLayerName = "MapBackground";
            backgroundRenderer.sortingOrder = -100;
            var grid = new GameObject("Grid", typeof(Grid), typeof(CityTerrain)); grid.transform.SetParent(terrainObject.transform, false);
            var ground = Layer(grid.transform, "Terrain_Ground");
            var stairs = Layer(grid.transform, "Terrain_Stairs");
            var bridges = Layer(grid.transform, "Terrain_Bridges");
            var obstacles = Layer(grid.transform, "Terrain_Obstacles");
            obstacles.gameObject.AddComponent<TilemapCollider2D>();
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer < 0) obstacleLayer = LayerMask.NameToLayer("Obstacles");
            if (obstacleLayer >= 0) obstacles.gameObject.layer = obstacleLayer;
            foreach (string name in new[] { "Props", "Monsters", "NPCs", "QuestLocations" })
            { var group = new GameObject(name); group.transform.SetParent(root.transform, false); }
            var spawn = new GameObject("PlayerSpawn"); spawn.transform.SetParent(root.transform, false);
            player.transform.position = Vector3.zero;
            player.gameObject.SetActive(true);
            camera.transform.position = new Vector3(0, 0, -10);
            var follow = camera.GetComponent<PlayerCameraFollow2D>();
            if (follow == null) follow = camera.gameObject.AddComponent<PlayerCameraFollow2D>();
            var cameraSettings = new SerializedObject(follow);
            cameraSettings.FindProperty("constrainToMap").boolValue = false;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();

            var config = root.AddComponent<ReusableLevelConfiguration>();
            config.player = player; config.playerSpawn = spawn.transform;
            config.cameraFollow = follow; config.mapBackground = backgroundRenderer;
            config.ground = ground; config.stairs = stairs; config.bridges = bridges;
            config.terrain = grid.GetComponent<CityTerrain>();
            config.minimap = root.GetComponentInChildren<SchematicMinimap>(true);
            config.ApplyConfiguration();
            var eventSystems = root.GetComponentsInChildren<EventSystem>(true);
            for (int i = 0; i < eventSystems.Length; i++)
            {
                eventSystems[i].enabled = i == 0;
                foreach (var module in eventSystems[i].GetComponents<BaseInputModule>()) module.enabled = i == 0;
            }
            if (obstacleLayer >= 0)
            {
                var motor = player.GetComponent<GridMovement>();
                if (motor != null) motor.Configure(ground, motor.Obstacles | (1 << obstacleLayer), motor.GridSize);
                player.obstacleMask |= 1 << obstacleLayer;
            }
            // A prefab may only retain internal scene links and persistent asset references.
            int internalReferences = Validate(root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Output, out bool saved);
            if (!saved || prefab == null) throw new IOException("Could not save reusable prefab.");
            VerifyContents();
            var report = "SUCCESS\n" + Output + "\nSource: " + Source +
                "\nSource scene left unchanged; snapshot includes its unsaved edits when open." +
                "\nValidated internal links: " + internalReferences +
                "\nIncludes Player, Main Camera, blank Terrain, UI, intro dialogues, quiz systems, EventSystem and lighting.";
            File.WriteAllText(Result, report);
            Debug.Log(report);
        }
        catch (Exception error) { File.WriteAllText(Result, "FAILED\n" + error); Debug.LogException(error); }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (File.Exists(snapshot)) AssetDatabase.DeleteAsset(snapshot);
            running = false;
            AssetDatabase.Refresh();
        }
    }

    private static Tilemap Layer(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Tilemap>();
    }
    [MenuItem("Tools/Levels/Verify Reusable Level Template")]
    public static void VerifySaved()
    {
        try
        {
            int links = VerifyContents();
            File.WriteAllText(Result, "VERIFIED\n" + Output + "\nInternal references: " + links +
                "\nPASS: blank terrain; Player/Camera/spawn references; all UI; no Map 1 enemies, scenery, portal or mission triggers.");
        }
        catch (Exception error) { File.WriteAllText(Result, "FAILED\n" + error); Debug.LogException(error); }
    }
    private static int VerifyContents()
    {
        var root = PrefabUtility.LoadPrefabContents(Output);
        try
        {
            int links = Validate(root);
            var config = root.GetComponent<ReusableLevelConfiguration>();
            if (config == null || config.player == null || config.playerSpawn == null || config.cameraFollow == null ||
                config.mapBackground == null || config.terrain == null || config.ground == null || config.stairs == null || config.bridges == null || config.minimap == null)
                throw new InvalidOperationException("Missing map configuration reference after saving the prefab.");
            if (config.mapBackground.sprite != null || root.GetComponentsInChildren<Tilemap>(true).Any(layer => layer.GetUsedTilesCount() != 0))
                throw new InvalidOperationException("Template terrain must be empty.");
            if (root.GetComponentsInChildren<EnemyBrain>(true).Length > 0 || root.GetComponentsInChildren<CityExitPortal>(true).Length > 0 ||
                root.GetComponentsInChildren<TowerDialogueTrigger>(true).Length > 0 || root.GetComponentsInChildren<ResearchDocument>(true).Length > 0)
                throw new InvalidOperationException("Map 1 mission objects remain in template.");
            if (root.GetComponentInChildren<QuizPanel>(true) == null || root.GetComponentInChildren<FlaskEnglishQuizPanel>(true) == null)
                throw new InvalidOperationException("Missing quiz UI after saving the prefab.");
            foreach (var story in root.GetComponentsInChildren<StoryQuizPanel>(true))
            {
                var settings = new SerializedObject(story);
                foreach (string field in new[] { "englishText", "vietnameseText", "nameText", "promptText" })
                    if (settings.FindProperty(field).objectReferenceValue == null)
                        throw new InvalidOperationException("Missing story UI field after saving: " + field);
            }
            return links;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static int Validate(GameObject root)
    {
        if (root.GetComponentInChildren<PlayerVitalsHUD>(true) == null || root.GetComponentInChildren<SchematicMinimap>(true) == null ||
            root.GetComponentInChildren<StoryClueJournal>(true) == null || root.GetComponentInChildren<StoryQuizPanel>(true) == null ||
            root.GetComponentInChildren<NPCIntroDialogue>(true) == null || root.GetComponentInChildren<QuizController>(true) == null ||
            root.GetComponentInChildren<EventSystem>(true) == null)
            throw new InvalidOperationException("Template is missing a required UI/gameplay system.");
        int links = 0;
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) throw new InvalidOperationException("Missing script in template.");
            if (!(component is MonoBehaviour) && !(component is Canvas)) continue;
            var settings = new SerializedObject(component);
            var property = settings.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = property.objectReferenceValue;
                if (value == null || EditorUtility.IsPersistent(value)) continue;
                var target = value is GameObject go ? go.transform : value is Component c ? c.transform : null;
                if (target != null && target != root.transform && !target.IsChildOf(root.transform))
                    throw new InvalidOperationException("External scene reference: " + component.name + "/" + property.propertyPath);
                links++;
            }
        }
        return links;
    }
}

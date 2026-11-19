using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyMovementValidation
{
    private const string Request = "Library/EnemyMovementValidation.request";
    static EnemyMovementValidation() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }
    [MenuItem("Tools/Enemies/Validate Movement and Map Navigation")]
    public static void Run()
    {
        var report = new StringBuilder();
        try
        {
            VerifyNavigation();
            report.AppendLine("PASS: semantic ground/bridge/hole checks; own collider and triggers ignored; solid obstacle blocked.");
            var scene = SceneManager.GetActiveScene();
            report.AppendLine("Scene: " + scene.path + "; mode=" + GameplayState.Current + "; timeScale=" + Time.timeScale);
            var awake = typeof(GridMovement).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var brain in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyBrain>(true)))
            {
                var motor = brain.GetComponent<GridMovement>();
                if (motor == null) { report.AppendLine(brain.name + ": MISSING MOTOR"); continue; }
                awake.Invoke(motor, null);
                int free = 0;
                string neighbors = "";
                foreach (var direction in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right })
                {
                    var point = brain.transform.position + direction * motor.GridSize;
                    bool ground = motor.HasGround(point);
                    var obstacle = motor.ObstacleAt(point);
                    if (ground && obstacle == null) free++;
                    neighbors += " [" + direction + ":ground=" + ground + ",obstacle=" + (obstacle != null ? obstacle.name : "none") + "]";
                }
                report.AppendLine(brain.name + ": enabled=" + brain.isActiveAndEnabled + ", immobilized=" + brain.IsImmobilized + ", physics=" + motor.UsesPhysicsMovement + ", groundHere=" + motor.HasGround(brain.transform.position) + ", freeNeighbors=" + free + neighbors);
            }
            File.WriteAllText("Library/EnemyMovementValidation.result", "SUCCESS\n" + report);
            Debug.Log("Enemy navigation validation complete. See Library/EnemyMovementValidation.result");
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/EnemyMovementValidation.result", "FAILED\n" + report + error);
            Debug.LogException(error);
        }
    }

    private static void VerifyNavigation()
    {
        var active = SceneManager.GetActiveScene();
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject gridRoot = null, cityRoot = null, actor = null, blocker = null;
        var tile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            gridRoot = new GameObject("Navigation validation grid", typeof(Grid)) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(gridRoot, preview);
            gridRoot.transform.position = new Vector3(50000, 50000);
            Tilemap Layer(string name)
            {
                var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
                go.transform.SetParent(gridRoot.transform, false);
                return go.GetComponent<Tilemap>();
            }
            var ground = Layer("Terrain_Ground"); var bridge = Layer("Terrain_Bridges");
            ground.SetTile(Vector3Int.zero, tile); ground.SetTile(Vector3Int.right, tile);
            bridge.SetTile(Vector3Int.right * 2, tile);
            cityRoot = new GameObject("Navigation validation terrain") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(cityRoot, preview);
            var city = cityRoot.AddComponent<CityTerrain>();
            city.surfaceLayers = new[] { ground, bridge };
            actor = new GameObject("Navigation validation enemy") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(actor, preview);
            var motor = actor.AddComponent<GridMovement>();
            typeof(GridMovement).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(motor, null);
            var start = ground.GetCellCenterWorld(Vector3Int.zero);
            if (!motor.HasGround(start) || !motor.HasGround(bridge.GetCellCenterWorld(Vector3Int.right * 2)) || motor.HasGround(ground.GetCellCenterWorld(Vector3Int.right * 3)))
                throw new InvalidOperationException("Semantic terrain admission failed without legacy Ground references.");
            ground.SetTile(Vector3Int.right, null);
            if (motor.HasGround(ground.GetCellCenterWorld(Vector3Int.right))) throw new InvalidOperationException("A ground hole was admitted.");
            SceneManager.MoveGameObjectToScene(actor, active);
            actor.transform.position = start;
            actor.layer = 6;
            actor.AddComponent<BoxCollider2D>();
            motor.Configure(null, 1 << 6, 1);
            Physics2D.SyncTransforms();
            if (motor.ObstacleAt(start) != null) throw new InvalidOperationException("Enemy blocks itself.");
            blocker = new GameObject("Navigation validation blocker") { hideFlags = HideFlags.HideAndDontSave, layer = 6 };
            blocker.transform.position = start + Vector3.right * 2;
            var collider = blocker.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            Physics2D.SyncTransforms();
            if (motor.ObstacleAt(blocker.transform.position) != null) throw new InvalidOperationException("A trigger blocks navigation.");
            collider.isTrigger = false;
            Physics2D.SyncTransforms();
            if (motor.ObstacleAt(blocker.transform.position) != collider) throw new InvalidOperationException("A solid wall was ignored.");
        }
        finally
        {
            if (blocker != null) Object.DestroyImmediate(blocker);
            if (actor != null) Object.DestroyImmediate(actor);
            if (cityRoot != null) Object.DestroyImmediate(cityRoot);
            if (gridRoot != null) Object.DestroyImmediate(gridRoot);
            Object.DestroyImmediate(tile);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}

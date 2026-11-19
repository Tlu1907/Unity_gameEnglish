using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayerCameraSceneRepair
{
    private const string Request = "Library/PlayerCameraSceneRepair.request";
    static PlayerCameraSceneRepair() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }
    [MenuItem("Tools/Player/Repair Camera Follow and Map Bounds")]
    public static void Run()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerMovement>(true)).FirstOrDefault(item => item.gameObject.activeInHierarchy);
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).FirstOrDefault(item => item.CompareTag("MainCamera"));
            if (player == null || camera == null) throw new InvalidOperationException("Active scene needs Player and Main Camera.");
            var follow = camera.GetComponent<PlayerCameraFollow2D>();
            if (follow == null) follow = Undo.AddComponent<PlayerCameraFollow2D>(camera.gameObject);
            string before = "Previous target=" + (follow.Target != null ? follow.Target.name : "NULL") + "; bounds=" + follow.MapBounds;
            Undo.RecordObject(follow, "Repair Player camera");
            follow.enabled = true;
            follow.SetTarget(player.transform);
            if (!follow.DetectMapBounds()) throw new InvalidOperationException("No painted Terrain_Ground or MapBackground found for camera bounds.");
            var settings = new SerializedObject(follow);
            settings.FindProperty("smoothTime").floatValue = .1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Undo.RecordObject(camera, "Repair orthographic camera");
            camera.orthographic = true;
            Undo.RecordObject(camera.transform, "Focus Player camera");
            follow.Focus();
            if (PrefabUtility.IsPartOfPrefabInstance(follow)) PrefabUtility.RecordPrefabInstancePropertyModifications(follow);
            if (PrefabUtility.IsPartOfPrefabInstance(camera)) PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            if (PrefabUtility.IsPartOfPrefabInstance(camera.transform)) PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
            string verification = Verify(follow.MapBounds);
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            File.WriteAllText("Library/PlayerCameraSceneRepair.result", "SUCCESS\n" + scene.path + "\n" + before + "\nTarget=" + player.name + "; bounds=" + follow.MapBounds + "\n" + verification);
            Debug.Log("Camera follow and bounds repaired. " + verification);
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/PlayerCameraSceneRepair.result", "FAILED\n" + error);
            Debug.LogException(error);
        }
    }

    private static string Verify(Bounds bounds)
    {
        var go = new GameObject("Temporary camera verification") { hideFlags = HideFlags.HideAndDontSave };
        var target = new GameObject("Temporary follow target") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            target.AddComponent<PlayerMovement>();
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5;
            var follow = go.AddComponent<PlayerCameraFollow2D>();
            follow.SetTarget(target.transform);
            follow.SetMapBounds(bounds);
            var settings = new SerializedObject(follow);
            settings.FindProperty("smoothTime").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var lateUpdate = typeof(PlayerCameraFollow2D).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            int cases = 0;
            foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
            {
                camera.aspect = aspect;
                foreach (Vector3 point in new[] { bounds.center, bounds.min - Vector3.one * 100, bounds.max + Vector3.one * 100,
                    new Vector3(bounds.min.x - 100, bounds.max.y + 100), new Vector3(bounds.max.x + 100, bounds.min.y - 100) })
                {
                    target.transform.position = point;
                    lateUpdate.Invoke(follow, null);
                    var position = go.transform.position;
                    float halfHeight = camera.orthographicSize, halfWidth = halfHeight * camera.aspect;
                    const float tolerance = .001f;
                    if (position.x - halfWidth < bounds.min.x - tolerance || position.x + halfWidth > bounds.max.x + tolerance ||
                        position.y - halfHeight < bounds.min.y - tolerance || position.y + halfHeight > bounds.max.y + tolerance)
                        throw new InvalidOperationException("Camera extends outside map bounds at aspect " + aspect);
                    if (Vector2.Distance(position, follow.ClampToMap(point)) > tolerance)
                        throw new InvalidOperationException("Camera did not follow its moving target.");
                    cases++;
                }
            }
            return "PASS: follows a moving target and stays inside map at all 4 corners, in portrait/landscape/ultrawide (" + cases + " cases).";
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(target); }
    }
}

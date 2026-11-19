using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class MapRenderOrderRepair
{
    private const string Request = "Library/MapRenderOrderRepair.v2.request";
    static MapRenderOrderRepair() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }

    [MenuItem("Tools/Map/Fix Background Behind All Objects")]
    public static void Run()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var report = new StringBuilder("Scene: " + scene.path + "\n");
            report.AppendLine("Before: " + string.Join(", ", SortingLayer.layers.Select(item => item.name + ":" + item.value)));
            // Update the live Editor settings, rather than only the YAML on disk.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("m_SortingLayers");
            int index = -1;
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "MapBackground") index = i;
            if (index < 0)
            {
                index = layers.arraySize;
                layers.InsertArrayElementAtIndex(index);
                var entry = layers.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("name").stringValue = "MapBackground";
                entry.FindPropertyRelative("uniqueID").intValue = 1500100106;
                entry.FindPropertyRelative("locked").boolValue = false;
            }
            layers.MoveArrayElement(index, 0);
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(settings.targetObject);
            int backgroundID = SortingLayer.NameToID("MapBackground");
            if (backgroundID == 0) throw new InvalidOperationException("Background layer was not registered.");
            var renderers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();
            foreach (var sprite in renderers.OfType<SpriteRenderer>())
                if (sprite.name == "lab" || sprite.name == "Tower" || sprite.bounds.size.x > 10 || sprite.bounds.size.y > 10)
                    report.AppendLine("Sprite audit: " + PathOf(sprite.transform) + " enabled=" + sprite.enabled + " active=" + sprite.gameObject.activeInHierarchy + " sprite=" + AssetDatabase.GetAssetPath(sprite.sprite) + " bounds=" + sprite.bounds + " layer=" + sprite.sortingLayerName + " order=" + sprite.sortingOrder);
            var grounds = renderers.Where(renderer => renderer.name == "MapBackground" || renderer.name == "Ground" ||
                (renderer is TilemapRenderer && renderer.name.StartsWith("Terrain_"))).ToArray();
            if (grounds.Length == 0) throw new InvalidOperationException("No ground renderers found.");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Fix map background render order");
            foreach (var renderer in grounds)
            {
                Undo.RecordObject(renderer, "Fix map background render order");
                renderer.sortingLayerID = backgroundID;
                renderer.sortingOrder = renderer.name == "MapBackground" ? -30000 : renderer.sortingOrder;
                if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
                report.AppendLine("Ground: " + PathOf(renderer.transform) + " layer=" + renderer.sortingLayerName + " order=" + renderer.sortingOrder);
            }
            int floor = SortingLayer.GetLayerValueFromID(backgroundID);
            foreach (var renderer in renderers.Except(grounds))
            {
                if (renderer.GetComponentInParent<Canvas>() != null) continue;
                var group = renderer.GetComponentInParent<SortingGroup>();
                if (group != null)
                {
                    while (group.transform.parent != null)
                    {
                        var parent = group.transform.parent.GetComponentInParent<SortingGroup>();
                        if (parent == null) break;
                        group = parent;
                    }
                    if (SortingLayer.GetLayerValueFromID(group.sortingLayerID) <= floor)
                    {
                        Undo.RecordObject(group, "Fix world group above map");
                        group.sortingLayerName = "WorldDepth";
                        if (PrefabUtility.IsPartOfPrefabInstance(group)) PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                        EditorUtility.SetDirty(group);
                    }
                }
                else if (SortingLayer.GetLayerValueFromID(renderer.sortingLayerID) <= floor)
                {
                    Undo.RecordObject(renderer, "Fix object above map");
                    renderer.sortingLayerName = "Default";
                    if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    EditorUtility.SetDirty(renderer);
                }
                if (renderer.name == "lab" || renderer.name == "Tower")
                    report.AppendLine("Object: " + PathOf(renderer.transform) + " layer=" + renderer.sortingLayerName + " value=" + SortingLayer.GetLayerValueFromID(renderer.sortingLayerID));
            }
            report.AppendLine("After: " + string.Join(", ", SortingLayer.layers.Select(item => item.name + ":" + item.value)));
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            CapturePreview();
            report.AppendLine("SUCCESS: scene updated in memory; save the scene to retain overrides.");
            File.WriteAllText("Library/MapRenderOrderRepair.result", report.ToString());
            Debug.Log("Map background render order repaired in " + scene.name);
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/MapRenderOrderRepair.result", "FAILED\n" + error);
            Debug.LogException(error);
        }
    }
    private static string PathOf(Transform node) => node.parent == null ? node.name : PathOf(node.parent) + "/" + node.name;

    private static void CapturePreview()
    {
        var cameraObject = new GameObject("Map render verification") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 25;
        camera.transform.position = new Vector3(0, 0, -50);
        camera.enabled = false;
        var texture = new RenderTexture(1024, 1024, 24);
        var previous = RenderTexture.active;
        var pixels = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        try
        {
            texture.Create();
            var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = texture };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Library/MapRenderOrderPreview.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}

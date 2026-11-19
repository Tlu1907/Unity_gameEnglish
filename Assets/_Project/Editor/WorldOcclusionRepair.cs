using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class WorldOcclusionRepair
{
    private const string Request = "Library/WorldOcclusionRepair.request";
    static WorldOcclusionRepair() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }

    [MenuItem("Tools/Map/Fix Player Occlusion Behind Buildings")]
    public static void Run()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SortingLayer.NameToID("WorldDepth") == 0) throw new InvalidOperationException("WorldDepth layer is missing.");
            var sprites = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
            var sorts = new HashSet<WorldDepthSort>(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldDepthSort>(true)));
            var report = new StringBuilder("Scene: " + scene.path + "\n");
            int added = 0;
            foreach (var sprite in sprites)
            {
                if (sprite.sprite == null || sprite.GetComponentInParent<Canvas>() != null ||
                    sprite.GetComponentInParent<WorldDepthSort>() != null || sprite.name.Contains("Shadow") ||
                    sprite.name == "MapBackground" || sprite.name == "Ground" || sprite.name.StartsWith("Terrain_")) continue;
                var ownerGroup = sprite.GetComponentInParent<SortingGroup>();
                var solid = sprite.GetComponentsInParent<Collider2D>(true).FirstOrDefault(collider => !collider.isTrigger);
                // Leave flat map decorations alone; only solid props and authored object groups need occlusion.
                if (ownerGroup == null && solid == null) continue;
                var owner = ownerGroup != null ? ownerGroup.gameObject : solid.gameObject;
                if (owner.GetComponentInParent<Canvas>() != null) continue;
                var sort = owner.GetComponent<WorldDepthSort>();
                if (sort == null) { sort = Undo.AddComponent<WorldDepthSort>(owner); added++; }
                Undo.RecordObject(sort, "Set object ground depth");
                var group = owner.GetComponent<SortingGroup>();
                if (group == null) group = Undo.AddComponent<SortingGroup>(owner);
                sort.group = group;
                sort.target = sprite;
                sort.groundAnchor = owner.transform;
                sort.feetCollider = solid;
                if (solid == null) sort.footOffset = owner.transform.InverseTransformPoint(new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, sprite.bounds.center.z)).y;
                sorts.Add(sort);
                report.AppendLine("Added ground-depth sorting: " + PathOf(owner.transform));
            }
            foreach (var sort in sorts)
            {
                Undo.RecordObject(sort, "Repair world depth sorting");
                sort.enabled = true;
                if (sort.group == null) sort.group = sort.GetComponent<SortingGroup>();
                if (sort.group == null && sort.target == null) sort.target = sort.GetComponentInChildren<SpriteRenderer>(true);
                if (sort.group == null && sort.target == null) continue;
                if (sort.group != null) Undo.RecordObject(sort.group, "Apply object depth order");
                else Undo.RecordObject(sort.target, "Apply sprite depth order");
                sort.Apply();
                Record(sort);
                if (sort.group != null) Record(sort.group); else Record(sort.target);
                var group = sort.group;
                report.AppendLine(PathOf(sort.transform) + ": feetY=" + sort.GroundY + " layer=" +
                    (group != null ? group.sortingLayerName : sort.target.sortingLayerName) + " order=" +
                    (group != null ? group.sortingOrder : sort.target.sortingOrder));
            }
            Verify(scene, sorts);
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            report.AppendLine("SUCCESS: " + sorts.Count + " world objects sorted; " + added + " missing components added. Verified front/behind ordering and sprite/group consistency. Colliders were not changed. Save the scene to retain overrides.");
            File.WriteAllText("Library/WorldOcclusionRepair.result", report.ToString());
            Debug.Log("World occlusion repaired for " + sorts.Count + " objects.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/WorldOcclusionRepair.result", "FAILED\n" + error);
            Debug.LogException(error);
        }
    }

    private static void Verify(Scene scene, IEnumerable<WorldDepthSort> sorts)
    {
        foreach (var sort in sorts)
        {
            if (sort.group == null && sort.target == null) continue;
            int layer = sort.group != null ? sort.group.sortingLayerID : sort.target.sortingLayerID;
            if (layer != SortingLayer.NameToID("WorldDepth")) throw new InvalidOperationException("Wrong layer on " + sort.name);
        }
        var go = new GameObject("Temporary depth verification") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var sprite = go.AddComponent<SpriteRenderer>();
            var group = go.AddComponent<SortingGroup>();
            var sort = go.AddComponent<WorldDepthSort>();
            sort.groundAnchor = go.transform;
            sort.group = group;
            go.transform.position = Vector3.up;
            sort.Apply();
            int behind = group.sortingOrder;
            go.transform.position = Vector3.down;
            sort.Apply();
            int front = group.sortingOrder;
            if (behind >= 0 || front <= 0) throw new InvalidOperationException("Front/behind order is reversed.");
            sort.group = null;
            sort.target = sprite;
            sort.Apply();
            if (sprite.sortingOrder != front || sprite.sortingLayerID != group.sortingLayerID)
                throw new InvalidOperationException("Sprite and SortingGroup use different depth scales.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    private static void Record(UnityEngine.Object item)
    {
        EditorUtility.SetDirty(item);
        if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
    }
    private static string PathOf(Transform node) => node.parent == null ? node.name : PathOf(node.parent) + "/" + node.name;
}

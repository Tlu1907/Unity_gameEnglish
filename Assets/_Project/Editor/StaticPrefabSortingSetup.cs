using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>Authors reusable depth settings into prefab assets, including exported layout prefabs.</summary>
[InitializeOnLoad]
public static class StaticPrefabSortingSetup
{
    private const string Request = "Library/StaticPrefabSortingSetup.v3.request";
    private const string Result = "Library/StaticPrefabSortingSetup.result";
    private static bool running;
    static StaticPrefabSortingSetup() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Run();
    }

    [MenuItem("Tools/Map/Apply Depth Sorting to All Project Static Prefabs")]
    public static void Run()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        var report = new StringBuilder();
        string backup = "Library/PrefabSortingBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        int scanned = 0, modified = 0, objectCount = 0, validated = 0, failures = 0;
        try
        {
            if (SortingLayer.NameToID("WorldDepth") == 0) throw new InvalidOperationException("WorldDepth sorting layer is missing.");
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            report.AppendLine("Scope: Assets/_Project, including Prefab1 exports. Original GUIDs and colliders are preserved.");
            report.AppendLine("Backup: " + backup);
            foreach (string path in paths)
            {
                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    var candidates = Candidates(contents);
                    scanned++;
                    if (candidates.Count == 0) continue;
                    string fileBackup = backup + "/" + path;
                    Directory.CreateDirectory(Path.GetDirectoryName(fileBackup));
                    File.Copy(path, fileBackup, true);
                    if (File.Exists(path + ".meta")) File.Copy(path + ".meta", fileBackup + ".meta", true);
                    foreach (var pair in candidates)
                    {
                        var owner = pair.Key;
                        var sprite = pair.Value;
                        var sort = owner.GetComponent<WorldDepthSort>();
                        bool existing = sort != null;
                        if (sort == null) sort = owner.AddComponent<WorldDepthSort>();
                        var group = owner.GetComponent<SortingGroup>();
                        if (group == null) group = owner.AddComponent<SortingGroup>();
                        sort.enabled = true;
                        sort.group = group;
                        sort.target = sprite;
                        if (!existing || sort.groundAnchor == null) sort.groundAnchor = owner.transform;
                        if (sort.feetCollider == null)
                        {
                            sort.feetCollider = owner.GetComponents<Collider2D>().FirstOrDefault(item => !item.isTrigger);
                            if (sort.feetCollider == null)
                                sort.feetCollider = owner.GetComponentsInChildren<Collider2D>(true)
                                    .FirstOrDefault(item => !item.isTrigger && !(item is TilemapCollider2D));
                        }
                        if (!existing && sort.feetCollider == null)
                        {
                            // Set the foot in local space once, so moving/scaling the prefab keeps it attached.
                            float bottom = float.PositiveInfinity;
                            foreach (var renderer in owner.GetComponentsInChildren<SpriteRenderer>(true))
                            {
                                if (renderer.sprite == null || IsShadow(renderer)) continue;
                                Bounds bounds = renderer.sprite.bounds;
                                for (int x = 0; x < 2; x++)
                                for (int y = 0; y < 2; y++)
                                {
                                    Vector3 corner = renderer.transform.TransformPoint(new Vector3(x == 0 ? bounds.min.x : bounds.max.x, y == 0 ? bounds.min.y : bounds.max.y));
                                    bottom = Mathf.Min(bottom, owner.transform.InverseTransformPoint(corner).y);
                                }
                            }
                            sort.footOffset = float.IsInfinity(bottom) ? 0 : bottom;
                        }
                        sort.Apply();
                        if (PrefabUtility.IsPartOfPrefabInstance(sort)) PrefabUtility.RecordPrefabInstancePropertyModifications(sort);
                        if (PrefabUtility.IsPartOfPrefabInstance(group)) PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                        Validate(sort);
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, path, out bool saved);
                    if (!saved) throw new IOException("Could not save prefab.");
                    modified++;
                    objectCount += candidates.Count;
                    report.AppendLine("UPDATED " + path + " (" + candidates.Count + " objects)");
                    PrefabUtility.UnloadPrefabContents(contents);
                    contents = null;
                    // Re-open the saved asset to catch references lost during prefab serialization.
                    contents = PrefabUtility.LoadPrefabContents(path);
                    foreach (var pair in Candidates(contents))
                    {
                        var sort = pair.Key.GetComponent<WorldDepthSort>();
                        if (sort == null) throw new InvalidOperationException("Depth component missing after save: " + pair.Key.name);
                        Validate(sort);
                        validated++;
                    }
                }
                catch (Exception error)
                {
                    failures++;
                    report.AppendLine("FAILED " + path + ": " + error.Message);
                }
                finally
                {
                    if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                    if (scanned % 20 == 0) File.WriteAllText("Library/StaticPrefabSortingSetup.progress", scanned + "/" + paths.Length + " scanned; " + modified + " updated; " + failures + " failed");
                }
            }
            report.AppendLine("RESULT: " + scanned + " prefabs scanned, " + modified + " updated, " + objectCount + " static objects configured, " + validated + " saved objects validated, " + failures + " failures.");
            File.WriteAllText(Result, (failures == 0 ? "SUCCESS\n" : "PARTIAL\n") + report);
            File.WriteAllText("Library/StaticPrefabSortingSetup.progress", "Finished: " + scanned + " scanned, " + modified + " updated, " + failures + " failures");
            AssetDatabase.Refresh();
            SceneView.RepaintAll();
            Debug.Log("Static prefab sorting: " + modified + " prefabs updated; " + failures + " failures. Report: " + Result);
        }
        catch (Exception error)
        {
            File.WriteAllText(Result, "FAILED\n" + report + "\n" + error);
            Debug.LogException(error);
        }
        finally { running = false; }
    }

    private static Dictionary<GameObject, SpriteRenderer> Candidates(GameObject contents)
    {
        var result = new Dictionary<GameObject, SpriteRenderer>();
        foreach (var sprite in contents.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sprite.sprite == null || Excluded(sprite.transform)) continue;
            var prior = sprite.GetComponentInParent<WorldDepthSort>();
            GameObject owner;
            if (prior != null) owner = prior.gameObject;
            else
            {
                var group = sprite.GetComponentInParent<SortingGroup>();
                var solid = sprite.GetComponentsInParent<Collider2D>(true)
                    .FirstOrDefault(item => !item.isTrigger && !(item is TilemapCollider2D));
                if (group != null) owner = group.gameObject;
                else if (solid != null) owner = solid.gameObject;
                else if (sprite.name == "Visual" && sprite.transform.parent != null &&
                    sprite.transform.parent.GetComponentsInChildren<SpriteRenderer>(true).Length <= 8)
                    owner = sprite.transform.parent.gameObject;
                else owner = sprite.gameObject;
            }
            if (Excluded(owner.transform)) continue;
            if (!result.ContainsKey(owner)) result.Add(owner, sprite);
        }
        // Children of one whole-object group must not receive independent depth components.
        foreach (var owner in result.Keys.ToArray())
            for (Transform parent = owner.transform.parent; parent != null; parent = parent.parent)
                if (result.ContainsKey(parent.gameObject)) { result.Remove(owner); break; }
        return result;
    }

    private static bool Excluded(Transform node)
    {
        if (node.GetComponentInParent<Canvas>() != null || node.GetComponentInParent<PlayerMovement>() != null ||
            node.GetComponentInParent<EnemyBrain>() != null || node.GetComponentInParent<StoryNPC>() != null ||
            node.GetComponentInParent<NPCDialogueTrigger>() != null || node.GetComponentInParent<WeaponPickup>() != null ||
            node.GetComponentInParent<HealthPickup>() != null || node.GetComponentInParent<Tilemap>() != null) return true;
        for (Transform current = node; current != null; current = current.parent)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("shadow") || name.Contains("mapbackground") || name.Contains("npc") || name.Contains("scientist") || name.StartsWith("terrain_") ||
                name == "ground" || name.Contains("palette") || name == "stationexitportal") return true;
        }
        return false;
    }
    private static bool IsShadow(SpriteRenderer renderer) => renderer.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0;

    private static void Validate(WorldDepthSort sort)
    {
        if (!sort.enabled || sort.group == null || sort.groundAnchor == null)
            throw new InvalidOperationException("Incomplete depth settings on " + sort.name);
        sort.Apply();
        if (sort.group.sortingLayerID != SortingLayer.NameToID("WorldDepth"))
            throw new InvalidOperationException("Wrong sorting layer on " + sort.name);
        Vector3 original = sort.transform.position;
        try
        {
            sort.transform.position = new Vector3(original.x, original.y - sort.GroundY, original.z);
            Physics2D.SyncTransforms();
            sort.Apply();
            int center = sort.group.sortingOrder;
            sort.transform.position += Vector3.up;
            Physics2D.SyncTransforms();
            sort.Apply();
            if (sort.group.sortingOrder >= center) throw new InvalidOperationException("Object does not sort behind when moved up: " + sort.name);
            sort.transform.position -= Vector3.up * 2;
            Physics2D.SyncTransforms();
            sort.Apply();
            if (sort.group.sortingOrder <= center) throw new InvalidOperationException("Object does not sort in front when moved down: " + sort.name);
        }
        finally { sort.transform.position = original; Physics2D.SyncTransforms(); sort.Apply(); }
    }
}

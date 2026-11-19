using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exports saved scenes only. Never saves or replaces an author's open scenes.</summary>
[InitializeOnLoad]
public static class Prefab1SceneBuilder
{
    private const string Output = "Assets/_Project/Prefab1";
    private const string Map = "Assets/_Project/Scenes/Sence_1/Scene1.unity";
    private const string Story = "Assets/_Project/Scenes/Sence_1/Sence1_final.unity";
    private const string Request = "Library/Prefab1SceneBuilder.request";
    private static bool running;

    static Prefab1SceneBuilder() { EditorApplication.update += CheckRequest; }

    private static void CheckRequest()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Run();
    }

    [MenuItem("Tools/Prefab1/Export Both Scenes and Build Merged Scene")]
    public static void Run()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        Scene previous = SceneManager.GetActiveScene();
        var opened = new List<Scene>();
        string folder = Output + "/Export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var report = new List<string>();
        try
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            report.Add("Source scenes were copied from disk; unsaved editor changes are not included.");
            report.Add("Original scenes and prefabs were not modified.");
            Scene map = CopyScene(Map, folder + "/Scene1_Layout.unity", opened);
            Scene story = CopyScene(Story, folder + "/Sence1_final_Layout.unity", opened);
            Export(map, folder + "/Scene1", report);
            Export(story, folder + "/Sence1_final", report);
            EditorSceneManager.SaveScene(map);
            EditorSceneManager.SaveScene(story);

            // Use the saved story as the base so all authored mission links stay together.
            Scene merged = CopyScene(Story, folder + "/Scene1_Merged.unity", opened);
            Scene mapInput = CopyScene(Map, folder + "/Scene1_MapInput.unity", opened);
            Merge(mapInput, merged, report);
            Export(merged, folder + "/Merged", report);
            EditorSceneManager.SaveScene(merged);
            report.Add("Merged scene: " + merged.path);
            report.Add("Build Settings were not changed. Play-test the merged scene before including it in a build.");
            report.Add("Standalone prefabs cannot store references to other scene objects. The exported layout scenes retain those links as instance overrides.");
            report.Add("Mission behavior still depends on the existing EnemyBrain implementation; exporting does not repair missing encounter logic.");
            File.WriteAllLines(folder + "/EXPORT_REPORT.txt", report);
            File.WriteAllText(Output + "/LATEST_EXPORT.txt", folder);
            File.WriteAllText("Library/Prefab1SceneBuilder.result", "SUCCESS\n" + folder);
            AssetDatabase.Refresh();
            Debug.Log("Prefab1 export complete: " + folder + "/Scene1_Merged.unity");
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(folder);
            report.Add("FAILED: " + error);
            File.WriteAllLines(folder + "/EXPORT_REPORT.txt", report);
            File.WriteAllText("Library/Prefab1SceneBuilder.result", "FAILED\n" + error);
            Debug.LogException(error);
        }
        finally
        {
            foreach (Scene scene in opened.AsEnumerable().Reverse())
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            running = false;
        }
    }

    private static Scene CopyScene(string source, string destination, List<Scene> opened)
    {
        if (!AssetDatabase.CopyAsset(source, destination)) throw new IOException("Cannot copy " + source);
        Scene scene = EditorSceneManager.OpenScene(destination, OpenSceneMode.Additive);
        opened.Add(scene);
        return scene;
    }

    private sealed class Link
    {
        public Component owner;
        public string property;
        public Object target;
    }

    private static List<Link> Links(Scene scene)
    {
        var result = new List<Link>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.propertyPath == "m_GameObject" || property.propertyPath.StartsWith("m_Corresponding") ||
                    property.propertyPath.StartsWith("m_Prefab")) continue;
                Object target = property.objectReferenceValue;
                if (target == null || EditorUtility.IsPersistent(target)) continue;
                result.Add(new Link { owner = component, property = property.propertyPath, target = target });
            }
        }
        return result;
    }

    private static void RestoreLinks(IEnumerable<Link> links)
    {
        foreach (Link link in links)
        {
            if (link.owner == null || link.target == null)
                throw new InvalidOperationException("Object disappeared while exporting " + link.property);
            var serialized = new SerializedObject(link.owner);
            var property = serialized.FindProperty(link.property);
            if (property == null) throw new InvalidOperationException("Property disappeared: " + link.property);
            property.objectReferenceValue = link.target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(link.owner))
                PrefabUtility.RecordPrefabInstancePropertyModifications(link.owner);
        }
    }

    private static void Export(Scene scene, string folder, List<string> report)
    {
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var candidates = new HashSet<GameObject>(scene.GetRootGameObjects());
        // Export placed prefab instances separately, retaining their internal Visual/UI hierarchy.
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) candidates.Add(child.gameObject);
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != null && PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject))
                PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        var links = Links(scene);
        int index = 0;
        foreach (GameObject candidate in candidates.OrderByDescending(go => Depth(go.transform)).ThenBy(go => Hierarchy(go.transform)))
        {
            string name = (++index).ToString("D4") + "_" + Safe(candidate.name);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(candidate, path, InteractionMode.AutomatedAction, out bool success);
            if (!success) throw new IOException("Cannot export " + Hierarchy(candidate.transform));
            report.Add(path + " <- " + Hierarchy(candidate.transform));
        }
        RestoreLinks(links);
        // Re-read every recorded property; never publish a layout with links silently stripped.
        foreach (Link link in links)
            if (new SerializedObject(link.owner).FindProperty(link.property).objectReferenceValue != link.target)
                throw new InvalidOperationException("Reference validation failed: " + link.property);
        report.Add(scene.name + ": " + index + " prefabs; validated " + links.Count + " scene references.");
    }

    private static void Merge(Scene map, Scene story, List<string> report)
    {
        var mapRoots = map.GetRootGameObjects().ToDictionary(root => root.name);
        var storyRoots = story.GetRootGameObjects().ToDictionary(root => root.name);
        if (!mapRoots.TryGetValue("Grid", out GameObject newGrid) ||
            !storyRoots.TryGetValue("Grid", out GameObject oldGrid))
            throw new InvalidOperationException("Both scenes must contain a Grid root.");
        var replacements = new Dictionary<Object, Object>();
        MatchHierarchy(oldGrid, newGrid, replacements);
        // Map components may refer to Player, Camera or other roots supplied by the story scene.
        foreach (var pair in mapRoots)
            if (pair.Key != "Grid" && storyRoots.TryGetValue(pair.Key, out GameObject replacement))
                MatchHierarchy(pair.Value, replacement, replacements);
        var storyLinks = Links(story);
        var mapLinks = Links(map);
        var moved = new HashSet<GameObject>();
        foreach (string name in new[] { "Grid", "CityExpansion", "LayeredCityTerrain", "HidingAndTraps" })
        {
            if (!mapRoots.TryGetValue(name, out GameObject root)) continue;
            SceneManager.MoveGameObjectToScene(root, story);
            moved.Add(root);
            report.Add("Map root imported: " + name);
        }
        // A camera authored for the expanded map supplies framing; its target is remapped below.
        if (mapRoots.TryGetValue("Main Camera", out GameObject mapCamera) &&
            storyRoots.TryGetValue("Main Camera", out GameObject storyCamera))
        {
            foreach (Component source in mapCamera.GetComponents<Component>())
            {
                if (source == null || source is Transform || source is AudioListener) continue;
                Component destination = storyCamera.GetComponent(source.GetType());
                if (destination != null) EditorUtility.CopySerialized(source, destination);
            }
            // Capture copied references from Camera too.
            storyLinks = Links(story);
        }
        var links = storyLinks.Concat(mapLinks.Where(link => link.owner != null && moved.Contains(link.owner.transform.root.gameObject))).ToList();
        foreach (Link link in links)
        {
            if (link.owner == null || link.owner.transform.IsChildOf(oldGrid.transform)) continue;
            if (replacements.TryGetValue(link.target, out Object replacement)) link.target = replacement;
            GameObject targetObject = link.target is GameObject go ? go : (link.target as Component)?.gameObject;
            if (targetObject != null && targetObject.scene != story)
                throw new InvalidOperationException("Unresolved map reference: " + link.owner.name + "." + link.property + " -> " + targetObject.name);
            if (targetObject != null && targetObject.transform.IsChildOf(oldGrid.transform))
                throw new InvalidOperationException("No matching object in the new Grid: " + link.owner.name + "." + link.property + " -> " + targetObject.name);
        }
        RestoreLinks(links.Where(link => link.owner != null && !link.owner.transform.IsChildOf(oldGrid.transform)));
        Object.DestroyImmediate(oldGrid);
        report.Add("Story roots retained: " + string.Join(", ", storyRoots.Keys.Where(name => name != "Grid")));
    }

    private static void MatchHierarchy(GameObject source, GameObject target, Dictionary<Object, Object> replacements)
    {
        foreach (Transform node in source.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(node, source.transform);
            Transform match = string.IsNullOrEmpty(path) ? target.transform : target.transform.Find(path);
            if (match == null) continue;
            replacements[node.gameObject] = match.gameObject;
            var used = new Dictionary<Type, int>();
            foreach (Component component in node.GetComponents<Component>())
            {
                if (component == null) continue;
                Type type = component.GetType();
                used.TryGetValue(type, out int index);
                var matches = match.GetComponents(type);
                if (index < matches.Length) replacements[component] = matches[index];
                used[type] = index + 1;
            }
        }
    }

    private static int Depth(Transform node) { int depth = 0; while (node.parent != null) { depth++; node = node.parent; } return depth; }
    private static string Hierarchy(Transform node) => node.parent == null ? node.name : Hierarchy(node.parent) + "/" + node.name;
    private static string Safe(string value)
    {
        foreach (char character in Path.GetInvalidFileNameChars()) value = value.Replace(character, '_');
        return string.IsNullOrWhiteSpace(value) ? "Object" : value.Trim();
    }
}

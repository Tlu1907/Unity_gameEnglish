using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SceneHierarchyOrganizer
{
    static SceneHierarchyOrganizer() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        const string request = "Library/SceneHierarchyOrganizer.v4.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action = File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            var scene = SceneManager.GetActiveScene();
            var lines = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => t.parent == null || t.parent.parent == null)
                .Select(t => (t.parent == null ? "ROOT " : "  ") + t.name + " | " + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) + " | " + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
            File.WriteAllText("Library/SceneHierarchyOrganizer.audit", "Scene: " + scene.path + "\n" + string.Join("\n", lines));
            if (action == "organize") Organize(scene);
        }
        catch (Exception e) { File.WriteAllText("Library/SceneHierarchyOrganizer.result", "FAILED\n" + e); }
    }
    [MenuItem("Tools/Scene/Organize Current Hierarchy")]
    public static void OrganizeCurrent() => Organize(SceneManager.GetActiveScene());

    private static void Organize(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Save the scene first.");
        var originals = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        var matrices = originals.ToDictionary(t => t, t => t.localToWorldMatrix);
        var componentCount = originals.Sum(t => t.GetComponents<Component>().Length);
        var references = new List<KeyValuePair<Component, KeyValuePair<string, Object>>>();
        // TMP may regenerate temporary font materials on reparent; verify persistent gameplay links instead.
        foreach (var component in originals.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(c => c != null && c.GetType().Assembly == typeof(EnemyBrain).Assembly))
        {
            var serialized = new SerializedObject(component); var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_GameObject" &&
                    property.name != "m_CorrespondingSourceObject" && property.name != "m_PrefabInstance" && property.name != "m_PrefabAsset" && property.objectReferenceValue != null)
                    references.Add(new KeyValuePair<Component, KeyValuePair<string, Object>>(component,
                        new KeyValuePair<string, Object>(property.propertyPath, property.objectReferenceValue)));
        }
        string backup = "Library/SceneHierarchy.Before.unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not create scene backup.");
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Organize scene hierarchy");
        var groups = new Dictionary<string, Transform>();
        string[] names = { "Houses", "Trees", "Vegetation", "Props", "Monsters", "NPCs", "QuestLocations", "Terrain", "UI", "Systems" };
        foreach (string name in names)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name && g.GetComponents<Component>().Length == 1);
            if (root == null)
            {
                root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create " + name);
            }
            groups.Add(name, root.transform);
        }
        var report = new StringBuilder();
        Action<Transform> move = null;
        move = t =>
        {
            if (groups.Values.Contains(t) || t.GetComponent<PlayerMovement>() != null || t.GetComponent<Camera>() != null) return;
            // Only dissolve scenery containers; never split characters, buildings or functional systems.
            if (t.name == "CityExpansion" || t.name == "CityScenery")
            {
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
                foreach (var child in t.Cast<Transform>().ToArray()) move(child);
                if (t.childCount == 0) { Undo.SetTransformParent(t, groups["Terrain"], "Keep scenery container"); }
                return;
            }
            string category = Category(t);
            var position = t.position; var rotation = t.rotation;
            Undo.SetTransformParent(t, groups[category], "Group " + t.name);
            // Undo.SetTransformParent preserves world position, rotation and scale.
            if (Vector3.Distance(t.position, position) > .001f || Quaternion.Angle(t.rotation, rotation) > .01f)
                throw new InvalidOperationException("Transform changed: " + t.name);
            if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            report.AppendLine(category + "/" + t.name);
        };
        try
        {
            foreach (var root in scene.GetRootGameObjects().ToArray()) move(root.transform);
            foreach (var pair in matrices)
            {
                if (pair.Key == null) throw new InvalidOperationException("Original object was lost.");
                var current = pair.Key.localToWorldMatrix;
                for (int i = 0; i < 16; i++) if (Mathf.Abs(pair.Value[i] - current[i]) > .005f)
                    throw new InvalidOperationException("World matrix changed: " + pair.Key.name);
            }
            if (originals.Sum(t => t.GetComponents<Component>().Length) != componentCount) throw new InvalidOperationException("Component count changed.");
            foreach (var reference in references)
            {
                var property = new SerializedObject(reference.Key).FindProperty(reference.Value.Key);
                if (property == null || property.objectReferenceValue != reference.Value.Value)
                    throw new InvalidOperationException("Reference changed: " + reference.Key.name + "/" + reference.Value.Key);
            }
            var player = originals.FirstOrDefault(t => t.GetComponent<PlayerMovement>() != null);
            if (player != null && player.parent != null) throw new InvalidOperationException("Player must remain a root object.");
            if (player != null) { Undo.RecordObject(player, "Player first"); player.SetSiblingIndex(0); }
            foreach (var group in groups.Values)
            {
                var children = group.Cast<Transform>().OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase).ToArray();
                for (int i = 0; i < children.Length; i++) children[i].SetSiblingIndex(i);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save organized scene.");
            Undo.CollapseUndoOperations(undo);
            File.WriteAllText("Library/SceneHierarchyOrganizer.result", "SUCCESS\n" + scene.path + "\nPASS: " + originals.Length + " world matrices, " + componentCount + " components, " + references.Count + " references; Player at root.\n" + string.Join("\n", groups.Select(p => p.Key + ": " + p.Value.childCount)) + "\n" + report);
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    private static string Category(Transform t)
    {
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).ToLowerInvariant();
        string name = t.name.ToLowerInvariant();
        if (t.GetComponent<EnemyBrain>() != null || path.Contains("/enemies/")) return "Monsters";
        if (path.Contains("/npc/") || t.GetComponentInChildren<NPCDialogueTrigger>(true) != null || t.GetComponentInChildren<StoryNPC>(true) != null) return "NPCs";
        if (t.GetComponent<TowerDialogueTrigger>() != null || t.GetComponent<LabBuildingInteraction>() != null || name.Contains("spawnpoint")) return "QuestLocations";
        if (t.GetComponent<Canvas>() != null || t.GetComponentInChildren<PlayerVitalsHUD>(true) != null) return "UI";
        if (t.GetComponentInChildren<QuizController>(true) != null) return "Systems";
        if (path.Contains("/houses/") || name.Contains("house")) return "Houses";
        if (path.Contains("/trees/") || name.Contains("tree")) return "Trees";
        if (path.Contains("/vegetation/") || name.Contains("flower") || name.Contains("bush") || name.Contains("planter")) return "Vegetation";
        if (t.GetComponent<Grid>() != null || t.GetComponent<CityTerrain>() != null || name.Contains("terrain")) return "Terrain";
        if (name == "hidingandtraps") return "Systems";
        if (t.GetComponentInChildren<SpriteRenderer>(true) != null) return "Props";
        return "Systems";
    }
}

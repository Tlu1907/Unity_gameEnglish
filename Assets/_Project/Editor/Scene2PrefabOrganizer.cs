using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Scene2PrefabOrganizer
{
    private const string Folder = "Assets/_Project/Gameplay/Items/Map2/Prefab";
    private const string ScenePath = "Assets/_Project/Scenes/Scence_2/Scence2.unity";
    private const string Request = "Library/Scene2PrefabOrganizer.request";
    private const string Result = "Library/Scene2PrefabOrganizer.result";
    static Scene2PrefabOrganizer() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.ReadAllText(Request); File.Delete(Request); } catch (IOException) { return; }
        Run();
    }
    private static string Key(Sprite sprite)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id);
        return guid + ":" + id;
    }
    private static int Index(Sprite sprite)
    {
        string suffix = sprite.name.Substring(sprite.name.LastIndexOf('_') + 1);
        return int.TryParse(suffix, out int value) ? value : 0;
    }
    private static string Name(SpriteRenderer renderer)
    {
        var sprite = renderer.sprite;
        string kind = Scene2PropSetup.Kind(renderer);
        int index = Index(sprite);
        if (kind == "Tree") return index < 4 ? "Tree_Oak_" + (char)('A' + index) : "Tree_Dead_" + (char)('A' + index - 4);
        if (kind == "Log") return new[] { "FallenLog_Horizontal", "FallenLog_Vertical", "FallenLog_DiagonalUp", "FallenLog_DiagonalDown" }[Mathf.Clamp(index - 8, 0, 3)];
        if (kind == "Bush") return new[] { "Shrub_Broadleaf", "Grass_Tall", "Bush_Round", "GroundCover_Cluster" }[Mathf.Clamp(index - 12, 0, 3)];
        if (kind == "Tent") return new[] { "Tent_Entrance", "Tent_Closed", "Tent_SideLeft", "Tent_SideRight" }[Mathf.Clamp(index, 0, 3)];
        if (kind == "Table") return "MapTable_" + (index < 6 ? "Horizontal_" : "Vertical_") + (index % 2 == 0 ? "A" : "B");
        if (kind == "Sign") return "Sign_Arrow" + new[] { "Down", "Up", "Left", "Right" }[Mathf.Clamp(index - 8, 0, 3)];
        if (kind == "Crates") return new[] { "SupplyCrates_BagRight", "SupplyCrates_BagLeft", "SupplyCrates_Closed", "SupplyCrates_BagFront" }[Mathf.Clamp(index - 12, 0, 3)];
        int column = Mathf.Clamp(Mathf.FloorToInt(sprite.rect.center.x / sprite.texture.width * 4), 0, 3);
        if (kind == "Bridge")
        {
            bool junction = sprite.rect.center.y / sprite.texture.height < .70f;
            return junction ? "Bridge_Junction" + new[] { "Down", "Up", "Left", "Right" }[column]
                : "Bridge_" + new[] { "Horizontal", "Vertical", "DiagonalUp", "DiagonalDown" }[column];
        }
        if (kind == "Ruins") return "RuinedWall_" + new[] { "CornerLeft_A", "CornerRight_A", "CornerLeft_B", "CornerRight_B" }[column];
        if (kind == "Monument") return "StoneMarker_" + new[] { "Carved", "Plain", "SideLeft", "SideRight" }[column];
        return "Rock_" + index;
    }
    private static string Category(SpriteRenderer sprite)
    {
        switch (Scene2PropSetup.Kind(sprite))
        {
            case "Tree": return "Trees/StandingTrees";
            case "Log": return "Trees/FallenLogs";
            case "Bush": return "Vegetation";
            case "Tent": return "Structures/Tents";
            case "Bridge": return "Structures/Bridges";
            case "Ruins": return "Structures/Ruins";
            case "Monument": return "Props/StoneMarkers";
            case "Table": return "Props/Tables";
            case "Sign": return "Props/Signs";
            case "Crates": return "Props/Supplies";
            default: return "Props/Rocks";
        }
    }
    private static Transform Group(Transform parent, string path)
    {
        foreach (string name in path.Split('/'))
        {
            var child = parent.Find(name);
            if (child == null) { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create scenery category"); go.transform.SetParent(parent, false); child = go.transform; }
            parent = child;
        }
        return parent;
    }
    [MenuItem("Tools/Map 2/Complete Prefab Library and Organize Scene")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = false;
        var report = new StringBuilder();
        try
        {
            if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
            string backup = "Library/Scene2OrganizationBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            Directory.CreateDirectory(backup);
            if (!EditorSceneManager.SaveScene(scene, backup + "/Scence2.unity", true)) throw new IOException("Scene backup failed.");
            foreach (var file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(Folder.Length).TrimStart('/', '\\');
                string destination = Path.Combine(backup, "Prefabs", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(file, destination, true);
            }
            var sprites = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true)).Where(Scene2PropSetup.Candidate).ToArray();
            var poses = sprites.ToDictionary(s => s.transform, s => s.transform.localToWorldMatrix);
            // Preserve scene-authored variants before completing their source assets, so newly added
            // prefab colliders cannot be inherited alongside an existing scene collider override.
            var shapes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Collider2D>(true))
                .ToDictionary(c => c, ColliderShape);
            foreach (var sprite in sprites)
            {
                var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(sprite.gameObject);
                if (instance != null) PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            foreach (var pair in shapes)
                if (pair.Key == null || ColliderShape(pair.Key) != pair.Value) throw new InvalidOperationException("Collider changed while preserving scene variants.");
            var colliderState = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Collider2D>(true)).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            var representatives = sprites.GroupBy(s => Key(s.sprite)).ToDictionary(g => g.Key, g => g.First());
            var covered = new HashSet<string>();
            int renamed = 0, created = 0, updated = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var view = asset.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(Scene2PropSetup.Candidate);
                if (view == null) continue;
                string key = Key(view.sprite);
                string name = Name(view);
                string directory = Folder + "/" + Category(view);
                Directory.CreateDirectory(directory);
                string target = directory + "/" + name + ".prefab";
                if (target != path)
                {
                    if (File.Exists(target)) target = AssetDatabase.GenerateUniqueAssetPath(target);
                    AssetDatabase.Refresh();
                    string error = AssetDatabase.MoveAsset(path, target);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    path = target; renamed++;
                }
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    contents.name = name;
                    var sprite = contents.GetComponentsInChildren<SpriteRenderer>(true).First(Scene2PropSetup.Candidate);
                    if (representatives.TryGetValue(key, out var source)) CopyMissingColliders(source, sprite);
                    var before = contents.GetComponentsInChildren<Collider2D>(true).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
                    int add = 0, sort = 0, keep = 0;
                    Scene2PropSetup.Configure(sprite, ref add, ref sort, ref keep);
                    foreach (var pair in before)
                        if (EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new InvalidOperationException("Prefab collider changed: " + path);
                    if (contents.transform.position != Vector3.zero) contents.transform.position = Vector3.zero;
                    PrefabUtility.SaveAsPrefabAsset(contents, path, out bool saved);
                    if (!saved) throw new IOException("Prefab save failed: " + path);
                    updated++; covered.Add(key); report.AppendLine("PREFAB " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            foreach (var pair in representatives.Where(pair => !covered.Contains(pair.Key)))
            {
                var source = pair.Value;
                string directory = Folder + "/" + Category(source);
                Directory.CreateDirectory(directory); AssetDatabase.Refresh();
                string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + Name(source) + ".prefab");
                var clone = Object.Instantiate(source.gameObject);
                try
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(clone)) PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    clone.name = Name(source); clone.transform.position = Vector3.zero;
                    PrefabUtility.SaveAsPrefabAsset(clone, path, out bool saved);
                    if (!saved) throw new IOException("New prefab save failed: " + path);
                    covered.Add(pair.Key); created++; report.AppendLine("NEW " + path);
                }
                finally { Object.DestroyImmediate(clone); }
            }
            var level = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ReusableLevelConfiguration>(true)).FirstOrDefault();
            Transform parent = level != null ? level.transform : scene.GetRootGameObjects().First().transform;
            Transform environment = Group(parent, "Environment");
            foreach (var type in sprites.GroupBy(s => Key(s.sprite)))
            {
                var ordered = type.OrderByDescending(s => s.transform.position.y).ThenBy(s => s.transform.position.x).ToArray();
                for (int i = 0; i < ordered.Length; i++)
                {
                    var sprite = ordered[i];
                    Undo.RecordObject(sprite.gameObject, "Name map scenery");
                    sprite.name = Name(sprite) + (ordered.Length > 1 ? "_" + (i + 1).ToString("D3") : "");
                    var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(sprite.gameObject);
                    if (outer != null && outer != sprite.gameObject)
                        PrefabUtility.UnpackPrefabInstance(outer, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    Undo.SetTransformParent(sprite.transform, Group(environment, Category(sprite)), "Group Map 2 scenery");
                    if (PrefabUtility.IsPartOfPrefabInstance(sprite)) PrefabUtility.RecordPrefabInstancePropertyModifications(sprite.gameObject);
                }
            }
            foreach (var pair in colliderState)
                if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new InvalidOperationException("Scene collider changed.");
            if (scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Collider2D>(true).Length) != colliderState.Count)
                throw new InvalidOperationException("Duplicate or missing scene colliders after prefab synchronization.");
            foreach (var pair in poses)
                for (int i = 0; i < 16; i++)
                    if (Mathf.Abs(pair.Key.localToWorldMatrix[i] - pair.Value[i]) > .005f) throw new InvalidOperationException("Scenery moved.");
            // Remove only now-empty organizational containers, never functional objects.
            foreach (var transform in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Reverse().ToArray())
                if (transform != null && transform != environment && transform.childCount == 0 && transform.GetComponents<Component>().Length == 1 &&
                    (transform.name == "Enviroment" || transform.name == "Trees")) Undo.DestroyObjectImmediate(transform.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            report.Insert(0, "SUCCESS\nBackup: " + backup + "\nExisting prefabs updated: " + updated + "\nPrefab assets renamed/moved: " + renamed +
                "\nMissing prefabs added: " + created + "\nScene objects named/grouped: " + sprites.Length +
                "\nPASS: all " + colliderState.Count + " scene colliders unchanged; world transforms preserved; " + covered.Count + " sprite types covered.\n");
            File.WriteAllText(Result, report.ToString());
            AssetDatabase.Refresh();
        }
        catch (Exception error) { File.WriteAllText(Result, "FAILED\n" + error + "\n" + report); Debug.LogException(error); }
        finally { if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
    }
    private static void CopyMissingColliders(SpriteRenderer source, SpriteRenderer target)
    {
        if (target.GetComponentsInChildren<Collider2D>(true).Length != 0) return;
        foreach (var collider in source.GetComponentsInChildren<Collider2D>(true))
        {
            GameObject destination = target.gameObject;
            if (collider.transform != source.transform)
            {
                string relative = AnimationUtility.CalculateTransformPath(collider.transform, source.transform);
                var existing = target.transform.Find(relative);
                if (existing != null) destination = existing.gameObject;
                else
                {
                    var child = Object.Instantiate(collider.gameObject, target.transform, false);
                    child.name = collider.name;
                    continue;
                }
            }
            ComponentUtility.CopyComponent(collider);
            ComponentUtility.PasteComponentAsNew(destination);
        }
    }
    private static string ColliderShape(Collider2D collider)
    {
        string result = collider.GetType().Name + ":" + collider.enabled + ":" + collider.isTrigger + ":" + collider.offset.ToString("R");
        if (collider is BoxCollider2D box) return result + ":" + box.size.ToString("R") + ":" + box.edgeRadius;
        if (collider is CircleCollider2D circle) return result + ":" + circle.radius;
        if (collider is CapsuleCollider2D capsule) return result + ":" + capsule.size.ToString("R") + ":" + capsule.direction;
        if (collider is PolygonCollider2D polygon)
            for (int i = 0; i < polygon.pathCount; i++) result += ":" + string.Join(";", polygon.GetPath(i).Select(p => p.ToString("R")));
        return result;
    }
}

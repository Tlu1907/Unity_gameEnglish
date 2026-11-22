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
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>Scoped authoring tools for Map 2 scenery; source/shared prefabs are not modified.</summary>
[InitializeOnLoad]
public static class Scene2PropSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scence_2/Scence2.unity";
    private const string Request = "Library/Scene2PropSetup.request";
    private const string Result = "Library/Scene2PropSetup.result";
    static Scene2PropSetup() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action;
        try { action = File.ReadAllText(Request).Trim(); File.Delete(Request); }
        catch (IOException) { return; }
        Run(action == "apply");
    }
    [MenuItem("Tools/Map 2/Audit Scenery")]
    public static void Audit() => Run(false);
    [MenuItem("Tools/Map 2/Configure Scenery Colliders and Sorting")]
    public static void Apply() => Run(true);

    internal static bool Candidate(SpriteRenderer sprite)
    {
        if (sprite.sprite == null || sprite.GetComponentInParent<Canvas>() != null ||
            sprite.GetComponentInParent<PlayerMovement>() != null || sprite.GetComponentInParent<EnemyBrain>() != null ||
            sprite.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0 || sprite.name == "MapBackground") return false;
        string path = AssetDatabase.GetAssetPath(sprite.sprite).Replace('\\', '/');
        return path.StartsWith("Assets/_Project/Gameplay/Items/Map2/UI/") || sprite.name.StartsWith("stone_", StringComparison.OrdinalIgnoreCase);
    }
    internal static string Kind(SpriteRenderer sprite)
    {
        string file = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sprite.sprite)).ToLowerInvariant();
        Vector2 center = sprite.sprite.rect.center;
        float row = 1f - center.y / sprite.sprite.texture.height;
        if (file == "tree") return row < .5f ? "Tree" : row < .75f ? "Log" : "Bush";
        if (file == "item") return row < .25f ? "Tent" : row < .5f ? "Table" : row < .75f ? "Sign" : "Crates";
        if (file == "item2") return row < .5f ? "Bridge" : row < .75f ? "Ruins" : "Monument";
        return "Rock";
    }
    private static void Run(bool apply)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = false;
        try
        {
            if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
            var sprites = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true)).Where(Candidate).ToArray();
            var report = new StringBuilder("Scene: " + scene.path + "\nCount: " + sprites.Length + "\n");
            foreach (var sprite in sprites)
            {
                report.AppendLine(Kind(sprite) + " | " + sprite.name + " | sprite=" + sprite.sprite.name +
                    " | rect=" + sprite.sprite.rect + " | layer=" + LayerMask.LayerToName(sprite.gameObject.layer) +
                    " | sorting=" + sprite.sortingLayerName + " | colliders=" + string.Join(",", sprite.GetComponents<Collider2D>().Select(c => c.GetType().Name)) +
                    " | body=" + (sprite.GetComponent<Rigidbody2D>() != null ? sprite.GetComponent<Rigidbody2D>().bodyType.ToString() : "none"));
            }
            if (apply)
            {
                string backup = "Library/Scene2PropsBackups/Scence2_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not back up Map 2.");
                var existingColliders = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Collider2D>(true))
                    .ToDictionary(collider => collider, collider => EditorJsonUtility.ToJson(collider));
                var poses = sprites.ToDictionary(sprite => sprite.transform, sprite => sprite.transform.localToWorldMatrix);
                var tiles = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Tilemap>(true))
                    .ToDictionary(layer => layer, layer => layer.GetTilesBlock(layer.cellBounds));
                int added = 0, sorts = 0, preserved = 0;
                Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Configure Map 2 scenery");
                try
                {
                    foreach (var sprite in sprites) Configure(sprite, ref added, ref sorts, ref preserved);
                    foreach (var pair in existingColliders)
                        if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value)
                            throw new InvalidOperationException("An authored collider was changed; restoring the operation.");
                    foreach (var pair in poses)
                        if (pair.Key.localToWorldMatrix != pair.Value) throw new InvalidOperationException("A scenery transform was changed.");
                    foreach (var pair in tiles)
                        if (!pair.Value.SequenceEqual(pair.Key.GetTilesBlock(pair.Key.cellBounds))) throw new InvalidOperationException("Painted terrain was changed.");
                    foreach (var sprite in sprites)
                    {
                        var kind = Kind(sprite);
                        var collider = sprite.GetComponentsInChildren<Collider2D>(true).FirstOrDefault(c => !c.isTrigger);
                        if (collider != null && collider.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
                            throw new InvalidOperationException("Solid scenery is not on the Obstacle layer: " + sprite.name);
                        if (kind != "Bridge" && sprite.GetComponent<WorldDepthSort>() == null)
                            throw new InvalidOperationException("Scenery is missing depth sorting: " + sprite.name);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Map 2.");
                    Undo.CollapseUndoOperations(undo);
                    report.Insert(0, "SUCCESS\nBackup: " + backup + "\nAdded colliders: " + added +
                        "\nPreserved authored prop colliders: " + preserved + "\nDepth-sorted objects: " + sorts +
                        "\nPASS: all " + existingColliders.Count + " existing scene colliders unchanged; all scenery transforms and painted terrain unchanged.\n");
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, "FAILED\n" + exception); Debug.LogException(exception); }
        finally { if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
    }

    internal static void Configure(SpriteRenderer sprite, ref int added, ref int sorts, ref int preserved)
    {
        var go = sprite.gameObject;
        string kind = Kind(sprite);
        var authored = go.GetComponentsInChildren<Collider2D>(true);
        preserved += authored.Length;
        // Never resize, replace, disable or change trigger state on an authored collider.
        bool flatDecoration = kind == "Bush" && sprite.sprite.name == "tree_15" && authored.Length == 0;
        if (authored.Length == 0 && kind != "Bridge" && !flatDecoration)
        {
            AddFootprint(sprite, kind);
            added++;
        }
        int obstacle = LayerMask.NameToLayer("Obstacle");
        if (obstacle < 0) throw new InvalidOperationException("Obstacle layer is missing.");
        foreach (var collider in go.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider.isTrigger) continue;
            Undo.RecordObject(collider.gameObject, "Set scenery physics layer");
            collider.gameObject.layer = obstacle;
            Record(collider.gameObject);
        }
        var body = go.GetComponent<Rigidbody2D>();
        if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
        {
            Undo.RecordObject(body, "Keep scenery stationary");
            body.bodyType = RigidbodyType2D.Static;
            Record(body);
        }
        var sort = go.GetComponent<WorldDepthSort>();
        var group = go.GetComponent<SortingGroup>();
        if (kind == "Bridge")
        {
            // Authored rail colliders are preserved; the deck is drawn beneath characters.
            if (sort != null) { Undo.RecordObject(sort, "Use fixed bridge sorting"); sort.enabled = false; Record(sort); }
            Undo.RecordObject(sprite, "Draw bridge deck below characters");
            sprite.sortingLayerName = "MapGround"; sprite.sortingOrder = 10;
            if (group != null)
            { Undo.RecordObject(group, "Draw bridge below characters"); group.sortingLayerName = "MapGround"; group.sortingOrder = 10; Record(group); }
        }
        else
        {
            if (sort == null) sort = Undo.AddComponent<WorldDepthSort>(go);
            if (group == null) group = Undo.AddComponent<SortingGroup>(go);
            Undo.RecordObject(sort, "Configure scenery depth");
            Undo.RecordObject(group, "Configure scenery depth group");
            Undo.RecordObject(sprite, "Set local scenery layer");
            sort.target = sprite; sort.group = group; sort.groundAnchor = go.transform;
            sort.feetCollider = go.GetComponentsInChildren<Collider2D>(true).FirstOrDefault(c => !c.isTrigger);
            if (sort.feetCollider == null) sort.footOffset = sprite.sprite.bounds.min.y;
            sort.enabled = true;
            sprite.sortingLayerName = "WorldProps"; sprite.sortingOrder = 0;
            sort.Apply();
            Record(sort); Record(group);
            sorts++;
        }
        foreach (var shadow in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (shadow == sprite || shadow.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0) continue;
            Undo.RecordObject(shadow, "Keep shadow below scenery");
            shadow.sortingLayerName = "GroundShadows"; shadow.sortingOrder = -1;
            Record(shadow);
        }
        Record(sprite);
    }
    private static void AddFootprint(SpriteRenderer sprite, string kind)
    {
        Bounds bounds = sprite.sprite.bounds;
        float width = .72f, height = .28f;
        if (kind == "Tree") { width = .35f; height = .18f; }
        else if (kind == "Tent") { width = .82f; height = .34f; }
        else if (kind == "Table") { width = .82f; height = .38f; }
        else if (kind == "Crates") { width = .85f; height = .42f; }
        else if (kind == "Sign") { width = .25f; height = .18f; }
        else if (kind == "Monument") { width = .65f; height = .23f; }
        if (kind == "Log")
        {
            var polygon = Undo.AddComponent<PolygonCollider2D>(sprite.gameObject);
            int column = Mathf.Clamp(Mathf.FloorToInt(sprite.sprite.rect.center.x / sprite.sprite.texture.width * 4), 0, 3);
            Vector2[] points = column == 0 ? new[] { new Vector2(.05f,.3f), new Vector2(.95f,.3f), new Vector2(.95f,.7f), new Vector2(.05f,.7f) }
                : column == 1 ? new[] { new Vector2(.3f,.05f), new Vector2(.7f,.05f), new Vector2(.7f,.95f), new Vector2(.3f,.95f) }
                : column == 2 ? new[] { new Vector2(.05f,.2f), new Vector2(.2f,.05f), new Vector2(.95f,.8f), new Vector2(.8f,.95f) }
                : new[] { new Vector2(.05f,.8f), new Vector2(.2f,.95f), new Vector2(.95f,.2f), new Vector2(.8f,.05f) };
            polygon.SetPath(0, points.Select(point => new Vector2(bounds.min.x + point.x * bounds.size.x, bounds.min.y + point.y * bounds.size.y)).ToArray());
            return;
        }
        var box = Undo.AddComponent<BoxCollider2D>(sprite.gameObject);
        box.size = new Vector2(bounds.size.x * width, bounds.size.y * height);
        box.offset = new Vector2(bounds.center.x, bounds.min.y + box.size.y * .5f);
    }
    private static void Record(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayRefactorMigration
{
    private const string Root = "Assets/_Project";
    [MenuItem("EarthDefender/Refactor/Migrate project scenes and prefabs")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before migration.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your open scenes before migration.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        int changed = 0;
        try
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(VariantDepth).ToArray();
            foreach (string path in prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (MigrateRoot(root, 1 + VariantDepth(path), false))
                    { PrefabUtility.SaveAsPrefabAsset(root, path); changed++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool dirty = false;
                foreach (GameObject root in scene.GetRootGameObjects()) dirty |= MigrateRoot(root, 1000, true);
                if (dirty) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); changed++; }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Refactor migration completed: {changed} assets changed; legacy callbacks retained.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    private static int VariantDepth(string path)
    {
        int depth = 0;
        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        while (asset != null && PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant)
        { depth++; asset = PrefabUtility.GetCorrespondingObjectFromSource(asset); }
        return depth;
    }
    private static bool MigrateRoot(GameObject root, int version, bool scene)
    {
        // Compare the full hierarchy: configuring a trigger also changes its linked chest.
        string before = Snapshot(root);
        foreach (PlayerMovement player in root.GetComponentsInChildren<PlayerMovement>(true)) player.Migrate(version);
        foreach (EnemyBrain enemy in root.GetComponentsInChildren<EnemyBrain>(true)) enemy.Migrate(version);
        foreach (QuizTrigger trigger in root.GetComponentsInChildren<QuizTrigger>(true)) trigger.ConfigureLinks(scene);
        foreach (QuizController controller in root.GetComponentsInChildren<QuizController>(true)) controller.ResolveView(scene);
        foreach (QuizPanel panel in root.GetComponentsInChildren<QuizPanel>(true)) panel.ConfigureLegacyLayout();
        bool changed = before != Snapshot(root);
        if (changed)
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                EditorUtility.SetDirty(component);
                if (scene && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        return changed;
    }
    private static string Snapshot(GameObject root) => string.Join("\n", root.GetComponentsInChildren<MonoBehaviour>(true)
        .Where(c => c != null).Select(c => EditorJsonUtility.ToJson(c)));
}
#endif

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Adds editable grammar fields in the Editor; runtime only fills their text.</summary>
[InitializeOnLoad]
public static class DialogueGrammarSetup
{
    static DialogueGrammarSetup() => EditorApplication.delayCall += Run;

    [MenuItem("Tools/Story/Set Up Dialogue Grammar Texts")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var panels = Object.FindObjectsByType<StoryQuizPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        TMP_Text template = null;
        foreach (var panel in panels)
        {
            template = FindGrammar(panel);
            if (template != null) break;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/UI/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<StoryQuizPanel>(true) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var panel in root.GetComponentsInChildren<StoryQuizPanel>(true))
                    changed |= EnsureGrammar(panel, template, false);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var panel in panels)
            if (panel != null && panel.gameObject.scene.IsValid() && panel.gameObject.scene.isLoaded &&
                EnsureGrammar(panel, template, true))
                EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
    }

    private static TMP_Text FindGrammar(StoryQuizPanel panel)
    {
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            if (string.Equals(text.name.Trim(), "Grammar", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text.name.Trim(), "Grammar Notes", System.StringComparison.OrdinalIgnoreCase))
                return text;
        return null;
    }

    private static bool EnsureGrammar(StoryQuizPanel panel, TMP_Text template, bool undo)
    {
        var settings = new SerializedObject(panel);
        var property = settings.FindProperty("grammarNotesText");
        TMP_Text text = property.objectReferenceValue as TMP_Text;
        if (text != null) return false;
        text = FindGrammar(panel);
        if (text == null)
        {
            TMP_Text source = template != null ? template :
                settings.FindProperty("vietnameseText").objectReferenceValue as TMP_Text;
            if (source == null) return false;
            text = Object.Instantiate(source, panel.transform, false);
            text.name = "Grammar";
            if (undo) Undo.RegisterCreatedObjectUndo(text.gameObject, "Add dialogue grammar text");
            if (template == null)
            {
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(.04f, .025f);
                rect.anchorMax = new Vector2(.67f, .16f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            text.text = "";
            text.richText = false;
            text.raycastTarget = false;
            text.gameObject.SetActive(true);
        }
        property.objectReferenceValue = text;
        settings.ApplyModifiedProperties();
        if (undo) PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
        return true;
    }
}

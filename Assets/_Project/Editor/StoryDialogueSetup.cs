using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-time authoring tool. Existing authored layouts are never rebuilt automatically.</summary>
public static class StoryDialogueSetup
{
    public const string PrefabPath = "Assets/_Project/UI/Prefabs/Resources/StoryDialogue.prefab";
    private const string ScenePath = "Assets/_Project/Scenes/Sence_1/Scene1.unity";

    [MenuItem("Tools/Story/Add Static Treasure Dialogue")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = BuildPrefab();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (prefab == null || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<StoryQuizPanel>(true) != null)
            { root.name = "TreasureMirrorDialogueCanvas"; Selection.activeGameObject = root; return; }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "TreasureMirrorDialogueCanvas";
        Undo.RegisterCreatedObjectUndo(instance, "Add static treasure dialogue");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = instance;
        Debug.Log("Static treasure dialogue added. Edit DialoguePanel and CustomActions in the Hierarchy.", instance);
    }

    private static GameObject BuildPrefab()
    {
        Scene preview = EditorSceneManager.OpenPreviewScene(ScenePath);
        GameObject canvas = null;
        try
        {
            NPCIntroDialogue template = null;
            foreach (GameObject root in preview.GetRootGameObjects())
            {
                template = root.GetComponentInChildren<NPCIntroDialogue>(true);
                if (template != null) break;
            }
            if (template == null) throw new System.InvalidOperationException("Scene1 needs the existing NPC dialogue frame.");
            StoryQuizPanel view = template.CreateStoryPanel();
            if (view == null) throw new System.InvalidOperationException("NPC dialogue frame is missing its text references.");
            canvas = view.transform.parent.gameObject;
            canvas.name = "TreasureMirrorDialogueCanvas";
            view.gameObject.name = "DialoguePanel";
            var settings = new SerializedObject(view);
            TMP_Text english = (TMP_Text)settings.FindProperty("englishText").objectReferenceValue;
            TMP_Text vietnamese = (TMP_Text)settings.FindProperty("vietnameseText").objectReferenceValue;
            TMP_Text speaker = (TMP_Text)settings.FindProperty("nameText").objectReferenceValue;
            TMP_Text prompt = (TMP_Text)settings.FindProperty("promptText").objectReferenceValue;
            Image portrait = (Image)settings.FindProperty("portrait").objectReferenceValue;
            if (portrait != null) portrait.sprite = null;
            if (speaker != null)
            {
                speaker.text = "LINH HỒN";
                Place(speaker.rectTransform, new Vector2(.2f, .86f), new Vector2(.87f, .97f));
            }
            english.text = "Agent... I am a spirit bound to this mirror. Will you listen?";
            vietnamese.text = "Đặc vụ... tôi là linh hồn bị giữ trong chiếc gương này. Anh có thể lắng nghe tôi không?";
            if (prompt != null)
            {
                prompt.text = "C to continue · Esc to close";
                Place(prompt.rectTransform, new Vector2(.2f, .02f), new Vector2(.96f, .1f));
            }
            view.ConfigureActionButtons(null, null);
            for (int i = 0; i < 4; i++)
            {
                var button = (Button)settings.FindProperty("buttons").GetArrayElementAtIndex(i).objectReferenceValue;
                var label = (TMP_Text)settings.FindProperty("labels").GetArrayElementAtIndex(i).objectReferenceValue;
                label.text = (i + 1) + ". " + new[] { "sword", "key", "chest", "shield" }[i];
                button.gameObject.SetActive(true);
            }
            var custom = new GameObject("CustomActions", typeof(RectTransform));
            custom.layer = view.gameObject.layer;
            custom.transform.SetParent(view.transform, false);
            Place((RectTransform)custom.transform, new Vector2(.025f, .02f), new Vector2(.18f, .15f));
            // Keep the frame available for editing, but hidden by default in the scene.
            view.gameObject.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath);
        }
        finally
        {
            if (canvas != null) Object.DestroyImmediate(canvas);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static Button Button(Transform parent, string name, string caption, TMP_FontAsset font, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, min, max);
        var image = go.GetComponent<Image>();
        image.color = new Color(.1f, .15f, .22f, .96f);
        Button button = go.GetComponent<Button>(); button.targetGraphic = image;
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = go.layer;
        labelObject.transform.SetParent(go.transform, false);
        Place((RectTransform)labelObject.transform, Vector2.zero, Vector2.one);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = font; label.text = caption; label.color = Color.white;
        label.fontSize = 22; label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 22;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return button;
    }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}

using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ChestSceneSetup
{
    private const string SessionKey = "DoAn.Scene1.ChestQuizSetup.v2.RootVisual";
    private const string SpritePath = "Assets/_Project/Art/Sprites/Item/Items.png";
    private const string QuestionPath = "Assets/_Project/Learning/Quiz/Data/Questions/Test/Q_01_test_vocab_01.asset";
    static ChestSceneSetup() => EditorApplication.update += SetupOnce;
    private static bool CanEdit() => !EditorApplication.isPlayingOrWillChangePlaymode
        && PrefabStageUtility.GetCurrentPrefabStage() == null;
    private static void SetupOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) { EditorApplication.update -= SetupOnce; return; }
        if (!CanEdit() || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Sence_1/Scene1.unity") return;
        SpriteRenderer[] candidates = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<SpriteRenderer>(true))
            .Where(sprite => sprite.sprite != null && sprite.sprite.name == "Items_1"
                && AssetDatabase.GetAssetPath(sprite.sprite) == SpritePath).ToArray();
        if (candidates.Length != 1) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.update -= SetupOnce;
        Setup(ResolveRoot(candidates[0].gameObject));
    }

    [MenuItem("Tools/Chests/Setup Selected Chest with Quiz", true)]
    private static bool Validate() => CanEdit() && Selection.activeGameObject != null
        && Selection.activeGameObject.scene == SceneManager.GetActiveScene();
    [MenuItem("Tools/Chests/Setup Selected Chest with Quiz")]
    public static void Run()
    {
        if (Validate()) Setup(ResolveRoot(Selection.activeGameObject));
    }

    private static GameObject ResolveRoot(GameObject selected)
    {
        // Selecting the Visual is equivalent to selecting its collider-bearing chest root.
        Transform parent = selected.transform.parent;
        if (selected.GetComponent<SpriteRenderer>() != null && parent != null
            && parent.GetComponent<Collider2D>() != null
            && (string.Equals(parent.name, "Key", StringComparison.OrdinalIgnoreCase)
                || parent.GetComponent<Chest>() != null
                || string.Equals(selected.name, "Visual", StringComparison.OrdinalIgnoreCase)))
            return parent.gameObject;
        return selected;
    }

    private static void Setup(GameObject go)
    {
        SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null && renderers.Length == 1) renderer = renderers[0];
        Sprite open = AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().FirstOrDefault(sprite => sprite.name == "Items_5");
        var question = AssetDatabase.LoadAssetAtPath<QuestionData>(QuestionPath);
        int layer = LayerMask.NameToLayer("Obstacle");
        if (renderer == null || open == null || question == null || layer < 0)
        { Debug.LogWarning("Chest setup requires one SpriteRenderer on the root or a Visual child, Items_5, sample QuestionData and an Obstacle layer.", go); return; }
        Chest[] childChests = go.GetComponentsInChildren<Chest>(true).Where(chest => chest.gameObject != go).ToArray();
        if (childChests.Length > 1)
        { Debug.LogWarning("Chest setup: select one chest root, not a group containing multiple chests.", go); return; }
        if (go.GetComponent<QuizTrigger>() != null || go.GetComponentInParent<QuizTrigger>() != null)
        { Debug.LogWarning("Chest setup: this object already belongs to a QuizTrigger. Keep that flow or remove it before using direct chest interaction.", go); return; }
        Scene scene = go.scene;
        QuizController[] controllers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<QuizController>(true)).ToArray();
        if (controllers.Length > 1)
        { Debug.LogWarning("Chest setup found multiple QuizControllers. Keep one controller in the scene first.", go); return; }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup chest and quiz");
        try
        {
            QuizController controller = controllers.FirstOrDefault();
            if (controller == null) controller = CreateQuizSystem(scene);
            Chest chest = Ensure<Chest>(go);
            ChestQuizInteraction interaction = Ensure<ChestQuizInteraction>(go);
            // Preserve the question/visual configuration if old scripts were left on Visual.
            if (childChests.Length == 1)
            {
                Chest oldChest = childChests[0];
                if (new SerializedObject(chest).FindProperty("openedSprite").objectReferenceValue == null)
                {
                    Undo.RecordObject(chest, "Move chest settings to root");
                    EditorUtility.CopySerialized(oldChest, chest);
                }
                ChestQuizInteraction oldInteraction = oldChest.GetComponent<ChestQuizInteraction>();
                if (oldInteraction != null)
                {
                    if (new SerializedObject(interaction).FindProperty("question").objectReferenceValue == null)
                    {
                        Undo.RecordObject(interaction, "Move quiz settings to root");
                        EditorUtility.CopySerialized(oldInteraction, interaction);
                    }
                    Undo.DestroyObjectImmediate(oldInteraction);
                }
                Undo.DestroyObjectImmediate(oldChest);
            }
            SetReference(chest, "chestRenderer", renderer);
            SetReference(chest, "openedSprite", open);
            SetReference(interaction, "question", question);
            SetReference(interaction, "controller", controller);
            var chestSettings = new SerializedObject(chest);
            chestSettings.FindProperty("requiresQuiz").boolValue = true;
            var interactionSettings = new SerializedObject(interaction);
            var assignedQuestion = interactionSettings.FindProperty("question").objectReferenceValue as QuestionData;
            chestSettings.FindProperty("questionId").stringValue = assignedQuestion != null ? assignedQuestion.id : string.Empty;
            chestSettings.ApplyModifiedProperties();
            // Preserve a legacy open prefab if one was explicitly configured.
            Collider2D solid = go.GetComponents<Collider2D>().FirstOrDefault(c => !c.isTrigger);
            if (solid == null) solid = Undo.AddComponent<BoxCollider2D>(go);
            // The root owns collision; stale colliders on Visual must not block after opening.
            foreach (Collider2D childCollider in go.GetComponentsInChildren<Collider2D>(true))
            {
                if (childCollider.gameObject == go || childCollider.isTrigger || !childCollider.enabled) continue;
                Undo.RecordObject(childCollider, "Disable duplicate visual collider");
                childCollider.enabled = false;
                Record(childCollider);
            }
            Undo.RecordObject(go, "Set chest layer");
            go.layer = layer;
            foreach (Component component in go.GetComponents<Component>()) Record(component);
            Record(go);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Chest quiz configured: answer correctly to swap to Items_5 and open once. Ensure Player GridMovement.Obstacle Mask includes the chest layer. Review and save scene; Undo is available.", go);
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static QuizController CreateQuizSystem(Scene scene)
    {
        GameObject root = Create("ChestQuizSystem", null);
        SceneManager.MoveGameObjectToScene(root, scene);
        GameObject canvasObject = Create("QuizCanvas", root.transform, true);
        Canvas canvas = Undo.AddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        Undo.AddComponent<GraphicRaycaster>(canvasObject);
        GameObject panel = Create("QuizPanel", canvasObject.transform, true);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.sizeDelta = new Vector2(720, 560);
        Undo.AddComponent<Image>(panel).color = new Color(.08f, .1f, .16f, .98f);
        QuizPanel view = Undo.AddComponent<QuizPanel>(panel);
        TMP_Text title = CreateText("Question", panel.transform, -30, 130, "Question");
        title.fontSize = 30;
        Button[] buttons = new Button[4];
        for (int i = 0; i < buttons.Length; i++)
            buttons[i] = CreateButton("Answer " + (i + 1), panel.transform, -175 - 65 * i, 55);
        TMP_Text feedback = CreateText("Feedback", panel.transform, -435, 35, "");
        feedback.fontSize = 20;
        Button cancel = CreateButton("Cancel", panel.transform, -485, 45);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(cancel.onClick, view.OnCancelClicked);
        var viewSettings = new SerializedObject(view);
        viewSettings.FindProperty("contentText").objectReferenceValue = title;
        viewSettings.FindProperty("feedbackText").objectReferenceValue = feedback;
        SerializedProperty answers = viewSettings.FindProperty("answerButtons");
        answers.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++) answers.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        viewSettings.ApplyModifiedProperties();
        QuizController controller = Undo.AddComponent<QuizController>(root);
        SetReference(controller, "panel", panel);
        SetReference(controller, "contentText", title);
        panel.SetActive(false);

        bool hasEventSystem = scene.GetRootGameObjects().Any(go => go.GetComponentInChildren<EventSystem>(true) != null);
        if (!hasEventSystem)
        {
            GameObject events = Create("QuizEventSystem", root.transform);
            Undo.AddComponent<EventSystem>(events);
            // Project uses Both input handling; gameplay still uses UnityEngine.Input.
            Undo.AddComponent<StandaloneInputModule>(events);
        }
        return controller;
    }

    private static GameObject Create(string name, Transform parent, bool ui = false)
    {
        var go = ui ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create quiz object");
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }
    private static void LayoutTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(.06f, 1f);
        rect.anchorMax = new Vector2(.94f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, height);
    }
    private static TMP_Text CreateText(string name, Transform parent, float y, float height, string value)
    {
        GameObject go = Create(name, parent, true);
        LayoutTop((RectTransform)go.transform, y, height);
        var text = Undo.AddComponent<TextMeshProUGUI>(go);
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = 25;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }
    private static Button CreateButton(string name, Transform parent, float y, float height)
    {
        GameObject go = Create(name, parent, true);
        LayoutTop((RectTransform)go.transform, y, height);
        Image image = Undo.AddComponent<Image>(go);
        image.color = new Color(.18f, .27f, .42f);
        Button button = Undo.AddComponent<Button>(go);
        button.targetGraphic = image;
        TMP_Text label = CreateText("Label", go.transform, 0f, height, name);
        return button;
    }
    private static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? Undo.AddComponent<T>(go);
    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property.objectReferenceValue != null) return;
        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
        Record(target);
    }
    private static void Record(UnityEngine.Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}

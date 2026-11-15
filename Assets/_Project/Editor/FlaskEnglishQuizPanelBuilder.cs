using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class FlaskEnglishQuizPanelBuilder
{
    [MenuItem("GameObject/UI/Flask English Quiz (Test)", false, 2100)]
    private static void Create()
    {
        if (EditorApplication.isPlaying) return;
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Flask English Quiz");
        // A separate canvas avoids inheriting the inactive gameplay quiz panel.
        var root = new GameObject("Flask English Quiz Test", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Create test canvas");
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;

        var panel = Node("Panel", root.transform, typeof(Image));
        panel.GetComponent<Image>().color = new Color(.06f, .09f, .14f, .97f);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.1f, .04f);
        rect.anchorMax = new Vector2(.9f, .96f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Text(panel.transform, "Title", "Giải mã gương — Câu hỏi tiếng Anh", .88f, .98f, 27);
        var question = Text(panel.transform, "Question", "Đang lấy câu hỏi…", .65f, .87f, 22);

        var inputObject = Node("Answer Input", panel.transform, typeof(Image));
        Place(inputObject.GetComponent<RectTransform>(), .41f, .63f);
        inputObject.GetComponent<Image>().color = new Color(.93f, .95f, .98f);
        var viewport = Node("Viewport", inputObject.transform, typeof(RectMask2D));
        Stretch(viewport.GetComponent<RectTransform>(), 12);
        var answer = Text(viewport.transform, "Text", "", 0, 1, 22);
        answer.color = Color.black;
        answer.overflowMode = TextOverflowModes.Overflow;
        var placeholder = Text(viewport.transform, "Placeholder", "Nhập câu trả lời tiếng Anh…", 0, 1, 22);
        placeholder.color = Color.gray;
        var input = inputObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport.GetComponent<RectTransform>();
        input.textComponent = answer;
        input.placeholder = placeholder;
        input.targetGraphic = inputObject.GetComponent<Image>();
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.characterLimit = 8000;

        var submit = Button(panel.transform, "Submit", "Nộp bài", .04f, .96f);
        var status = Text(panel.transform, "Status", "", .24f, .32f, 19);
        var result = Text(panel.transform, "Result", "", .03f, .23f, 21);
        // Inactive while attaching so OnEnable only runs after references are assigned.
        panel.SetActive(false);
        panel.AddComponent<FlaskEnglishQuizPanel>().Configure(question, input, null, submit, status, result);

        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(events, "Create EventSystem");
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(group);
    }

    private static GameObject Node(string name, Transform parent, params System.Type[] components)
    {
        var node = new GameObject(name, typeof(RectTransform));
        node.transform.SetParent(parent, false);
        foreach (var component in components) node.AddComponent(component);
        return node;
    }

    private static void Place(RectTransform rect, float bottom, float top, float left = .04f, float right = .96f)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * padding;
        rect.offsetMax = Vector2.one * -padding;
    }

    private static TMP_Text Text(Transform parent, string name, string value, float bottom, float top, int size)
    {
        var node = Node(name, parent, typeof(TextMeshProUGUI));
        Place(node.GetComponent<RectTransform>(), bottom, top);
        var text = node.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = Color.white;
        text.richText = false;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button Button(Transform parent, string name, string label, float left, float right)
    {
        var node = Node(name, parent, typeof(Image), typeof(Button));
        Place(node.GetComponent<RectTransform>(), .34f, .40f, left, right);
        node.GetComponent<Image>().color = new Color(.16f, .38f, .58f);
        var text = Text(node.transform, "Label", label, 0, 1, 22);
        text.alignment = TextAlignmentOptions.Center;
        return node.GetComponent<Button>();
    }
}

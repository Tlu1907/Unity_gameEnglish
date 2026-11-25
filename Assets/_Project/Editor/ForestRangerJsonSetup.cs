using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>Connects the authored ranger UI to JSON without rebuilding the NPC or its layout.</summary>
[InitializeOnLoad]
public static class ForestRangerJsonSetup
{
    private const string Request = "Library/ForestRangerJsonSetup.request";
    private const string Result = "Library/ForestRangerJsonSetup.result";
    private const string ScenePath = "Assets/_Project/Scenes/Scence_2/Scence2.unity";
    private const string JsonPath = "Assets/_Project/Data/Questions/Map2/map2_forest_ranger_dialogue_en.json";
    static ForestRangerJsonSetup() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        if (action == "validate") Validate();
        else if (action == "preview") Preview();
        else if (action == "tests") ForestRangerJsonTests.Run();
        else if (action == "runtime-check") ForestRangerJsonSmoke.Run();
        else if (action == "refresh") AssetDatabase.Refresh();
        else Connect();
    }

    [MenuItem("Tools/Map 2/Connect Ranger JSON And UnityAI")]
    public static void Connect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var view = FindView(scene);
            if (view == null) throw new InvalidOperationException("ForestRangerDialogueCanvas is missing; create the ranger UI first.");
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            if (json == null || !NpcJsonDialogue.TryParse(json.text, out var source))
                throw new InvalidOperationException("Missing or invalid ranger dialogue JSON: " + JsonPath);
            var from = new SerializedObject(view);
            var panel = Read<GameObject>(from, "dialoguePanel");
            var input = Read<TMP_InputField>(from, "answerInput");
            var submit = Read<Button>(from, "submitAnswerButton");
            var data = Read<DialogueData>(from, "dialogue");
            if (panel == null || input == null || submit == null || data == null)
                throw new InvalidOperationException("The existing ranger panel, answer input, submit button and DialogueData must be assigned.");
            Directory.CreateDirectory("Library/RangerBackups");
            EditorSceneManager.SaveScene(scene, "Library/RangerBackups/BeforeJson_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity", true);
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Connect ranger dialogue JSON");
            Undo.RecordObject(data, "Assign ranger dialogue JSON");
            data.conversationJson = json;
            EditorUtility.SetDirty(data);
            var client = view.GetComponent<UnityAiDialogueClient>();
            if (client == null) client = Undo.AddComponent<UnityAiDialogueClient>(view.gameObject);
            var flow = view.GetComponent<NpcJsonDialoguePanel>();
            if (flow == null) flow = Undo.AddComponent<NpcJsonDialoguePanel>(view.gameObject);
            var template = Read<TMP_Text>(from, "continueText") ?? Read<TMP_Text>(from, "englishText");
            Button next = panel.transform.Find("JsonContinueButton")?.GetComponent<Button>();
            if (next == null)
            {
                var rect = Rect("JsonContinueButton", panel.transform);
                rect.anchorMin = rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one * .5f;
                rect.sizeDelta = new Vector2(160, 42);
                var close = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "CloseButton");
                if (close != null)
                {
                    var closeRect = (RectTransform)close.transform;
                    rect.anchorMin = closeRect.anchorMin; rect.anchorMax = closeRect.anchorMax; rect.pivot = closeRect.pivot;
                    rect.anchoredPosition = closeRect.anchoredPosition - Vector2.right * (closeRect.rect.width * .5f + 92);
                }
                else rect.anchoredPosition = new Vector2(-225, -37);
                var background = rect.gameObject.AddComponent<Image>(); background.color = new Color(.24f, .33f, .22f);
                next = rect.gameObject.AddComponent<Button>(); next.targetGraphic = background;
                var label = Text("Label", rect, template, "Tiếp tục");
                Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 24;
                label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 24;
            }
            TMP_Text feedback;
            var feedbackPanel = panel.transform.Find("JsonGradeFeedback") as RectTransform;
            if (feedbackPanel == null)
            {
                feedbackPanel = Rect("JsonGradeFeedback", panel.transform);
                feedbackPanel.anchorMin = feedbackPanel.anchorMax = new Vector2(0, 1);
                feedbackPanel.pivot = Vector2.zero;
                feedbackPanel.anchoredPosition = new Vector2(0, 18);
                feedbackPanel.sizeDelta = new Vector2(Mathf.Clamp(((RectTransform)panel.transform).rect.width * .62f, 500, 960), 340);
                feedbackPanel.gameObject.AddComponent<Image>().color = new Color(.045f, .095f, .075f, .98f);
                feedback = Text("Feedback", feedbackPanel, template, "");
                MakeScroll(feedbackPanel, feedback);
                feedbackPanel.gameObject.SetActive(false);
            }
            else feedback = feedbackPanel.GetComponentInChildren<TMP_Text>(true);
            if (feedback != null) feedback.textWrappingMode = TextWrappingModes.Normal;
            // Upgrade only the new controls once; subsequent connects preserve designer edits.
            var status = panel.transform.Find("JsonAnswerStatus")?.GetComponent<TMP_Text>();
            if (status == null)
            {
                status = Text("JsonAnswerStatus", panel.transform, template, "");
                var inputCorners = new Vector3[4]; input.textViewport.GetWorldCorners(inputCorners);
                var inputTop = panel.transform.InverseTransformPoint(inputCorners[1]);
                var inputLeft = panel.transform.InverseTransformPoint(inputCorners[0]);
                var submitCorners = new Vector3[4]; ((RectTransform)submit.transform).GetWorldCorners(submitCorners);
                var submitRight = panel.transform.InverseTransformPoint(submitCorners[2]);
                var bounds = ((RectTransform)panel.transform).rect;
                float left = inputLeft.x - bounds.xMin - 14;
                float width = Mathf.Clamp(submitRight.x - inputLeft.x + 14, 420, 760);
                var rect = status.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(left, 18); rect.sizeDelta = new Vector2(width, 46);
                status.fontSize = 20;
                feedbackPanel.anchorMin = feedbackPanel.anchorMax = new Vector2(0, 1);
                feedbackPanel.pivot = new Vector2(0, 1);
                feedbackPanel.anchoredPosition = new Vector2(left, -150);
                float available = bounds.yMax - 150 - inputTop.y - 20;
                feedbackPanel.sizeDelta = new Vector2(width, Mathf.Clamp(available, 120, 240));
            }
            var oldPrompt = Read<TMP_Text>(from, "continueText");
            if (oldPrompt != null) oldPrompt.gameObject.SetActive(false);
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true))
                if (label != status && label != feedback && label != Read<TMP_Text>(from, "learningHints") &&
                    (label.text.Trim() == "New Text" || label.text.Trim() == "Ngữ pháp")) label.gameObject.SetActive(false);
            var learning = Read<TMP_Text>(from, "learningHints");
            if (learning != null) learning.textWrappingMode = TextWrappingModes.Normal;
            if (learning != null)
            {
                var scrolls = learning.GetComponentsInParent<ScrollRect>(true);
                if (scrolls.Length == 0) MakeScroll((RectTransform)learning.transform.parent, learning);
                else if (scrolls.Length > 1)
                {
                    // Older setup passes could miss inactive parents and nest viewports.
                    // Keep the original outer panel and only remove our empty wrappers.
                    var original = scrolls[scrolls.Length - 1];
                    learning.transform.SetParent(original.viewport, false);
                    var nested = original.viewport.GetComponent<ScrollRect>();
                    if (nested != null) UnityEngine.Object.DestroyImmediate(nested);
                    for (int i = original.viewport.childCount - 1; i >= 0; i--)
                    {
                        var child = original.viewport.GetChild(i);
                        if (child != learning.transform && child.name == "Viewport") UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                    original.content = learning.rectTransform;
                    var rect = learning.rectTransform;
                    rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
                    rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
                }
            }
            if (feedback != null) EnsureScrollbar(feedback.GetComponentInParent<ScrollRect>(true));
            if (learning != null) EnsureScrollbar(learning.GetComponentInParent<ScrollRect>(true));
            var settings = new SerializedObject(flow);
            Set(settings, "client", client); Set(settings, "dialoguePanel", panel);
            Set(settings, "npcNameText", Read<TMP_Text>(from, "npcNameText"));
            Set(settings, "englishText", Read<TMP_Text>(from, "englishText"));
            Set(settings, "learningHints", learning);
            Set(settings, "statusText", status);
            Set(settings, "answerInput", input); Set(settings, "submitButton", submit);
            Set(settings, "continueButton", next); Set(settings, "feedbackText", feedback);
            Set(settings, "feedbackPanel", feedbackPanel.gameObject);
            Set(settings, "portrait", Read<Image>(from, "portraitImage"));
            settings.FindProperty("typewriterEffect").boolValue = from.FindProperty("typewriterEffect").boolValue;
            settings.FindProperty("charactersPerSecond").floatValue = from.FindProperty("charactersPerSecond").floatValue;
            settings.ApplyModifiedProperties();
            input.characterLimit = 300; input.richText = false;
            input.lineType = TMP_InputField.LineType.MultiLineSubmit;
            if (input.placeholder is TMP_Text placeholder) placeholder.text = "Nhập câu trả lời bằng tiếng Anh…";
            var oldTranslation = Read<TMP_Text>(from, "vietnameseText");
            var english = Read<TMP_Text>(from, "englishText");
            if (english != null)
            {
                english.fontSizeMax = Mathf.Max(18, english.fontSize);
                english.fontSizeMin = 16; english.enableAutoSizing = true;
                EditorUtility.SetDirty(english);
            }
            if (oldTranslation != null) oldTranslation.gameObject.SetActive(false);
            TempleDialogueSetup.MakeReadable(flow);
            if (!scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<EventSystem>(true) != null))
            {
                var events = new GameObject("RangerEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(events, scene);
                Undo.RegisterCreatedObjectUndo(events, "Add Map 2 UI input events");
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            EditorUtility.SetDirty(input); EditorUtility.SetDirty(flow);
            AssetDatabase.SaveAssetIfDirty(data);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save ranger JSON bindings.");
            File.WriteAllText(Result, "SUCCESS: " + source.id + "; " + source.dialogues.Length +
                " dialogue nodes; " + source.dialogues.Count(n => n.IsQuestion) +
                " graded questions. Existing ranger transform/frame/input preserved. UTF-8 UnityAI client and optional hidden intent branch connected.");
        }
        catch (Exception error) { File.WriteAllText(Result, "FAILED\n" + error); Debug.LogException(error); }
    }

    [MenuItem("Tools/Map 2/Validate Ranger JSON Connection")]
    public static void Validate()
    {
        try
        {
            var view = FindView(SceneManager.GetSceneByPath(ScenePath));
            if (view == null || view.GetComponent<NpcJsonDialoguePanel>() == null || view.GetComponent<UnityAiDialogueClient>() == null)
                throw new InvalidOperationException("Ranger JSON components are not connected.");
            var data = Read<DialogueData>(new SerializedObject(view), "dialogue");
            if (data == null || data.conversationJson == null || !view.AcceptsDialogue(data))
                throw new InvalidOperationException("Invalid ranger JSON data binding.");
            var settings = new SerializedObject(view.GetComponent<NpcJsonDialoguePanel>());
            foreach (string field in new[] { "client", "dialoguePanel", "englishText", "answerInput", "submitButton", "continueButton", "feedbackPanel", "feedbackText", "learningHints" })
                if (settings.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing UI binding: " + field);
            foreach (string field in new[] { "feedbackText", "learningHints" })
            {
                var text = Read<TMP_Text>(settings, field);
                var scrolls = text.GetComponentsInParent<ScrollRect>(true);
                if (scrolls.Length != 1 || scrolls[0].verticalScrollbar == null)
                    throw new InvalidOperationException("Expected one scroll view and scrollbar for " + field);
            }
            File.WriteAllText("Library/ForestRangerJsonValidation.result", "SUCCESS: ranger JSON and all UI bindings are valid.");
        }
        catch (Exception error) { File.WriteAllText("Library/ForestRangerJsonValidation.result", "FAILED\n" + error); Debug.LogException(error); }
    }

    private static NPCIntroDialogue FindView(Scene scene) => !scene.isLoaded ? null : scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<NPCIntroDialogue>(true)).FirstOrDefault(v => v.name == "ForestRangerDialogueCanvas");
    private static T Read<T>(SerializedObject settings, string name) where T : UnityEngine.Object => settings.FindProperty(name).objectReferenceValue as T;
    private static void Set(SerializedObject settings, string name, UnityEngine.Object value) => settings.FindProperty(name).objectReferenceValue = value;
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        return rect;
    }
    private static TMP_Text Text(string name, Transform parent, TMP_Text template, string value)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = template.font; text.fontSharedMaterial = template.fontSharedMaterial;
        text.fontSize = 22; text.color = new Color(.92f, .94f, .86f);
        text.richText = false; text.alignment = TextAlignmentOptions.TopLeft; text.text = value;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max; }
    private static void MakeScroll(RectTransform panel, TMP_Text text)
    {
        var viewport = Rect("Viewport", panel); Stretch(viewport, new Vector2(14, 12), new Vector2(-14, -12));
        viewport.gameObject.AddComponent<RectMask2D>();
        text.transform.SetParent(viewport, false);
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
        text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Overflow; text.richText = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        var fitter = text.GetComponent<ContentSizeFitter>() ?? text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = panel.GetComponent<ScrollRect>() ?? panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = rect; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
    }

    private static void EnsureScrollbar(ScrollRect scroll)
    {
        if (scroll == null || scroll.verticalScrollbar != null) return;
        var track = Rect("VerticalScrollbar", scroll.transform);
        track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one; track.pivot = Vector2.right;
        track.offsetMin = new Vector2(-12, 10); track.offsetMax = new Vector2(-3, -10);
        track.gameObject.AddComponent<Image>().color = new Color(.25f, .34f, .25f, .35f);
        var handle = Rect("Handle", track); Stretch(handle, Vector2.zero, Vector2.zero);
        var graphic = handle.gameObject.AddComponent<Image>(); graphic.color = new Color(.68f, .75f, .55f, .9f);
        var bar = track.gameObject.AddComponent<Scrollbar>(); bar.targetGraphic = graphic; bar.handleRect = handle;
        bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.viewport.offsetMax += new Vector2(-10, 0);
    }

    // Render only an isolated UI copy, so preview does not change the user's scene or enter Play Mode.
    [MenuItem("Tools/Map 2/Preview Ranger JSON UI")]
    public static void Preview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject copy = null; RenderTexture texture = null; Camera camera = null;
        float scale = Time.timeScale;
        try
        {
            var source = FindView(SceneManager.GetSceneByPath(ScenePath));
            if (source == null) throw new InvalidOperationException("Ranger scene is not loaded.");
            copy = UnityEngine.Object.Instantiate(source.gameObject);
            SceneManager.MoveGameObjectToScene(copy, preview);
            foreach (var child in copy.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("UI");
            var cameraObject = new GameObject("Ranger UI preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            camera = cameraObject.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f, .15f, .12f);
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            texture = new RenderTexture(1920, 1080, 24); camera.targetTexture = texture;
            var canvas = copy.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var view = copy.GetComponent<NPCIntroDialogue>();
            var data = Read<DialogueData>(new SerializedObject(view), "dialogue");
            var panel = copy.GetComponent<NpcJsonDialoguePanel>();
            var settings = new SerializedObject(panel); settings.FindProperty("typewriterEffect").boolValue = false; settings.ApplyModifiedPropertiesWithoutUndo();
            if (!view.TryShowDialogue(data, null)) throw new InvalidOperationException("Could not preview JSON dialogue.");
            view.Advance(); // show the first question after the greeting
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var old = RenderTexture.active; RenderTexture.active = texture;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            File.WriteAllBytes("Library/RangerJsonDialoguePreview.png", image.EncodeToPNG());
            RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
            view.Close();
            File.WriteAllText("Library/RangerJsonPreview.result", "SUCCESS: isolated JSON question UI rendered to Library/RangerJsonDialoguePreview.png");
        }
        catch (Exception error) { File.WriteAllText("Library/RangerJsonPreview.result", "FAILED\n" + error); Debug.LogException(error); }
        finally
        {
            if (copy != null) { copy.GetComponent<NPCIntroDialogue>()?.Close(); UnityEngine.Object.DestroyImmediate(copy); }
            if (camera != null) camera.targetTexture = null;
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            Time.timeScale = scale; EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}

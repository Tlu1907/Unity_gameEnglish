using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor.TestTools.TestRunner.Api;

/// <summary>Connects the temple to a separate copy of the authored ranger exercise UI.</summary>
[InitializeOnLoad]
public static class TempleDialogueSetup
{
    private const string Request = "Library/TempleDialogueSetup.request";
    private const string Result = "Library/TempleDialogueSetup.result";
    private const string ScenePath = "Assets/_Project/Scenes/Scence_2/Scence2.unity";
    static TempleDialogueSetup() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        if (action == "tests") RunTests();
        else if (action == "preview") Preview();
        else if (action == "fonts") EnlargeDialogueText();
        else Connect();
    }
    [MenuItem("Tools/Map 2/Connect Temple Exercises")]
    public static void Connect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var roots = scene.GetRootGameObjects();
            var temple = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "tower_2");
            var ranger = roots.SelectMany(root => root.GetComponentsInChildren<NpcJsonDialoguePanel>(true))
                .FirstOrDefault(panel => panel.name == "ForestRangerDialogueCanvas");
            if (temple == null || ranger == null) throw new InvalidOperationException("Missing tower_2 temple or ranger JSON UI.");
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Questions/Map2/map2_ancient_temple_dialogue_en.json");
            if (source == null || !NpcJsonDialogue.TryParse(source.text, out var parsed) || parsed.exercises?.Length != 4)
                throw new InvalidOperationException("Temple JSON did not load with four exercises.");
            Directory.CreateDirectory("Library/TempleBackups");
            EditorSceneManager.SaveScene(scene, "Library/TempleBackups/BeforeTemple_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity", true);
            const string dataPath = "Assets/_Project/Data/Dialogues/TempleGuardianDialogue.asset";
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(dataPath);
            if (data == null) { data = ScriptableObject.CreateInstance<DialogueData>(); AssetDatabase.CreateAsset(data, dataPath); }
            Undo.RecordObject(data, "Connect temple dialogue");
            data.speakerName = parsed.speaker; data.conversationJson = source; data.portrait = null;
            EditorUtility.SetDirty(data);
            var flow = roots.SelectMany(root => root.GetComponentsInChildren<NpcJsonDialoguePanel>(true))
                .FirstOrDefault(panel => panel.name == "TempleDialogueCanvas");
            if (flow == null)
            {
                flow = UnityEngine.Object.Instantiate(ranger);
                SceneManager.MoveGameObjectToScene(flow.gameObject, scene);
                flow.name = "TempleDialogueCanvas";
                Undo.RegisterCreatedObjectUndo(flow.gameObject, "Create temple exercise UI");
            }
            var legacy = flow.GetComponent<NPCIntroDialogue>();
            if (legacy != null) legacy.enabled = false;
            flow.SetJournalMap("map2", "Map 2 · Forest");
            var settings = new SerializedObject(flow);
            var panelRoot = (GameObject)settings.FindProperty("dialoguePanel").objectReferenceValue;
            var submit = (Button)settings.FindProperty("submitButton").objectReferenceValue;
            var next = (Button)settings.FindProperty("continueButton").objectReferenceValue;
            Wire(submit, flow.SubmitAnswer);
            var close = panelRoot.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "CloseButton");
            if (close != null) Wire(close, flow.Close);
            var reference = panelRoot.transform.Find("TempleReferenceButton")?.GetComponent<Button>();
            if (reference == null)
            {
                reference = UnityEngine.Object.Instantiate(next, next.transform.parent);
                reference.name = "TempleReferenceButton";
                var rect = (RectTransform)reference.transform;
                rect.anchoredPosition -= new Vector2(215, 0);
                rect.sizeDelta = new Vector2(200, Mathf.Max(42, rect.sizeDelta.y));
                var label = reference.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "Bài tham khảo";
            }
            // Bound by the flow lifecycle; no inherited persistent callback from the Next template.
            while (reference.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(reference.onClick, 0);
            settings.FindProperty("referenceButton").objectReferenceValue = reference;
            settings.ApplyModifiedPropertiesWithoutUndo();
            reference.gameObject.SetActive(false);
            var portrait = settings.FindProperty("portrait").objectReferenceValue as Image;
            if (portrait != null) portrait.gameObject.SetActive(false);
            panelRoot.SetActive(false);
            flow.gameObject.SetActive(true);
            MakeReadable(flow);
            EditorUtility.SetDirty(flow);

            var entrance = temple.Find("TempleTalkRange");
            if (entrance == null)
            {
                entrance = new GameObject("TempleTalkRange").transform;
                entrance.SetParent(temple, false);
                Undo.RegisterCreatedObjectUndo(entrance.gameObject, "Add temple interaction");
            }
            var solid = temple.GetComponent<Collider2D>();
            if (solid == null) throw new InvalidOperationException("Temple solid collider is missing.");
            entrance.position = new Vector3(solid.bounds.center.x, solid.bounds.min.y - .25f, temple.position.z);
            var trigger = entrance.GetComponent<TempleDialogueTrigger>() ?? Undo.AddComponent<TempleDialogueTrigger>(entrance.gameObject);
            trigger.dialogue = data; trigger.panel = flow;
            trigger.minimap = roots.SelectMany(root => root.GetComponentsInChildren<SchematicMinimap>(true)).FirstOrDefault();
            EditorUtility.SetDirty(trigger);
            var marker = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Gameplay/Items/Map2/Prefab/Props/StoneMarkers/StoneMarker_Carved.prefab");
            for (int i = 0; i < 3 && marker != null; i++)
            {
                if (temple.Find("TempleStone" + (i + 1)) != null) continue;
                var stone = (GameObject)PrefabUtility.InstantiatePrefab(marker, scene);
                stone.name = "TempleStone" + (i + 1);
                stone.transform.SetParent(temple, true);
                stone.transform.position = entrance.position + new Vector3((i - 1) * .65f, .65f, 0);
                foreach (var collider in stone.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
                var sprite = stone.GetComponentInChildren<SpriteRenderer>();
                if (sprite != null)
                {
                    sprite.color = new Color(.6f, .82f, 1f);
                    var glow = new GameObject("BlueLight", typeof(SpriteRenderer));
                    glow.transform.SetParent(sprite.transform, false); glow.transform.localScale = Vector3.one * 1.3f;
                    var light = glow.GetComponent<SpriteRenderer>(); light.sprite = sprite.sprite;
                    light.color = new Color(.12f, .5f, 1f, .45f); light.sortingLayerID = sprite.sortingLayerID;
                    light.sortingOrder = sprite.sortingOrder - 1;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText(Result, "SUCCESS: temple entrance + separate exercise UI, four exercises, three blue-lit stones, reference/evidence review connected.");
        }
        catch (Exception error) { File.WriteAllText(Result, "FAILED\n" + error); Debug.LogException(error); }
    }
    private static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }
    [MenuItem("Tools/Map 2/Enlarge Dialogue And Exercise Text")]
    public static void EnlargeDialogueText()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var flow in root.GetComponentsInChildren<NpcJsonDialoguePanel>(true))
            {
                Undo.RegisterFullObjectHierarchyUndo(flow.gameObject, "Enlarge exercise text");
                MakeReadable(flow); count++;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { File.WriteAllText(Request, "fonts"); return; }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Library/DialogueTypography.result", "SUCCESS: " + count + " panels. Question 38, notes/feedback 30, answer 34, buttons 28. Long questions scroll instead of shrinking.");
        }
        catch (Exception error) { File.WriteAllText("Library/DialogueTypography.result", "FAILED\n" + error); }
    }
    public static void MakeReadable(NpcJsonDialoguePanel flow)
    {
        flow.ApplyReadableTypography();
        var settings = new SerializedObject(flow);
        var text = settings.FindProperty("englishText").objectReferenceValue as TMP_Text;
        if (text == null) return;
        var status = settings.FindProperty("statusText").objectReferenceValue as TMP_Text;
        if (status != null)
            status.rectTransform.sizeDelta = new Vector2(status.rectTransform.sizeDelta.x, 64);
        if (text.GetComponentInParent<ScrollRect>(true) == null)
        {
            var rect = text.rectTransform;
            var wrapper = new GameObject("QuestionScroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            wrapper.layer = text.gameObject.layer;
            var viewport = (RectTransform)wrapper.transform;
            viewport.SetParent(rect.parent, false);
            viewport.SetSiblingIndex(rect.GetSiblingIndex());
            viewport.anchorMin = rect.anchorMin; viewport.anchorMax = rect.anchorMax;
            viewport.pivot = rect.pivot; viewport.sizeDelta = rect.sizeDelta;
            viewport.anchoredPosition = rect.anchoredPosition; viewport.localScale = rect.localScale;
            rect.SetParent(viewport, false);
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            var fitter = text.GetComponent<ContentSizeFitter>() ?? text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = wrapper.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = rect;
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 40;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            text.raycastTarget = true;
        }
        var questionScroll = text.GetComponentInParent<ScrollRect>(true);
        var questionArea = (RectTransform)questionScroll.transform;
        questionArea.anchorMin = new Vector2(.025f, .68f);
        questionArea.anchorMax = new Vector2(.50f, .94f);
        questionArea.offsetMin = questionArea.offsetMax = Vector2.zero;
        questionArea.localScale = Vector3.one;
        text.overflowMode = TextOverflowModes.Overflow;
        foreach (var item in flow.GetComponentsInChildren<TMP_Text>(true)) EditorUtility.SetDirty(item);
        EditorUtility.SetDirty(flow);
    }
    private static TestRunnerApi runner;
    [MenuItem("Tools/Map 2/Run Temple Exercise Tests")]
    public static void RunTests()
    {
        if (runner != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new Results());
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "TempleExerciseTests", "NpcJsonDialogueTests" } }));
    }
    [MenuItem("Tools/Map 2/Preview Temple Exercises")]
    public static void Preview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var source = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "TempleDialogueCanvas");
        if (source == null) return;
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject copy = null, cameraObject = null;
        RenderTexture texture = null;
        float scale = Time.timeScale;
        try
        {
            copy = UnityEngine.Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(copy, preview);
            foreach (var child in copy.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("UI");
            var flow = copy.GetComponent<NpcJsonDialoguePanel>();
            var settings = new SerializedObject(flow);
            settings.FindProperty("typewriterEffect").boolValue = false; settings.ApplyModifiedPropertiesWithoutUndo();
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/_Project/Data/Dialogues/TempleGuardianDialogue.asset");
            if (!flow.TryOpen(data, null)) throw new InvalidOperationException("Temple UI could not open.");
            flow.Advance(); flow.Advance();
            cameraObject = new GameObject("Temple UI preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .15f, .12f);
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            texture = new RenderTexture(1920, 1080, 24); camera.targetTexture = texture;
            var canvas = copy.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var old = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            File.WriteAllBytes("Library/TempleDialoguePreview.png", image.EncodeToPNG());
            RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
            flow.Close();
            File.WriteAllText("Library/TempleDialoguePreview.result", "SUCCESS");
        }
        catch (Exception error) { File.WriteAllText("Library/TempleDialoguePreview.result", "FAILED\n" + error); }
        finally
        {
            if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            Time.timeScale = scale; EditorSceneManager.ClosePreviewScene(preview);
        }
    }
    private sealed class Results : ICallbacks
    {
        private readonly System.Text.StringBuilder details = new System.Text.StringBuilder();
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        { if (result.FailCount > 0) details.AppendLine(result.Name).AppendLine(result.Message).AppendLine(result.StackTrace); }
        public void RunFinished(ITestResultAdaptor result)
        {
            File.WriteAllText("Library/TempleDialogueTests.result", "passed=" + result.PassCount + "; failed=" + result.FailCount + "\n" + details);
            var finished = runner; runner = null;
            EditorApplication.delayCall += () => UnityEngine.Object.DestroyImmediate(finished);
        }
    }
}

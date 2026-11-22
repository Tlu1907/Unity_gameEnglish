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

/// <summary>Authors the Map 2 ranger and editable dialogue once, outside Play Mode.</summary>
[InitializeOnLoad]
public static class ForestRangerSetup
{
    private const string Request = "Library/ForestRangerSetup.request";
    private const string Result = "Library/ForestRangerSetup.result";
    private const string ScenePath = "Assets/_Project/Scenes/Scence_2/Scence2.unity";
    public const string DataPath = "Assets/_Project/Data/Dialogues/ForestRangerDialogue.asset";
    private const string PrefabPath = "Assets/_Project/Gameplay/Characters/NPC/Prefabs/ForestRanger.prefab";
    static ForestRangerSetup() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        if (action == "validate") Validate();
        else if (action == "translation") UpgradeTranslation();
        else if (action == "submit-below") MoveSubmitBelow();
        else Build();
    }

    [MenuItem("Tools/Map 2/Move Ranger Submit Below Input")]
    public static void MoveSubmitBelow()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NPCIntroDialogue>(true))
                .First(v => v.name == "ForestRangerDialogueCanvas");
            var fields = new SerializedObject(view);
            var input = ((TMP_InputField)fields.FindProperty("answerInput").objectReferenceValue).GetComponent<RectTransform>();
            var button = ((Button)fields.FindProperty("submitAnswerButton").objectReferenceValue).GetComponent<RectTransform>();
            Undo.RecordObject(button, "Move Submit below answer input");
            Undo.SetTransformParent(button, input, "Anchor Submit to answer input");
            button.anchorMin = button.anchorMax = new Vector2(1, 0);
            button.pivot = Vector2.one;
            button.anchoredPosition = new Vector2(0, -12);
            button.localRotation = Quaternion.identity;
            button.localScale = Vector3.one;
            EditorUtility.SetDirty(button);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save button position.");
            File.WriteAllText("Library/RangerSubmitBelow.result", "SUCCESS: Submit anchored 12 units below input, aligned right; input and other layout retained.");
        }
        catch (Exception error) { File.WriteAllText("Library/RangerSubmitBelow.result", "FAILED\n" + error); Debug.LogException(error); }
    }

    [MenuItem("Tools/Map 2/Add Ranger Translation Answer")]
    public static void UpgradeTranslation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NPCIntroDialogue>(true))
                .First(v => v.name == "ForestRangerDialogueCanvas");
            var fields = new SerializedObject(view);
            var panel = ((GameObject)fields.FindProperty("dialoguePanel").objectReferenceValue).GetComponent<RectTransform>();
            if (panel.Find("AnswerRow") != null)
            { File.WriteAllText("Library/RangerTranslation.result", "ALREADY EXISTS; layout preserved."); return; }
            // Work on the loaded scene so unsaved user layout edits remain authoritative.
            Directory.CreateDirectory("Library/RangerBackups");
            EditorSceneManager.SaveScene(scene, "Library/RangerBackups/BeforeTranslation_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Add ranger translation controls");
            var oldTranslation = fields.FindProperty("vietnameseText").objectReferenceValue as TMP_Text;
            var sourceRect = oldTranslation != null ? oldTranslation.rectTransform : null;
            var row = Rect("AnswerRow", panel, new Vector2(0, 1), new Vector2(265, -190), new Vector2(980, 65));
            row.pivot = new Vector2(0, .5f);
            if (sourceRect != null)
            {
                row.anchorMin = sourceRect.anchorMin; row.anchorMax = sourceRect.anchorMax;
                row.pivot = sourceRect.pivot; row.anchoredPosition = sourceRect.anchoredPosition;
                row.sizeDelta = sourceRect.sizeDelta; row.localScale = sourceRect.localScale;
                oldTranslation.gameObject.SetActive(false);
            }
            var inputRect = Rect("AnswerInput", row, Vector2.zero, Vector2.zero, Vector2.zero);
            inputRect.anchorMin = Vector2.zero; inputRect.anchorMax = Vector2.one;
            inputRect.offsetMin = Vector2.zero; inputRect.offsetMax = new Vector2(-130, 0);
            var background = inputRect.gameObject.AddComponent<Image>(); background.color = new Color(.92f, .94f, .85f);
            var input = inputRect.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = background;
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.characterLimit = 2000; input.richText = false;
            var viewport = Rect("Viewport", inputRect, Vector2.zero, Vector2.zero, Vector2.zero);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(14, 6); viewport.offsetMax = new Vector2(-14, -6);
            viewport.gameObject.AddComponent<RectMask2D>(); input.textViewport = viewport;
            var value = Label("Text", viewport, Vector2.zero, Vector2.zero, Vector2.zero, "", 24);
            var placeholder = Label("Placeholder", viewport, Vector2.zero, Vector2.zero, Vector2.zero, "Nhập bản dịch của bạn…", 24);
            foreach (var text in new[] { value, placeholder })
            {
                text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                text.alignment = TextAlignmentOptions.TopLeft; text.richText = false;
            }
            value.color = new Color(.08f, .14f, .1f); placeholder.color = new Color(.35f, .42f, .34f);
            input.textComponent = value; input.placeholder = placeholder; input.fontAsset = value.font;
            var submitRect = Rect("SubmitAnswerButton", row, new Vector2(1, .5f), new Vector2(-58, 0), new Vector2(116, 54));
            var submitImage = submitRect.gameObject.AddComponent<Image>(); submitImage.color = new Color(.28f, .41f, .23f);
            var submit = submitRect.gameObject.AddComponent<Button>(); submit.targetGraphic = submitImage;
            var submitLabel = Label("Label", submitRect, Vector2.one * .5f, Vector2.zero, new Vector2(116, 54), "Gửi", 26);
            submitLabel.rectTransform.pivot = Vector2.one * .5f; submitLabel.alignment = TextAlignmentOptions.Center;
            var hintsPanel = Rect("LearningHintsPanel", panel, Vector2.one, new Vector2(0, 18), new Vector2(420, 240));
            hintsPanel.pivot = new Vector2(1, 0);
            hintsPanel.gameObject.AddComponent<Image>().color = new Color(.045f, .095f, .075f, .98f);
            var hints = Label("VocabularyAndGrammar", hintsPanel, Vector2.zero, Vector2.zero, Vector2.zero, "TỪ VỰNG\n\nNGỮ PHÁP", 24);
            hints.rectTransform.anchorMin = Vector2.zero; hints.rectTransform.anchorMax = Vector2.one;
            hints.rectTransform.offsetMin = new Vector2(18, 14); hints.rectTransform.offsetMax = new Vector2(-18, -14);
            hints.alignment = TextAlignmentOptions.TopLeft; hints.enableAutoSizing = true;
            hints.fontSizeMin = 16; hints.fontSizeMax = 24;
            var status = fields.FindProperty("continueText").objectReferenceValue as TMP_Text;
            if (status != null) status.text = "Tự dịch câu thoại rồi bấm Gửi.";
            fields.FindProperty("playerTranslation").boolValue = true;
            Set(fields, "learningHints", hints); Set(fields, "answerInput", input);
            Set(fields, "submitAnswerButton", submit); Set(fields, "answerStatus", status);
            fields.ApplyModifiedProperties();
            // Keep authored dialogue text; new hint fields are edited separately in the data asset.
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save translation UI.");
            File.WriteAllText("Library/RangerTranslation.result", "SUCCESS: original layout retained; translation hidden; answer input, Submit button and vocabulary/grammar panel saved. No grading service attached.");
        }
        catch (Exception error)
        { File.WriteAllText("Library/RangerTranslation.result", "FAILED\n" + error); Debug.LogException(error); }
    }

    [MenuItem("Tools/Map 2/Validate Forest Ranger Dialogue")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        NPCIntroDialogue view = null;
        float previousScale = Time.timeScale;
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var npc = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == "ForestRanger");
            var trigger = npc.GetComponentInChildren<NPCDialogueTrigger>();
            var settings = new SerializedObject(trigger);
            view = settings.FindProperty("dialogueUI").objectReferenceValue as NPCIntroDialogue;
            var data = settings.FindProperty("dialogueData").objectReferenceValue as DialogueData;
            if (view == null || !view.AcceptsDialogue(data)) throw new InvalidOperationException("Invalid ranger dialogue references.");
            var viewSettings = new SerializedObject(view);
            bool translation = viewSettings.FindProperty("playerTranslation").boolValue;
            if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NPCInteractor>(true)).Count() != 1)
                throw new InvalidOperationException("Expected one Player NPCInteractor for E interaction.");
            for (int i = 0; i < 2; i++)
            {
                if (!trigger.TryTalk() || !view.IsOpen || GameplayState.Current != GameplayMode.Dialogue)
                    throw new InvalidOperationException("Ranger dialogue did not open.");
                if (translation)
                {
                    var input = viewSettings.FindProperty("answerInput").objectReferenceValue as TMP_InputField;
                    var button = viewSettings.FindProperty("submitAnswerButton").objectReferenceValue as Button;
                    var oldVietnamese = viewSettings.FindProperty("vietnameseText").objectReferenceValue as TMP_Text;
                    if (input == null || button == null || (oldVietnamese != null && oldVietnamese.gameObject.activeSelf))
                        throw new InvalidOperationException("Missing answer controls or translation still visible.");
                    input.text = "   "; view.SubmitAnswer();
                    if (view.LastSubmittedAnswer != null) throw new InvalidOperationException("Blank answer was accepted.");
                    input.text = "  Câu trả lời của người chơi  "; view.SubmitAnswer();
                    if (view.LastSubmittedAnswer != "Câu trả lời của người chơi" || !view.IsOpen || button.interactable)
                        throw new InvalidOperationException("Answer submission did not retain the answer and keep dialogue open.");
                    view.Close();
                }
                else
                {
                view.Advance();
                if (view.IsOpen) view.Advance();
                }
                if (view.IsOpen || !GameplayState.CanAct || Time.timeScale != previousScale)
                    throw new InvalidOperationException("Ranger dialogue did not restore gameplay.");
            }
            File.WriteAllText("Library/ForestRangerValidation.result", "SUCCESS: scene UI references and E interactor; two open/close cycles restore gameplay and timeScale. Translation mode: " + translation + "; blank rejected, answer retained, Vietnamese hidden, submit leaves dialogue open.");
        }
        catch (Exception error)
        { File.WriteAllText("Library/ForestRangerValidation.result", "FAILED\n" + error); Debug.LogException(error); }
        finally { if (view != null && view.IsOpen) view.Close(); Time.timeScale = previousScale; }
    }

    [MenuItem("Tools/Map 2/Create Forest Ranger and Dialogue")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ForestRanger")))
            { File.WriteAllText(Result, "ALREADY EXISTS: ForestRanger. Existing content preserved."); return; }
            var tent = roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                .FirstOrDefault(r => r.name == "Tent_Entrance");
            if (tent == null) throw new InvalidOperationException("Tent_Entrance was not found in Scence2.");
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/NPC/LexiResidents.png")
                .OfType<Sprite>().First(s => s.name == "Ti_Idle_0");
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DialogueData>();
                data.speakerName = "Kiểm lâm";
                data.portrait = sprite;
                data.continuePrompt = "Nhấn C để tiếp tục";
                data.closePrompt = "Nhấn C hoặc Đóng để kết thúc";
                data.lines = new[] { new DialogueData.Line {
                    english = "[Dialogue content will be added later.]",
                    vietnamese = "[Nội dung hội thoại sẽ được bổ sung sau.]" } };
                AssetDatabase.CreateAsset(data, DataPath);
            }

            var canvasObject = new GameObject("ForestRangerDialogueCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(NPCIntroDialogue));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create ranger dialogue");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var view = canvasObject.GetComponent<NPCIntroDialogue>();
            var panel = Rect("DialoguePanel", canvasObject.transform, new Vector2(.5f, 0),
                new Vector2(0, 48), new Vector2(1320, 310));
            panel.pivot = new Vector2(.5f, 0);
            panel.gameObject.AddComponent<Image>().color = new Color(.045f, .095f, .075f, .98f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.61f, .68f, .39f);
            outline.effectDistance = new Vector2(2, -2);
            var portraitFrame = Rect("PortraitFrame", panel, new Vector2(0, .5f), new Vector2(125, 0), new Vector2(210, 260));
            portraitFrame.gameObject.AddComponent<Image>().color = new Color(.13f, .22f, .16f);
            var portrait = Rect("Portrait", portraitFrame, Vector2.one * .5f, Vector2.zero, new Vector2(190, 240))
                .gameObject.AddComponent<Image>();
            portrait.sprite = sprite; portrait.preserveAspect = true; portrait.raycastTarget = false;
            var name = Label("SpeakerName", panel, new Vector2(0, 1), new Vector2(265, -38), new Vector2(730, 45), "Kiểm lâm", 32);
            name.fontStyle = FontStyles.Bold; name.color = new Color(.87f, .82f, .54f);
            var english = Label("EnglishText", panel, new Vector2(0, 1), new Vector2(265, -110), new Vector2(980, 65), data.lines[0].english, 27);
            var vietnamese = Label("VietnameseText", panel, new Vector2(0, 1), new Vector2(265, -190), new Vector2(980, 65), data.lines[0].vietnamese, 27);
            var hint = Label("ContinueHint", panel, Vector2.zero, new Vector2(265, 35), new Vector2(760, 35), data.closePrompt, 22);
            hint.color = new Color(.69f, .76f, .67f);
            var closeRect = Rect("CloseButton", panel, new Vector2(1, 1), new Vector2(-80, -37), new Vector2(110, 42));
            closeRect.gameObject.AddComponent<Image>().color = new Color(.24f, .33f, .22f);
            var button = closeRect.gameObject.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(button.onClick, view.Close);
            var closeLabel = Label("Label", closeRect, Vector2.one * .5f, Vector2.zero, new Vector2(110, 42), "Đóng", 24);
            closeLabel.rectTransform.pivot = Vector2.one * .5f;
            closeLabel.alignment = TextAlignmentOptions.Center;
            var fields = new SerializedObject(view);
            Set(fields, "dialoguePanel", panel.gameObject); Set(fields, "npcNameText", name);
            Set(fields, "englishText", english); Set(fields, "vietnameseText", vietnamese);
            Set(fields, "continueText", hint); Set(fields, "portraitImage", portrait); Set(fields, "dialogue", data);
            fields.FindProperty("playOnStart").boolValue = false;
            fields.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);

            var npc = new GameObject("ForestRanger");
            SceneManager.MoveGameObjectToScene(npc, scene);
            Undo.RegisterCreatedObjectUndo(npc, "Create forest ranger");
            var npcParent = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "NPCs");
            if (npcParent != null) npc.transform.SetParent(npcParent, false);
            var body = npc.AddComponent<SpriteRenderer>();
            body.sprite = sprite;
            body.sortingLayerName = "WorldDepth";
            float scale = 1.65f / sprite.bounds.size.y;
            npc.transform.localScale = Vector3.one * scale;
            var feet = npc.AddComponent<BoxCollider2D>();
            feet.size = new Vector2(.52f / scale, .24f / scale);
            feet.offset = new Vector2(0, .12f / scale);
            int obstacle = LayerMask.NameToLayer("Obstacle");
            if (obstacle >= 0) npc.layer = obstacle;
            // Stand beside the entrance, clear of the tent footprint and approach path.
            npc.transform.position = new Vector3(tent.bounds.max.x + .8f, tent.bounds.min.y - .5f, 0);
            var depth = npc.AddComponent<WorldDepthSort>();
            depth.target = body; depth.feetCollider = feet; depth.Apply();
            var idle = npc.AddComponent<Animator>();
            idle.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Project/Gameplay/Characters/NPC/Animations/Ti.controller");
            var range = new GameObject("TalkRange"); range.transform.SetParent(npc.transform, false);
            var trigger = range.AddComponent<NPCDialogueTrigger>();
            var triggerFields = new SerializedObject(trigger);
            Set(triggerFields, "dialogueUI", view); Set(triggerFields, "dialogueData", data);
            triggerFields.FindProperty("onlyOnce").boolValue = false;
            triggerFields.ApplyModifiedPropertiesWithoutUndo();
            // Save a reusable NPC prefab; scene UI references remain on the scene instance.
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(npc, PrefabPath, InteractionMode.AutomatedAction);
            if (prefab == null) throw new InvalidOperationException("Could not create ranger prefab.");
            trigger = npc.GetComponentInChildren<NPCDialogueTrigger>();
            triggerFields = new SerializedObject(trigger); Set(triggerFields, "dialogueUI", view);
            triggerFields.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Scence2.");
            Selection.activeGameObject = npc;
            File.WriteAllText(Result, "SUCCESS\nNPC: " + npc.transform.position + "\nDialogue: " + DataPath +
                "\nPrefab: " + PrefabPath + "\nE opens via NPCInteractor; C advances; Close button closes; repeat conversations enabled.");
        }
        catch (Exception error) { File.WriteAllText(Result, "FAILED\n" + error); Debug.LogException(error); }
    }

    private static void Set(SerializedObject settings, string name, UnityEngine.Object value)
        => settings.FindProperty(name).objectReferenceValue = value;
    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    private static TMP_Text Label(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string value, float fontSize)
    {
        var rect = Rect(name, parent, anchor, position, size); rect.pivot = new Vector2(0, .5f);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Art/HUD/HUD_Font.asset");
        text.text = value; text.fontSize = fontSize; text.color = new Color(.92f, .94f, .86f);
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        return text;
    }
}

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Copy Linh's authored frame once; runtime dialogue keeps designer edits.</summary>
public static class NpcDialogueLayoutSetup
{
    [MenuItem("Tools/Story/Apply Linh Layout To Story Dialogue")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var preview = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sence_1/Scene1.unity");
        GameObject contents = null;
        try
        {
            NPCIntroDialogue source = null;
            foreach (var root in preview.GetRootGameObjects())
            {
                source = root.GetComponentInChildren<NPCIntroDialogue>(true);
                if (source != null) break;
            }
            if (source == null) throw new System.InvalidOperationException("Linh dialogue frame is missing.");
            contents = PrefabUtility.LoadPrefabContents(StoryDialogueSetup.PrefabPath);
            StoryQuizPanel target = contents.GetComponentInChildren<StoryQuizPanel>(true);
            var from = new SerializedObject(source);
            var to = new SerializedObject(target);
            GameObject frame = (GameObject)from.FindProperty("dialoguePanel").objectReferenceValue;
            CopyRect((RectTransform)frame.transform, (RectTransform)target.transform);
            CopyImage(frame.GetComponent<Image>(), target.GetComponent<Image>(), true);
            foreach (var pair in new[] { new[] { "npcNameText", "nameText" }, new[] { "englishText", "englishText" },
                new[] { "vietnameseText", "vietnameseText" }, new[] { "continueText", "promptText" } })
            {
                var a = (TMP_Text)from.FindProperty(pair[0]).objectReferenceValue;
                var b = (TMP_Text)to.FindProperty(pair[1]).objectReferenceValue;
                if (a == null || b == null) continue;
                CopyRect(a.rectTransform, b.rectTransform);
                b.font = a.font; b.fontSharedMaterial = a.fontSharedMaterial;
                b.color = a.color; b.fontSize = a.fontSize; b.fontStyle = a.fontStyle;
                b.alignment = a.alignment; b.margin = a.margin; b.lineSpacing = a.lineSpacing;
                b.enableAutoSizing = true; b.fontSizeMin = 14; b.fontSizeMax = a.fontSize;
                b.richText = false;
            }
            var sourcePortrait = (Image)from.FindProperty("portraitImage").objectReferenceValue;
            var targetPortrait = (Image)to.FindProperty("portrait").objectReferenceValue;
            if (sourcePortrait != null && targetPortrait != null)
            {
                CopyRect(sourcePortrait.rectTransform, targetPortrait.rectTransform);
                CopyImage(sourcePortrait, targetPortrait, false);
            }
            Transform choices = target.transform.Find("Choices");
            if (choices == null)
            {
                var go = new GameObject("Choices", typeof(RectTransform));
                go.layer = target.gameObject.layer;
                go.transform.SetParent(target.transform, false);
                choices = go.transform;
            }
            var choiceRect = (RectTransform)choices;
            choiceRect.anchorMin = new Vector2(0, 1); choiceRect.anchorMax = Vector2.one;
            choiceRect.pivot = new Vector2(.5f, 0); choiceRect.anchoredPosition = new Vector2(0, 12);
            choiceRect.sizeDelta = new Vector2(0, 128);
            for (int i = 0; i < 4; i++)
            {
                var button = (Button)to.FindProperty("buttons").GetArrayElementAtIndex(i).objectReferenceValue;
                button.transform.SetParent(choices, false);
                var rect = (RectTransform)button.transform;
                int column = i % 2, row = i / 2;
                rect.anchorMin = new Vector2(.02f + column * .49f, .52f - row * .5f);
                rect.anchorMax = new Vector2(.49f + column * .49f, .98f - row * .5f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var english = (TMP_Text)to.FindProperty("englishText").objectReferenceValue;
            var vietnamese = (TMP_Text)to.FindProperty("vietnameseText").objectReferenceValue;
            var speaker = (TMP_Text)to.FindProperty("nameText").objectReferenceValue;
            var prompt = (TMP_Text)to.FindProperty("promptText").objectReferenceValue;
            var data = StoryBeatData.Load("q1_spirit");
            if (data.TryGetConversation(out var conversation))
            {
                english.text = conversation.introduction.dialogue_en;
                vietnamese.text = conversation.introduction.dialogue_vi;
                if (speaker != null) speaker.text = conversation.speaker;
            }
            if (prompt != null) prompt.text = "C to continue · Esc to close";
            target.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(contents, StoryDialogueSetup.PrefabPath);
        }
        finally
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void CopyRect(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot;
        to.sizeDelta = from.sizeDelta; to.anchoredPosition = from.anchoredPosition;
        to.localScale = from.localScale; to.localRotation = from.localRotation;
    }
    private static void CopyImage(Image from, Image to, bool copySprite)
    {
        if (from == null || to == null) return;
        if (copySprite) to.sprite = from.sprite;
        to.color = from.color; to.type = from.type; to.preserveAspect = from.preserveAspect;
    }
}

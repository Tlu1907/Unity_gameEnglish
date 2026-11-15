using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ScientistNPCSetup
{
    [MenuItem("Tools/NPC/Fix Scientist Dialogue")]
    private static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject npc = GameObject.Find("ScientistNPC");
        NPCIntroDialogue ui = Object.FindAnyObjectByType<NPCIntroDialogue>();
        DialogueData data = AssetDatabase.LoadAssetAtPath<DialogueData>(
            "Assets/_Project/Data/Dialogues/ScientistDialogue.asset");
        if (npc == null || ui == null || data == null)
        {
            Debug.LogWarning("Open the scene with ScientistNPC, an active DialogueCanvas and ScientistDialogue.asset first.");
            return;
        }

        Undo.RecordObject(data, "Configure scientist dialogue");
        if (string.IsNullOrWhiteSpace(data.speakerName)) data.speakerName = "Nhà bác học";
        if (!data.IsValid)
        {
            if (data.lines == null || data.lines.Length == 0)
                data.lines = new DialogueData.Line[2];
            for (int i = 0; i < data.lines.Length; i++)
            {
                if (data.lines[i] == null) data.lines[i] = new DialogueData.Line();
                if (string.IsNullOrWhiteSpace(data.lines[i].english))
                    data.lines[i].english = i == 0 ? "Hello! Use WASD to move around the city."
                        : "Answer questions to open chests. Equip your gear to gain 100 armor.";
                if (string.IsNullOrWhiteSpace(data.lines[i].vietnamese))
                    data.lines[i].vietnamese = i == 0 ? "Xin chào! Dùng WASD để di chuyển trong thành phố."
                        : "Trả lời câu hỏi để mở rương. Trang bị vật phẩm để nhận 100 giáp.";
            }
        }
        SpriteRenderer renderer = npc.GetComponent<SpriteRenderer>();
        if (data.portrait == null && renderer != null) data.portrait = renderer.sprite;
        EditorUtility.SetDirty(data);

        Transform range = npc.transform.Find("TalkRange");
        if (range == null)
        {
            GameObject go = new GameObject("TalkRange");
            Undo.RegisterCreatedObjectUndo(go, "Create talk range");
            go.transform.SetParent(npc.transform, false);
            range = go.transform;
        }
        CircleCollider2D trigger = range.GetComponent<CircleCollider2D>();
        if (trigger == null) trigger = Undo.AddComponent<CircleCollider2D>(range.gameObject);
        Undo.RecordObject(trigger, "Resize talk range");
        trigger.enabled = true;
        trigger.isTrigger = true;
        trigger.offset = Vector2.zero;
        float scale = Mathf.Max(.001f, Mathf.Min(Mathf.Abs(range.lossyScale.x), Mathf.Abs(range.lossyScale.y)));
        trigger.radius = 1.2f / scale;

        NPCDialogueTrigger script = range.GetComponent<NPCDialogueTrigger>();
        if (script == null) script = Undo.AddComponent<NPCDialogueTrigger>(range.gameObject);
        Undo.RecordObject(script, "Link scientist dialogue");
        var settings = new SerializedObject(script);
        settings.FindProperty("dialogueUI").objectReferenceValue = ui;
        settings.FindProperty("dialogueData").objectReferenceValue = data;
        settings.ApplyModifiedProperties();

        BoxCollider2D solid = npc.GetComponent<BoxCollider2D>();
        if (solid == null)
        {
            solid = Undo.AddComponent<BoxCollider2D>(npc);
            solid.offset = new Vector2(0, -2.5f);
            solid.size = new Vector2(2.8f, 1.4f);
        }
        Undo.RecordObject(solid, "Enable NPC collision");
        solid.enabled = true;
        solid.isTrigger = false;
        EditorSceneManager.MarkSceneDirty(npc.scene);
        Selection.activeGameObject = range.gameObject;
        Debug.Log("Scientist NPC repaired. Review the collider and dialogue, then save the scene and project.", npc);
    }
}

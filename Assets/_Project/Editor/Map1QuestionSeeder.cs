#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tạo hai QuestionData cho Màn 1: sword và shield.
/// Menu: EarthDefender/Tạo câu hỏi Màn 1
/// </summary>
public static class Map1QuestionSeeder
{
    private const string FolderPath = "Assets/_Project/Learning/Quiz/Data/Questions/Map1";

    [MenuItem("EarthDefender/Tạo câu hỏi Màn 1")]
    public static void Seed()
    {
        EnsureFolder();

        Upsert("map1_sword", "vocabulary",
            "Which word matches the picture?\n(Hình: thanh kiếm)",
            new[] { "key", "sword", "shield", "chest" }, 1,
            "sword /sɔːd/ (n.) — kiếm. Lựa chọn B là đúng.");

        Upsert("map1_shield", "vocabulary",
            "Which word matches the picture?\n(Hình: chiếc khiên)",
            new[] { "chest", "key", "shield", "sword" }, 2,
            "shield /ʃiːld/ (n.) — khiên. Lựa chọn C là đúng.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Map1] Đã tạo/cập nhật 2 câu hỏi tại " + FolderPath);
    }

    private static void Upsert(string id, string tag, string content,
        string[] answers, int correct, string explanation)
    {
        string path = $"{FolderPath}/Q_{id}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<QuestionData>(path);
        if (existing == null)
        {
            existing = ScriptableObject.CreateInstance<QuestionData>();
            AssetDatabase.CreateAsset(existing, path);
        }
        existing.id         = id;
        existing.skillTag   = tag;
        existing.content    = content;
        existing.answers    = answers;
        existing.correctIndex = correct;
        existing.explanation = explanation;
        existing.freezeDuration = 3f;
        EditorUtility.SetDirty(existing);
    }

    private static void EnsureFolder()
    {
        string cur = "Assets";
        foreach (string part in FolderPath.Substring("Assets/".Length).Split('/'))
        {
            if (!AssetDatabase.IsValidFolder(cur + "/" + part))
                AssetDatabase.CreateFolder(cur, part);
            cur += "/" + part;
        }
    }
}
#endif

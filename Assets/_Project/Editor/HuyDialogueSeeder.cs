#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tạo DialogueData asset cho NPC Huy (nhà khảo cổ ở ga).
/// Menu: EarthDefender/Tạo thoại Huy
/// </summary>
public static class HuyDialogueSeeder
{
    private const string SavePath = "Assets/_Project/Data/Dialogues/Huy_Station.asset";

    [MenuItem("EarthDefender/Tạo thoại Huy")]
    public static void Seed()
    {
        var asset = AssetDatabase.LoadAssetAtPath<DialogueData>(SavePath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DialogueData>();
            AssetDatabase.CreateAsset(asset, SavePath);
        }

        asset.speakerName   = "Huy";
        asset.continuePrompt = "Nhấn C để tiếp tục";
        asset.closePrompt   = "Nhấn C để đóng";
        asset.lines = new[]
        {
            new DialogueData.Line
            {
                english    = "The monsters came after the object fell. I saw the same mark on a stone in the forest.",
                vietnamese = "Lũ quái vật xuất hiện sau khi vật thể rơi xuống. Tôi đã thấy ký hiệu giống thế này trên một tảng đá trong rừng."
            },
            new DialogueData.Line
            {
                english    = "My team went to the forest. Please find them. Take this map. It shows the way to the North Gate.",
                vietnamese = "Đội của tôi đã đi vào rừng. Hãy tìm họ giúp tôi. Hãy lấy bản đồ này. Nó chỉ đường đến Cổng Bắc."
            }
        };

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Map1] Đã tạo thoại Huy tại " + SavePath);
    }
}
#endif

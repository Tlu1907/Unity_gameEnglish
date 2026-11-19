#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PlayerAnimationRepairMenu
{
    [MenuItem("Tools/Player/Reimport Animation Assets")]
    private static void ReimportAnimations()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode and disable Animation Preview before reimporting Player animations.");
            return;
        }
        const string root = "Assets/_Project/Gameplay/Characters/Player/Frame";
        // Import dependencies first, then clips, then their controllers.
        foreach (string direction in new[] { "down", "left", "right", "up" })
            AssetDatabase.ImportAsset(root + "/attack/" + direction + ".png", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".anim")) continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            count++;
        }
        AssetDatabase.ImportAsset(root + "/Player_NoItem/Animation/Visual.controller", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(root + "/Player_Item/Animation/Player_Equipped.overrideController", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"Reimported 4 attack sprite sheets, {count} Player clips and both controllers. Clear Console, then test attack again. If the assertion persists, save your scene and restart Unity.");
    }
}
#endif

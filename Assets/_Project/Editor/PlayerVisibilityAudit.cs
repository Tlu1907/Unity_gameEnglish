using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayerVisibilityAudit
{
    static PlayerVisibilityAudit() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (!File.Exists("Library/PlayerVisibilityAudit.request") || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try { File.Delete("Library/PlayerVisibilityAudit.request"); }
        catch (IOException) { return; }
        var text = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        text.AppendLine("Scene: " + scene.path + "; playing=" + EditorApplication.isPlaying);
        text.AppendLine("Layers: " + string.Join(", ", SortingLayer.layers.Select(layer => layer.name + "=" + layer.id)));
        foreach (var root in scene.GetRootGameObjects())
        foreach (var player in root.GetComponentsInChildren<PlayerMovement>(true))
        {
            text.AppendLine("Player: " + player.name + " active=" + player.gameObject.activeInHierarchy + " position=" + player.transform.position);
            foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                text.AppendLine("Sprite: " + renderer.name + " active=" + renderer.gameObject.activeInHierarchy + " enabled=" + renderer.enabled + " sprite=" + (renderer.sprite != null ? AssetDatabase.GetAssetPath(renderer.sprite) + "/" + renderer.sprite.name : "NULL") + " color=" + renderer.color + " bounds=" + renderer.bounds + " sorting=" + renderer.sortingLayerName + "/" + renderer.sortingLayerID + "/" + renderer.sortingOrder);
            foreach (var group in player.GetComponentsInChildren<SortingGroup>(true))
                text.AppendLine("Group: " + group.name + " sorting=" + group.sortingLayerName + "/" + group.sortingLayerID + "/" + group.sortingOrder);
            foreach (var animator in player.GetComponentsInChildren<Animator>(true))
                text.AppendLine("Animator: " + animator.name + " controller=" + (animator.runtimeAnimatorController != null ? AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) : "NULL"));
            foreach (var camera in Camera.allCameras)
                text.AppendLine("Camera: " + camera.name + " position=" + camera.transform.position + " viewportPlayer=" + camera.WorldToViewportPoint(player.transform.position) + " mask=" + camera.cullingMask);
        }
        foreach (var root in scene.GetRootGameObjects())
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            text.AppendLine("Canvas: " + canvas.name + " active=" + canvas.gameObject.activeInHierarchy + " order=" + canvas.sortingOrder);
        File.WriteAllText("Library/PlayerVisibilityAudit.result", text.ToString());
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Persistent fade overlay, driven by unscaled time while gameplay is blocked.</summary>
public sealed class SceneTransition : MonoBehaviour
{
    private static SceneTransition active;
    public static bool IsTransitioning => active != null;
    private CanvasGroup overlay;

    public static bool Load(string destination)
    {
        if (IsTransitioning || string.IsNullOrWhiteSpace(destination) || !Application.CanStreamedLevelBeLoaded(destination)) return false;
        var root = new GameObject("Scene Transition");
        DontDestroyOnLoad(root);
        active = root.AddComponent<SceneTransition>();
        active.StartCoroutine(active.ChangeScene(destination));
        return true;
    }

    private IEnumerator ChangeScene(string destination)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        gameObject.AddComponent<GraphicRaycaster>();
        overlay = gameObject.AddComponent<CanvasGroup>();
        overlay.alpha = 0;
        overlay.blocksRaycasts = true;
        var curtain = new GameObject("Black Curtain", typeof(RectTransform), typeof(Image));
        curtain.transform.SetParent(transform, false);
        var rect = (RectTransform)curtain.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        curtain.GetComponent<Image>().color = Color.black;
        float previousScale = Time.timeScale;
        Time.timeScale = 0;
        try
        {
            yield return Fade(0, 1, .55f);
            GameplayState.Reset();
            AudioListener.pause = false;
            var operation = SceneManager.LoadSceneAsync(destination);
            if (operation != null)
                while (!operation.isDone) yield return null;
            yield return null;
            yield return Fade(1, 0, .55f);
            previousScale = 1;
        }
        finally
        {
            Time.timeScale = previousScale;
            active = null;
            Destroy(gameObject);
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        overlay.alpha = to;
    }

    private void OnDestroy() { if (active == this) active = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active = null;
}

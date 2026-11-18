using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Commands invoked by UI; gameplay state remains authoritative.</summary>
public sealed class LevelSessionActions : MonoBehaviour
{
    public void Pause() => GameplayState.Pause();
    public void Resume() => GameplayState.Resume();
    public void Restart()
    {
        if (GameplayState.Current != GameplayMode.GameOver && GameplayState.Current != GameplayMode.Paused) return;
        string path = gameObject.scene.path;
        if (!Application.CanStreamedLevelBeLoaded(path))
        {
            Debug.LogError("Restart requires the current level in Build Settings.", this);
            return;
        }
        GameplayState.Reset();
        SceneManager.LoadScene(path);
    }
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (GameplayState.Current == GameplayMode.Playing) Pause();
        else if (GameplayState.Current == GameplayMode.Paused) Resume();
    }
}

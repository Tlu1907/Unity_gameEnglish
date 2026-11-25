using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Pause and scene navigation for the current play session.</summary>
public sealed class LevelSessionActions : MonoBehaviour
{
    public const string MainMenuScene = "Assets/_Project/Scenes/MainMenu.unity";
    private bool ownsPause, loading;
    public bool IsPausedByMenu => ownsPause;
    private float previousTimeScale = 1;
    private bool previousAudioPause;
    public void Pause()
    {
        if (loading || !GameplayState.CanAct) return;
        previousTimeScale = Time.timeScale; previousAudioPause = AudioListener.pause;
        ownsPause = true; Time.timeScale = 0; AudioListener.pause = true;
        GameplayState.Pause();
    }
    public void Resume()
    {
        if (loading || GameplayState.Current != GameplayMode.Paused) return;
        RestoreClock(); GameplayState.Resume();
    }
    private void RestoreClock()
    {
        if (!ownsPause) return;
        Time.timeScale = previousTimeScale; AudioListener.pause = previousAudioPause; ownsPause = false;
    }
    public void Restart()
    {
        if (GameplayState.Current != GameplayMode.GameOver && GameplayState.Current != GameplayMode.Paused) return;
        Load(gameObject.scene.path);
    }
    // Existing Quit button bindings now return to the menu instead of closing the application.
    public void Quit() => Load(MainMenuScene);
    private void Load(string path)
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(path)) { Debug.LogError("Scene is not enabled in Build Settings: " + path, this); return; }
        loading = true; ownsPause = false;
        GameplayState.Reset(); StoryProgress.Reset(); LearningJournal.Reset();
        Time.timeScale = 1; AudioListener.pause = false;
        SceneManager.LoadScene(path);
    }
    public void TogglePause()
    {
        if (GameplayState.Current == GameplayMode.Playing) Pause();
        else if (GameplayState.Current == GameplayMode.Paused && ownsPause) Resume();
    }
    private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) TogglePause(); }
    private void OnDisable() { RestoreClock(); }
}

using UnityEngine;
using UnityEngine.UI;

public sealed class GameStateUI : MonoBehaviour
{
    public GameObject pausePanel;
    public GameObject gameOverPanel;
    public Button pauseButton;
    private void OnEnable() { GameplayState.Changed += Refresh; Refresh(GameplayState.Current); }
    private void OnDisable() { GameplayState.Changed -= Refresh; }
    private void Refresh(GameplayMode mode)
    {
        pausePanel.SetActive(mode == GameplayMode.Paused);
        gameOverPanel.SetActive(mode == GameplayMode.GameOver);
        pauseButton.interactable = mode == GameplayMode.Playing;
    }
}

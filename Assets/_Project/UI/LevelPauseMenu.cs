using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class LevelPauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    private void OnEnable() { GameplayState.Changed += Refresh; Refresh(GameplayState.Current); }
    private void OnDisable() => GameplayState.Changed -= Refresh;
    private void Refresh(GameplayMode mode)
    {
        if (pausePanel == null) return;
        var actions = GetComponent<LevelSessionActions>() ?? FindAnyObjectByType<LevelSessionActions>();
        bool visible = mode == GameplayMode.Paused && actions != null && actions.IsPausedByMenu;
        pausePanel.SetActive(visible);
        if (visible && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(pausePanel.GetComponentInChildren<Button>().gameObject);
    }
}

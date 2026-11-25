using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    public const string NameKey = "EarthDefender.PlayerName";
    public const string VolumeKey = "EarthDefender.MasterVolume";
    public const string MuteKey = "EarthDefender.Muted";
    public string gameplayScene = "Assets/_Project/Scenes/Sence_1/Scene1_Merged.unity";
    public InputField username;
    public Slider volume;
    public Toggle mute;
    public Text volumeLabel, status;
    public GameObject settingsPanel, helpPanel;
    public Button playButton, settingsButton, helpButton;
    private bool loading;
    public static string PlayerName => PlayerProfileStore.PlayerName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedAudio()
    {
        AudioListener.volume = PlayerPrefs.GetInt(MuteKey, 0) == 1 ? 0 : Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, .8f));
    }
    private void Start()
    {
        Time.timeScale = 1; AudioListener.pause = false;
        username.SetTextWithoutNotify(PlayerName);
        volume.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, .8f)));
        mute.SetIsOnWithoutNotify(PlayerPrefs.GetInt(MuteKey, 0) == 1);
        settingsPanel.SetActive(false); helpPanel.SetActive(false);
        RefreshAudio(); Select(playButton.gameObject);
    }
    public void SaveName(string value)
    {
        string clean = PlayerProfileStore.NormalizeName(value);
        username.SetTextWithoutNotify(clean);
        PlayerProfileStore.SetName(clean);
    }
    public void ChangeVolume(float value) { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); RefreshAudio(); }
    public void ChangeMute(bool value) { PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); RefreshAudio(); }
    private void RefreshAudio()
    {
        ApplySavedAudio();
        volumeLabel.text = mute.isOn ? "Đã tắt tiếng" : Mathf.RoundToInt(volume.value * 100) + "%";
    }
    public void ShowSettings() { if (loading) return; helpPanel.SetActive(false); settingsPanel.SetActive(true); Select(volume.gameObject); }
    public void ShowHelp() { if (loading) return; settingsPanel.SetActive(false); helpPanel.SetActive(true); Select(helpPanel.GetComponentInChildren<Button>().gameObject); }
    public void ClosePanels()
    {
        bool wasSettings = settingsPanel.activeSelf;
        settingsPanel.SetActive(false); helpPanel.SetActive(false); PlayerPrefs.Save();
        Select(wasSettings ? settingsButton.gameObject : helpButton.gameObject);
    }
    public void Play()
    {
        if (loading) return;
        if (string.IsNullOrWhiteSpace(username.text))
        {
            status.text = "Vui lòng nhập tên người chơi trước khi bắt đầu.";
            Select(username.gameObject); username.ActivateInputField();
            return;
        }
        SaveName(username.text);
        if (!Application.CanStreamedLevelBeLoaded(gameplayScene)) { status.text = "Không tìm thấy màn chơi. Kiểm tra Build Settings."; return; }
        loading = true; playButton.interactable = settingsButton.interactable = helpButton.interactable = false;
        status.text = "Đang vào thành phố…"; PlayerPrefs.Save();
        GameplayState.Reset(); StoryProgress.Reset(); LearningJournal.Reset();
        Time.timeScale = 1; AudioListener.pause = false;
        if (!SceneTransition.Load(gameplayScene))
        {
            loading = false;
            playButton.interactable = settingsButton.interactable = helpButton.interactable = true;
            status.text = "Chưa thể mở màn chơi. Hãy thử lại.";
        }
    }
    private void Update()
    {
        if (!loading && Input.GetKeyDown(KeyCode.Escape) && (settingsPanel.activeSelf || helpPanel.activeSelf)) ClosePanels();
    }
    private static void Select(GameObject target) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target); }
    private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    private void OnApplicationQuit() => PlayerPrefs.Save();
}

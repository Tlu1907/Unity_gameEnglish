using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Attach to the active DialogueCanvas; assign its existing panel and text objects.</summary>
[DisallowMultipleComponent]
public sealed class NPCIntroDialogue : MonoBehaviour
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text englishText;
    [SerializeField] private TMP_Text vietnameseText;
    [SerializeField] private TMP_Text continueText;
    [SerializeField] private DialogueData dialogue;
    [SerializeField] private Image portraitImage;
    [SerializeField] private bool playOnStart = true;
    [Header("Text reveal")]
    [SerializeField] private bool typewriterEffect = true;
    [SerializeField, Min(1f)] private float charactersPerSecond = 40f;

    private DialogueData activeDialogue;
    private Sprite originalPortrait;
    private bool replacedPortrait;
    private int lineIndex;
    private float previousTimeScale;
    private float revealedCharacters;
    private int characterCount;
    private System.Action<bool> completed;
    public bool IsTyping { get; private set; }
    public bool IsOpen { get; private set; }

    private void Start()
    {
        if (playOnStart) Open();
        else if (!IsOpen && dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    public void Open() => ShowDialogue(dialogue);

#if UNITY_EDITOR
    /// <summary>Copies the authored NPC canvas for the story dialogue Editor builder.</summary>
    public StoryQuizPanel CreateStoryPanel()
    {
        if (dialoguePanel == null || englishText == null || vietnameseText == null ||
            !dialoguePanel.transform.IsChildOf(transform) ||
            !englishText.transform.IsChildOf(dialoguePanel.transform) ||
            !vietnameseText.transform.IsChildOf(dialoguePanel.transform)) return null;

        NPCIntroDialogue copy = Instantiate(this);
        copy.gameObject.SetActive(false);
        copy.playOnStart = false;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy.gameObject, gameObject.scene);
        StoryQuizPanel view = copy.dialoguePanel.GetComponent<StoryQuizPanel>();
        if (view == null) view = copy.dialoguePanel.AddComponent<StoryQuizPanel>();
        view.Configure(copy.npcNameText, copy.englishText, copy.vietnameseText, copy.continueText);
        if (copy.portraitImage != null) view.ConfigurePortrait(copy.portraitImage);
        GameObject canvas = copy.gameObject;
        DestroyImmediate(copy);
        canvas.SetActive(true);
        return view;
    }
#endif

    public void ShowDialogue(DialogueData data) => TryShowDialogue(data, null);

    public bool TryShowDialogue(DialogueData data, System.Action<bool> onCompleted)
    {
        if (IsOpen || !enabled) return false;
        if (dialoguePanel == null || englishText == null || vietnameseText == null ||
            data == null || !data.IsValid)
        {
            Debug.LogWarning("NPCIntroDialogue: assign Dialogue Panel, English Text, Vietnamese Text and a valid DialogueData asset.", this);
            return false;
        }
        if (!GameplayState.CanAct) return false;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!isActiveAndEnabled || !GameplayState.BeginDialogue(this)) return false;
        completed = onCompleted;
        activeDialogue = data;
        previousTimeScale = Time.timeScale;
        IsOpen = true;
        Time.timeScale = 0f;
        lineIndex = 0;
        if (npcNameText != null) npcNameText.text = data.speakerName;
        replacedPortrait = portraitImage != null && data.portrait != null;
        if (replacedPortrait)
        {
            originalPortrait = portraitImage.sprite;
            portraitImage.sprite = data.portrait;
        }
        dialoguePanel.SetActive(true);
        ShowLine();
        return true;
    }

    // Update and legacy keyboard input keep working with timeScale at zero.
    private void Update()
    {
        if (!IsOpen) return;
        if (GameplayState.Current != GameplayMode.Dialogue || dialoguePanel == null || !dialoguePanel.activeInHierarchy)
        { Close(); return; }
        if (Input.GetKeyDown(KeyCode.C)) Advance();
        else if (IsTyping) RevealText(Time.unscaledDeltaTime);
    }

    public void Advance()
    {
        if (!IsOpen || GameplayState.Current != GameplayMode.Dialogue) return;
        if (IsTyping) { FinishTyping(); return; }
        if (lineIndex + 1 >= activeDialogue.lines.Length) { CloseInternal(true); return; }
        lineIndex++;
        ShowLine();
    }

    private void ShowLine()
    {
        englishText.text = activeDialogue.lines[lineIndex].english;
        vietnameseText.text = activeDialogue.lines[lineIndex].vietnamese;
        englishText.maxVisibleCharacters = vietnameseText.maxVisibleCharacters = int.MaxValue;
        englishText.ForceMeshUpdate();
        vietnameseText.ForceMeshUpdate();
        characterCount = Mathf.Max(englishText.textInfo.characterCount, vietnameseText.textInfo.characterCount);
        revealedCharacters = 0f;
        IsTyping = typewriterEffect && characterCount > 0;
        if (IsTyping) englishText.maxVisibleCharacters = vietnameseText.maxVisibleCharacters = 0;
        RefreshPrompt();
    }

    private void RevealText(float deltaTime)
    {
        revealedCharacters += Mathf.Max(1f, charactersPerSecond) * deltaTime;
        int visible = Mathf.FloorToInt(revealedCharacters);
        englishText.maxVisibleCharacters = vietnameseText.maxVisibleCharacters = visible;
        if (visible >= characterCount) FinishTyping();
    }

    private void FinishTyping()
    {
        IsTyping = false;
        englishText.maxVisibleCharacters = vietnameseText.maxVisibleCharacters = int.MaxValue;
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        if (continueText != null)
            continueText.text = IsTyping ? "Nhấn C để hiện toàn bộ" : lineIndex == activeDialogue.lines.Length - 1
                ? activeDialogue.closePrompt : activeDialogue.continuePrompt;
    }

    public void Close() => CloseInternal(false);

    private void CloseInternal(bool readToEnd)
    {
        if (!IsOpen) return;
        var callback = completed;
        completed = null;
        IsOpen = false;
        IsTyping = false;
        activeDialogue = null;
        if (replacedPortrait && portraitImage != null) portraitImage.sprite = originalPortrait;
        replacedPortrait = false;
        Time.timeScale = previousTimeScale;
        GameplayState.EndDialogue(this);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        callback?.Invoke(readToEnd);
    }

    private void OnDisable() => Close();
}

using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays authored JSON on the existing NPC frame; pauses gameplay, not UI/network IO.</summary>
[DisallowMultipleComponent]
public sealed class NpcJsonDialoguePanel : MonoBehaviour
{
    [SerializeField] private UnityAiDialogueClient client;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text npcNameText, englishText, learningHints, statusText, feedbackText;
    [SerializeField] private GameObject feedbackPanel;
    [SerializeField] private TMP_InputField answerInput;
    [SerializeField] private Button submitButton, continueButton;
    [SerializeField] private Button referenceButton;
    [SerializeField] private Image portrait;
    [SerializeField] private bool typewriterEffect = true;
    [SerializeField, Min(1)] private float charactersPerSecond = 40;
    [SerializeField] private string journalMapId = "map2";
    [SerializeField] private string journalMapTitle = "Map 2 · Forest";
    private NpcJsonDialogue conversation;
    private NpcJsonDialogue.Node hiddenNode;
    private Action<bool> completed;
    private UnityAiDialogueResponse grade;
    private Sprite previousPortrait;
    private bool replacedPortrait, busy, followUp;
    private int index, generation, pendingRequests;
    private float previousScale, visible;
    private int characterCount;
    private sealed class ExerciseState { public int attempts; public bool resolved; public string answer, feedback; }
    private readonly Dictionary<string, ExerciseState> exerciseStates = new Dictionary<string, ExerciseState>();
    public string CompletedTempleReport { get; private set; }
    private TempleExercise Exercise => conversation?.ExerciseFor(CurrentNode);
    private ExerciseState StateFor(TempleExercise exercise)
    {
        string key = conversation.id + ":" + exercise.id;
        if (!exerciseStates.TryGetValue(key, out var state)) exerciseStates[key] = state = new ExerciseState();
        return state;
    }
    public bool IsOpen { get; private set; }
    public bool IsTyping { get; private set; }
    public bool IsBusy => busy;
    public UnityAiDialogueResponse LastGrade => grade;
    public string CurrentNodeId => CurrentNode != null ? CurrentNode.id : null;
    public void SetJournalMap(string id, string title)
    { journalMapId = id; journalMapTitle = title; }
    public void ApplyReadableTypography()
    {
        SetFont(englishText, 38);
        SetFont(learningHints, 30);
        SetFont(feedbackText, 30);
        SetFont(statusText, 26);
        SetFont(npcNameText, 32);
        if (answerInput != null)
        {
            answerInput.pointSize = 34;
            SetFont(answerInput.textComponent, 34);
            SetFont(answerInput.placeholder as TMP_Text, 32);
        }
        foreach (var button in GetComponentsInChildren<Button>(true))
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) SetFont(label, 28);
        if (statusText != null)
            statusText.rectTransform.sizeDelta = new Vector2(statusText.rectTransform.sizeDelta.x, 64);
        EnsureQuestionArea();
    }
    private void EnsureQuestionArea()
    {
        if (englishText == null || dialoguePanel == null) return;
        var scroll = englishText.GetComponentInParent<ScrollRect>(true);
        if (scroll == null)
        {
            var wrapper = new GameObject("QuestionScroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            wrapper.layer = englishText.gameObject.layer;
            wrapper.transform.SetParent(dialoguePanel.transform, false);
            scroll = wrapper.GetComponent<ScrollRect>();
            englishText.transform.SetParent(wrapper.transform, false);
        }
        var area = (RectTransform)scroll.transform;
        area.anchorMin = new Vector2(.025f, .46f);
        area.anchorMax = new Vector2(.50f, .94f);
        area.offsetMin = area.offsetMax = Vector2.zero;
        area.localScale = Vector3.one;
        scroll.viewport = area;
        scroll.content = englishText.rectTransform;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.scrollSensitivity = 40;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var content = englishText.rectTransform;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        content.localScale = Vector3.one;
        englishText.alignment = TextAlignmentOptions.TopLeft;
        englishText.margin = new Vector4(4, 10, 8, 8);
        englishText.overflowMode = TextOverflowModes.Overflow;
        englishText.raycastTarget = true;
        var fitter = englishText.GetComponent<ContentSizeFitter>() ?? englishText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
    private static void SetFont(TMP_Text text, float size)
    {
        if (text == null) return;
        text.enableAutoSizing = false;
        text.fontSize = text.fontSizeMin = text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.Normal;
    }
    private NpcJsonDialogue.Node CurrentNode => hiddenNode ??
        (conversation != null && index < conversation.dialogues.Length ? conversation.dialogues[index] : null);

    public bool TryOpen(DialogueData data, Action<bool> onCompleted)
    {
        if (IsOpen || !enabled || !GameplayState.CanAct || data == null || data.conversationJson == null ||
            !NpcJsonDialogue.TryParse(data.conversationJson.text, out var parsed) ||
            client == null || dialoguePanel == null || englishText == null || answerInput == null ||
            submitButton == null || continueButton == null) return false;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!isActiveAndEnabled || !GameplayState.BeginDialogue(this)) return false;
        ApplyReadableTypography();
        conversation = parsed; index = 0; hiddenNode = null; followUp = false;
        generation++; busy = pendingRequests > 0; completed = onCompleted;
        previousScale = Time.timeScale; Time.timeScale = 0;
        IsOpen = true;
        if (npcNameText != null) npcNameText.text = string.IsNullOrWhiteSpace(data.speakerName) ? parsed.speaker : data.speakerName;
        replacedPortrait = portrait != null && data.portrait != null;
        if (replacedPortrait) { previousPortrait = portrait.sprite; portrait.sprite = data.portrait; }
        dialoguePanel.SetActive(true);
        ShowNode();
        return true;
    }

    private void OnEnable()
    {
        if (continueButton != null) continueButton.onClick.AddListener(Advance);
        if (referenceButton != null) referenceButton.onClick.AddListener(ShowReferenceAnswer);
        if (answerInput != null)
        {
            answerInput.onValueChanged.AddListener(OnInputChanged);
            answerInput.onSubmit.AddListener(OnInputSubmitted);
        }
    }
    private void OnInputChanged(string _) => RefreshControls();
    private void OnInputSubmitted(string _) => SubmitAnswer();

    private void Update()
    {
        if (!IsOpen) return;
        if (GameplayState.Current != GameplayMode.Dialogue || !dialoguePanel.activeInHierarchy) { Close(); return; }
        if (IsTyping)
        {
            visible += charactersPerSecond * Time.unscaledDeltaTime;
            englishText.maxVisibleCharacters = Mathf.FloorToInt(visible);
            if (visible >= characterCount) FinishTyping();
        }
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        else if (!answerInput.isFocused && Input.GetKeyDown(KeyCode.C)) Advance();
    }

    private void ShowNode()
    {
        grade = null;
        var node = CurrentNode;
        englishText.richText = false;
        englishText.text = node.dialogue_en;
        if (Exercise != null)
            englishText.text += "\n\n" + Exercise.prompt_en + (string.IsNullOrWhiteSpace(Exercise.reading_text_en) ? "" : "\n\n" + Exercise.reading_text_en);
        englishText.maxVisibleCharacters = int.MaxValue;
        englishText.ForceMeshUpdate();
        characterCount = englishText.textInfo.characterCount;
        ResetScroll(englishText);
        visible = 0;
        IsTyping = typewriterEffect && characterCount > 0;
        if (IsTyping) englishText.maxVisibleCharacters = 0;
        if (learningHints != null)
        {
            learningHints.richText = false; learningHints.text = node.LearningText();
            learningHints.maxVisibleCharacters = int.MaxValue;
            ResetScroll(learningHints);
            if (Exercise?.IsReport == true) learningHints.text += "\n\nTƯ LIỆU BA PHIẾN ĐÁ\n" + TempleEvidence();
        }
        if (feedbackText != null) { feedbackText.richText = false; feedbackText.text = ""; }
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        answerInput.SetTextWithoutNotify("");
        answerInput.characterLimit = Exercise?.IsReport == true ? 1500 : 300;
        answerInput.richText = false;
        if (Exercise != null)
        {
            var state = StateFor(Exercise);
            answerInput.SetTextWithoutNotify(state.answer ?? "");
            if (!string.IsNullOrWhiteSpace(state.feedback)) ShowExerciseFeedback(state.feedback);
            SetStatus(state.resolved ? "Bấm Tiếp tục để đọc tư liệu." : Exercise.IsReport
                ? "Viết đúng 3 câu. Có thể xem tư liệu bên cạnh và sửa bài đến khi đạt."
                : "Nhập đáp án. Tối đa " + Exercise.retry_policy.max_attempts + " lượt cho phiến đá này.");
        }
        else if (node.IsQuestion && client.TryGetGrade(node.id, out grade))
        {
            if (grade.data != null) answerInput.SetTextWithoutNotify(grade.data.original_answer ?? "");
            ShowGrade();
        }
        else SetStatus(node.IsQuestion ? "Trả lời câu hỏi bằng tiếng Anh rồi bấm Gửi." : "C hoặc Tiếp tục để đọc tiếp.");
        RefreshControls();
    }

    public void RefreshControls()
    {
        if (!IsOpen || answerInput == null) return;
        bool question = CurrentNode.IsQuestion && !followUp;
        bool resolved = Exercise != null ? StateFor(Exercise).resolved : grade != null && grade.CanContinue;
        bool canContinue = !question || resolved;
        bool canAnswer = question && !resolved || followUp;
        answerInput.gameObject.SetActive(question || followUp);
        answerInput.interactable = canAnswer && !busy;
        submitButton.gameObject.SetActive(question || followUp);
        submitButton.interactable = canAnswer && !busy && !string.IsNullOrWhiteSpace(answerInput.text);
        continueButton.interactable = !busy && canContinue;
        if (referenceButton != null) referenceButton.gameObject.SetActive(Exercise?.IsReport == true);
        Label(submitButton, busy ? "Đang chấm…" : followUp ? "Hỏi" : grade != null && grade.CanContinue ? "Đã chấm" : grade != null ? "Gửi lại" : "Gửi");
        Label(continueButton, followUp || hiddenNode != null ? "Kết thúc" : "Tiếp tục");
        if (answerInput.placeholder is TMP_Text hint)
            hint.text = followUp ? "Hỏi vị trí hoặc đường đến di tích (không bắt buộc)…" : "Nhập câu trả lời bằng tiếng Anh…";
    }

    private static void Label(Button button, string text)
    { var label = button.GetComponentInChildren<TMP_Text>(true); if (label != null) label.text = text; }
    private void SetStatus(string text) { if (statusText != null) { statusText.richText = false; statusText.text = text; } }
    private void FinishTyping() { IsTyping = false; englishText.maxVisibleCharacters = int.MaxValue; }

    public void Advance()
    {
        if (!IsOpen || busy || GameplayState.Current != GameplayMode.Dialogue) return;
        if (IsTyping) { FinishTyping(); return; }
        if (followUp || hiddenNode != null)
        {
            if (hiddenNode != null) RecordCurrentLearning();
            CloseInternal(true);
            return;
        }
        if (CurrentNode.IsQuestion && !(Exercise != null ? StateFor(Exercise).resolved : grade != null && grade.CanContinue)) return;
        RecordCurrentLearning();
        if (!string.IsNullOrEmpty(CurrentNode.next_id))
        {
            index = Array.FindIndex(conversation.dialogues, node => node.id == CurrentNode.next_id);
            ShowNode(); return;
        }
        if (index + 1 < conversation.dialogues.Length) { index++; ShowNode(); return; }
        if (conversation.conditional_dialogues != null && Array.Exists(conversation.conditional_dialogues,
            node => conversation.FindFollowUp(node.trigger.player_intent, node.trigger.target_location, index) != null))
        {
            followUp = true;
            SetStatus("Bạn có thể hỏi đường đến di tích hoặc bấm Kết thúc.");
            answerInput.SetTextWithoutNotify("");
            RefreshControls();
            return;
        }
        CloseInternal(true);
    }

    private void RecordCurrentLearning()
    {
        var node = CurrentNode;
        if (node == null) return;
        var vocabulary = node.vocabulary == null ? null : Array.ConvertAll(node.vocabulary,
            word => word == null ? null : new ResearchDocument.Vocabulary { word = word.word, meaning = word.meaning });
        LearningJournal.Record(journalMapId, journalMapTitle, vocabulary, null);
        if (node.grammar == null) return;
        foreach (var rule in node.grammar)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.formula)) continue;
            string name = rule.name_en ?? "";
            string group = name.StartsWith("Present perfect", StringComparison.OrdinalIgnoreCase) ? "present_perfect"
                : name.StartsWith("Past simple", StringComparison.OrdinalIgnoreCase) ? "past_simple"
                : name.StartsWith("Present continuous", StringComparison.OrdinalIgnoreCase) ? "present_continuous"
                : name.StartsWith("Present simple", StringComparison.OrdinalIgnoreCase) ? "present_simple"
                : name.StartsWith("Future simple", StringComparison.OrdinalIgnoreCase) ? "ability_future"
                : name.StartsWith("Imperative", StringComparison.OrdinalIgnoreCase) ? "commands_suggestions" : "general";
            LearningJournal.RecordGrammar(journalMapId, journalMapTitle, new LearningJournal.GrammarNote
            {
                Id = conversation.id + ":" + rule.formula.Trim(), GroupId = group,
                Text = rule.formula.Trim() + (string.IsNullOrWhiteSpace(rule.note_vi) ? "" : ": " + rule.note_vi.Trim())
            });
        }
    }

    public void SubmitAnswer()
    {
        if (!IsOpen || busy || string.IsNullOrWhiteSpace(answerInput.text)) return;
        if (Exercise != null) { SubmitExercise(); return; }
        if (!followUp && (!CurrentNode.IsQuestion || grade != null && grade.CanContinue)) return;
        FinishTyping();
        string answer = answerInput.text.Trim();
        answerInput.DeactivateInputField();
        busy = true; pendingRequests++;
        int requestGeneration = generation;
        string nodeId = CurrentNodeId;
        SetStatus(followUp ? "Đang hỏi kiểm lâm…" : "Đang chấm câu trả lời, vui lòng chờ…");
        RefreshControls();
        if (followUp)
            client.DetectIntent(answer, result =>
            {
                RequestFinished();
                if (!IsOpen) return;
                if (generation != requestGeneration) { ShowNode(); return; }
                if (result.status != "ok") SetStatus("Chưa nhận được phản hồi. Hãy thử lại.");
                else
                {
                    var node = result.triggered ? conversation.FindFollowUp(result.intent, result.target_location, index) : null;
                    if (node != null) { followUp = false; hiddenNode = node; ShowNode(); }
                    else SetStatus("Chưa có gợi ý chỉ đường cho câu hỏi này. Bạn có thể hỏi lại hoặc kết thúc.");
                }
                RefreshControls();
            });
        else
            client.Grade(nodeId, answer, result =>
            {
                RequestFinished();
                if (!IsOpen) return;
                if (generation != requestGeneration) { ShowNode(); return; }
                if (CurrentNodeId != nodeId) return;
                if (result.IsGraded || result.IsExhausted) { grade = result; ShowGrade(); }
                else SetStatus(result.status == "busy" ? "Bộ chấm đang bận. Hãy thử lại; lượt này chưa tính."
                    : result.status == "review_required" ? "Chưa chấm được câu này. Hãy thử lại; lượt này chưa tính."
                    : "Không kết nối được bộ chấm hoặc phản hồi không hợp lệ. Kiểm tra dịch vụ chấm rồi thử lại.");
                RefreshControls();
            });
    }

    private string TempleEvidence()
    {
        var text = new StringBuilder();
        for (int i = 0; i <= index && i < conversation.dialogues.Length; i++)
        {
            var node = conversation.dialogues[i];
            if (node.type != "question") text.AppendLine(node.dialogue_en).AppendLine();
            var exercise = conversation.ExerciseFor(node);
            if (exercise != null && !string.IsNullOrWhiteSpace(exercise.reading_text_en))
                text.AppendLine(exercise.reading_text_en).AppendLine();
        }
        return text.ToString();
    }
    private void ShowExerciseFeedback(string text)
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(true);
        if (feedbackText != null) { feedbackText.text = text; ResetScroll(feedbackText); }
    }
    public void ShowReferenceAnswer()
    {
        if (!IsOpen || Exercise?.IsReport != true) return;
        ShowExerciseFeedback("BÀI THAM KHẢO\n" + Exercise.reference_answer_en + "\n\nTƯ LIỆU\n" + TempleEvidence());
    }
    private void SubmitExercise()
    {
        var exercise = Exercise;
        var state = StateFor(exercise);
        if (state.resolved) return;
        FinishTyping();
        state.answer = answerInput.text.Trim();
        answerInput.DeactivateInputField();
        busy = true; pendingRequests++;
        int requestGeneration = generation;
        string submitted = state.answer;
        SetStatus("Đang chấm bài tập…"); RefreshControls();
        client.Grade(CurrentNode.id, submitted, result =>
        {
            RequestFinished();
            // Keep a validated result on its exercise even if the view was closed meanwhile.
            HandleExerciseResult(exercise, result);
            if (!IsOpen || generation != requestGeneration) return;
            ShowExerciseFeedback(result.IsGraded || result.IsExhausted ? state.feedback
                : "Chưa nhận được kết quả hợp lệ. Kiểm tra dịch vụ chấm rồi thử lại; bài không bị tính sai.");
            SetStatus(state.resolved ? "Bấm Tiếp tục để đọc tư liệu và làm bài tiếp theo."
                : exercise.IsReport ? "Xem nhận xét và tư liệu, rồi sửa báo cáo. Không giới hạn lượt sửa."
                : "Xem nhận xét rồi sửa câu trả lời. Lỗi kết nối không mất lượt.");
            RefreshControls();
        });
    }
    private void HandleExerciseResult(TempleExercise exercise, UnityAiDialogueResponse result)
    {
        if (!result.IsGraded && !result.IsExhausted) return;
        var state = StateFor(exercise);
        state.attempts = result.attempt;
        state.resolved = result.passed || !exercise.IsReport && result.IsExhausted;
        state.feedback = result.FeedbackText();
        if (!result.passed && !state.resolved && !exercise.IsReport)
            state.feedback += "\n" + exercise.hint_after_first_incorrect?.vi;
        if (exercise.IsReport && result.passed) CompletedTempleReport = state.answer;
    }

    private void RequestFinished()
    {
        pendingRequests = Mathf.Max(0, pendingRequests - 1);
        busy = pendingRequests > 0;
        // A late result is cached by the client, but never written to a different page.
        if (IsOpen) RefreshControls();
    }
    private void ShowGrade()
    {
        if (feedbackText != null)
        {
            if (feedbackPanel != null) feedbackPanel.SetActive(true);
            feedbackText.text = grade.FeedbackText();
            ResetScroll(feedbackText);
        }
        SetStatus(grade.passed ? "Đạt yêu cầu. Bấm Tiếp tục."
            : grade.IsExhausted ? "Đã hết lượt. Xem đáp án tham khảo rồi bấm Tiếp tục."
            : "Hãy xem nhận xét, sửa câu trả lời rồi gửi lại.");
    }

    public void Close() => CloseInternal(false);
    private static void ResetScroll(TMP_Text text)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(text.rectTransform);
        var scroll = text.GetComponentInParent<ScrollRect>(true);
        if (scroll != null) scroll.verticalNormalizedPosition = 1;
    }
    private void CloseInternal(bool readToEnd)
    {
        if (!IsOpen) return;
        IsOpen = false; IsTyping = false; generation++;
        Time.timeScale = previousScale;
        GameplayState.EndDialogue(this);
        if (replacedPortrait && portrait != null) portrait.sprite = previousPortrait;
        replacedPortrait = false;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        var callback = completed; completed = null;
        callback?.Invoke(readToEnd);
    }
    private void OnDisable()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Advance);
        if (referenceButton != null) referenceButton.onClick.RemoveListener(ShowReferenceAnswer);
        if (answerInput != null)
        {
            answerInput.onValueChanged.RemoveListener(OnInputChanged);
            answerInput.onSubmit.RemoveListener(OnInputSubmitted);
        }
        Close();
        pendingRequests = 0; busy = false;
    }
}

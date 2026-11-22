using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>Fixed bilingual story conversation using the existing NPC frame.</summary>
public sealed class StoryQuizPanel : MonoBehaviour
{
    private enum Step { Intro, Consent, ConsentReply, Question, QuestionFeedback, Success }
    [Header("Authored dialogue view")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text englishText;
    [SerializeField] private TMP_Text vietnameseText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text vocabGrammarText;
    [SerializeField] private TMP_Text grammarNotesText;
    [SerializeField] private Color vocabularyHighlightColor = new Color(1f, .78f, .25f);
    private string plainEnglishText;
    [Header("Learning journal map")]
    [SerializeField] private string journalMapId = "map1";
    [SerializeField] private string journalMapTitle = "Màn 1 · Thành phố";
    [SerializeField] private bool showVietnamese = true;
    [SerializeField] private Button[] buttons = new Button[4];
    [SerializeField] private TMP_Text[] labels = new TMP_Text[4];
    [SerializeField] private Button advanceButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Color consentHighlightColor = new Color(.2f, .45f, .25f);
    [Header("Extra interactions")]
    [SerializeField] private UnityEvent onOpened = new UnityEvent();
    [SerializeField] private UnityEvent onCorrect = new UnityEvent();
    [SerializeField] private UnityEvent onWrong = new UnityEvent();
    [SerializeField] private UnityEvent onClosed = new UnityEvent();
    private readonly UnityAction[] choiceListeners = new UnityAction[4];
    private readonly Color[] choiceColors = new Color[4];
    private Sprite authoredPortrait;
    private bool authoredPortraitVisible;
    private StoryBeatData beat;
    private NpcQuestionConversation conversation;
    private int questionIndex;
    private bool consentAccepted, repeatConsent;
    private string[] answers;
    private Action<bool> completed;
    private Action<int> scoredCompletion;
    private int correctAnswers;
    private Step step;
    private int declines;
    private float previousTimeScale;
    private bool open;
    private QuestionData eventQuestion;
    private ResearchDocument.Document researchDocument;
    private NpcQuestionConversation.Choice selectedConsent;
    private bool editorPreview;
    private LearningJournal.GrammarNote currentPageGrammar;
    private readonly List<LearningJournal.GrammarNote> pendingDialogueGrammar = new List<LearningJournal.GrammarNote>();
    public bool IsOpen => open;
    public void SetJournalMap(string id, string title)
    {
        journalMapId = id;
        journalMapTitle = title;
    }

#if UNITY_EDITOR
    public bool PreviewPage(TextAsset source, bool research, string page, int index)
    {
        if (open) Close();
        if (!GameplayState.CanAct) return false;
        editorPreview = true;
        bool opened = research
            ? ResearchDocument.TryParse(source, out var document) && TryOpenResearchDocument(document, _ => { })
            : TryOpenConversation(source, null);
        if (!opened) { editorPreview = false; return false; }
        if (page == "Introduction") return true;
        if (page == "Completion")
        {
            step = Step.Success;
            SetText(conversation.completion.after_en, conversation.completion.after_vi);
            SetChoices();
        }
        else if (page == "Consent" || page == "Consent Reply")
        {
            if (conversation.consent == null) return true;
            ShowConsent();
            if (page == "Consent Reply")
                Choose(Mathf.Clamp(index, 0, conversation.consent.choices.Length - 1));
        }
        else if (conversation.questions.Length > 0)
        {
            questionIndex = Mathf.Clamp(index, 0, conversation.questions.Length - 1);
            ShowQuestion();
            if (page == "Correct Feedback") Choose(Array.IndexOf(answers, conversation.questions[questionIndex].correct));
            else if (page == "Wrong Feedback") Choose((Array.IndexOf(answers, conversation.questions[questionIndex].correct) + 1) % answers.Length);
        }
        return true;
    }
#endif

    public static StoryQuizPanel FindOrCreate()
    {
        // Ignore leftover/incomplete panels created while editing other UIs.
        foreach (var candidate in FindObjectsByType<StoryQuizPanel>(FindObjectsInactive.Include))
            if (candidate.enabled && candidate.HasReferences) return candidate;
        GameObject prefab = Resources.Load<GameObject>("StoryDialogue");
        if (prefab != null) return Instantiate(prefab).GetComponentInChildren<StoryQuizPanel>(true);
        Debug.LogWarning("Story dialogue: add the authored StoryDialogue prefab to the scene.");
        return null;
    }

    // Authoring helper, called only by the Editor builder and isolated tests.
    // Runtime playback preserves the authored layout and font settings.
    public void Configure(TMP_Text speaker, TMP_Text english, TMP_Text vietnamese, TMP_Text prompt)
    {
        nameText = speaker; englishText = english; vietnameseText = vietnamese; promptText = prompt;
        var panel = (RectTransform)transform;
        Place(panel, new Vector2(.03f, .02f), new Vector2(.97f, .49f));
        if (nameText != null) Place(nameText.rectTransform, new Vector2(.2f, .86f), new Vector2(.96f, .97f));
        Place(englishText.rectTransform, new Vector2(.2f, .61f), new Vector2(.96f, .85f));
        Place(vietnameseText.rectTransform, new Vector2(.2f, .34f), new Vector2(.96f, .60f));
        englishText.maxVisibleCharacters = vietnameseText.maxVisibleCharacters = int.MaxValue;
        englishText.enableAutoSizing = vietnameseText.enableAutoSizing = true;
        englishText.fontSizeMin = vietnameseText.fontSizeMin = 14;
        englishText.fontSizeMax = vietnameseText.fontSizeMax = 27;
        englishText.richText = vietnameseText.richText = false;
        if (promptText != null)
        {
            Place(promptText.rectTransform, new Vector2(.2f, .02f), new Vector2(.96f, .10f));
            promptText.enableAutoSizing = true;
            promptText.fontSizeMin = 12; promptText.fontSizeMax = 19;
        }
        for (int i = 0; i < buttons.Length; i++)
        {
            var go = new GameObject("ChoiceButton" + (i + 1), typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            int column = i % 2, row = i / 2;
            Place((RectTransform)go.transform, new Vector2(.20f + column * .39f, .22f - row * .11f),
                new Vector2(.57f + column * .39f, .32f - row * .11f));
            go.GetComponent<Image>().color = new Color(.10f, .15f, .22f, .96f);
            buttons[i] = go.GetComponent<Button>();
            buttons[i].targetGraphic = go.GetComponent<Image>();
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.layer = gameObject.layer;
            label.transform.SetParent(go.transform, false);
            Place((RectTransform)label.transform, Vector2.zero, Vector2.one);
            labels[i] = label.GetComponent<TextMeshProUGUI>();
            labels[i].font = vietnameseText.font;
            labels[i].fontSize = 28;
            labels[i].enableAutoSizing = true;
            labels[i].fontSizeMin = 25; labels[i].fontSizeMax = 28;
            labels[i].alignment = TextAlignmentOptions.Center;
            labels[i].color = Color.white;
            labels[i].raycastTarget = false;
        }
        if (isActiveAndEnabled) BindControls();
    }

    public void ConfigureActionButtons(Button next, Button close)
    {
        UnbindControls();
        advanceButton = next; closeButton = close;
        if (isActiveAndEnabled) BindControls();
    }

    private bool HasReferences => englishText != null && vietnameseText != null && buttons != null &&
        labels != null && buttons.Length == 4 && labels.Length == 4 &&
        Array.TrueForAll(buttons, button => button != null) && Array.TrueForAll(labels, label => label != null);

    private void Start() { if (!open) gameObject.SetActive(false); }

    private void OnEnable()
    {
        if (portrait != null)
        {
            authoredPortrait = portrait.sprite;
            authoredPortraitVisible = portrait.gameObject.activeSelf;
        }
        for (int i = 0; i < 4; i++)
            if (buttons != null && i < buttons.Length && buttons[i] != null && buttons[i].targetGraphic != null)
                choiceColors[i] = buttons[i].targetGraphic.color;
        BindControls();
    }

    private void BindControls()
    {
        UnbindControls();
        if (buttons != null)
            for (int i = 0; i < Mathf.Min(4, buttons.Length); i++)
            {
                if (buttons[i] == null) continue;
                int index = i;
                if (choiceListeners[i] == null) choiceListeners[i] = () => Choose(index);
                buttons[i].onClick.AddListener(choiceListeners[i]);
            }
        if (advanceButton != null) advanceButton.onClick.AddListener(Advance);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void UnbindControls()
    {
        if (buttons != null)
            for (int i = 0; i < Mathf.Min(4, buttons.Length); i++)
                if (buttons[i] != null && choiceListeners[i] != null) buttons[i].onClick.RemoveListener(choiceListeners[i]);
        if (advanceButton != null) advanceButton.onClick.RemoveListener(Advance);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    private void RestoreChoiceColors()
    {
        if (buttons == null) return;
        for (int i = 0; i < Mathf.Min(4, buttons.Length); i++)
            if (buttons[i] != null && buttons[i].targetGraphic != null) buttons[i].targetGraphic.color = choiceColors[i];
    }

    public void ConfigurePortrait(Image image)
    {
        portrait = image;
        Place(image.rectTransform, new Vector2(.025f, .17f), new Vector2(.18f, .85f));
        image.preserveAspect = true;
    }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    public bool TryOpen(StoryBeatData data, Action<bool> onCompleted, Sprite speakerPortrait = null)
    {
        if (data == null || !StoryProgress.CanStart(data.beatId)) return false;
        bool fromJson = data.TryGetConversation(out var parsed);
        if (!fromJson && !data.IsValid) return false;
        if (data.conversationJson != null && !fromJson)
            Debug.LogWarning("Invalid conversation JSON; using the authored fallback for " + data.beatId, this);
        return OpenSession(data, parsed, onCompleted, speakerPortrait);
    }

    // Other NPCs can use the same authored view without registering a Map 1 story beat.
    public bool TryOpenConversation(TextAsset source, Action<bool> onCompleted, Sprite speakerPortrait = null)
    {
        if (source == null || !NpcQuestionConversation.TryParse(source.text, null, out var parsed)) return false;
        return OpenSession(null, parsed, onCompleted, speakerPortrait);
    }

    public bool TryOpenResearchDocument(ResearchDocument.Document source, Action<int> onCompleted)
    {
        if (source == null || source.questions == null || source.questions.Length != 5 || onCompleted == null) return false;
        var parsed = new NpcQuestionConversation
        {
            id = "research_document", speaker = source.title,
            introduction = new NpcQuestionConversation.Dialogue { dialogue_en = source.introduction, dialogue_vi = source.introductionVietnamese },
            completion = new NpcQuestionConversation.Completion { after_en = source.destination, after_vi = source.destinationVietnamese },
            questions = new NpcQuestionConversation.Question[5]
        };
        for (int i = 0; i < 5; i++)
        {
            var q = source.questions[i];
            if (q == null || q.answers == null || q.correctIndex < 0 || q.correctIndex >= q.answers.Length) return false;
            var distractors = new System.Collections.Generic.List<string>();
            for (int j = 0; j < q.answers.Length; j++) if (j != q.correctIndex) distractors.Add(q.answers[j]);
            parsed.questions[i] = new NpcQuestionConversation.Question
            {
                id = "research_" + i, type = "multiple_choice", skill = "reading",
                dialogue_en = q.text, dialogue_vi = q.vietnamese,
                correct = q.answers[q.correctIndex], distractors = distractors.ToArray(),
                explanation_en = q.explanation, explanation_vi = q.explanationVietnamese
            };
        }
        return OpenSession(null, parsed, null, null, onCompleted, source);
    }

    private bool OpenSession(StoryBeatData data, NpcQuestionConversation parsed, Action<bool> onCompleted, Sprite speakerPortrait, Action<int> onScored = null, ResearchDocument.Document research = null)
    {
        if (open || !enabled || !HasReferences || !GameplayState.BeginDialogue(this)) return false;
        beat = data; conversation = parsed; questionIndex = 0;
        pendingDialogueGrammar.Clear();
        currentPageGrammar = null;
        researchDocument = research;
        selectedConsent = null;
        completed = onCompleted; declines = 0;
        scoredCompletion = onScored;
        correctAnswers = 0;
        answers = conversation == null ? data.ShuffledAnswers() : null;
        previousTimeScale = Time.timeScale;
        open = true;
        Time.timeScale = 0;
        Canvas ownerCanvas = GetComponentInParent<Canvas>(true);
        if (ownerCanvas != null) ownerCanvas.gameObject.SetActive(true);
        gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy) { Close(); return false; }

        if (portrait != null)
        {
            portrait.sprite = speakerPortrait != null ? speakerPortrait : authoredPortrait;
            portrait.gameObject.SetActive(portrait.sprite != null && (researchDocument == null || researchDocument.showImage));
        }
        if (nameText != null) nameText.text = conversation != null ? conversation.speaker : beat.speaker;
        step = Step.Intro;
        SetText(conversation != null ? conversation.introduction.dialogue_en : beat.introEnglish,
            conversation != null ? conversation.introduction.dialogue_vi : beat.introVietnamese);
        SetChoices();
        if (!editorPreview) onOpened.Invoke();
        return open;
    }

    private void Update()
    {
        if (!open) return;
        if (GameplayState.Current != GameplayMode.Dialogue) { Close(); return; }
        if (StoryClueJournal.IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.C)) Advance();
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) ||
                Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i))) Choose(i);
    }

    public void Advance()
    {
        if (!open || GameplayState.Current != GameplayMode.Dialogue) return;
        if (step == Step.Success) { Close(); return; }
        if (step == Step.QuestionFeedback) { questionIndex++; ShowQuestion(); return; }
        if (step == Step.ConsentReply)
        {
            if (consentAccepted) ShowQuestion();
            else if (repeatConsent) ShowConsent();
            else Close();
            return;
        }
        if (step != Step.Intro) return;
        if (conversation != null ? conversation.consent != null : beat.requiresConsent) ShowConsent();
        else ShowQuestion();
    }

    private void ShowConsent()
    {
        step = Step.Consent;
        if (conversation != null)
        {
            SetText(conversation.consent.dialogue_en, conversation.consent.dialogue_vi);
            SetChoices(Array.ConvertAll(conversation.consent.choices, choice => choice.label_en));
            if (declines >= 2)
            {
                int yes = Array.FindIndex(conversation.consent.choices, choice => choice.id == "yes");
                if (buttons[yes].targetGraphic != null) buttons[yes].targetGraphic.color = consentHighlightColor;
            }
        }
        else
        {
            SetText("Agent, will you help me protect this city?", "Đặc vụ, anh có đồng ý giúp tôi bảo vệ thành phố này không?");
            SetChoices("Tôi đồng ý.", "Tôi chưa sẵn sàng.");
        }
    }

    private void ShowQuestion()
    {
        // The archaeologist can give a mission through dialogue without a quiz.
        if (conversation != null && conversation.questions.Length == 0)
        {
            step = Step.Success;
            SetText(conversation.completion.after_en, conversation.completion.after_vi);
            SetChoices();
            return;
        }
        step = Step.Question;
        ReleaseQuestionEvent();
        var question = conversation != null ? conversation.questions[questionIndex] : null;
        if (question != null) answers = question.ShuffledAnswers();
        SetText(question != null ? question.dialogue_en : beat.questionEnglish,
            question != null ? question.dialogue_vi : beat.questionVietnamese);
        SetChoices(answers);
        eventQuestion = ScriptableObject.CreateInstance<QuestionData>();
        eventQuestion.id = question != null ? question.id : beat.beatId;
        eventQuestion.skillTag = question != null ? question.skill : "vocabulary";
        eventQuestion.content = question != null ? question.dialogue_en : beat.questionEnglish + "\n" + beat.questionVietnamese;
        eventQuestion.answers = (string[])answers.Clone();
        eventQuestion.correctIndex = Array.IndexOf(answers, question != null ? question.correct : beat.correct);
        eventQuestion.explanation = question != null ? question.explanation_en : beat.explanation;
        QuizEvents.RaiseOpened(eventQuestion);
    }

    public void Choose(int index)
    {
        if (!open || GameplayState.Current != GameplayMode.Dialogue || index < 0 || index >= 4 ||
            !buttons[index].gameObject.activeSelf) return;
        if (step == Step.Consent)
        {
            if (conversation != null)
            {
                var choice = conversation.consent.choices[index];
                selectedConsent = choice;
                consentAccepted = choice.id == "yes"; repeatConsent = choice.loop;
                if (!consentAccepted) declines++;
                step = Step.ConsentReply;
                SetText(choice.reply_en, choice.reply_vi);
                SetChoices();
                return;
            }
            if (index == 0) ShowQuestion();
            else
            {
                declines++;
                SetText("I will wait. The city still needs you, Agent.",
                    "Tôi sẽ đợi. Thành phố vẫn cần anh, đặc vụ. Khi sẵn sàng, hãy chọn đồng ý.");
                SetChoices("Tôi đồng ý.", "Tôi chưa sẵn sàng.");
                if (declines >= 2 && buttons[0].targetGraphic != null) buttons[0].targetGraphic.color = consentHighlightColor;
            }
            return;
        }
        if (step != Step.Question) return;
        if (!editorPreview)
            LearningJournal.RecordGrammar(journalMapId, journalMapTitle, currentPageGrammar);
        var question = conversation != null ? conversation.questions[questionIndex] : null;
        if (scoredCompletion != null)
        {
            bool correct = answers[index] == question.correct;
            if (correct) correctAnswers++;
            bool finalQuestion = questionIndex == conversation.questions.Length - 1;
            step = finalQuestion ? Step.Success : Step.QuestionFeedback;
            int wrong = conversation.questions.Length - correctAnswers;
            string summary = finalQuestion
                ? $"\nResult: {correctAnswers}/5 correct.\n" + conversation.completion.after_en + "\n" +
                    (wrong > 3 ? "Containment failed: monsters are invulnerable and kill in one hit. Escape!"
                        : $"Monsters: {100 + wrong * 50} HP, {20 + wrong * 15} damage.") : "";
            SetText((correct ? "Correct. " : "Incorrect. ") + question.explanation_en + summary,
                question.explanation_vi + (finalQuestion ? "\n" + conversation.completion.after_vi +
                $"\nĐúng {correctAnswers}/5 câu. Sai {wrong} câu." +
                (wrong > 3 ? " Quái bất tử và hạ bạn bằng một đòn. Hãy chạy thoát!" : "") : ""));
            SetChoices();
            if (!editorPreview) QuizEvents.RaiseAnswered(correct, eventQuestion);
            if (!editorPreview) { if (correct) onCorrect.Invoke(); else onWrong.Invoke(); }
            return;
        }
        if (answers[index] != (question != null ? question.correct : beat.correct))
        {
            SetText(question != null ? question.dialogue_en + "\n" + question.wrong_hint_en : beat.questionEnglish,
                question != null ? question.dialogue_vi + (string.IsNullOrEmpty(question.wrong_hint_vi) ? "" : "\n" + question.wrong_hint_vi)
                    : beat.questionVietnamese + "\n" + beat.wrongHint);
            if (!editorPreview) QuizEvents.RaiseAnswered(false, eventQuestion);
            if (!editorPreview) onWrong.Invoke();
            return;
        }
        bool last = conversation == null || questionIndex == conversation.questions.Length - 1;
        step = last ? Step.Success : Step.QuestionFeedback;
        if (question != null)
            SetText(question.explanation_en + "\n" + question.after_en + (last ? "\n" + conversation.completion.after_en : ""),
                last ? conversation.completion.after_vi : question.explanation_vi + "\n" + question.after_vi);
        else SetText(beat.afterEnglish, beat.explanation + "\n" + beat.afterVietnamese);
        SetChoices();
        if (!editorPreview) QuizEvents.RaiseAnswered(true, eventQuestion);
        if (!editorPreview) onCorrect.Invoke();
    }

    private void SetText(string english, string vietnamese)
    {
        plainEnglishText = english;
        // Research questions are reading practice; vocabulary remains available as a guide.
        if (researchDocument != null && step != Step.Intro) vietnamese = "";
        englishText.text = english; vietnameseText.text = vietnamese;
        vietnameseText.gameObject.SetActive(showVietnamese && !string.IsNullOrWhiteSpace(vietnamese));
        englishText.maxVisibleCharacters = vietnameseText.maxVisibleCharacters = int.MaxValue;
        RefreshLearningNotes();
    }

    private void RefreshLearningNotes()
    {
        // Support the authored Vocab object and older combined learning-note widgets.
        if (vocabGrammarText == null)
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                if (string.Equals(text.name.Trim(), "Vocab", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.name.Trim(), "Text Vocab", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.name.Trim(), "Vocab_gammar", StringComparison.OrdinalIgnoreCase))
                { vocabGrammarText = text; break; }
        var notes = new System.Text.StringBuilder();
        ResearchDocument.Vocabulary[] vocabulary = null;
        string grammar = null;
        string grammarId = null, groupId = null;
        if (researchDocument != null)
        {
            if (step == Step.Intro)
            {
                vocabulary = researchDocument.introductionVocabulary;
                grammar = researchDocument.introductionGrammar;
                grammarId = researchDocument.introductionGrammarId; groupId = researchDocument.introductionGroupId;
            }
            else if (step == Step.Success)
            {
                vocabulary = researchDocument.destinationVocabulary;
                grammar = researchDocument.destinationGrammar;
                grammarId = researchDocument.destinationGrammarId; groupId = researchDocument.destinationGroupId;
            }
            else
            {
                var question = researchDocument.questions[questionIndex];
                vocabulary = question.vocabulary;
                grammar = question.grammar;
                grammarId = question.grammarId; groupId = question.groupId;
            }
        }
        else if (conversation != null)
        {
            if (step == Step.Success)
            {
                vocabulary = conversation.completion.vocabulary;
                grammar = conversation.completion.grammar;
                grammarId = conversation.completion.grammarId; groupId = conversation.completion.groupId;
            }
            else if (step == Step.ConsentReply && selectedConsent != null)
            {
                vocabulary = selectedConsent.vocabulary;
                grammar = selectedConsent.grammar;
                grammarId = selectedConsent.grammarId; groupId = selectedConsent.groupId;
            }
            else
            {
                NpcQuestionConversation.Dialogue page = step == Step.Intro ? conversation.introduction
                    : step == Step.Consent ? conversation.consent
                    : questionIndex < conversation.questions.Length ? conversation.questions[questionIndex] : null;
                if (page != null)
                {
                    vocabulary = page.vocabulary; grammar = page.grammar;
                    grammarId = page.grammarId; groupId = page.groupId;
                }
            }
        }
        currentPageGrammar = new LearningJournal.GrammarNote { Id = grammarId, GroupId = groupId, Text = grammar };
        if (!editorPreview)
        {
            LearningJournal.Record(journalMapId, journalMapTitle, JournalVocabularyCatalog.Get(
                journalMapId, conversation != null ? conversation.id : beat != null ? beat.beatId : null,
                JournalPageId()), null);
            if ((step == Step.Intro || step == Step.Consent || step == Step.ConsentReply || step == Step.Success) &&
                !string.IsNullOrWhiteSpace(grammar) && !pendingDialogueGrammar.Exists(note => note.Text == grammar))
                pendingDialogueGrammar.Add(currentPageGrammar);
        }
        englishText.richText = true;
        englishText.text = HighlightVocabulary(plainEnglishText, vocabulary, vocabularyHighlightColor);
        if (grammarNotesText == null)
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                if (string.Equals(text.name.Trim(), "Grammar Notes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.name.Trim(), "Grammar", StringComparison.OrdinalIgnoreCase))
                { grammarNotesText = text; break; }
        if (grammarNotesText != null)
        {
            grammarNotesText.text = string.IsNullOrWhiteSpace(grammar) ? "" : grammar.Trim();
            grammarNotesText.richText = false;
            grammarNotesText.maxVisibleCharacters = int.MaxValue;
            grammarNotesText.gameObject.SetActive(!string.IsNullOrWhiteSpace(grammar));
        }
        if (vocabGrammarText == null) return;
        bool combined = string.Equals(vocabGrammarText.name, "Vocab_gammar", StringComparison.OrdinalIgnoreCase);
        if (combined && !string.IsNullOrWhiteSpace(grammar)) notes.AppendLine("Grammar:").Append(grammar);
        if (vocabulary != null)
            foreach (var entry in vocabulary)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.word)) continue;
                if (notes.Length > 0) notes.AppendLine();
                string line = LearningJournal.FormatVocabulary(entry.word, entry.meaning);
                int separator = line.IndexOf(':');
                string term = separator < 0 ? line : line.Substring(0, separator);
                notes.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(vocabularyHighlightColor))
                    .Append('>').Append(EscapeMarkup(term)).Append("</color>");
                if (separator >= 0) notes.Append(EscapeMarkup(line.Substring(separator)));
            }
        vocabGrammarText.text = notes.ToString();
        vocabGrammarText.richText = true;
        vocabGrammarText.maxVisibleCharacters = int.MaxValue;
        vocabGrammarText.gameObject.SetActive(notes.Length > 0);
    }

    private static string EscapeMarkup(string text) => (text ?? "").Replace("<", "‹").Replace(">", "›");

    private string JournalPageId()
    {
        if (step == Step.Intro) return "introduction";
        if (step == Step.Success) return "completion";
        if (step == Step.Consent) return "consent";
        if (step == Step.ConsentReply) return "reply_" + selectedConsent?.id;
        if (researchDocument != null) return "question_" + (questionIndex + 1);
        return conversation != null && questionIndex < conversation.questions.Length
            ? conversation.questions[questionIndex].id : "question";
    }

    public static string HighlightVocabulary(string text, ResearchDocument.Vocabulary[] vocabulary, Color color)
    {
        string content = EscapeMarkup(text);
        if (vocabulary == null || vocabulary.Length == 0) return content;
        var terms = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in vocabulary)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.word)) continue;
            string term = entry.word.Trim();
            if (seen.Add(term)) terms.Add(term);
        }
        if (terms.Count == 0) return content;
        // Prefer a complete phrase over a shorter word inside it; never match inside another word.
        terms.Sort((a, b) => b.Length.CompareTo(a.Length));
        string pattern = @"(?<![\p{L}\p{N}_])(?:" + string.Join("|", terms.ConvertAll(term =>
            string.Join(@"\s+", Array.ConvertAll(EscapeMarkup(term).Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries), part => Regex.Escape(part))))) + @")(?![\p{L}\p{N}_])";
        string hex = ColorUtility.ToHtmlStringRGB(color);
        return Regex.Replace(content, pattern, match => "<color=#" + hex + ">" + match.Value + "</color>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private void SetChoices(params string[] choices)
    {
        RestoreChoiceColors();
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].gameObject.SetActive(i < choices.Length);
            if (i < choices.Length)
            {
                labels[i].text = (i + 1) + ". " + choices[i];
                FormatAnswerLabel(labels[i], buttons[i]);
            }
        }
        if (advanceButton != null) advanceButton.gameObject.SetActive(choices.Length == 0);
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (promptText != null) promptText.text = choices.Length == 0
            ? "C to continue · Esc to close" : "Click or press 1–" + choices.Length + " to choose · Esc to close";
        if (promptText != null && conversation != null && step == Step.Question)
            promptText.text += " · " + (questionIndex + 1) + "/" + conversation.questions.Length;
    }

    private static void FormatAnswerLabel(TMP_Text label, Button button)
    {
        var rect = label.rectTransform;
        var buttonRect = (RectTransform)button.transform;
        if (rect.parent != button.transform) rect.SetParent(button.transform, false);
        // Counter uneven ancestor scaling on the text only, preserving the authored button frame.
        var canvas = button.GetComponentInParent<Canvas>();
        Vector3 reference = canvas != null ? canvas.rootCanvas.transform.lossyScale : Vector3.one;
        Vector3 inherited = button.transform.lossyScale;
        float sx = Mathf.Abs(inherited.x) > .0001f ? Mathf.Abs(reference.x / inherited.x) : 1f;
        float sy = Mathf.Abs(inherited.y) > .0001f ? Mathf.Abs(reference.y / inherited.y) : 1f;
        rect.localScale = new Vector3(sx, sy, 1f);
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(Mathf.Max(1f, buttonRect.rect.width / sx - 12f),
            Mathf.Max(1f, buttonRect.rect.height / sy - 8f));
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 25f;
        label.fontSizeMax = 28f;
        label.characterWidthAdjustment = 0f;
        label.characterSpacing = 0f;
        label.wordSpacing = 0f;
        label.margin = Vector4.zero;
        label.richText = false;
        label.raycastTarget = false;
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        bool passed = step == Step.Success && GameplayState.Current == GameplayMode.Dialogue;
        bool finishedDialogue = passed || step == Step.ConsentReply && !consentAccepted && !repeatConsent;
        if (!editorPreview && finishedDialogue)
            foreach (var grammar in pendingDialogueGrammar)
                LearningJournal.RecordGrammar(journalMapId, journalMapTitle, grammar);
        Action<bool> result = completed;
        Action<int> scoredResult = scoredCompletion;
        int score = correctAnswers;
        scoredCompletion = null;
        completed = null;
        Time.timeScale = previousTimeScale;
        GameplayState.EndDialogue(this);
        gameObject.SetActive(false);
        try
        {
            if (passed && beat != null) passed = StoryProgress.Complete(beat.beatId);
            result?.Invoke(passed);
            scoredResult?.Invoke(passed ? score : -1);
            if (!editorPreview) onClosed.Invoke();
        }
        finally
        {
            ReleaseQuestionEvent();
            editorPreview = false;
        }
    }

    private void ReleaseQuestionEvent()
    {
        if (eventQuestion == null) return;
        QuizEvents.RaiseClosed();
        if (Application.isPlaying) Destroy(eventQuestion);
        else DestroyImmediate(eventQuestion);
        eventQuestion = null;
    }

    private void OnDisable()
    {
        Close();
        researchDocument = null;
        if (vocabGrammarText != null) vocabGrammarText.text = string.Empty;
        if (grammarNotesText != null) grammarNotesText.text = string.Empty;
        UnbindControls();
        RestoreChoiceColors();
        if (portrait != null)
        {
            portrait.sprite = authoredPortrait;
            portrait.gameObject.SetActive(authoredPortraitVisible);
        }
    }
}

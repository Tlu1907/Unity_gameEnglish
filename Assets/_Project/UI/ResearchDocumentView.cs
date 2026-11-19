using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>References authored UI only; never creates or rearranges controls at runtime.</summary>
public sealed class ResearchDocumentView : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Button[] answerButtons = new Button[4];
    [SerializeField] private TMP_Text[] answerLabels = new TMP_Text[4];
    [SerializeField] private Button continueButton;
    [SerializeField] private Button closeButton;
    private readonly UnityAction[] choices = new UnityAction[4];
    private ResearchDocument owner;
    private void Start()
    {
        if (owner == null && panelRoot != null) panelRoot.SetActive(false);
    }
    public bool IsConfigured
    {
        get
        {
            if (panelRoot == null || titleText == null || bodyText == null || feedbackText == null ||
                continueButton == null || closeButton == null || answerButtons.Length != 4 || answerLabels.Length != 4) return false;
            for (int i = 0; i < 4; i++) if (answerButtons[i] == null || answerLabels[i] == null) return false;
            return true;
        }
    }
    private void OnEnable()
    {
        for (int i = 0; i < 4; i++)
        {
            int choice = i;
            choices[i] = () => owner?.Choose(choice);
            if (i < answerButtons.Length && answerButtons[i] != null) answerButtons[i].onClick.AddListener(choices[i]);
        }
        continueButton?.onClick.AddListener(Continue);
        closeButton?.onClick.AddListener(Cancel);
    }
    private void OnDisable()
    {
        for (int i = 0; i < 4; i++)
            if (i < answerButtons.Length && answerButtons[i] != null && choices[i] != null)
                answerButtons[i].onClick.RemoveListener(choices[i]);
        continueButton?.onClick.RemoveListener(Continue);
        closeButton?.onClick.RemoveListener(Cancel);
        var previous = owner;
        owner = null;
        previous?.Close();
    }
    public void Show(ResearchDocument document)
    {
        owner = document;
        gameObject.SetActive(true);
        panelRoot.SetActive(true);
        Refresh();
    }
    public void Hide()
    {
        owner = null;
        if (panelRoot != null) panelRoot.SetActive(false);
    }
    public void Refresh()
    {
        if (owner == null || !owner.IsOpen) return;
        var data = owner.Content;
        int index = owner.QuestionIndex;
        bool result = index >= data.questions.Length;
        titleText.text = data.title;
        if (progressText != null) progressText.text = result ? $"Result: {owner.CorrectAnswers}/5" : $"Question {index + 1}/5";
        bodyText.text = result ? data.destination : data.introduction + "\n\n" + data.questions[index].text;
        feedbackText.text = result
            ? (5 - owner.CorrectAnswers > 3 ? "The monsters are invulnerable and kill in one hit. Escape!"
                : $"Monster HP: {100 + (5 - owner.CorrectAnswers) * 50} | Damage: {20 + (5 - owner.CorrectAnswers) * 15}")
            : owner.HasAnswered ? "Answer: " + data.questions[index].answers[data.questions[index].correctIndex] + "\n" + data.questions[index].explanation : "";
        foreach (var text in new[] { titleText, bodyText, feedbackText, progressText })
            if (text != null) text.maxVisibleCharacters = int.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            bool visible = !result && i < data.questions[index].answers.Length;
            answerButtons[i].gameObject.SetActive(visible);
            answerButtons[i].interactable = !owner.HasAnswered;
            if (visible) answerLabels[i].text = data.questions[index].answers[i];
        }
        continueButton.gameObject.SetActive(result || owner.HasAnswered);
        var caption = continueButton.GetComponentInChildren<TMP_Text>(true);
        if (caption != null) caption.text = result ? "Finish reading" : "Continue";
    }
    private void Continue()
    {
        if (owner == null) return;
        if (owner.QuestionIndex == 5) owner.Finish();
        else owner.Advance();
    }
    private void Cancel() => owner?.Close();
}

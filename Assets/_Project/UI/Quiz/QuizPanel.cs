using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class QuizPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button[] answerButtons;
    private readonly Dictionary<Button, UnityAction> bindings = new Dictionary<Button, UnityAction>();
    private QuizController controller;
    private int selectedIndex = -1;
    private bool dispatching;
    public void ConfigureContent(TMP_Text text) { if (text != null) contentText = text; }

    public void ConfigureLegacyLayout()
    {
        if (answerButtons != null && answerButtons.Length > 0) return;
        var answers = new List<Button>();
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            bool cancel = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == nameof(OnCancelClicked)) cancel = true;
            if (!cancel && !button.name.Contains("Cancel")) answers.Add(button);
        }
        answerButtons = answers.ToArray();
    }
    public void Show(QuizController owner, QuestionData question)
    {
        controller = owner;
        selectedIndex = -1;
        ConfigureLegacyLayout();
        Unbind();
        if (answerButtons.Length < question.answers.Length)
            Debug.LogWarning($"QuizPanel has {answerButtons.Length} answer buttons for {question.answers.Length} answers. Assign additional existing UI buttons in the Inspector.", this);
        if (contentText != null) contentText.text = question.content;
        if (feedbackText != null) feedbackText.text = string.Empty;
        for (int i = 0; i < answerButtons.Length; i++)
        {
            Button button = answerButtons[i];
            if (button == null) continue;
            bool available = i < question.answers.Length;
            button.gameObject.SetActive(available);
            if (!available) continue;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = question.answers[i];
            int index = i;
            UnityAction callback = () => Submit(index);
            bindings.Add(button, callback);
            button.onClick.AddListener(callback);
        }
        gameObject.SetActive(true);
    }
    private void Unbind()
    {
        foreach (var binding in bindings)
            if (binding.Key != null) binding.Key.onClick.RemoveListener(binding.Value);
        bindings.Clear();
    }
    private void Submit(int index)
    {
        if (dispatching || controller == null) return;
        dispatching = true;
        selectedIndex = index;
        try { controller.SubmitAnswer(index); }
        finally { dispatching = false; }
    }
    public void OnCancelClicked()
    {
        if (controller != null) controller.CloseQuestion();
    }
    // Retained for persistent UnityEvents; an answer Button's indexed binding owns submission.
    public void OnSubmitClicked()
    {
        if (selectedIndex >= 0 && bindings.Count == 0) Submit(selectedIndex);
    }
    public void ShowFeedback(bool correct, string explanation)
    {
        string message = (correct ? "Correct. " : "Incorrect. ") + explanation;
        if (feedbackText != null) feedbackText.text = message;
        else Debug.Log(message);
    }
    public void Hide() { Unbind(); gameObject.SetActive(false); }
    protected virtual void OnDisable()
    {
        Unbind();
        if (controller != null && controller.CurrentQuestion != null) controller.CloseQuestion();
    }
}

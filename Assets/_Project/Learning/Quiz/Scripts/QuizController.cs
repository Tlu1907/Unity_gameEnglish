using System;
using TMPro;
using UnityEngine;

public class QuizController : MonoBehaviour
{
    public static QuizController Instance { get; private set; }
    public static bool IsQuestionOpen => Instance != null && Instance.currentQuestion != null;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text contentText;
    private QuizPanel view;
    private QuestionData currentQuestion;
    private Action<bool> completed;
    private bool submitting;
    public QuestionData CurrentQuestion => currentQuestion;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        ResolveView();
        if (panel != null) panel.SetActive(false);
    }
    public void ResolveView(bool searchScene = true)
    {
        // One-time type-based fallback supports old scenes with an inactive panel.
        view = panel != null ? panel.GetComponent<QuizPanel>() : GetComponentInChildren<QuizPanel>(true);
        if (view == null && searchScene) view = FindAnyObjectByType<QuizPanel>(FindObjectsInactive.Include);
        if (panel == null && view != null) panel = view.gameObject;
        if (view != null) view.ConfigureContent(contentText);
    }
    public void ShowQuestion(QuestionData question) => TryShowQuestion(question, null);
    public bool TryShowQuestion(QuestionData question, Action<bool> onCompleted)
    {
        if (Instance != this || currentQuestion != null || question == null || view == null ||
            question.answers == null || question.answers.Length == 0 || question.correctIndex < 0 ||
            question.correctIndex >= question.answers.Length || !GameplayState.BeginQuiz(this)) return false;
        currentQuestion = question;
        completed = onCompleted;
        view.Show(this, question);
        QuizEvents.RaiseOpened(question);
        return true;
    }
    public void SubmitAnswer(int selectedIndex)
    {
        if (submitting || currentQuestion == null || GameplayState.Current != GameplayMode.Quiz ||
            selectedIndex < 0 || selectedIndex >= currentQuestion.answers.Length) return;
        submitting = true;
        QuestionData question = currentQuestion;
        bool correct = selectedIndex == question.correctIndex;
        Action<bool> result = completed;
        completed = null;
        try
        {
            view.ShowFeedback(correct, question.explanation);
            result?.Invoke(correct);
            QuizEvents.RaiseAnswered(correct, question);
        }
        finally { submitting = false; CloseQuestion(); }
    }
    public void CloseQuestion()
    {
        if (currentQuestion == null) return;
        currentQuestion = null;
        Action<bool> result = completed;
        completed = null;
        try { result?.Invoke(false); }
        finally
        {
            if (view != null) view.Hide();
            GameplayState.EndQuiz(this);
            QuizEvents.RaiseClosed();
        }
    }
    protected virtual void OnDisable()
    {
        if (Instance == this) CloseQuestion();
    }
    protected virtual void OnDestroy()
    {
        if (Instance == this) { CloseQuestion(); Instance = null; }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;
}

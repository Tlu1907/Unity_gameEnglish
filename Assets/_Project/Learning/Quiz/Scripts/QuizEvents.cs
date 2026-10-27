using System;

public static class QuizEvents
{
    public static event Action<QuestionData> OnQuizOpened;
    public static event Action<bool, QuestionData> OnAnswerSubmitted;
    public static event Action OnQuizClosed;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        OnQuizOpened = null;
        OnAnswerSubmitted = null;
        OnQuizClosed = null;
    }

    public static void RaiseOpened(QuestionData question) => OnQuizOpened?.Invoke(question);
    public static void RaiseAnswered(bool isCorrect, QuestionData question) => OnAnswerSubmitted?.Invoke(isCorrect, question);
    public static void RaiseClosed() => OnQuizClosed?.Invoke();
}

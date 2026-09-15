using UnityEngine;

public static class MonsterFreezeController
{
    private static float freezeUntilTime;

    public static bool IsFrozen
    {
        get { return Time.time < freezeUntilTime; }
    }

    public static void TriggerFreeze(float duration)
    {
        freezeUntilTime = Time.time + duration;
    }

    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        QuizEvents.OnQuizOpened += HandleQuizOpened;
        QuizEvents.OnQuizClosed += HandleQuizClosed;
    }

    private static void HandleQuizOpened(QuestionData question)
    {
        if (question == null)
            return;

        TriggerFreeze(question.freezeDuration > 0f ? question.freezeDuration : 3f);
    }

    private static void HandleQuizClosed()
    {
        freezeUntilTime = 0f;
    }
}

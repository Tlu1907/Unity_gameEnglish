using UnityEngine;

/// <summary>Wrong submitted answers alert nearby enemies; closing a quiz does not.</summary>
[RequireComponent(typeof(Chest))]
public sealed class ChestQuizAlarm : MonoBehaviour
{
    [SerializeField] private QuestionData question;
    [SerializeField, Min(0)] private float radius = 6f;
    [SerializeField, Min(0)] private float chaseSeconds = 8f;
    private void OnEnable() => QuizEvents.OnAnswerSubmitted += Answered;
    private void OnDisable() => QuizEvents.OnAnswerSubmitted -= Answered;
    private void Answered(bool correct, QuestionData answered)
    {
        if (correct || answered != question || GetComponent<Chest>().IsOpened) return;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude))
            if (((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude <= radius * radius)
                enemy.Alert(chaseSeconds);
        // Only a submitted wrong answer consumes the chest. Closing the quiz raises no answer event.
        GetComponent<Chest>().FinishFailedQuestion(answered);
    }
}

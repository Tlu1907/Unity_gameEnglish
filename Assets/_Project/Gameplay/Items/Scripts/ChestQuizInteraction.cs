using UnityEngine;

/// <summary>Starts a quiz when the grid interactor reaches the chest; Chest owns opened/locked state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Chest))]
public sealed class ChestQuizInteraction : MonoBehaviour
{
    [SerializeField] private QuestionData question;
    [SerializeField] private QuizController controller;
    [SerializeField, Min(0f)] private float retryDelay = .5f;
    private Chest chest;
    private bool pending;
    private float nextRequest;
    private bool warned;
    private void Awake() => ConfigureLock();
    private void ConfigureLock()
    {
        if (chest == null) chest = GetComponent<Chest>();
        // Even a missing question must not accidentally turn this into a free chest.
        chest.SetQuestionId(question != null ? question.id : string.Empty);
    }
    public void RequestQuestion()
    {
        ConfigureLock();
        if (chest.IsOpened || pending || !GameplayState.CanAct || Time.unscaledTime < nextRequest) return;
        if (controller == null) controller = QuizController.Instance;
        if (question == null || controller == null)
        {
            if (!warned) Debug.LogWarning("Chest quiz: assign QuestionData and a scene QuizController. Chest remains locked.", this);
            warned = true;
            return;
        }
        pending = true;
        if (!controller.TryShowQuestion(question, HandleResult))
        {
            pending = false;
            nextRequest = Time.unscaledTime + retryDelay;
        }
    }
    private void HandleResult(bool correct)
    {
        if (this == null) return;
        pending = false;
        nextRequest = Time.unscaledTime + retryDelay;
        if (correct) chest.UnlockForQuestion(question);
    }
}

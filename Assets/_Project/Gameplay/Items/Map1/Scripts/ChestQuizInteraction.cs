using UnityEngine;

/// <summary>Starts a quiz when the grid interactor reaches the chest; Chest owns opened/locked state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Chest))]
public sealed class ChestQuizInteraction : MonoBehaviour
{
    [SerializeField] private QuestionData question;
    [SerializeField] private QuizController controller;
    [SerializeField] private bool useFlaskQuiz = true;
    [SerializeField] private FlaskEnglishQuizPanel flaskPanel;
    [SerializeField, Min(0f)] private float retryDelay = .5f;
    private Chest chest;
    private bool pending;
    private float nextRequest;
    private bool warned;
    private bool awaitingExit;
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
        if (chest.IsOpened || pending || (useFlaskQuiz && awaitingExit) || !GameplayState.CanAct || Time.unscaledTime < nextRequest) return;
        if (useFlaskQuiz)
        {
            if (flaskPanel == null) flaskPanel = FlaskEnglishQuizPanel.FindOrCreate();
            if (flaskPanel == null)
            {
                if (!warned) Debug.LogWarning("Chest quiz: FlaskEnglishQuiz prefab is missing from Resources.", this);
                warned = true;
                return;
            }
            pending = true;
            if (flaskPanel.TryOpen(HandleResult)) awaitingExit = true;
            else
            {
                pending = false;
                nextRequest = Time.unscaledTime + retryDelay;
            }
            return;
        }
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

    private void OnCollisionExit2D(Collision2D collision) => AllowNextContact(collision.collider);
    private void OnTriggerExit2D(Collider2D other) => AllowNextContact(other);
    private void AllowNextContact(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMovement>() != null) awaitingExit = false;
    }
}

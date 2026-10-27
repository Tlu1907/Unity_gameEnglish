using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInteractor : MonoBehaviour
{
    private GridMovement movement;
    private QuizTrigger pendingQuiz;
    private void OnEnable()
    {
        movement = GetComponent<GridMovement>();
        if (movement != null) movement.StepCompleted += OnStepCompleted;
    }
    private void OnDisable()
    {
        if (movement != null) movement.StepCompleted -= OnStepCompleted;
        pendingQuiz = null;
    }
    public void RequestMove(Vector3 direction, float speed)
    {
        if (movement != null && movement.UsesPhysicsMovement)
        {
            movement.SetMovementInput(direction, speed);
            return;
        }
        if (movement == null || movement.IsMoving || !GameplayState.CanAct) return;
        GetComponent<CharacterAnimation>()?.Face(direction);
        Vector3 target = transform.position + direction * movement.GridSize;
        QuizTrigger quiz = null;
        foreach (Collider2D col in Physics2D.OverlapBoxAll(target, new Vector2(.8f, .8f), 0f))
        {
            quiz = col.GetComponentInParent<QuizTrigger>();
            if (quiz != null) break;
        }
        if (quiz != null && movement.HasGround(target))
        {
            pendingQuiz = quiz;
            if (!movement.TryMove(direction, speed, true)) pendingQuiz = null;
            return;
        }
        Collider2D obstacle = movement.ObstacleAt(target);
        if (obstacle != null)
        {
            IInteractable interactable = obstacle.GetComponentInParent<Chest>();
            interactable?.Interact();
            return;
        }
        movement.TryMove(direction, speed);
    }
    private void OnStepCompleted()
    {
        QuizTrigger quiz = pendingQuiz;
        pendingQuiz = null;
        if (quiz != null) quiz.Interact();
    }
    // Continuous Player movement interacts at actual contacts, not at a future grid cell.
    private void OnCollisionEnter2D(Collision2D collision) => InteractAtContact(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => InteractAtContact(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => InteractAtContact(other);
    private void OnTriggerStay2D(Collider2D other) => InteractAtContact(other);
    private void InteractAtContact(Collider2D other)
    {
        if (movement == null || !movement.UsesPhysicsMovement || !GameplayState.CanAct
            || movement.MovementInput == Vector2.zero) return;
        Chest chest = other.GetComponentInParent<Chest>();
        if (chest != null) { chest.Interact(); return; }
        other.GetComponentInParent<QuizTrigger>()?.Interact();
    }
}

using UnityEngine;

public class QuizTrigger : MonoBehaviour, IInteractable
{
    [SerializeField] private QuestionData question;
    [SerializeField] private TMPro.TMP_Text questionText;
    [SerializeField] private Chest relatedChest;
    [SerializeField] private QuizController controller;
    private bool isCompleted;
    public bool IsCompleted => isCompleted;
    protected virtual void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        gameObject.layer = LayerMask.NameToLayer("Default");
        ConfigureLinks();
    }
    public void ConfigureLinks(bool searchScene = true)
    {
        if (relatedChest == null) relatedChest = GetComponentInChildren<Chest>(true);
        if (relatedChest != null && question != null) relatedChest.SetQuestionId(question.id);
        if (controller == null) controller = transform.root.GetComponentInChildren<QuizController>(true);
        if (controller == null && searchScene) controller = FindAnyObjectByType<QuizController>(FindObjectsInactive.Include);
    }
    public void TryActivate() => Interact();
    public virtual void Interact()
    {
        if (isCompleted || !GameplayState.CanAct) return;
        if (controller == null) controller = QuizController.Instance;
        if (controller != null) controller.TryShowQuestion(question, HandleResult);
        else Debug.LogWarning("QuizTrigger requires a QuizController in the scene.", this);
    }
    protected void HandleResult(bool correct)
    {
        if (!correct || isCompleted || this == null) return;
        isCompleted = true;
        if (relatedChest != null) relatedChest.UnlockForQuestion(question);
        gameObject.SetActive(false);
    }
}

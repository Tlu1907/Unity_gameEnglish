using UnityEngine;

public class Chest : MonoBehaviour
{
    [Header("Chest state")]
    public GameObject openChestPrefab; // prefab chest đã mở, kéo vào Inspector

    [SerializeField] private string questionId;
    [SerializeField] private string openAnimationTrigger = "Open";

    private Animator animator;
    private bool isOpened = false;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        QuizEvents.OnAnswerSubmitted += HandleAnswerSubmitted;
    }

    private void OnDisable()
    {
        QuizEvents.OnAnswerSubmitted -= HandleAnswerSubmitted;
    }

    public void Open()
    {
        if (isOpened) return;
        isOpened = true;

        if (animator != null)
        {
            animator.SetBool(openAnimationTrigger, true);
            if (animator.runtimeAnimatorController != null)
                animator.Play(openAnimationTrigger, 0, 0f);
        }

        ChangeToOpenVisual();
    }

    public void SetQuestionId(string id)
    {
        questionId = id;
    }

    private void HandleAnswerSubmitted(bool isCorrect, QuestionData answeredQuestion)
    {
        if (!isCorrect || answeredQuestion == null || string.IsNullOrEmpty(questionId))
            return;

        if (questionId == answeredQuestion.id)
        {
            Open();
        }
    }

    private void ChangeToOpenVisual()
    {
        if (openChestPrefab != null)
        {
            Instantiate(openChestPrefab, transform.position, transform.rotation, transform.parent);
            gameObject.SetActive(false);
            return;
        }

        // Fallback nếu không có prefab thì vẫn giữ trạng thái "đã mở".
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;
    }
}
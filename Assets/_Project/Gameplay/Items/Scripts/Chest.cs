using System;
using UnityEngine;

public class Chest : MonoBehaviour, IInteractable
{
    public GameObject openChestPrefab;
    [SerializeField] private string questionId;
    [SerializeField] private string openAnimationTrigger = "Open";
    [SerializeField] private bool requiresQuiz;
    [SerializeField] private SpriteRenderer chestRenderer;
    [SerializeField] private Sprite openedSprite;
    private Animator animator;
    private bool isOpened;
    private bool unlocked;
    private bool failedQuestion;
    public bool AllowsReward => !failedQuestion;
    public bool IsOpened => isOpened;
    public bool IsLocked => (requiresQuiz || !string.IsNullOrEmpty(questionId)) && !unlocked;
    public event Action Opened;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        // Resolve sibling/parent trigger even if this Awake runs before the trigger's Awake.
        QuizTrigger trigger = GetComponentInParent<QuizTrigger>();
        if (trigger != null) trigger.ConfigureLinks();
    }
    public void SetQuestionId(string id)
    {
        questionId = id;
        requiresQuiz = true;
    }
    public void UnlockForQuestion(QuestionData question)
    {
        if (isOpened) return;
        if (question == null || string.IsNullOrEmpty(questionId) || question.id != questionId) return;
        unlocked = true;
        Open();
    }
    public void FinishFailedQuestion(QuestionData question)
    {
        if (isOpened || question == null || string.IsNullOrEmpty(questionId) || question.id != questionId) return;
        // Set before Opened fires: progress advances, but reward listeners must not spawn loot.
        failedQuestion = true;
        unlocked = true;
        Open();
    }
    public void Interact()
    {
        if (!GameplayState.CanAct || isOpened) return;
        ChestQuizInteraction quiz = GetComponent<ChestQuizInteraction>();
        if (quiz != null && quiz.enabled) { quiz.RequestQuestion(); return; }
        Open();
    }
    public void Open()
    {
        if (isOpened || IsLocked) return;
        isOpened = true;
        if (chestRenderer == null) chestRenderer = GetComponentInChildren<SpriteRenderer>();
        if (openedSprite != null && chestRenderer != null) chestRenderer.sprite = openedSprite;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (openedSprite == null && animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(openAnimationTrigger, true);
            animator.Play(openAnimationTrigger, 0, 0f);
        }
        // State is set before notification to prevent reentrant/double rewards.
        Opened?.Invoke();
        if (openChestPrefab != null)
        {
            Instantiate(openChestPrefab, transform.position, transform.rotation, transform.parent);
            gameObject.SetActive(false);
        }
        else
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }
}

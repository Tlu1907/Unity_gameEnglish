using UnityEngine;

public class MirrorTreasureItem : MonoBehaviour
{
    [Header("Câu hỏi của gương này")]
    [SerializeField] private QuestionData question;
    [Tooltip("TMP_Text hiển thị preview câu hỏi trong world space nếu có.")]
    [SerializeField] private TMPro.TMP_Text questionText;
    [SerializeField] private Chest relatedChest;

    private bool isCompleted = false;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        gameObject.layer = LayerMask.NameToLayer("Default");

        if (questionText == null)
            questionText = GetComponentInChildren<TMPro.TMP_Text>(includeInactive: true);

        if (question != null && relatedChest == null)
            relatedChest = GetComponentInChildren<Chest>(true);

        if (relatedChest != null && question != null)
            relatedChest.SetQuestionId(question.id);
    }

    private void OnEnable()
    {
        QuizEvents.OnAnswerSubmitted += HandleAnswerSubmitted;
    }

    private void OnDisable()
    {
        QuizEvents.OnAnswerSubmitted -= HandleAnswerSubmitted;
    }

    public void TryActivate()
    {
        if (QuestionManager.IsQuestionOpen || isCompleted)
            return;

        if (QuestionManager.Instance == null)
        {
            QuestionManager manager = FindAnyObjectByType<QuestionManager>();
            if (manager == null)
            {
                Debug.LogWarning("QuestionManager không có sẵn trong scene, đang tự tạo runtime - kiểm tra lại Hierarchy nếu đây không phải điều bạn muốn.");
                GameObject managerObj = new GameObject("QuestionManager");
                managerObj.AddComponent<QuestionManager>();
            }
        }

        if (QuestionManager.Instance != null)
            QuestionManager.Instance.ShowQuestion(question);
    }

    private void HandleAnswerSubmitted(bool isCorrect, QuestionData answeredQuestion)
    {
        if (question == null || answeredQuestion == null)
            return;

        if (question.id == answeredQuestion.id && isCorrect)
        {
            isCompleted = true;

            if (relatedChest != null)
                relatedChest.Open();

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}


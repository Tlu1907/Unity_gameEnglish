using UnityEngine;
using TMPro;

public class QuestionManager : MonoBehaviour
{
    public static QuestionManager Instance;
    public static bool IsQuestionOpen { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text contentText;

    private QuestionData currentQuestion;

    private void Awake()
    {
        Instance = this;
        IsQuestionOpen = false;

        if (panel == null)
        {
            panel = FindGameObjectIncludingInactive("Panel");
            if (panel == null)
                Debug.LogWarning("QuestionManager: chưa gán Panel trong Inspector, đang fallback sang tìm theo tên - hãy gán tay để tránh lỗi khi đổi tên object trong Hierarchy.");
        }

        if (panel != null)
            panel.SetActive(false);

        if (contentText == null && panel != null)
        {
            Transform contentTransform = panel.transform.Find("Content");
            if (contentTransform != null)
                contentText = contentTransform.GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        QuizEvents.OnQuizClosed += HandleQuizClosed;
    }

    private void OnDisable()
    {
        QuizEvents.OnQuizClosed -= HandleQuizClosed;
    }

    public void ShowQuestion(QuestionData question)
    {
        if (question == null)
        {
            Debug.LogWarning("QuestionManager: ShowQuestion được gọi với question null.");
            return;
        }

        currentQuestion = question;

        if (panel == null)
            panel = FindGameObjectIncludingInactive("Panel");

        if (panel != null)
        {
            panel.SetActive(true);
            if (contentText != null)
                contentText.text = question.content;
        }
        else
        {
            Debug.LogWarning("QuestionManager: Không tìm thấy Panel câu hỏi trong scene.");
        }

        IsQuestionOpen = true;
        QuizEvents.RaiseOpened(question);
    }

    public void CloseQuestion()
    {
        IsQuestionOpen = false;
        currentQuestion = null;

        if (panel != null)
            panel.SetActive(false);

        QuizEvents.RaiseClosed();
    }

    public void SubmitAnswer(int selectedIndex)
    {
        if (currentQuestion == null)
            return;

        bool isCorrect = selectedIndex == currentQuestion.correctIndex;
        QuizEvents.RaiseAnswered(isCorrect, currentQuestion);
        CloseQuestion();
    }

    public QuestionData CurrentQuestion => currentQuestion;

    private void HandleQuizClosed()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private GameObject FindGameObjectIncludingInactive(string objectName)
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
        {
            if (t.gameObject.name == objectName)
                return t.gameObject;
        }
        return null;
    }

    private void Reset()
    {
        if (panel == null)
            panel = FindGameObjectIncludingInactive("Panel");
    }
}



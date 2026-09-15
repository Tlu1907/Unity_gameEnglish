using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn script này lên GameObject "Panel".
/// Các nút Cancel/Submit gọi phương thức của script này qua OnClick().
/// </summary>
public class PanelController : MonoBehaviour
{
    private int selectedIndex = -1;

    private void OnEnable()
    {
        BindAnswerButtons();
    }

    private void BindAnswerButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        QuestionData currentQuestion = QuestionManager.Instance != null ? QuestionManager.Instance.CurrentQuestion : null;

        if (currentQuestion == null || currentQuestion.answers == null || currentQuestion.answers.Length == 0)
            return;

        int buttonIndex = 0;
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            string buttonName = button.name;

            // Bỏ qua nút Cancel nếu có, chỉ bind các nút đáp án.
            if (buttonName.Contains("Cancel"))
                continue;

            if (buttonIndex >= currentQuestion.answers.Length)
                break;

            string answerText = currentQuestion.answers[buttonIndex];
            TMP_Text textComponent = button.GetComponentInChildren<TMP_Text>(true);
            if (textComponent != null)
                textComponent.text = answerText;

            int answerValue = buttonIndex;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SubmitAnswer(answerValue));

            buttonIndex++;
        }
    }

    public void OnCancelClicked()
    {
        Debug.Log("[Panel] Cancel: đóng panel, tiếp tục chơi.");

        if (QuestionManager.Instance != null)
            QuestionManager.Instance.CloseQuestion();
        else
            gameObject.SetActive(false);
    }

    public void OnSubmitClicked()
    {
        if (selectedIndex < 0)
            selectedIndex = 0;

        SubmitAnswer(selectedIndex);
    }

    private void SubmitAnswer(int answerIndex)
    {
        selectedIndex = answerIndex;

        if (QuestionManager.Instance == null)
        {
            gameObject.SetActive(false);
            return;
        }

        QuestionData currentQuestion = QuestionManager.Instance.CurrentQuestion;
        if (currentQuestion == null)
        {
            QuestionManager.Instance.CloseQuestion();
            return;
        }

        QuestionManager.Instance.SubmitAnswer(answerIndex);

        if (currentQuestion.explanation != null && currentQuestion.explanation.Length > 0)
            Debug.Log("[Panel] Explanation: " + currentQuestion.explanation);
    }
}

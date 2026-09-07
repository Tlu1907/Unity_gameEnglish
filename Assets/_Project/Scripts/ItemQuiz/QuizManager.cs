using UnityEngine;

public class QuestionManager : MonoBehaviour
{
    public static QuestionManager Instance;

    public GameObject questionPanel; // kéo panel UI (đối tượng "Question" trong Hierarchy) vào đây

    private void Awake()
    {
        Instance = this;
        questionPanel.SetActive(false);
    }

    public void ShowQuestion(string questionId, Chest sourceChest)
    {
        questionPanel.SetActive(true);
        // TODO: load nội dung câu hỏi theo questionId, hiển thị lên UI Text/Button
        // sourceChest có thể dùng lại nếu bạn muốn xử lý logic khi trả lời đúng/sai
    }

    public void CloseQuestion()
    {
        questionPanel.SetActive(false);
    }
}
using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>Generated question session opened by mirror interaction.</summary>
public sealed class FlaskEnglishQuizPanel : MonoBehaviour
{
    [SerializeField] private string serverUrl = "http://127.0.0.1:8000";
    [SerializeField, Min(1)] private int timeoutSeconds = 60;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_InputField answerInput;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text resultText;

    public string QuestionId { get; private set; }
    public bool IsBusy { get; private set; }
    public bool CanAwardReward { get; private set; }
    private UnityWebRequest activeRequest;
    private bool submitted;
    private Action<bool> completed;
    private float previousTimeScale;
    public bool IsSessionOpen { get; private set; }

    public static FlaskEnglishQuizPanel FindOrCreate()
    {
        var panel = FindAnyObjectByType<FlaskEnglishQuizPanel>(FindObjectsInactive.Include);
        if (panel != null) return panel;
        GameObject prefab = Resources.Load<GameObject>("FlaskEnglishQuiz");
        return prefab != null ? Instantiate(prefab).GetComponentInChildren<FlaskEnglishQuizPanel>(true) : null;
    }

    private bool HasReferences => questionText != null && answerInput != null &&
        submitButton != null && statusText != null && resultText != null;

    private void Start()
    {
        if (!IsSessionOpen) gameObject.SetActive(false);
    }

    public bool TryOpen(Action<bool> onCompleted)
    {
        if (IsSessionOpen || !HasReferences || !GameplayState.CanAct || !GameplayState.BeginQuiz(this)) return false;
        completed = onCompleted;
        previousTimeScale = Time.timeScale;
        IsSessionOpen = true;
        Time.timeScale = 0f;
        // The saved scene keeps the entire Flask canvas inactive until interaction.
        for (Transform node = transform; node != null; node = node.parent)
            node.gameObject.SetActive(true);
        enabled = true;
        GenerateQuestion();
        return true;
    }

    private void Update()
    {
        if (!IsSessionOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape) || GameplayState.Current != GameplayMode.Quiz)
        { Close(); return; }
        if (!IsBusy && !submitted && string.IsNullOrEmpty(QuestionId) && Input.GetKeyDown(KeyCode.R))
            GenerateQuestion();
    }

    public void Close() => gameObject.SetActive(false);

    [Serializable] private sealed class QuestionResponse
    {
        public string status;
        public string question_id;
        public QuestionBody data;
    }
    [Serializable] private sealed class QuestionBody { public string question; public string target_grammar; }
    [Serializable] private sealed class GradeRequest { public string question_id; public string answer; }
    [Serializable] private sealed class GradeResponse
    {
        public string status;
        public GradeBody data;
        public bool can_award_reward;
    }
    [Serializable] private sealed class GradeBody
    {
        public float total_score;
        public float max_score;
        public string feedback_vi;
        public string corrected_answer;
    }

    public void Configure(TMP_Text question, TMP_InputField answer, Button generate,
        Button submit, TMP_Text status, TMP_Text result)
    {
        questionText = question;
        answerInput = answer;
        generateButton = generate;
        submitButton = submit;
        statusText = status;
        resultText = result;
    }

    private void OnEnable()
    {
        if (!HasReferences)
        {
            Debug.LogError("FlaskEnglishQuizPanel: hãy gán Question, Answer, Submit, Status và Result trong Inspector.", this);
            enabled = false;
            return;
        }
        // Treat all API content as plain text, including user-supplied answers.
        questionText.richText = statusText.richText = resultText.richText = false;
        answerInput.richText = false;
        if (generateButton != null) generateButton.gameObject.SetActive(false);
        RectTransform submitRect = submitButton.GetComponent<RectTransform>();
        submitRect.anchorMin = new Vector2(.04f, submitRect.anchorMin.y);
        submitRect.anchorMax = new Vector2(.96f, submitRect.anchorMax.y);
        submitButton.onClick.AddListener(SubmitAnswer);
        answerInput.onValueChanged.AddListener(OnAnswerChanged);
        if (string.IsNullOrEmpty(QuestionId)) statusText.text = "Đang chuẩn bị câu hỏi…";
        RefreshControls();
    }

    private void OnAnswerChanged(string _) => RefreshControls();
    private void RefreshControls()
    {
        if (answerInput != null) answerInput.interactable = !IsBusy && !submitted && !string.IsNullOrEmpty(QuestionId);
        if (submitButton != null) submitButton.interactable = !IsBusy && !submitted &&
            !string.IsNullOrEmpty(QuestionId) && answerInput != null && !string.IsNullOrWhiteSpace(answerInput.text);
    }

    public void GenerateQuestion()
    {
        if (!IsSessionOpen || !isActiveAndEnabled || IsBusy) return;
        QuestionId = null;
        submitted = CanAwardReward = false;
        questionText.text = resultText.text = string.Empty;
        answerInput.text = string.Empty;
        StartCoroutine(Post("/generate-question", "{}", false));
    }

    public void SubmitAnswer()
    {
        if (!IsSessionOpen || !isActiveAndEnabled || IsBusy || submitted || string.IsNullOrEmpty(QuestionId) ||
            string.IsNullOrWhiteSpace(answerInput.text)) return;
        CanAwardReward = false;
        resultText.text = string.Empty;
        StartCoroutine(Post("/grade-answer", JsonUtility.ToJson(new GradeRequest
        { question_id = QuestionId, answer = answerInput.text.Trim() }), true));
    }

    private IEnumerator Post(string endpoint, string json, bool grading)
    {
        IsBusy = true;
        statusText.text = grading ? "Đang chấm bài…" : "Đang lấy câu hỏi…";
        RefreshControls();
        try
        {
            if (!Uri.TryCreate(serverUrl.Trim().TrimEnd('/'), UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                statusText.text = "Địa chỉ server không hợp lệ. Hãy cấu hình HTTP hoặc HTTPS trong Inspector.";
                yield break;
            }
            using (var request = new UnityWebRequest(uri.AbsoluteUri.TrimEnd('/') + endpoint, "POST"))
            {
                activeRequest = request;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.Max(1, timeoutSeconds);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                // Catch setup/platform errors before yielding; Unity drives the request asynchronously.
                UnityWebRequestAsyncOperation operation = null;
                try { operation = request.SendWebRequest(); }
                catch (Exception) { statusText.text = "Không thể bắt đầu kết nối. Kiểm tra địa chỉ và cấu hình HTTP của Unity."; }
                if (operation == null) yield break;
                yield return operation;
                string body = request.downloadHandler.text;
                // review_required can accompany a non-2xx response. Never display its data as a score.
                if (IsReviewRequired(body))
                {
                    CanAwardReward = false;
                    statusText.text = grading
                        ? "Server đã nhận bài nhưng chưa xác nhận được kết quả chấm."
                        : "Server chưa xác nhận được câu hỏi. Vui lòng lấy lại câu hỏi sau.";
                    resultText.text = grading
                        ? "API trả review_required và chưa có điểm hợp lệ. Bạn có thể sửa câu trả lời hoặc thử nộp lại sau. Không trao thưởng."
                        : "API trả review_required. Chưa có câu hỏi hợp lệ để nộp bài.";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"Flask quiz {endpoint}: review_required; HTTP {request.responseCode}; " +
                        $"question_id={QuestionId}. Server chưa xác nhận kết quả; đối chiếu log Flask bằng question_id.\nResponse: {body}", this);
#endif
                    yield break;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    long code = request.responseCode;
                    statusText.text = code == 429 || code == 503 || code == 502 || code == 504
                        ? "Server đang bận. Vui lòng thử lại sau."
                        : code == 0 ? "Không kết nối được hoặc đã hết thời gian chờ. Kiểm tra server rồi thử lại."
                        : $"Yêu cầu thất bại (HTTP {code}). Vui lòng thử lại.";
                    yield break;
                }
                try
                {
                    if (grading) ReadGrade(body);
                    else ReadQuestion(body);
                }
                catch (Exception exception)
                {
                    CanAwardReward = false;
                    statusText.text = "Phản hồi server không hợp lệ hoặc thiếu dữ liệu. Vui lòng thử lại.";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"Flask quiz {endpoint}: HTTP {request.responseCode}; " +
                        $"{exception.Message}\nResponse: {body}", this);
#endif
                }
            }
        }
        finally
        {
            activeRequest = null;
            IsBusy = false;
            RefreshControls();
            if (IsSessionOpen)
                statusText.text += string.IsNullOrEmpty(QuestionId)
                    ? "\nNhấn R để thử lấy lại câu hỏi. Esc để đóng."
                    : "\nEsc để đóng.";
        }
    }

    private static bool IsReviewRequired(string body)
    {
        try { return JsonUtility.FromJson<GradeResponse>(body)?.status == "review_required"; }
        catch (ArgumentException) { return false; }
    }

    private void ReadQuestion(string body)
    {
        var response = JsonUtility.FromJson<QuestionResponse>(body);
        if (response == null || !Successful(response.status) || response.data == null ||
            string.IsNullOrWhiteSpace(response.question_id) || string.IsNullOrWhiteSpace(response.data.question))
            throw new FormatException();
        QuestionId = response.question_id;
        questionText.text = response.data.question + "\nNgữ pháp: " + response.data.target_grammar;
        statusText.text = "Đã nhận câu hỏi. Nhập câu trả lời rồi bấm Nộp bài.";
    }

    private void ReadGrade(string body)
    {
        var response = JsonUtility.FromJson<GradeResponse>(body);
        if (response == null || !Successful(response.status) || response.data == null ||
            response.data.max_score <= 0 || float.IsNaN(response.data.total_score) ||
            float.IsInfinity(response.data.total_score) || float.IsNaN(response.data.max_score) ||
            float.IsInfinity(response.data.max_score) || response.data.total_score < 0 ||
            response.data.total_score > response.data.max_score) throw new FormatException();
        submitted = true;
        CanAwardReward = response.can_award_reward;
        resultText.text = $"Điểm: {response.data.total_score}/{response.data.max_score}\n" +
            $"Nhận xét: {response.data.feedback_vi}\nCâu sửa: {response.data.corrected_answer}";
        statusText.text = CanAwardReward
            ? "Đã chấm. Đạt yêu cầu. Nhấn Esc để hoàn tất."
            : "Đã chấm. Không được phép trao thưởng.";
    }

    private static bool Successful(string status) =>
        status == "validated" || status == "success" || status == "ok";

    private void OnDisable()
    {
        if (generateButton != null) generateButton.onClick.RemoveListener(GenerateQuestion);
        if (submitButton != null) submitButton.onClick.RemoveListener(SubmitAnswer);
        if (answerInput != null) answerInput.onValueChanged.RemoveListener(OnAnswerChanged);
        if (activeRequest != null) activeRequest.Abort();
        StopAllCoroutines();
        activeRequest = null;
        IsBusy = false;
        if (statusText != null) statusText.text = "Đã dừng yêu cầu. Có thể thử lại khi mở panel.";
        if (IsSessionOpen)
        {
            IsSessionOpen = false;
            Action<bool> result = completed;
            completed = null;
            Time.timeScale = previousTimeScale;
            bool passed = submitted && CanAwardReward && GameplayState.Current == GameplayMode.Quiz;
            GameplayState.EndQuiz(this);
            result?.Invoke(passed);
        }
    }
}

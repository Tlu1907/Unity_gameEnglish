using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Nonblocking UTF-8 client. One session per Play session; no reset on UI close.</summary>
[DisallowMultipleComponent]
public sealed class UnityAiDialogueClient : MonoBehaviour
{
    [SerializeField] private string baseUrl = "http://127.0.0.1:8000";
    [SerializeField, Min(1)] private int timeoutSeconds = 600;
    private static string sessionId = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, UnityAiDialogueResponse> grades = new Dictionary<string, UnityAiDialogueResponse>();
    private readonly HashSet<UnityWebRequest> active = new HashSet<UnityWebRequest>();
    public string SessionId => sessionId;

    [Serializable] private sealed class GradeRequest { public string node_id, session_id, answer; }
    [Serializable] private sealed class IntentRequest { public string text; }
    public void GradeTempleReport(string nodeId, string answer, Action<TempleReportResponse> completed)
    {
        var body = new GradeRequest { node_id = nodeId, session_id = sessionId, answer = answer };
        StartCoroutine(Send("/grade-temple-report", JsonUtility.ToJson(body), json =>
        {
            TempleReportResponse result = null;
            try { if (!string.IsNullOrWhiteSpace(json)) result = JsonUtility.FromJson<TempleReportResponse>(json); }
            catch (ArgumentException) { }
            completed?.Invoke(result ?? new TempleReportResponse { status = "error" });
        }));
    }

    public bool TryGetGrade(string nodeId, out UnityAiDialogueResponse result) => grades.TryGetValue(nodeId, out result);
    public void Grade(string nodeId, string answer, Action<UnityAiDialogueResponse> completed)
    {
        var body = new GradeRequest { node_id = nodeId, session_id = sessionId, answer = answer };
        StartCoroutine(Send("/grade-answer", JsonUtility.ToJson(body), json =>
        {
            UnityAiDialogueResponse result = ParseGrade(json);
            if (result.node_id != null && result.node_id != nodeId)
                result = new UnityAiDialogueResponse { status = "error" };
            if (result.IsGraded || result.IsExhausted) grades[nodeId] = result;
            completed?.Invoke(result);
        }));
    }
    public void DetectIntent(string text, Action<UnityAiIntentResponse> completed)
    {
        StartCoroutine(Send("/detect-intent", JsonUtility.ToJson(new IntentRequest { text = text }), json =>
        {
            UnityAiIntentResponse result = null;
            try { if (!string.IsNullOrWhiteSpace(json)) result = JsonUtility.FromJson<UnityAiIntentResponse>(json); }
            catch (ArgumentException) { }
            completed?.Invoke(result ?? new UnityAiIntentResponse { status = "error" });
        }));
    }

    public static UnityAiDialogueResponse ParseGrade(string json)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                var result = JsonUtility.FromJson<UnityAiDialogueResponse>(json);
                if (result != null && !string.IsNullOrWhiteSpace(result.status)) return result;
            }
        }
        catch (ArgumentException) { }
        return new UnityAiDialogueResponse { status = "error" };
    }

    private IEnumerator Send(string route, string json, Action<string> completed)
    {
        var request = new UnityWebRequest(baseUrl.TrimEnd('/') + route, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        request.timeout = Mathf.Max(1, timeoutSeconds);
        active.Add(request);
        try
        {
            yield return request.SendWebRequest();
            // HTTP 409 exhausted and 503 busy also carry useful API JSON.
            completed(request.downloadHandler.text);
        }
        finally { if (active.Remove(request)) request.Dispose(); }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (var request in active) { request.Abort(); request.Dispose(); }
        active.Clear();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => sessionId = Guid.NewGuid().ToString("N");
}

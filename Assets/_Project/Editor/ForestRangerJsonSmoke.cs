using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>One real UI/HTTP roundtrip, then restores Edit Mode. Explicit menu/request only.</summary>
[InitializeOnLoad]
public static class ForestRangerJsonSmoke
{
    private const string Pending = "RangerJson.RuntimeCheck";
    private const string Result = "Library/ForestRangerJsonRuntime.result";
    private static NPCIntroDialogue view;
    private static NpcJsonDialoguePanel panel;
    private static double started, screenshotRequested;
    static ForestRangerJsonSmoke()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }
    [MenuItem("Tools/Map 2/Test Ranger UI With Running UnityAI")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Pending, false)) return;
        view = null; panel = null; screenshotRequested = 0;
        SessionState.SetBool(Pending, true);
        File.WriteAllText(Result, "RUNNING: waiting for Play Mode and one real UnityAI request.");
        EditorApplication.EnterPlaymode();
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
        }
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 600) throw new TimeoutException("UnityAI UI roundtrip timed out.");
            if (view == null)
            {
                if (EditorApplication.timeSinceStartup - started < 1) return;
                view = UnityEngine.Object.FindObjectsByType<NPCIntroDialogue>(FindObjectsInactive.Include)
                    .First(v => v.name == "ForestRangerDialogueCanvas");
                panel = view.GetComponent<NpcJsonDialoguePanel>();
                if (UnityEngine.EventSystems.EventSystem.current == null)
                    throw new InvalidOperationException("No EventSystem for clicking buttons or typing answers.");
                view.Open();
                if (!view.IsOpen) throw new InvalidOperationException("Ranger did not open in Play Mode: " + GameplayState.Current);
                for (int i = 0; i < 4 && panel.CurrentNodeId != "ranger_question_01_player_identity"; i++) view.Advance();
                if (panel.CurrentNodeId != "ranger_question_01_player_identity") throw new InvalidOperationException("Wrong question node.");
                var input = (TMP_InputField)new SerializedObject(panel).FindProperty("answerInput").objectReferenceValue;
                input.text = "I am agent.";
                view.SubmitAnswer();
                if (!panel.IsBusy || Time.timeScale != 0) throw new InvalidOperationException("Submission did not pause game and start HTTP request.");
                return;
            }
            if (panel.IsBusy) return;
            if (screenshotRequested == 0)
            {
                var response = panel.LastGrade;
                if (response == null || !response.IsGraded) throw new InvalidOperationException("Backend did not return a validated grade; see the UI status.");
                if (response.data.corrected_answer != "I am an agent." || response.data.scores.grammar != 4)
                    throw new InvalidOperationException("Unexpected rule correction returned by backend.");
                var text = (TMP_Text)new SerializedObject(panel).FindProperty("feedbackText").objectReferenceValue;
                if (!text.gameObject.activeInHierarchy || !text.text.Contains("CẦN SỬA") || !text.text.Contains("Danh từ"))
                    throw new InvalidOperationException("Feedback missing or Vietnamese was corrupted.");
                ScreenCapture.CaptureScreenshot("Library/RangerJsonRuntime.png");
                screenshotRequested = EditorApplication.timeSinceStartup;
                return;
            }
            if (EditorApplication.timeSinceStartup - screenshotRequested < 2) return;
            var grade = panel.LastGrade;
            view.Close();
            if (!GameplayState.CanAct || Time.timeScale == 0) throw new InvalidOperationException("Closing failed to resume gameplay.");
            Finish("SUCCESS: real game UI POST /grade-answer while timeScale=0; node=" + grade.node_id +
                "; meaning=" + grade.data.scores.meaning + "; grammar=" + grade.data.scores.grammar +
                "; corrected=" + grade.data.corrected_answer + "; Vietnamese feedback displayed; gameplay restored. Screenshot: Library/RangerJsonRuntime.png");
        }
        catch (Exception error) { Finish("FAILED\n" + error); Debug.LogException(error); }
    }
    private static void Finish(string message)
    {
        File.WriteAllText(Result, message);
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick;
        if (view != null && view.IsOpen) view.Close();
        EditorApplication.ExitPlaymode();
    }
}

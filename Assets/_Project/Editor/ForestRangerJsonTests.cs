using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Runs the focused connection tests in the already open editor.</summary>
public static class ForestRangerJsonTests
{
    private static TestRunnerApi runner;
    [MenuItem("Tools/Map 2/Run Ranger JSON Tests")]
    public static void Run()
    {
        if (runner != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new Results());
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] { "NpcJsonDialogueTests" } }));
    }
    private sealed class Results : ICallbacks
    {
        private readonly StringBuilder details = new StringBuilder();
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.FailCount > 0) details.AppendLine(result.Name).AppendLine(result.Message).AppendLine(result.StackTrace);
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            File.WriteAllText("Library/ForestRangerJsonTests.result", (result.FailCount == 0 ? "SUCCESS" : "FAILED") +
                ": passed=" + result.PassCount + "; failed=" + result.FailCount + "; skipped=" + result.SkipCount + "\n" + details);
            var finished = runner; runner = null;
            EditorApplication.delayCall += () => Object.DestroyImmediate(finished);
        }
    }
}

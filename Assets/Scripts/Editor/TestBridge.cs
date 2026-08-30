using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace NodeWar.EditorTools
{
    /// <summary>
    /// Watches for a trigger file written by scripts/run-tests-live.ps1 and, when one
    /// appears, runs the EditMode test suite inside this already-open Editor session
    /// via TestRunnerApi -- avoiding Unity's single-instance project lock that would
    /// otherwise block a second batch-mode process from running tests while the
    /// Editor stays open.
    ///
    /// Protocol (mirrored in scripts/run-tests-live.ps1):
    ///   1. The external script writes a fresh GUID into TestResults/trigger.txt.
    ///   2. This watcher notices the new GUID and, once the Editor is idle (not
    ///      compiling, not in Play Mode -- it waits rather than interrupting either),
    ///      runs all EditMode tests.
    ///   3. On completion it writes TestResults/results.xml (real NUnit3 XML, the
    ///      same format Unity's own -testResults batch-mode flag produces) and then
    ///      TestResults/done.txt containing that same GUID as a completion signal.
    ///   4. The external script polls for done.txt, confirms the GUID matches the
    ///      one it wrote, and only then parses results.xml.
    /// </summary>
    [InitializeOnLoad]
    internal static class TestBridge
    {
        private static readonly string ProjectRoot = Directory.GetParent(Application.dataPath).FullName;
        private static readonly string TestResultsDir = Path.Combine(ProjectRoot, "TestResults");
        private static readonly string TriggerFile = Path.Combine(TestResultsDir, "trigger.txt");
        private static readonly string ResultsFile = Path.Combine(TestResultsDir, "results.xml");
        private static readonly string DoneFile = Path.Combine(TestResultsDir, "done.txt");

        private static string lastConsumedRunId;
        private static bool runInProgress;
        private static TestRunnerApi api;
        private static Listener listener;

        static TestBridge()
        {
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            listener = new Listener();
            api.RegisterCallbacks(listener);
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (runInProgress) return;
            if (!File.Exists(TriggerFile)) return;

            string runId;
            try
            {
                runId = File.ReadAllText(TriggerFile).Trim();
            }
            catch (IOException)
            {
                // Trigger file is mid-write by the external script; retry next frame.
                return;
            }

            if (string.IsNullOrEmpty(runId) || runId == lastConsumedRunId) return;

            // Don't interrupt Play Mode or an in-flight compile -- just keep polling
            // until the Editor is idle, then fire automatically.
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            lastConsumedRunId = runId;
            StartRun(runId);
        }

        private static void StartRun(string runId)
        {
            runInProgress = true;
            listener.Reset(runId);

            Debug.Log($"[TestBridge] Starting EditMode test run (id={runId}) triggered by {TriggerFile}");

            try
            {
                api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
            }
            catch (Exception ex)
            {
                // If Execute() throws before RunFinished can ever fire, runInProgress
                // would otherwise be stuck true forever, silently blocking every
                // future trigger until the Editor's next domain reload.
                Debug.LogError($"[TestBridge] Failed to start run {runId}: {ex}");
                runInProgress = false;
            }
        }

        private class Listener : ICallbacks
        {
            private string runId;

            public void Reset(string id)
            {
                runId = id;
            }

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    Directory.CreateDirectory(TestResultsDir);
                    TestRunnerApi.SaveResultToFile(result, ResultsFile);
                    WriteDoneFileWithRetry(runId);

                    Debug.Log($"[TestBridge] Run {runId} finished: {result.PassCount} passed, {result.FailCount} failed.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TestBridge] Failed to write results for run {runId}: {ex}");
                }
                finally
                {
                    runInProgress = false;
                }
            }

            // Writes done.txt via a temp file + move so the external script never
            // observes a partially-written file, retrying past the kind of transient
            // sharing violation that can happen if run-tests-live.ps1 is mid-read of
            // a stale done.txt from a previous run at the exact moment we try to
            // replace it. If every attempt fails, the last IOException propagates to
            // RunFinished's catch above (results.xml will still exist on disk even
            // though the caller-visible signal never arrived).
            private static void WriteDoneFileWithRetry(string content)
            {
                const int maxAttempts = 5;
                const int retryDelayMs = 100;
                string tempDone = DoneFile + ".tmp";

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        File.WriteAllText(tempDone, content);
                        if (File.Exists(DoneFile)) File.Delete(DoneFile);
                        File.Move(tempDone, DoneFile);
                        return;
                    }
                    catch (IOException) when (attempt < maxAttempts)
                    {
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                }
            }
        }
    }
}

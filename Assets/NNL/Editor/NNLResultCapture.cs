using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NNLResultCapture
{
    private const string shareFolderName = "GPTShare";
    private const int maxLogEntries = 20000;
    private static readonly object logLock = new object();
    private static readonly List<string> logEntries = new List<string>();
    private static bool isCapturing;

    static NNLResultCapture()
    {
        Application.logMessageReceived += onLogMessageReceived;
        EditorApplication.playModeStateChanged += onPlayModeStateChanged;
    }

    private static void onLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string entry = "[" + timestamp + "] [" + type + "] " + redact(condition);

        if (!string.IsNullOrWhiteSpace(stackTrace))
            entry += Environment.NewLine + redact(stackTrace);

        lock (logLock)
        {
            if (logEntries.Count >= maxLogEntries)
                logEntries.RemoveAt(0);

            logEntries.Add(entry);
        }
    }

    private static string redact(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        value = Regex.Replace(value, @"(?i)(authorization\s*[:=]\s*bearer\s+)[^\s]+", "$1[REDACTED]");
        value = Regex.Replace(value, @"(?i)((api[_-]?key|access[_-]?token|refresh[_-]?token|password|secret)\s*[:=]\s*)[^\s,;]+", "$1[REDACTED]");
        value = Regex.Replace(value, @"(?i)\bgh[pousr]_[A-Za-z0-9_]{20,}\b", "[REDACTED_GITHUB_TOKEN]");
        return value;
    }

    private static void onPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            lock (logLock)
                logEntries.Clear();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode && NNLResultCaptureSettings.AutoCaptureOnPlayStop)
        {
            capture(NNLResultCaptureSettings.AutoPushToGitHub);
        }
    }

    public static string CaptureAndPush()
    {
        return capture(true);
    }

    private static string capture(bool pushToRemote)
    {
        if (isCapturing)
            return "Capture is already running.";

        isCapturing = true;

        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputDirectory = Path.Combine(projectRoot, shareFolderName, timestamp);
            Directory.CreateDirectory(outputDirectory);

            string logText;
            int capturedLogCount;
            lock (logLock)
            {
                capturedLogCount = logEntries.Count;
                logText = string.Join(Environment.NewLine + Environment.NewLine, logEntries);
            }

            if (string.IsNullOrEmpty(logText))
                logText = "(No Unity Console messages were captured.)";

            File.WriteAllText(Path.Combine(outputDirectory, "console.txt"), logText, new UTF8Encoding(false));

            string screenshotPath = Path.Combine(outputDirectory, "preview.png");
            bool screenshotSaved = capturePreview(screenshotPath);

            string scenePath = SceneManager.GetActiveScene().path;
            string manifest = "{\n" +
                "  \"captured_at\": \"" + DateTime.Now.ToString("o") + "\",\n" +
                "  \"unity_version\": \"" + escapeJson(Application.unityVersion) + "\",\n" +
                "  \"platform\": \"" + escapeJson(Application.platform.ToString()) + "\",\n" +
                "  \"is_playing\": " + (Application.isPlaying ? "true" : "false") + ",\n" +
                "  \"active_scene\": \"" + escapeJson(scenePath) + "\",\n" +
                "  \"log_entries\": " + capturedLogCount + ",\n" +
                "  \"preview_saved\": " + (screenshotSaved ? "true" : "false") + "\n" +
                "}\n";

            File.WriteAllText(Path.Combine(outputDirectory, "manifest.json"), manifest, new UTF8Encoding(false));

            string gitResult = commitAndPush(projectRoot, outputDirectory, pushToRemote);
            string message = "Saved result: " + outputDirectory + Environment.NewLine + gitResult;
            UnityEngine.Debug.Log(message);
            return message;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError("NNL result capture failed: " + exception);
            return exception.Message;
        }
        finally
        {
            isCapturing = false;
        }
    }

    private static bool capturePreview(string outputPath)
    {
        Texture2D screenshot = null;

        try
        {
            if (Application.isPlaying)
            {
                screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            }
            else
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null && sceneView.camera != null)
                {
                    Camera camera = sceneView.camera;
                    RenderTexture renderTexture = new RenderTexture(
                        Mathf.Max(1, camera.pixelWidth),
                        Mathf.Max(1, camera.pixelHeight),
                        24,
                        RenderTextureFormat.ARGB32);

                    RenderTexture previous = RenderTexture.active;
                    camera.targetTexture = renderTexture;
                    camera.Render();
                    RenderTexture.active = renderTexture;

                    screenshot = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
                    screenshot.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    screenshot.Apply();

                    camera.targetTexture = null;
                    RenderTexture.active = previous;
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
            }

            if (screenshot == null)
                return false;

            File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
            return true;
        }
        finally
        {
            if (screenshot != null)
                UnityEngine.Object.DestroyImmediate(screenshot);
        }
    }

    private static string escapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string commitAndPush(string projectRoot, string outputDirectory, bool pushToRemote)
    {
        string gitRoot = runGit(projectRoot, "rev-parse --show-toplevel", out int rootExitCode);
        if (rootExitCode != 0)
            return "Git push skipped: project is not inside a Git repository.";

        gitRoot = gitRoot.Trim();
        string relativePath = Path.GetRelativePath(gitRoot, Path.Combine(projectRoot, shareFolderName))
            .Replace("\\", "/");

        if (relativePath == ".." || relativePath.StartsWith("../", StringComparison.Ordinal))
            return "Git push skipped: GPTShare folder is outside the Git repository.";

        runGit(gitRoot, "add -A -- " + quote(relativePath), out int addExitCode);
        if (addExitCode != 0)
            return "Git add failed. Check Git installation and repository permissions.";

        runGit(gitRoot, "diff --cached --quiet -- " + quote(relativePath), out int diffExitCode);
        if (diffExitCode == 1)
        {
            string commitMessage = "Add GPT-shared Unity result " + Path.GetFileName(outputDirectory);
            string commitOutput = runGit(gitRoot,
                "commit --only -m " + quote(commitMessage) + " -- " + quote(relativePath),
                out int commitExitCode);

            if (commitExitCode != 0)
                return "Git commit failed:\n" + commitOutput;
        }
        else if (diffExitCode != 0)
        {
            return "Could not inspect staged GPTShare changes.";
        }

        if (!pushToRemote)
            return "Saved and committed locally. Automatic GitHub push is disabled.";

        string branch = runGit(gitRoot, "rev-parse --abbrev-ref HEAD", out int branchExitCode).Trim();
        if (branchExitCode != 0 || string.IsNullOrEmpty(branch) || branch == "HEAD")
            return "Files saved locally. Push skipped because the current branch could not be determined.";

        string pushOutput = runGit(gitRoot, "push origin " + quote(branch), out int pushExitCode);
        if (pushExitCode != 0)
            return "Files and commit saved locally, but push failed:\n" + pushOutput;

        return "Committed and pushed GPTShare/" + Path.GetFileName(outputDirectory) + " to origin/" + branch + ".";
    }

    private static string runGit(string workingDirectory, string arguments, out int exitCode)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = new Process { StartInfo = startInfo })
        {
            process.Start();
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(30000))
            {
                process.Kill();
                exitCode = -1;
                return "Git command timed out: " + arguments;
            }

            exitCode = process.ExitCode;
            return (standardOutput + Environment.NewLine + standardError).Trim();
        }
    }

    private static string quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}

public static class NNLResultCaptureSettings
{
    private const string autoCaptureKey = "NNL.AutoCaptureOnPlayStop";
    private const string autoPushKey = "NNL.AutoPushToGitHub";

    public static bool AutoCaptureOnPlayStop
    {
        get { return EditorPrefs.GetBool(autoCaptureKey, true); }
        set { EditorPrefs.SetBool(autoCaptureKey, value); }
    }

    public static bool AutoPushToGitHub
    {
        get { return EditorPrefs.GetBool(autoPushKey, false); }
        set { EditorPrefs.SetBool(autoPushKey, value); }
    }
}

public sealed class NNLResultCaptureWindow : EditorWindow
{
    [MenuItem("NNL/Result Capture Window")]
    public static void ShowWindow()
    {
        GetWindow<NNLResultCaptureWindow>("NNL Result Capture");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("GPT Shared Results", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Play Mode終了時にConsoleログとGame View画像をGPTShareへ保存します。GitHubへの自動Pushは別設定です。",
            MessageType.Info);

        NNLResultCaptureSettings.AutoCaptureOnPlayStop =
            EditorGUILayout.Toggle("Auto capture on Play Stop", NNLResultCaptureSettings.AutoCaptureOnPlayStop);
        NNLResultCaptureSettings.AutoPushToGitHub =
            EditorGUILayout.Toggle("Auto push to GitHub", NNLResultCaptureSettings.AutoPushToGitHub);

        EditorGUILayout.Space();
        if (GUILayout.Button("Capture & Push Now", GUILayout.Height(32)))
        {
            string result = NNLResultCapture.CaptureAndPush();
            EditorUtility.DisplayDialog("NNL Result Capture", result, "OK");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", "ProjectRoot/GPTShare/<timestamp>/");
        EditorGUILayout.LabelField("Files", "console.txt / preview.png / manifest.json");
        EditorGUILayout.HelpBox(
            "Git操作はGPTShareフォルダだけを対象にします。現在のnnlリポジトリはPublicです。自動Pushを有効にするとログと画像も公開されます。",
            MessageType.None);
    }
}

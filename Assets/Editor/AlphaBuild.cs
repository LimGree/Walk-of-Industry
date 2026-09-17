#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AlphaBuild
{
    const string TriggerRelativePath = "Temp/request-windows-alpha-build.txt";
    const string StatusRelativePath = "Builds/Windows/last-build-status.txt";

    [InitializeOnLoadMethod]
    static void ConsumeCliTrigger()
    {
        string trigger = Path.GetFullPath(Path.Combine(Application.dataPath, "..", TriggerRelativePath));
        if (!File.Exists(trigger))
            return;

        File.Delete(trigger);
        EditorApplication.delayCall += () => BuildWindowsAlpha(exitEditor: false);
    }

    [MenuItem("Walk of Industry/Build Windows Alpha")]
    public static void BuildWindowsAlphaMenu()
    {
        BuildWindowsAlpha(exitEditor: false);
    }

    public static void BuildWindowsAlphaCli()
    {
        bool ok = BuildWindowsAlpha(exitEditor: false);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool BuildWindowsAlpha(bool exitEditor)
    {
        string version = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
            ? "0.0.0"
            : PlayerSettings.bundleVersion.Trim();
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outDir = Path.Combine(projectRoot, "Builds", "Windows", $"{GameBranding.BuildFolderPrefix}-{version}");
        string statusPath = Path.Combine(projectRoot, StatusRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(statusPath));

        WriteStatus(statusPath, "RUNNING");

        if (Directory.Exists(outDir))
            Directory.Delete(outDir, true);
        Directory.CreateDirectory(outDir);

        var scenePaths = new System.Collections.Generic.List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                scenePaths.Add(scene.path);
        }

        if (scenePaths.Count == 0)
        {
            WriteStatus(statusPath, "FAILED no enabled scenes in Build Settings");
            Debug.LogError("Alpha build failed: no enabled scenes.");
            return false;
        }

        string exePath = Path.Combine(outDir, GameBranding.ExeName);
        var options = new BuildPlayerOptions
        {
            scenes = scenePaths.ToArray(),
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CompressWithLz4HC
        };

        Debug.Log($"Alpha build started → {exePath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            WriteStatus(statusPath, $"FAILED {summary.result} errors={summary.totalErrors}");
            Debug.LogError($"Alpha build failed: {summary.result} ({summary.totalErrors} errors)");
            return false;
        }

        WriteReadme(Path.Combine(outDir, "README.txt"), version);
        WriteStatus(statusPath, $"SUCCESS {exePath}");
        Debug.Log($"Alpha build ok: {exePath} ({summary.totalSize} bytes, {summary.totalTime})");

        if (exitEditor)
            EditorApplication.Exit(0);
        return true;
    }

    static void WriteStatus(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text + "\n", Encoding.UTF8);
    }

    static void WriteReadme(string path, string version)
    {
        var sb = new StringBuilder();
        sb.AppendLine(GameBranding.Title + " — Alpha " + version);
        sb.AppendLine(GameBranding.Tagline);
        sb.AppendLine();
        sb.AppendLine("Windows 64-bit. Unpack the whole folder and run \"" + GameBranding.ExeName + "\".");
        sb.AppendLine("Alt+Enter toggles fullscreen.");
        sb.AppendLine();
        sb.AppendLine("This is an early playable build: expect missing machines, balance issues, and bugs.");
        sb.AppendLine("Known gaps: no Fabricator building yet; some late recipes cannot be crafted.");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}
#endif

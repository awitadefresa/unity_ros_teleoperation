using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildAndroid
{
    [MenuItem("Build/Build Android APK")]
    public static void BuildApk()
    {
        // Ensure output folder exists
        Directory.CreateDirectory("Builds");

        // Collect enabled scenes from Build Settings
        var scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);

        // If no scenes are added/enabled, fall back to the currently open scene
        if (scenes == null || scenes.Length == 0)
        {
            if (string.IsNullOrEmpty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path))
            {
                throw new System.Exception("No scenes in Build Settings and no active scene saved. Open a scene and save it, or add scenes to Build Settings.");
            }
            scenes = new[] { UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path };
            Debug.Log("No enabled scenes found in Build Settings. Falling back to active scene: " + scenes[0]);
        }

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Teleop.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        };

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        Debug.Log("Build result: " + report.summary.result);
        Debug.Log("Total errors: " + report.summary.totalErrors);
        Debug.Log("Total warnings: " + report.summary.totalWarnings);
        Debug.Log("Output path: " + report.summary.outputPath);

        foreach (var step in report.steps)
        {
            foreach (var msg in step.messages)
            {
                Debug.Log(msg.type + ": " + msg.content);
            }
        }

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception("Build failed: " + report.summary.result);
        }
    }
}

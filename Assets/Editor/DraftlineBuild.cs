using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Draftline.EditorTools
{
    /// <summary>One-click development build for the Windows portfolio demo.</summary>
    public static class DraftlineBuild
    {
        [MenuItem("Draftline/Build Windows Dev")]
        public static void BuildWindowsDev()
        {
            var scenes = new[] { "Assets/Scenes/SampleScene.unity" };
            var outputPath = "Builds/Driftline-Dev.exe";
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(scenes, outputPath,
                BuildTarget.StandaloneWindows64, BuildOptions.Development);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[DraftlineBuild] succeeded: {outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            else
                Debug.LogError($"[DraftlineBuild] failed: {report.summary.result}");
        }
    }
}

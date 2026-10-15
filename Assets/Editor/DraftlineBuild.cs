using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Draftline.EditorTools
{
    /// <summary>One-click development build for the Windows portfolio demo.</summary>
    public static class DraftlineBuild
    {
        [MenuItem("Driftline/Build Windows Battlefields V2")]
        public static void BuildWindowsBattlefields()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Build in Edit Mode.");
            AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            const string directory="Builds/BattlefieldsV2";Directory.CreateDirectory(directory);
            var report=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/SampleScene.unity"},directory+"/Driftline-Battlefields.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(report.summary.result==BuildResult.Succeeded)File.Copy("Assets/UI/Fonts/LICENSE.txt",directory+"/SourceHanSans-LICENSE.txt",true);
            Directory.CreateDirectory("Docs/Validation/BattlefieldsV2");
            File.WriteAllText("Docs/Validation/BattlefieldsV2/build.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{result=report.summary.result.ToString(),bytes=report.summary.totalSize,errors=report.summary.totalErrors,warnings=report.summary.totalWarnings}));
            if(report.summary.result!=BuildResult.Succeeded)Debug.LogError("Battlefield build failed: "+report.summary.result);
        }
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
            {
                File.Copy("Assets/UI/Fonts/LICENSE.txt", Path.Combine(directory, "SourceHanSans-LICENSE.txt"), true);
                Debug.Log($"[DraftlineBuild] succeeded: {outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            }
            else
                Debug.LogError($"[DraftlineBuild] failed: {report.summary.result}");
        }

        [MenuItem("Driftline/Build Windows Themes Dev")]
        public static void BuildWindowsThemes()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Build in Edit Mode.");
            AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            const string directory="Builds/Themed";Directory.CreateDirectory(directory);
            var report=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/SampleScene.unity"},directory+"/Driftline-Themes.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(report.summary.result==BuildResult.Succeeded)File.Copy("Assets/UI/Fonts/LICENSE.txt",directory+"/SourceHanSans-LICENSE.txt",true);
            Directory.CreateDirectory("Docs/Validation/2026-10-15");
            var record=new {result=report.summary.result.ToString(),bytes=report.summary.totalSize,errors=report.summary.totalErrors,warnings=report.summary.totalWarnings};
            File.WriteAllText("Docs/Validation/2026-10-15/themes-build.json",Newtonsoft.Json.JsonConvert.SerializeObject(record));
            if(report.summary.result!=BuildResult.Succeeded)Debug.LogError("Themed build failed: "+report.summary.result);
        }
    }
}

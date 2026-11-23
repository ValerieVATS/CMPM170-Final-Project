using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SmashBurgerWebGLBuild
{
    private const string BuildDirectory = "Builds/WebGL";

    [MenuItem("Build/WebGL (itch.io)")]
    public static void Build()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outputPath = Path.Combine(projectRoot, BuildDirectory);
        Directory.CreateDirectory(outputPath);

        PlayerSettings.SetAdditionalIl2CppArgs("--jobs=2");

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 600;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"WebGL build {summary.result}: {summary.totalSize} bytes in {summary.totalTime}");

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}

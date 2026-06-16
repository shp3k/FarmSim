using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class FarmSimBuild
{
    public static void BuildWindowsRelease()
    {
        string outputDir = GetArgument("-outputDir");
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            outputDir = Path.GetFullPath(Path.Combine("..", "..", "Release", "FarmSim-Windows"));
        }

        Directory.CreateDirectory(outputDir);

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        BuildPlayerOptions options = new()
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDir, "FarmSim.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Сборка FarmSim не удалась: {report.summary.result}");
        }

        UnityEngine.Debug.Log($"Сборка FarmSim готова: {outputDir}");
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }
}

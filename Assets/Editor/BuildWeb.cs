using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace CloudWhale.Editor
{
    public static class BuildWeb
    {
        public static void Build()
        {
            var output = Environment.GetEnvironmentVariable("CLOUDWHALE_WEB_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(output))
            {
                output = "Builds/Web";
            }

            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Web build failed: {report.summary.result}");
            }
        }
    }
}

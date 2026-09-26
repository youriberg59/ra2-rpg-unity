using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RA2RPG.EditorTools
{
    public static class WebGLBuild
    {
        [MenuItem("RA2 RPG/Build WebGL")]
        public static void Build()
        {
            const string output = "Build/WebGL";
            Directory.CreateDirectory(output);

            string[] scenes = { "Assets/Scenes/RPGDemo.unity" };

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"WebGL build completed: {output}");
            else
                Debug.LogError($"WebGL build failed: {report.summary.result}");
        }
    }
}

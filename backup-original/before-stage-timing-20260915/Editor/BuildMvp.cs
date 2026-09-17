using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildMvp {
    public static void Build() {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        string folder = Path.Combine(root, "交付", "四轨音游");
        Directory.CreateDirectory(folder);
        PlayerSettings.productName = "Four Key Rhythm";
        PlayerSettings.companyName = "RhythmPrototype";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = Path.Combine(folder, "RhythmMVP.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        File.WriteAllText(Path.Combine(root, "validation", "build-result.txt"), report.summary.result + " errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings);
        var messages = new System.Text.StringBuilder();
        foreach (var step in report.steps) foreach (var message in step.messages)
            if (message.type == LogType.Warning || message.type == LogType.Error) messages.AppendLine(message.type + ": " + message.content);
        File.WriteAllText(Path.Combine(root,"validation/build-messages.txt"),messages.ToString());
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed.");
    }
}

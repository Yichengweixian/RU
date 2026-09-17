using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// An explicit request allows verification in an already-open editor, without
// starting a second Unity process or interrupting an active Play session.
[InitializeOnLoad]
public static class VerifyFeedback {
    static bool waitingLogged;
    static string Root { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../..")); } }
    static string Request { get { return Path.Combine(Root, "validation/feedback-build.request"); } }
    static string Result { get { return Path.Combine(Root, "validation/feedback-build-result.txt"); } }
    static VerifyFeedback() {
        if (File.Exists(Request)) EditorApplication.update += Once;
    }
    static void Once() {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            if (!waitingLogged) File.WriteAllText(Result, "WAITING: Stop Play to verify and build.");
            waitingLogged = true;
            return;
        }
        EditorApplication.update -= Once;
        Run();
    }
    [MenuItem("音游/验证反馈并构建试玩版")]
    public static void Run() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            Debug.LogWarning("请先停止 Play，再构建试玩版。"); return;
        }
        if (File.Exists(Request)) File.Delete(Request);
        try {
            string checks = FeedbackChecks.Run();
            BuildMvp.Build();
            File.WriteAllText(Result, checks + "\n" + File.ReadAllText(Path.Combine(Root, "validation/build-result.txt")));
            Debug.Log(checks + "; feedback build succeeded.");
        } catch (Exception e) {
            File.WriteAllText(Result, "FAIL: " + e);
            Debug.LogException(e);
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RpeImportWindow : EditorWindow {
    string jsonPath = "", audioPath = "", status = "";
    bool failed;

    [MenuItem("音游/导入 PhiEdit 谱面…", priority = 1)]
    public static void Open() {
        var window = GetWindow<RpeImportWindow>("PhiEdit → 四轨音游");
        window.minSize = new Vector2(600, 360);
        window.Show();
    }
    void OnEnable() {
        jsonPath = EditorPrefs.GetString("RhythmMVP.LastRpeJson", "");
        audioPath = EditorPrefs.GetString("RhythmMVP.LastRpeAudio", "");
    }
    void OnGUI() {
        GUILayout.Space(12);
        GUILayout.Label("导入 PhiEdit / RPE JSON", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("选择导出的 JSON 和配套音频，点击“导入并运行”。以后更新同一个谱面，重新点一次即可。", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        PathRow("谱面 JSON", ref jsonPath, "json");
        if (EditorGUI.EndChangeCheck()) DetectAudio();
        PathRow("配套音频", ref audioPath, "wav,ogg,mp3");
        GUILayout.Space(8);
        EditorGUILayout.HelpBox("一条固定判定线；只用 Tap / Hold。\n四个区域从左到右对应 A / S / D / F，中心 X 为 -506.25、-168.75、168.75、506.25。\n不会导入背景美工、判定线移动或旋转。", MessageType.None);
        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || !File.Exists(jsonPath) || !File.Exists(audioPath))) {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导入", GUILayout.Height(36))) Import(false);
            if (GUILayout.Button("导入并运行", GUILayout.Height(36))) Import(true);
            EditorGUILayout.EndHorizontal();
        }
        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("先停止 Play 再导入；或者使用游戏里的“导入谱面”按钮。", MessageType.Info);
        if (!String.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, failed ? MessageType.Error : MessageType.Info);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("打开操作说明")) OpenGuide();
        HandleDrop();
    }
    void PathRow(string title, ref string path, string extension) {
        EditorGUILayout.BeginHorizontal();
        path = EditorGUILayout.TextField(title, path);
        if (GUILayout.Button("选择…", GUILayout.Width(70))) {
            string chosen = extension == "json" ? EditorUtility.OpenFilePanel("选择 RPE JSON", Directory.Exists(Path.GetDirectoryName(path)) ? Path.GetDirectoryName(path) : "", "json")
                : EditorUtility.OpenFilePanelWithFilters("选择配套音频", "", new[] { "音频", "wav,ogg,mp3" });
            if (!String.IsNullOrEmpty(chosen)) { path = chosen; GUI.changed = true; }
        }
        EditorGUILayout.EndHorizontal();
    }
    void DetectAudio() {
        if (!File.Exists(jsonPath)) return;
        string found = ChartImportService.FindAudio(jsonPath);
        // Clear any previous song's audio when the new JSON cannot resolve its own.
        audioPath = found ?? "";
    }
    void HandleDrop() {
        Event e = Event.current;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragPerform) {
            DragAndDrop.AcceptDrag();
            foreach (string path in DragAndDrop.paths) if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)) { jsonPath = Path.GetFullPath(path); DetectAudio(); }
            foreach (string path in DragAndDrop.paths) {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".mp3" || ext == ".ogg" || ext == ".wav") audioPath = Path.GetFullPath(path);
            }
        }
        e.Use();
    }
    void Import(bool play) {
        try {
            string target = ChartImportService.Import(jsonPath, audioPath, Path.Combine(Application.streamingAssetsPath, "Songs"));
            ChartData chart = ChartLoader.LoadFile(target);
            EditorPrefs.SetString("RhythmMVP.LastRpeJson", jsonPath);
            EditorPrefs.SetString("RhythmMVP.LastRpeAudio", audioPath);
            AssetDatabase.Refresh();
            failed = false;
            status = "已导入：" + chart.title + "，" + chart.notes.Length + " 个音符。\n" + String.Join("\n", chart.warnings);
            if (play && chart.notes.Length > 0 && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                PlayerPrefs.SetString("RhythmMVP.SelectedChart", target);
                SessionState.SetString("RhythmMVP.StartChart", target);
                EditorApplication.isPlaying = true;
            }
        } catch (Exception e) { failed = true; status = e.Message; }
    }
    [MenuItem("音游/打开游戏场景", priority = 2)]
    static void OpenScene() {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
    }
    [MenuItem("音游/打开操作说明", priority = 3)]
    static void OpenGuide() { Application.OpenURL(new Uri(Path.GetFullPath(Path.Combine(Application.dataPath, "../../开始使用.md"))).AbsoluteUri); }
}

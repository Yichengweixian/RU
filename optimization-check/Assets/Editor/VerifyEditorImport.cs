using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// Explicitly invoked integration check; never runs during ordinary editor startup.
public static class VerifyEditorImport {
    const string Key = "RhythmMVP.EditorVerification.";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string Root { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../..")); } }
    static string Phase { get { return SessionState.GetString(Key + "phase", ""); } set { SessionState.SetString(Key + "phase", value); } }
    static double Now { get { return EditorApplication.timeSinceStartup; } }
    static object Field(object o, string field) { return o.GetType().GetField(field, Private).GetValue(o); }
    static void SetField(object o, string field, object value) { o.GetType().GetField(field, Private).SetValue(o, value); }
    static void SavePref(string key) {
        SessionState.SetBool(Key + key + ".exists", EditorPrefs.HasKey(key));
        SessionState.SetString(Key + key, EditorPrefs.GetString(key, ""));
    }
    static void RestorePref(string key) {
        if (SessionState.GetBool(Key + key + ".exists", false)) EditorPrefs.SetString(key, SessionState.GetString(Key + key, ""));
        else EditorPrefs.DeleteKey(key);
    }
    [InitializeOnLoadMethod]
    static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    public static void Begin() {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before verification.");
        VerifyMvp.Run();
        string source = Path.GetFullPath(Path.Combine(Root, "validation/fixtures/rpe/source.json"));
        string hash;
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source.ToLowerInvariant()))).Replace("-", "").Substring(0,12);
        string folder = "Assets/StreamingAssets/Songs/chart-" + hash;
        if (Directory.Exists(Path.Combine(Root, "mvp", folder))) throw new Exception("Verification destination already exists; refusing to overwrite it.");
        SessionState.SetString(Key + "folder", folder);
        SavePref("RhythmMVP.LastRpeJson"); SavePref("RhythmMVP.LastRpeAudio");
        SessionState.SetBool(Key + "selected.exists", PlayerPrefs.HasKey("RhythmMVP.SelectedChart"));
        SessionState.SetString(Key + "selected", PlayerPrefs.GetString("RhythmMVP.SelectedChart", ""));
        SessionState.SetString(Key + "failure", "");
        SessionState.SetBool(Key + "focused", false);
        SessionState.SetFloat(Key + "started", (float)Now);
        RpeImportWindow.Open();
        var window = EditorWindow.GetWindow<RpeImportWindow>();
        SetField(window, "jsonPath", source);
        SetField(window, "audioPath", Path.Combine(Root, "validation/fixtures/rpe/tone.wav"));
        window.Repaint();
        Phase = "starting";
        // Invoke the same method used by the actual Import and Run button.
        typeof(RpeImportWindow).GetMethod("Import", Private).Invoke(window, new object[] { true });
        Hook();
    }
    static void Tick() {
        if (Phase.Length == 0) return;
        try {
            if (Phase == "stopping") {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Cleanup();
                return;
            }
            if (Now - SessionState.GetFloat(Key + "started", (float)Now) > 90) throw new Exception("Editor import/play verification timed out.");
            if (!EditorApplication.isPlaying) return;
            if (!SessionState.GetBool(Key + "focused", false)) {
                Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                EditorWindow.GetWindow(gameView).Focus();
                SessionState.SetBool(Key + "focused", true);
            }
            var game = UnityEngine.Object.FindObjectOfType<GameManager>();
            if (game == null) return;
            string state = Field(game, "screen").ToString();
            if (state == "Menu" && ((string)Field(game, "error")).Length > 0) throw new Exception((string)Field(game, "error"));
            if (Phase == "starting" && state == "Playing" && game.SongTime > 1) {
                var chart = (ChartData)Field(game, "chart");
                var audio = game.GetComponent<AudioSource>();
                if (chart.notes.Length != 7 || !audio.isPlaying || audio.clip == null) throw new Exception("Imported chart/audio did not start correctly.");
                SessionState.SetFloat(Key + "pausedTime", (float)game.SongTime);
                game.SendMessage("Pause");
                SessionState.SetFloat(Key + "pauseStart", (float)Now);
                Phase = "paused";
            } else if (Phase == "paused" && Now - SessionState.GetFloat(Key + "pauseStart", (float)Now) > .4) {
                if (Math.Abs(game.SongTime - SessionState.GetFloat(Key + "pausedTime", 0)) > .02) throw new Exception("Pause did not freeze the chart clock.");
                game.SendMessage("Resume");
                Phase = "resumed";
            } else if (Phase == "resumed" && state == "Playing" && game.SongTime > 3) {
                string output = Path.Combine(Root, "validation/editor-integration");
                Directory.CreateDirectory(output);
                SetField(game, "qaDir", output);
                game.StartCoroutine((System.Collections.IEnumerator)typeof(GameManager).GetMethod("Capture", Private).Invoke(game, new object[] { "playing" }));
                Phase = "captured";
            } else if (Phase == "captured" && state == "Playing" && game.SongTime > 4) {
                Phase = "stopping";
                EditorApplication.isPlaying = false;
            }
        } catch (Exception e) {
            SessionState.SetString(Key + "failure", e.ToString());
            Debug.LogException(e);
            Phase = "stopping";
            EditorApplication.isPlaying = false;
        }
    }
    static void Cleanup() {
        // Only the absent-before-test, deterministic fixture directory can be removed.
        string asset = SessionState.GetString(Key + "folder", "");
        string full = Path.GetFullPath(Path.Combine(Root, "mvp", asset));
        string songs = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "Songs")) + Path.DirectorySeparatorChar;
        if (!asset.StartsWith("Assets/StreamingAssets/Songs/chart-") || !full.StartsWith(songs, StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe verification cleanup path.");
        if (Directory.Exists(full) && !AssetDatabase.DeleteAsset(asset)) throw new Exception("Could not clean the verification chart.");
        RestorePref("RhythmMVP.LastRpeJson"); RestorePref("RhythmMVP.LastRpeAudio");
        if (SessionState.GetBool(Key + "selected.exists", false)) PlayerPrefs.SetString("RhythmMVP.SelectedChart", SessionState.GetString(Key + "selected", ""));
        else PlayerPrefs.DeleteKey("RhythmMVP.SelectedChart");
        SessionState.EraseString("RhythmMVP.StartChart");
        var window = EditorWindow.GetWindow<RpeImportWindow>();
        SetField(window, "jsonPath", EditorPrefs.GetString("RhythmMVP.LastRpeJson", ""));
        SetField(window, "audioPath", EditorPrefs.GetString("RhythmMVP.LastRpeAudio", ""));
        SetField(window, "status", ""); window.Repaint();
        string failure = SessionState.GetString(Key + "failure", "");
        Phase = "";
        string report = Path.Combine(Root, "validation/editor-integration-result.json");
        if (failure.Length == 0) {
            BuildMvp.Build();
            File.WriteAllText(report, new JObject { ["ok"] = true, ["importAndPlay"] = true, ["audioPlaying"] = true, ["pauseResume"] = true, ["testChartRemoved"] = !Directory.Exists(full) }.ToString());
            Debug.Log("EDITOR_IMPORT_VERIFIED " + report);
        } else File.WriteAllText(report, new JObject { ["ok"] = false, ["error"] = failure, ["testChartRemoved"] = !Directory.Exists(full) }.ToString());
        window.Focus();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public sealed class GameManager : MonoBehaviour {
    enum ScreenState { Menu, Loading, Playing, Paused, Resuming, Result, Calibration, Story }
    ScreenState screen = ScreenState.Menu;
    readonly KeyCode[] keys = { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F };
    readonly List<ChartData> charts = new List<ChartData>();
    AudioSource audioSource;
    RhythmFeedback feedback;
    RhythmInput rhythmInput;
    RhythmPlayfield playfield;
    RhythmCalibration calibration;
    StageTimeline stage;
    readonly SongClock clock = new SongClock();
    readonly Dictionary<string, Tuple<long,long,ChartData>> chartCache = new Dictionary<string, Tuple<long,long,ChartData>>();
    AudioClip loadedClip;
    ChartData chart;
    RhythmSession session;
    int selected, offsetMs, songOffsetMs, settingsTab;
    bool practice, loopPractice;
    float playbackRate = 1;
    string practiceFrom = "0", practiceTo = "0", stageMessage = "";
    double rangeStart, rangeEnd, resumeAt, audioStart;
    string loadedAudioPath = "", scoreKey = "";
    long loadedAudioStamp, loadedAudioLength;
    Action<RhythmInput.Edge> consumeInput;
    float speed = 330;
    double originDsp, pausedTime;
    string error = "", judge = "";
    float judgeUntil;
    Vector2 scroll;
    GUIStyle normal, small, title, centered, button, big, laneText, field;
    Font font;
    bool[] held = new bool[4];
    bool stylesReady;
    const float LaneLeft = 424, LaneWidth = 108, JudgeY = 532;
    string qaMode, qaDir, qaChart;
    bool qaMenu, qaPlay, qaPause, qaResult, qaHold;
    bool qaFastEnd, qaSkippedTail;
    double qaPauseRealtime;
    int qaNext;
    int qaPracticeLoops;
    bool QaAutoplay { get { return qaMode == "perfect" || qaMode == "practice"; } }
    public double SongTime { get { return clock.Now(AudioSettings.dspTime); } }
    double Offset { get { return (offsetMs * (practice ? playbackRate : 1) + songOffsetMs) / 1000.0; } }
    double JudgeTime { get { return SongTime - Offset; } }
    string SongsFolder { get { return Path.Combine(Application.streamingAssetsPath, "Songs"); } }

    void Start() {
        audioSource = GetComponent<AudioSource>();
        feedback = gameObject.AddComponent<RhythmFeedback>();
        rhythmInput = gameObject.AddComponent<RhythmInput>();
        calibration = gameObject.AddComponent<RhythmCalibration>();
        stage = FindObjectOfType<StageTimeline>();
        if (stage == null) stage = gameObject.AddComponent<StageTimeline>();
        playfield = RhythmPlayfield.Create(); playfield.gameObject.SetActive(false);
        consumeInput = ConsumeInput;
        feedback.Volume = PlayerPrefs.GetFloat("RhythmHitVolume", .45f);
        audioSource.volume = 0.65f;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
        Application.runInBackground = true;
        offsetMs = PlayerPrefs.GetInt("RhythmOffsetMs", 0);
        speed = PlayerPrefs.GetFloat("RhythmSpeed", 330);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        foreach (string arg in Environment.GetCommandLineArgs()) {
            if (arg.StartsWith("--verify-run=")) qaMode = arg.Substring(13);
            if (arg.StartsWith("--verify-output=")) qaDir = arg.Substring(16);
            if (arg.StartsWith("--verify-chart=")) qaChart = arg.Substring(15);
            if (arg == "--verify-fast-end") qaFastEnd = true;
        }
#endif
        RefreshCharts();
#if UNITY_EDITOR
        string startChart = UnityEditor.SessionState.GetString("RhythmMVP.StartChart", "");
        UnityEditor.SessionState.EraseString("RhythmMVP.StartChart");
        if (!String.IsNullOrEmpty(startChart)) {
            int startIndex = charts.FindIndex(c => c.sourcePath == startChart);
            if (startIndex >= 0) { selected = startIndex; Begin(); }
        }
#endif
        if (!String.IsNullOrEmpty(qaMode) && !String.IsNullOrEmpty(qaChart)) {
            charts.Clear();
            charts.Add(ChartLoader.LoadFile(qaChart));
            selected = 0;
            error = "";
        }
        if (!String.IsNullOrEmpty(qaMode)) {
            offsetMs = 0;
            speed = 330;
            if (qaMode == "practice") { practice = loopPractice = true; practiceFrom = "1"; practiceTo = "9"; playbackRate = .75f; }
            if (!String.IsNullOrEmpty(qaDir)) Directory.CreateDirectory(qaDir);
            StartCoroutine(QaStart());
        }
    }

    void RefreshCharts() {
        string previous = charts.Count > 0 ? charts[Mathf.Clamp(selected, 0, charts.Count - 1)].sourcePath : PlayerPrefs.GetString("RhythmMVP.SelectedChart", "");
        charts.Clear();
        error = "";
        if (!Directory.Exists(SongsFolder)) Directory.CreateDirectory(SongsFolder);
        string[] paths = Directory.GetFiles(SongsFolder, "*.json", SearchOption.AllDirectories);
        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths) {
            if (path.EndsWith(".stage.json", StringComparison.OrdinalIgnoreCase)) continue;
            try { charts.Add(ReadCachedChart(path)); }
            catch (Exception e) { error += Path.GetFileName(path) + "：" + e.Message + "\n"; }
        }
        selected = Math.Max(0, charts.FindIndex(c => c.sourcePath == previous));
        if (charts.Count == 0 && error.Length == 0) error = "还没有谱面。在 Unity 顶部“音游”菜单导入，或点击这里的“导入谱面”。";
    }

    ChartData ReadCachedChart(string path) {
        var file = new FileInfo(path);
        Tuple<long,long,ChartData> cached;
        if (chartCache.TryGetValue(path,out cached) && cached.Item1 == file.LastWriteTimeUtc.Ticks && cached.Item2 == file.Length) return cached.Item3;
        ChartData value = ChartLoader.LoadFile(path);
        chartCache[path] = Tuple.Create(file.LastWriteTimeUtc.Ticks,file.Length,value);
        return value;
    }

    void OpenChart() {
        try {
            string path = NativeChartPicker.Open(SongsFolder);
            if (String.IsNullOrEmpty(path)) return;
            RpeChartLoader.Parse(File.ReadAllText(path));
            string audio = ChartImportService.FindAudio(path);
            if (audio == null) audio = NativeChartPicker.OpenAudio(Path.GetDirectoryName(path));
            if (String.IsNullOrEmpty(audio)) return;
            string imported = ChartImportService.Import(path, audio, SongsFolder);
            ChartData opened = ChartLoader.LoadFile(imported);
            int found = charts.FindIndex(c => c.sourcePath == opened.sourcePath);
            if (found >= 0) { charts[found] = opened; selected = found; }
            else { charts.Add(opened); selected = charts.Count - 1; }
            error = "";
        } catch (Exception e) { error = e.Message; }
    }

    void Begin() {
        if (charts.Count == 0 || screen == ScreenState.Loading) return;
        if (charts[selected].notes.Length == 0) { error = "这是空谱面，请先放置音符并重新导入。"; return; }
        if (String.IsNullOrEmpty(qaMode)) SaveSettings();
        if (practice && (!Double.TryParse(practiceFrom, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out rangeStart)
            || !Double.TryParse(practiceTo, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out rangeEnd)
            || Double.IsNaN(rangeStart) || Double.IsInfinity(rangeStart) || Double.IsNaN(rangeEnd) || Double.IsInfinity(rangeEnd)
            || rangeStart < 0 || rangeEnd < 0 || (rangeEnd > 0 && rangeEnd <= rangeStart))) {
            error = "练习区间需要有效秒数，结束应晚于开始；结束填 0 表示曲终。"; return;
        }
        if (!practice) { rangeStart = 0; rangeEnd = 0; playbackRate = 1; }
        songOffsetMs = String.IsNullOrEmpty(qaMode) ? PlayerPrefs.GetInt("SongOffset:" + charts[selected].sourcePath,0) : 0;
        StartCoroutine(LoadAndPlay(charts[selected].sourcePath));
    }
    IEnumerator LoadAndPlay(string path) {
        screen = ScreenState.Loading;
        error = "";
        audioSource.Stop();
        feedback.StopAudio(); rhythmInput.Clear();
        chart = null;
        string audioPath = "";
        try {
            chart = ChartLoader.LoadFile(path);
            if (chart.notes.Length == 0) throw new FormatException("这是空谱面，请先放置音符并重新导入。");
            audioPath = ChartLoader.AudioPath(chart);
            if (!File.Exists(audioPath)) throw new FileNotFoundException("找不到音频：" + chart.audioName + "。请把音频与谱面放在一起。");
        } catch (Exception e) { error = e.Message; }
        if (error.Length > 0) { screen = ScreenState.Menu; yield break; }
        AudioType type;
        switch (Path.GetExtension(audioPath).ToLowerInvariant()) {
            case ".wav": type = AudioType.WAV; break;
            case ".ogg": type = AudioType.OGGVORBIS; break;
            case ".mp3": type = AudioType.MPEG; break;
            default: error = "支持 WAV、OGG、MP3 音频。"; screen = ScreenState.Menu; yield break;
        }
        var audioFile = new FileInfo(audioPath);
        if (loadedClip == null || loadedAudioPath != audioPath || loadedAudioStamp != audioFile.LastWriteTimeUtc.Ticks || loadedAudioLength != audioFile.Length) {
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(audioPath).AbsoluteUri, type)) {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) {
                error = "音频加载失败：" + request.error;
                screen = ScreenState.Menu;
                yield break;
            }
            if (loadedClip != null) Destroy(loadedClip);
            loadedClip = DownloadHandlerAudioClip.GetContent(request);
            loadedAudioPath = audioPath; loadedAudioStamp = audioFile.LastWriteTimeUtc.Ticks; loadedAudioLength = audioFile.Length;
        }
        }
        if (loadedClip == null || loadedClip.length <= 0) { error = "音频为空或无法解码。"; screen = ScreenState.Menu; yield break; }
        double lastEnd = 0;
        foreach (NoteData note in chart.notes) lastEnd = Math.Max(lastEnd, note.endTime);
        if (lastEnd > loadedClip.length + 0.15) {
            error = "谱面最后音符超出音频长度，请检查配套音频。";
            screen = ScreenState.Menu;
            yield break;
        }
        charts[selected] = chart;
        if (practice) {
            rangeEnd = rangeEnd == 0 ? loadedClip.length : Math.Min(rangeEnd,loadedClip.length);
            if (rangeStart >= rangeEnd) { error = "练习开始时间超出了音乐长度。"; screen = ScreenState.Menu; yield break; }
            var included = Array.FindAll(chart.notes,n => n.time >= rangeStart-Offset && n.endTime <= rangeEnd-Offset);
            if (included.Length == 0) { error = "此区间没有完整音符；请扩大范围，覆盖长按头尾。"; screen = ScreenState.Menu; yield break; }
            chart = new ChartData { title = chart.title, artist = chart.artist, difficulty = chart.difficulty, audioName = chart.audioName,
                sourcePath = chart.sourcePath, warnings = chart.warnings, notes = included };
        }
        session = new RhythmSession(chart.notes,practice ? playbackRate : 1);
        feedback.Bind(session);
        session.HitStarted += OnHitStarted;
        session.Judged += OnJudged;
        playfield.Bind(session,feedback.State);
        stageMessage = stage.Load(chart.sourcePath);
        stage.Seek(practice ? rangeStart : double.NegativeInfinity);
        using (var hash = System.Security.Cryptography.SHA256.Create())
            scoreKey = "Best:" + BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(chart.sourcePath))).Replace("-","");
        judge = "";
        Array.Clear(held, 0, held.Length);
        qaNext = 0;
        audioSource.clip = loadedClip;
        audioSource.pitch = (float)session.PlaybackRate;
        audioStart = practice ? rangeStart : 0;
        audioSource.time = (float)audioStart;
        originDsp = AudioSettings.dspTime + Math.Max(2.0, 1.5 - (chart.notes[0].time+Offset-audioStart)/session.PlaybackRate);
        clock.Start(originDsp,audioStart,session.PlaybackRate);
        audioSource.PlayScheduled(originDsp);
        screen = ScreenState.Playing;
        Debug.Log("RHYTHM_START " + Path.GetFileName(path) + " notes=" + chart.notes.Length + " audio=" + loadedClip.length);
    }

    void Update() {
        if (screen == ScreenState.Calibration) { rhythmInput.Drain(consumeInput); return; }
        if (screen == ScreenState.Resuming) {
            rhythmInput.Clear();
            if (Input.GetKeyDown(KeyCode.Escape)) { screen = ScreenState.Paused; return; }
            if (Time.realtimeSinceStartupAsDouble >= resumeAt) CompleteResume();
            return;
        }
        if (screen == ScreenState.Menu && Input.GetKeyDown(KeyCode.Return)) Begin();
        if (screen == ScreenState.Result && Input.GetKeyDown(KeyCode.R)) Begin();
        if (screen == ScreenState.Paused) {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) Resume();
            if (qaPause && !String.IsNullOrEmpty(qaMode) && Time.realtimeSinceStartupAsDouble - qaPauseRealtime > 0.5) Resume();
            return;
        }
        if (screen != ScreenState.Playing) { rhythmInput.Clear(); return; }
        if (Input.GetKeyDown(KeyCode.Escape)) { Pause(); return; }
        double time = JudgeTime;
        feedback.State.Tick(Time.unscaledDeltaTime, time);
        if (String.IsNullOrEmpty(qaMode)) {
            rhythmInput.Drain(consumeInput);
            for (int lane = 0; lane < 4; lane++) {
                held[lane] = rhythmInput.Held[lane];
                feedback.State.SetKey(lane, held[lane], false);
            }
        } else {
            if (QaAutoplay) {
                while (qaNext < chart.notes.Length && chart.notes[qaNext].time <= time) {
                    session.Press(chart.notes[qaNext].lane, time);
                    qaNext++;
                }
                for (int lane = 0; lane < 4; lane++) held[lane] = false;
                for (int i = 0; i < chart.notes.Length; i++)
                    if (session.States[i] == NoteState.Holding) held[chart.notes[i].lane] = true;
                for (int lane = 0; lane < 4; lane++) feedback.State.SetKey(lane, held[lane], false);
            }
            if (!qaPlay && time > 4) { qaPlay = true; StartCoroutine(Capture("playing")); }
            if (!qaHold) foreach (var lane in feedback.State.Lanes) {
                if (lane.HoldNote >= 0 && lane.HoldProgress >= .35 && lane.HoldProgress < .8) {
                    qaHold = true; StartCoroutine(Capture("holding")); break;
                }
            }
            if (!qaPause && time > 7) {
                qaPause = true;
                Pause();
                qaPauseRealtime = Time.realtimeSinceStartupAsDouble;
                StartCoroutine(Capture("paused"));
                return;
            }
        }
        session.Advance(time);
        stage.Tick(SongTime);
        if (practice && SongTime >= rangeEnd) {
            if (qaMode == "practice" && ++qaPracticeLoops >= 2) loopPractice = false;
            if (loopPractice) Begin(); else EndSong();
            return;
        }
        // Verification only: after judging every note, seek over an empty audio
        // tail and exercise the actual audio-end/result transition.
        if (qaFastEnd && !qaSkippedTail && !String.IsNullOrEmpty(qaMode) && session.JudgedCount == chart.notes.Length) {
            double lastEnd = 0;
            foreach (var note in chart.notes) lastEnd = Math.Max(lastEnd, note.endTime);
            if (time > lastEnd + 1 && loadedClip.length - SongTime > 2) {
                qaSkippedTail = true;
                audioSource.time = loadedClip.length - 1;
                originDsp = AudioSettings.dspTime - audioSource.time;
                clock.Start(AudioSettings.dspTime,audioSource.time,session.PlaybackRate);
            }
        }
        if (SongTime >= loadedClip.length + 0.4 + Math.Max(0, Offset)) EndSong();
    }

    void ConsumeInput(RhythmInput.Edge edge) {
        if (screen == ScreenState.Calibration) { if (edge.Down) calibration.Tap(edge.Time); return; }
        if (screen != ScreenState.Playing) return;
        double at = clock.EventTime(edge.Time,Time.realtimeSinceStartupAsDouble,AudioSettings.dspTime)-Offset;
        feedback.State.SetKey(edge.Lane,edge.Down,edge.Down);
        if (edge.Down) session.Press(edge.Lane,at); else session.Release(edge.Lane,at);
    }
    void LateUpdate() {
        bool show = screen == ScreenState.Playing || screen == ScreenState.Paused || screen == ScreenState.Resuming;
        playfield.gameObject.SetActive(show);
        if (show) { playfield.Accent = stage.Accent; playfield.Present(JudgeTime,speed); }
    }

    void OnHitStarted(int note, Judgement value) {
        judge = value + (chart.notes[note].IsHold ? " · HOLD" : "") + "\n" + TimingLabel(session.LastErrorMs);
        judgeUntil = Time.unscaledTime + 0.35f;
    }
    void OnJudged(int note, Judgement value) {
        if (value == Judgement.Miss || chart.notes[note].IsHold) judge = value.ToString();
        judgeUntil = Time.unscaledTime + 0.4f;
    }
    void Pause() {
        if (screen != ScreenState.Playing) return;
        pausedTime = SongTime;
        clock.Pause(AudioSettings.dspTime);
        rhythmInput.Clear();
        audioSource.Pause();
        feedback.StopAudio();
        screen = ScreenState.Paused;
    }
    void Resume() {
        if (screen != ScreenState.Paused) return;
        resumeAt = Time.realtimeSinceStartupAsDouble + 3;
        screen = ScreenState.Resuming;
        rhythmInput.Clear();
    }
    void CompleteResume() {
        clock.Resume(AudioSettings.dspTime);
        if (pausedTime < audioStart) {
            originDsp = AudioSettings.dspTime+(audioStart-pausedTime)/session.PlaybackRate;
            audioSource.Stop(); audioSource.time = (float)audioStart; audioSource.PlayScheduled(originDsp);
        } else audioSource.UnPause();
        rhythmInput.Clear();
        screen = ScreenState.Playing;
        if (String.IsNullOrEmpty(qaMode)) for (int lane = 0; lane < 4; lane++)
            if (!rhythmInput.Held[lane]) session.Release(lane,JudgeTime);
    }
    void OnApplicationFocus(bool focus) {
        if (!focus && String.IsNullOrEmpty(qaMode)) {
            if (screen == ScreenState.Resuming) screen = ScreenState.Paused;
            else if (screen == ScreenState.Calibration) { calibration.Stop(); screen = ScreenState.Menu; }
            else Pause();
        }
    }
    void OnApplicationPause(bool paused) { if (paused) OnApplicationFocus(false); }
    void EndSong() {
        session.FinishAll();
        audioSource.Stop();
        screen = ScreenState.Result;
        if (!practice && String.IsNullOrEmpty(qaMode)) {
            PlayerPrefs.SetFloat(scoreKey,Mathf.Max(PlayerPrefs.GetFloat(scoreKey,0),(float)session.Completion));
            PlayerPrefs.Save();
        }
        Debug.Log("RHYTHM_RESULT perfect=" + session.Perfect + " good=" + session.Good + " miss=" + session.Miss + " maxCombo=" + session.MaxCombo);
        if (!String.IsNullOrEmpty(qaMode) && !qaResult) { qaResult = true; StartCoroutine(QaFinish()); }
    }
    void Menu() {
        calibration.Stop(); rhythmInput.Clear();
        audioSource.Stop();
        feedback.StopAudio();
        screen = ScreenState.Menu;
        Array.Clear(held, 0, held.Length);
        judge = "";
    }

    void SetupStyles() {
        if (stylesReady) return;
        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        normal = new GUIStyle(GUI.skin.label) { font = font, fontSize = 24, wordWrap = true };
        normal.normal.textColor = Color.white;
        small = new GUIStyle(normal) { fontSize = 18 };
        title = new GUIStyle(normal) { fontSize = 38 };
        centered = new GUIStyle(normal) { alignment = TextAnchor.MiddleCenter };
        laneText = new GUIStyle(centered) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = false };
        big = new GUIStyle(centered) { fontSize = 46 };
        button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 22 };
        field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 18 };
        stylesReady = true;
    }
    void OnGUI() {
        SetupStyles();
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
        GUI.color = Color.white;
        if (screen == ScreenState.Menu) DrawMenu();
        else if (screen == ScreenState.Calibration) DrawCalibration();
        else if (screen == ScreenState.Story) DrawStory();
        else if (screen == ScreenState.Loading) GUI.Label(new Rect(300, 300, 680, 80), "正在加载音频…", big);
        else if (screen == ScreenState.Result) DrawResult();
        else {
            DrawPlayfield();
            if (screen == ScreenState.Paused) DrawPause();
            if (screen == ScreenState.Resuming) {
                GUI.color = new Color(0,0,0,.8f); GUI.DrawTexture(new Rect(0,0,1280,720),Texture2D.whiteTexture); GUI.color = Color.white;
                GUI.Label(new Rect(390,240,500,80),Math.Ceiling(resumeAt-Time.realtimeSinceStartupAsDouble).ToString(),big);
                GUI.Label(new Rect(300,350,680,100),"准备继续\n有未结束的长按时，请先按住对应按键。",centered);
            }
        }
    }

    void DrawMenu() {
        GUI.Label(new Rect(70, 38, 1100, 60), "四轨音游 · 演奏与练习", title);
        GUI.Label(new Rect(72, 105, 1120, 38), "A / S / D / F　单键、同时按键、长按　｜　完成度 60% 及格", small);
        scroll = GUI.BeginScrollView(new Rect(70, 170, 610, 310), scroll, new Rect(0, 0, 575, Math.Max(305, charts.Count * 84)));
        if (charts.Count == 0) {
            GUI.Label(new Rect(30, 65, 515, 60), "尚未导入谱面", centered);
            GUI.Label(new Rect(45, 142, 485, 100), "在 PhiEdit 写好谱面并导出 JSON，\n再导入配套音频即可开始。", small);
        }
        for (int i = 0; i < charts.Count; i++) {
            ChartData c = charts[i];
            if (GUI.Button(new Rect(0, i * 84, 575, 74), (i == selected ? "● " : "") + c.title + "\n" + c.difficulty + "　·　" + c.notes.Length + " 音符", button)) selected = i;
        }
        GUI.EndScrollView();
        DrawSettings();
        GUI.enabled = charts.Count > 0 && charts[selected].notes.Length > 0;
        if (GUI.Button(new Rect(70, 504, 260, 60), "开始演奏  Enter", button)) Begin();
        GUI.enabled = true;
        if (GUI.Button(new Rect(350, 504, 160, 60), "导入谱面", button)) OpenChart();
        if (GUI.Button(new Rect(530, 504, 150, 60), "刷新列表", button)) RefreshCharts();
        if (GUI.Button(new Rect(735, 504, 180, 60), "谱面文件夹", button)) Application.OpenURL(new Uri(SongsFolder).AbsoluteUri);
        if (GUI.Button(new Rect(935, 504, 200, 60), "退出", button)) { SaveSettings(); Application.Quit(); }
        string message = error;
        if (message.Length == 0 && charts.Count > 0 && charts[selected].warnings.Length > 0) message = String.Join("\n", charts[selected].warnings);
        GUI.Label(new Rect(70, 590, 1135, 92), message.Length > 0 ? message : "PhiEdit 导出 JSON → 导入谱面 → 开始演奏。修改后重新导入同一份文件即可更新。", small);
    }
    void SaveSettings() {
        PlayerPrefs.SetInt("RhythmOffsetMs", offsetMs); PlayerPrefs.SetFloat("RhythmSpeed", speed); PlayerPrefs.SetFloat("RhythmHitVolume", feedback.Volume);
        if (charts.Count > 0) PlayerPrefs.SetString("RhythmMVP.SelectedChart",charts[selected].sourcePath);
        PlayerPrefs.Save();
    }
    static string TimingLabel(double errorMs) { return Math.Abs(errorMs) <= 1 ? "准时" : (errorMs < 0 ? "偏快 Early " : "偏慢 Late ") + Math.Abs(errorMs).ToString("F0") + " ms"; }
    void DrawSettings() {
        string[] tabs = { "游玩", "对音", "练习" };
        for (int i = 0; i < 3; i++) if (GUI.Button(new Rect(735+i*140,170,132,42),(settingsTab==i ? "● " : "")+tabs[i],button)) settingsTab=i;
        if (settingsTab == 0) {
            GUI.Label(new Rect(735,235,420,35),"下落速度  " + speed.ToString("F0"),small);
            Fill(new Rect(737,287,400,3),.28f);
            speed = GUI.HorizontalSlider(new Rect(737,281,400,25),speed,200,520);
            GUI.Label(new Rect(735,325,420,35),"打击音量  " + Mathf.RoundToInt(feedback.Volume*100) + "%",small);
            Fill(new Rect(737,377,400,3),.28f);
            feedback.Volume = GUI.HorizontalSlider(new Rect(737,371,400,25),feedback.Volume,0,1);
            GUI.Label(new Rect(735,414,430,65),"按键 A / S / D / F\n判定后显示偏快、偏慢及毫秒误差。",small);
        } else if (settingsTab == 1) {
            GUI.Label(new Rect(735,228,440,32),"全局偏移  " + offsetMs + " ms",small);
            offsetMs = OffsetButtons(735,267,offsetMs);
            if (charts.Count > 0) {
                string key = "SongOffset:" + charts[selected].sourcePath;
                int value = PlayerPrefs.GetInt(key,0);
                GUI.Label(new Rect(735,317,440,32),"当前曲目微调  " + value + " ms",small);
                int changed = OffsetButtons(735,356,value);
                if (changed != value) { PlayerPrefs.SetInt(key,changed); PlayerPrefs.Save(); }
            }
            if (GUI.Button(new Rect(735,409,180,40),"节拍校准",button)) { rhythmInput.Clear(); calibration.Begin(); screen = ScreenState.Calibration; }
            GUI.Label(new Rect(735,457,430,32),"正值：判定线到达时间延后。",small);
        } else {
            practice = GUI.Toggle(new Rect(735,226,420,32),practice,(practice ? "■ " : "□ ") + "启用练习（不保存正式成绩）",small);
            GUI.enabled = practice;
            GUI.Label(new Rect(735,270,95,32),"开始秒",small);
            practiceFrom = GUI.TextField(new Rect(835,269,125,32),practiceFrom,field);
            GUI.Label(new Rect(978,270,70,32),"结束秒",small);
            practiceTo = GUI.TextField(new Rect(1050,269,110,32),practiceTo,field);
            GUI.Label(new Rect(735,312,430,32),"结束填 0 为曲终；跨区间长按不计入。",small);
            Fill(new Rect(737,368,400,3),practice ? .28f : .12f);
            playbackRate = Mathf.Round(GUI.HorizontalSlider(new Rect(737,362,400,25),playbackRate,.5f,1.5f)*20)/20;
            GUI.Label(new Rect(735,390,450,32),"播放速度 " + playbackRate.ToString("F2") + "×（音调随速度变化）",small);
            loopPractice = GUI.Toggle(new Rect(735,438,420,32),loopPractice,(loopPractice ? "■ " : "□ ") + "循环这个区间",small);
            GUI.enabled = true;
        }
    }
    int OffsetButtons(float x, float y, int value) {
        string[] labels = { "−10", "−1", "+1", "+10", "归零" };
        int[] changes = { -10,-1,1,10,0 };
        for (int i = 0; i < 5; i++) if (GUI.Button(new Rect(x+i*84,y,77,37),labels[i],button)) value = i == 4 ? 0 : Mathf.Clamp(value+changes[i],-300,300);
        return value;
    }
    void DrawCalibration() {
        GUI.Label(new Rect(220,60,840,70),"节拍校准",big);
        GUI.Label(new Rect(220,155,840,100),"跟随听到的节拍，反复按 A。\n前四拍用于准备；请均匀敲击，不追随画面。",centered);
        GUI.Label(new Rect(220,278,840,70),"已采样 " + calibration.Count + " 次 / 至少 12 次",title);
        GUI.Label(new Rect(220,360,840,100),calibration.Ready ? "建议偏移 " + calibration.SuggestedMs + " ms　·　中间样本跨度 " + calibration.SpreadMs.ToString("F0") + " ms" : calibration.Ended ? "采样不足，请重新开始。" : "正在听拍采样…",centered);
        GUI.enabled = calibration.Ready;
        if (GUI.Button(new Rect(220,500,250,60),"应用建议",button)) { offsetMs = Mathf.Clamp(calibration.SuggestedMs,-300,300); SaveSettings(); Menu(); }
        GUI.enabled = true;
        if (GUI.Button(new Rect(510,500,250,60),"重新校准",button)) { rhythmInput.Clear(); calibration.Begin(); }
        if (GUI.Button(new Rect(800,500,250,60),"返回",button)) Menu();
        GUI.Label(new Rect(220,605,840,50),"此结果包含设备延迟与个人敲击习惯；波动较大时建议重测。",small);
    }
    void DrawPlayfield() {
        GUI.Label(new Rect(48, 35, 355, 75), chart.title, normal);
        GUI.Label(new Rect(48, 132, 320, 110), "COMBO\n" + session.Combo, title);
        GUI.Label(new Rect(48, 284, 335, 80), "完成度  " + session.Completion.ToString("F1") + "%", normal);
        if (Time.unscaledTime < judgeUntil) GUI.Label(new Rect(48, 410, 330, 60), judge, centered);
        GUI.Label(new Rect(930, 70, 310, 175), "Perfect  " + session.Perfect + "\nGood     " + session.Good + "\nMiss       " + session.Miss, normal);
        GUI.Label(new Rect(930, 300, 290, 75), Math.Max(0, SongTime).ToString("F1") + " / " + loadedClip.length.ToString("F1") + " 秒", small);
        GUI.Label(new Rect(930, 408, 300, 150), "Esc 暂停\nEarly " + session.Early + " / Late " + session.Late + "\n平均偏差 " + session.MeanErrorMs.ToString("F1") + " ms\n" + (practice ? "练习 " + playbackRate.ToString("F2") + "×" : "正式演奏"), small);
        GUI.BeginGroup(new Rect(LaneLeft, 34, LaneWidth * 4, 620));
        float localJudgeY = JudgeY - 34;
        for (int i = 0; i < 4; i++) {
            GUI.Label(new Rect(i * LaneWidth, localJudgeY+42, LaneWidth, 43), keys[i].ToString(), centered);
            var lane = feedback.State.Lanes[i];
            if (lane.HoldNote >= 0) GUI.Label(new Rect(i*LaneWidth,localJudgeY+17,LaneWidth,24),"HOLD " + Mathf.FloorToInt((float)lane.HoldProgress*100) + "%",laneText);
        }
        if (SongTime < audioStart) GUI.Label(new Rect(0, 240, LaneWidth * 4, 65), Math.Ceiling((audioStart-SongTime)/session.PlaybackRate).ToString(), big);
        GUI.EndGroup();
        GUI.Label(new Rect(48,560,345,65),stage.Instrument,normal);
        GUI.Label(new Rect(48,639,1160,65),String.IsNullOrEmpty(stage.Lyric) ? (String.IsNullOrEmpty(stageMessage) ? "A / S / D / F　　Perfect ±60 ms · Good ±140 ms" : stageMessage) : stage.Lyric,small);
    }
    void Fill(Rect rect, float gray) {
        GUI.color = new Color(gray, gray, gray, 1);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
    void DrawPause() {
        GUI.color = new Color(0, 0, 0, 0.92f);
        GUI.DrawTexture(new Rect(0, 0, 1280, 720), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(390, 180, 500, 80), "已暂停", big);
        if (GUI.Button(new Rect(440, 300, 400, 60), "继续  Space", button)) Resume();
        if (GUI.Button(new Rect(440, 385, 400, 60), "重新开始", button)) Begin();
        if (GUI.Button(new Rect(440, 470, 400, 60), "返回选歌", button)) Menu();
    }
    void DrawResult() {
        GUI.Label(new Rect(200, 55, 880, 80), practice ? "练习完成" : session.Passed ? "演奏完成 · 及格" : "演奏完成 · 再试一次", big);
        GUI.Label(new Rect(200, 151, 880, 65), chart.title, centered);
        GUI.Label(new Rect(200, 228, 880, 75), "完成度  " + session.Completion.ToString("F1") + "%", big);
        GUI.Label(new Rect(260, 336, 760, 55), "Perfect  " + session.Perfect + "      Good  " + session.Good + "      Miss  " + session.Miss, centered);
        GUI.Label(new Rect(260, 405, 760, 50), "最大连击  " + session.MaxCombo + "　｜　音符总数  " + chart.notes.Length, centered);
        DrawTimingHistogram();
        if (GUI.Button(new Rect(320, 521, 300, 65), "重新演奏  R", button)) Begin();
        if (GUI.Button(new Rect(660, 521, 300, 65), "返回选歌", button)) Menu();
        if (!practice && session.Passed && !String.IsNullOrWhiteSpace(stage.Track.ending)) {
            if (GUI.Button(new Rect(440,600,400,45),"进入演出后记",button)) screen = ScreenState.Story;
        }
        GUI.Label(new Rect(260,657,760,40),practice ? "练习成绩不写入纪录。" : !String.IsNullOrEmpty(qaMode) ? "自动验证演奏，不写入最佳纪录。" : "本谱面最佳 " + PlayerPrefs.GetFloat(scoreKey,0).ToString("F1") + "% · 长按按一颗音符计分。",small);
    }
    void DrawTimingHistogram() {
        int[] bins = new int[14]; int max = 1;
        foreach (double value in session.ErrorsMs) { int bin = Mathf.Clamp((int)((value+140)/20),0,13); bins[bin]++; max = Math.Max(max,bins[bin]); }
        for (int i = 0; i < bins.Length; i++) {
            float height = 30f*bins[i]/max;
            GUI.color = i < 7 ? RhythmFeedback.Good : RhythmFeedback.Perfect;
            GUI.DrawTexture(new Rect(390+i*35,490-height,29,height),Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(260,492,760,27),"← 偏快 " + session.Early + "　　平均 " + session.MeanErrorMs.ToString("F1") + " ms　　偏慢 " + session.Late + " →",laneText);
    }
    void DrawStory() {
        GUI.Label(new Rect(180,70,920,70),chart.title,centered);
        GUI.Label(new Rect(230,190,820,320),stage.Track.ending,centered);
        if (GUI.Button(new Rect(440,560,400,60),"返回选歌",button)) Menu();
    }
    void OnDestroy() {
        if (loadedClip != null) Destroy(loadedClip);
        if (playfield != null) Destroy(playfield.transform.parent.gameObject);
    }

    IEnumerator QaStart() {
        yield return new WaitForSecondsRealtime(1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string inputReport;
        try { inputReport = RuntimeInputChecks.Run(); }
        catch (Exception e) { inputReport = "FAIL: " + e; }
        if (!String.IsNullOrEmpty(qaDir)) File.WriteAllText(Path.Combine(qaDir,"input-report.txt"),inputReport);
        Debug.Log(inputReport);
        if (inputReport.StartsWith("FAIL")) { Application.Quit(1); yield break; }
#endif
        if (!qaMenu) { qaMenu = true; yield return Capture("menu"); }
        if (qaMode == "perfect") {
            settingsTab=1; yield return Capture("settings-offset");
            settingsTab=2; yield return Capture("settings-practice");
            calibration.Begin(); screen=ScreenState.Calibration; yield return Capture("calibration"); calibration.Stop(); screen=ScreenState.Menu;
            settingsTab=0;
        }
        if (qaMode == "empty") {
            yield return new WaitForSecondsRealtime(1);
            bool empty = charts.Count == 0;
            File.WriteAllText(Path.Combine(qaDir, "report.json"), "{\"mode\":\"empty\",\"ok\":" + (empty ? "true" : "false") + "}");
            Application.Quit(empty ? 0 : 1);
            yield break;
        }
        Begin();
    }
    IEnumerator Capture(string name) {
        if (String.IsNullOrEmpty(qaDir)) yield break;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(qaDir, name + ".png"));
    }
    IEnumerator QaFinish() {
        yield return Capture("result");
        yield return new WaitForSecondsRealtime(1);
        bool ok = QaAutoplay ? session.Perfect == chart.notes.Length && session.Miss == 0 : session.Miss == chart.notes.Length;
        if (qaMode == "practice") ok = ok && qaPracticeLoops == 2;
        string report = "{\"mode\":\"" + qaMode + "\",\"ok\":" + (ok ? "true" : "false") + ",\"notes\":" + chart.notes.Length + ",\"perfect\":" + session.Perfect + ",\"good\":" + session.Good + ",\"miss\":" + session.Miss + ",\"pauseTest\":" + (qaPause ? "true" : "false") + ",\"feedbackHeads\":" + feedback.State.AcceptedHeads + ",\"holdStarts\":" + feedback.State.HoldStarts + ",\"holdCompletions\":" + feedback.State.HoldCompletions + ",\"holdBreaks\":" + feedback.State.HoldBreaks + "}";
        report = report.Substring(0, report.Length-1) + ",\"skippedEmptyTail\":" + (qaSkippedTail ? "true" : "false") + "}";
        report = report.Substring(0, report.Length-1) + ",\"practiceLoops\":" + qaPracticeLoops + ",\"playbackRate\":" + session.PlaybackRate.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
        if (!String.IsNullOrEmpty(qaDir)) File.WriteAllText(Path.Combine(qaDir, "report.json"), report);
        Debug.Log("RHYTHM_VERIFY " + report);
        Application.Quit(ok ? 0 : 1);
    }
}

using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

[Serializable] public sealed class StageCue {
    public double time;
    public string kind = "lyric", value = "";
}
[Serializable] public sealed class StageTrack { public StageCue[] cues = new StageCue[0]; public string ending = ""; }
[Serializable] public sealed class StageCueEvent : UnityEvent<string, string> { }

// Optional chart.stage.json, kept separate from the user's RPE file.
// Seek reconstructs persistent visual state without replaying one-shot animations.
public sealed class StageTimeline : MonoBehaviour {
    public StageCueEvent OnCue = new StageCueEvent();
    public StageTrack Track { get; private set; } = new StageTrack();
    public string Lyric { get; private set; } = "";
    public string Instrument { get; private set; } = "";
    public Color Accent { get; private set; } = RhythmFeedback.Hold;
    int next;
    double previous = double.NegativeInfinity;
    public string Load(string chartPath) {
        Track = new StageTrack();
        string path = Path.ChangeExtension(chartPath, ".stage.json");
        try {
            if (File.Exists(path)) Track = JsonUtility.FromJson<StageTrack>(File.ReadAllText(path));
            if (Track == null || Track.cues == null) throw new FormatException("缺少 cues 列表。");
            foreach (var cue in Track.cues) {
                if (cue == null || Double.IsNaN(cue.time) || Double.IsInfinity(cue.time)) throw new FormatException("演出时间无效。");
                if (cue.kind != "lyric" && cue.kind != "instrument" && cue.kind != "color" && cue.kind != "animation") throw new FormatException("未知演出事件类型。");
                Color color;
                if (cue.kind == "color" && !ColorUtility.TryParseHtmlString(cue.value,out color)) throw new FormatException("颜色需要 #RRGGBB 格式。");
            }
            // Stable order makes simultaneous authored events deterministic.
            Track.cues = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(Track.cues,c => c.time));
        } catch (Exception e) { Track = new StageTrack(); Seek(double.NegativeInfinity); return "演出文件未加载：" + e.Message; }
        Seek(double.NegativeInfinity); return "";
    }
    public void Seek(double time) {
        next = 0; Lyric = Instrument = ""; Accent = RhythmFeedback.Hold;
        while (next < Track.cues.Length && Track.cues[next].time <= time) Apply(Track.cues[next++],false);
        previous = time;
    }
    public void Tick(double time) {
        if (time < previous) { Seek(time); return; }
        while (next < Track.cues.Length && Track.cues[next].time <= time) Apply(Track.cues[next++],true);
        previous = time;
    }
    void Apply(StageCue cue, bool emit) {
        if (cue.kind == "lyric") Lyric = cue.value;
        else if (cue.kind == "instrument") Instrument = cue.value;
        else if (cue.kind == "color") { Color color; if (ColorUtility.TryParseHtmlString(cue.value,out color)) Accent = color; }
        if (emit) OnCue.Invoke(cue.kind,cue.value);
    }
}

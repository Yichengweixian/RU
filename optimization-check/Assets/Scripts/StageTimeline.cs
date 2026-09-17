using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

[Serializable] public sealed class StageCue {
    public double time;
    public string kind = "lyric", value = "";
}
[Serializable] public sealed class StageTrack {
    public StageCue[] cues = new StageCue[0];
    public StageLyric[] lyrics = new StageLyric[0];
    public StageAnimation[] animations = new StageAnimation[0];
    public string spriteBank = "", ending = "";
}
[Serializable] public sealed class StageCueEvent : UnityEvent<string, string> { }
[Serializable] public sealed class StageFrameEvent : UnityEvent<string, int> { }
public sealed class StageAnimationOutput {
    public StageAnimation Clip;
    public int Frame = -1, FrameCount;
    public Sprite Sprite;
    public Sprite[] Frames;
}

// Optional chart.stage.json, kept separate from the user's RPE file.
// Seek reconstructs persistent visual state without replaying one-shot animations.
public sealed class StageTimeline : MonoBehaviour {
    public StageCueEvent OnCue = new StageCueEvent();
    public StageFrameEvent OnAnimationFrame = new StageFrameEvent();
    public StageSpriteBank SpriteBank;
    public bool PresentationVisible = true;
    public StageAnimationOutput[] Animations { get; private set; } = new StageAnimationOutput[0];
    public StagePlayback Playback { get; private set; }
    public StageTrack Track { get; private set; } = new StageTrack();
    public string Lyric { get; private set; } = "";
    public string Instrument { get; private set; } = "";
    public Color Accent { get; private set; } = RhythmFeedback.Hold;
    int next;
    double previous = double.NegativeInfinity;
    public string Load(string chartPath, ChartData chart = null) {
        Track = new StageTrack();
        Playback = null; Animations = new StageAnimationOutput[0];
        string path = Path.ChangeExtension(chartPath, ".stage.json");
        try {
            if (File.Exists(path)) Track = JsonUtility.FromJson<StageTrack>(File.ReadAllText(path));
            if (Track == null) throw new FormatException("演出文件内容为空。");
            if (Track.cues == null) Track.cues = new StageCue[0];
            if (Track.animations != null && Track.animations.Length > 0 && chart == null) chart = ChartLoader.LoadFile(chartPath);
            Playback = new StagePlayback(Track,chart);
            foreach (var cue in Track.cues) {
                if (cue == null || Double.IsNaN(cue.time) || Double.IsInfinity(cue.time)) throw new FormatException("演出时间无效。");
                if (cue.kind != "lyric" && cue.kind != "instrument" && cue.kind != "color" && cue.kind != "animation") throw new FormatException("未知演出事件类型。");
                Color color;
                if (cue.kind == "color" && !ColorUtility.TryParseHtmlString(cue.value,out color)) throw new FormatException("颜色需要 #RRGGBB 格式。");
            }
            // Stable order makes simultaneous authored events deterministic.
            Track.cues = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(Track.cues,c => c.time));
            StageSpriteBank bank = String.IsNullOrEmpty(Track.spriteBank) ? SpriteBank : Resources.Load<StageSpriteBank>(Track.spriteBank);
            if (!String.IsNullOrEmpty(Track.spriteBank) && bank == null) throw new FormatException("找不到动画素材库："+Track.spriteBank);
            if (bank != null) bank.Validate();
            Animations = new StageAnimationOutput[Track.animations.Length];
            for(int i=0;i<Animations.Length;i++) {
                var clip=Track.animations[i]; Sprite[] frames=bank==null?null:bank.Find(clip.sequence);
                if(!String.IsNullOrEmpty(clip.sequence) && (frames==null||frames.Length==0)) throw new FormatException("找不到动画素材："+clip.sequence);
                Animations[i]=new StageAnimationOutput { Clip=clip, Frames=frames, FrameCount=frames==null?clip.placeholderFrames:frames.Length };
            }
        } catch (Exception e) { Track = new StageTrack(); Playback = null; Animations = new StageAnimationOutput[0]; Seek(double.NegativeInfinity); return "演出文件未加载：" + e.Message; }
        Seek(double.NegativeInfinity); return "";
    }
    public void Seek(double time) {
        Seek(time,time);
    }
    public void Seek(double time, double chartTime) {
        next = 0; Lyric = Instrument = ""; Accent = RhythmFeedback.Hold;
        while (next < Track.cues.Length && Track.cues[next].time <= time) Apply(Track.cues[next++],false);
        previous = time;
        Sample(time,chartTime,false);
    }
    public void Tick(double time) {
        Tick(time,time);
    }
    public void Tick(double time, double chartTime) {
        if (time < previous) { Seek(time,chartTime); return; }
        while (next < Track.cues.Length && Track.cues[next].time <= time) Apply(Track.cues[next++],true);
        previous = time;
        Sample(time,chartTime,true);
    }
    void Sample(double musicTime, double chartTime, bool emit) {
        if(Playback==null) return;
        if(Track.lyrics.Length>0) Lyric=Playback.LyricAt(musicTime);
        foreach(var output in Animations) {
            int frame=Playback.FrameAt(output.Clip,chartTime,output.FrameCount);
            bool changed=frame!=output.Frame;
            output.Frame=frame; output.Sprite=frame<0||output.Frames==null?null:output.Frames[frame];
            if(changed&&emit) OnAnimationFrame.Invoke(output.Clip.target,frame);
        }
    }
    void Apply(StageCue cue, bool emit) {
        if (cue.kind == "lyric") Lyric = cue.value;
        else if (cue.kind == "instrument") Instrument = cue.value;
        else if (cue.kind == "color") { Color color; if (ColorUtility.TryParseHtmlString(cue.value,out color)) Accent = color; }
        if (emit) OnCue.Invoke(cue.kind,cue.value);
    }
}

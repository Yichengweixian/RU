using System;
using System.Collections.Generic;
using System.Globalization;

[Serializable] public sealed class StageLyric {
    public double start, end = 2;
    public string text = "";
}
[Serializable] public sealed class StageAnimation {
    public string target = "角色", sequence = "";
    public double startBeat, endBeat = 4;
    public int placeholderFrames = 16;
}

// Pure, random-access sampling shared by the editor and the game.
// Beat means a quarter note. One sixteenth = 0.25 beat, independent of BPM.
public sealed class StagePlayback {
    public readonly StageTrack Track;
    public readonly ChartData Chart;
    readonly StageLyric[] lyrics;
    public StagePlayback(StageTrack track, ChartData chart) {
        Track = track ?? throw new ArgumentNullException("track"); Chart = chart;
        if (track.lyrics == null) track.lyrics = new StageLyric[0];
        if (track.animations == null) track.animations = new StageAnimation[0];
        lyrics = (StageLyric[])track.lyrics.Clone();
        foreach (var lyric in lyrics) {
            if (lyric == null || !Finite(lyric.start) || !Finite(lyric.end) || lyric.end <= lyric.start)
                throw new FormatException("歌词结束时间必须晚于开始时间。");
        }
        Array.Sort(lyrics,(a,b)=>a.start.CompareTo(b.start));
        for (int i=1;i<lyrics.Length;i++) if (lyrics[i].start < lyrics[i-1].end)
            throw new FormatException("歌词区间重叠，请调整起止时间。");
        if (track.animations.Length > 0 && (chart == null || chart.tempo == null))
            throw new FormatException("按拍播放动画需要含 BPMList 的 RPE 谱面。");
        var lanes = new Dictionary<string,List<StageAnimation>>(StringComparer.Ordinal);
        foreach (var clip in track.animations) {
            if (clip == null || String.IsNullOrWhiteSpace(clip.target) || !Finite(clip.startBeat) || !Finite(clip.endBeat)
                || clip.startBeat < 0 || clip.endBeat <= clip.startBeat || !OnGrid(clip.startBeat) || !OnGrid(clip.endBeat)
                || clip.placeholderFrames < 1 || clip.placeholderFrames > 4096)
                throw new FormatException("动画需填写控件名、有效的十六分拍点范围和 1～4096 个占位帧。");
            List<StageAnimation> list;
            if (!lanes.TryGetValue(clip.target,out list)) lanes[clip.target] = list = new List<StageAnimation>();
            list.Add(clip);
        }
        foreach (var list in lanes.Values) {
            list.Sort((a,b)=>a.startBeat.CompareTo(b.startBeat));
            for (int i=1;i<list.Count;i++) if (list[i].startBeat < list[i-1].endBeat)
                throw new FormatException("控件“"+list[i].target+"”的动画区间重叠，请分开安排。");
        }
    }
    static bool Finite(double n) { return !Double.IsNaN(n) && !Double.IsInfinity(n); }
    static bool OnGrid(double beat) { return Math.Abs(beat*4-Math.Round(beat*4)) < 1e-7; }
    public string LyricAt(double musicSeconds) {
        int lo=0,hi=lyrics.Length;
        while(lo<hi) { int mid=(lo+hi)/2; if(lyrics[mid].start<=musicSeconds) lo=mid+1; else hi=mid; }
        if(lo==0) return "";
        var lyric=lyrics[lo-1]; return musicSeconds < lyric.end ? lyric.text ?? "" : "";
    }
    public double BeatAt(double chartSeconds) { return Chart.tempo.BeatAt(chartSeconds-Chart.musicOffset); }
    public double SecondsAt(double beat) { return Chart.tempo.Seconds(beat)+Chart.musicOffset; }
    public int FrameAt(StageAnimation clip, double chartSeconds, int frameCount) {
        if (frameCount < 1 || !Finite(chartSeconds)) return -1;
        return FrameAtBeat(clip,BeatAt(chartSeconds),frameCount);
    }
    public static int FrameAtBeat(StageAnimation clip, double beat, int frameCount) {
        if(frameCount<1 || !Finite(beat)) return -1;
        // Tiny tolerance only corrects floating-point inversion at exact beat boundaries.
        double step=(beat-clip.startBeat)*4;
        if(step < -1e-8 || beat >= clip.endBeat-1e-9) return -1;
        return (int)(Math.Floor(Math.Max(0,step)+1e-8)%frameCount);
    }
}

public static class StageTimeText {
    public static string Seconds(double seconds) {
        string sign=seconds<0?"-":""; seconds=Math.Abs(seconds);
        long millis=(long)Math.Round(seconds*1000,MidpointRounding.AwayFromZero);
        return sign+(millis/60000).ToString("00")+":"+((millis%60000)/1000.0).ToString("00.000",CultureInfo.InvariantCulture);
    }
    public static double ParseSeconds(string value) {
        value=value.Trim(); double sign=value.StartsWith("-")?-1:1;
        if(sign<0) value=value.Substring(1);
        string[] parts=value.Split(':'); double seconds;
        if(parts.Length==1) seconds=Double.Parse(parts[0],CultureInfo.InvariantCulture);
        else if(parts.Length==2) {
            int minutes=Int32.Parse(parts[0],CultureInfo.InvariantCulture);
            double tail=Double.Parse(parts[1],CultureInfo.InvariantCulture);
            if(minutes<0 || tail<0 || tail>=60) throw new FormatException("时间示例：01:12.500。");
            seconds=minutes*60+tail;
        } else throw new FormatException("时间示例：01:12.500。");
        if(Double.IsNaN(seconds)||Double.IsInfinity(seconds)||seconds<0||seconds>86400) throw new FormatException("请输入有效时间。");
        return sign*seconds;
    }
    // 1-based bar and beat; final digit is 0..3 sixteenth subdivision within the beat.
    public static string Musical(double beat) {
        long tick=(long)Math.Round(beat*4);
        return (tick/16+1)+":"+(tick%16/4+1)+":"+(tick%4);
    }
    public static double ParseMusical(string value) {
        string[] p=value.Trim().Split(':');
        if(p.Length!=3) throw new FormatException("拍点示例：2:3:0（第2小节，第3拍）。");
        int bar=Int32.Parse(p[0]),beat=Int32.Parse(p[1]),sixteenth=Int32.Parse(p[2]);
        if(bar<1||bar>100000||beat<1||beat>4||sixteenth<0||sixteenth>3) throw new FormatException("小节从1开始，拍为1～4，细分为0～3。");
        return (bar-1)*4.0+(beat-1)+sixteenth*.25;
    }
}

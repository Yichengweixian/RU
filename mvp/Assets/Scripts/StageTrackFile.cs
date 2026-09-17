using System;
using System.IO;
using UnityEngine;

public static class StageTrackFile {
    public static StageTrack Read(string path) {
        var track=JsonUtility.FromJson<StageTrack>(File.ReadAllText(path));
        if(track==null) throw new FormatException("演出配置为空。");
        if(track.cues==null) track.cues=new StageCue[0];
        if(track.lyrics==null) track.lyrics=new StageLyric[0];
        if(track.animations==null) track.animations=new StageAnimation[0];
        return track;
    }
    public static void Validate(StageTrack track, ChartData chart) {
        new StagePlayback(track,chart);
        if(track.cues==null) track.cues=new StageCue[0];
        foreach(var cue in track.cues) {
            if(cue==null||Double.IsNaN(cue.time)||Double.IsInfinity(cue.time)) throw new FormatException("演出事件时间无效。");
            if(cue.kind!="lyric"&&cue.kind!="instrument"&&cue.kind!="color"&&cue.kind!="animation") throw new FormatException("未知演出事件类型。");
            Color c; if(cue.kind=="color"&&!ColorUtility.TryParseHtmlString(cue.value,out c)) throw new FormatException("颜色需要 #RRGGBB 格式。");
        }
    }
    public static void Save(string path, StageTrack track, ChartData chart) {
        Validate(track,chart);
        string destination=Path.GetFullPath(path), temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            File.WriteAllText(temporary,JsonUtility.ToJson(track,true),new System.Text.UTF8Encoding(false));
            if(File.Exists(destination)) File.Replace(temporary,destination,null); else File.Move(temporary,destination);
        } finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}

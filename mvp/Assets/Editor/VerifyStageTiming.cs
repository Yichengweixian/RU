using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class VerifyStageTiming {
    static int passed;
    static void Check(bool value,string name) { if(!value) throw new Exception("Stage timing: "+name); passed++; }
    static void Near(double a,double b,string name) { Check(Math.Abs(a-b)<1e-7,name); }
    static void Reject(Action action,string name) { bool rejected=false; try { action(); } catch(FormatException) { rejected=true; } Check(rejected,name); }
    static string Root { get { return Path.GetFullPath(Path.Combine(Application.dataPath,"../..")); } }
    [MenuItem("音游/验证歌词与拍点动画并构建")]
    public static void RunAndBuild() {
        string output=Path.Combine(Root,"validation/stage-timing-result.txt");
        try {
            VerifyMvp.Run(); VerifyOptimization.Run(); string feedback=FeedbackChecks.Run();
            Type core=Type.GetType("CoreTests, Assembly-CSharp-Editor");
            string original=core==null?"Core fixture not loaded.":(string)core.GetMethod("Run").Invoke(null,new object[]{Path.Combine(Root,"validation/no-chart.osu")});
            Run(); string editor=VerifyStageEditor.Run(); string sheets=VerifyStageSheet.Run(); BuildMvp.Build();
            File.WriteAllText(output,"PASS "+passed+" stage timing checks\n"+editor+"\n"+sheets+"\n"+feedback+"\n"+original+"\n"+File.ReadAllText(Path.Combine(Root,"validation/build-result.txt")));
        } catch(Exception e) { File.WriteAllText(output,"FAIL "+e); throw; }
    }
    public static void Run() {
        passed=0;
        JObject source=JObject.Parse(File.ReadAllText(Path.Combine(Root,"validation/fixtures/rpe/source.json")));
        source["BPMList"][1]["bpm"]=150;
        var chart=RpeChartLoader.Parse(source.ToString());
        Check(chart.tempo!=null,"RPE retains tempo map"); Near(chart.musicOffset,.25,"RPE retains chart offset");
        foreach(double beat in new[]{-2.0,0,.25,7.75,8,8.25,16,100}) Near(chart.tempo.BeatAt(chart.tempo.Seconds(beat)),beat,"tempo inverse "+beat);
        Near(chart.tempo.Seconds(8.25)-chart.tempo.Seconds(8),.1,"sixteenth duration after BPM change");
        Near(chart.tempo.Seconds(7.75)-chart.tempo.Seconds(7.5),.125,"sixteenth duration before BPM change");
        var track=new StageTrack {
            lyrics=new[]{new StageLyric{start=.5,end=1,text="第一句"},new StageLyric{start=1,end=2,text="第二句"},new StageLyric{start=3,end=4.5,text="跨越变 BPM 的歌词"}},
            animations=new[]{new StageAnimation{target="角色",startBeat=0,endBeat=16,placeholderFrames=8},new StageAnimation{target="灯",startBeat=4,endBeat=20,placeholderFrames=16}}
        };
        var playback=new StagePlayback(track,chart); var clip=track.animations[0];
        Check(playback.LyricAt(.499999)=="","before lyric start"); Check(playback.LyricAt(.5)=="第一句","inclusive start");
        Check(playback.LyricAt(1)=="第二句","adjacent ranges switch on boundary"); Check(playback.LyricAt(2)=="","exclusive end");
        Check(playback.LyricAt(2.5)=="","lyric gap"); Check(playback.LyricAt(3.5)==track.lyrics[2].text,"forward seek lyric"); Check(playback.LyricAt(.75)=="第一句","backward seek lyric");
        Check(playback.FrameAt(clip,.249,8)==-1,"chart offset hides animation before beat zero");
        for(int tick=0;tick<64;tick++) Check(playback.FrameAt(clip,playback.SecondsAt(tick*.25),8)==tick%8,"each sixteenth and loop "+tick);
        Check(playback.FrameAt(clip,playback.SecondsAt(16),8)==-1,"animation interval end hides frame");
        Check(playback.FrameAt(clip,playback.SecondsAt(9.5),8)==6,"direct seek after tempo change");
        Check(playback.FrameAt(clip,playback.SecondsAt(.25)-.00001,8)==0,"before frame boundary");
        var frozen=new SongClock(); frozen.Start(100,playback.SecondsAt(9.5),1); frozen.Pause(100);
        Check(playback.FrameAt(clip,frozen.Now(200),8)==6,"paused animation frame freezes");
        frozen.Resume(200); Check(playback.FrameAt(clip,frozen.Now(200.1),8)==7,"resume follows next sixteenth");
        frozen.Start(500,playback.SecondsAt(8),.5); Check(playback.FrameAt(clip,frozen.Now(500.2),8)==1,"half-speed clock maps to next sixteenth");
        foreach(int fps in new[]{30,60,120,144}) {
            int expected=playback.FrameAt(clip,5.137,8);
            for(int i=0;i<fps*5;i++) playback.FrameAt(clip,i/(double)fps,8);
            Check(playback.FrameAt(clip,5.137,8)==expected,"sampling independent of frame history "+fps);
        }
        Near(StageTimeText.ParseSeconds("01:12.500"),72.5,"minute second parsing"); Check(StageTimeText.Seconds(59.9999)=="01:00.000","format carries rounded minute");
        Near(StageTimeText.ParseMusical("2:3:1"),6.25,"bar beat subdivision parsing"); Check(StageTimeText.Musical(6.25)=="2:3:1","musical roundtrip");
        Reject(()=>StageTimeText.ParseMusical("1:5:0"),"invalid beat"); Reject(()=>StageTimeText.ParseSeconds("01:60"),"invalid seconds"); Reject(()=>StageTimeText.ParseSeconds("NaN"),"nonfinite time");
        Reject(()=>new StagePlayback(new StageTrack{lyrics=new[]{new StageLyric{start=1,end=1}}},chart),"zero length lyric");
        Reject(()=>new StagePlayback(new StageTrack{lyrics=new[]{new StageLyric{start=0,end=2},new StageLyric{start=1,end=3}}},chart),"overlapping lyrics rejected");
        Reject(()=>new StagePlayback(new StageTrack{animations=new[]{new StageAnimation{startBeat=.1}}},chart),"off-grid animation rejected");
        Reject(()=>new StagePlayback(new StageTrack{animations=new[]{new StageAnimation(),new StageAnimation()}},chart),"overlapping same target rejected");
        string dir=Path.Combine(Root,"validation/fixtures/stage-timing"); Directory.CreateDirectory(dir);
        string song=Path.Combine(dir,"chart.json"), side=Path.ChangeExtension(song,".stage.json"); File.WriteAllText(song,source.ToString());
        File.Copy(Path.Combine(Root,"validation/fixtures/rpe/tone.wav"),Path.Combine(dir,"tone.wav"),true);
        StageTrackFile.Save(side,track,chart); var reread=StageTrackFile.Read(side);
        Check(reread.lyrics.Length==3&&reread.animations.Length==2,"roundtrip lyrics and animation settings");
        string before=File.ReadAllText(side); var invalid=new StageTrack{lyrics=new[]{new StageLyric{start=1,end=0}}};
        Reject(()=>StageTrackFile.Save(side,invalid,chart),"invalid save rejected"); Check(File.ReadAllText(side)==before,"invalid save preserves previous file");
        JObject other=(JObject)source.DeepClone(); other["BPMList"][0]["bpm"]=160; other["BPMList"][1]["bpm"]=110; other["META"]["offset"]=-300;
        var otherChart=RpeChartLoader.Parse(other.ToString()); var otherPlayback=new StagePlayback(reread,otherChart);
        Check(otherPlayback.FrameAt(reread.animations[0],otherPlayback.SecondsAt(6.25),8)==1,"template on second song with different tempo and offset");
        Near(otherPlayback.SecondsAt(0),-.3,"negative chart offset");
        var go=new GameObject("Stage timing test");
        try {
            var timeline=go.AddComponent<StageTimeline>(); Check(timeline.Load(song)=="","runtime stage load");
            timeline.Tick(3.5); Check(timeline.Lyric==track.lyrics[2].text,"runtime lyric range");
            timeline.Tick(4.5); Check(timeline.Lyric=="","runtime lyric auto-clears");
            timeline.Seek(1); Check(timeline.Lyric=="第二句","runtime rewind reconstructs lyric");
            int events=0; timeline.OnAnimationFrame.AddListener((id,frame)=>events++);
            timeline.Seek(4.35); int frameAtSeek=timeline.Animations[0].Frame; timeline.Tick(4.35);
            Check(events==0 && timeline.Animations[0].Frame==frameAtSeek,"seek does not replay animation transitions");
            timeline.Tick(4.45); Check(events>0,"playing emits frame transition");
            timeline.Tick(1,playback.SecondsAt(6.25)); Check(timeline.Lyric=="第二句"&&timeline.Animations[0].Frame==1,"lyrics use music clock while animation follows calibrated chart clock");
            VerifySpriteBinding(timeline,song,track,chart);
        } finally { UnityEngine.Object.DestroyImmediate(go); }
        Check(StageEditorAudio.Available,"installed editor audio preview adapter available");
        Debug.Log("PASS "+passed+" stage timing checks");
    }
    static void VerifySpriteBinding(StageTimeline timeline,string song,StageTrack track,ChartData chart) {
        // Procedural four-cell sheet is an isolated fixture, not user art.
        var texture=new Texture2D(32,8); var sprites=new Sprite[4]; var bank=ScriptableObject.CreateInstance<StageSpriteBank>();
        string side=Path.ChangeExtension(song,".stage.json");
        try {
            for(int i=0;i<sprites.Length;i++) sprites[i]=Sprite.Create(texture,new Rect(i*8,0,8,8),new Vector2(.5f,.5f),8);
            bank.sequences=new[]{new StageSpriteSequence{id="test",frames=sprites}}; timeline.SpriteBank=bank;
            var bound=StageTrackFile.Read(side); bound.animations[0].sequence="test"; StageTrackFile.Save(side,bound,chart);
            Check(timeline.Load(song)=="","format-independent sprite bank load"); timeline.Seek(chart.tempo.Seconds(.75)+chart.musicOffset);
            Check(timeline.Animations[0].Frame==3&&timeline.Animations[0].Sprite==sprites[3],"actual sprite selected from sheet at beat");
            timeline.Seek(chart.tempo.Seconds(1)+chart.musicOffset); Check(timeline.Animations[0].Sprite==sprites[0],"actual sprite loops by frame count");
            timeline.Seek(chart.tempo.Seconds(16)+chart.musicOffset); Check(timeline.Animations[0].Sprite==null,"actual sprite clears outside clip");
        } finally {
            timeline.SpriteBank=null; StageTrackFile.Save(side,track,chart);
            foreach(var sprite in sprites) if(sprite!=null) UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(bank); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

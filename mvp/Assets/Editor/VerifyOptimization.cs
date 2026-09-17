using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class VerifyOptimization {
    static int passed;
    static void Check(bool value, string name) { if (!value) throw new Exception("Optimization: " + name); passed++; }
    static NoteData N(double t, int lane = 0, double end = -1) { return new NoteData { time=t, endTime=end<0?t:end, lane=lane }; }
    static void Near(double a, double b, string name) { Check(Math.Abs(a-b)<.00001,name); }
    [MenuItem("音游/验证优化并构建试玩版")]
    public static void RunAndBuild() {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        string output = Path.Combine(root,"validation/optimization-result.txt");
        try {
            Run();
            VerifyMvp.Run();
            string feedback = FeedbackChecks.Run();
            Type core = Type.GetType("CoreTests, Assembly-CSharp-Editor");
            string original = core == null ? "Original core fixture not included in this editor." : (string)core.GetMethod("Run").Invoke(null,new object[] { Path.Combine(root,"validation/no-chart.osu") });
            BuildMvp.Build();
            File.WriteAllText(output,"PASS " + passed + " optimization checks\n" + feedback + "\n" + original + "\n" + File.ReadAllText(Path.Combine(root,"validation/build-result.txt")));
        } catch (Exception e) { File.WriteAllText(output,"FAIL " + e); throw; }
    }
    public static void Run() {
        passed=0;
        var dense = new RhythmSession(new[] { N(1),N(1.1) });
        dense.Press(0,1.06);
        Check(dense.States[0] == NoteState.Done && dense.States[1] == NoteState.Waiting,"earliest candidate wins dense overlap");
        dense.Press(0,1.1); Check(dense.Perfect == 2,"dense second note remains hittable");
        foreach(double time in new[] { .94,1,1.06 }) { var s=new RhythmSession(new[]{N(1)}); s.Press(0,time); Check(s.Perfect==1,"perfect boundary"); }
        foreach(double time in new[] { .86,.939,1.061,1.14 }) { var s=new RhythmSession(new[]{N(1)}); s.Press(0,time); Check(s.Good==1,"good boundary"); }
        var hold=new RhythmSession(new[]{N(1,0,2),N(2)}); hold.Press(0,1); hold.Press(0,2);
        Check(hold.Perfect==2 && hold.HoldingNote(0)<0,"touching hold tail and next tap");
        hold.FinishAll(); Check(hold.JudgedCount==2,"finish idempotence");
        var slow=new RhythmSession(new[]{N(1)},.5); slow.Press(0,1.03); Check(slow.Perfect==1,"slow rate real-time perfect window"); Near(slow.LastErrorMs,60,"slow rate statistics in real milliseconds");
        var slowMiss=new RhythmSession(new[]{N(1)},.5); slowMiss.Advance(1.071); Check(slowMiss.Miss==1,"slow rate miss window");
        var stats=new RhythmSession(new[]{N(1),N(2),N(3)}); stats.Press(0,.97); stats.Press(0,2.05); stats.Press(0,3);
        Check(stats.Early==1 && stats.Late==1 && stats.ErrorsMs.Count==3,"signed timing statistics"); Near(stats.MeanErrorMs,20.0/3,"mean timing error");
        var clock=new SongClock(); clock.Start(100,20,.75); Near(clock.Now(104),23,"clock rate");
        Near(clock.EventTime(203.92,204,104),22.94,"input timestamp maps to song clock");
        clock.Pause(104); Near(clock.Now(500),23,"pause freezes time"); clock.Resume(500); Near(clock.Now(502),24.5,"resume excludes paused duration");
        clock.Start(700,10,1); Near(clock.Now(698),8,"practice pre-roll");
        var notes=new[]{N(0,0,20),N(2,1),N(10,2),N(100,3)}; var visible=new VisibleNotes(notes); var states=new NoteState[4];
        visible.Update(9,2,states); Check(visible.Active.Count==2 && visible.Active.Contains(0) && visible.Active.Contains(2),"long hold remains visible while future notes culled");
        states[0]=NoteState.Done; visible.Update(10,2,states); Check(!visible.Active.Contains(0),"completed hold removed");
        visible.Update(1,2,states); Check(visible.Active.Contains(1),"backward seek resets visible index");
        var many=Enumerable.Range(0,100000).Select(i=>N(i*.1,i%4)).ToArray(); var large=new RhythmSession(many);
        for(int i=0;i<1000;i++) large.Advance(i/120.0);
        Check(large.CandidateVisits < 5000,"100k note chart bounded candidate visits for first 1000 frames");
        large.Advance(20000); Check(large.Miss==100000,"large jump catches every miss exactly once");
        // InputActions intentionally do not run in Edit mode. The player executes
        // RuntimeInputChecks before the full-song verification instead.
        if (Application.isPlaying) {
        var go=new GameObject("Isolated input verification"); var keyboard=InputSystem.AddDevice<Keyboard>();
        try {
            var input=go.AddComponent<RhythmInput>(); input.Initialize(); int edges=0; double first=0,last=0;
            double now=Time.realtimeSinceStartupAsDouble;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A),now-.02);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(),now-.01);
            InputSystem.Update();
            input.Drain(e=>{ if(edges==0) first=e.Time; last=e.Time; edges++; });
            Check(edges==2 && !input.Held[0],"press and release in one input update preserved; edges="+edges); Near(last-first,.01,"input timestamps preserved");
            input.Clear(); input.Drain(e=>edges++); Check(edges==2,"discard stale input on pause or seek");
        } finally { UnityEngine.Object.DestroyImmediate(go); InputSystem.RemoveDevice(keyboard); }
        }
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/fixtures/stage")); Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"chart.json");
        File.WriteAllText(Path.ChangeExtension(path,".stage.json"),"{\"cues\":[{\"time\":1,\"kind\":\"lyric\",\"value\":\"test\"},{\"time\":2,\"kind\":\"color\",\"value\":\"#336699\"},{\"time\":2,\"kind\":\"animation\",\"value\":\"cue\"}],\"ending\":\"test ending\"}");
        var stageObject=new GameObject("Isolated stage verification");
        try {
            var stage=stageObject.AddComponent<StageTimeline>(); Check(stage.Load(path)=="","stage track loads");
            int events=0; stage.OnCue.AddListener((kind,value)=>events++); stage.Tick(2.5); Check(events==3 && stage.Lyric=="test","stage applies all crossed cues");
            stage.Tick(3); Check(events==3,"stage does not repeat one-shot events");
            stage.Seek(2.5); Check(events==3 && stage.Lyric=="test","seek reconstructs without one-shot events");
            stage.Seek(0); Check(stage.Lyric=="","rewind clears state"); stage.Tick(1); Check(events==4,"loop events fire again after rewind");
        } finally { UnityEngine.Object.DestroyImmediate(stageObject); }
        Debug.Log("PASS " + passed + " optimization checks");
    }
}

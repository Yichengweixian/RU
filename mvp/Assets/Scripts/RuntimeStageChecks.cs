#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

// Opt-in development verification against independently authored time windows.
public sealed class RuntimeStageChecks {
    [Serializable] public sealed class Sample { public double start,end; public int frame; public string lyric; }
    [Serializable] public sealed class Cases { public Sample[] samples; }
    readonly Cases cases;
    readonly int[] observed;
    int mismatches,pausedSamples;
    int pausedFrame=-2;
    string pausedLyric;
    bool wasPaused;
    public RuntimeStageChecks(string chartPath) {
        cases=JsonUtility.FromJson<Cases>(File.ReadAllText(Path.ChangeExtension(chartPath,".expected.json")));
        if(cases==null||cases.samples==null||cases.samples.Length==0) throw new FormatException("Missing stage verification samples.");
        observed=new int[cases.samples.Length];
    }
    public void Observe(StageTimeline stage,double seconds,bool paused) {
        if(stage.Animations.Length!=1) { mismatches++; return; }
        var output=stage.Animations[0];
        if(paused) {
            if(wasPaused) { pausedSamples++; if(output.Frame!=pausedFrame||stage.Lyric!=pausedLyric) mismatches++; }
            pausedFrame=output.Frame; pausedLyric=stage.Lyric; wasPaused=true; return;
        }
        wasPaused=false;
        for(int i=0;i<cases.samples.Length;i++) {
            var sample=cases.samples[i]; if(seconds<sample.start||seconds>=sample.end) continue;
            observed[i]++;
            if(output.Frame!=sample.frame||output.Sprite==null||output.Sprite.name!="frame_"+sample.frame.ToString("D4")||stage.Lyric!=sample.lyric) mismatches++;
        }
    }
    public bool Finish(string directory) {
        bool ok=mismatches==0&&pausedSamples>0; int windows=0;
        foreach(int count in observed) { if(count==0) ok=false; else windows++; }
        File.WriteAllText(Path.Combine(directory,"stage-report.json"),"{\"ok\":"+(ok?"true":"false")+",\"observedWindows\":"+windows+",\"requiredWindows\":"+observed.Length+",\"mismatches\":"+mismatches+",\"pausedSamples\":"+pausedSamples+"}");
        return ok;
    }
}
#endif

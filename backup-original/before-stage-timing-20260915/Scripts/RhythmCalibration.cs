using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RhythmCalibration : MonoBehaviour {
    AudioSource source;
    AudioClip clip;
    double origin;
    int lastBeat = -1;
    readonly List<double> samples = new List<double>();
    public bool Running { get; private set; }
    public int Count { get { return samples.Count; } }
    public bool Ready { get { return samples.Count >= 12; } }
    public int SuggestedMs {
        get { if (samples.Count == 0) return 0; var sorted = samples.ToArray(); Array.Sort(sorted); return (int)Math.Round((sorted[(sorted.Length-1)/2]+sorted[sorted.Length/2])*.5); }
    }
    public double SpreadMs {
        get { if (samples.Count < 2) return 0; var sorted = samples.ToArray(); Array.Sort(sorted); return sorted[(int)((sorted.Length-1)*.75)]-sorted[(int)((sorted.Length-1)*.25)]; }
    }
    public void Begin() {
        if (source == null) {
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false;
            const int rate = 44100; var data = new float[rate*14];
            for (int beat = 0; beat < 28; beat++) for (int i = 0; i < 1800; i++)
                data[beat*rate/2+i] = .45f*Mathf.Sin(2*Mathf.PI*1200*i/rate)*Mathf.Exp(-i/260f);
            clip = AudioClip.Create("Calibration metronome",data.Length,1,rate,false); clip.SetData(data,0); source.clip = clip;
        }
        source.Stop(); samples.Clear(); lastBeat = -1;
        origin = AudioSettings.dspTime+1; source.PlayScheduled(origin); Running = true;
    }
    public void Tap(double eventRealtime) {
        if (!Running) return;
        double at = AudioSettings.dspTime-(Time.realtimeSinceStartupAsDouble-eventRealtime)-origin;
        int beat = (int)Math.Round(at/.5);
        // First four clicks are for settling into the rhythm; one sample per click.
        if (beat < 4 || beat >= 28 || beat <= lastBeat || Math.Abs(at-beat*.5) > .2) return;
        lastBeat = beat; samples.Add((at-beat*.5)*1000);
    }
    public bool Ended { get { return Running && AudioSettings.dspTime >= origin+14; } }
    public void Stop() { Running = false; if (source != null) source.Stop(); }
    void OnDestroy() { if (clip != null) Destroy(clip); }
}

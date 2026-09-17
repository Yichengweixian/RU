using System;

public sealed class SongClock {
    double originDsp, originSong, frozen;
    public double Rate { get; private set; } = 1;
    public bool Paused { get; private set; }
    public void Start(double dsp, double song, double rate) { originDsp = dsp; originSong = song; Rate = rate; Paused = false; }
    public double Now(double dsp) { return Paused ? frozen : originSong + (dsp - originDsp) * Rate; }
    public void Pause(double dsp) { frozen = Now(dsp); Paused = true; }
    public void Resume(double dsp) { originDsp = dsp; originSong = frozen; Paused = false; }
    public double EventTime(double eventRealtime, double nowRealtime, double nowDsp) {
        return Now(nowDsp) - Math.Max(0, nowRealtime - eventRealtime) * Rate;
    }
}

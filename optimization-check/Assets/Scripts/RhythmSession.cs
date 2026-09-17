using System;
using System.Collections.Generic;

public enum NoteState { Waiting, Holding, Done }
public enum Judgement { Perfect, Good, Miss }

public sealed class RhythmSession {
    public const double PerfectWindow = 0.060;
    public const double GoodWindow = 0.140;
    const double Epsilon = 0.0000001;
    public readonly NoteData[] Notes;
    public readonly NoteState[] States;
    readonly Judgement[] heads;
    readonly int[][] lanes = new int[4][];
    readonly int[] next = new int[4], holding = { -1, -1, -1, -1 };
    readonly List<double> errors = new List<double>();
    public readonly double PlaybackRate;
    public int Early { get; private set; }
    public int Late { get; private set; }
    public double LastErrorMs { get; private set; }
    public double MeanErrorMs { get; private set; }
    public IReadOnlyList<double> ErrorsMs { get { return errors; } }
    public long CandidateVisits { get; private set; }
    public int Perfect { get; private set; }
    public int Good { get; private set; }
    public int Miss { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }
    public int JudgedCount { get { return Perfect + Good + Miss; } }
    public double Completion { get { return Notes.Length == 0 ? 0 : (Perfect + Good * 0.6) / Notes.Length * 100.0; } }
    public bool Passed { get { return Completion + Epsilon >= 60; } }
    public event Action<int, Judgement> Judged;
    // A successful head is feedback, not a completed Hold or an extra score.
    public event Action<int, Judgement> HitStarted;

    public RhythmSession(NoteData[] notes, double playbackRate = 1) {
        if (notes == null || playbackRate < .5 || playbackRate > 2 || Double.IsNaN(playbackRate)) throw new ArgumentException("Invalid session.");
        PlaybackRate = playbackRate;
        Notes = notes;
        States = new NoteState[notes.Length];
        heads = new Judgement[notes.Length];
        var lists = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        for (int i = 0; i < notes.Length; i++) {
            if (notes[i].lane < 0 || notes[i].lane >= 4) throw new ArgumentException("Invalid lane.");
            lists[notes[i].lane].Add(i);
        }
        for (int lane = 0; lane < 4; lane++) {
            lists[lane].Sort((a,b) => { int c = notes[a].time.CompareTo(notes[b].time); return c != 0 ? c : a.CompareTo(b); });
            lanes[lane] = lists[lane].ToArray();
        }
    }
    public int HoldingNote(int lane) { return holding[lane]; }
    public void Press(int lane, double time) {
        Advance(time);
        if (holding[lane] >= 0 || next[lane] >= lanes[lane].Length) return;
        int best = lanes[lane][next[lane]];
        CandidateVisits++;
        double diff = (time - Notes[best].time) / PlaybackRate;
        double distance = Math.Abs(diff);
        if (distance > GoodWindow + Epsilon) return;
        next[lane]++;
        heads[best] = distance <= PerfectWindow + Epsilon ? Judgement.Perfect : Judgement.Good;
        LastErrorMs = diff * 1000;
        errors.Add(LastErrorMs);
        MeanErrorMs += (LastErrorMs - MeanErrorMs) / errors.Count;
        if (LastErrorMs < -1) Early++; else if (LastErrorMs > 1) Late++;
        if (Notes[best].IsHold) { States[best] = NoteState.Holding; holding[lane] = best; }
        if (HitStarted != null) HitStarted(best, heads[best]);
        if (!Notes[best].IsHold) Finish(best, heads[best]);
    }
    public void Release(int lane, double time) {
        int i = holding[lane];
        if (i < 0) return;
        double remaining = (Notes[i].endTime - time) / PlaybackRate;
        if (remaining > GoodWindow + Epsilon) Finish(i, Judgement.Miss);
        else Finish(i, remaining > PerfectWindow + Epsilon ? Judgement.Good : heads[i]);
    }
    public void Advance(double time) {
        for (int lane = 0; lane < 4; lane++) {
            int active = holding[lane];
            if (active >= 0 && time >= Notes[active].endTime) Finish(active, heads[active]);
            while (next[lane] < lanes[lane].Length) {
                int i = lanes[lane][next[lane]];
                CandidateVisits++;
                if ((time - Notes[i].time) / PlaybackRate <= GoodWindow + Epsilon) break;
                next[lane]++;
                Finish(i, Judgement.Miss);
            }
        }
    }
    public void FinishAll() {
        for (int i = 0; i < Notes.Length; i++) if (States[i] != NoteState.Done) Finish(i, Judgement.Miss);
        for (int lane = 0; lane < 4; lane++) next[lane] = lanes[lane].Length;
    }
    void Finish(int i, Judgement judgement) {
        if (States[i] == NoteState.Done) return;
        States[i] = NoteState.Done;
        if (holding[Notes[i].lane] == i) holding[Notes[i].lane] = -1;
        if (judgement == Judgement.Miss) { Miss++; Combo = 0; }
        else { if (judgement == Judgement.Perfect) Perfect++; else Good++; Combo++; MaxCombo = Math.Max(Combo, MaxCombo); }
        if (Judged != null) Judged(i, judgement);
    }
}

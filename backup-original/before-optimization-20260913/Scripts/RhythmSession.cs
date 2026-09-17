using System;

public enum NoteState { Waiting, Holding, Done }
public enum Judgement { Perfect, Good, Miss }

public sealed class RhythmSession {
    public const double PerfectWindow = 0.060;
    public const double GoodWindow = 0.140;
    const double Epsilon = 0.0000001;
    public readonly NoteData[] Notes;
    public readonly NoteState[] States;
    readonly Judgement[] heads;
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

    public RhythmSession(NoteData[] notes) {
        Notes = notes;
        States = new NoteState[notes.Length];
        heads = new Judgement[notes.Length];
    }
    public void Press(int lane, double time) {
        int best = -1;
        double distance = GoodWindow + Epsilon;
        for (int i = 0; i < Notes.Length; i++) {
            if (Notes[i].lane != lane || States[i] != NoteState.Waiting) continue;
            double diff = Math.Abs(Notes[i].time - time);
            if (diff <= distance) { best = i; distance = diff; }
        }
        if (best < 0) return;
        heads[best] = distance <= PerfectWindow + Epsilon ? Judgement.Perfect : Judgement.Good;
        if (Notes[best].IsHold) States[best] = NoteState.Holding;
        if (HitStarted != null) HitStarted(best, heads[best]);
        if (!Notes[best].IsHold) Finish(best, heads[best]);
    }
    public void Release(int lane, double time) {
        for (int i = 0; i < Notes.Length; i++) {
            if (Notes[i].lane != lane || States[i] != NoteState.Holding) continue;
            double remaining = Notes[i].endTime - time;
            if (remaining > GoodWindow + Epsilon) Finish(i, Judgement.Miss);
            else Finish(i, remaining > PerfectWindow + Epsilon ? Judgement.Good : heads[i]);
        }
    }
    public void Advance(double time) {
        for (int i = 0; i < Notes.Length; i++) {
            if (States[i] == NoteState.Waiting && time - Notes[i].time > GoodWindow + Epsilon) Finish(i, Judgement.Miss);
            else if (States[i] == NoteState.Holding && time >= Notes[i].endTime) Finish(i, heads[i]);
        }
    }
    public void FinishAll() {
        for (int i = 0; i < Notes.Length; i++) if (States[i] != NoteState.Done) Finish(i, Judgement.Miss);
    }
    void Finish(int i, Judgement judgement) {
        if (States[i] == NoteState.Done) return;
        States[i] = NoteState.Done;
        if (judgement == Judgement.Miss) { Miss++; Combo = 0; }
        else { if (judgement == Judgement.Perfect) Perfect++; else Good++; Combo++; MaxCombo = Math.Max(Combo, MaxCombo); }
        if (Judged != null) Judged(i, judgement);
    }
}

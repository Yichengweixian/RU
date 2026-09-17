using System;

public enum FeedbackStage { None, Hit, HoldStarted, HoldComplete, HoldBroken, Miss }

// The renderer consumes this state; frame rate and animation never affect judgement.
public sealed class RhythmFeedbackState {
    public sealed class Lane {
        public bool KeyDown;
        public double PressAge = 10, ResultAge = 10;
        public int HoldNote = -1;
        public double HoldProgress;
        public FeedbackStage Stage;
        public Judgement Grade;
    }
    public readonly Lane[] Lanes = { new Lane(), new Lane(), new Lane(), new Lane() };
    readonly RhythmSession session;
    public int AcceptedHeads { get; private set; }
    public int HoldStarts { get; private set; }
    public int HoldCompletions { get; private set; }
    public int HoldBreaks { get; private set; }
    public RhythmFeedbackState(RhythmSession session) {
        this.session = session;
        session.HitStarted += Head;
        session.Judged += Result;
    }
    public void SetKey(int lane, bool down, bool pressed) {
        Lanes[lane].KeyDown = down;
        if (pressed) Lanes[lane].PressAge = 0;
    }
    public void Tick(double delta, double chartTime) {
        foreach (var lane in Lanes) {
            lane.PressAge += Math.Max(0, delta);
            lane.ResultAge += Math.Max(0, delta);
            if (lane.HoldNote >= 0) {
                var n = session.Notes[lane.HoldNote];
                lane.HoldProgress = Math.Max(0, Math.Min(1, (chartTime-n.time)/(n.endTime-n.time)));
            }
        }
    }
    void Head(int index, Judgement grade) {
        var n = session.Notes[index]; var lane = Lanes[n.lane];
        AcceptedHeads++;
        lane.Grade = grade; lane.ResultAge = 0; lane.PressAge = 0;
        lane.Stage = n.IsHold ? FeedbackStage.HoldStarted : FeedbackStage.Hit;
        if (n.IsHold) { lane.HoldNote = index; lane.HoldProgress = 0; HoldStarts++; }
    }
    void Result(int index, Judgement grade) {
        var n = session.Notes[index]; var lane = Lanes[n.lane];
        bool wasHolding = lane.HoldNote == index;
        if (wasHolding) lane.HoldNote = -1;
        lane.Grade = grade; lane.ResultAge = 0;
        if (n.IsHold && grade != Judgement.Miss) {
            lane.HoldProgress = 1; lane.Stage = FeedbackStage.HoldComplete; HoldCompletions++;
        } else if (wasHolding && grade == Judgement.Miss) {
            lane.Stage = FeedbackStage.HoldBroken; HoldBreaks++;
        } else lane.Stage = grade == Judgement.Miss ? FeedbackStage.Miss : FeedbackStage.Hit;
    }
}

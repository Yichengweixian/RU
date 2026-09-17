using System;

public static class FeedbackChecks {
    static int passed;
    static void Check(bool value, string message) {
        if (!value) throw new Exception("Feedback: " + message);
        passed++;
    }
    static NoteData N(int lane, double start, double end) {
        return new NoteData { lane = lane, time = start, endTime = end };
    }
    public static string Run() {
        passed = 0;
        var s = new RhythmSession(new[] { N(0,1,3), N(1,1,1), N(2,1,3), N(3,1,1) });
        var f = new RhythmFeedbackState(s);
        f.SetKey(0,true,true); s.Press(0,0);
        Check(f.Lanes[0].KeyDown && f.Lanes[0].PressAge == 0 && f.AcceptedHeads == 0 && s.JudgedCount == 0, "empty press responds without a hit");
        f.SetKey(0,false,false);
        Check(!f.Lanes[0].KeyDown, "key release clears pressed state");
        int heads = 0, results = 0;
        s.HitStarted += (i,g) => heads++;
        s.Judged += (i,g) => results++;
        s.Press(0,1); s.Press(1,1); s.Press(2,1); s.Press(3,1);
        Check(heads == 4 && f.AcceptedHeads == 4 && results == 2, "four-key chord emits four heads, only taps score immediately");
        Check(f.HoldStarts == 2 && f.Lanes[0].HoldNote == 0 && f.Lanes[2].HoldNote == 2 && s.JudgedCount == 2, "independent holds accepted immediately without extra score");
        Check(f.Lanes[0].Stage == FeedbackStage.HoldStarted && f.Lanes[1].Stage == FeedbackStage.Hit, "head stages distinguish hold and tap");
        s.Press(0,1.01); s.Press(1,1.01);
        Check(heads == 4 && results == 2, "repeated press does not duplicate feedback or score");
        f.Tick(1,2);
        Check(f.Lanes[0].HoldProgress == .5 && f.Lanes[2].HoldProgress == .5, "half-duration holds display half progress");
        double age = f.Lanes[0].ResultAge;
        f.Tick(0,2);
        Check(f.Lanes[0].HoldProgress == .5 && f.Lanes[0].ResultAge == age, "frozen song time preserves hold progress and animation age");
        s.Release(2,2);
        Check(f.HoldBreaks == 1 && f.Lanes[2].Stage == FeedbackStage.HoldBroken && f.Lanes[2].HoldNote == -1 && s.Miss == 1, "early release visibly breaks and misses once");
        Check(f.Lanes[0].HoldNote == 0, "breaking one chord lane preserves the other hold");
        s.Release(2,2.1); s.Advance(3); s.Advance(4); s.FinishAll();
        Check(f.HoldCompletions == 1 && f.Lanes[0].Stage == FeedbackStage.HoldComplete && f.Lanes[0].HoldNote == -1 && f.Lanes[0].HoldProgress == 1, "tail completes and clears sustained glow");
        Check(s.JudgedCount == 4 && results == 4 && heads == 4 && f.HoldBreaks == 1, "finalization never duplicates scoring or completion effects");
        var missed = new RhythmSession(new[] { N(0,1,2) }); var mf = new RhythmFeedbackState(missed);
        missed.Advance(1.15);
        Check(mf.Lanes[0].Stage == FeedbackStage.Miss && mf.HoldBreaks == 0 && mf.HoldStarts == 0, "missed head is not presented as a broken hold");
        var good = new RhythmSession(new[] { N(0,1,2) }); var gf = new RhythmFeedbackState(good);
        good.Press(0,1.1);
        Check(gf.Lanes[0].Grade == Judgement.Good && good.JudgedCount == 0, "Good head shown immediately before settlement");
        good.Release(0,1.98);
        Check(gf.Lanes[0].Stage == FeedbackStage.HoldComplete && gf.Lanes[0].Grade == Judgement.Good && good.Good == 1, "completion preserves Good head grade");
        var reset = new RhythmFeedbackState(new RhythmSession(new[] { N(0,1,2) }));
        Check(reset.HoldStarts == 0 && reset.Lanes[0].HoldNote == -1 && reset.Lanes[0].Stage == FeedbackStage.None && !reset.Lanes[0].KeyDown, "retry starts with clean feedback");
        return "PASS " + passed + " feedback checks";
    }
}

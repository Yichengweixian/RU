using System;
using System.Globalization;
using System.IO;

public static class CoreTests {
    static int passed;
    static void Check(bool value, string message) {
        if (!value) throw new Exception(message);
        passed++;
    }
    static void Reject(Action action, string message) {
        bool rejected = false;
        try { action(); } catch (FormatException) { rejected = true; }
        Check(rejected, message);
    }
    static string Map(string objects) {
        return "osu file format v14\n[General]\nAudioFilename:beat.wav\nMode:3\n[Metadata]\nTitle:Test: title\nTitleUnicode:节拍练习\nVersion:4K\n[Difficulty]\nCircleSize:4\n[TimingPoints]\n-250,500,4,0,0,100,1,0\n2000,400,4,0,0,100,1,0\n[HitObjects]\n" + objects;
    }
    static NoteData Note(double t, int lane = 0, double end = -1) { return new NoteData { time = t, endTime = end < 0 ? t : end, lane = lane }; }
    static RhythmSession Single(double end = -1) { return new RhythmSession(new[] { Note(1, 0, end) }); }
    public static string Run(string exercisePath) {
        passed = 0;
        var c = ChartLoader.Parse("\uFEFF" + Map("512,192,1500,1,0\n128,192,1000,1,0\n0,192,1000,1,0\n256,192,1250,128,0,2000:0:0:0:0:"));
        Check(c.title == "节拍练习", "Unicode metadata");
        Check(c.notes.Length == 4 && c.notes[0].lane == 0 && c.notes[1].lane == 1 && c.notes[2].lane == 2 && c.notes[3].lane == 3, "4K lane mapping and sort");
        Check(c.notes[0].time == 1 && c.notes[2].time == 1.25 && c.notes[2].endTime == 2, "Absolute timing, no double BPM/offset conversion");
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        var fractional = ChartLoader.Parse(Map("64,192,1234.5,1,0"));
        CultureInfo.CurrentCulture = previous;
        Check(Math.Abs(fractional.notes[0].time - 1.2345) < 1e-9, "Culture-independent milliseconds");
        Reject(() => ChartLoader.Parse(Map("64,192,1000,1,0").Replace("Mode:3", "Mode:0")), "Wrong mode");
        Reject(() => ChartLoader.Parse(Map("64,192,1000,1,0").Replace("CircleSize:4", "CircleSize:7")), "Wrong key count");
        Reject(() => ChartLoader.Parse(Map("64,192,1000,1,0").Replace("Mode:3", "Mode:3.5")), "Fractional mode");
        Reject(() => ChartLoader.Parse(Map("64,192,NaN,1,0")), "NaN rejected");
        Reject(() => ChartLoader.Parse(Map("64,192,-1,1,0")), "Negative time");
        Reject(() => ChartLoader.Parse(Map("64,192,1000,2,0")), "Slider rejected");
        Reject(() => ChartLoader.Parse(Map("64,192,1000,128,0")), "Truncated hold");
        Reject(() => ChartLoader.Parse(Map("64,192,1000,128,0,900:0:0:0:0:")), "Reversed hold");
        Reject(() => ChartLoader.Parse(Map("64,192,1000,1,0\n64,192,1000,1,0")), "Duplicate note");
        Reject(() => ChartLoader.Parse(Map("64,192,1000,128,0,2000:0:0:0:0:\n64,192,1500,1,0")), "Overlapping hold");
        Reject(() => ChartLoader.Parse(Map("")), "Empty chart");
        var inherited = ChartLoader.Parse(Map("64,192,1000,1,0").Replace("[HitObjects]", "3000,-50,4,0,0,100,0,0\n[HitObjects]"));
        Check(inherited.warnings.Length == 1 && inherited.notes[0].time == 1, "SV warning preserves timing");
        c.sourcePath = Path.GetFullPath(exercisePath);
        c.audioName = "../outside.wav";
        Reject(() => ChartLoader.AudioPath(c), "Path traversal");
        foreach (double time in new[] { .94, 1.0, 1.06 }) {
            var s = Single(); s.Press(0, time); s.Advance(3);
            Check(s.Perfect == 1 && s.Miss == 0, "Perfect boundary " + time);
        }
        foreach (double time in new[] { .86, .939, 1.061, 1.14 }) {
            var s = Single(); s.Press(0, time); s.Advance(3);
            Check(s.Good == 1 && s.Miss == 0, "Good boundary " + time);
        }
        var missed = Single(); missed.Press(0, .859); missed.Advance(1.141); missed.Press(0, 1.15); missed.Advance(10);
        Check(missed.Miss == 1 && missed.JudgedCount == 1, "Miss exactly once");
        var spam = Single(); spam.Press(1, 1); spam.Press(0, 1); spam.Press(0, 1);
        Check(spam.Perfect == 1 && spam.Combo == 1, "Wrong lane and repeat press");
        var hold = Single(2); hold.Press(0, 1); hold.Advance(1.5);
        Check(hold.JudgedCount == 0 && hold.States[0] == NoteState.Holding, "Hold waits for tail");
        hold.Advance(2); hold.Release(0, 2.2);
        Check(hold.Perfect == 1 && hold.Miss == 0, "Held through tail");
        var early = Single(2); early.Press(0, 1); early.Release(0, 1.859); early.Advance(3);
        Check(early.Miss == 1 && early.Perfect == 0, "Early release");
        var tailGood = Single(2); tailGood.Press(0, 1); tailGood.Release(0, 1.86);
        Check(tailGood.Good == 1, "Tail good boundary");
        var headGood = Single(2); headGood.Press(0, 1.1); headGood.Release(0, 1.98);
        Check(headGood.Good == 1, "Tail cannot upgrade head");
        var never = Single(2); never.Advance(1.2); never.Press(0, 1.5); never.Advance(3);
        Check(never.Miss == 1, "Hold missed head");
        var chord = new RhythmSession(new[] { Note(1), Note(1, 1), Note(2, 2), Note(3, 3) });
        chord.Press(0, 1); chord.Press(1, 1); chord.Advance(2.2); chord.Press(3, 3);
        Check(chord.Perfect == 3 && chord.Miss == 1 && chord.MaxCombo == 2 && chord.Combo == 1, "Chord, combo reset and maximum");
        var pass = Single(); pass.Press(0, 1.1);
        Check(Math.Abs(pass.Completion - 60) < 1e-9 && pass.Passed, "60 percent pass boundary");
        var finish = new RhythmSession(new[] { Note(1), Note(2, 1, 3) }); finish.FinishAll(); finish.FinishAll();
        Check(finish.Miss == 2, "Finalization idempotent");
        if (!File.Exists(exercisePath)) return "PASS " + passed + " core checks.";
        var exercise = ChartLoader.LoadFile(exercisePath);
        Check(File.Exists(ChartLoader.AudioPath(exercise)), "Editor-exported chart resolves its audio");
        int holds = 0, chords = 0;
        for (int i = 0; i < exercise.notes.Length; i++) {
            if (exercise.notes[i].IsHold) holds++;
            if (i > 0 && exercise.notes[i].time == exercise.notes[i-1].time) chords++;
        }
        Check(exercise.notes.Length >= 60 && holds >= 4 && chords >= 4, "Exercise covers taps, holds and chords");
        var full = new RhythmSession(exercise.notes);
        foreach (var note in exercise.notes) { full.Advance(note.time); full.Press(note.lane, note.time); }
        full.Advance(1000);
        Check(full.Perfect == exercise.notes.Length && full.MaxCombo == exercise.notes.Length && full.Completion == 100, "Full exercise perfect run");
        return "PASS " + passed + " checks; exercise notes=" + exercise.notes.Length + " holds=" + holds + " chordPairs=" + chords;
    }
}

using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Regression fixtures live outside Assets so the delivered game starts empty.
public static class VerifyMvp {
    static int passed;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; }
    static void Reject(Action action, string label) {
        bool rejected = false;
        try { action(); } catch (FormatException) { rejected = true; }
        Check(rejected, label);
    }
    static JObject Note(int type, double x, int start, int end) {
        return new JObject { ["type"] = type, ["positionX"] = x, ["startTime"] = new JArray(start, 0, 1),
            ["endTime"] = new JArray(end, 0, 1), ["above"] = 1, ["isFake"] = 0, ["speed"] = 1, ["alpha"] = 255 };
    }
    public static void Run() {
        passed = 0;
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../validation/fixtures/rpe"));
        Directory.CreateDirectory(dir);
        JObject json = JObject.Parse(@"{'META':{'name':'导入验证','song':'tone.wav','offset':250,'RPEVersion':130},
            'BPMList':[{'startTime':[0,0,1],'bpm':120},{'startTime':[8,0,1],'bpm':180}],
            'judgeLineList':[{'bpmfactor':1,'father':-1,'eventLayers':[{'rotateEvents':[{'start':0,'end':0}]}],'notes':[]}]}");
        var notes = (JArray)json["judgeLineList"][0]["notes"];
        notes.Add(Note(1, -506.25, 2, 2));
        notes.Add(Note(1, -168.75, 4, 4));
        notes.Add(Note(1, 168.75, 4, 4));
        notes.Add(Note(2, 506.25, 6, 10));
        notes.Add(Note(1, -506.25, 12, 12));
        notes.Add(Note(2, -168.75, 16, 22));
        notes.Add(Note(1, 168.75, 24, 24));
        ChartData chart = RpeChartLoader.Parse(json.ToString());
        var priorCulture = System.Globalization.CultureInfo.CurrentCulture;
        try {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Check(RpeChartLoader.Parse(json.ToString()).notes[0].lane == 0, "culture independent coordinates");
        } finally { System.Globalization.CultureInfo.CurrentCulture = priorCulture; }
        Check(chart.notes.Length == 7 && chart.notes[0].lane == 0 && chart.notes[1].lane == 1 && chart.notes[2].lane == 2 && chart.notes[3].lane == 3, "four lanes and simultaneous notes");
        Check(Math.Abs(chart.notes[0].time - 1.25) < 1e-9, "positive offset in milliseconds");
        Check(Math.Abs(chart.notes[3].endTime - (4.25 + 2.0/3)) < 1e-9, "hold crossing BPM change");
        Check(Math.Abs(chart.notes[4].time - (4.25 + 4.0/3)) < 1e-9, "time after BPM change");
        Check(Math.Abs(RpeChartLoader.Beat(new JArray(2, 1, 3), "test") - 7.0/3) < 1e-9, "fractional beats");
        Reject(() => RpeChartLoader.Beat(new JArray(2, 1, 0), "test"), "zero denominator");
        Action<Action<JObject>, string> bad = (change, label) => { var copy = (JObject)json.DeepClone(); change(copy); Reject(() => RpeChartLoader.Parse(copy.ToString()), label); };
        bad(j => j["judgeLineList"][0]["notes"][0]["type"] = 3, "flick rejected");
        bad(j => j["judgeLineList"][0]["notes"][0]["type"] = 4, "drag rejected");
        bad(j => j["judgeLineList"][0]["notes"][0]["isFake"] = 1, "fake rejected");
        bad(j => j["judgeLineList"][0]["notes"][0]["above"] = 2, "below line rejected");
        bad(j => j["judgeLineList"][0]["notes"][0]["positionX"] = 676, "out of range");
        bad(j => j["BPMList"][0]["bpm"] = 0, "zero BPM");
        bad(j => j["BPMList"][0]["startTime"] = new JArray(1,0,1), "first BPM at zero");
        bad(j => ((JArray)j["BPMList"]).Add(j["BPMList"][0].DeepClone()), "duplicate BPM");
        bad(j => ((JArray)j["judgeLineList"]).Add(j["judgeLineList"][0].DeepClone()), "multiple active lines");
        bad(j => j["judgeLineList"][0]["bpmfactor"] = 2, "BPM factor");
        bad(j => j["judgeLineList"][0]["eventLayers"][0]["rotateEvents"][0]["end"] = 20, "rotating line");
        bad(j => ((JArray)j["judgeLineList"][0]["notes"]).Add(Note(1, 500, 7, 7)), "overlapping hold");
        bad(j => ((JArray)j["judgeLineList"][0]["notes"]).Add(Note(1, -500, 2, 2)), "duplicate lane hit");
        var negative = (JObject)json.DeepClone(); negative["META"]["offset"] = -2000;
        Check(RpeChartLoader.Parse(negative.ToString()).notes[0].time == -1, "negative offset allowed with pre-roll");
        var empty = (JObject)json.DeepClone(); empty["judgeLineList"][0]["notes"] = new JArray();
        Check(RpeChartLoader.Parse(empty.ToString()).notes.Length == 0, "empty draft supported");
        var boundary = (JObject)empty.DeepClone(); var boundaryNotes = (JArray)boundary["judgeLineList"][0]["notes"];
        for (int i = 0; i < 4; i++) boundaryNotes.Add(Note(1, -675 + i*337.5, 1, 1));
        var mapped = RpeChartLoader.Parse(boundary.ToString());
        Check(mapped.notes[3].lane == 3 && mapped.notes[2].lane == 2, "zone boundaries");
        string source = Path.Combine(dir, "source.json"), audio = Path.Combine(dir, "tone.wav");
        File.WriteAllText(source, json.ToString());
        WriteAudio(audio);
        string output = ChartImportService.Import(source, audio, Path.Combine(dir, "imported"));
        ChartData imported = ChartLoader.LoadFile(output);
        Check(File.Exists(ChartLoader.AudioPath(imported)), "import copies resolvable audio");
        Check(File.ReadAllText(source) == json.ToString(), "source unchanged");
        Check(ChartImportService.FindAudio(output) != null, "auto locate imported audio");
        json["META"]["name"] = "重新导入验证"; File.WriteAllText(source, json.ToString());
        string updated = ChartImportService.Import(source, audio, Path.Combine(dir, "imported"));
        Check(updated == output && ChartLoader.LoadFile(updated).title == "重新导入验证", "reimport updates same song");
        string before = File.ReadAllText(updated);
        json["judgeLineList"][0]["notes"][0]["type"] = 3; File.WriteAllText(source, json.ToString());
        Reject(() => ChartImportService.Import(source, audio, Path.Combine(dir, "imported")), "invalid import rejected");
        Check(before == File.ReadAllText(updated), "failed import preserves existing song");
        json["judgeLineList"][0]["notes"][0]["type"] = 1; File.WriteAllText(source, json.ToString());
        imported.audioName = "../outside.wav";
        Reject(() => ChartLoader.AudioPath(imported), "audio traversal rejected");
        var perfect = new RhythmSession(chart.notes);
        foreach (var n in chart.notes) { perfect.Advance(n.time); perfect.Press(n.lane, n.time); }
        perfect.Advance(100);
        Check(perfect.Perfect == 7 && perfect.Completion == 100, "converted chart perfect run");
        var miss = new RhythmSession(chart.notes); miss.Advance(100);
        Check(miss.Miss == 7 && !miss.Passed, "converted chart miss run");
        string report = "PASS " + passed + " RPE/import checks";
        File.WriteAllText(Path.Combine(dir, "../../rpe-test-result.txt"), report);
        Debug.Log(report);
    }
    static void WriteAudio(string path) {
        const int rate = 22050, seconds = 12, bytes = rate * seconds * 2;
        using (var writer = new BinaryWriter(File.Create(path))) {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate*2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            for (int i = 0; i < rate*seconds; i++) writer.Write((short)(Math.Sin(i*2*Math.PI*440/rate)*1000));
        }
    }
    public static void VerifyAndBuild() { Run(); BuildMvp.Build(); }
}

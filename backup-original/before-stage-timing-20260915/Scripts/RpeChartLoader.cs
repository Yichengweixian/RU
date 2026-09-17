using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// The gameplay contract is fixed four-key Tap/Hold, not a Phigros renderer.
public static class RpeChartLoader {
    public static readonly double[] LaneCenters = { -506.25, -168.75, 168.75, 506.25 };
    const double Epsilon = 0.0000001;

    public static JObject ReadJson(string text) {
        try {
            return JObject.Parse(text.TrimStart('\uFEFF'), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        } catch (JsonException e) { throw new FormatException("JSON 无法读取：" + e.Message); }
    }

    public static double Number(JToken token, string label) {
        if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            throw new FormatException(label + " 必须是有限数字。");
        double n = token.Value<double>();
        if (Double.IsNaN(n) || Double.IsInfinity(n)) throw new FormatException(label + " 必须是有限数字。");
        return n;
    }
    static double Optional(JObject obj, string key, double fallback) { return obj[key] == null ? fallback : Number(obj[key], key); }
    public static double Beat(JToken token, string label) {
        var tuple = token as JArray;
        if (tuple == null || tuple.Count != 3) throw new FormatException(label + " 必须是 [整数拍, 分子, 分母]。");
        double a = Number(tuple[0], label), b = Number(tuple[1], label), c = Number(tuple[2], label);
        if (a != Math.Floor(a) || b != Math.Floor(b) || c != Math.Floor(c) || c <= 0)
            throw new FormatException(label + " 的三个值必须是整数，分母必须大于零。");
        return a + b / c;
    }

    public sealed class TempoMap {
        readonly double[] beats, bpms, times;
        public TempoMap(JArray list) {
            if (list == null || list.Count == 0) throw new FormatException("缺少 BPMList，请在制谱器设置 BPM。");
            var entries = new List<Tuple<double, double>>();
            foreach (JObject item in list) {
                double b = Beat(item["startTime"], "BPM 起始拍"), bpm = Number(item["bpm"], "BPM");
                if (b < 0 || bpm <= 0) throw new FormatException("BPM 必须大于零，BPM 起始拍不能小于零。");
                entries.Add(Tuple.Create(b, bpm));
            }
            entries.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            if (entries[0].Item1 != 0) throw new FormatException("第一项 BPM 必须从第 0 拍开始。");
            beats = new double[entries.Count]; bpms = new double[entries.Count]; times = new double[entries.Count];
            for (int i = 0; i < entries.Count; i++) {
                beats[i] = entries[i].Item1; bpms[i] = entries[i].Item2;
                if (i > 0) {
                    if (beats[i] == beats[i - 1]) throw new FormatException("同一拍存在重复 BPM 设置。");
                    times[i] = times[i - 1] + (beats[i] - beats[i - 1]) * 60.0 / bpms[i - 1];
                }
            }
        }
        public double Seconds(double beat) {
            int index = Array.BinarySearch(beats, beat);
            if (index < 0) index = Math.Max(0, ~index - 1);
            return times[index] + (beat - beats[index]) * 60.0 / bpms[index];
        }
    }

    public static ChartData Parse(string text) {
        JObject root = ReadJson(text);
        var meta = root["META"] as JObject;
        var lines = root["judgeLineList"] as JArray;
        if (meta == null || lines == null || root["BPMList"] == null)
            throw new FormatException("这不是 RPE JSON 谱面。请在 Re:PhiEdit 选择“导出为 JSON”，不要选旧 PEC 或其他 JSON。");
        var tempo = new TempoMap(root["BPMList"] as JArray);
        double offset = Optional(meta, "offset", 0) / 1000.0;
        var chart = new ChartData {
            title = (string)meta["name"] ?? "未命名曲目", artist = (string)meta["composer"] ?? "",
            difficulty = (string)meta["level"] ?? "4K", audioName = (string)meta["song"] ?? ""
        };
        var notes = new List<NoteData>();
        var warnings = new HashSet<string>();
        int activeLines = 0;
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++) {
            var line = lines[lineIndex] as JObject;
            if (line == null) throw new FormatException("判定线数据不完整。");
            var lineNotes = line["notes"] as JArray;
            if (lineNotes == null || lineNotes.Count == 0) continue;
            activeLines++;
            if (Optional(line, "bpmfactor", 1) != 1) throw new FormatException("请将判定线 bpmfactor 设为 1。");
            if (Optional(line, "father", -1) >= 0) throw new FormatException("基础四轨不使用父子判定线，请取消父线。");
            CheckEvents(line, warnings);
            for (int index = 0; index < lineNotes.Count; index++) {
                string at = "第 " + (lineIndex + 1) + " 条判定线、第 " + (index + 1) + " 个音符：";
                try {
                    var n = lineNotes[index] as JObject;
                    if (n == null) throw new FormatException("音符数据不完整。");
                    double type = Number(n["type"], "音符类型");
                    if (type != 1 && type != 2) throw new FormatException("只支持 Tap（1）和 Hold（2），请移除或改写 Flick/Drag。");
                    if (Optional(n, "isFake", 0) != 0) throw new FormatException("不支持假音符，请改为真实音符或删除。");
                    if (Optional(n, "above", 1) != 1) throw new FormatException("音符必须从判定线上方下落（above=1）。");
                    double x = Number(n["positionX"], "横向位置");
                    if (x < -675 || x > 675) throw new FormatException("横向位置必须在 -675 到 675 之间。");
                    double startBeat = Beat(n["startTime"], "开始节拍");
                    double endBeat = type == 2 ? Beat(n["endTime"], "结束节拍") : startBeat;
                    if (startBeat < 0) throw new FormatException("开始节拍不能小于零。");
                    if (type == 2 && endBeat <= startBeat) throw new FormatException("长按结束必须晚于开始。");
                    int lane = Math.Min(3, (int)Math.Floor((x + 675) / 337.5));
                    notes.Add(new NoteData { time = tempo.Seconds(startBeat) + offset, endTime = tempo.Seconds(endBeat) + offset, lane = lane });
                    if (Optional(n, "speed", 1) != 1 || Optional(n, "yOffset", 0) != 0 || Optional(n, "alpha", 255) < 255)
                        warnings.Add("音符采用游戏统一的下落速度和外观；保留原始击打时间。");
                } catch (Exception e) { throw new FormatException(at + e.Message); }
            }
        }
        if (activeLines > 1) throw new FormatException("基础四轨模板使用一条判定线，请将音符放在同一条线上。");
        notes.Sort((a, b) => { int c = a.time.CompareTo(b.time); return c != 0 ? c : a.lane.CompareTo(b.lane); });
        NoteData[] previous = new NoteData[4];
        foreach (NoteData n in notes) {
            NoteData p = previous[n.lane];
            if (p != null && (n.time < p.endTime - Epsilon || Math.Abs(n.time - p.time) <= Epsilon))
                throw new FormatException("映射后第 " + (n.lane + 1) + " 轨有重叠音符（" + n.time.ToString("F3", CultureInfo.InvariantCulture) + " 秒）。请将同一时刻的音符移到不同横向区域。");
            previous[n.lane] = n;
        }
        chart.notes = notes.ToArray();
        if (notes.Count == 0) warnings.Add("这是空谱面；请在制谱器放置音符后重新导出。");
        chart.warnings = new List<string>(warnings).ToArray();
        return chart;
    }

    static void CheckEvents(JObject line, HashSet<string> warnings) {
        var layers = line["eventLayers"] as JArray;
        if (layers == null) return;
        foreach (JToken layerToken in layers) {
            var layer = layerToken as JObject;
            if (layer == null) continue;
            foreach (string key in new[] { "moveXEvents", "moveYEvents", "rotateEvents" }) {
                var events = layer[key] as JArray;
                if (events == null || events.Count == 0) continue;
                double? fixedValue = null;
                foreach (JObject e in events) {
                    double start = Number(e["start"], key), end = Number(e["end"], key);
                    if (Math.Abs(start - end) > Epsilon || (fixedValue.HasValue && Math.Abs(start - fixedValue.Value) > Epsilon))
                        throw new FormatException("检测到判定线运动（" + key + "）。基础模板只支持固定判定线。");
                    if (key == "rotateEvents" && Math.Abs(start) > Epsilon)
                        throw new FormatException("请将判定线旋转角度设为 0。");
                    fixedValue = start;
                }
            }
            var speed = layer["speedEvents"] as JArray;
            if (speed != null && speed.Count > 1) warnings.Add("谱面速度事件由游戏统一下落速度替代；击打时间不变。");
        }
    }
}

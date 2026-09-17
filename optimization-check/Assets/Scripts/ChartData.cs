using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public sealed class NoteData {
    public double time, endTime;
    public int lane;
    public bool IsHold { get { return endTime > time; } }
}
public sealed class ChartData {
    public RpeChartLoader.TempoMap tempo;
    public double musicOffset;
    public string title = "Untitled", artist = "", difficulty = "4K", audioName = "", sourcePath = "";
    public NoteData[] notes;
    public string[] warnings;
}
public static class ChartLoader {
    public static ChartData LoadFile(string path) {
        string text = File.ReadAllText(path);
        ChartData chart = text.TrimStart('\uFEFF', ' ', '\r', '\n', '\t').StartsWith("{") ? RpeChartLoader.Parse(text) : Parse(text);
        chart.sourcePath = Path.GetFullPath(path);
        return chart;
    }
    public static string AudioPath(ChartData chart) {
        string folder = Path.GetDirectoryName(chart.sourcePath);
        if (String.IsNullOrWhiteSpace(folder) || String.IsNullOrWhiteSpace(chart.audioName))
            throw new FormatException("谱面缺少音频文件名。");
        string relative = chart.audioName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relative)) throw new FormatException("音频必须放在谱面所在文件夹内。");
        string root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(folder, relative));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("音频路径不能指向谱面文件夹之外。");
        return full;
    }
    static double Number(string s, string field) {
        double value;
        if (!Double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || Double.IsNaN(value) || Double.IsInfinity(value))
            throw new FormatException(field + " 不是有效数字。");
        return value;
    }
    // .osu note timestamps already incorporate BPM and offset, in milliseconds.
    public static ChartData Parse(string text) {
        if (String.IsNullOrWhiteSpace(text) || !text.TrimStart('\uFEFF', ' ', '\r', '\n').StartsWith("osu file format v", StringComparison.Ordinal))
            throw new FormatException("请选择 osu!mania 的 .osu 谱面文件。");
        ChartData chart = new ChartData();
        var notes = new List<NoteData>();
        var warnings = new List<string>();
        string section = "", unicodeTitle = "", unicodeArtist = "";
        double mode = -1, keys = -1;
        int lineNumber = 0;
        using (var reader = new StringReader(text)) {
            string raw;
            while ((raw = reader.ReadLine()) != null) {
                lineNumber++;
                string line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith("//")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) { section = line; continue; }
                try {
                    if (section == "[HitObjects]") {
                        string[] p = line.Split(',');
                        if (p.Length < 5) throw new FormatException("音符字段不完整。");
                        double typeNumber = Number(p[3], "音符类型");
                        if (typeNumber != Math.Floor(typeNumber)) throw new FormatException("音符类型必须是整数。");
                        int type = checked((int)typeNumber);
                        bool hold = (type & 128) != 0;
                        if ((type & (2 | 8)) != 0 || (!hold && (type & 1) == 0))
                            throw new FormatException("只支持单键和长按音符。");
                        double time = Number(p[2], "音符时间") / 1000.0;
                        double x = Number(p[0], "轨道位置");
                        if (time < 0) throw new FormatException("音符时间不能小于零。");
                        double end = time;
                        if (hold) {
                            if (p.Length < 6) throw new FormatException("长按缺少结束时间。");
                            end = Number(p[5].Split(':')[0], "长按结束时间") / 1000.0;
                            if (end <= time) throw new FormatException("长按结束时间必须晚于开始时间。");
                        }
                        notes.Add(new NoteData { time = time, endTime = end, lane = (int)Math.Max(0, Math.Min(3, Math.Floor(x * 4 / 512.0))) });
                    } else if (section == "[TimingPoints]") {
                        string[] p = line.Split(',');
                        if (p.Length >= 7 && p[6].Trim() == "0" && warnings.Count == 0)
                            warnings.Add("变速特效采用固定下落速度；音符击打时间保留。");
                    } else {
                        int colon = line.IndexOf(':');
                        if (colon < 0) continue;
                        string key = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
                        if (section == "[General]") {
                            if (key == "Mode") mode = Number(value, "游戏模式");
                            if (key == "AudioFilename") chart.audioName = value.Trim('"');
                        } else if (section == "[Difficulty]" && key == "CircleSize") keys = Number(value, "轨道数");
                        else if (section == "[Metadata]") {
                            if (key == "Title") chart.title = value;
                            if (key == "TitleUnicode") unicodeTitle = value;
                            if (key == "Artist") chart.artist = value;
                            if (key == "ArtistUnicode") unicodeArtist = value;
                            if (key == "Version") chart.difficulty = value;
                        }
                    }
                } catch (Exception e) { throw new FormatException("谱面第 " + lineNumber + " 行：" + e.Message); }
            }
        }
        if (mode != 3) throw new FormatException("请选择 osu!mania 谱面（Mode:3）。");
        if (keys != 4) throw new FormatException("当前模板只支持四轨（CircleSize:4）。");
        if (String.IsNullOrWhiteSpace(chart.audioName)) throw new FormatException("谱面缺少 AudioFilename。");
        if (notes.Count == 0) throw new FormatException("谱面中还没有音符，请先制谱并保存。");
        notes.Sort((a, b) => { int c = a.time.CompareTo(b.time); return c != 0 ? c : a.lane.CompareTo(b.lane); });
        double[] lastEnd = { -1, -1, -1, -1 };
        foreach (NoteData note in notes) {
            if (note.time <= lastEnd[note.lane]) throw new FormatException("第 " + (note.lane + 1) + " 轨有重复或重叠音符，时间 " + note.time.ToString("F3", CultureInfo.InvariantCulture) + " 秒。");
            lastEnd[note.lane] = note.endTime;
        }
        if (unicodeTitle.Length > 0) chart.title = unicodeTitle;
        if (unicodeArtist.Length > 0) chart.artist = unicodeArtist;
        chart.notes = notes.ToArray();
        chart.warnings = warnings.ToArray();
        return chart;
    }
}

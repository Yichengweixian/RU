using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Copies a chart/audio pair into the project without modifying the exported source.
public static class ChartImportService {
    public static string Import(string jsonPath, string audioPath, string songsRoot) {
        jsonPath = Path.GetFullPath(jsonPath);
        audioPath = Path.GetFullPath(audioPath);
        string text = File.ReadAllText(jsonPath);
        RpeChartLoader.Parse(text);
        if (!File.Exists(audioPath)) throw new FileNotFoundException("配套音频不存在。", audioPath);
        string extension = Path.GetExtension(audioPath).ToLowerInvariant();
        if (extension != ".wav" && extension != ".ogg" && extension != ".mp3") throw new FormatException("音频请选择 WAV、OGG 或 MP3。");
        JObject json = RpeChartLoader.ReadJson(text);
        string id;
        using (var hash = SHA256.Create()) id = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(jsonPath.ToLowerInvariant()))).Replace("-", "").Substring(0, 12);
        string destination = Path.Combine(Path.GetFullPath(songsRoot), "chart-" + id);
        string targetAudio = Path.Combine(destination, "audio" + extension), targetChart = Path.Combine(destination, "chart.json");
        // Re-importing a file already inside Songs updates its own directory.
        string root = Path.GetFullPath(songsRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (jsonPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
            destination = Path.GetDirectoryName(jsonPath);
            targetChart = jsonPath;
            targetAudio = Path.Combine(destination, "audio" + extension);
        }
        Directory.CreateDirectory(destination);
        json["META"]["song"] = Path.GetFileName(targetAudio);
        string written = json.ToString(Formatting.Indented);
        // Validate all JSON before writing any destination file.
        RpeChartLoader.Parse(written);
        if (!audioPath.Equals(targetAudio, StringComparison.OrdinalIgnoreCase)) File.Copy(audioPath, targetAudio, true);
        File.WriteAllText(targetChart, written, new UTF8Encoding(false));
        return targetChart;
    }
    public static string FindAudio(string jsonPath) {
        try {
            ChartData chart = ChartLoader.LoadFile(jsonPath);
            string path = ChartLoader.AudioPath(chart);
            return File.Exists(path) ? path : null;
        } catch { return null; }
    }
}

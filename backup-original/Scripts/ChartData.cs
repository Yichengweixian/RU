using UnityEngine;

[System.Serializable]
public class NoteData {
    public float time;   // 这颗音符应该被击打的时刻（秒，从歌曲开头算）
    public int lane;     // 轨道编号：0=最左，3=最右
}

[System.Serializable]
public class LyricData {
    public float time;   // 这句歌词出现的时刻（秒）
    public string text;  // 歌词文字
}

[System.Serializable]
public class ChartData {
    public string title = "";
    public string audioName = "";   // 音频名，对应 Resources/Audio 下的文件（不带扩展名）
    public NoteData[] notes;
    public LyricData[] lyrics;
}

public static class ChartLoader {
    public static ChartData Load(string chartName) {
        TextAsset json = Resources.Load<TextAsset>("Charts/" + chartName);
        if (json == null) {
            Debug.LogError("找不到谱面文件 Resources/Charts/" + chartName + ".json");
            return null;
        }
        return JsonUtility.FromJson<ChartData>(json.text);
    }
}

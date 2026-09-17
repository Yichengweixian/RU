using System;
using System.Runtime.InteropServices;

public static class NativeChartPicker {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    sealed class OpenFileName {
        public int structSize = Marshal.SizeOf(typeof(OpenFileName));
        public IntPtr owner, instance;
        public string filter = "PhiEdit / RPE JSON (*.json)\0*.json\0\0";
        public string customFilter;
        public int maxCustomFilter, filterIndex = 1;
        public string file = new string('\0', 4096);
        public int maxFile = 4096;
        public string fileTitle;
        public int maxFileTitle;
        public string initialDir, title = "选择 PhiEdit 导出的 JSON 谱面";
        public int flags = 0x00001000 | 0x00000800 | 0x00080000 | 0x00000008;
        public short fileOffset, fileExtension;
        public string defaultExtension = "json";
        public IntPtr customData, hook;
        public string templateName;
        public IntPtr reserved;
        public int reserved2, flagsEx;
    }
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetOpenFileName([In, Out] OpenFileName file);
    public static string Open(string directory) {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        var file = new OpenFileName { initialDir = directory };
        return GetOpenFileName(file) ? file.file.TrimEnd('\0') : null;
#else
        return null;
#endif
    }
    public static string OpenAudio(string directory) {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        var file = new OpenFileName { initialDir = directory, title = "选择这份谱面的配套音频", filter = "音频 (*.wav;*.ogg;*.mp3)\0*.wav;*.ogg;*.mp3\0\0", defaultExtension = "" };
        return GetOpenFileName(file) ? file.file.TrimEnd('\0') : null;
#else
        return null;
#endif
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class StageTrackWindow : EditorWindow {
    string chartPath="", message="";
    StageTrack track = new StageTrack();
    Vector2 scroll;
    [MenuItem("音游/编辑演出时间轴")]
    public static void Open() { GetWindow<StageTrackWindow>("演出时间轴").minSize = new Vector2(680,420); }
    void OnGUI() {
        EditorGUILayout.HelpBox("为已导入歌曲添加歌词、乐器提示、灯光颜色、动画事件和演出后记。时间单位为音乐播放秒数。文件单独保存，不改变 RPE 音符。",MessageType.Info);
        if (GUILayout.Button("选择已导入的 chart.json")) {
            string path=EditorUtility.OpenFilePanel("选择谱面",Path.Combine(Application.streamingAssetsPath,"Songs"),"json");
            if (!String.IsNullOrEmpty(path)) {
                try { ChartLoader.LoadFile(path); chartPath=path; string side=Path.ChangeExtension(path,".stage.json");
                    track=File.Exists(side)?JsonUtility.FromJson<StageTrack>(File.ReadAllText(side)):new StageTrack();
                    if(track==null || track.cues==null) track=new StageTrack(); message="";
                } catch(Exception e) { message=e.Message; }
            }
        }
        EditorGUILayout.LabelField(chartPath);
        scroll=EditorGUILayout.BeginScrollView(scroll);
        int remove=-1;
        for(int i=0;i<track.cues.Length;i++) {
            var c=track.cues[i]; EditorGUILayout.BeginHorizontal();
            c.time=EditorGUILayout.DoubleField(c.time,GUILayout.Width(70));
            string[] names={"歌词","乐器","颜色","动画"}, kinds={"lyric","instrument","color","animation"};
            c.kind=kinds[EditorGUILayout.Popup(Math.Max(0,Array.IndexOf(kinds,c.kind)),names,GUILayout.Width(85))];
            c.value=EditorGUILayout.TextField(c.value);
            if(GUILayout.Button("删除",GUILayout.Width(48))) remove=i;
            EditorGUILayout.EndHorizontal();
        }
        if(remove>=0) { var list=new System.Collections.Generic.List<StageCue>(track.cues); list.RemoveAt(remove); track.cues=list.ToArray(); }
        if(GUILayout.Button("添加事件")) { Array.Resize(ref track.cues,track.cues.Length+1); track.cues[track.cues.Length-1]=new StageCue(); }
        EditorGUILayout.LabelField("演出后记（正式演奏及格后可进入）"); track.ending=EditorGUILayout.TextArea(track.ending,GUILayout.MinHeight(80));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox("歌词/乐器：填写文字，空文字表示清除。颜色：#RRGGBB。动画：填写事件名，通过 StageTimeline.OnCue 接到角色动画。",MessageType.None);
        GUI.enabled=!String.IsNullOrEmpty(chartPath);
        if(GUILayout.Button("保存演出文件")) {
            try {
                foreach(var c in track.cues) { if(Double.IsNaN(c.time)||Double.IsInfinity(c.time)) throw new FormatException("时间必须是有效数字。");
                    Color color; if(c.kind=="color"&&!ColorUtility.TryParseHtmlString(c.value,out color)) throw new FormatException("颜色需要 #RRGGBB 格式。"); }
                File.WriteAllText(Path.ChangeExtension(chartPath,".stage.json"),JsonUtility.ToJson(track,true)); AssetDatabase.Refresh(); message="演出已保存，下次开始演奏时生效。";
            } catch(Exception e) { message=e.Message; }
        }
        GUI.enabled=true; EditorGUILayout.LabelField(message);
    }
}

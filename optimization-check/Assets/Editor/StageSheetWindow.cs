using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class StageSheetWindow : EditorWindow {
    [SerializeField] string png="",json="",bankName="";
    [SerializeField] int mode,cellWidth=32,cellHeight=32,count=16,margin,spacing;
    [SerializeField] float pixelsPerUnit=100;
    string error="";
    StageSpriteBank imported;
    Action<StageSpriteBank> onImported;
    [MenuItem("音游/导入动画精灵表")]
    public static void Open() { Open(null); }
    public static void Open(Action<StageSpriteBank> callback) {
        var window=GetWindow<StageSheetWindow>("导入动画精灵表"); window.minSize=new Vector2(520,420); window.onImported=callback;
    }
    string FileField(string label,string path,string extension) {
        EditorGUILayout.BeginHorizontal(); path=EditorGUILayout.TextField(label,path);
        if(GUILayout.Button("选择",GUILayout.Width(55))) { string selected=EditorUtility.OpenFilePanel(label,String.IsNullOrEmpty(path)?"":Path.GetDirectoryName(path),extension); if(!String.IsNullOrEmpty(selected)) path=selected; }
        EditorGUILayout.EndHorizontal(); return path;
    }
    void OnGUI() {
        GUILayout.Label("把精灵表转为可复用动画",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("每个十六分音符换一帧，循环播放。导入只决定画面与顺序，Aseprite 中的帧时长不影响音乐同步。",MessageType.Info);
        png=FileField("精灵表 PNG",png,"png");
        mode=GUILayout.Toolbar(mode,new[]{"Aseprite PNG + JSON","规则网格 PNG"});
        if(mode==0) json=FileField("配套帧数据 JSON",json,"json");
        else {
            cellWidth=EditorGUILayout.IntField("单帧宽度（像素）",cellWidth); cellHeight=EditorGUILayout.IntField("单帧高度（像素）",cellHeight);
            count=EditorGUILayout.IntField("有效帧数",count); margin=EditorGUILayout.IntField("图片四周边距",margin); spacing=EditorGUILayout.IntField("帧间距",spacing);
            EditorGUILayout.HelpBox("从左上角开始，先从左到右，再逐行向下读取。末尾空格不计入有效帧数。",MessageType.None);
        }
        bankName=EditorGUILayout.TextField("素材库名称（可空）",bankName);
        pixelsPerUnit=EditorGUILayout.FloatField("每单位像素数",pixelsPerUnit);
        if(GUILayout.Button("导入为新素材库",GUILayout.Height(30))) try {
            if(mode==0&&String.IsNullOrEmpty(json)) throw new FormatException("请选择配套 JSON，或切换到规则网格模式。");
            imported=StageSheetImporter.Import(png,mode==0?json:"",cellWidth,cellHeight,count,bankName,pixelsPerUnit,margin,spacing);
            error=""; Selection.activeObject=imported; EditorGUIUtility.PingObject(imported); onImported?.Invoke(imported);
        } catch(Exception e) { error=e.Message; }
        if(!String.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error,MessageType.Error);
        if(imported!=null) {
            EditorGUILayout.ObjectField("已导入",imported,typeof(StageSpriteBank),false);
            EditorGUILayout.HelpBox("导入成功。在动画片段中选择素材库和动画名称即可使用；同一个素材库可以用于多首歌曲。",MessageType.Info);
        }
    }
}

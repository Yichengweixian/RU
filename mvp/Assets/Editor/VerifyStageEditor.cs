using System;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class VerifyStageEditor {
    static int passed;
    static void Check(bool condition,string name) { if(!condition) throw new Exception("Stage editor: "+name); passed++; }
    static FieldInfo Field(string name) { return typeof(StageTrackWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic); }
    static void Dirty(StageTrackWindow window,bool value) { typeof(StageTrackWindow).GetProperty("dirty",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,value,null); }
    public static string Run() {
        passed=0;
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        string folder=Path.Combine(root,"validation/fixtures/stage-editor"); Directory.CreateDirectory(folder);
        string chart=Path.Combine(folder,"chart.json"), invalid=Path.Combine(folder,"invalid.json");
        File.Copy(Path.Combine(root,"validation/fixtures/stage-timing/chart.json"),chart,true);
        File.Copy(chart,invalid,true);
        string side=Path.ChangeExtension(chart,".stage.json");
        File.Copy(Path.Combine(root,"validation/fixtures/stage-timing/chart.stage.json"),side,true);
        File.WriteAllText(Path.ChangeExtension(invalid,".stage.json"),"{\"lyrics\":[{\"start\":2,\"end\":1,\"text\":\"bad\"}]}");
        var window=ScriptableObject.CreateInstance<StageTrackWindow>();
        try {
            window.LoadChart(chart);
            Check(!window.hasUnsavedChanges,"load starts clean");
            var track=(StageTrack)Field("track").GetValue(window);
            Check(track.lyrics.Length==3&&track.animations.Length==2,"both tracks loaded");
            window.SetPreviewTime(3.5);
            Check(Math.Abs((double)Field("cursor").GetValue(window)-3.5)<1e-9,"precise seek");
            window.SetPreviewTime(-1); Check((double)Field("cursor").GetValue(window)==0,"seek clamps at start");
            window.SetPreviewTime(1e6); Check((double)Field("cursor").GetValue(window)==(double)Field("duration").GetValue(window),"seek clamps at end");
            track.lyrics[0].text="尚未保存的修改"; Dirty(window,true);
            bool rejected=false; try { window.LoadChart(invalid); } catch(FormatException) { rejected=true; }
            Check(rejected,"invalid replacement rejected");
            Check(ReferenceEquals(track,Field("track").GetValue(window))&&track.lyrics[0].text=="尚未保存的修改","failed load preserves current edits");
            Check((string)Field("chartPath").GetValue(window)==chart&&window.hasUnsavedChanges,"failed load preserves destination and dirty state");
            window.SaveTrack(); Check(!window.hasUnsavedChanges,"successful save clears dirty state");
            Check(StageTrackFile.Read(side).lyrics[0].text=="尚未保存的修改","saved edits round trip");
            string before=File.ReadAllText(side);
            track.lyrics[0].end=track.lyrics[0].start; Dirty(window,true);
            window.SaveTrack();
            Check(window.hasUnsavedChanges,"invalid save retains unsaved state");
            Check(File.ReadAllText(side)==before,"invalid save preserves disk copy");
            Check(!String.IsNullOrEmpty((string)Field("validation").GetValue(window)),"invalid save displays error");
            window.LoadChart(chart);
            Check(((StageTrack)Field("track").GetValue(window)).lyrics[0].text=="尚未保存的修改","reload restores saved edits");
            var sparse=new StageTrack { cues=null };
            StageTrackFile.Save(Path.Combine(folder,"empty.stage.json"),sparse,(ChartData)Field("chart").GetValue(window));
            Check(StageTrackFile.Read(Path.Combine(folder,"empty.stage.json")).cues.Length==0,"empty legacy track can save");
            string result="PASS "+passed+" stage editor checks";
            File.WriteAllText(Path.Combine(root,"validation/stage-editor-result.txt"),result); return result;
        } finally { Dirty(window,false); UnityEngine.Object.DestroyImmediate(window); }
    }
}

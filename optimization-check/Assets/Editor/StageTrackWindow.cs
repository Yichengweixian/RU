using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class StageTrackWindow : EditorWindow {
    [SerializeField] string chartPath="";
    [SerializeField] StageTrack track=new StageTrack();
    [SerializeField] double cursor;
    [SerializeField] int tab;
    ChartData chart;
    AudioClip audio;
    StagePlayback playback;
    StageAnimationOutput[] outputs=new StageAnimationOutput[0];
    StageSpriteBank bank;
    string message="", validation="";
    Vector2 scroll;
    double duration=30, previewStart, previewClock;
    bool playing, audioPlaying;
    bool dirty { get { return hasUnsavedChanges; } set { hasUnsavedChanges=value; } }
    static readonly string[] tabs={"歌词区间","动画片段","原有事件 / 后记"};

    [MenuItem("音游/编辑演出时间轴")]
    public static void Open() { GetWindow<StageTrackWindow>("演出时间轴").minSize=new Vector2(880,680); }
    void OnEnable() {
        saveChangesMessage="演出时间轴有未保存的修改。";
        minSize=new Vector2(880,680); EditorApplication.update+=UpdatePreview;
        if(File.Exists(chartPath)) try { ReadChart(); Rebuild(); } catch(Exception e) { message=e.Message; }
    }
    void OnDisable() { StopPreview(); EditorApplication.update-=UpdatePreview; }
    public override void SaveChanges() { SaveTrack(); if(!dirty) base.SaveChanges(); }
    public override void DiscardChanges() { dirty=false; base.DiscardChanges(); }
    void ReadChart() {
        chart=ChartLoader.LoadFile(chartPath);
        string path=ChartLoader.AudioPath(chart).Replace('\\','/'), assets=Application.dataPath.Replace('\\','/')+"/";
        audio=path.StartsWith(assets,StringComparison.OrdinalIgnoreCase)?AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/"+path.Substring(assets.Length)):null;
        duration=audio!=null?audio.length:Math.Max(30,chart.notes.Length==0?0:chart.notes[chart.notes.Length-1].endTime+2);
        cursor=Math.Max(0,Math.Min(cursor,duration));
    }
    public void LoadChart(string path) {
        // Validate the replacement before touching the current unsaved document.
        var nextChart=ChartLoader.LoadFile(path);
        string side=Path.ChangeExtension(path,".stage.json");
        var nextTrack=File.Exists(side)?StageTrackFile.Read(side):new StageTrack();
        StageTrackFile.Validate(nextTrack,nextChart);
        StopPreview(); chartPath=path; chart=nextChart; track=nextTrack; ReadChart();
        dirty=false; message=""; Rebuild(); Repaint();
    }
    bool CanReplace() { return !dirty||EditorUtility.DisplayDialog("未保存的演出修改","切换会放弃当前未保存的修改。","放弃并继续","返回编辑"); }
    void Rebuild() {
        playback=null; outputs=new StageAnimationOutput[0]; validation="";
        try {
            StageTrackFile.Validate(track,chart);
            playback=new StagePlayback(track,chart);
            bank=String.IsNullOrEmpty(track.spriteBank)?null:Resources.Load<StageSpriteBank>(track.spriteBank);
            if(!String.IsNullOrEmpty(track.spriteBank)&&bank==null) throw new FormatException("找不到素材库："+track.spriteBank);
            if(bank!=null) bank.Validate();
            outputs=new StageAnimationOutput[track.animations.Length];
            for(int i=0;i<outputs.Length;i++) {
                var clip=track.animations[i]; var frames=bank==null?null:bank.Find(clip.sequence);
                if(!String.IsNullOrEmpty(clip.sequence)&&(frames==null||frames.Length==0)) throw new FormatException("找不到动画："+clip.sequence);
                outputs[i]=new StageAnimationOutput { Clip=clip, Frames=frames, FrameCount=frames==null?clip.placeholderFrames:frames.Length };
            }
        } catch(Exception e) { playback=null; outputs=new StageAnimationOutput[0]; validation=e.Message; }
    }
    public void SetPreviewTime(double seconds) { StopPreview(); cursor=Math.Max(0,Math.Min(seconds,duration)); Repaint(); }
    public void SetSpriteBank(StageSpriteBank value) {
        string path=StageSheetImporter.ResourcePath(value);
        if(value!=null) value.Validate();
        track.spriteBank=path; dirty=true; Rebuild(); Repaint();
    }
    void StopPreview() { if(audioPlaying) StageEditorAudio.Stop(); playing=audioPlaying=false; }
    void UpdatePreview() {
        if(!playing) return;
        double now=audioPlaying?StageEditorAudio.Position:previewStart+EditorApplication.timeSinceStartup-previewClock;
        if(now>=0) cursor=now;
        if(cursor>=duration-.02) { cursor=duration; StopPreview(); }
        Repaint();
    }
    void OnGUI() {
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("选择曲目",GUILayout.Width(95))&&CanReplace()) {
            string p=EditorUtility.OpenFilePanel("选择 RPE 谱面",Path.Combine(Application.streamingAssetsPath,"Songs"),"json");
            if(!String.IsNullOrEmpty(p)) try { LoadChart(p); } catch(Exception e) { message=e.Message; }
        }
        GUILayout.Label(chart==null?"请选择已经导入的曲目":chart.title,EditorStyles.boldLabel);
        using(new EditorGUI.DisabledScope(chart==null||!String.IsNullOrEmpty(validation))) {
            if(GUILayout.Button(dirty?"保存 *":"保存",GUILayout.Width(90))) SaveTrack();
            if(GUILayout.Button("导出模板",GUILayout.Width(90))) {
                string p=EditorUtility.SaveFilePanel("导出演出模板","","演出模板.stage.json","json");
                if(!String.IsNullOrEmpty(p)) try { StageTrackFile.Save(p,track,chart); message="模板已导出，可用于其他曲目。"; } catch(Exception e) { message=e.Message; }
            }
        }
        if(GUILayout.Button("套用模板",GUILayout.Width(90))&&chart!=null&&CanReplace()) {
            string p=EditorUtility.OpenFilePanel("选择演出模板","","json");
            if(!String.IsNullOrEmpty(p)) try { var candidate=StageTrackFile.Read(p); new StagePlayback(candidate,chart); track=candidate; dirty=true; Rebuild(); } catch(Exception e) { message=e.Message; }
        }
        EditorGUILayout.EndHorizontal();
        if(chart==null) { EditorGUILayout.HelpBox("歌词按音乐秒数设置；动画读取谱面 BPM，每个十六分音符换一帧并循环。正式素材格式待定，目前可以先使用占位帧编排。",MessageType.Info); return; }
        EditorGUILayout.LabelField(chartPath,EditorStyles.miniLabel);
        DrawTransport(); DrawRuler(); DrawPreview();
        tab=GUILayout.Toolbar(tab,tabs);
        scroll=EditorGUILayout.BeginScrollView(scroll);
        EditorGUI.BeginChangeCheck();
        if(tab==0) DrawLyrics(); else if(tab==1) DrawAnimations(); else DrawLegacy();
        if(EditorGUI.EndChangeCheck()) { dirty=true; Rebuild(); }
        EditorGUILayout.EndScrollView();
        if(!String.IsNullOrEmpty(validation)) EditorGUILayout.HelpBox(validation,MessageType.Error);
        else if(!String.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message,MessageType.Info);
        EditorGUILayout.LabelField("歌词区间按音乐时间；动画按谱面拍点。素材格式与最终角色布局可后续接入。",EditorStyles.miniLabel);
    }
    public void SaveTrack() {
        try { Rebuild(); if(!String.IsNullOrEmpty(validation)) throw new FormatException(validation);
            StageTrackFile.Save(Path.ChangeExtension(chartPath,".stage.json"),track,chart); dirty=false; message="已保存，下次演奏时生效。"; AssetDatabase.Refresh();
        } catch(Exception e) { message=e.Message; }
    }
    void DrawTransport() {
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button(playing?"暂停预览":"播放预览",GUILayout.Width(100))) {
            if(playing) StopPreview();
            else { if(cursor>=duration) cursor=0; previewStart=cursor; previewClock=EditorApplication.timeSinceStartup;
                audioPlaying=StageEditorAudio.Play(audio,cursor); playing=true; message=audioPlaying?"跟随音乐预览。":"静音预览：音频未导入或当前编辑器不支持预听。"; }
        }
        string t=EditorGUILayout.DelayedTextField(StageTimeText.Seconds(cursor),GUILayout.Width(105));
        if(t!=StageTimeText.Seconds(cursor)) try { SetPreviewTime(StageTimeText.ParseSeconds(t)); } catch(Exception e) { message=e.Message; }
        if(chart.tempo!=null) GUILayout.Label("小节:拍:细分  "+StageTimeText.Musical(Math.Max(0,chart.tempo.BeatAt(cursor-chart.musicOffset))));
        GUILayout.FlexibleSpace(); GUILayout.Label("4/4 · 每十六分音符一帧 · 循环",EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        float at=GUILayout.HorizontalSlider((float)cursor,0,(float)duration);
        if(Math.Abs(at-cursor)>.001) SetPreviewTime(at);
    }
    void DrawRuler() {
        Rect r=GUILayoutUtility.GetRect(100,88,GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r,new Color(.09f,.1f,.12f));
        double from=Math.Max(0,cursor-4),to=Math.Min(duration,from+12); from=Math.Max(0,to-12);
        double span=Math.Max(1,to-from);
        Func<double,float> x=t=>r.x+(float)((t-from)/span)*r.width;
        for(int sec=(int)Math.Ceiling(from);sec<=to;sec++) {
            EditorGUI.DrawRect(new Rect(x(sec),r.y,1,20),Color.gray);
            GUI.Label(new Rect(x(sec)+2,r.y,65,18),StageTimeText.Seconds(sec),EditorStyles.miniLabel);
        }
        if(chart.tempo!=null) {
            int first=Math.Max(0,(int)Math.Ceiling(chart.tempo.BeatAt(from-chart.musicOffset)/4));
            for(int bar=first;bar<first+100;bar++) {
                double at=chart.tempo.Seconds(bar*4)+chart.musicOffset; if(at>to) break;
                EditorGUI.DrawRect(new Rect(x(at),r.y+20,1,68),new Color(.3f,.55f,.55f));
                GUI.Label(new Rect(x(at)+2,r.y+18,70,18),"第"+(bar+1)+"小节",EditorStyles.miniLabel);
            }
        }
        foreach(var note in chart.notes) if(note.time>=from&&note.time<=to) EditorGUI.DrawRect(new Rect(x(note.time),r.y+38+note.lane*3,2,3),Color.white);
        foreach(var lyric in track.lyrics) if(lyric!=null) DrawRange(r,x,from,to,lyric.start,lyric.end,53,new Color(.4f,.65f,1));
        if(chart.tempo!=null) foreach(var clip in track.animations) if(clip!=null) DrawRange(r,x,from,to,chart.tempo.Seconds(clip.startBeat)+chart.musicOffset,chart.tempo.Seconds(clip.endBeat)+chart.musicOffset,68,new Color(.4f,.85f,.6f));
        EditorGUI.DrawRect(new Rect(x(cursor),r.y,2,r.height),new Color(1,.7f,.2f));
        var evt=Event.current;
        if((evt.type==EventType.MouseDown||evt.type==EventType.MouseDrag)&&evt.button==0&&r.Contains(evt.mousePosition)) { SetPreviewTime(from+(evt.mousePosition.x-r.x)/r.width*span); evt.Use(); }
    }
    static void DrawRange(Rect r,Func<double,float> x,double from,double to,double start,double end,float y,Color color) {
        if(end<=from||start>=to) return;
        float a=x(Math.Max(from,start)),b=x(Math.Min(to,end)); EditorGUI.DrawRect(new Rect(a,r.y+y,Mathf.Max(1,b-a),9),color);
    }
    void DrawPreview() {
        Rect r=GUILayoutUtility.GetRect(100,115,GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(r,new Color(.025f,.035f,.045f));
        if(playback==null) { GUI.Label(r,"调整下方配置后即可预览。",EditorStyles.centeredGreyMiniLabel); return; }
        foreach(var output in outputs) { output.Frame=playback.FrameAt(output.Clip,cursor,output.FrameCount); output.Sprite=output.Frame<0||output.Frames==null?null:output.Frames[output.Frame]; }
        StagePreview.Draw(new Rect(r.x+10,r.y+5,r.width-20,80),outputs,EditorStyles.whiteMiniLabel);
        GUI.Label(new Rect(r.x+10,r.y+88,r.width-20,25),playback.LyricAt(cursor),EditorStyles.whiteLabel);
    }
    double TimeField(double value,bool musical,float width=105) {
        string before=musical?StageTimeText.Musical(value):StageTimeText.Seconds(value);
        string after=EditorGUILayout.DelayedTextField(before,GUILayout.Width(width));
        if(after!=before) try { return musical?StageTimeText.ParseMusical(after):StageTimeText.ParseSeconds(after); } catch(Exception e) { message=e.Message; }
        return value;
    }
    void DrawLyrics() {
        EditorGUILayout.HelpBox("填写 分:秒.毫秒。起点显示，终点隐藏；整句歌词无需逐帧设置。暂不接受重叠区间，避免显示规则不明确。",MessageType.None);
        int remove=-1;
        for(int i=0;i<track.lyrics.Length;i++) {
            var lyric=track.lyrics[i]; EditorGUILayout.BeginHorizontal();
            lyric.start=TimeField(lyric.start,false); GUILayout.Label("至",GUILayout.Width(20)); lyric.end=TimeField(lyric.end,false);
            lyric.text=EditorGUILayout.TextField(lyric.text);
            if(GUILayout.Button("定位",GUILayout.Width(45))) SetPreviewTime(lyric.start);
            if(GUILayout.Button("删除",GUILayout.Width(45))) remove=i;
            EditorGUILayout.EndHorizontal();
        }
        if(remove>=0) { Remove(ref track.lyrics,remove); GUI.changed=true; }
        if(GUILayout.Button("在播放位置添加歌词")) { double start=cursor; if(track.lyrics.Length>0) start=Math.Max(start,track.lyrics[track.lyrics.Length-1].end); Add(ref track.lyrics,new StageLyric{start=start,end=start+2}); GUI.changed=true; }
    }
    void DrawAnimations() {
        EditorGUILayout.HelpBox("拍点格式：小节:拍:细分。例 2:3:0 = 第2小节第3拍；细分为0～3。素材名留空可使用占位帧，正式素材接入后计时规则不变。",MessageType.None);
        EditorGUILayout.BeginHorizontal();
        var selectedBank=(StageSpriteBank)EditorGUILayout.ObjectField("动画素材库",bank,typeof(StageSpriteBank),false);
        if(selectedBank!=bank) try { SetSpriteBank(selectedBank); } catch(Exception e) { message=e.Message; }
        if(GUILayout.Button("导入精灵表",GUILayout.Width(100))) StageSheetWindow.Open(value=> { if(this!=null) SetSpriteBank(value); });
        EditorGUILayout.EndHorizontal();
        int remove=-1;
        for(int i=0;i<track.animations.Length;i++) {
            var clip=track.animations[i]; EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.BeginHorizontal();
            GUILayout.Label("控件",GUILayout.Width(32)); clip.target=EditorGUILayout.TextField(clip.target,GUILayout.Width(115));
            GUILayout.Label("开始",GUILayout.Width(32)); clip.startBeat=TimeField(clip.startBeat,true,80);
            GUILayout.Label("结束",GUILayout.Width(32)); clip.endBeat=TimeField(clip.endBeat,true,80);
            if(GUILayout.Button("定位",GUILayout.Width(45))&&chart.tempo!=null) SetPreviewTime(chart.tempo.Seconds(clip.startBeat)+chart.musicOffset);
            GUILayout.FlexibleSpace(); if(GUILayout.Button("删除",GUILayout.Width(45))) remove=i;
            EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal();
            if(bank!=null&&bank.sequences!=null) {
                var choices=new System.Collections.Generic.List<string>{"占位动画"};
                foreach(var sequence in bank.sequences) if(sequence!=null) choices.Add(sequence.id);
                int selected=String.IsNullOrEmpty(clip.sequence)?0:choices.IndexOf(clip.sequence);
                if(selected<0) { choices.Add(clip.sequence+"（缺失）"); selected=choices.Count-1; }
                int next=EditorGUILayout.Popup("动画名称",selected,choices.ToArray());
                if(next!=selected) clip.sequence=next==0?"":choices[next];
            } else clip.sequence=EditorGUILayout.TextField("素材名（留空为占位）",clip.sequence);
            using(new EditorGUI.DisabledScope(!String.IsNullOrEmpty(clip.sequence))) { GUILayout.Label("占位帧数",GUILayout.Width(64)); clip.placeholderFrames=EditorGUILayout.IntField(clip.placeholderFrames,GUILayout.Width(65)); }
            EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
        }
        if(remove>=0) { Remove(ref track.animations,remove); GUI.changed=true; }
        using(new EditorGUI.DisabledScope(chart.tempo==null)) if(GUILayout.Button("添加循环动画")) {
            double start=Math.Max(0,Math.Round(chart.tempo.BeatAt(cursor-chart.musicOffset)*4)/4);
            Add(ref track.animations,new StageAnimation{target="角色"+(track.animations.Length+1),startBeat=start,endBeat=start+4}); GUI.changed=true;
        }
    }
    void DrawLegacy() {
        EditorGUILayout.HelpBox("保留原有事件。已有歌词事件仍可播放；新增歌词区间后，歌词由区间轨道管理。动画事件为扩展回调，逐帧播放使用“动画片段”页。",MessageType.None);
        int remove=-1; string[] kinds={"lyric","instrument","color","animation"}; string[] names={"旧歌词事件","乐器","颜色","扩展事件"};
        for(int i=0;i<track.cues.Length;i++) {
            var cue=track.cues[i]; EditorGUILayout.BeginHorizontal(); cue.time=TimeField(cue.time,false);
            cue.kind=kinds[EditorGUILayout.Popup(Math.Max(0,Array.IndexOf(kinds,cue.kind)),names,GUILayout.Width(100))]; cue.value=EditorGUILayout.TextField(cue.value);
            if(GUILayout.Button("删除",GUILayout.Width(45))) remove=i; EditorGUILayout.EndHorizontal();
        }
        if(remove>=0) { Remove(ref track.cues,remove); GUI.changed=true; }
        if(GUILayout.Button("添加事件")) { Add(ref track.cues,new StageCue{time=cursor,kind="instrument"}); GUI.changed=true; }
        EditorGUILayout.LabelField("演出后记"); track.ending=EditorGUILayout.TextArea(track.ending,GUILayout.MinHeight(70));
    }
    static void Add<T>(ref T[] array,T item) { Array.Resize(ref array,array.Length+1); array[array.Length-1]=item; }
    static void Remove<T>(ref T[] array,int index) { var list=new System.Collections.Generic.List<T>(array); list.RemoveAt(index); array=list.ToArray(); }
}

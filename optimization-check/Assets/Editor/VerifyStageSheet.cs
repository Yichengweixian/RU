using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class VerifyStageSheet {
    static int passed;
    static void Check(bool condition,string name) { if(!condition) throw new Exception("Stage sheet: "+name); passed++; }
    static void Reject(Action action,string name) { bool rejected=false; try { action(); } catch(FormatException) { rejected=true; } Check(rejected,name); }
    static JObject Rect(int x,int y,int w,int h) { return new JObject { ["x"]=x,["y"]=y,["w"]=w,["h"]=h }; }
    static JObject Tag(string name,int from,int to,string direction) { return new JObject{["name"]=name,["from"]=from,["to"]=to,["direction"]=direction}; }
    static Color32 Pixel(Sprite sprite,int x,int y) { return sprite.texture.GetPixels32()[((int)sprite.rect.y+y)*sprite.texture.width+(int)sprite.rect.x+x]; }
    static void ColorIs(Color32 actual,Color32 expected,string name) { Check(actual.r==expected.r&&actual.g==expected.g&&actual.b==expected.b&&actual.a==expected.a,name); }
    public static string Run() {
        passed=0;
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../..")),dir=Path.Combine(root,"validation/fixtures/stage-sheet"); Directory.CreateDirectory(dir);
        string png=Path.Combine(dir,"demo.png"),json=Path.Combine(dir,"demo.json"),hashJson=Path.Combine(dir,"demo-hash.json");
        var texture=new Texture2D(32,16,TextureFormat.RGBA32,false); var pixels=new Color32[512];
        // Deliberately asymmetric frames make flipped UVs and trim offsets observable.
        for(int y=0;y<16;y++) for(int x=0;x<32;x++) {
            int local=x%16; pixels[y*32+x]=new Color32(0,0,0,0);
            if(local>=4&&local<=11&&y>=2&&y<=13) pixels[y*32+x]=x<16?new Color32(50,200,240,255):new Color32(240,150,40,255);
            if(y==10&&(local==6||local==9)) pixels[y*32+x]=new Color32(10,20,30,255);
        }
        pixels[15*32]=new Color32(255,0,0,255); pixels[15*32+1]=new Color32(0,255,0,255);
        pixels[14*32]=new Color32(0,0,255,255); pixels[14*32+1]=new Color32(255,255,0,255);
        texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(png,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        var frames=new JArray();
        for(int i=0;i<4;i++) frames.Add(new JObject{["filename"]="frame "+i,["frame"]=Rect((i%2)*16,0,16,16),["rotated"]=false,["trimmed"]=false,["spriteSourceSize"]=Rect(0,0,16,16),["sourceSize"]=new JObject{["w"]=16,["h"]=16},["duration"]=i==0?1000:50});
        var data=new JObject{["frames"]=frames,["meta"]=new JObject{["size"]=new JObject{["w"]=32,["h"]=16},["frameTags"]=new JArray(Tag("摆动",0,3,"forward"),Tag("倒序",0,3,"reverse"),Tag("往返",0,3,"pingpong"),Tag("倒序往返",0,3,"pingpong_reverse"),Tag("单帧",2,2,"pingpong"))}};
        File.WriteAllText(json,data.ToString());
        var parsed=StageSheetImporter.ParseJson(data.ToString(),32,16);
        Check(parsed.frames.Length==4,"JSON array includes all frames");
        Check(parsed.sequences.First(s=>s.name=="往返").indices.SequenceEqual(new[]{0,1,2,3,2,1}),"pingpong avoids repeated endpoints");
        Check(parsed.sequences.First(s=>s.name=="倒序往返").indices.SequenceEqual(new[]{3,2,1,0,1,2}),"reverse pingpong order");
        Check(parsed.sequences.First(s=>s.name=="单帧").indices.SequenceEqual(new[]{2}),"single frame pingpong");
        var hash=(JObject)data.DeepClone(); var dictionary=new JObject(); string[] keys={"frame10","frame2","frame9","frame1"};
        for(int i=0;i<4;i++) dictionary[keys[i]]=frames[i].DeepClone(); hash["frames"]=dictionary; File.WriteAllText(hashJson,hash.ToString());
        var hashParsed=StageSheetImporter.ParseJson(hash.ToString(),32,16);
        Check(hashParsed.frames.Select(f=>f.x).SequenceEqual(new[]{0,16,0,16}),"JSON hash preserves exported order instead of sorting filenames");
        var bad=(JObject)data.DeepClone(); bad["frames"][0]["frame"]["x"]=31;
        Reject(()=>StageSheetImporter.ParseJson(bad.ToString(),32,16),"out of image rect");
        bad=(JObject)data.DeepClone(); bad["frames"][0]["rotated"]=true; Reject(()=>StageSheetImporter.ParseJson(bad.ToString(),32,16),"rotated packing reports unsupported");
        bad=(JObject)data.DeepClone(); bad["meta"]["frameTags"][0]["to"]=99; Reject(()=>StageSheetImporter.ParseJson(bad.ToString(),32,16),"invalid tag range");
        Reject(()=>StageSheetImporter.ParseJson(data.ToString(),16,16),"mismatched PNG dimensions");
        Reject(()=>StageSheetImporter.Grid(32,16,16,16,3),"grid frame overflow");
        var grid=StageSheetImporter.Grid(36,36,16,16,4,1,2);
        Check(grid.frames[3].x==19&&grid.frames[3].y==19,"grid margin spacing row order");
        byte[] before=File.ReadAllBytes(png);
        var gridBank=StageSheetImporter.Import(png,"",16,16,2,"StageDemoGrid",16);
        var arrayBank=StageSheetImporter.Import(png,json,0,0,0,"StageDemoAseprite",16);
        var hashBank=StageSheetImporter.Import(png,hashJson,0,0,0,"StageDemoHash",16);
        Check(before.SequenceEqual(File.ReadAllBytes(png)),"import preserves source PNG");
        Check(gridBank.sequences[0].frames.Length==2,"grid imports real sprites");
        ColorIs(Pixel(gridBank.sequences[0].frames[0],0,15),new Color32(255,0,0,255),"top left remains top left");
        ColorIs(Pixel(gridBank.sequences[0].frames[0],0,14),new Color32(0,0,255,255),"vertical direction preserved");
        Check(arrayBank.Find("倒序")[0]==arrayBank.sequences[0].frames[3],"tag references correct sprite");
        Check(hashBank.Find("摆动").Length==4,"hash import tags retained");
        Check(arrayBank.Find("摆动")[0].texture.filterMode==FilterMode.Point,"pixel texture filter");
        Check(arrayBank.Find("摆动")[0].texture.mipmapCount==1,"pixel texture has no mipmaps");
        var trim=new JObject { ["frames"]=new JArray(new JObject{["frame"]=Rect(0,0,2,2),["trimmed"]=true,["spriteSourceSize"]=Rect(3,1,2,2),["sourceSize"]=new JObject{["w"]=8,["h"]=8}}) };
        string trimJson=Path.Combine(dir,"trim.json"); File.WriteAllText(trimJson,trim.ToString());
        var trimBank=StageSheetImporter.Import(png,trimJson,0,0,0,"StageDemoTrim",8); var trimmedSprite=trimBank.sequences[0].frames[0];
        Check(trimmedSprite.rect.width==8&&trimmedSprite.rect.height==8,"trimmed frame restored canvas size");
        ColorIs(Pixel(trimmedSprite,3,6),new Color32(255,0,0,255),"trim restored top left offset");
        ColorIs(Pixel(trimmedSprite,4,5),new Color32(255,255,0,255),"trim restored bottom right pixel");
        ColorIs(Pixel(trimmedSprite,0,0),new Color32(0,0,0,0),"trim restored transparent padding");
        Check(Resources.Load<StageSpriteBank>(StageSheetImporter.ResourcePath(arrayBank))==arrayBank,"persisted bank loadable by runtime resource path");
        VerifySongs(root,dir,arrayBank);
        string result="PASS "+passed+" sprite sheet checks";
        File.WriteAllText(Path.Combine(root,"validation/stage-sheet-result.txt"),result+"\nDemo bank: "+AssetDatabase.GetAssetPath(arrayBank)); return result;
    }
    static void VerifySongs(string root,string dir,StageSpriteBank bank) {
        var source=JObject.Parse(File.ReadAllText(Path.Combine(root,"validation/fixtures/stage-timing/chart.json")));
        File.Copy(Path.Combine(root,"validation/fixtures/stage-timing/tone.wav"),Path.Combine(dir,"tone.wav"),true);
        var go=new GameObject("Imported sprite timeline check");
        var ui=new GameObject("Bound image",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        var window=ScriptableObject.CreateInstance<StageTrackWindow>();
        try {
            var timeline=go.AddComponent<StageTimeline>(); var renderer=go.AddComponent<SpriteRenderer>(); var target=go.AddComponent<StageSpriteTarget>();
            target.timeline=timeline; target.targetName="角色";
            // Assign both supported presentation components by type to avoid depending
            // on Inspector field labels; call the real binding update below.
            foreach(var field in typeof(StageSpriteTarget).GetFields()) {
                if(field.FieldType==typeof(SpriteRenderer)) field.SetValue(target,renderer);
                if(field.FieldType==typeof(Image)) field.SetValue(target,ui.GetComponent<Image>());
            }
            var refresh=typeof(StageSpriteTarget).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int songIndex=0;songIndex<2;songIndex++) {
                var json=(JObject)source.DeepClone(); json["BPMList"][0]["bpm"]=songIndex==0?120:160; json["BPMList"][1]["bpm"]=songIndex==0?150:110;
                json["META"]["name"]="演出工具示例 "+(songIndex+1);
                string path=Path.Combine(dir,"song"+(songIndex+1)+".json"); File.WriteAllText(path,json.ToString());
                // Independent windows deliberately stay away from exact boundaries.
                string lyric="第 "+(songIndex+1)+" 首歌曲";
                var samples=songIndex==0?new[]{
                    new RuntimeStageChecks.Sample{start=.27,end=.34,frame=0,lyric=""},
                    new RuntimeStageChecks.Sample{start=.395,end=.465,frame=1,lyric=""},
                    new RuntimeStageChecks.Sample{start=.52,end=.59,frame=2,lyric=lyric},
                    new RuntimeStageChecks.Sample{start=.645,end=.715,frame=3,lyric=lyric},
                    new RuntimeStageChecks.Sample{start=2.02,end=2.09,frame=2,lyric=""},
                    new RuntimeStageChecks.Sample{start=4.38,end=4.42,frame=1,lyric=""}
                }:new[]{
                    new RuntimeStageChecks.Sample{start=.27,end=.32,frame=0,lyric=""},
                    new RuntimeStageChecks.Sample{start=.36,end=.41,frame=1,lyric=""},
                    new RuntimeStageChecks.Sample{start=.45,end=.49,frame=2,lyric=""},
                    new RuntimeStageChecks.Sample{start=.55,end=.60,frame=3,lyric=lyric},
                    new RuntimeStageChecks.Sample{start=2.04,end=2.10,frame=3,lyric=""},
                    new RuntimeStageChecks.Sample{start=3.41,end=3.49,frame=1,lyric=""}
                };
                File.WriteAllText(Path.ChangeExtension(path,".expected.json"),JsonUtility.ToJson(new RuntimeStageChecks.Cases{samples=samples},true));
                var chart=ChartLoader.LoadFile(path); var track=new StageTrack{spriteBank=StageSheetImporter.ResourcePath(bank),lyrics=new[]{new StageLyric{start=.5,end=2,text="第 "+(songIndex+1)+" 首歌曲"}},animations=new[]{new StageAnimation{target="角色",sequence="摆动",startBeat=0,endBeat=24}}};
                StageTrackFile.Save(Path.ChangeExtension(path,".stage.json"),track,chart);
                Check(timeline.Load(path,chart)=="","imported bank loaded on song "+songIndex);
                double beatTime=chart.tempo.Seconds(.75)+chart.musicOffset;
                timeline.Seek(beatTime); refresh.Invoke(target,null);
                Check(timeline.Animations[0].Sprite==bank.Find("摆动")[3],"song selects sprite at sixteenth regardless exported duration "+songIndex);
                Check(renderer.enabled&&renderer.sprite==bank.Find("摆动")[3],"SpriteRenderer binding "+songIndex);
                Check(ui.GetComponent<Image>().enabled&&ui.GetComponent<Image>().sprite==renderer.sprite,"UI Image binding "+songIndex);
                timeline.Tick(1); Check(timeline.Lyric==track.lyrics[0].text,"song lyric visible "+songIndex);
                timeline.Tick(2); Check(timeline.Lyric=="","song lyric expires "+songIndex);
                timeline.Seek(chart.tempo.Seconds(8.25)+chart.musicOffset);
                Check(timeline.Animations[0].Sprite==bank.Find("摆动")[1],"sprite after BPM change "+songIndex);
                timeline.PresentationVisible=false; refresh.Invoke(target,null); Check(!renderer.enabled&&!ui.GetComponent<Image>().enabled,"binding hidden outside play "+songIndex); timeline.PresentationVisible=true;
                window.LoadChart(path); window.SetSpriteBank(bank); window.SetPreviewTime(beatTime);
                var outputs=(StageAnimationOutput[])typeof(StageTrackWindow).GetField("outputs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
                Check(outputs.Length==1&&outputs[0].Frames[3]==bank.Find("摆动")[3],"editor uses imported frames "+songIndex);
                window.SaveTrack(); Check(!window.hasUnsavedChanges,"editor saves bank reference "+songIndex);
            }
        } finally { window.DiscardChanges(); UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(ui); }
    }
}

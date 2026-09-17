using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// Import-time adapter; the player only loads the generated bank and sprites.
public static class StageSheetImporter {
    public sealed class Frame { public int x,y,w,h,offsetX,offsetY,width,height; }
    public sealed class Sequence { public string name; public int[] indices; }
    public sealed class Sheet { public Frame[] frames; public Sequence[] sequences; }
    const int MaxDimension=8192, MaxPixels=16777216, MaxFrames=4096;
    static int Integer(JToken token,string field) {
        var value=token==null?null:token[field];
        if(value==null||value.Type!=JTokenType.Integer) throw new FormatException("精灵表缺少整数字段："+field);
        long n=(long)value; if(n<0||n>MaxDimension) throw new FormatException("精灵表字段超出范围："+field); return (int)n;
    }
    static void Size(int width,int height) {
        if(width<1||height<1||width>MaxDimension||height>MaxDimension||(long)width*height>MaxPixels)
            throw new FormatException("精灵表尺寸过大或无效，请分组导出（最多 1600 万像素，单边不超过 8192）。");
    }
    public static Sheet ParseJson(string json,int width,int height) {
        Size(width,height);
        var root=JObject.Parse(json,new JsonLoadSettings { DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error });
        var data=root["frames"]; IEnumerable<JToken> entries;
        if(data is JArray) entries=data.Children();
        else if(data is JObject) entries=((JObject)data).Properties().Select(p=>p.Value);
        else throw new FormatException("请选择 Aseprite 导出的帧数据 JSON。");
        var frames=new List<Frame>();
        foreach(var entry in entries) {
            if(frames.Count>=MaxFrames) throw new FormatException("单个素材库最多 4096 帧，请分组导出。");
            if((bool?)entry["rotated"]==true) throw new FormatException("暂不支持旋转打包，请关闭旋转后导出。");
            var box=entry["frame"];
            var f=new Frame{x=Integer(box,"x"),y=Integer(box,"y"),w=Integer(box,"w"),h=Integer(box,"h")};
            var source=entry["sourceSize"]; var trimmed=entry["spriteSourceSize"];
            f.width=source==null?f.w:Integer(source,"w"); f.height=source==null?f.h:Integer(source,"h");
            if(trimmed!=null) {
                f.offsetX=Integer(trimmed,"x"); f.offsetY=Integer(trimmed,"y");
                if(Integer(trimmed,"w")!=f.w||Integer(trimmed,"h")!=f.h) throw new FormatException("帧尺寸与裁剪数据不一致，请使用 1 倍尺寸、无内部填充导出。");
            } else if((bool?)entry["trimmed"]==true) throw new FormatException("裁剪帧缺少 spriteSourceSize 信息。");
            Size(f.width,f.height);
            if(f.w<1||f.h<1||f.x+f.w>width||f.y+f.h>height||f.offsetX+f.w>f.width||f.offsetY+f.h>f.height)
                throw new FormatException("精灵帧超出图片或原始画布范围，请检查 PNG 与 JSON 是否配套。");
            frames.Add(f);
        }
        if(frames.Count==0) throw new FormatException("精灵表没有动画帧。");
        var size=root["meta"]?["size"];
        if(size!=null&&(Integer(size,"w")!=width||Integer(size,"h")!=height)) throw new FormatException("PNG 尺寸与 JSON 不一致。");
        var sequences=new List<Sequence>(); var names=new HashSet<string>(StringComparer.Ordinal);
        var tags=root["meta"]?["frameTags"];
        if(tags!=null&&!(tags is JArray)) throw new FormatException("动画标签列表格式无效。");
        if(tags!=null) foreach(var tag in tags) {
            string name=(string)tag["name"]; int from=Integer(tag,"from"),to=Integer(tag,"to");
            if(String.IsNullOrWhiteSpace(name)||!names.Add(name)||from>to||to>=frames.Count) throw new FormatException("动画标签重名或帧范围无效。");
            string direction=(string)tag["direction"]??"forward";
            if(direction!="forward"&&direction!="reverse"&&direction!="pingpong"&&direction!="pingpong_reverse") throw new FormatException("不支持的标签播放方向："+direction);
            var order=Enumerable.Range(from,to-from+1).ToList();
            if(direction=="reverse"||direction=="pingpong_reverse") order.Reverse();
            if(direction=="pingpong"||direction=="pingpong_reverse") for(int i=order.Count-2;i>0;i--) order.Add(order[i]);
            sequences.Add(new Sequence{name=name,indices=order.ToArray()});
        }
        string all="全部帧"; while(names.Contains(all)) all="_"+all;
        sequences.Insert(0,new Sequence{name=all,indices=Enumerable.Range(0,frames.Count).ToArray()});
        return new Sheet{frames=frames.ToArray(),sequences=sequences.ToArray()};
    }
    public static Sheet Grid(int width,int height,int cellWidth,int cellHeight,int count,int margin=0,int spacing=0) {
        Size(width,height);
        if(cellWidth<1||cellHeight<1||cellWidth>width||cellHeight>height||margin<0||spacing<0||margin>MaxDimension||spacing>MaxDimension)
            throw new FormatException("请输入有效的单帧尺寸、边距和间距。");
        int columns=(width-2*margin+spacing)/(cellWidth+spacing),rows=(height-2*margin+spacing)/(cellHeight+spacing);
        if(columns<1||rows<1||count<1||count>MaxFrames||count>(long)columns*rows) throw new FormatException("帧数超出网格容量，请检查单帧尺寸与帧数。");
        var frames=new Frame[count];
        for(int i=0;i<count;i++) frames[i]=new Frame{x=margin+(i%columns)*(cellWidth+spacing),y=margin+(i/columns)*(cellHeight+spacing),w=cellWidth,h=cellHeight,width=cellWidth,height=cellHeight};
        return new Sheet{frames=frames,sequences=new[]{new Sequence{name="全部帧",indices=Enumerable.Range(0,count).ToArray()}}};
    }
    static int PngInt(byte[] bytes,int offset) { return checked((int)(((uint)bytes[offset]<<24)|((uint)bytes[offset+1]<<16)|((uint)bytes[offset+2]<<8)|bytes[offset+3])); }
    public static StageSpriteBank Import(string png,string json,int cellWidth,int cellHeight,int count,string name,float pixelsPerUnit=100,int margin=0,int spacing=0) {
        if(!File.Exists(png)) throw new FormatException("请选择精灵表 PNG。");
        if(new FileInfo(png).Length>100*1024*1024) throw new FormatException("PNG 文件过大，请分组导出。");
        byte[] bytes=File.ReadAllBytes(png); byte[] signature={137,80,78,71,13,10,26,10};
        if(bytes.Length<24||!signature.SequenceEqual(bytes.Take(8))) throw new FormatException("所选图片不是有效 PNG。");
        int width=PngInt(bytes,16),height=PngInt(bytes,20); Size(width,height);
        if(!String.IsNullOrEmpty(json)&&(!File.Exists(json)||new FileInfo(json).Length>16*1024*1024)) throw new FormatException("JSON 不存在或文件过大。");
        Sheet sheet=String.IsNullOrEmpty(json)?Grid(width,height,cellWidth,cellHeight,count,margin,spacing):ParseJson(File.ReadAllText(json),width,height);
        if(Single.IsNaN(pixelsPerUnit)||Single.IsInfinity(pixelsPerUnit)||pixelsPerUnit<=0) throw new FormatException("每单位像素数必须大于零。");
        int cellW=sheet.frames.Max(f=>f.width),cellH=sheet.frames.Max(f=>f.height);
        int columns=Math.Max(1,Math.Min(MaxDimension/cellW,(int)Math.Ceiling(Math.Sqrt(sheet.frames.Length*(double)cellH/cellW))));
        int rows=(sheet.frames.Length+columns-1)/columns;
        if(rows*cellH>MaxDimension) { columns=(sheet.frames.Length+(MaxDimension/cellH)-1)/(MaxDimension/cellH); rows=(sheet.frames.Length+columns-1)/columns; }
        int atlasW=columns*cellW,atlasH=rows*cellH; Size(atlasW,atlasH);
        Texture2D source=null,atlas=null; StageSpriteBank bank=null; var sprites=new List<Sprite>(); string asset=null;
        try {
            source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!source.LoadImage(bytes)||source.width!=width||source.height!=height) throw new FormatException("无法读取 PNG 图片。");
            var original=source.GetPixels32(); var pixels=new Color32[atlasW*atlasH];
            atlas=new Texture2D(atlasW,atlasH,TextureFormat.RGBA32,false){name="Frames",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,anisoLevel=0};
            for(int i=0;i<sheet.frames.Length;i++) {
                var f=sheet.frames[i]; int dstX=(i%columns)*cellW,dstY=(i/columns)*cellH;
                // JSON uses a top-left origin. Restore each trimmed frame onto its
                // original transparent canvas before creating stable full-rect sprites.
                for(int y=0;y<f.h;y++) Array.Copy(original,(height-f.y-f.h+y)*width+f.x,pixels,(dstY+f.height-f.offsetY-f.h+y)*atlasW+dstX+f.offsetX,f.w);
                var sprite=Sprite.Create(atlas,new Rect(dstX,dstY,f.width,f.height),new Vector2(.5f,.5f),pixelsPerUnit,0,SpriteMeshType.FullRect);
                sprite.name="frame_"+i.ToString("D4"); sprites.Add(sprite);
            }
            atlas.SetPixels32(pixels); atlas.Apply(false,false);
            bank=ScriptableObject.CreateInstance<StageSpriteBank>();
            bank.sequences=sheet.sequences.Select(s=>new StageSpriteSequence{id=s.name,frames=s.indices.Select(i=>sprites[i]).ToArray()}).ToArray(); bank.Validate();
            EnsureFolder("Assets/Resources"); EnsureFolder("Assets/Resources/StageAnimations");
            string safe=String.Concat((String.IsNullOrWhiteSpace(name)?Path.GetFileNameWithoutExtension(png):name).Select(c=>Char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_'));
            if(safe.Length>80) safe=safe.Substring(0,80);
            asset=AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/StageAnimations/"+safe+".asset");
            AssetDatabase.CreateAsset(bank,asset); AssetDatabase.AddObjectToAsset(atlas,bank);
            foreach(var sprite in sprites) AssetDatabase.AddObjectToAsset(sprite,bank);
            EditorUtility.SetDirty(bank); AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<StageSpriteBank>(asset);
        } catch {
            // Only the unique asset created by this invocation may be removed.
            if(asset!=null&&AssetDatabase.LoadMainAssetAtPath(asset)!=null) AssetDatabase.DeleteAsset(asset);
            foreach(var sprite in sprites) if(sprite!=null&&!AssetDatabase.Contains(sprite)) UnityEngine.Object.DestroyImmediate(sprite);
            if(atlas!=null&&!AssetDatabase.Contains(atlas)) UnityEngine.Object.DestroyImmediate(atlas);
            if(bank!=null&&!AssetDatabase.Contains(bank)) UnityEngine.Object.DestroyImmediate(bank);
            throw;
        } finally { if(source!=null) UnityEngine.Object.DestroyImmediate(source); }
    }
    static void EnsureFolder(string path) { if(!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path)); }
    public static string ResourcePath(StageSpriteBank bank) {
        if(bank==null) return "";
        string path=AssetDatabase.GetAssetPath(bank); int start=path.LastIndexOf("/Resources/",StringComparison.Ordinal);
        if(start<0) throw new FormatException("请使用“导入精灵表”创建素材库，或将素材库放入 Resources 文件夹。");
        return Path.ChangeExtension(path.Substring(start+11),null);
    }
}

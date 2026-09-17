using UnityEngine;

// Small default presentation while final character layout is being designed.
// Projects can consume StageTimeline.Animations / OnAnimationFrame instead.
public static class StagePreview {
    public static void Draw(Rect area, StageAnimationOutput[] animations, GUIStyle label) {
        int count=0; foreach(var output in animations) if(output.Frame>=0) count++;
        if(count==0) return;
        int slot=0;
        foreach(var output in animations) {
            if(output.Frame<0) continue;
            float width=area.width/count;
            Rect cell=new Rect(area.x+slot++*width,area.y,width-6,area.height);
            Rect image=new Rect(cell.x,cell.y,cell.width,Mathf.Max(8,cell.height-23));
            DrawFrame(image,output.Sprite,output.Frame,output.FrameCount);
            GUI.Label(new Rect(cell.x,cell.yMax-22,cell.width,24),output.Clip.target+" "+(output.Frame+1)+"/"+output.FrameCount,label);
        }
    }
    public static void DrawFrame(Rect area, Sprite sprite, int frame, int count) {
        if(sprite!=null) {
            // Sprite rect is in source texture coordinates; sliced/trimmed metadata
            // is owned by the asset adapter, not the timing model.
            var rect=sprite.rect; float scale=Mathf.Min(area.width/rect.width,area.height/rect.height);
            Rect dst=new Rect(area.center.x-rect.width*scale/2,area.center.y-rect.height*scale/2,rect.width*scale,rect.height*scale);
            Rect uv=new Rect(rect.x/sprite.texture.width,rect.y/sprite.texture.height,rect.width/sprite.texture.width,rect.height/sprite.texture.height);
            GUI.DrawTextureWithTexCoords(dst,sprite.texture,uv,true);
        } else {
            GUI.color=new Color(.08f,.13f,.16f); GUI.DrawTexture(area,Texture2D.whiteTexture);
            float size=Mathf.Min(24,Mathf.Min(area.width,area.height)*.5f);
            float fraction=count<=1?0:frame/(float)(count-1);
            GUI.color=RhythmFeedback.Hold;
            GUI.DrawTexture(new Rect(area.x+fraction*Mathf.Max(0,area.width-size),area.center.y-size/2,size,size),Texture2D.whiteTexture);
            GUI.color=Color.white;
        }
    }
}

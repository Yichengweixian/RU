using UnityEngine;
using UnityEngine.UI;

// One retained Canvas graphic: reusable mesh, no per-note GameObjects or Instantiate/Destroy.
public sealed class RhythmPlayfield : MaskableGraphic {
    RhythmSession session;
    RhythmFeedbackState feedback;
    VisibleNotes visible;
    double time;
    float speed;
    public Color Accent = new Color(.42f,.94f,.85f);
    public static RhythmPlayfield Create() {
        var canvasObject = new GameObject("Rhythm Stage Canvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var go = new GameObject("Pooled Note Mesh", typeof(RectTransform), typeof(CanvasRenderer), typeof(RhythmPlayfield));
        go.transform.SetParent(canvasObject.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(-216,326); rect.sizeDelta = new Vector2(432,620);
        var field = go.GetComponent<RhythmPlayfield>(); field.raycastTarget = false;
        return field;
    }
    public void Bind(RhythmSession value, RhythmFeedbackState state) {
        session = value; feedback = state; visible = new VisibleNotes(value.Notes);
    }
    public void Present(double songTime, float scrollSpeed) {
        time = songTime; speed = scrollSpeed;
        if (visible != null) visible.Update(time, 520.0/speed, session.States);
        SetVerticesDirty();
    }
    static Color Fade(Color c, float alpha) { c.a = alpha; return c; }
    static void Box(VertexHelper vh, float x, float y, float width, float height, Color c) {
        float left = Mathf.Max(0,x), right = Mathf.Min(432,x+width);
        float top = Mathf.Max(0,y), bottom = Mathf.Min(620,y+height);
        if (bottom <= top || right <= left) return;
        int n = vh.currentVertCount;
        vh.AddVert(new Vector3(left,-top),c,Vector2.zero); vh.AddVert(new Vector3(right,-top),c,Vector2.zero);
        vh.AddVert(new Vector3(right,-bottom),c,Vector2.zero); vh.AddVert(new Vector3(left,-bottom),c,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
    }
    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear(); if (session == null) return;
        const float judgeY = 498, width = 108;
        for (int lane = 0; lane < 4; lane++) {
            var f = feedback.Lanes[lane]; float x = lane*width;
            float flash = Mathf.Clamp01(1-(float)f.PressAge/.14f);
            Box(vh,x,0,width-1,580,new Color(.035f+flash*.08f,.045f+flash*.1f,.06f+flash*.1f));
            if (f.HoldNote >= 0) Box(vh,x+1,0,width-3,judgeY,Fade(Accent,.08f));
            Box(vh,x,0,1,580,new Color(.18f,.22f,.28f));
            Box(vh,x+7,540,width-14,43,f.KeyDown ? Fade(Accent,.4f) : new Color(.09f,.11f,.14f));
            if (f.HoldNote >= 0) {
                Box(vh,x+5,judgeY-4,width-10,8,Accent);
                Box(vh,x+8,judgeY+12,(width-16)*(float)f.HoldProgress,5,Accent);
            }
        }
        Box(vh,0,judgeY-2,432,4,Color.white);
        foreach (int id in visible.Active) {
            var n = session.Notes[id]; var state = session.States[id];
            float x = n.lane*width+10;
            float head = state == NoteState.Holding ? judgeY : judgeY-(float)(n.time-time)*speed;
            float tail = judgeY-(float)(n.endTime-time)*speed;
            Color c = state == NoteState.Holding ? Accent : Color.white;
            if (n.IsHold) {
                float top = Mathf.Max(0,tail), bottom = Mathf.Min(judgeY+65,head);
                Box(vh,x+16,top,width-52,bottom-top,Fade(c,.5f));
                Box(vh,x+42,top,4,bottom-top,Fade(c,.75f));
                if (tail <= judgeY+65) Box(vh,x,tail-3,width-20,6,c);
            }
            if (head <= judgeY+65) Box(vh,x,head-8,width-20,16,c);
        }
        for (int lane = 0; lane < 4; lane++) {
            var f = feedback.Lanes[lane]; float age = (float)f.ResultAge, center = lane*108+54;
            if (f.Stage == FeedbackStage.None || age >= .32f) continue;
            Color c = Fade(RhythmFeedback.GradeColor(f.Grade),1-age/.32f);
            if (f.Grade == Judgement.Miss) { c.a *= .13f; Box(vh,lane*108+2,458,104,80,c); continue; }
            float radius = 9+32*age/.32f;
            Box(vh,center-radius,judgeY-radius,radius*2,2,c); Box(vh,center-radius,judgeY+radius-2,radius*2,2,c);
            Box(vh,center-radius,judgeY-radius,2,radius*2,c); Box(vh,center+radius-2,judgeY-radius,2,radius*2,c);
            for (int p = 0; p < 6; p++) {
                float angle = p*Mathf.PI/3+.25f, distance = 15+48*age/.32f;
                Box(vh,center+Mathf.Cos(angle)*distance-2,judgeY+Mathf.Sin(angle)*distance-2,4,4,c);
            }
        }
    }
}

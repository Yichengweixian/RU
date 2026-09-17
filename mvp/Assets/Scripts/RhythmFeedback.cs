using System;
using UnityEngine;

// Original procedural visuals/audio: no Phira textures, audio or source copied.
public sealed class RhythmFeedback : MonoBehaviour {
    public RhythmFeedbackState State { get; private set; }
    AudioSource hitAudio;
    AudioClip hitClip, finishClip;
    RhythmSession session;
    public float Volume = .45f;
    public static readonly Color Perfect = new Color(1f, .83f, .36f);
    public static readonly Color Good = new Color(.38f, .78f, 1f);
    public static readonly Color Broken = new Color(1f, .38f, .36f);
    public static readonly Color Hold = new Color(.42f, .94f, .85f);

    void Awake() {
        hitAudio = gameObject.AddComponent<AudioSource>();
        hitAudio.playOnAwake = false; hitAudio.spatialBlend = 0;
        hitClip = MakeTone("Hit", .055f, 1800, true);
        finishClip = MakeTone("Hold complete", .09f, 920, false);
    }
    public void Bind(RhythmSession value) {
        if (session != null) { session.HitStarted -= PlayHead; session.Judged -= PlayResult; }
        session = value; State = new RhythmFeedbackState(value);
        session.HitStarted += PlayHead; session.Judged += PlayResult;
        hitAudio.Stop();
    }
    void PlayHead(int index, Judgement grade) { hitAudio.PlayOneShot(hitClip, Mathf.Clamp01(Volume)); }
    void PlayResult(int index, Judgement grade) {
        if (session.Notes[index].IsHold && grade != Judgement.Miss) hitAudio.PlayOneShot(finishClip, Mathf.Clamp01(Volume)*.65f);
    }
    public void StopAudio() { if (hitAudio != null) hitAudio.Stop(); }
    void OnDestroy() {
        if (session != null) { session.HitStarted -= PlayHead; session.Judged -= PlayResult; }
        if (hitClip != null) Destroy(hitClip);
        if (finishClip != null) Destroy(finishClip);
    }
    static AudioClip MakeTone(string name, float duration, float hz, bool click) {
        const int rate = 44100;
        var samples = new float[(int)(rate*duration)];
        uint noise = 173;
        for (int i = 0; i < samples.Length; i++) {
            float t = i/(float)rate;
            noise = noise*1664525u+1013904223u;
            float random = ((noise >> 8)/16777215f)*2-1;
            float envelope = Mathf.Min(1,t/.0007f)*Mathf.Exp(-t*(click ? 95 : 48));
            float tone = Mathf.Sin(2*Mathf.PI*hz*t) + .25f*Mathf.Sin(2*Mathf.PI*hz*1.5f*t);
            samples[i] = (tone*.32f + (click ? random*.23f : 0))*envelope;
        }
        var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
        clip.SetData(samples, 0); return clip;
    }
    public static Color GradeColor(Judgement grade) { return grade == Judgement.Perfect ? Perfect : grade == Judgement.Good ? Good : Broken; }
    static void Box(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
    static Color Alpha(Color c, float alpha) { c.a = alpha; return c; }

    public void DrawBase(int laneIndex, float x, float width, float judgeY) {
        var lane = State.Lanes[laneIndex];
        float press = Mathf.Clamp01(1-(float)lane.PressAge/.14f);
        float gray = .055f + (lane.KeyDown ? .10f : 0) + press*.11f;
        Box(new Rect(x, 0, width-1, judgeY+80), new Color(gray,gray,gray));
        if (lane.HoldNote >= 0) Box(new Rect(x+1, 0, width-3, judgeY), Alpha(Hold,.075f));
        Box(new Rect(x, 0, 1, judgeY+80), new Color(.25f,.27f,.3f));
        Box(new Rect(x+7, judgeY+42, width-14, 43), lane.KeyDown ? Alpha(lane.HoldNote >= 0 ? Hold : Color.white,.23f) : new Color(.10f,.11f,.12f));
        Box(new Rect(x+7, judgeY+80, width-14, lane.KeyDown ? 4 : 1), lane.HoldNote >= 0 ? Hold : Alpha(Color.white,lane.KeyDown ? .8f : .28f));
    }
    public void DrawEffects(int laneIndex, float x, float width, float judgeY, double songTime, GUIStyle textStyle) {
        var lane = State.Lanes[laneIndex];
        float center = x+width/2;
        bool holding = lane.HoldNote >= 0;
        float pulse = .5f+.5f*Mathf.Sin((float)songTime*2*Mathf.PI*4);
        if (holding) {
            for (int i = 3; i >= 1; i--) Box(new Rect(x+5-i*2, judgeY-3-i*3, width-10+i*4, 6+i*6), Alpha(Hold,.055f));
            Box(new Rect(x+5,judgeY-4,width-10,8), Alpha(Hold,.75f+pulse*.25f));
            Box(new Rect(x+8,judgeY+12,width-16,5), Alpha(Hold,.15f));
            Box(new Rect(x+8,judgeY+12,(width-16)*(float)lane.HoldProgress,5), Hold);
            for (int i = 0; i < 3; i++) {
                float travel = Mathf.Repeat((float)songTime*1.8f+i/3f,1);
                Box(new Rect(center-22+i*20, judgeY-10-travel*48, 3, 6), Alpha(Hold,(1-travel)*.8f));
            }
            GUI.color = Hold;
            GUI.Label(new Rect(x, judgeY+17, width, 24), "HOLD " + Mathf.FloorToInt((float)lane.HoldProgress*100) + "%", textStyle);
            GUI.color = Color.white;
        }
        if (lane.Stage == FeedbackStage.None) return;
        float age = (float)lane.ResultAge;
        Color color = GradeColor(lane.Grade);
        if (age < .32f && lane.Grade != Judgement.Miss) {
            float t = age/.32f, radius = 9+32*t;
            Box(new Rect(center-radius,judgeY-radius,radius*2,2),Alpha(color,1-t));
            Box(new Rect(center-radius,judgeY+radius-2,radius*2,2),Alpha(color,1-t));
            Box(new Rect(center-radius,judgeY-radius,2,radius*2),Alpha(color,1-t));
            Box(new Rect(center+radius-2,judgeY-radius,2,radius*2),Alpha(color,1-t));
            for (int i = 0; i < 6; i++) {
                float a = i*Mathf.PI/3+.25f, distance = 15+48*t;
                Box(new Rect(center+Mathf.Cos(a)*distance-2,judgeY+Mathf.Sin(a)*distance-2,4,4),Alpha(color,(1-t)*(1-t)));
            }
        }
        if (age < .65f) {
            float opacity = Mathf.Clamp01((.65f-age)/.25f);
            string label = lane.Stage == FeedbackStage.HoldComplete ? "完成" : lane.Stage == FeedbackStage.HoldBroken ? "断开" : lane.Stage == FeedbackStage.HoldStarted ? "接住" : lane.Grade.ToString();
            if (lane.Grade == Judgement.Miss) Box(new Rect(x+2,judgeY-40,width-4,80),Alpha(Broken,.12f*opacity));
            GUI.color = Alpha(color,opacity);
            GUI.Label(new Rect(x-6,judgeY-83-Mathf.Min(age,.2f)*35,width+12,34),label,textStyle);
            GUI.color = Color.white;
        }
    }
}

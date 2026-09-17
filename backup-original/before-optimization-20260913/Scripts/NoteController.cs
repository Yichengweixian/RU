using UnityEngine;

// Drawing only. Timing and judgement belong to RhythmSession.
public static class NoteController {
    public static void Draw(NoteData note, NoteState state, double time, float speed, float left, float width, float judgeY) {
        if (state == NoteState.Done) return;
        float head = judgeY - (float)(note.time - time) * speed;
        float tail = judgeY - (float)(note.endTime - time) * speed;
        float x = left + note.lane * width + 10;
        if (state == NoteState.Holding) head = judgeY;
        if (head < -20 || tail > judgeY + 80) return;
        if (note.IsHold) {
            float top = Mathf.Max(0, tail), bottom = Mathf.Min(judgeY + 65, head);
            if (bottom > top) {
                bool holding = state == NoteState.Holding;
                GUI.color = holding ? Color.Lerp(RhythmFeedback.Hold, Color.white, .15f+.15f*Mathf.Sin((float)time*8)) : new Color(.38f,.47f,.49f);
                GUI.DrawTexture(new Rect(x + 16, top, width - 52, bottom - top), Texture2D.whiteTexture);
                GUI.color = holding ? new Color(1,1,1,.7f) : new Color(1,1,1,.25f);
                GUI.DrawTexture(new Rect(x + (width-20)/2-2, top, 4, bottom-top), Texture2D.whiteTexture);
            }
            if (tail >= 0 && tail <= judgeY + 65) {
                GUI.color = state == NoteState.Holding ? RhythmFeedback.Hold : Color.white;
                GUI.DrawTexture(new Rect(x, tail - 3, width - 20, 6), Texture2D.whiteTexture);
            }
        }
        if (head >= 0 && head <= judgeY + 65) {
            GUI.color = state == NoteState.Holding ? RhythmFeedback.Hold : Color.white;
            GUI.DrawTexture(new Rect(x, head - 8, width - 20, 16), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }
}

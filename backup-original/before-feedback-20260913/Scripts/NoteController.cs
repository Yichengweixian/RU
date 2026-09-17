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
                GUI.color = state == NoteState.Holding ? Color.white : new Color(0.45f, 0.45f, 0.45f);
                GUI.DrawTexture(new Rect(x + 20, top, width - 60, bottom - top), Texture2D.whiteTexture);
            }
            if (tail >= 0 && tail <= judgeY + 65) {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(x, tail - 3, width - 20, 6), Texture2D.whiteTexture);
            }
        }
        if (head >= 0 && head <= judgeY + 65) {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, head - 8, width - 20, 16), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }
}

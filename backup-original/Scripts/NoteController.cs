using UnityEngine;

public class NoteController : MonoBehaviour {
    [HideInInspector] public NoteData data;
    [HideInInspector] public float speed = 4f;   // 下落速度（世界单位/秒）
    [HideInInspector] public float judgeY = -3f;
    [HideInInspector] public bool judged = false;

    void Update() {
        if (!GameManager.playing) return;
        float t = GameManager.songTime;
        Vector3 p = transform.position;
        p.y = judgeY + (data.time - t) * speed;   // 下落公式：位置由时间差决定
        transform.position = p;
        if (p.y < judgeY - 0.5f) {                // 掉过判定线太远 = Miss
            judged = true;
            GameManager.instance.OnNoteMiss();
            Destroy(gameObject);
        }
    }

    public void Hit() {
        judged = true;
        Destroy(gameObject);
    }
}

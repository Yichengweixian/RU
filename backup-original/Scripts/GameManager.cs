using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {
    public static GameManager instance;
    public static bool playing = false;       // 是否正在打歌
    public static float songTime = 0f;        // 当前歌曲播放到第几秒（用音频时钟，精确）

    // ---- 由 Bootstrap 在代码里自动赋值，不需要在 Inspector 拖 ----
    public AudioSource audioSource;
    public GameObject notePrefab;
    public Transform noteRoot;
    public string chartName = "song1";        // 谱面名（对应 Resources/Charts/song1.json）
    public float laneCount = 4;
    public float laneWidth = 1.5f;
    public float laneStartX = -2.25f;         // 第一列中心 X
    public float judgeY = -3f;                // 判定线 Y
    public float noteSpeed = 4f;

    public GameObject menuPanel;
    public GameObject resultPanel;
    public GameObject storyPanel;
    public Text songTitleText;
    public Text comboText;
    public Text timeText;
    public Text judgeText;
    public Text resultText;
    public Text storyText;
    public Button btnStart;
    public Button btnRetry;
    public Button btnStory;
    public Button btnBack;
    public Button btnSkip;

    // ---- 内部状态 ----
    ChartData chart;
    double playStartDsp = 0;
    int totalNotes = 0;
    float scoreSum = 0f;
    int combo = 0;
    int maxCombo = 0;
    int perfectCount = 0, goodCount = 0, missCount = 0;
    bool ended = false;

    void Awake() {
        instance = this;
    }

    void Start() {
        chart = ChartLoader.Load(chartName);
        if (chart == null) return;
        songTitleText.text = chart.title;
        audioSource.clip = Resources.Load<AudioClip>("Audio/" + chart.audioName);
        if (audioSource.clip == null)
            Debug.LogError("找不到音频 Resources/Audio/" + chart.audioName +
                "（请把歌曲文件放进 Assets/Resources/Audio/ 并改名为 " + chart.audioName + "）");

        btnStart.onClick.AddListener(StartSong);
        btnRetry.onClick.AddListener(StartSong);
        btnStory.onClick.AddListener(ShowStory);
        btnBack.onClick.AddListener(BackToMenu);
        btnSkip.onClick.AddListener(BackToMenu);

        ShowMenu();
    }

    void Update() {
        if (playing) {
            songTime = (float)(AudioSettings.dspTime - playStartDsp);
            if (timeText != null) timeText.text = "t = " + songTime.ToString("F2");
            if (!ended && audioSource.clip != null && songTime > audioSource.clip.length + 1f) EndSong();
        }
        if (!playing) return;

        if (Input.GetKeyDown(KeyCode.A)) TryHit(0);
        if (Input.GetKeyDown(KeyCode.S)) TryHit(1);
        if (Input.GetKeyDown(KeyCode.D)) TryHit(2);
        if (Input.GetKeyDown(KeyCode.F)) TryHit(3);
    }

    // ---------- 开始打歌 ----------
    void StartSong() {
        StopAllCoroutines();
        resultPanel.SetActive(false);
        storyPanel.SetActive(false);
        menuPanel.SetActive(false);
        judgeText.gameObject.SetActive(false);

        foreach (Transform t in noteRoot) Destroy(t.gameObject);   // 清掉旧音符

        totalNotes = chart.notes.Length;
        scoreSum = 0f; combo = 0; maxCombo = 0;
        perfectCount = 0; goodCount = 0; missCount = 0;
        ended = false;
        comboText.text = "";

        // 按谱面生成音符：X 由轨道决定，Y 用公式算（t=0 时刻的初始位置）
        for (int i = 0; i < chart.notes.Length; i++) {
            NoteData n = chart.notes[i];
            GameObject go = Instantiate(notePrefab, noteRoot);
            go.SetActive(true);
            float x = laneStartX + n.lane * laneWidth;
            go.transform.position = new Vector3(x, judgeY + n.time * noteSpeed, 0f);
            NoteController nc = go.GetComponent<NoteController>();
            nc.data = n;
            nc.speed = noteSpeed;
            nc.judgeY = judgeY;
        }

        // 用音频时钟精确对拍：0.6 秒后开始播，游戏时钟从那刻算起
        playStartDsp = AudioSettings.dspTime + 0.6;
        audioSource.PlayScheduled(playStartDsp);
        songTime = 0f;
        playing = true;
    }

    // ---------- 判定 ----------
    void TryHit(int lane) {
        // 找这一列里离判定时刻最近、且还没判定的音符
        NoteController best = null;
        float bestDiff = 999f;
        foreach (Transform t in noteRoot) {
            NoteController nc = t.GetComponent<NoteController>();
            if (nc == null || nc.judged || nc.data.lane != lane) continue;
            float diff = Mathf.Abs(nc.data.time - songTime);
            if (diff < bestDiff) { bestDiff = diff; best = nc; }
        }
        if (best == null || bestDiff > 0.2f) return;   // 太早按 / 这一列没有可判定的音符，忽略

        if (bestDiff <= 0.06f) {          // ±60ms：Perfect
            perfectCount++; scoreSum += 1f;
            combo++; ShowJudge("Perfect");
        } else if (bestDiff <= 0.14f) {   // ±140ms：Good
            goodCount++; scoreSum += 0.6f;
            combo++; ShowJudge("Good");
        } else {                          // 按了但差太多
            missCount++; combo = 0; ShowJudge("Miss");
        }
        maxCombo = Mathf.Max(maxCombo, combo);
        comboText.text = combo > 0 ? "COMBO " + combo : "";
        best.Hit();
    }

    public void OnNoteMiss() {            // 音符掉到底部没按
        missCount++; combo = 0;
        comboText.text = "";
        ShowJudge("Miss");
    }

    void ShowJudge(string s) {
        judgeText.text = s;
        judgeText.gameObject.SetActive(true);
        StopCoroutine("HideJudge");
        StartCoroutine(HideJudge());
    }

    IEnumerator HideJudge() {
        yield return new WaitForSeconds(0.4f);
        judgeText.gameObject.SetActive(false);
    }

    // ---------- 结算 ----------
    void EndSong() {
        playing = false;
        ended = true;
        audioSource.Stop();
        float completion = totalNotes > 0 ? (scoreSum / totalNotes) * 100f : 0f;
        bool pass = completion >= 60f;
        resultText.text = "完成度 " + completion.ToString("F1") + "%\n"
            + (pass ? "演出顺利" : "演出不完美，但依然动人") + "\n\n"
            + "Perfect " + perfectCount + "     Good " + goodCount + "     Miss " + missCount + "\n"
            + "最大连击 " + maxCombo;
        resultPanel.SetActive(true);
    }

    // ---------- 剧情（占位：歌词逐句浮现） ----------
    void ShowStory() {
        resultPanel.SetActive(false);
        storyPanel.SetActive(true);
        StartCoroutine(PlayStory());
    }

    IEnumerator PlayStory() {
        float lastT = 0f;
        foreach (LyricData l in chart.lyrics) {
            yield return new WaitForSeconds(Mathf.Max(0f, l.time - lastT));
            storyText.text = l.text;
            lastT = l.time;
        }
        storyText.text = "……（未完待续）";
    }

    void BackToMenu() {
        StopAllCoroutines();
        resultPanel.SetActive(false);
        storyPanel.SetActive(false);
        ShowMenu();
    }

    void ShowMenu() {
        menuPanel.SetActive(true);
        playing = false;
        songTime = 0f;
    }
}

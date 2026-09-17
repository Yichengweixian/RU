using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 全自动搭建：播放游戏时自动创建相机设置、判定线、轨道、音符模板、全部 UI。
// 不需要手动搭场景、不需要拖任何引用。放好音频和谱面文件后直接 Play 即可。
public static class Bootstrap {

    static Sprite whitePixel;
    static readonly Dictionary<int, Font> fontCache = new Dictionary<int, Font>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSetup() {
        SetupCamera();
        EnsureEventSystem();

        // ---- 世界层：判定线、轨道线、音符模板 ----
        GameObject judgeLine = MakeSpriteObject("JudgeLine", new Vector2(6f, 0.06f), new Color(0.85f, 0.95f, 1f));
        judgeLine.transform.position = new Vector3(0f, -3f, 0f);

        for (int i = 0; i < 4; i++) {
            GameObject lane = MakeSpriteObject("Lane" + i, new Vector2(0.03f, 5f), new Color(1f, 1f, 1f, 0.3f));
            lane.transform.position = new Vector3(-2.25f + i * 1.5f, 0f, 0f);
        }

        GameObject noteTemplate = MakeSpriteObject("NoteTemplate", new Vector2(0.6f, 0.6f), Color.white);
        noteTemplate.AddComponent<NoteController>();
        noteTemplate.SetActive(false);   // 作为模板，复制时由 GameManager 激活

        GameObject noteRoot = new GameObject("NoteRoot");

        // ---- GameManager 与音频 ----
        GameObject gmGo = new GameObject("GameManager");
        AudioSource audioSource = gmGo.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        GameManager gm = gmGo.AddComponent<GameManager>();

        // ---- UI ----
        Canvas canvas = MakeCanvas();

        // 菜单面板
        GameObject menuPanel = MakePanel(canvas.transform, "MenuPanel", new Color(0.05f, 0.05f, 0.12f, 0.92f));
        Text title = MakeText(menuPanel.transform, "TitleText", "歌名会显示在这里", 80, new Vector2(0f, 150f));
        Button startBtn = MakeButton(menuPanel.transform, "StartButton", "开始演出", new Vector2(0f, -50f));

        // 结算面板（初始隐藏）
        GameObject resultPanel = MakePanel(canvas.transform, "ResultPanel", new Color(0.05f, 0.05f, 0.12f, 0.95f));
        Text resultText = MakeText(resultPanel.transform, "ResultText", "", 48, new Vector2(0f, 80f));
        Button retryBtn = MakeButton(resultPanel.transform, "RetryButton", "重新演出", new Vector2(0f, -110f));
        Button storyBtn = MakeButton(resultPanel.transform, "StoryButton", "继续剧情", new Vector2(0f, -190f));
        Button backBtn = MakeButton(resultPanel.transform, "BackButton", "返回菜单", new Vector2(0f, -270f));
        resultPanel.SetActive(false);

        // 剧情面板（初始隐藏）
        GameObject storyPanel = MakePanel(canvas.transform, "StoryPanel", new Color(0f, 0f, 0f, 1f));
        Text storyText = MakeText(storyPanel.transform, "StoryText", "", 52, new Vector2(0f, 0f));
        Button skipBtn = MakeButton(storyPanel.transform, "SkipButton", "跳过", new Vector2(0f, -330f));
        storyPanel.SetActive(false);

        // 打歌 HUD
        Text comboText = MakeText(canvas.transform, "ComboText", "", 64, new Vector2(-700f, 400f));
        comboText.alignment = TextAnchor.MiddleLeft;
        Text timeText = MakeText(canvas.transform, "TimeText", "t = 0.00", 24, new Vector2(700f, 430f));
        timeText.alignment = TextAnchor.MiddleRight;
        Text judgeText = MakeText(canvas.transform, "JudgeText", "", 100, new Vector2(0f, 80f));
        judgeText.gameObject.SetActive(false);
        Text hintText = MakeText(canvas.transform, "HintText", "A  S  D  F", 40, new Vector2(0f, -420f));
        hintText.color = new Color(1f, 1f, 1f, 0.5f);

        // ---- 把引用全部交给 GameManager ----
        gm.audioSource = audioSource;
        gm.notePrefab = noteTemplate;
        gm.noteRoot = noteRoot.transform;
        gm.menuPanel = menuPanel;
        gm.resultPanel = resultPanel;
        gm.storyPanel = storyPanel;
        gm.songTitleText = title;
        gm.comboText = comboText;
        gm.timeText = timeText;
        gm.judgeText = judgeText;
        gm.resultText = resultText;
        gm.storyText = storyText;
        gm.btnStart = startBtn;
        gm.btnRetry = retryBtn;
        gm.btnStory = storyBtn;
        gm.btnBack = backBtn;
        gm.btnSkip = skipBtn;
    }

    // ================= 辅助函数 =================

    static void SetupCamera() {
        Camera cam = Camera.main;
        if (cam == null) {
            GameObject camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.15f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;
    }

    static void EnsureEventSystem() {
        if (Object.FindObjectOfType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static Canvas MakeCanvas() {
        GameObject go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        return canvas;
    }

    static GameObject MakePanel(Transform parent, string name, Color color) {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.sprite = GetWhitePixel();
        img.color = color;
        return go;
    }

    static Text MakeText(Transform parent, string name, string content, int size, Vector2 pos) {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1600f, 200f);
        rt.anchoredPosition = pos;
        Text t = go.GetComponent<Text>();
        t.text = content;
        t.font = GetFont(size);
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos) {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360f, 90f);
        rt.anchoredPosition = pos;
        Image img = go.GetComponent<Image>();
        img.sprite = GetWhitePixel();
        img.color = new Color(0.2f, 0.25f, 0.38f, 1f);
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        Text t = MakeText(go.transform, "Label", label, 40, Vector2.zero);
        t.fontSize = 40;
        t.rectTransform.sizeDelta = new Vector2(360f, 90f);
        return btn;
    }

    static GameObject MakeSpriteObject(string name, Vector2 scale, Color color) {
        GameObject go = new GameObject(name);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhitePixel();
        sr.color = color;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        return go;
    }

    static Sprite GetWhitePixel() {
        if (whitePixel != null) return whitePixel;
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        whitePixel = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return whitePixel;
    }

    // 直接用系统字体（微软雅黑/黑体），不用手动导入字体文件
    static Font GetFont(int size) {
        if (!fontCache.TryGetValue(size, out Font f)) {
            f = Font.CreateDynamicFontFromOSFont(
                new string[] { "Microsoft YaHei", "SimHei", "SimSun", "Arial" }, size);
            fontCache[size] = f;
        }
        return f;
    }
}

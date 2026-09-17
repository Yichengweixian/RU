> 当前版本已搭好框架，请先阅读 [开始使用](开始使用.md)，通过 Unity 的“音游”菜单导入 PhiEdit JSON。以下保留为旧版手动制作参考。

# MVP 制作教程（Unity / 团结引擎，从零到能玩）

> 目标：做出一首歌的完整闭环——**选歌 → 打歌（四列下落音符 + 判定）→ 结算（完成度 + 60% 及格）→ 剧情（占位：歌词逐句浮现）→ 返回菜单**
> 本教程完全零基础向，每一步告诉你"点哪里、建什么、为什么"。
> 完成后你就有一个可以玩、可以展示的 MVP，之后再慢慢替换成真正的美术和剧情。

---

## 0. 你会做出来的东西

按下 Play，看到一个菜单（歌名 + "开始演出"按钮）。点开始后：四列音符从上方掉下来，用键盘 **A / S / D / F** 在判定线上敲击。歌曲结束后弹出结算（完成度、Perfect/Good/Miss 数量、及格与否），点"继续剧情"后黑底画面里歌词一句句浮现。打完点"返回菜单"，回到开头。

技术要点只有四个（记住这四个词，后面都围绕它们）：
1. **AudioSettings.dspTime**：音频硬件时钟，用它做节拍判定最精确（这是音游的精髓）
2. **下落公式**：音符 Y 坐标 = 判定线Y + (音符时间 - 当前时间) × 速度
3. **JsonUtility**：Unity 内置的 JSON 解析，读谱面用
4. **判定窗口**：按键时刻和音符时刻的差（±0.06 秒 Perfect，±0.14 秒 Good，超出算 Miss）

---

## 1. Unity 界面速览（先认识你的工作台）

打开你的项目后，你会看到这些区域（默认布局）：

| 面板 | 是干什么的 |
|---|---|
| **Scene（场景）** | 中间最大的区域，编辑游戏画面用的。可以拖拽物体、缩放视角 |
| **Game（游戏）** | Scene 旁边的标签页，点它看到的是"玩家视角"，游戏实际运行画面 |
| **Hierarchy（层级）** | 左侧竖条，当前场景里所有物体的列表 |
| **Inspector（检视器）** | 右侧，选中任何一个物体时，它的所有属性都在这改 |
| **Project（项目）** | 下方，你电脑上这个项目的所有文件（脚本、图片、音频、场景） |
| **Console（控制台）** | Project 旁边的标签，报错信息都在这 |

**几个立刻要会的操作：**
- 选物体：Hierarchy 里点一下
- 移动物体：Scene 里选中后按 **W**（拖动），按 **R**（缩放），按 **E**（旋转）
- 缩放视角：Scene 里按住**鼠标右键**拖动、滚轮缩放
- 运行游戏：顶部中央的 **▶ Play** 按钮，再点一次停止
- 建脚本：Project 窗口里右键 → Create → C# Script
- 场景文件：Hierarchy 里的东西都存在场景里（Assets/Scenes 下），记得 Ctrl+S 保存

**黄金规则：运行游戏时，在 Inspector/Project 里改的东西，停止后全部还原。** 所以改配置要停止状态改。

---

## 2. 准备素材（三样东西）

### 2.1 歌曲音频
1. 在 Project 窗口里右键 → Create → Folder，命名 `Audio`
2. 找到你的歌曲文件（MP3 或 WAV 都行），**直接拖进 Project 的 Audio 文件夹**
3. 把拖进去的音频**重命名为 `song1`**（选中它，按 F2，或 Inspector 顶部改名）——注意是改名，不是扩展名
4. 选中 song1，在 Inspector 里确认：**Load Type = Decompress On Load**（音游需要即时播放，这样延迟最小）

> 为什么叫 song1：谱面 JSON 里写 `"audioName": "song1"`，代码按这个名字加载，MVp 阶段固定写死。

### 2.2 中文字体（关键！不然剧情文字全是方块）
Unity 默认字体不含中文，必须给项目一个中文字体。
1. 打开文件管理器，进 `C:\Windows\Fonts`
2. 找 **simhei.ttf**（黑体，**注意要 .ttf 单个文件**，别选 .ttc 那种合集——Unity 不认 ttc）
3. 复制 simhei.ttf，粘贴到项目的 **Assets 文件夹**（不是子文件夹也行的，就放 Assets 根目录）
4. 回到 Unity，等它自动导入完，在 Project 里能看到 `simhei`（字体图标）
5. 如果没有 simhei.ttf，去网上搜"思源黑体 ttf"下载一个中文 .ttf 放进 Assets 也行

### 2.3 谱面文件（JSON）
谱面 = 音符和歌词的时间安排表，写在 JSON 里，Unity 自动读取。

1. 在 Project 里创建文件夹 `Resources`，再在里面创建 `Charts`（最终路径：`Assets/Resources/Charts`）
2. 在 Project 的 Charts 文件夹里右键 → Create → Text 文件（txt），**重命名成 `song1.json`**
   - 注意：新建的是 .txt，要把后缀改成 .json。Windows 默认隐藏扩展名，先在文件管理器"查看"里勾选"文件扩展名"，然后重命名把 .txt 改成 .json。或者在 Unity 里：右键 txt → Rename，把名字改成 song1.json（Unity 里改名会自动改扩展名，推荐这个方式）
3. 双击打开 song1.json（会用系统文本编辑器打开），把下面内容**全部替换**进去，保存：

```json
{
  "title": "歌名显示在菜单上",
  "audioName": "song1",
  "notes": [
    { "time": 1.0, "lane": 0 },
    { "time": 1.5, "lane": 1 },
    { "time": 2.0, "lane": 2 },
    { "time": 2.5, "lane": 3 },
    { "time": 3.0, "lane": 0 },
    { "time": 3.5, "lane": 1 },
    { "time": 4.0, "lane": 2 },
    { "time": 4.5, "lane": 3 }
  ],
  "lyrics": [
    { "time": 1.0, "text": "把歌词写在引号里，这是第一句" },
    { "time": 4.0, "text": "这是第二句，会在这首歌的第4秒出现" }
  ]
}
```

**字段解释：**
- `title`：菜单上显示的歌名，随便写
- `audioName`：对应 Audio 文件夹里的音频名（就是 song1）
- `notes`：音符列表。`time` = 这首歌的第几秒需要敲这个音符；`lane` = 第几列（0=最左，3=最右）
- `lyrics`：歌词列表。`time` = 这句歌词第几秒出现（剧情阶段会按这个顺序逐句显示）

先把这个跑通，之后再教你"怎么把整首歌的音符都填出来"（第 7 节）。

---

## 3. 搭建场景（30 分钟，做一次后面都是复用）

### 3.1 相机
场景里默认有 Main Camera。选中它，Inspector 里设置：
- **Projection = Orthographic**（正交，2D 用）
- **Size = 5**（画面世界高度=10 个单位）
- **Background = 深色**（点色块，用十六进制 `#1A1A2E` 之类，随你喜欢）

### 3.2 判定线（音符落到这里时敲击）
1. Hierarchy 右键 → **2D Object → Sprites → Square**，命名 `JudgeLine`
2. Inspector 里把 **Position Y 改成 -3**
3. **Scale 改成 (6, 0.06, 1)**（一条横线）
4. 颜色：Inspector 里 Sprite Renderer 的 Color 改成亮色（比如白色或青色）

### 3.3 四列轨道线（视觉辅助，帮玩家看清列）
1. 再次创建 Square，命名 `Lane0`，**Scale = (0.03, 5, 1)**，Position X = **-2.25**，Y = 0，颜色半透明（Color 里把 A 拉到 100 左右）
2. 再创建三个，X 分别是 **-0.75、0.75、2.25**（对应 Lane1、Lane2、Lane3）
   - 快捷方式：选中 Lane0 按 Ctrl+D 复制，然后只改 X 值

### 3.4 音符 Prefab（关键概念：预制体）
Prefab 就是"模板"——做一份，复制 N 份，还能批量改。
1. 创建 Square，命名 `Note`，**Scale = (0.6, 0.6, 1)**，颜色亮色（比如白色）
2. 给 Note 挂上脚本：见第 4 节创建脚本后，把 `NoteController.cs` 拖到 Note 上（或点 Add Component 搜索）
3. 把 Hierarchy 里的 Note **拖进 Project 窗口**（拖到 Assets 里任意位置），松手——Project 里出现一个 Note 图标，**Hierarchy 里的 Note 会变蓝**，说明它是 prefab 实例了
4. 现在把 Hierarchy 里的 Note **删掉**（Project 里的那份保留）

### 3.5 空容器（用来装音符和挂脚本）
1. Hierarchy 右键 → Create Empty，命名 `NoteRoot`（位置 0,0,0）
2. 再 Create Empty，命名 `GameManager`
3. 给 GameManager 挂 **AudioSource 组件**（Inspector → Add Component → AudioSource），**取消勾选 Play On Awake**

### 3.6 UI（Canvas + 面板）
1. Hierarchy 右键 → **UI → Canvas**（会自动连带创建 EventSystem，别删）
2. 选中 Canvas，Inspector 里找到 **Canvas Scaler** 组件，**UI Scale Mode = Scale With Screen Size**，Reference Resolution = **1920 × 1080**
3. 在 Canvas 下创建 UI 面板。Hierarchy 里右键 Canvas → **UI → Panel**，命名 `MenuPanel`
   - Panel 的 Image 组件可以改颜色当背景，点开 Image 的 Color 调个半透明深色
4. MenuPanel 里加文字：右键 MenuPanel → UI → **Text（Legacy）**（注意选 Legacy，别选 TextMeshPro）
   - 命名 `TitleText`，Inspector 里：**Font 选 simhei**（之前导入的字体）、**Font Size = 80**、把文本改成"开始演出前的歌名"、居中（Alignment 中间那个）
   - 位置：调 Rect Transform 让它显示在面板上部（点面板左上角的"移动"工具拖动，或直接改 Pos）
5. 加按钮：右键 MenuPanel → **UI → Button**，命名 `StartButton`
   - Button 里的子物体 Text 改成"开始演出"，Font 选 simhei，Size = 40
   - 拖到面板中间

**现在复制粘贴做另外两个面板**（Hierarchy 里右键 Canvas → UI → Panel 各来一次）：
- **ResultPanel**（结算面板，初始要隐藏，见下）：
  - 一个 Text `ResultText`：Size 45，居中，内容留空
  - 三个按钮：`RetryButton`（重来）、`StoryButton`（继续剧情）、`BackButton`（返回菜单）
- **StoryPanel**（剧情面板，初始隐藏）：
  - 一个 Text `StoryText`：Size 45，居中，内容留空
  - 一个按钮 `SkipButton`（跳过）

**两个打歌时的 HUD**（直接在 Canvas 下，不在任何 Panel 里）：
- `ComboText`：左上角（Pos X=-700, Y=400 左右），Size 60
- `TimeText`：右上角，Size 20，内容 "t = 0.00"
- `JudgeText`：画面中央，Size 90，内容留空——**这个要初始隐藏**（选中它，Inspector 最上面的勾选取消掉）

**初始隐藏**：选中 ResultPanel 和 StoryPanel，把 Inspector 最顶部的勾选**取消掉**（灰掉）——这样运行时默认不显示。

---

## 4. 创建脚本（三段代码，全部复制粘贴）

在 Project 里右键 → Create → Folder，命名 `Scripts`。然后依次创建三个 C# 脚本（右键 Scripts 文件夹 → Create → C# Script），名字必须**严格一致**（文件名=类名，不能改）。

### 4.1 `ChartData.cs`（数据：读 JSON 用）
```csharp
using UnityEngine;

[System.Serializable]
public class NoteData {
    public float time;   // 这颗音符应该被击打的时刻（秒）
    public int lane;     // 轨道编号：0=最左，3=最右
}

[System.Serializable]
public class LyricData {
    public float time;   // 这句歌词出现的时刻（秒）
    public string text;  // 歌词文字
}

[System.Serializable]
public class ChartData {
    public string title = "";
    public string audioName = "";   // 音频名，对应 Resources/Audio 下的文件
    public NoteData[] notes;
    public LyricData[] lyrics;
}

public static class ChartLoader {
    public static ChartData Load(string chartName) {
        TextAsset json = Resources.Load<TextAsset>("Charts/" + chartName);
        if (json == null) {
            Debug.LogError("找不到谱面文件 Resources/Charts/" + chartName + ".json");
            return null;
        }
        return JsonUtility.FromJson<ChartData>(json.text);
    }
}
```

### 4.2 `NoteController.cs`（音符的行为）
```csharp
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
        p.y = judgeY + (data.time - t) * speed;   // 下落公式
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
```

### 4.3 `GameManager.cs`（总控制：流程 + 判定 + 结算）
```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {
    public static GameManager instance;
    public static bool playing = false;       // 是否正在打歌
    public static float songTime = 0f;        // 当前歌曲播放到第几秒

    // ---- 以下全部在 Inspector 里拖引用 ----
    public AudioSource audioSource;
    public GameObject notePrefab;
    public Transform noteRoot;                // 装音符的容器
    public string chartName = "song1";        // 谱面名
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
            Debug.LogError("找不到音频 Resources/Audio/" + chart.audioName);

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
            if (!ended && songTime > audioSource.clip.length + 1f) EndSong();
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

        // 按谱面生成音符：X 由轨道决定，Y 用公式算（t=0 时的位置）
        for (int i = 0; i < chart.notes.Length; i++) {
            NoteData n = chart.notes[i];
            GameObject go = Instantiate(notePrefab, noteRoot);
            float x = laneStartX + n.lane * laneWidth;
            go.transform.position = new Vector3(x, judgeY + n.time * noteSpeed, 0f);
            NoteController nc = go.GetComponent<NoteController>();
            nc.data = n;
            nc.speed = noteSpeed;
            nc.judgeY = judgeY;
        }

        // 用 dspTime 精确对拍：0.6 秒后开始播，游戏时钟从那刻算起
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
        if (best == null || bestDiff > 0.2f) return;   // 太早按/这列没音符，忽略

        if (bestDiff <= 0.06f) {          // ±60ms
            perfectCount++; scoreSum += 1f;
            combo++; ShowJudge("Perfect");
        } else if (bestDiff <= 0.14f) {   // ±140ms
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
```

---

## 5. 组装（把线接起来）

1. 在 Hierarchy 选中 `GameManager` 物体
2. 把 `GameManager.cs` 拖到它的 Inspector 上（或 Add Component 搜索 GameManager）
3. 现在按照脚本顶部的字段，**逐个拖引用**（从 Hierarchy/Project 拖到 Inspector 的框里）：

| 字段 | 拖什么 |
|---|---|
| Audio Source | GameManager 物体上的 AudioSource 组件 |
| Note Prefab | Project 里的 Note 预制体 |
| Note Root | Hierarchy 里的 NoteRoot |
| Menu Panel | Canvas 下的 MenuPanel |
| Result Panel | ResultPanel |
| Story Panel | StoryPanel |
| Song Title Text | MenuPanel 下的 TitleText |
| Combo Text | Canvas 下的 ComboText |
| Time Text | TimeText |
| Judge Text | JudgeText |
| Result Text | ResultPanel 下的 ResultText |
| Story Text | StoryPanel 下的 StoryText |
| Btn Start | StartButton |
| Btn Retry | ResultPanel 下的 RetryButton |
| Btn Story | StoryButton |
| Btn Back | BackButton |
| Btn Skip | StoryPanel 下的 SkipButton |

> 小技巧：Inspector 里字段名的左边有个小圆圈图标，点开是"按类型搜索"，可以快速选。

4. 保存场景：Ctrl+S（第一次会问你存哪，存到 Assets/Scenes，命名 Main）

---

## 6. 运行测试（激动人心的时刻）

1. 顶部 Game 视图下拉选 **1920×1080**（和 Canvas 参考分辨率一致，UI 不变形）
2. 按 **▶ Play**
3. 你应该看到菜单面板：歌名 + 开始演出按钮
4. 点"开始演出"——0.6 秒后音乐响起，音符开始下落
5. 按 **A/S/D/F** 对应 0-3 列敲击，看 JudgeText 显示 Perfect/Good/Miss
6. 歌曲放完 → 结算面板弹出 → 点"继续剧情" → 歌词逐句浮现 → 点"跳过"或"返回菜单"
7. 点"重来"再打一遍

**测试用的 8 个音符**间隔 0.5 秒、四列轮流，正好教你熟悉手感。跑通了，MVP 就算成了。

---

## 7. 填整首歌的谱面（进阶：怎么给真歌做谱）

跑通后你会想给整首歌填音符。方法（不用任何工具）：

1. **先听歌**，在纸上或记事本里记下：第几秒进鼓、进主歌、副歌。可以边听边看 `TimeText` 右上角显示的 "t = x.xx"（这是精确秒数，比你自己读秒准）
2. 打开 song1.json，按节奏把音符补进去。**先补重拍**（鼓点/每拍一下），lane 轮流换，保证手指不打架
3. 保存 JSON → 回 Unity → 点 Play 试玩 → 感觉哪个音符不准就改 time
4. 反复试错——这是所有音游制作人的日常

给新手的原则：**音符宁少勿多，间距别小于 0.2 秒**，否则按不过来。把一首歌的"主旋律重拍"做出来，已经很有成就感了。

**关于判定节奏的整体偏移**：如果你觉得"所有音符都偏早/偏晚"，不要一个个改 time——在 JSON 里统一加减：把音符放慢，就在所有 time 上加个差值。比如全歌感觉早了 0.1 秒，就把每个 time 加 0.1（注意歌词也对应调整）。后续版本我们会加"判定偏移"设置，MVP 先用这个土办法。

---

## 8. 常见问题（先看这个再问我）

| 问题 | 原因与解决 |
|---|---|
| 点 Play 后 Console 报错，一片红 | 把报错文字复制发我，常见是：脚本文件名和类名不一致（改名会出错）、JSON 写错了（少个逗号/括号） |
| 音乐不响 | 检查：AudioSource 挂了吗？Play On Awake 是否取消？音频是否改名叫 song1？Resources/Audio 路径对不对 |
| 点"开始演出"没反应 | Btn Start 没拖引用，或 menuPanel 没拖引用 |
| 音符不掉/瞬移 | 下落公式依赖 songTime——确认 AudioSource 拖对了、音乐在播 |
| 按 A/S/D/F 没判定 | 音符 prefab 上是否挂了 NoteController？noteRoot 是否拖对了？音符在 noteRoot 下面吗（音符生成到 noteRoot 里） |
| 中文全是方块 □□ | 字体没设置：每个 Text 的 Font 都要选 simhei |
| 文字太小/太大 | 改每个 Text 的 Font Size；或 Canvas Scaler 的参考分辨率 |
| 结算面板没出来 | 歌放完才会出。检查 timeText 是否在走（在走说明时钟对）；还在走但不结算，看 audioSource.clip.length 是否有值 |
| 判定感觉不对 | 先确认音乐开头是否和谱面 time=0 对齐。整体偏了就按第 7 节整体加减 time |
| 音符被 UI 挡住 | UI 是屏幕层，音符是世界层，不会真挡住，习惯就好；不喜欢可以调 HUD 透明 |

---

## 9. 做完 MVP 之后（下一步路线图）

- **美术替换**：把 Square 方块换成你在 aseprite 画的音符像素图（Sprite 拖进 Note prefab 的 Sprite 栏，Filter Mode 设 Point，像素风立刻有了）
- **像素风设置**：相机加 Pixel Perfect Camera 组件、贴图 Filter Mode = Point、分辨率调成 整数倍
- **多首歌/选歌列表**：ChartLoader 改成读多个 JSON，加个列表界面
- **歌词收集系统**：打歌时命中歌词碎片 → 图鉴页回看
- **演出舞台**：用 aseprite 画舞台背景图，音符层前移，做出"乐队演出"的感觉

每走一步卡住了，随时把报错截图/文字发我，我带你排。

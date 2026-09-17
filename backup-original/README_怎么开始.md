# MVP 项目：怎么开始

这个项目是**全自动搭建**的——你只需要做三件事：

## 第一步：放歌曲文件
把你选的歌复制到这个文件夹（还没创建就先建）：

```
mvp\Assets\Resources\Audio\
```

把文件**改名为 `song1`**（扩展名保持 .mp3 或 .wav 不变，比如 `song1.mp3`）。

## 第二步：改谱面
打开 `mvp\Assets\Resources\Charts\song1.json`，把歌名和歌词改成你的（音符可以先不动，先用示例跑通）。

JSON 字段说明：
- `title`：菜单上显示的歌名
- `audioName`：音频名（就是第一步的文件名 song1）
- `notes`：音符列表，`time` = 第几秒敲，`lane` = 第几列（0=最左 3=最右）
- `lyrics`：歌词，`time` = 第几秒出现

填整首歌谱面的方法见《MVP制作教程.md》第 7 节（在项目文件夹上一层）。

## 第三步：打开项目，按 Play
1. 团结 Hub → 打开 → 选择 `mvp` 文件夹
2. Unity 打开后，双击打开场景：`Assets/Scenes/SampleScene.unity`
3. 点顶部 **▶ Play** 按钮

然后：
- 菜单出现 → 点"开始演出"
- 音符从上方掉落，用 **A / S / D / F** 对应 0-3 列在判定线上敲击
- 歌曲结束出结算（完成度 ≥ 60% 算演出顺利）
- 点"继续剧情" → 歌词逐句浮现（占位剧情）
- 点"返回菜单"或"重新演出"

## 常见问题
- 音乐不响：确认文件确实在 `Assets/Resources/Audio/` 里且改名为 `song1`
- 报错：把 Console（下方）里的红字复制发给我
- 音符按了没反应：确认按键是 A S D F（英文输入法状态）
- 全部代码在 `Assets/Scripts/` 里，改完保存后 Unity 会自动重新编译，不用重启

## 代码结构
| 文件 | 作用 |
|---|---|
| Bootstrap.cs | 运行时自动搭建一切（UI/判定线/音符），不用碰场景 |
| GameManager.cs | 总控制：流程、判定、结算、剧情 |
| NoteController.cs | 单颗音符：下落、Miss 检测 |
| ChartData.cs | 谱面 JSON 的数据结构 |

# 演出工具交付核对 · 2026-09-15

本次交付针对可复用歌词与精灵动画时间轴。正式游戏舞台布局、素材美术规范、命中或漏按反馈仍按用户要求留待后续设计。

| 要求 | 当前证据 |
| --- | --- |
| 保留 Unity，可用于多首曲目 | mvp 工程内实现；两首不同 BPM 的示例共用一个持久化 StageSpriteBank，在构建后的程序中分别通过验证。 |
| 歌词按输入的开始、结束时间显示 | StageTrackWindow 提供分秒输入；StagePlayback 的区间边界和跳转检查通过；实机预设时间窗口内验证歌词出现与消失。 |
| Aseprite 导出精灵表后可接入 | StageSheetWindow / StageSheetImporter 支持 PNG + JSON Array/Hash，以及规则网格 PNG；44 项检查覆盖顺序、裁剪、标签、像素方向、持久化与控件绑定。使用合成测试素材验证文件格式，未要求用户先制作正式美术。 |
| 每个十六分音符一帧、默认循环 | StagePlayback 以谱面 BPM 和拍点直接求帧，122 项检查含循环、变 BPM、偏移、暂停、变速和跳转；两首实机歌曲验证换帧。 |
| 基础动画不因漏按而中断 | song1 全 Perfect、song2 全 Miss；各 6 个预设精灵与歌词验证窗口全部覆盖，mismatches=0。 |
| 暂停时画面不推进 | 两次实机验证分别记录 55 个暂停样本，均无精灵帧或歌词变化。 |
| 配置可保存、再次读取及复用 | 15 项编辑器检查验证加载、跳转、保存、重载和失败保护；44 项导入检查包含两首歌通过实际编辑器保存共享素材引用。 |
| 程序能够显示真实精灵 | 构建后跨进程读取资源成功；已查看 stage-sheet-runtime-song1/playing.png，右侧角色与帧编号正常显示。 |

最终结果：122 项时间轴、15 项编辑器、44 项精灵表检查通过；16 项反馈和 35 项核心检查通过，原有 RPE/优化验证入口也成功返回。最新构建成功，0 错误、1 项无图形批处理环境的环境光探针警告。

实机证据：

- `stage-sheet-runtime-song1/report.json`：7 Perfect，0 Miss，ok=true。
- `stage-sheet-runtime-song2-miss/report.json`：0 Perfect，7 Miss，ok=true。
- 两个目录中的 `stage-report.json`：observedWindows=requiredWindows=6，mismatches=0，pausedSamples=55，ok=true。

交付一致性：主工程与验证工程的 C# 源文件校验差异为 0；两首示例引用的素材库均已复制到主工程；用户原歌曲 5 个文件的校验差异为 0。主工程示例位于 `mvp/Assets/StageSamples`，使用说明位于根目录 `演出时间轴使用说明.md`。

验证边界：编辑器使用自动功能调用验证，尚未进行完整人工点击验收；正式素材和最终舞台美术效果不属于这次已经选定的设计。可选导入入口不构成对未来美术流程的强制约定。

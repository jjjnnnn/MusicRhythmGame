# 海豹节奏队 · MusicRhythmGame

基于现有海豹舞台、美术、动画与 Koreographer 制作的完整三键节奏关卡。Unity 6000.5.9f1。

## 开始游玩

Windows 版本：运行 `Builds/SealRhythm/SealRhythm.exe`，同目录的 Data、MonoBleedingEdge 和 DLL 文件需保留在一起。

打开 `Assets/Scenes/GamePlay.unity`，点击 Unity Play。标题页可选“正式演出”（约106秒、235个动作）或“节奏练习”（前32拍、约14秒、23个动作）。Enter 开始；四拍倒数后播放音乐。最右侧海豹由玩家操作，其余三只跟谱演示。

| 动作 | 按键 |
| --- | --- |
| 点头 | S / ↓ / 空格 |
| 左拍 | A / ← |
| 右拍 | D / → |
| 暂停、继续 | Esc / P |
| 结算或暂停时重来 | R（结算时也可 Enter） |

下方动作按钮支持鼠标与触摸，按下即响应。失去窗口焦点时自动暂停。标题页可以关闭节拍辅助，也可以按每次10ms调整输入偏移（−250至+250ms，正数用于补偿偏晚输入）。设置和正式演出个人最佳保存在本机。

## 判定与流程

完整谱面在播放前加载，使用 Koreographer 的音乐采样时钟，支持提前与延后命中。Perfect 为±55ms，Good 为±120ms；练习 Good 放宽到±180ms。每个音符只结算一次，错方向与漏按为Miss，无目标时按键记为多按并中断连击。

满分1,000,000；Perfect权重1、Good权重0.65，每次多按增加0.25个音符的评分分母。顶部准确率按已结算音符计算，结束评分按完整谱面计算。90%以上“好极了”、70%以上“合格”。练习成绩不写个人最佳。

关卡包括标题、教学说明、四拍倒数、演奏、暂停、结算、重开与返回标题。海豹动画由玩家输入驱动，同伴与队长由原有两条Koreography轨驱动；暂停同时冻结动画和音乐，重开清空判定与动画。

## 谱面编辑

源数据：`Tools/SealChart.json`。保留原开头动作，以133 BPM补齐全曲；18处三连口令的额外半拍经过音频相关性校验。点头短段为本项目改编，详细依据见 `Doc/SealChart.md`。

```powershell
python Tools/generate_seal_chart.py
python Tools/generate_seal_chart.py --check
python Tools/analyze_seal_audio.py
```

生成/校验器仅需Python标准库；音频分析另需FFmpeg、NumPy与SciPy。原有素材保持使用项目内资源。

## 验证与构建

Unity 菜单 `Tools > Seal Rhythm > Run Judge Tests` 运行10组判定测试。进入Play模式后，`Tools > Seal Rhythm > Run Full Level Smoke Test` 通过实际输入接口跑练习及整曲，验证倒数暂停、音乐暂停恢复、重开和结算，截图与报告输出到 `Logs/SealValidation`。该自动测试不会保留测试生成的最佳成绩。

`GamePlay` 已加入构建场景列表，直接用 Windows Build Profile 构建。运行时逻辑为 `RhythmGame.cs`、`SealRhythmJudge.cs` 与 `SealRhythmHud.cs`；Editor测试不会进入发行包。

2026-10-18 实测：10组判定测试通过；真实音乐播放回归中，练习23/23 Perfect、整曲235/235 Perfect、Miss=0、多按=0；倒数暂停、音乐暂停/恢复、重开与完整结算通过。记录见 `Logs/SealValidation/result.txt`，画面见同目录PNG。

Windows x64 构建成功（0错误，约125MiB），并完成独立程序启动检查，启动日志无异常。构建产生488条警告，主要来自项目原有Unity Inference依赖的ConvGeneric计算着色器变体；本关未使用推理功能。完整构建摘要与启动日志见 `Logs/SealValidation/build.txt` 和 `player.log`。

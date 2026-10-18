# 海豹节奏队 · MusicRhythmGame

使用用户提供的 Wii 日版《大家的节奏天国》SOMJ01 镜像还原海豹关卡（goma/ver0）的音乐动作时序与身体动画。Unity 6000.5.9f1。

## 开始游玩

Windows版运行 `Builds/SealRhythm/SealRhythm.exe`，同目录Data、MonoBleedingEdge和DLL需保留在一起。也可打开 `Assets/Scenes/GamePlay.unity` 后点击Unity Play。

标题页可选“开始”（完整演出，约106秒、230个判定输入）或“练习”（前32拍、约14秒、18个输入）。Enter开始，四拍倒数后进入音乐。最右侧海豹由玩家操作，其余三只演示原版动作。

| 操作 | 按键 |
| --- | --- |
| 点头 | S / ↓ / 空格 |
| 向左翻身 / 翻滚 | A / ← |
| 向右翻身 / 翻滚 | D / → |
| 暂停、继续 | Esc / P |
| 暂停或结算时重来 | R（结算时也可Enter） |

前奏5次点头由四只海豹自动示范，不计分。从第15拍开始正式输入，第一下向右。左右键在普通拍执行翻身，在滚动拍执行对应方向的翻滚；方向必须与谱面一致。下键、S与空格始终点头，不触发翻滚。滚动会切换正立/倒立状态，也会推进下一次方向；听口令完成1至10次连续滚动。

下方按钮支持鼠标与触摸，接触即响应。窗口失焦自动暂停。标题页可开关节拍提示、每次10ms调整输入偏移（−250至+250ms；正数补偿晚输入）。个人最佳使用独立的原版谱面存档键，保留旧改编谱存档而不混算。

界面使用 ImageGen 生成的奶油白纸面、冰蓝与珊瑚色图集 `Assets/Resources/UI/SealPaper/seal-ui-atlas.png`，生成提示保存在 `Tools/SealUiImagePrompt.txt`。下方操作按钮仅保留 ←、↓、→；顶部节拍条把未来3拍内的方向箭头从左右两侧向中心海豹汇合，判定飘字也显示在中心。前奏5次点头以 ↓ 示范提示，不增加判定或计分。UI截图与验证记录集中保存在 `Logs/SealValidation/UI`。

## 原版数据

230个输入从主程序TickFlow判定指令与NPC动作交叉提取：168翻身（84左/84右）、62滚动。原script48ticks/beat；与项目MP3对齐时加5拍。18处三连额外半拍再次通过音频指纹核验。每个输入在 `Tools/SealChart.json` 中都有原始tick、指令地址、调用地址、动作与动画索引。

身体使用原版BRCAD的78个组合姿势、30个动画，包含60Hz关键帧、位置/旋转/缩放/透明度与脸部挂点。原始图集复用项目现有图片。原版A键翻身、A+B滚动在本项目适配为左右键，按当前节拍决定翻身或翻滚，点头另保留下键；镜头/舞台继续固定，滚动根位移有对齐补偿；队长使用原始事件时刻映射到项目已有三种动画。判定窗口、计分规则沿用本项目，并非Wii判定器复刻。

来源与完整说明见 `Doc/SealChart.md`；提取工具与日版地址依据见 `Tools/ReverseEngineering/OriginalTiming.md`。镜像及提取出的原始二进制保存在 `Logs/SealExtraction`，不会打包进游戏或提交Git。

## 判定

音乐采样时钟驱动，支持提前与延后命中。Perfect±55ms、Good±120ms；练习Good±180ms。每个音符只结算一次，错方向与漏按为Miss，无目标按键记多按并打断连击。

满分1,000,000；Perfect权重1、Good权重0.65，每次多按增加0.25音符的评分分母。顶部准确率按已结算音符计算，结算评分按完整谱面计算。90%以上“好极了”、70%以上“合格”。练习不写入个人最佳。

## 生成与验证

```powershell
python Tools/generate_seal_chart.py
python Tools/generate_seal_chart.py --check
python Tools/analyze_seal_audio.py
```

前两条仅需Python标准库；音频分析需FFmpeg、NumPy和SciPy。解包需要cryptography，BRCAD工具的图像校验另需Pillow；Codex bundled Python已提供。

Unity菜单 `Tools > Seal Rhythm > Run Judge Tests` 验证判定；`Validate Original Animation Data` 校验原版姿势、矩阵、帧边界、230个动作/动画索引及独立Intro轨。进入Play后 `Run Full Level Smoke Test` 通过真实输入接口跑练习和整曲，并检查四只海豹最终动画/正倒立/方向；记录与截图输出到 `Logs/SealValidation`。

`GamePlay` 已加入构建场景列表，可用Windows Build Profile重新构建。Editor测试不会进入发行包。

2026-10-18按键修正验证：判定13组测试、原版78姿势/30动画/230索引/采样映射和3种错误输入恢复校验通过。实际Nod/Left/Right输入接口跑通整曲230/230 Perfect、练习18/18 Perfect，含向左翻滚31次、向右翻滚31次；下键预览确认播放点头。四只海豹最终motion=9、正立、下一次向右。倒数暂停、音乐暂停恢复、重开及结算通过。记录在 `Logs/SealValidation/Controls/result.txt`，初次原版还原记录保留在 `Logs/SealValidation/Original`。

同日Windows x64版按键修正后重新构建，构建报告0错误、0警告，并完成独立程序启动检查。构建摘要和启动日志见 `Logs/SealValidation/Controls/build.txt`、`player.log`。

ImageGen UI改版验证：纯箭头按钮的三个PointerDown入口、点头映射和节拍样本位移检查通过。最终界面整曲230/230 Perfect、练习18/18 Perfect，暂停恢复、重开、结算及四只海豹状态通过；截图和日志见 `Logs/SealValidation/UI`。Windows新版构建成功，0错误；保留项目原有Inference着色器及队长Animator转换警告，详细数量见该目录的build.txt。

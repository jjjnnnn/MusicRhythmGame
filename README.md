# 海豹节奏队 · MusicRhythmGame

使用用户提供的 Wii 日版《大家的节奏天国》SOMJ01 镜像还原海豹关卡（goma/ver0）的音乐动作时序。当前画面使用新绘制的领巾海豹与企鹅队长，保留原关卡动作与判定节拍。Unity 6000.5.9f1。

## 开始游玩

Windows版运行 `Builds/SealRhythm/SealRhythm.exe`，同目录Data、MonoBleedingEdge和DLL需保留在一起。也可打开 `Assets/Scenes/GamePlay.unity` 后点击Unity Play。

标题页可选“开始”（完整演出，约106秒、230个判定输入）或“练习”（前32拍、约14秒、18个输入）。Enter开始，四拍倒数后进入音乐。蓝色海豹由玩家操作，其余三只奶白色海豹跟随原关卡动作。

| 操作 | 按键 |
| --- | --- |
| 点头 | S / ↓ / 空格 |
| 向左翻身 / 翻滚 | A / ← |
| 向右翻身 / 翻滚 | D / → |
| 暂停、继续 | Esc / P |
| 暂停或结算时重来 | R（结算时也可Enter） |

前奏5次点头由四只海豹自动示范，不计分。从第15拍开始正式输入，第一下向右。左右键在普通拍执行翻身，在滚动拍执行对应方向的翻滚；方向必须与谱面一致。下键、S与空格始终点头，不触发翻滚。滚动会切换正立/倒立状态，也会推进下一次方向；听口令完成1至10次连续滚动。

下方按钮支持鼠标与触摸，接触即响应。窗口失焦自动暂停。标题页可开关中央节拍强调、每次10ms调整输入偏移（−250至+250ms；正数补偿晚输入）。滚动箭头始终显示，不受强调开关或旧存档设置影响。个人最佳使用独立的原版谱面存档键，保留旧改编谱存档而不混算。

界面使用 ImageGen 生成的奶油白纸面、冰蓝与珊瑚色图集 `Assets/Resources/UI/SealPaper/seal-ui-atlas.png`，生成提示保存在 `Tools/SealUiImagePrompt.txt`。下方操作按钮仅保留 ←、↓、→；顶部节拍条把未来3拍内的方向箭头从左右两侧向中心海豹汇合，判定飘字也显示在中心。前奏5次点头以 ↓ 示范提示，不增加判定或计分。UI截图与验证记录集中保存在 `Logs/SealValidation/UI`。

滚动箭头放大为44逻辑像素，中心位于节拍线，左右同时按音乐采样时间汇向中央。←为珊瑚红、↓为青绿、→为蓝色，下方三个按钮使用相同颜色；按下以缩放和变亮反馈，保留各自的方向色。

进入Play后可用 `Tools > Seal Rhythm > Run Rolling Arrow Test` 验证三色、44px尺寸、线上对齐、实际向中心移动，以及旧强调设置关闭时仍显示箭头。记录和截图位于 `Logs/SealValidation/ColoredArrows`；显示检查不自动命中，因此截图中的Miss不代表测试失败。

## 原版数据

230个输入从主程序TickFlow判定指令与NPC动作交叉提取：168翻身（84左/84右）、62滚动。原script48ticks/beat；与项目MP3对齐时加5拍。18处三连额外半拍再次通过音频指纹核验。每个输入在 `Tools/SealChart.json` 中都有原始tick、指令地址、调用地址、动作与动画索引。

原版BRCAD的78个组合姿势、30个动画及60Hz帧时钟仍保留为动作状态与验证依据。画面由新的 `SealCharacterAppearance` 绘制：奶白伙伴／蓝色玩家各有4种领巾海豹姿势，点头、侧身和翻滚沿用原动作触发与帧时长；翻滚保留正立／倒立切换。企鹅队长与海豹共享音乐采样时钟：前奏5次下键拍点头，230个正式拍点按实际输入方向向左／向右指挥，每次动作200ms；旧队长轨道保留作原版数据记录。素材在 `Assets/Resources/Characters/ArcticBand`，内置imagegen提示词在 `Tools/ArcticCharacterPrompts.txt`。原版A键翻身、A+B滚动适配为左右键，点头保留下键；判定窗口、计分规则仍使用本项目规则。

舞台背景使用内置 ImageGen 生成的简洁雪岸 `Assets/Resources/Characters/ArcticBand/background.png`，提示词保存在 `Tools/ArcticBackgroundPrompt.txt`。四只海豹中心间距为1.08世界单位，保留原先的队列顺序。企鹅右侧纸面气泡使用26号加粗中文：开场显示“开始准备，根据节拍跟上队伍的动作”，半拍和连续翻滚变奏前显示对应提示；暂停与结算隐藏。对白取自现有谱面样本时刻，不改动音乐或判定。

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

新角色验证：整曲230/230 Perfect、练习18/18 Perfect，四个原动作状态、唯一蓝色玩家、三只奶白伙伴、企鹅队长和旧素材隐藏检查通过。原音乐与谱面资源未修改，记录在 `Logs/SealValidation/ArcticBand`。自动整曲测试会临时隔离桌面实时按键，结束后恢复；正式游戏默认接受正常键盘与触摸输入。

新角色Windows版已重新构建，0错误；构建摘要与独立程序启动日志为 `Logs/SealValidation/ArcticBand/build.txt`、`player.log`。原项目Inference依赖的着色器警告保留，海豹关卡没有使用推理。

舞台与队长同步验证：整曲230/230 Perfect、练习18/18 Perfect；企鹅235次前奏／正式拍动作的样本时刻、物理方向与姿势全部核验通过。准备中文精确匹配，12.807秒记录首次翻滚变奏气泡；暂停隐藏、音乐恢复、新背景、1.08单位队列间距及唯一蓝色玩家通过。截图与完整记录见 `Logs/SealValidation/StagePolish`。

上述舞台更新已重新构建Windows x64并完成独立启动检查，0构建错误；原项目未使用字段和Inference着色器警告保留。该目录下 `build.txt`、`startup.txt`、`player.log` 保存构建与启动记录。

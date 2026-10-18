# 海豹节奏队 · MusicRhythmGame

使用用户提供的 Wii 日版《大家的节奏天国》SOMJ01 镜像还原海豹关卡（goma/ver0）的音乐动作时序。当前画面使用新绘制的领巾海豹与企鹅队长，保留原关卡动作与判定节拍。Unity 6000.5.9f1。

## 开始游玩

Windows版运行 `Builds/SealRhythm/SealRhythm.exe`，同目录Data、MonoBleedingEdge和DLL需保留在一起。也可打开 `Assets/Scenes/GamePlay.unity` 后点击Unity Play。

安卓离线单文件版为 `Builds/H5/SealRhythm.html`。只需把这个HTML文件传到手机，用支持JavaScript和Web Audio的浏览器打开，点击“开始”后播放；推荐横屏。音乐、角色、背景、节奏棒和235拍谱面全部内嵌，无需联网、服务器、安装或同目录资源文件。此版以Canvas/Web Audio复用Unity关卡数据和素材，独立于Unity WebGL运行时；前5次点头、原版左右动作与翻滚相位、中文变奏、暂停恢复和练习均保留。

背景铺满整个视口，覆盖底部按钮区域。下方只有一个带白圆 ⚪ 的大按钮，Unity占安全区宽度约92%，H5在无额外安全区边距时占视口宽度94%；白圆在Unity中为62逻辑像素。手机可点击按钮任意位置，电脑使用空格键；游戏根据按下时最近的可判节拍自动选择点头、左右翻身或翻滚。动作会适配，时机仍需玩家逐拍把握。

H5源码在 `Web`；运行 `python Tools/build_h5.py` 重新生成单HTML（需要Pillow与FFmpeg），原始图片与MP3不会被改写。`node Web/engine-tests.cjs` 验证判定和音频时钟状态机，`Web/validate-h5.cjs` 使用Playwright与本机Chrome验证生成文件。所有打包缓存仅写到日志目录，最终手机只需HTML本身。

标题页可选“开始”（完整演出，约106秒、235个计分输入）或“练习”（前32拍、约14秒、23个输入）。Enter开始，四拍倒数后进入音乐。蓝色海豹由玩家操作，其余三只奶白色海豹跟随原关卡动作。

| 操作 | 按键 |
| --- | --- |
| 跟随当前节拍做动作 | 电脑空格 / 手机唯一的 ⚪ 按钮 |
| 暂停、继续 | Esc / 界面按钮（Unity也支持P） |
| 暂停或结算时重来 | 界面“重来”（Unity也支持R，结算时支持Enter） |

前奏5次点头与后续230个翻身／翻滚都使用同一个按钮，共235个计分输入；练习仍为前32拍的23个输入。每拍需要按一下，长按不会代替后续拍点。按下时只在Good窗口内的未结算音符中选择最近的一拍，距离相同时选较早的一拍，再读取该拍的原动作、方向与翻滚相位。前奏自动适配为点头，第15拍起适配原230拍动作；点头不推进左右方向，翻滚仍切换正立／倒立状态。其余三只海豹与企鹅按音乐示范，玩家没有按下时不会自动命中。

下方按钮支持鼠标与触摸，接触即响应；电脑只用空格提交节奏输入。窗口失焦自动暂停。标题页可开关中央节拍强调、每次10ms调整输入偏移（−250至+250ms；正数补偿晚输入）。滚动节奏棒始终显示，不受强调开关或旧存档设置影响。单键版使用独立最高分：Unity为 `SealRhythm.BestScore.OriginalSOMJ01.Intro235.v4.OneButton`，H5本地存储为 `SealRhythm.SingleButton.v4.BestScore`；两个运行环境分别保存，不混用旧三键版、230拍或改编谱成绩。

界面沿用 ImageGen 生成的奶油白纸面、冰蓝与珊瑚色图集 `Assets/Resources/UI/SealPaper/seal-ui-atlas.png`，原界面提示保存在 `Tools/SealUiImagePrompt.txt`。顶部节拍条保留内置 ImageGen 生成的竖向节奏棒 `Assets/Resources/UI/SealPaper/rhythm-bar.png`，生成提示保存在 `Tools/SingleBeatImagePrompt.txt`。未来3拍的竖棒从左右两侧向中央海豹图标汇合，判定飘字显示在同一位置；不显示方向箭头。

Unity中的移动竖棒为14×48逻辑像素，中央海豹目标为48×48并保持图标比例，中心均位于节拍线上；素材保留原色，不按动作方向染色。前奏5次点头与后续230拍使用同一判定序列，每拍只显示一组左右镜像竖棒，命中即消失，避免与旧演示轨重复。唯一的青绿按钮以缩放和变亮反馈按下，图标仅保留白圆 ⚪。

进入Play后可用 `Tools > Seal Rhythm > Run Rhythm Bar Test` 验证竖棒素材、尺寸、线上对齐、原谱采样位置、实际向中心移动、单按钮，以及强调关闭时仍显示竖棒；旧菜单 `Run Rolling Arrow Test` 保留为同一测试的兼容入口。记录和截图位于 `Logs/SealValidation/RhythmBars`；显示检查不提交输入，因此截图中的Miss不代表测试失败。

## 原版数据

原版230个输入从主程序TickFlow判定指令与NPC动作交叉提取：168翻身（84左/84右）、62滚动。本项目额外将原版只作演示的5个前奏点头纳入计分，运行时合并为235拍；`Tools/SealChart.json`与原始主判定轨仍保留230个目标。原script48ticks/beat；与项目MP3对齐时加5拍。18处三连额外半拍再次通过音频指纹核验。每个原版输入在JSON中都有原始tick、指令地址、调用地址、动作与动画索引。

原版BRCAD的78个组合姿势、30个动画及60Hz帧时钟仍保留为动作状态与验证依据。画面由新的 `SealCharacterAppearance` 绘制：奶白伙伴／蓝色玩家各有4种领巾海豹姿势，点头、侧身和翻滚沿用原动作触发与帧时长；翻滚保留正立／倒立切换。企鹅队长与海豹共享音乐采样时钟：前奏5拍点头，230个正式拍点按谱面方向向左／向右指挥，每次动作200ms；旧队长轨道保留作原版数据记录。素材在 `Assets/Resources/Characters/ArcticBand`，内置imagegen提示词在 `Tools/ArcticCharacterPrompts.txt`。原版A键翻身、A+B滚动的区别保留为谱面动作数据，当前由单键命中后自动选取；判定窗口、计分规则仍使用本项目规则。

舞台背景使用内置 ImageGen 生成的简洁雪岸 `Assets/Resources/Characters/ArcticBand/background.png`，提示词保存在 `Tools/ArcticBackgroundPrompt.txt`。四只海豹中心间距为1.08世界单位，保留原先的队列顺序。企鹅右侧纸面气泡使用26号加粗中文：开场显示“开始准备，根据节拍跟上队伍的动作”，半拍和连续翻滚变奏前显示对应提示；暂停与结算隐藏。对白取自现有谱面样本时刻，不改动音乐或判定。

来源与完整说明见 `Doc/SealChart.md`；提取工具与日版地址依据见 `Tools/ReverseEngineering/OriginalTiming.md`。镜像及提取出的原始二进制保存在 `Logs/SealExtraction`，不会打包进游戏或提交Git。

## 判定

音乐采样时钟驱动，支持提前与延后命中。Perfect±55ms、Good±120ms；练习Good±180ms，窗口端点可命中。单键自动选择窗口内最近的未结算音符及动作，不会改变输入时刻；漏按或过晚记Miss，过早或无可判音符时记Stray（“按多了”）并打断连击。同一个音符只结算一次，重复按键不能重复得分；半拍相邻窗口重叠时仍按最近音符处理。

满分1,000,000；Perfect权重1、Good权重0.65，每次多按增加0.25音符的评分分母。顶部准确率按已结算音符计算，结算评分按完整谱面计算。90%以上“好极了”、70%以上“合格”。练习不写入个人最佳。

## 生成与验证

```powershell
python Tools/generate_seal_chart.py
python Tools/generate_seal_chart.py --check
python Tools/analyze_seal_audio.py
```

前两条仅需Python标准库；音频分析需FFmpeg、NumPy和SciPy。解包需要cryptography，BRCAD工具的图像校验另需Pillow；Codex bundled Python已提供。

Unity菜单 `Tools > Seal Rhythm > Run Judge Tests` 验证判定；`Validate Original Animation Data` 校验原版姿势、矩阵、帧边界、230个动作/动画索引及独立Intro轨。进入Play后 `Run Full Level Smoke Test` 通过与空格／触控相同的单键接口跑练习和整曲，并检查四只海豹最终动画/正倒立/方向；记录与截图输出到 `Logs/SealValidation`。

`GamePlay` 已加入构建场景列表，可用Windows Build Profile重新构建。Editor测试不会进入发行包。

本次单键版已通过19组Unity判定回归、JavaScript引擎单元测试，以及Unity真实音频和离线浏览器中各自的235/235整曲、23/23练习Perfect验证。Unity每次单键动作都对照原始动作编号、正倒立与下次方向核验；H5验证空格长按与触控后click不双计分、旧方向键不触发、0网络请求和0运行错误。报告和截图在 `Logs/SealValidation/SingleButton`。

## 历史验证记录

以下为单键改版前的阶段记录，三键、方向错误、三色箭头及旧最高分键仅用于回溯。前奏计分修复曾通过235/235整曲、23/23练习；更早的230拍与18拍记录属于纳入前奏计分前的结果。

2026-10-18按键修正验证：判定13组测试、原版78姿势/30动画/230索引/采样映射和3种错误输入恢复校验通过。实际Nod/Left/Right输入接口跑通整曲230/230 Perfect、练习18/18 Perfect，含向左翻滚31次、向右翻滚31次；下键预览确认播放点头。四只海豹最终motion=9、正立、下一次向右。倒数暂停、音乐暂停恢复、重开及结算通过。记录在 `Logs/SealValidation/Controls/result.txt`，初次原版还原记录保留在 `Logs/SealValidation/Original`。

同日Windows x64版按键修正后重新构建，构建报告0错误、0警告，并完成独立程序启动检查。构建摘要和启动日志见 `Logs/SealValidation/Controls/build.txt`、`player.log`。

ImageGen UI改版验证：纯箭头按钮的三个PointerDown入口、点头映射和节拍样本位移检查通过。最终界面整曲230/230 Perfect、练习18/18 Perfect，暂停恢复、重开、结算及四只海豹状态通过；截图和日志见 `Logs/SealValidation/UI`。Windows新版构建成功，0错误；保留项目原有Inference着色器及队长Animator转换警告，详细数量见该目录的build.txt。

新角色验证：整曲230/230 Perfect、练习18/18 Perfect，四个原动作状态、唯一蓝色玩家、三只奶白伙伴、企鹅队长和旧素材隐藏检查通过。原音乐与谱面资源未修改，记录在 `Logs/SealValidation/ArcticBand`。自动整曲测试会临时隔离桌面实时按键，结束后恢复；正式游戏默认接受正常键盘与触摸输入。

新角色Windows版已重新构建，0错误；构建摘要与独立程序启动日志为 `Logs/SealValidation/ArcticBand/build.txt`、`player.log`。原项目Inference依赖的着色器警告保留，海豹关卡没有使用推理。

舞台与队长同步验证：整曲230/230 Perfect、练习18/18 Perfect；企鹅235次前奏／正式拍动作的样本时刻、物理方向与姿势全部核验通过。准备中文精确匹配，12.807秒记录首次翻滚变奏气泡；暂停隐藏、音乐恢复、新背景、1.08单位队列间距及唯一蓝色玩家通过。截图与完整记录见 `Logs/SealValidation/StagePolish`。

上述舞台更新已重新构建Windows x64并完成独立启动检查，0构建错误；原项目未使用字段和Inference着色器警告保留。该目录下 `build.txt`、`startup.txt`、`player.log` 保存构建与启动记录。

此前的前奏计分与资源优化版：整曲235/235、练习23/23 Perfect，14组判定回归、原动作数据和前奏箭头无重复检查通过。旧角色骨骼图、旧动画控制器、旧UI、示例场景/音乐、无引用脚本及未用推理等包已清理；原版254个队长事件移到 `Assets/Editor/OriginalData` 供来源校验，不进入游戏包。四张现用大图使用BC7，原画像素仅作块尺寸补边；音乐Vorbis质量0.8，保留44100Hz、4687488采样及双声道。Windows构建使用LZ4HC，完整目录从148.36 MiB降至93.83 MiB，减少36.76%；构建0错误/0警告，独立启动通过。记录见 `Logs/SealValidation/Optimization`。

重新构建可用 `Tools > Seal Rhythm > Build Optimized Windows`；块尺寸打包与安全资源清理工具也保留在Editor菜单。原始来源JSON与动画状态数据继续保留，源图补边前备份仅位于不打包的验证日志目录。

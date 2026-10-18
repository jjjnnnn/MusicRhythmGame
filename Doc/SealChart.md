# 海豹关卡：原版 Wii 谱面与动作

当前关卡使用用户提供的 Wii 日版《みんなのリズム天国》镜像 SOMJ01 中的 goma/ver0 数据。动作时序由主程序 TickFlow 指令提取，身体动画由原版 BRCAD 提取，已替换早期按音频编排的改编谱。

## 原始数据与时间基准

- ROM：`G:\其他游戏\游戏本体\节奏天国.wbfs`，只读提取。
- 主程序 SHA256：`3f2f92fe0519f0e815626583dd6be4370b5b6850c346db727aa6796d65d4a8c9`。
- 海豹内部名为 `goma`，场景 ID 11；内部名 `seal` 实际是封箱关卡，不能混用。
- 原关入口 `0x8022E3EC`；原音乐开始指令 `0x8022E48C`；wave sound ID 45。
- TickFlow 为大端32位指令，48 ticks/beat；opcode=word&0x3ff，argc=(word>>10)&15，arg0=word>>14。
- 音乐网格133 BPM，44100 Hz。项目现有 Perfect Version MP3 比原背景音乐起点多5拍前奏，故 `MP3拍位 = 原脚本音乐拍位 + 5`。
- 5个开场动作逐一落在MP3第7/9/11/12/13拍；18个三连额外半拍均与独立音频指纹匹配。
- MP3实际导入4,687,488样本，106.292245秒。最后输入第226拍=101.954898秒，留4.337秒尾奏。

## 判定与动作

原版共230个真正输入：168次翻身（本项目映射84左、84右）和62次滚动。原判定指令0x200的delay参数决定目标采样时间，而非指令执行时刻。每一输入都与独立调度的NPC 0x203/0x204动作交叉验证；成功/失败/Perfect分支的输入时序与类型一致。

原版开场5次点头只有0x201演示动画，没有0x200输入，因此原始数据放入独立 `haibaoIntroEventID`。本项目现在将这5个点头在运行时与230个原版判定目标合并，整曲共235个计分输入，练习共23个；玩家需按↓、S或空格完成点头，同伴与企鹅仍按音乐采样时间示范。该轨另含第14拍的ReadyForR预备动作，共6个原始非判定事件，其中预备动作仍自动播放且不计分。`haibaoEventID`与来源JSON仍只有230个原版判定目标；`PeopleEventID`包含254个原始队长动作事件，原始参数保留在JSON。当前企鹅由同一235拍序列驱动，每拍只触发一次；旧队长轨道不再覆盖新外观动作。

节拍条直接读取合并后的判定序列，前奏不再叠加独立演示箭头，每拍只显示一组左右镜像箭头。235拍最高分使用独立存档键 `SealRhythm.BestScore.OriginalSOMJ01.Intro235.v3`，不混用旧230拍成绩。真实音频播放已通过整曲235/235、练习23/23 Perfect；既有230拍验证结果仅作历史记录。

资源清理后，旧 `PeopleEvent.asset` 移至 `Assets/Editor/OriginalData` 供生成器核对254个来源事件，运行歌曲已移除该轨引用；不再把旧队长事件打入发行包。`generate_seal_chart.py --check` 同时核验235拍适配所依据的230主判定、6个前奏原事件与这份原始队长归档。

原版A键负责翻身，A+B负责滚动。本项目保留三键操作：S/↓/空格始终点头，A/←向左，D/→向右。左右键在普通目标拍执行翻身，在滚动目标拍执行对应方向的翻滚；滚动同样严格检查方向，按反方向不能命中。点头不会替代滚动，原版230个目标及其时序保持不变。这是键盘输入适配，判定窗口与评分仍使用本项目规则。左右方向按原动画朝向，首个翻身为向右。

每一次翻身和滚动都会切换下一次方向；滚动还在正立A组与倒立B组间切换，不能在滚动后重置方向。下表为MP3拍位，所有滚动输入均来自原脚本：

同伴动作与企鹅指挥由主控按同一235拍音乐采样序列逐条派发，暂停恢复不会因事件回调边界漏动作。玩家前奏点头与后续动作均由按键触发；每次有效命中按该音符的规范状态播放，漏按或多按后的下一次正确命中会恢复到相应的原版姿势。点头不推进左右方向或正倒立状态，原230拍的动画索引与翻滚方向保持不变。

| 首次滚动拍 | 连续次数 |
| --- | --- |
| 30 | 1 |
| 45 | 2 |
| 60 | 3 |
| 75 | 4 |
| 87 | 5 |
| 103 | 6 |
| 119 | 7 |
| 135 | 8 |
| 154 | 1 |
| 161 | 2 |
| 169 | 9 |
| 186 | 1 |
| 193 | 2 |
| 201 | 10 |
| 226 | 1 |

18个三连额外半拍：25.5、41.5、57.5、73.5、85.5、97.5、117.5、133.5、145.5、151.5、159.5、167.5、183.5、191.5、199.5、213.5、217.5、221.5。

## 原版身体动画

当前可见外观已替换为新绘制的领巾海豹和企鹅队长：三只伙伴奶白色，实际玩家单独使用蓝色图集。旧版身体分片隐藏，原始 `SealOriginalAnimation` 继续运行以保持动作编号、原生帧时长、方向与正倒立状态。`SealCharacterAppearance` 读取这些状态显示点头、侧身与半周翻滚；`SealCaptainAppearance` 由主控制器在同一音乐采样帧驱动，下键拍点头、左右键拍作对应方向指挥，200ms后回待机，短于半拍间隔。队列中心间距调整为1.08世界单位。中文对白按现有半拍和连续滚动分组提前提示，原230个目标、音乐与原始口令时刻保持不变；新增计分的5个前奏点头使用其原始演示采样点。

从 `content2/cellanim/goma/ver0/cellanim.szs` 解开Yaz0和U8，再解析 `goma.brcad`，得到78个组合姿势、30个动画及ROM自带标签。`OriginalGoma/goma.json`保留分片UV、原始位置、缩放、旋转、透明度、关键帧和时长；复用项目中几何/透明度相同的512x512海豹图集。

原版60Hz离散帧、不插值、不将每个动作拉伸成一拍，正常动作结束停留末帧。动画索引与原版状态机逐项校对；TurnedTo/RolledFrom属于原版失败处理，不用作正常动作收尾。

本项目使用固定舞台、新企鹅与雪岸背景，没有复刻Wii原关的镜头/场景水平移动。原版滚动的身体帧保持不变，对根位移作固定舞台起点对齐，避免直接应用原帧造成跳跃。这一镜头适配与原版逐拍动作时序分开记录。

## 可复现提取与生成

原始二进制、完整音乐容器和第三方研究缓存只放在被Git忽略的Logs目录，不加入Assets。可维护谱面为 `Tools/SealChart.json`，每个输入保存来源地址、调用地址、原始tick、动作类型与原动画索引。提取工具只读镜像，输出到项目内指定目录。

```powershell
# WBFS读取工具需要cryptography（本机Codex bundled Python已提供）
python Tools/ReverseEngineering/extract_wii_seal.py 'G:\其他游戏\游戏本体\节奏天国.wbfs' Logs/SealExtraction/SOMJ01
python Tools/ReverseEngineering/extract_goma_timing.py Logs/SealExtraction/SOMJ01/sys/main.dol Logs/SealExtraction/SOMJ01/derived
python Tools/ReverseEngineering/import_goma_chart.py Logs/SealExtraction/SOMJ01/derived/goma_original_timing.json
python Tools/generate_seal_chart.py
python Tools/generate_seal_chart.py --check
python Tools/analyze_seal_audio.py
```

生成器检查230输入、168翻身/62滚动、严格采样顺序、原始48tick换算、方向状态、5个非判定演示、18半拍、足够尾奏与Koreographer payload索引。音频分析另需FFmpeg/NumPy/SciPy。

格式研究依据：[Dolphin DiscIO](https://github.com/dolphin-emu/dolphin/tree/master/Source/Core/DiscIO)、[rhf反编译项目](https://github.com/conhlee/rhf)。公开项目以美版为主；本次日版地址与原始时序均从用户提供的SOMJ01主程序验证，未套用美版地址或3DS的Tickompiler格式。

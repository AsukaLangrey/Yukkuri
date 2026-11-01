# 音效和音乐交接

已完成静态审查、检查源码补强，并在 2026-10-30 00:02 追加授权后补入默认小院音频接线。求助声防截断、伤声抢占、静音立即忽略旧反应、背景音乐循环与音量同步已写入源码；未编译或运行，音源质量尚未试听确认。

读取时 HEAD 为 `8238c6e6380fd605ec84cc5267f38de875f6d647`。本轮变更尚未提交，这个提交号不能代表本轮新增检查。远端须使用协调者整合后的完整源码提交。

## 本轮文件

- 修改 `src/Yukkuri.Game/Main.MilestoneAudio.cs`。
- 新增 `src/Yukkuri.Game/InteractionRoom.Audio.cs`。
- 精确修改 `src/Yukkuri.Game/InteractionRoom.cs` 的音频字段、初始化单行和旧反应播放方法。
- 精确修改 `src/Yukkuri.Game/InteractionRoom.Ui.cs` 的静音按钮单行。
- 新增 `assets-source/audio-polish-20261029.txt`。
- 新增本交接。

没有修改 InteractionPresentation.cs、音频二进制、导入参数、生成器、冻结 art03 或启动器。没有新增未接入的 InteractionAudioPolicy.cs。共享文件使用精确补丁，保留托举、panning、生理等所有其他差异。没有暂存、提交、推送、切分支、重置或清理。

## 00:02 追加授权后实际接线与选择性整合边界

新增 Audio partial 已实际接入，不依赖未提交 Physiology/Vitals 功能，可配源码 HEAD 的现有 Settings、Creature 和 InteractionPresentation 使用。

- 从 InteractionRoom.cs 移出 `_heardReactions`、`_sound`、`_muted`、`_lastSound`；混合声明行只移走音频字段，保留 `_ready/_recovered/_windowFocused` 与 `_uiClock/_autoClock/_elapsed`。删除只供旧音频方法使用的 OilExpression 别名。
- 原 `_sound=new AudioStreamPlayer{VolumeDb=-20};AddChild(_sound);` 单行替换为 `InitializeRoomAudio();`。完整删除旧 `PlayRoomReactionVoice()`，由新 partial 提供；其他方法和原 _Process 调用顺序未改。
- Ui.cs 仅将声音按钮回调中的 `_muted=!_muted;if(_muted)_sound.Stop();` 换成 `SetRoomMuted(!_muted);`，保留 ShowSettings 调用和所有其他 UI 改动。
- 新 partial 为反应播放器增加 Playing 门槛。按最终音效名确定基础优先级，选中者 +1 只排序候选。新 coo-hurt 能立即抢占低级声，同级伤声不截断彼此。未播候选不提前记已听。
- SetRoomMuted 在点击当下停止反应音并记住当前反应序号。静音或音效音量为 0 期间持续忽略反应，恢复不补旧声；音乐通过 StreamPaused 与零线性音量静音，恢复原播放位置。
- 背景音乐仅初始化时加载、复制并设置现有 sanctuary-loop 的 Forward 循环。音乐增益复用 Main 的 `LinearToDb(volume)-12`；音效以 `.7 → -20 dB` 保持原默认响度，读取现有 Settings 音量，不加滑块或改素材。
- Audio partial 自己订阅 SceneTree.ProcessFrame，只同步音频状态，即使模态暂停反应轮询仍同步静音/音量。通过 Sim 引用变化识别读档/重开，停止旧反应并忽略旧状态的声音；不改原帧逻辑、读档或重开实现。音乐不因选人、近看、设置页、读档/重开重播。
- 初始化识别 --mute 及现有两个截图检查入口，避免新增背景音乐让原静音检查起始漏声；现有检查直接写 `_muted=true` 的代码仍兼容同步入口。
- Audio partial 的 _ExitTree 解除 ProcessFrame 订阅、停止并清空两路播放器。已静态确认其他 InteractionRoom partial 没有同名 _ExitTree。

协调者选择性整合时：新增 Audio.cs 与 Main.MilestoneAudio.cs 可整文件取本轮差异；InteractionRoom.cs 只取上述字段、别名、初始化与方法删除；Ui.cs 只取声音按钮单行。本轮没有修改其他 shared-file 段落，不能将工作区整个 InteractionRoom.cs/Ui.cs 直接暂存为本轮内容。

以下两个问题的触发条件仍是修复依据，后面的“最小建议”记录的是实现规格；00:02 前未接线状态已由本节取代。

## 确定问题一：普通回应能截断求助声（已补源码，待远端）

默认可达链：project.godot → InteractionRoom.tscn → InteractionRoom._Process → PlayRoomReactionVoice。

`tools/make-m01-audio.ps1` 里 coo-help 的源长度为 1.1 秒，其余反应声为 0.62 秒。PlayRoomReactionVoice 只检查距上次开始是否超过 0.65 秒，然后直接替换同一个 `_sound.Stream`。本地 GodotSharp.xml 对 AudioStreamPlayer.Stream 的说明明确指出：设置它会停止所有正在播放的声音。

触发例：A 开始求助，B 有尚未播过的普通回应，且 B 在 0.65 秒以后仍可见、仍有有效反应和台词。下一次选择候选时 A 已在 `_heardReactions` 中，B 可以替换播放器，A 最后约 0.45 秒被截断。无需极端参数或隐藏入口。实际设备上截断声响未试听。

旧阶段已经修好“只给选中者发声”“节流前提前记已听序号”，本轮不重复修。新问题是候选优先级只用于待播候选之间，不保护当前正在播放的高优先级声音。

### 给文件负责人的最小修改

1. 保留当前可见性、过期、死亡、准备取食过滤，以及播放后才写 `_heardReactions` 的规则。
2. 选择候选后，依据最终 `InteractionPresentation.VoiceCue(c)` 得到声音基础优先级：coo-hurt=100、coo-help=80、coo-refuse=60、其余=20。不要另复制一套可能与腹痛/拒绝映射分歧的表情规则。选中者的 +1 仅用于同级候选排序，不能用来决定打断。
3. 增加一个记录当前声音基础优先级的字段。`urgent = candidatePriority == 100 && currentPriority < 100`。若 `_sound.Playing && !urgent`，本帧不播；若 `!urgent && _elapsed - _lastSound <= .65`，本帧不播。这样普通声、拒绝声及同级求助等当前声正常播完；新受伤声可以越过 0.65 秒限制，打断较低级声音；连续同级受伤不能每帧相互截断。
4. 真正播放后更新当前基础优先级、开始时间和已听序号。被截断的旧声保持已听，不从头重放；未播候选仍在有效期内才重试。静音、读档/重开停止播放器时同时重置当前优先级。
5. 不建议只把全局间隔改成 1.1 秒：这会给所有 0.5/0.62 秒短声增加无必要等待，未来素材长度变化又会失效。使用播放器实际 Playing 状态可覆盖素材变化。

上述允许伤声抢占的规则已写入新 Audio partial；没有采用“全部声音都不可打断”的替代规则。

## 确定问题二：设置页内静音再恢复会漏过“旧反馈不补播”（已补源码，待远端）

当前 `_muted` 分支会记住反应序号，但它位于 PlayRoomReactionVoice 内；_Process 仅在 `!paused` 时调用该方法。ShowSettings 打开模态界面，声音开关只切换 `_muted` 并在关闭声音时 Stop。

触发：两只油同时反应，一只正在发声，另一只尚未播；打开设置，先关声再开声，最后关闭设置。整个静音时段都在模态界面内，PlayRoomReactionVoice 没有执行，因此另一只旧反应没有被记为忽略，返回后可补播。与旧阶段约定的静音不补播语义不一致。

实际采用统一 `SetRoomMuted(bool)` 入口，进入静音的当下就 Stop 反应播放器，并把所有当前 ReactionSerial 写入 `_heardReactions`，不只依赖下一帧。解除静音只允许之后的新反应发声。设置页调用这个入口；静音中的音频同步仍忽略后续反应。InteractionRoom.Ui.cs 单行修改使用 00:02 追加权限。

补充：原默认 InteractionRoom._Ready 没有读取 `--mute`，该参数只有 Main 入口处理；新 InitializeRoomAudio 已读取，实际远端启动行为待测。

## 背景音乐和音量：当前缺口及接线边界

修补前默认小院没有背景音乐播放器，反应播放器固定 -20 dB，不读取 Settings.EffectsVolume。新 partial 已接背景音乐与现有 Settings 音量；小院仍只有声音开关，没有新增音乐/音效独立滑块。

建议接线仅复用现有素材，先不更改音源：

- 在 InteractionRoom 增加独立背景音乐播放器，在真实小院初始化后加载 sanctuary-loop；沿用 Main 中已存在的 AudioStreamWav Forward、LoopBegin=0、LoopEnd=长度乘采样率设置。只在曲目改变时设置 Stream/Play，不能在每帧、选中角色、近看、刷新设置页时重启音乐。
- 声音开关控制背景音乐与反应音两个播放器。静音当下停止反应音并标记旧反应已忽略；背景音乐可 StreamPaused 保留当前位置，恢复时继续。音量为 0 也应真正静音，恢复音效音量时不补旧声。
- 若接入独立滑块，读取/写回现有 Settings.MusicVolume 和 EffectsVolume，加载存档、重开后同步播放器。滑块调大不能绕过 `_muted`。没有滑块之前，不宣称小院已支持独立调音。
- 要保持现有默认 EffectsVolume=.7 时 -20 dB 的响度，可用 `LinearToDb(volume / .7f) - 20` 映射非零音量；0 时停止反应声，不只靠 -80 dB 近似静音。映射先做 0..1 有限数值处理。音乐的基准响度待远端听音确认，不以新常数冒充听感优化。
- 只在音乐播放器上设置循环，反应音始终一次播放。不要用全局主总线静音影响其他声音用途。
- 新 partial 已增加 _ExitTree 清理两个播放器，不改 QuitRoom 的保存和退出逻辑。

## 已落地：M01 音频检查源码

原检查只听取 sanctuary 的开头 1.2 秒，没覆盖 wild 曲或任何循环边界；音效检查在旧音乐仍以 -80 dB 混音时进行，且 Main._Process 仍可自动发求助声/触发重建。录音效果只在成功路径移除，断言或写文件失败时会跳过移除。

本轮修改：

- 用 finally 移除录音效果，然后才释放 capture；成功和失败都恢复之前的 Main._Process 开关。
- 仅在这次检查中暂停 Main._Process，排除自主求助和区域重建对录音片段的干扰；音频播放器子节点与 SceneTree 计时器继续。未改变正常游玩入口。
- 两首曲均检查实际混音，再跳到尾部前 0.25 秒，等待 0.5 秒跨过结尾，采集之后 0.6 秒，要求仍在播放且实际混音非静音。只证明循环后仍响，不证明接缝无爆音或主观流畅。
- 反应声前停止音乐并检查静音基线；六种声逐个要求有混音、1.2 秒后已停止，避免短声误循环。
- 保留原音量为零检查，增加单独音乐零音量与静音后新播放请求的检查。静音测试先停止旧音效，不声称覆盖“播到一半点静音”的 UI 行为。
- 每段采样检查录音缓冲没有溢出，保留原 samples>1000、peak<.99、非静音/静音阈值，没有降低任何断言。
- 结果 scope 明确是隔离的 M01 混音检查，不是默认 InteractionRoom 调度检查或试听验收。

只做源码与本地 GodotSharp.xml API 文本核对。未编译、运行检查或生成报告；没把这些新断言写成已经通过。

### API 与计时的静态依据

核对文件为 `.tools/godot/Godot_v4.7.2-stable_mono_win64/GodotSharp/Api/Release/GodotSharp.xml`，没有执行 DLL、引擎或编译器。

- 5594 行 `Node.SetProcess(bool)`：只影响该节点的 `_Process` 回调，不等同于 `ProcessMode.Disabled`，更没有设置 SceneTree.Paused。5601 行提供 `Node.IsProcessing()`。
- 52055 行提供 `AudioEffectCapture.GetDiscardedFrames()`，说明返回缓冲满时丢弃的样本数。比较每段前后的计数用于拒绝不完整采集。
- 58618 行提供 `AudioStreamPlayer.Seek(float)`；58576 行提供 Playing。58554 行的 Stream 说明也是“替换流会停止旧声音”的依据。
- 224155 行提供 `SceneTree.CreateTimer(...)`，说明到期发出 Timeout。现有 Main.Verification.cs:35 的 Wait 正是等待 SceneTreeTimer.Timeout，不依赖 Main._Process 累加。
- 小院新增接线另核对了同文件 224393 行 `SceneTree.ProcessFrame` 事件、219523 行 `Resource.Duplicate(bool)`、58565 行 `AudioStreamPlayer.VolumeLinear`、58586 行 `StreamPaused`；后者文档明确恢复 false 会续播。所有核对都是读取 XML。
- Main.cs:69—70 把 `_music` 和 `_sfx` 作为独立 AudioStreamPlayer 子节点加入；本轮只对 Main 调用 SetProcess(false)，未改变两个播放器或父节点的 ProcessMode，也未暂停场景树，因此不会以此关闭它们的音频播放。Main._PhysicsProcess 没有声音播放调用。

这些依据支持调用方式和预期推进关系；仍不等同于本轮编译或运行验证。`git diff --check` 只做文本空白审查，没有运行项目检查。静态名称检索确认新增音频成员与 _ExitTree 没有在其他小院 partial 重复定义。

## 远端待测（全部未运行）

按 testing/PROTOCOL.md 在获授权的 Alice/dot 独立机器执行，记录真实机器、整合后的完整源码提交、命令、证据及未测项。不要从本机运行。

1. M01 音频检查：两首曲首次播放与越过循环边界均有混音；六个一次性音效均结束；零音量和静音请求无可闻输出；断言失败时录音效果仍被移除。这个检查不覆盖默认小院。
2. 默认小院 A 求助，0.65—1.1 秒之间 B 普通回应：A 应完整结束，再根据 B 是否仍有效决定播不播。选中 B 不得以同级 +1 打断 A。
3. A 普通回应/求助期间 B 新受伤：高优先级受伤按决定的规则及时插入；同级连续受伤不反复切头；被打断旧声不重新排队。
4. 两只同时反应时打开设置，在设置中关声再开声，返回小院：旧未播回应不应补播；之后的新回应可以播放。关闭声音应立即终止当前反应音。
5. 若接入音乐：默认启动有音乐；选人、进出近看、打开设置不重启；重复两次完整循环听接缝；关闭声音同时停音效和音乐，恢复后无旧反应突发。
6. 若接入滑块：音乐/音效分别为 0、默认、1；静音时调大不得出声；存档读回/重开正确同步。新增混音需听主次、刺耳程度和重复疲劳，不能仅凭峰值判定体验合格。
7. 若统一 --mute：默认入口与旧 M01 入口均启动无声，仍需单独检查默认入口。

默认小院实际接线已补源码；素材听感改善与以上远程验证尚未完成。本轮交付不替代编译、运行和试听。

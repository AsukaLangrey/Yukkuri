# 第二轮独立静态审查

最新结论：本审查提出的 R2-1、R2-2 均已见修补并完成独立静态复核；contact 输入/Continuity 与 life 生理接线/恢复计时的最终限定差异未发现阻断，可整合。追加的废物编号续接问题和 R-ACTOR-1 暂停切近看问题也已修补并经本任务静态复读，见末节；仍不能把静态结论当成实机验收。全部未编译、未运行。具体最终快照见后续章节。

初次结论（2026-10-30 00:15，北京时间）：发现一处默认拖拽可达的输入断点问题 R2-1，建议交 contact 修补。另记录近看排泄状态的跨模块中断问题 R2-2，建议 physiology 与 art/life 协调处理。这里只写审查，尚未修改产品。作者仍在并行修改，后续按最终快照复核。

## 边界

- 基线：f02b0fcfbb3e9649b5f94d35201c4c22661158b9；审查包含其上的共享未提交功能。
- 已读根 AGENTS.md、本轮 PLAN、上一轮 review 及旧试玩交接。仅允许静态文件阅读和本 review.md 写入；未运行引擎、编译器、测试、脚本或桌面操作。
- 不继承旧试玩聊天的本机编译记录。不暂存、提交、切分支或向其他聊天发消息。

## R2-1 · P2 · 拖出操作区后重新进入，仍将跨界位移直接用于搬运

默认操作路径：空手按住小油搬运 → 不松键，将指针移入侧栏/底部非操作区域 → 在该区内移动 → 仍按住，从另一位置返回操作区。也可返回当刻松手，EndPointerGesture 会补采最后一段。

证据：

- InteractionRoom.Input.cs 的 ProcessRoomGesture 在约401行先计算 `SupportDrag(current-_lastPointer)`，并用它构造托举的 carriedTarget。
- 约409—416行的离场分支只暂停作用、清松手速度，并把 `_lastPointer=current` 保存为操作区外的坐标；没有让下一段搬运失效。
- 重新入场时直接走约420行 ContinueInteractionGesture；InteractionGesture.cs:315—316 的 Lift 会立即 MoveCreature 到输入目标，没有按帧限速或再次判断跨界。
- 约425—427行虽然检查上个指针是否在操作区，并清理松手速度，但发生在角色已经移动之后。它只能避免把这一次位移作为抛出速度，不能撤回身体跳动。

影响：从侧栏重新进入时，小油会把非操作区到当前点之间的整段移动一次吃入；可跳过正常拖拽段、移到另一只上方或不可放置的位置。返回当刻松手还可能在意外新位置放下。现有“离开操作区保持托着”的承诺未覆盖重入点，速度采样器 Reset 检查也不能证明该入口安全。

建议：先判断本次是否是重入，重设指针/接触/松手采样基准，重入这一帧不要搬运或生成推/抛力度；之后真实区内移动才正常应用。保留当前托举、高度及其他目标的状态。请 contact 检查 Lift、AdjustHanging 及其他使用相对指针的路径，避免只补速度。

有区分力的断言建议：在操作区内搬运后记录身体位置，输入一次区外样本、一次距离较大的重入样本，重入后 X/Z、高度、单手占用不变且速度为零；下一次区内小位移才按该小位移移动。重入当刻松手不能抛出或落到跨界计算的新位置。若无法脱离 Godot 测输入路径，明确保留远端 UI 检查，不以独立 CarryReleaseMotion.Reset 的通过替代。

## R2-2 · P2 · 排泄进行中进入近看，身体操作没有即时终止排泄状态

路径：普通时间等到开始撒尿/排便 → 按 Tab 近看（自然时间暂停）→ 空手抓起，或执行翻身/固定/喂食。原排泄状态直到退出近看、普通世界 Tick 再执行才清除。

证据：

- TickPhysiology 是唯一清除抓起、跌落、仰卧、进食、身体手势中排泄状态的入口；它挂在 WorldSimulation.Tick 的普通世界步，近看不执行。
- AdvanceInteractionPresentation 在近看仍推进现有动作，但当前没有相同的排泄中断归一化。Begin/ContinueInteractionGesture 的 Lift 设置 Carried，也不清 p.Excreting/Remaining。
- InteractionRoom.Ui.cs 约194—196行先判断 `i.Physiology.Active`，再判断 Falling、Carried 和装置，因此近看抓起后仍会显示“正在撒尿/正在排便”。CreatureActor.Interaction.cs 的排泄覆盖仅排除 Dead/Critical/Carried/Falling，喂食、翻身和固定时仍可能覆盖动作显示。
- 现有“excretion pauses in close-up and interruption preserves the unmet need”检查在抓起前已经切回 Management；不能覆盖上述路径。

影响：当前身体已被抓起/喂食/翻身，保存状态和界面仍表示排泄进行中；近看可以长期保持这个矛盾。这里不声称发生了重复产物：产物和计数要等普通时间，当前源码会先按中断判断清理。

建议：把“中断状态清理”与“尿意增长/排泄倒计时”分开，使近看实际身体操作也能取消正在排泄，保留 Bladder/Bowel，不推进自然时间、不产物、不加次数。不要仅把 UI 的排泄文案放到 Carried 后面掩盖状态残留。界面/动作仍应优先显示死亡、重伤、托举、落下和正在进行的身体动作。

断言建议：先正常开始排泄，再进入 CloseUp，执行 Lift/喂食/翻身中的实际入口并推进表现步；Active 应为 false，需求不减少，计数及产物不变。结束/放稳后仍在近看不应重启排泄；回到普通时间后可以重新排泄并只完成一次。保留原“近看本身暂停而不凭空取消”的断言。

## 已核对但暂不报为新缺陷

- f02b0fc 的死亡支撑清理仍在 Die、死者手势取消及世界构造入口；新增 ReleaseDraggedCreature 仍先 ValidateInteraction，未放开死者/暂停/重伤的抛出限制。
- 新抛出使用实际搬运位移的短窗均速，支持方向反转及停止后速度衰减；上述重入问题在调用层，不是均速计算公式本身。
- 默认音频初始化和退出事件清理未被共享输入/生理差异覆盖。
- 身体状态 Healthy 的 Health>60 门槛是旧试玩交接明确的产品口径，本轮不把这个阈值本身另报为缺陷；详细伤处观察仍有独立展示。
## 00:18 后续审：已经落盘的修补与检查

### contact：R2-1 的主体修补已见源码，等待最终快照

ProcessRoomGesture 现在先计算实际间隔 gestureSeconds，对 Lift/AdjustHanging/Roll 要求本帧采样及时、实际间隔不超过 .15 秒且上个指针在操作区；不满足时 relative=Zero，再构造输入目标。检查在 ContinueInteractionGesture 之前，已消除 R2-1 指出的“先移动、后清速度”。EndPointerGesture 也先将操作区外松手作为 CancelGesture 返回；区内松手先补采样再结算。失焦、切工具、模态与暂停仍走取消，保留活体托举，不套用待执行冲量。

这属于 UI 调用顺序的静态依据。现有均速、方向反转、停顿和直接模拟松手检查不能独立验证 Godot 输入事件路径；重入/区外松手/失焦仍须远程真实输入验证，不因此要求本机运行。

### life：排泄不冻结普通身体时间，新增检查有实际区分力

- 已看到 Active 分支移动至疾病、护理、底部治疗、恢复倒计时、表面干燥及装置被动作用之后；仍在自主取食、练习、普通求助和移动之前。没有重复执行装置时间或恐惧增长。
- UpdateInteractionRest 清 Resting 后排除 Active，不建立新休息请求、不获得睡眠收益、不消耗待完成休息时长；排泄结束后沿原入口继续。跟随意图不会仅因排泄而被清掉。
- 恢复期检查先自然启动排泄，再比较一秒内的变化。症状有护理且饱渴充足，恢复率为1.5/秒，故 `<symptoms-1.4` 与真实分支相符；护理、足部治疗延迟和恢复期按1秒下降，`.9` 余量合理。旧提前 continue 会让它们不下降。
- 箱中检查在排泄开始后才把 LastHelpAt 置为-100，故检查期间既能证明装置时间/恐惧继续，也能发现普通求助抢占。休息检查先记录已开始排泄时的能量和剩余休息，排泄期只消耗，完成后留足时间恢复。
- 四种急需摄入组合在排泄开始后才把饱/渴降到10、投放前方(0,.4)的一份资源。来源在感知/嘴边距离内，Box 的前方路径许可该位置，供给支撑高度由 SettleRoomSupplies 更新；没有复用上一轮超出感知半径的问题。饭+19、水+24越过15，单份来源与 Exposures==1 可发现重复摄入。检查本身未运行。
- 表现/世界入口已有生理中断同步接线，生理作者仍在收尾共享方法，最终一致性另验。不能仅因 Active 分支移位就将 R2-2 标为解决。

### 保存边界的增量检查

独立复读 Simulation.Smoke 新65行与原辅助函数，未发现断言被削弱或只检查内存却声称读档的情况。坏 JSON 确实写入隔离主档，通过 Load 走备份；Save 在坏主档下必须拒绝且主备逐字不变。缺整个对象和空对象各自覆盖默认初始化。满64×2、份数1000与 int.MaxValue 次数的回读比较为完整序列化内容，清理时关闭无限资源并比对全部非废物对象，能发现错误退款/误删。没有新增仅在读档时生效的数量限制，未把每区域上限说成全局上限。此处仍只是检查源码审查。
## life / physiology 最终接线复核：R2-2 已静态关闭

协调者确认两位作者已收尾后，重新读取最终方法、三个调用、Behavior/Rest 完整差异及 NaturalLife.Smoke 六块十三组合。**未发现阻止这些限定差异整合的静态问题。**

- 统一签名为 `private bool SynchronizeInterruptedPhysiology(Creature c)`，定义恰一处；表现步、普通世界步和 TickPhysiology 都调用同一方法。返回值只供生理步决定是否跳过开始/继续，另外两处作为取消副作用使用，签名一致且同在 WorldSimulation partial。
- 表现步同步在摄入倒计时、落地/下放等表现推进之前，普通步同步在死亡跳过和行为选择之前；默认小院先输入、再表现、再普通世界、再绘制，所以近看托举/开始饮水后不会再把旧 Active 输出到本帧 UI。同步只写 None/0 和必要活动文字；不改变尿意便意、次数、世界时钟或产物。
- 阻塞改用真正的 InteractionGestureBlocksAutonomy，不再只凭 TargetId。已拒绝或完成后不占身体的旧手势不会无限阻止排泄；有效托举、吞咽、护理完成短回应仍保持原阻塞时长。其一次性 CompletionReleased 逻辑保留。
- 生理开始分支先清 Resting，再建立排泄状态；排泄第一帧不同时声称睡眠。前一普通时间段已合法获得的休息收益没有被倒扣。
- 新近看检查中的 Lift/Drink 通过公开手势入口；Drink 先将水分设为10并保持嘴部接触 .9秒，超过实际 .65秒接受门槛，且明确断言 EatingRemaining>0 后才推进表现。仰面、重伤、死亡三组是状态组合检查，报告不将其称为完整 UI 复现。
- 普通步独立检查先 BeginIntake 后只 Advance(.25)，能够发现“表现入口修了但普通入口漏接”的问题；TickNaturalNeeds 对 EatingRemaining 的判断先于是否饥渴，故期待“小口喝水”对应真实路径，不依赖补设饥饿值。
- 原先近看本身不推进排泄的约定仍在；有身体中断才取消。未满足需求留到普通时间重新开始，未生成完成计数或排泄物。R2-2 改为“修补已静态复核，待远程运行”。

最终快照：

| 文件 | SHA256 |
| --- | --- |
| InteractionBehavior.cs | 63895D8ED4DFA43D651A61D5864661D53E607BF619DCC24B784647FE0EA8B648 |
| InteractionRest.cs | B843F6636C39EA80F0291C14B704B2173498EBEEFCA2D4088A1B06FE9587E85A |
| InteractionPhysiology.cs | 09D6C385947E81BC59D1858B9267D58D4C344C2C7E9ECDF157A516A9D32F57DF |
| checks/NaturalLife.Smoke/Program.cs | 33B78862B532CB853A227B8C2022BF608A1D947F7D93ECB252374C673E91302B |

没有运行以上检查，没有编译通过结论。contact 的输入/Continuity 最终快照仍待其收尾后复核。
## contact 最终独立复核：R2-1 已静态关闭

已读取 contact.md 收尾交接及最终 Input/Continuity 源码，独立核对 Begin → Process → End/Cancel、托举/抛出、暂停失焦、相机与自动保存。**未发现阻止这些限定差异整合的静态问题。**

- R2-1 的连续性判断先于创建目标和调用模拟层；重入/长停顿的相对位移为零，随后更新指针基准。Lift、AdjustHanging、Roll 共用该判断；下一次有效小位移恢复正常。直接抛出均速仍基于实际身体位移，断点不继承旧速度。
- EndPointerGesture 首先拒绝区外正常释放，改走 CancelGesture。区内释放对所有模拟手势补采最后一段，使用每次 Process 都更新的 `_lastGestureSampleUsec`；没有继续沿用仅托举更新时间戳的旧字段。
- 补采样仍要求 contact.Id==原 TargetId 且 CanUseContact，因此 Push 在最后一段离开身体、移到另一只或被装置遮挡时，会先经 Continue 的断触分支清 ContactActive/Motion，再 End，不能复用上一帧接触。有效接触的正常释放继续走原结算。
- Throw/PlayBall 只要实际采到离场就 Cancel，不再在重入后沿用旧瞄准冲量；Tease 保持原离场取消。取消 Throw 不撤销 Carried；死亡仍由 f02b0fc 的独立清理接管。实物食水没有被强行送进补采样的模拟手势入口，剩余容器沿原 ReleaseSupply 回原处，已入口一份不退款。
- 失焦、切对象/工具、暂停、近看/模态仍取消旧手势。中键平移与环境旋转只在按下时确定归属，不会跨过小油就变成身体接触；自动保存增加 panning 门槛，原手势、持物、旋转、模态和备份恢复门槛保留。相机跟随在直接搬运时关闭的逻辑仍在，音频初始化/清理和 Vitals 调用未被本轮输入差异移除。

### Continuity 追加检查核对

- 足伤检查覆盖92.5总健康、足部25以及79.9/80边界，并验证 Critical/Recovering/Dead 优先，不靠降低生命值来伪造足伤已正确分类。
- 悬挂与下放走实际 Hang/Lower 入口；滑动初速度 .2，三次 .1秒表现后的速度约 `.2×exp(-1.2)=.0602`，仍高于 .05 中断门槛，所以首个 .25秒世界步的“不排泄”断言有意义；之后八秒足以滑停/下放、重新开始并完成一次。Lower 原本受 Carried 限制，是保持正常行为的控制组，不算独立新缺陷。
- 近看抓起检查先验证单纯近看不会取消，再验证 Lift 后仅表现步就取消。普通步长 .25秒，恢复前 TickRemainder 保留 .05秒，回普通时间推进 .3秒只发生一次世界步，故期待重新开始的完整2.5秒计时与调用顺序一致；没有把随机计时当精确常数。
- 持续按住三组分别为水分100的饮水拒绝、Health80的护理完成、Fullness80接受饭。18次 .1秒 Continue 足以越过护理1.6秒与食物 .65秒门槛；后续12秒覆盖吞咽/完成回应和排泄时间。仍断言手势存在但不占姿态，可发现仅凭 TargetId 永久阻挡的旧逻辑。
- 原始均速/反向/直接松手检查及旧断言保留。新增四块没有假装覆盖引擎原生输入；R2-1、末段接触和区外释放还须远程真实 UI 验证。

最终快照：

| 文件 | SHA256 |
| --- | --- |
| InteractionRoom.Input.cs | F8B39D568D099AB70B7C8E5BDA1E23434A071F9C30701D469A5619116F72228B |
| InteractionRoom.cs | 947645480A4986A0B6EA67545AAB339C99B9B279282CFCB319087A353E3AA5F9 |
| CarryReleaseMotion.cs | 589033F115682BCA2253F4FD22F77B630C5074262C764078A926706287E08679 |
| checks/Interaction.Continuity/Program.cs | A2D2CA4B47586101A614FF85F8347E0FEF7884ACF0530AB59091FC77B8553D5B |

## 整体依赖与剩余范围

- InteractionState 末尾新物品枚举、InteractionPhysiology/CarryReleaseMotion 新文件、WorldSimulation 普通步调用、InteractionWorld.CanSelfMove、InteractionValidation、Vitals 和 Game 的 BuildWaste/输入分流须一并整合，不能只收检查或单个接线。Simulation 项目默认编入新 .cs 文件；Game 的排除项不包含 Vitals/Audio；三个检查项目通过 Simulation 项目引用获得新类型，未要求引擎运行依赖。
- 已读 physiology-review.md，其状态中断、存档、上限、足伤和拾取范围结论与本任务已读源码一致。只认定静态一致，不沿用旧聊天的编译记录。
- 已读 actor-review.md 的 R-ACTOR-1，并另查 ActorHopMotion.Advance 确认 !allowed 的 Reset 先于 paused 返回；该问题由协调者单独分配。此文不擅自关闭它，也不把它归咎于本轮排泄/重伤形变修补。
- 本聊天仍只写本 review.md。所有检查、真实输入、画面和音频效果均未运行，最终验证应注明独立远程机器与完整源码提交。
## 两项追加修补的总集成复读

### 自动废物编号

主协调发现的自动 Guid 续接差异已由生理作者修补，physiology-review.md 的最新结论已收齐。本任务另读最终 AddRoomWaste 和 Simulation.Smoke 追加案例：

- 新对象显式使用 `waste:个体ID:种类数值:完成次数+1`；次数先转 long 再加1，int.MaxValue 不会溢出。格式使用 Invariant，不依赖本机数字格式或当前时间，也不消耗模拟随机序列。
- 对整个 RoomObjects（包含其他区域、Stored 和非废物）收集 ID；碰撞时按1、2…取第一个空后缀。相同状态会作出相同选择，不改已有对象。计数饱和后仍能借后缀生成不同的当前对象 ID。
- 达64个同类时仍直接合并，Portions=1000时饱和，不进入新编号分支。清理后复用未再占用的编号不会引用旧废物；食水/资源记忆的白名单不包含废物。
- 新检查从同一真实存档读出左右两个世界，推进相同表现/普通步后比较完整 JSON；两种废物×普通/饱和计数，各完成两次，另放异区 Stored 宝物和软垫占据候选及:1。除了左右相等，还断言只增一个正确种类、所有 ID 唯一、原对象逐字不动，不能用“没有生成东西”假通过。64个×1000份的合并案例也确认不增/改编号、不溢出次数。
- 此增量不触及前述中断 helper/计时接线，所以 life 最终快照的交界结论仍成立；生理文件哈希由09D6…更新为下面117D…，旧快照不再代表最终交付。

### R-ACTOR-1 暂停切近看

已读 art 追加交接、ActorHopMotion、最终 RefreshInteraction 接线和新增 ActorGeometry.Smoke 检查。未发现新增静态阻断：

- AllowsView 只扩展“暂停中且前一运行帧允许跳跃”的近看视图资格，真正的 CanSelfMove、死亡/托举/下落/仰面/装置/速度/姿态门槛依旧参与 canHop。没有把 Advance 的 paused 提到所有重置之前，因此真实身体变化、未初始化和超过1.5的位置突变仍可重置。
- canHop 同时用于 Advance、朝向和末尾形变路径，避免只保存 hop 时钟却丢掉最终叠加。暂停视图变更时 _hadHopPresentation 不更新，Frame/Position/Heading 不推进，平滑器也保持原基础变换。
- 恢复后仍在近看时，close 进入 smoothHandoff；先用上一帧完整位置/缩放/旋转调用 BeginFrom，再 Reset hop、向近看基础姿态收敛。回小院后恢复则继续原冻结跳跃。
- 新检查先确认 Lift>.1、Travel介于0和1的半空状态，反复切视图时比较整个 HopFrame、位置和朝向；同时覆盖恢复交接、fresh近看、真实allowed=false及大位置跳变。ActorGeometry.Smoke 原项目已链接 ActorHopMotion 和 ActorPoseSmoothing，无新增未接入类型。
- 检查只覆盖共享资格/运动/平滑器，未实例化真实 actor；没有以此替代 Tab 事件和最终视觉变换验证。R-ACTOR-1 在本任务中标记“源码修补已静态复核，待远程运行”，替代前文的待修状态。

追加最终快照：

| 文件 | SHA256 |
| --- | --- |
| InteractionPhysiology.cs | 117D9BC4117BDFE0B8DB1FB479F026025E3C58FDCF40CA4B64DC1C761D685113 |
| checks/Simulation.Smoke/Program.cs | FE7A75407DC7BF14D11D20E60D59004041F4CDF30C093EF609ABDD532242A594 |
| CreatureActor.Interaction.cs | 7B02C454C46CA6DF78BE284D8D485DA4E402F1E1D543497ED8555A1B16BD6488 |
| ActorHopMotion.cs | B5FCA37EAD2950BBA147F97B9A049AC01772704A2C29F420F7093A43680FC30C |
| checks/ActorGeometry.Smoke/Program.cs | A088573E54A0723C8CFAA4E12224ED1A5EB4A478E97B648562DCF1A93CBDC0C8 |

本次复核时 Input、Continuity、Behavior、NaturalLife.Smoke 的哈希仍与前述最终快照相同。除专项报告可能尚在补记的历史状态文字外，当前已审限定差异没有需要继续阻止整合的源码问题。本聊天没有运行任何检查、引擎或编译，也没有暂存、提交、推送或修改他人文件。
## 最终整体整合结论

已收齐并读取 actor-review.md 的 R-ACTOR-1 修补续审，以及 physiology-review.md 的确定编号最终结论；其登记哈希与本任务复读快照一致。两项不再是待修或待专项复核状态。

**结论：本轮已审产品及配套检查差异可按分工整体整合，没有本任务确认后仍未解决的静态阻断项。** R2-1、R2-2、身体计时冻结、首帧睡眠残留、旧手势阻挡排泄、自动废物编号和暂停切近看均已见相应修补；上一轮死亡支撑及音频接线保留。必须包含相关新源文件、跨模块调用和检查源码，不能只提交局部接线。

这里只确认源码、调用顺序和检查设计的静态一致性。编译、自动检查、真实输入、画面及试听尚未执行；不得引用旧试玩编译记录替代本轮最终提交的远程验证。本聊天只修改本 review.md，没有暂存、提交或推送。
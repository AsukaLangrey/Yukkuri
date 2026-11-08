# 生理功能独立静态复核

最新静态结论：共享中断 helper、首帧 Resting、保存拒绝/回退、数量上限、足伤分类和射线尺寸的前述结论保留；追加确认的自动排泄物 GUID 续接回归，作者已限范围改为确定编号，复读后源码闭合。新增真实存档双分支、候选碰撞、次数饱和和 64 合并边界的检查源码，均未运行。

整合判断更新：可按最新生理快照 `117D9BC4...` 和检查快照 `FE7A7540...` 纳入整合；P2 不再有本次静态审查确认的剩余修补项。旧 `09D6C385...` 快照仍含该缺陷，不能沿用。合并后的编译、行为与拾取效果仍按远程待测清单处理。

本次追加任务的产品源码始终只读。初次交叉复核只写本文；协调者随后明确授权在原独占 Simulation.Smoke 添加自主废物 ID 回归检查，本次仅按该授权追加检查并更新 save.md/本文，没有修改 Physiology 或其他产品。没有运行、编译、测试、渲染、截图、桌面操作或 Git 写操作。基线 `f02b0fcfbb3e9649b5f94d35201c4c22661158b9` 加共享未提交工作。

## P1 · 原 P2 严重性 · 首次排泄残留睡眠标志（已静态收口）

修复前可达条件：角色已在地面或软垫休息，RestRemaining>0，尿意/便意在本次普通世界步达到 85。WorldSimulation.Tick 先 TickInteractionWorld，再 TickPhysiology。前者 UpdateInteractionRest 看到本次尚未 Active，正常设 Resting=true；后者开始分支设 Excreting、Remaining 和目标，初次读取版本没有清 Resting。

该版本第一次输出帧会同时有 Physiology.Active=true 与 Resting=true。UI 的 state 明确写排泄，但 mood 仍可选择“先让它安静歇一会儿”；表现更新在下一次普通步到来前也会依 Resting 设 Sleeping。后续普通步的 UpdateInteractionRest 会清 Resting，所以它是开始交界问题，不是持续获得睡眠收益或存档丢失。

life.md 已指出同一项，不重复计成新发现。最终 InteractionPhysiology 开始分支已经先设 i.Resting=false，没有倒扣已经完成世界步的休息收益，也没有取消 RestRemaining。NaturalLife.Smoke 的休息/排泄案例现已在刚看到 Active 的输出步立即要求 !i.Resting，随后检查休息剩余量保留和结束后恢复。源码上可区分旧错/新对，尚未运行。

## P2 · 原 P2 严重性 · 自动排泄物 ID 破坏同档续接确定性（已修、已静态复核，未运行）

主协调提出候选后独立核对，确认属于本轮新引入的被动模拟分歧。缺陷对应当时的 InteractionPhysiology 哈希 `09D6C385947E81BC59D1858B9267D58D4C344C2C7E9ECDF157A516A9D32F57DF`；确认候选时只写文档，后续检查改动依据追加授权，见本节末尾。

项目依据有明确范围：Simulation.Smoke 的现有 `random state and tick remainder continue deterministically after load` 直接比较原世界与读回世界相同 Advance(3) 后的完整 JSON；Milestone.Smoke 也以完整 JSON 检查 M01 同档续接。tech-proposal-v001 同时明确“保存随机状态有助于重现模拟”，并排除跨机器刚体完全确定性的承诺。本问题出在纯 C# 普通世界步，没有跨机器刚体或用户手动物理输入，属于上述模拟续接口径；但现有普通/M01 夹具没有开启 InteractionPlaytest，所以旧检查不会自动暴露本次回归。

证据链：WorldSimulation.Tick → TickInteractionWorld → TickPhysiology。在当前正在排泄且本步剩余时间归零时，TickPhysiology 调用 AddRoomWaste。少于同区域同类 64 个时，后者 `new RoomObject` 未指定 Id；InteractionState.cs 的 Id 初始化器直接调用 `Guid.NewGuid().ToString("N")`。这个随机源与保存的 WorldState.RandomState、InteractionSequence、计时和完成次数都无关。Id 是被序列化的正式对象标识，随后用于场景对象缓存和清理输入，不是可忽略的日志时间戳。

最小可达复核方案（未执行）：

1. 从一个有效互动世界保留同一份状态，普通模式且未暂停；选中角色地面站稳、没有手势/进食/惯性，Excreting=Urine、Remaining=.25、Bladder=90，TickRemainder=0，当前同区域尿液少于 64。保存这一份世界。该组合也能由正常排泄倒计时自然到达。
2. 从这同一文件独立 Load 两次，分别 new WorldSimulation，先确认推进前完整 JSON 相等。不要分别 CreateInteractionPlaytest，因为那会把初始随机 ID 差异混进案例。
3. 两分支均连续三次执行 AdvanceInteractionPresentation(.1) 后 Advance(.1)，完全相同输入与时间步。两者经过同一个 .25 秒普通世界步，都会结束排泄并新增一个尿液对象。
4. 排泄次数、需求、时间及保存随机状态可相同，但每分支各调用一次 Guid.NewGuid，新对象 Id 不由共享输入决定，通常不同，因此完整 JSON 不再相等。GUID 极小的偶然碰撞概率不构成确定性保证。直接 .25 秒普通入口也能到达这一路径，不依赖表现步随机性。

少于 64 是关键前提：达到上限时只合并既有对象，没有这次新 ID 分歧。单次 Save/Load 原样往返也仍可成功；本轮 save.md 的边界回归只检查往返与清理，不能代替“保存后继续生成”的双分支案例。该缺陷不证明现有存档不可读、吞食重复或生理数值不同，影响是完整续接/重现的对象身份不一致。

建议仅让 AddRoomWaste 的新对象 ID 来自已保存状态并确保唯一，保留存档内已有 ID；不要重构所有手动物品创建，也不要从比较中删除 Id 来让检查通过。方案须考虑完成次数已饱和、既有自定义 ID 冲突、清理后重新生成、多只个体和尿便两种；不能简单使用会饱和的次数作为唯一标识。新增回归至少包括正在排泄的同档双分支完整 JSON 比较及新对象 ID 唯一性；最终由生理负责人修复、协调者复核。

作者最终实现复核：AddRoomWaste 明确指定 Id，以持久个体 Id、种类与已完成次数组成候选；次数先转 long 再加一。全体 RoomObjects 的 Id（含异区、Stored、其他种类）用 Ordinal 集合查重，固定递增后缀选第一个空闲值。数值用 Invariant 格式，不依赖系统语言，不消耗 RandomState、不新增 schema 字段。计数饱和后候选可能相同，但现存集合包含刚生成物体，下一次必选另一个空闲后缀。删除后的旧编号允许复用；未发现默认清理路径保留废物引用，因此不在本轮新增永久历史唯一承诺。最多绕过现有 List 中有限个编号，long 后缀不受 int 完成次数饱和影响。

新增检查位于 Simulation.Smoke：Urine/Feces × 普通计数/int.MaxValue 四种组合，各连续完成两次，每次先实际 Save 到独立目录的槽位，再 Load 两次建立双分支。先断言推进前完整 JSON 相等，再按三次表现→世界 .1 秒步推进，要求完整 JSON 相等、新增一件且当前 ID 全局唯一、完成一次、所有既有物件内容不变。候选和第一个后缀故意由异区 Stored 宝物与同区软垫占用，不能靠只检查同类对象避重。第二轮在饱和计数下要求继续生成独立物件，不只断言编号字符串格式。

另两种 64 合并边界：两类各 64 个、份数均 1000、完成次数均 int.MaxValue，分别保存临近尿/便完成状态并双分支相同推进。要求完整 JSON 一致、动作完成、计数不溢出，整个物品列表与推进前逐字相同，随后 Validate 两分支。原 GUID 实现在未满 64 的双分支完整 JSON 断言处无法保证相等；这不是将旧差异从比较中排除的弱断言。所有检查仅源码，未编译或执行。

## 完整调用链核对

默认 InteractionRoom._Process：ProcessRoomGesture → AdvanceInteractionPresentation → Advance → Actor/UI。近看时 Advance 返回，但表现步继续；暂停时两者都不推进。最终方法名是 private bool SynchronizeInterruptedPhysiology(Creature c)；InteractionBehavior 的角色循环入口已调用它，普通世界循环在死亡 continue 前也调用它，TickPhysiology 读取同方法返回值。定义和三个调用一致，旧名 CancelInterruptedExcretion 已没有源码引用，不是只把 helper 定义后忘记接线。

helper 与 TickPhysiology 使用同一组中断条件：重伤/死亡、托举、跌落、受托下放、高度>.25、仰躺、横向惯性>.05、进食，以及真正阻止自主动作的当前手势。近看只执行取消，不增长尿意便意、不消耗排泄计时、不生成物品、不增加完成次数。普通生理步在重伤/死亡时同样先取消并直接跳过需求增长。

因此近看时有效托举/进食已能在本帧显示前清掉旧 Active；放下后不在近看重新开始，回普通时间再按原未满足需求重新计时。拒绝饮水、完成回应结束后的旧手势引用不再单凭 TargetId 持续阻塞；有效托举、正在吞咽和仍需短回应的完成手势保留阻塞。该结论来自 InteractionGestureBlocksAutonomy 的真实定义，包括一次性 CompletionReleased，非只读方法名称推断。

暂停分支不会额外清理状态；默认暂停下交互入口已有保护。本文不将“暂停保持现场”当作要求生理时间继续运行的理由。重伤/死亡优先于 UI 排泄状态；实际入口产生身体变化后，普通/近看上述同步负责清旧动作。

## 动作、计时和存档

开始写 Excreting 与 2.5/3.5 秒；完成先把 Remaining 降到零，再生成/合并排泄物、更新一次计数和需求，最后 Excreting=None。中断同样成对写 None/0。同步函数内部短暂赋值先后没有异步回调或保存入口插入，不会在一次公开调用返回时留下半对状态。

Physiology setter 拒绝越界、非有限数值与非法枚举；IJsonOnDeserialized 检查配对；InteractionValidation 在 SaveStore.Validate 尾部真实调用，补足内存构造非法组合的保存前拒绝。Load 的 JsonException/InvalidDataException 过滤器包含这些失败，能回退好备份；Save 对坏旧主档只受控拒绝，不替换好备份。缺 Physiology 或空对象保留默认闲置值，明确 null 拒绝。更多存储回归细节见 save.md，本文未再改检查源码。

## 排泄物上限及清理

每区域、每种非 Stored 排泄物达到 64 后合并最近同类；Portions 最大 1000，最大合法值加一是 1001，不会 int 溢出。完成次数先限制到 int.MaxValue-1 再加一，可在最大值饱和。生成端与现有保存校验的份数范围一致。此处不是全局 RoomObjects 总数上限；默认没有把排泄物收纳成 Stored 的路径。

AddRoomWaste 不删除其他物品；CleanRoomWaste 只删除当前区域、非 Stored 的尿液/粪便，默认 Del 先按 IsWaste 分流到它，未触发食水退款。达到份数上限后不再精确累计更多份数，是当前上限策略，不声称无限保留每次数量。

## 足伤分类与隐藏精确生命值

BurnFeet 每减少 1 点 FootIntegrity 只造成 .1 点 Health 伤害，故足部降到 25 时 Health 仍可能约 92.5。新增分类以 Health<=60 或 FootIntegrity<80 判 Injured，和既有检查描述及足伤显示的 80 门槛一致。Dead、Critical、Recovering 的 switch 分支仍先于普通伤势。

Vitals 只输出五类状态，精确 Health 仍存在存档且未改伤害或恢复。饱食显示 Fullness，口渴显示 100-Hydration，条与数字使用同一计算；没有把口渴方向写反。低精力、疾病、疼痛仍有其他专门提示，不要求本次类别覆盖所有不适。

## 两粒粪便与尿液射线依据

SceneArt.Ball 使用半径 .5、高度 1 的 SphereMesh，再按 size 缩放，因此 size 是全尺寸。BuildWaste 的尿液中心 (0,.014,0)、尺寸 (.60,.024,.46)，对应 RayHit 中心及半轴 (.30,.012,.23)，最高点 .026。

粪便第一粒中心 (0,.07,0)、尺寸 (.26,.14,.23)，对应半轴 (.13,.07,.115)；第二粒中心 (.08,.05,.09)、尺寸 (.17,.10,.16)，对应半轴 (.085,.05,.08)。RayHit 两者分别求交，取有效最近距离；未命中一粒时不会用其默认 0 抢占另一粒。普通物品仍走原半轴和可选 centerHeight，只把相同二次方程抽入 helper。

MakeObject 使用 BuildWaste；显示根节点位置是 (obj.X,obj.Height,obj.Z)，ObjectAt 传给 RayHit 的也是该位置。它先筛掉隐藏、异区、手持和无可见几何对象，再与角色/地景遮挡的最近距离比较，没有漏掉模型局部偏移。解析椭球仍是有限分段球网格的近似，未声称逐三角形完全相同或实际手感已目测通过。

## 远程待测

- 休息转排泄第一帧；近看抓起/喂食/受伤到重伤/死亡后首帧；回普通时间重试只完成一次且不丢需求。
- 水分已满拒绝饮水后持续按住、喂食/护理回应结束后继续按住，与有效托举/吞咽对照。
- 两种各 64 个、Portions=1000 与最大完成次数时继续排泄，保存读回仍合法且无关物品保留；清理不增加有限资源库存。
- 足部伤但 Health>60 的 Injured 显示，以及 Critical/Recovering/Dead 优先。
- 粪便两粒中心及粒间/边缘空隙、X=-.18/Z=0 的旧虚边，尿液顶面 .026，和普通球/食水拾取回归。

以上均未执行。最终报告须写 Alice/dot 实际机器、完整源码提交和未测项。

## 稳定快照登记

physiology.md 已声明停止写入，InteractionPhysiology 的本次 SHA-256 与作者声明一致。下列为本次最终静态复读时计算的文件 SHA-256，只用于确认被审源码；若继续变更，应重核差异，不能沿用本结论当作新版本验证。

| 文件（均相对项目根目录） | SHA-256 |
| --- | --- |
| src/Yukkuri.Simulation/InteractionPhysiology.cs | 117D9BC4117BDFE0B8DB1FB479F026025E3C58FDCF40CA4B64DC1C761D685113 |
| checks/Simulation.Smoke/Program.cs | FE7A75407DC7BF14D11D20E60D59004041F4CDF30C093EF609ABDD532242A594 |
| src/Yukkuri.Simulation/InteractionBehavior.cs | 63895D8ED4DFA43D651A61D5864661D53E607BF619DCC24B784647FE0EA8B648 |
| src/Yukkuri.Simulation/InteractionRest.cs | B843F6636C39EA80F0291C14B704B2173498EBEEFCA2D4088A1B06FE9587E85A |
| src/Yukkuri.Simulation/InteractionValidation.cs | EB9FF8433EEB1E4BCEF3885241332F8B5AE937EFB3FEE8CFC7A4B43BF2CA4AE1 |
| src/Yukkuri.Simulation/SaveStore.cs | 85801C5F1A8EBF1309FEE8B8645981ACE5A35B4C79EE6AEEB0EF6BDE73D78FA5 |
| src/Yukkuri.Game/InteractionRoom.Vitals.cs | 3ECB3E5067162982D42A10DA26CD4E37225243F3DD70A35EDD205A0B33B985C0 |
| src/Yukkuri.Game/InteractionPropContact.cs | 8378EDF6055E95C97F9A57DA5B4843FB17CE1954F13A141F79ED198994026DF2 |
| src/Yukkuri.Game/InteractionRoom.Ui.cs | 7F2E4916A46961159A28323BCBC98DD335228902184C4DCFF527F3B4F8DC44F0 |

文档同步提醒：复读时 physiology.md 仍保留“近看中断未解决/待 life 接线”的中间状态段落；实际源码两个入口已经统一最终方法名，life.md 与本文记录的是更新后的接线状态。整合说明不要把该历史段落误报成现存未修问题。

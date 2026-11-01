# 第二轮生理与展示交接

已按本轮 PLAN 完成归属核对。基线为 `f02b0fcfbb3e9649b5f94d35201c4c22661158b9`。本轮仅修改 InteractionPhysiology.cs、InteractionPropContact.cs；Vitals/Ui 已读，未改。原有输入、松手速度、Continuity 检查交给 contact，Behavior/World 交给 life，Validation 交给 save，Actor 交给 art。本聊天不再写这些已转交文件。

## 确认并修补

1. 底部受伤仍显示“正常”：BurnFeet 每损失 1 点 FootIntegrity 只扣 0.1 点 Health，足部降至 25、已经不能走时，Health 仍约 92.5；旧 HealthStatus 只检查 Health <= 60，因此顶部“正常”与下方伤势提示矛盾。现在把 FootIntegrity < 80 纳入受伤分类，与 InteractionWorld 的既有检查描述阈值一致。仍不显示精确生命值，不改变扣血、恢复或死亡规则；重伤、恢复中和死亡仍优先。

2. 悬空或滑动中开始排泄：旧中断条件未覆盖 Height > .25、Lowering 和尚未停下的横向惯性。现在这些阶段中断/暂缓排泄，不清空尿意便意，也不生成地面排泄物；回到地面且停稳后重新开始。使用已有休息逻辑相同的横向速度阈值 .05。

3. 排泄物挡住可见空地：旧粪便拾取是半径 .20、高 .20 的单椭球，而模型为两个更小且偏心的球；旧尿液拾取最高 .052，实际最高 .026。现在按 BuildWaste 的实际位置和半尺寸分别求交，粪便取两个球的最近命中。普通物品继续用原尺寸和算法。未改外部签名，没有新增外部依赖。

排泄边界决定：正常悬挂通过 Height > .25 暂缓；托住解下的 Lowering 全程暂缓（现有入口同时设置 Carried，明确检查 Lowering 是保留该阶段约束）。已有排泄也会中断，保留需求、下次重新计时，不续接半途姿势。地面上的 Box/Frame/Wrap 不单凭装置名称禁止排泄，不增加厕所或悬挂排泄物下落机制；是否允许蹲姿由 art 保持装置优先。高度门槛沿用现有休息判断，状态展示的足伤门槛沿用现有检查描述，均没有新增玩法阈值。

## 共享中断方法与 life 接线已完成

按协调者最新约定，稳定签名统一为 **`private bool SynchronizeInterruptedPhysiology(Creature c)`**，位于 WorldSimulation 的 InteractionPhysiology.cs partial，Behavior 同类可直接调用并忽略返回值。返回 true 表示当前身体状态阻止排泄（即使原来没有进行中的排泄），false 表示没有中断条件。旧名 CancelInterruptedExcretion 已移除，调用方必须统一新名。条件只维护在此方法中，TickPhysiology 读取其 bool，不另设包装或重复判定。同步调用只把 Excreting/Remaining 清零；若 Activity 还显示本次“正在撒尿/正在排便”，改为“排泄暂时中断”，避免近看面板残留旧活动。不减少 Bladder/Bowel、不计时、不增加次数、不生成排泄物。死亡/重伤不积累需求，其余被打断状态仍按原规则积累需求。

已与同目录 life.md 一起静态复核：life 已在 AdvanceInteractionPresentation 的角色循环取得 Interaction 后调用；普通 TickInteractionWorld 的角色循环在死亡 continue 前调用。最终源码为一处定义、三处调用（上述两处及 TickPhysiology），均使用 SynchronizeInterruptedPhysiology，无旧名调用。方法不推进自然时间，也不替代身体其他更新。Behavior 由 life 修改，本聊天未越界写入。

另已按 life 的顺序复核补上开始排泄时 `i.Resting=false`：仅清除同一输出帧的休息标志，不减少 RestRemaining、不倒扣前一个合法休息步的体力收益、不重算时间。此时动作开始，下一普通步仍由 life 的 Active 休息保护负责。

已确认长按阻塞路径并修补：ContinueInteractionGesture 在水分 >=95 时 Refuse("not-thirsty")，Refused=true 但目标指针仍保留，旧 TargetId 判断会一直阻塞；喂食/护理完成后同样会保留目标。现在只在同一目标且既有 `InteractionGestureBlocksAutonomy` 为 true 时中断，复用身体占用规则。拒绝、已经结束身体回应的完成动作、原地停手、移开手，以及操作球均不因仅有目标引用阻塞；吞咽和尚未结束的完成回应仍按既有逻辑等待。不修改 Gesture 或扩展动作规则。

## 跨文件整合状态

- **近看期间排泄中断已接线修补，未运行验证**：展示入口在吞咽倒计时更新前调用共享同步方法，抓起/喂食等身体中断不再等待普通时间才清掉 Excreting/Remaining；不减少尿意便意、不推进排泄计时。普通世界入口也先清掉旧 Active，再选择行为。近看仍不调用完整 TickPhysiology，保留自然时间暂停约定。
- life 已将 Active 自主阻断移至恢复、消化和装置被动效果更新之后，并阻止排泄期间获得休息收益。已对照 life.md 及源码确认接线；本聊天没有覆盖其改动。六个新增检查块及其十三种场景由 life 维护，均未运行。
- 排泄物每区域每种 64 个上限、饱和时合并最近一份、清理不返还食物的现有实现未改。全局存档上限由 save 负责；如果其新增上限与运行时生产冲突，需要协调者一起对齐，不能只拒绝正常产生的存档。

## 建议 contact 补充检查源码

- Health=92.5、FootIntegrity=25 时应为 Injured；足部恢复到 80 且 Health>60 时回 Healthy；Critical/Dead 优先于足部状态。
- Bladder=100，Height=.5 / 横向速度=.2 / Lowering=true 时普通时间不得开始排泄；停稳落地后能重新开始，尿意不丢失。
- 向下射线：粪便中心 (0,0) 命中；(X=-.18,Z=0) 不命中（旧单椭球会误命中）；第二粒中心 (.08,.09) 命中；尿液最高交点为 Y=.026。普通球/食水原有命中行为应保持。
- 联检 life 已新增的近看中断检查，并覆盖“排泄中近看抓起，放回地面、回普通时间重新计时”；检查源码不代表运行通过。
- 水分 >=95 的拒绝饮水长按，以及喂食咽完/护理回应结束后继续按住，不应持续阻止 Bladder>=85 开始排泄；有效托举或吞咽仍会取消排泄。用于区分旧 TargetId 判断与新身体占用判断。

## 自动排泄物编号：依据与已完成修补

已确认确定续接缺陷：AddRoomWaste 原来的 new RoomObject 未指定 Id，InteractionState.cs 的默认值会调用 Guid.NewGuid。排泄途中同一存档加载两份、以相同时间推进，虽位置、需求和次数一致，新产物编号仍会不同，因此完整序列化状态不一致。项目 Simulation.Smoke 已有比较完整 Json 的确定续接约定，不能仅比较生理字段而忽略新增对象。

本次只修 AddRoomWaste 的自动产物：以持久化的个体 Id、排泄物种类及该类累计次数加一组成候选编号；计数先转换为 long 再加一，int.MaxValue 时也不溢出。检查全体 RoomObjects 的现存编号（包含其他区域、种类及 Stored 物品），候选已占用则按固定顺序追加数值后缀直到空闲。后缀用 long，最多绕过现存集合中的有限个编号。格式使用不随系统语言变化的数值格式。相同存档和推进顺序产生相同编号，不消耗模拟随机数、不增加存档字段，不改旧物品或手动创建方式；既有 64 个上限合并分支不创建编号。

建议 save 补检查源码：排泄途中复制存档后推进并比较完整 Json；预先用其他区域/Stored 的非排泄物占用候选及第一个后缀，确认确定避重；Urinations/Defecations=int.MaxValue 时仍能生成有效且不重复的编号；达到区域同类上限时仍只合并，不删除或重命名其他物件。以上仅静态依据，未运行。

上述方案已写入 AddRoomWaste，最终格式为 `waste:{个体Id}:{种类数值}:{累计次数加一}`，冲突依次追加 `:1`、`:2` 等。次数已饱和时继续用同一基础编号并避开现存编号；已清除物品的旧编号允许重新使用，不承诺永久历史唯一。代码仅要求现存对象不重复和同一持久状态确定续接，没有增加历史计数或更改清理语义。

## 验证范围

本次稳定源码快照：InteractionPhysiology.cs 的 SHA-256 为 `117D9BC4117BDFE0B8DB1FB479F026025E3C58FDCF40CA4B64DC1C761D685113`。该值用于协调者确认文件版本，不是编译或测试结果。除非收到复核修补请求，本聊天停止写入产品源码，供 life/review/save 整合。

仅静态对照调用、伤害比例、模型尺寸和差异；没有运行项目、编译、测试、模拟、预览、截图或桌面操作。没有暂存、提交或推送。旧编译记录不覆盖本轮代码。真实拾取手感、状态面板和近看中断需在授权远程机器复核。

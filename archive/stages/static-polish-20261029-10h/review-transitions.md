# 切区与近看公共入口限定审查

结论：**本轮建议不修改这些公共入口。** 未发现默认相处小院中可通过现有界面触发的切区残留故障。公共 `Travel` 确实没有完整处理互动临时状态，但当前互动界面没有调用它；要改变托举跨区行为，必须先确定“禁止切区、带着走、还是原地放下”的契约。直接在通用方法中强制放下，会改变已明确要求保留的支撑行为，收益不足以支持此轮扩改。

这是限定范围的静态结论，不是宣称接口在所有组合下安全。

## 观察版本与范围

- 时间：北京时间 2026-10-29 10:10—10:12。
- 源码提交：`7b18eca98df4202de34e759f22f30251bc0edc96`；观察期间 `git diff --name-only` 为空。
- 只读当前源码、调用点和已有检查。唯一新增文件为本交接，按本次明确分配执行；没有修改其他审查文档、产品或检查文件，没有暂存/提交/推送。
- 未运行游戏、Godot、模拟、测试、编译、验证脚本、渲染或 computer-use。收到的旧 `d729036` 云报告不作为本提交的通过证据；本任务没有读取或转述其具体运行结果。

## 真实调用链

### 默认相处小院

`src/Yukkuri.Game/project.godot` 的主场景是 `InteractionRoom.tscn`。`InteractionRoom._Ready` 默认建立 `CreateInteractionPlaytest()`，该场景只建立 home 区域；带特定 legacy/M01 参数才切换到 Main 场景。

`InteractionRoom` 内没有 `Travel`、`EnterCloseUp`、`ExitCloseUp` 的调用。实际近看由 `InteractionRoom.Ui.cs::ToggleClose` 实现：

1. `CancelGesture()`；
2. 直接切换 `State.Mode`；
3. 更新提示及界面。

Tab、近看按钮、空手双击都进入这条路径。`CancelGesture` 会取消模拟手势、清输入追踪器和鼠标持有状态；若拿着食水，通过 `ReleaseSupply` 回到原地，再清 `_heldSupplyId`。取消 Hold 会解除 Hand，取消 Roll 会结束滚动；取消推/抛不释放其蓄积冲量。模拟 `EndInteractionGesture` 按原 TargetId 查找对象，因此清理不依赖当前选中对象。

托举 `Carried` 不被 `CancelGesture` 清除。这与近看/转镜头文字中的“托举仍保持支撑”一致；不能把它当作漏清，或因公共 API 审查顺手改成摔落/瞬移放地。

### 旧框架与 M01 页面

`Main.Ui.cs`、`Main.MilestoneUi.cs` 的区域按钮经 `Main.Travel` 调 `Sim.Travel`；`Main.CloseUp`、`Main.ExitCloseUp` 调公共近看方法。另有 `Main.MilestoneUi` 的查看求助按钮，经 `Sim.FocusHelp` 间接调 ExitCloseUp/Travel。

但 Main 正常创建的是 `CreateMilestone()` 或旧 `Create()`，不是 InteractionPlaytest。Main 源文件没有调用互动小院的 Begin/ContinueInteractionGesture、HoldSupply、ReleaseSupply、SetInteractionHand。M01 使用自己的 `_gestureActive`、`Sim.Contact` 和 `_held` 等流程，不能把两个同名 `CancelGesture` 混作同一套状态。

Main 与 InteractionRoom 正常存档目录也分开。手动把互动档复制进旧页面目录、外部构造 WorldState 等不算默认玩法现有调用链；若将来要支持这种迁移，应另设明确入口，而不是据此宣称默认区域按钮会遗留 HeldByHand。

## 当前公共入口的实际契约与可构造状态

| 入口 | 当前源码行为 | 当前可达性与判断 |
| --- | --- | --- |
| `Travel(id)` | 近看中或目标区域不存在时返回 false；成功只改区域、选中个体、玩家坐标并记日志。不结束互动手势、不归还食水、不处理 Carried、不清手感知。即使 id 与当前区域相同也执行坐标重设。 | 在非互动 Main 页面真实使用；在互动世界仅直接 API/特制状态可构造。默认小院没有切区 UI，暂不改。 |
| `EnterCloseUp()` | 要求已选中当前区个体；成功只改 Mode。 | 小院不用这个公共入口，而是先取消的 ToggleClose。公共调用者若需要取消持有物，应负责边界清理。当前没有足够证据判定这是遗漏还是低层接口设计。 |
| `ExitCloseUp()` | 只改 Mode 为 Management。 | 同上。不能仅因没有清 Carried 就判错；当前小院跨视角保留托举是明确设计。 |

具体可构造情况，均未执行：

1. **手势残留：**互动世界加第二个有效区域，Begin Hold/Roll 后直接 `Travel(second)`，不再 Continue/End。`HasInteractionGesture`、原个体 Hand/Roll 状态可以继续保留。若随后调用 Continue，`InteractionTarget` 因异区返回 null，既有路径会 `EndInteractionGesture(true)` 并清原个体；因此这是“切换后无人再继续/结束手势”的入口空档，不能描述成现有清理完全失效。
2. **食水占用：**在 A 区 `HoldSupply(a)` 后直接 Travel 到 B。A 物品仍 HeldByHand；B 的 HoldSupply 因全世界存在已持有物而拒绝。`ReleaseSupply(a)` 不要求当前区域，显式取消仍可恢复。小院实际 ToggleClose 已调用它，不存在相同近看漏还。
3. **托举支撑：**A 区 Lift 后 Travel 到 B，原个体仍在 A 且 Carried。B 再 Lift 另一只会触发全世界“手里还托着另一只”的限制。这不能用随意清 Carried 解决，因为对象有 Height、下落和支撑语义；可能应阻止切区，也可能应连同对象转移，产品契约尚未明确。
4. **手感知：**Travel 不清 Present/Until/RegionId。单次 A→B 后 `SeesHelpingHand` 同时校验当前区与手记录区，因此不会直接把 A 的手当成 B 的手。若不调用 SetInteractionHand，也不推进足够世界时间，A→B→A 后旧记录仍可能生效，且玩家坐标已被 Travel 重设为 `(0,4)`；同区 Travel 也会重设旧感知的坐标。这是公共 API 的状态一致性问题，尚无小院 UI 触发链。

## 近看时手记忆：不要与失焦修复混为一谈

小院 ToggleClose 取消接触，但不立即清三项手记忆。近看时 `UpdateInteractionHand` 调 `SetInteractionHand(...,false)`；它沿用普通收手的短暂记忆规则，只有有效期过后才清 Present。近看冻结 WorldSeconds，所以该有效期也冻结；退出后普通时间继续，或实际新操作刷新手位置。

这可能使短暂手记忆跨近看保留，但没有发现因此继续施力或重复取食的调用链。是否把“切近看”当作人立即离场，现有设计未明确；当前代码更接近“观察方式变化，人仍在旁边”。失焦则已经在 FocusExited 立即清三项，这是独立而明确的离场约束。**不建议为了统一形式，把视角切换也直接改成失焦。** 若后续需求明确视角切换必须撤销感知，再改小院的集中切换入口并补案例即可。

## 何时再修，以及最小方案

本轮不为上述 API 组合另加产品改动或运行负载。若将来新增互动小院切区按钮、脚本调用或统一公共视角入口，应在接入同一轮落实以下契约：

- 先验证切换允许且目标有效，再做清理；失败的 Travel/EnterCloseUp 不应顺便结束原操作。同区 Travel 是不操作还是重置视图，也要先确定。
- 对真正切区，最保守的小改法是：仍托着个体时拒绝切区，提示先放稳；允许切区时先用现有取消路径结束手势、归还 HeldByHand 食水、清三项手感知，再改变区域。不要擅自移动个体到新区或制造跌落。
- 界面侧也要同步清 `_mouseHeld`、`_heldSupplyId`、追踪器及提示；单改模拟的 Travel 无法处理这些 UI 状态。宜由新增切区操作统一调用房间取消流程，再执行模拟切换。
- 若要让纯模拟公共 API 独立保证这些不变量，再在模拟层补同等约束；其范围限定 InteractionPlaytest，避免改变已存在的 M01/框架行为。Carried 跨区域的扩展规则另行设计。
- Enter/ExitCloseUp 如改为统一入口，保留“取消接触但保留托举”的现有小院语义；不能把“结束手势”解释成“结束所有身体支撑”。

## 对应检查边界与待远端案例

已有 `Interaction.Continuity` 的 “changing active region during a gesture still cleans up the original oil's transient hold” 用的是直接改 ActiveRegionId，再调用 Continue，覆盖的是**下一次继续时清理原 Hold/Roll**，没有承诺 Travel 本身原子清理。`Simulation.Smoke`/`Milestone.Smoke` 的 Travel 和近看检查是旧框架/M01 状态，不能替代互动持有物组合。这个覆盖边界应如实保留，不需要把已有断言放宽。

现有版本应由独立远端按实际界面验证：Hold、Roll、手持未入口/已入口食水、托举各自切入和退出近看；接触停止，食水只退未吃份量，托举保持，不补做推抛。再测切换期间失焦：三项手感知立即清除，恢复焦点后不恢复旧手势。报告须对应完整 `7b18eca98df4202de34e759f22f30251bc0edc96` 或后续实际被测提交，不能用 d729036 报告替代。

将来实际支持互动切区时，再补上表四类 API 组合、失败切换不改变原状态、同区切换约定，以及 A→B→A 不复活旧手位置。尚未实现的跨区托举行为，不应现在以假定规则编写强制验收断言。

本项审查到此收尾：无本轮必须修正的默认玩法故障；公共接口的残留组合与设计前提已记录，供未来接入时处理。

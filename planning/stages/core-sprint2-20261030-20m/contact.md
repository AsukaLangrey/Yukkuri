# contact 第二轮交接

结论：修复两个真实界面入口的问题，并按协调者追加要求补了四个生理相关检查块。全部只做静态文件工作；未编译、运行检查、游戏、脚本或桌面操作，未暂存、提交、推送。基线 `f02b0fcfbb3e9649b5f94d35201c4c22661158b9`，相关输入和 Continuity 文件在接手时已有上一聊天的未提交功能，完整保留。

## 本轮修改

### 1. R2-1：重入操作区先移动身体，后清速度

触发：空手托起 → 持续按住移入侧栏/底部界面 → 在界面内移到另一处 → 返回操作区，或返回当刻松手。

旧 ProcessRoomGesture 先用 `current-_lastPointer` 构造托举目标，再 Continue/MoveCreature，最后才根据上一个点在界面外清松手速度。因此速度清零不能防止身体吃进跨界位移。与 review.md R2-1 的独立证据一致。

现在 Lift、AdjustHanging、Roll 在构造相对目标前先检查上一点在操作区、帧间隔有效，以及实际采样时间不超过现有 .15 秒门槛。不连续时相对位移为零；本帧末尾建立新的指针基准，下一帧才使用真实区内小位移。卡顿恢复同样不先补算身体位移再清速度。直接托举仍只按实际身体位移采样松手速度；断点的速度历史清空。

### 2. 松手用上一渲染帧，场景外也可能执行旧释放冲量

触发：选“推开”，按住短拖，最后一段移离身体或移到另一只、遮挡物上并立即松手；最后移动发生在两次 Process 之间时，旧 EndPointerGesture 不更新接触，仍可能按上一帧 ContactActive=true 推出。另一个明确路径是 Throw/PlayBall 已有瞄准位移后移到界面再松手：旧离场分支没有取消这些动作，正常 End 仍释放旧冲量。

现在场景外松手先 CancelGesture；场景内松手对普通模拟手势补采最后一段实际时间，再正常 End。原先只用于直接 Lift 的时间戳改为全部模拟手势统一维护，避免把其他动作从按下至今的总时间误当最后一段。实物容器保留原有释放流程，不把它传进要求鼠标仍按住的 ProcessHeldSupply。Throw/PlayBall 在采到离场时就取消瞄准，不能跨界回来继承旧抛/推球记录；原 Tease 离场取消保留。

取消仍使用既有 EndInteractionGesture(true)：活体托举不掉落、已发生伤害和修补进度保留；已入口食水不退款，剩余容器按既有 ReleaseSupply 放回原处。没有改消费或伤害结算，也没有把取消改成复原状态。

## 修改文件与保留范围

- `src/Yukkuri.Game/InteractionRoom.Input.cs`：本轮仅改时间戳命名/维护、EndPointerGesture 最后一段与场景外取消、相对拖拽连续性门槛、离场瞄准取消。文件内其余既有拖拽/排泄物处理/水杯显示等改动保留。
- `checks/Interaction.Continuity/Program.cs`：在旧 hidden-health 检查之后新增四个 Check 块，旧检查与断言保持。
- 本文。

`CarryReleaseMotion.cs` 的 80 毫秒实际位移均速算法、速度上限和反向/静止采样没有确定需要改动的新缺陷，本轮未改。`InteractionRoom.cs` 的拖拽相机与自动保存段落复核后未改；生理与音频接线原样保留。

## 新增检查源码（均未运行）

1. `local foot injury remains visible above the health cutoff and preserves condition priority`：Health=92.5、FootIntegrity=25 时 Injured；足部79.9/80边界；Critical、Recovering、Dead 优先。旧仅 Health 阈值会把足伤标成 Healthy。
2. `suspended lowering and sliding bodies retain unmet excretion until grounded and still`：真实 Hang/Lower 入口及横向速度 .2 的滑动，三个普通采样步后不开始排泄、不失去尿意、不产物；落稳后完成且只产生一次。悬挂高度和滑动会区分旧错/新对；Lower 本身已有 Carried 拦截，作为不回退控制组保留。
3. `close-up lifting cancels excretion without advancing or consuming its unmet need`：自然开始后进入近看，近看本身只暂停；真实 Lift 后仅推进表现步即中断，需求/时间/产物不变。放回后留在近看不重启，回普通时间从完整 2.5 秒重新开始并完成一次。旧只在 TickPhysiology 中取消会在近看保留 Active。
4. `refused and completed held tools do not indefinitely block excretion`：饮水拒绝、护理完成、喂食吞咽完成后三种持续按住，手势一直存在，最终仍能排泄一次。旧仅检查 InteractionGestureTarget 会永久阻挡。吞咽和完成反馈按原规则等待，不提前清除。

这些检查对应 physiology/life 的本轮产品修补，本任务没有抢改其实现。00:21 左右静态读取时，Behavior 两处已使用 SynchronizeInterruptedPhysiology，生理文件也存在同名方法；后续若作者再变更签名，协调者仍需检查最终一致性。

## 已静态核对的其他路径

- 失焦、切目标、切工具、暂停、近看切换和弹窗都经 CancelGesture 清两个接触采样器与松手速度，不会在恢复后重新开始旧手势；活体托举继续有支撑。
- 自动保存同时排除 HasInteractionGesture、mouseHeld、heldSupplyId、orbiting、panning、tapContact、modal 和 recovered。正在拿容器但未到嘴边、转镜头、中键平移不会被后台保存取消；取消后单纯保留 Carried 的存档仍按既有设计保存。
- 普通身体手势始终锁原 TargetId，跨另一只不会把旧速度交给新目标；新增末段采样使松手瞬间也执行这项接触匹配。实物食水沿用旧一次按住只吃一口的标记。
- 松手速度采样实际移动量，静止窗口会排掉旧速度，反向按近80毫秒均速，不按从按下起的总拖拽距离。没有擅自改成瞬时末包速度。
- 限定 Input 与 Continuity 的 git diff --check 无空白错误。它不证明语法、编译、测试或效果通过。

## 原生输入远端必测，尚无自动运行证据

此次 R2-1 与松手边界发生在 Godot 原生输入入口。Continuity 仅引用 Simulation，本轮未跨项目增加引擎依赖，也未用单独的速度 Reset 断言冒充身体不跳变验证。独立 Alice/dot 必须对最终完整提交按 testing/PROTOCOL.md 检查以下真实 UI 路径，记录机器、40位 source_commit、帧顺序和未测项：

1. 托举至普通空处，记录 X/Z/Height/Carried；区外采样后远距离重入，首帧位置与高度不变、仍托举、速度零；下一帧区内小移动仅移动这一小段。重入当刻松手应在原位置放下（若原处不可落地则保留支撑），不得跳到跨界目标或抛出。AdjustHanging 和 Roll 同样检查无跨界补位移。
2. 超过 .15 秒的采样停顿后恢复，不补算这段托举移动；直接松手不使用旧速度。正常连续慢拖、快速甩手、停满窗口、反向仍按现有规则工作。
3. Push 的最后一段移离原身体、移到另一只或被遮挡处并立即松手，不施加旧推力。有效身体接触内正常松手仍可推开。Throw/PlayBall 在场景内正常松手仍释放；移入界面取消，回来松手不补发。
4. 场景外松手/失焦/右键/暂停/切目标都不使活体掉落；已吞食一口后取消不退款、不再喂第二口，未入口的实物剩余份量回原处。近看/普通视角各覆盖一次。
5. 在自动保存时间点持续搬运、拿食水、转镜头和中键平移，操作不中断；结束后自动保存可以发生。

生理追加测试需与其最终源码一起运行 Continuity；仍需相关 Input.Intent、NaturalLife 及实机状态显示复核。没有宣称本轮检查通过。

## c04 远程失败后的最小诊断追加

协调者转来 c04：被测 `8238c6e6380fd605ec84cc5267f38de875f6d647` / v4，完成手喂后释放需求案例的 Drink/water 复合断言失败，旧信息未给出 accepted、sawSwallow、moved、终点 X 的独立结果。当前追加基于协调者所述已提交的 65f06c3，仅修改本检查与本文，不修改产品，不判断根因。

保留原 `accepted&&sawSwallow&&moved&&c.X>.5`、其他断言、240帧、每帧调用次序和所有阈值。新增只读观察量：峰值 X、距起点最远距离、累计行程、到目标最近距离、是否实际观察到目标 ID 对应的自主摄入，以及之后是否曾返回 X<=.5。后者复用原终点边界做诊断，不进入任何通过条件；到达证据要求 GroundIntake、EatingRemaining>0 且来源 ID 为 destination，而非只靠“走近了”的猜测。待入口预留是否已真正吞下另由 NaturalIntakePending、剩余份量等字段辅助区分。

只扩充该复合断言的失败 JSON：四个分量相关值、终点/峰值/行程、目标与目标坐标、需求、吞咽与自然来源、显示/反应姿态和计时、手势存在/动作/目标/占姿态/已接食，以及托举/跌落/装置/排泄/休息状态。没有调用会锁存完成释放状态的私有阻挡判定；记录公开手势状态及其身体阻挡条件供分析，不改变模拟状态。

本机仍未运行、编译或测试，未暂存提交。c04 不能直接证明当前最终代码仍有同一根因；这次追加只是让下一次远端失败具有足够证据，不声称修复产品。

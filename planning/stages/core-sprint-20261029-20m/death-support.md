# R1 死亡后释放手持支撑

结论：最小修补与两个回归检查块已落盘，未运行、未编译、未提交。阅读基线为 `8dc072d2c71c33a445d6503d1c5ac865dd444411`，本次改动尚未进入该提交。已读 review.md 的 R1、追加分工及 playtest-additions.md，没有继承旧聊天的本机运行授权。

## 实际问题与改动

正常小院允许托住重伤个体。普通时间中重伤倒计时结束会调用 Die，但旧死亡收尾没有清 Carried，所有死者互动又被统一拒绝。其余个体的 Lift/Lower 都检查单手占用，因此会一直被死者阻挡；合法的 Dead+Carried 状态保存再读回也不能解除。

- `MilestoneInteraction.cs`：新增 `NormalizeDeadInteractionSupport`，在 Die 确认死亡后调用。仅处理 Dead 且 Carried/Lowering 的个体，清 Carried、Lowering、Falling、高度和三个速度，清瞬时 Hand 限制，调用既有 ClearLandingOverlap 处理落点重叠并将目标点归于当前位置。未托举的固定装置死者不被这个收尾擅自解开。帮助目标更新、健康置零、死亡事件等原有死亡流程保持。
- `WorldSimulation.cs`：只在互动世界构造循环中新增相同归一化调用及必要花括号。旧合法 Dead+Carried 档不必重放 Die，因此不复活、不回血、不制造第二次死亡事件。**本文件另有旧聊天的 TickPhysiology 差异，已保留原样，不属于本修补，协调者不得整文件混入本轮提交。**
- `InteractionGesture.cs`：死者不能继续拥有手势姿态。死者手势结束时执行相同支撑归一化，并按取消结算，避免正常松手补出 Push/Throw 等释放冲量。活体取消逻辑不变，仍保留托举支撑。没有放宽 ValidateInteraction 的死者禁用规则。
- `checks/Input.Intent/Program.cs`：新增下面两个检查块。其余分配外文件未改。

## 新增检查源码，均未运行

1. `death during held or released lift frees support without replaying death`：用公开 Hurt 小额累计进入重伤，再 Lift。覆盖松开工具后仍托住、一直保持手势两种情况，按表现步/世界步推进至死亡。断言一次死亡、健康零、支撑与速度全部清零、不再占姿态；重复结束/再 Hurt 不多出死亡；死者不能再次 Lift；其他活体能 Lift，活体取消仍保持支撑，放稳后另一个悬挂个体能 Lower。
2. `loading a valid legacy dead carried state normalizes support without new events`：构造本版本可表达的合成 Dead+Carried 状态，经 JSON 往返和 SaveStore.Validate 后构造模拟。断言落地、占用和速度清除、健康/死亡不变，重复构造不增加事件或日志，其他活体可托举。该案例是合法状态回归，不是授权历史存档兼容验证。

旧源码在两块的 Carried/Height 与其他活体 Lift 断言上应失败；没有降低旧断言。限定四个源码文件的 `git diff --check` 未报告空白错误，仅是静态文本结果，不等于编译或检查通过。

## 待复核和远端验证

本修补采用立即归地，未新增缓降动画。沿用 ClearLandingOverlap 的三米邻域搜索；极度拥挤、邻域无合法空间时，该既有函数仍保留原水平位置，可能出现重叠。这不会重新占手或悬空，但视觉过渡、密集场景及落点选择尚未运行确认，不能宣称平滑放下或所有场景无穿插。

独立 Alice/dot 按 testing/PROTOCOL.md 对最终完整提交运行 Input.Intent、Interaction.Continuity；记录实际机器、40 位 source_commit 与未测项。实机按 review R1 的重伤→工具托举→普通时间死亡→再托举/解悬挂操作，另测死亡前保存读回再死亡、旧合法 Dead+Carried 读回、活体取消仍托稳。检查死亡后松手不重新抛出，界面和模型随新状态更新。没有启动或检查任何本机窗口。

需要协调者独立复核后提交。产品行为修补已写完；编译、自动检查和实机结果均未确认。

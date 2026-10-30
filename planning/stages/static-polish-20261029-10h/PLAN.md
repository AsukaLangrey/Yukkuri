# 10 小时静态优化与整合

2026-10-31 当前新轮：用户明确授权七小时三线开发，最新文件分工与完整11项需求见 `planning/stages/night-three-lines-20261031/PLAN.md`。本轮分工覆盖下方历史轮次；原开发机禁运行和主协调统一Git约束不变。

## 2026-10-29 23:47 起约 20 分钟加速轮（最新分工）

用户明确要求多聊天并行处理当前核心问题，约 20 分钟收口。新轮不延长原十小时窗口；各聊天先查明确可达问题再最小修补，不为消耗额度制造改动。详细分工见 `planning/stages/core-sprint-20261029-20m/PLAN.md`。本段的新分工在该短轮内优先于下面历史归属；本机禁运行、冻结包保护及统一提交规则不变。

工作窗口：北京时间 2026-10-29 09:39—19:39。各对话完成明确任务即汇报；不要求各自凑满十小时。主对话协调、整合、处理远程反馈并交付真实进展。

本机仅文件读写、静态审查与 Git/GitHub 操作。禁止游戏、测试、模拟、编译、渲染和所有变相执行入口。当前改动没有本机运行验证。不要启动现有 tools 中的检查、构建或预览脚本。

## 文件归属

- input：`src/Yukkuri.Simulation/ContactMotion.cs`、`src/Yukkuri.Game/ContactStrokeTracker.cs`、`src/Yukkuri.Game/InteractionRoom.Input.cs`，以及本目录 `input.md`。处理真实接触、急停、抽手、切目标与退出边界。
- life：`src/Yukkuri.Simulation/NaturalLife.cs`、`src/Yukkuri.Simulation/InteractionBehavior.cs`，以及本目录 `life.md`。处理自主需求、目标切换、中断/恢复、信任与求助，避免盲调数值。
- presentation：`src/Yukkuri.Simulation/InteractionPresentation.cs`、`src/Yukkuri.Simulation/RoomResourceText.cs`、`src/Yukkuri.Game/InteractionRoom.Ui.cs`，以及本目录 `presentation.md`。处理提示、声音选择、状态优先级与操作说明一致性，不在无预览条件下盲调几何。
- review：源码只读，仅可新增/更新本目录 `review.md`、`review-integration.md`。重点审查存档、结算、状态清理、各任务交界与已有测试覆盖缺口。发现问题提供具体路径、条件及建议。
- 主对话：其余共享文件、接线、检查源文件、全局文档、测试要求、Git 提交与推送。修改分工内文件前先等任务交回，避免冲突。

## 每个任务的交付

第二轮限定分工（10:17 +08）：input 对话改为负责 `src/Yukkuri.Simulation/InteractionGesture.cs`、`checks/Interaction.Continuity/Program.cs` 与 `feeding-release.md`，处理一口已咽下后仍按住食水导致自主行动被阻挡的具体问题。主对话在其交回前不改这两个源码文件。review 的切区/近看只读补充交接为 `review-transitions.md`；审查结论是不在本轮扩改未接入默认小院的公共切区行为。

记录读到的问题及具体触发条件、修改文件与理由、静态核对依据、尚未运行的远程验证案例。不能仅报告检查数量。跨文件需求只建议，不自行侵入别人负责范围。不得修改历史快照。

## 远程测试与整合

第三轮限定静态审查：基于已推送 `ee65c8041f4cfd66eb17f533690358798a13e9ec`，input 只写 `review-input-switches.md`，查持续接触中的切目标/切工具/弹窗与取消时序；life 只写 `review-resource-sharing.md`，查多只争用有限食水与来源中断后的预留/退款/暴露；presentation 只写 `review-resource-scene.md`，查可见资源、鼠标拾取、遮挡和实际取用状态一致性。均可只读相关源码，不改产品或检查，不暂存提交。区分实际接线可达路径与仅可人工构造状态，已修问题不重复列为新问题。发现具体问题后由主对话决定下一步范围。

第三轮实现归属：三份只读报告均已交回，主对话修改 `InteractionRoom.cs`、`InteractionRoom.Input.cs`、`InteractionRoom.Tools.cs` 处理五项确定的保存/显示/命中问题。review 只写 `review-scene-integration.md`，对上述工作区差异独立复核。其余对话保持完成状态，不同时修改共享源码。此次为晚上用户继续后的有限后续工作，不延长原十小时窗口。

第二轮补充分工（10:43 +08，11:00 收回）：life 对话获准修改 `InteractionBehavior.cs`、`checks/NaturalLife.Smoke/Program.cs` 和 `review-swallow.md`，修正实际自主位移后旧吞咽姿态残留；主对话的旧档缺字段检查此前已提交，不再并行编辑。input 已交回完成动作释放的三份文件，life 也已交回。review 可写 `review-completion.md` 和 `review-swallow.md`，其余源码只读。后续共享整合由主对话负责。

已有私有仓库 `AsukaLangrey/Yukkuri`，源码 `main`，报告分支 `codex/dot-test-results`。沿用 `testing/PROTOCOL.md`、`testing/request.json`、`testing/DOT-HANDOFF.md`。迁移对话已配置每小时读取新报告，不重复建立同类自动任务。远程执行方在原约定中称 dot，用户当前称 Alice；以报告中的实际机器和提交身份为准。

主对话只提交已完成并静态审查过的明确文件集合，不使用全量暂存吞入其他对话进行中的改动。各批次注明未运行本机验证。远程结果按 source_commit 对应；旧提交通过不能覆盖新修改。结果分支只读取，不合并到当前共享工作区。等待外部结果时继续独立审查，不伪造反馈。

10 小时结束交付：已整合提交、远程结果对应关系、遗留问题、尚未验证项。外部条件不足时如实报告，不把时长到点当作所有功能完成。

2026-10-30 第二轮最新分工：planning/stages/core-sprint2-20261030-20m/PLAN.md。用户明确授权统一协调既有试玩聊天，并要求继续约20分钟并行查修。文件归属以新轮为准，原机禁运行和统一提交要求不变。

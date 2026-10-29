# 10 小时静态优化与整合

工作窗口：北京时间 2026-10-29 09:39—19:39。各对话完成明确任务即汇报；不要求各自凑满十小时。主对话协调、整合、处理远程反馈并交付真实进展。

本机仅文件读写、静态审查与 Git/GitHub 操作。禁止游戏、测试、模拟、编译、渲染和所有变相执行入口。当前改动没有本机运行验证。不要启动现有 tools 中的检查、构建或预览脚本。

## 文件归属

- input：`src/Yukkuri.Simulation/ContactMotion.cs`、`src/Yukkuri.Game/ContactStrokeTracker.cs`、`src/Yukkuri.Game/InteractionRoom.Input.cs`，以及本目录 `input.md`。处理真实接触、急停、抽手、切目标与退出边界。
- life：`src/Yukkuri.Simulation/NaturalLife.cs`、`src/Yukkuri.Simulation/InteractionBehavior.cs`，以及本目录 `life.md`。处理自主需求、目标切换、中断/恢复、信任与求助，避免盲调数值。
- presentation：`src/Yukkuri.Simulation/InteractionPresentation.cs`、`src/Yukkuri.Simulation/RoomResourceText.cs`、`src/Yukkuri.Game/InteractionRoom.Ui.cs`，以及本目录 `presentation.md`。处理提示、声音选择、状态优先级与操作说明一致性，不在无预览条件下盲调几何。
- review：源码只读，仅可新增/更新本目录 `review.md`、`review-integration.md`。重点审查存档、结算、状态清理、各任务交界与已有测试覆盖缺口。发现问题提供具体路径、条件及建议。
- 主对话：其余共享文件、接线、检查源文件、全局文档、测试要求、Git 提交与推送。修改分工内文件前先等任务交回，避免冲突。

## 每个任务的交付

记录读到的问题及具体触发条件、修改文件与理由、静态核对依据、尚未运行的远程验证案例。不能仅报告检查数量。跨文件需求只建议，不自行侵入别人负责范围。不得修改历史快照。

## 远程测试与整合

已有私有仓库 `AsukaLangrey/Yukkuri`，源码 `main`，报告分支 `codex/dot-test-results`。沿用 `testing/PROTOCOL.md`、`testing/request.json`、`testing/DOT-HANDOFF.md`。迁移对话已配置每小时读取新报告，不重复建立同类自动任务。远程执行方在原约定中称 dot，用户当前称 Alice；以报告中的实际机器和提交身份为准。

主对话只提交已完成并静态审查过的明确文件集合，不使用全量暂存吞入其他对话进行中的改动。各批次注明未运行本机验证。远程结果按 source_commit 对应；旧提交通过不能覆盖新修改。结果分支只读取，不合并到当前共享工作区。等待外部结果时继续独立审查，不伪造反馈。

10 小时结束交付：已整合提交、远程结果对应关系、遗留问题、尚未验证项。外部条件不足时如实报告，不把时长到点当作所有功能完成。

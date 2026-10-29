# dot 云测试：partial

**结论：自动规则子集通过，但不是游戏或整体验收通过。** 8 项必需纯 .NET 检查合计 **475 个断言/场景通过，0 失败**；另有 **1728 组进食组合数值运动检查通过**。完整 LFS、游戏构建、历史存档兼容、真实运行和视觉交互均未验证。

- 被测提交：`d729036ccfa4773c92b2f8794f804e26fc9c02a1`
- 源码树：`a031ecb2081ec8ab814c5ee966aa89ea617a1e93`
- 开始/结束（UTC）：`2026-10-29T01:39:47Z` → `2026-10-29T01:46:57.454489+00:00`
- 发布前检查 main：`4a7acd621c2b37d201e0cf196aab63ed83a6eb04`（`2026-10-29T01:49:07.363Z`），已经变化，本报告不覆盖新的 main。
- 环境：dot 自身云 Linux / Debian 13.6 / .NET SDK 10.0.402；不是用户原开发机，也不是 Windows 锁定环境。

| 检查 | 状态 | 通过 / 失败 |
|---|---|---:|
| lfs-integrity | blocked | 未执行 |
| game-build | blocked | 未执行 |
| Simulation.Smoke | passed | 33 / 0 |
| Milestone.Smoke | passed | 63 / 0 |
| Interaction.Smoke | passed | 48 / 0 |
| Input.Intent | passed | 33 / 0 |
| NaturalLife.Smoke | passed | 79 / 0 |
| Interaction.Continuity | passed | 104 / 0 |
| ActorGeometry.Smoke | passed | 91 / 0 |
| NaturalLife.Soak | passed | 24 / 0 |
| Save.Compatibility | blocked | 未执行 |
| runtime-smoke | blocked | 未执行 |
| visual-interaction-review | blocked | 未执行 |
| ActorGeometry.IntakeCombinations | passed | 1728 / 0 |

## 自动检查覆盖与证据

实际命令、退出码、UTC 时间、种子、恢复步骤和证据 SHA256 见 [report.json](report.json)。所有必需自动项 exit=0；有 JSON 的项同时核验 success/failures 和结果数组。

- Soak：6 场景 × 4 固定种子（72341、592813、1597331、20261029）=24 组；每组 3600 **模拟秒**，不等于运行游戏一小时。
- 附加组合检查：1728 组；30/60/144fps 和非均匀时间步只是共享几何与姿态平滑的数值采样，**不是原生渲染、模型观感或鼠标手感验收**。
- 108 个物化源码 blob 均与固定提交树、Git blob hash、SHA256 一致，运行后未改动。没有完整 checkout，故 `source_worktree_clean=null`。
- 大型 Soak/组合 JSON 为明确标记的派生摘要，保留全局结果、覆盖范围、原始 SHA256/字节数；Soak 保留所有场景结果和指标。完整原始 JSON 仅留当前云工作区。

## 失败复现与恢复

本轮已执行的产品检查没有失败。首次准备阶段的 restore 因 SDK/NuGet 在只读默认运行目录创建互斥锁失败，**产品检查尚未执行**。证据保留于 `evidence/bootstrap-attempt-1/`。仅把 `XDG_RUNTIME_DIR` 等指向任务可写目录后，一次恢复执行通过；没有修改产品源码、断言或锁定配置。

复现本轮自动项：使用固定提交依赖闭包、官方 Linux .NET 10.0.402 与可写运行目录；先按 `report.json` 的 restore 命令使用报告内空源 NuGet 配置，再执行逐项实际命令。`evidence/run_checks.py.txt` 保留实际核心执行器。

## 未测项与下一步

1. **LFS / 游戏构建 blocked**：私有 Git HTTPS 无认证；只有 connector 物化的纯 .NET 依赖闭包。锁定的是 Windows x64 + Godot 4.7.2 .NET；现有 Godot 4.6.3 只读取版本，未用于项目。
2. **Save.Compatibility blocked**：仓库未提供获准历史样本；未伪造样本，也未提交私人存档。同版本临时存档测试不替代历史兼容。
3. **运行 / 视觉交互 blocked**：未启动游戏，无本轮显示观察或桌面操作。跳跃、腾空落地、模型/饰品穿插、接触撤离急停、递食饮水、自主觅食及信任差异都没有目视验收。

下一步是补齐受支持的完整远程 Git/LFS 与锁定工具链、获准历史样本；有显示和明确观察权限后再进行运行/视觉检查。环境变化后新建 run_id 复试。本报告保留，不能当作所有必需项全绿。

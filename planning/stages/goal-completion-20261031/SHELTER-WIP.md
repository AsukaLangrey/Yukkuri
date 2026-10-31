# 居所未完成草稿，按用户要求停止

2026-10-31 08:35 左右，用户要求“收个尾”。所有开发与守护停止扩展，住居草稿独立保留，**不是可编译或可测试交付**。供 Alice 测试的完整批次仍为 `5b0015e11271815f3b639bd15f0cdc4f78a6cb62`（v53，有限食水供给器）。本草稿不得混入该源码包，不把未完成的住居或教学计入完成项。

草稿分支：`codex/wip-shelter-20261031`，基于上述 v53。主分支恢复到完整批次后只补收尾说明。已有 AGENTS.md 改动与本地候选素材均不属于本草稿，保留原样。

## 已保留的工作

- Simulation 新增 6 文件：ShelterState.cs、ShelterGeometry.cs、ShelterBodyEnvelope.cs、ShelterSpatial.cs、Shelters.cs、ShelterLife.cs。包含拟定状态、门洞/墙段几何、部分购买/操作与自主行为代码。
- Game 新增 ShelterActor.cs、InteractionRoom.Shelters.cs、CreatureActor.Shelter.cs、InteractionRoom.SheltersUi.cs；原房间输入、生命周期、器具选择、油动作、用品、观察、求助与调试字段有接线草稿。
- 本目录计划/主协调记录及 testing/goal-completion-20261031.md 中的居所情境是待实现/待验证要求，不能用作完成证据。

## 尚未接通，不能直接合入

1. 动作和目标枚举、Execute/Begin 分派尚未追加，Game 已引用这些名称。
2. 世界更新、自主行为优先级、实际移动碰撞、视线、物件和球的几何接线尚未完成。
3. 居所求助种类与现有求助入口、保存校验和旧档归一化尚未接好。
4. prototype-common 包络需按帽/结最大值处理；只读状态应补呼唤拒绝原因。
5. 屋顶与真实托举/抛掷、占用释放、死亡/换区/出生/失能/照护等中断边界未收口。进入/退出停止普通跳跃的 Game 改动也尚未完成整合复核。
6. 没有居所检查源码；本机没有编译、运行、模拟、渲染或预览。

后续若用户重新安排，只能从本草稿继续整合并保留禁跑约束，不把它当已完成版本启动测试。扩展自愿教学尚未开始。

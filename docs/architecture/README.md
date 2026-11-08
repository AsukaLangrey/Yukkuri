# 技术架构

[Godot 工程](../../game/src/Yukkuri.Game/project.godot) 的主场景是 `InteractionRoom.tscn`。游戏端负责场景、表现、输入和 Godot 集成，`game/src/Yukkuri.Simulation` 保存独立的 C# 模拟逻辑，`game/checks` 保存可直接构建的检查项目。项目从 [共享资产清单](../../assets/manifests/runtime-assets.json) 接入运行素材。运行配置和数据位于 game 内。

[个体状态实现审计](design-audit/20261103-个体状态设计与实现审计.txt) 和 [引擎来源](engine-provenance/README.txt) 保留原记录及原机器路径。当前使用 [锁定工具链](../../game/toolchain.lock.json)。

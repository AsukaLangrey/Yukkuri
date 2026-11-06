# 门外入住预约不再压住优先需要

结论：本次窄修复通过专用规则检查。同一最终测试程序在干净基线为15/29通过，修复后29/29通过。原有连续性检查仍为128/129，基线与修复的失败完全一致。因此总状态为 failed，不能称全项目通过。

## 来源和范围

- 基线：main `e8d96a78f49182eb17e269e2b62a738c7a9bb7ea`。协调者提供的独立工作区；改动前复制源码快照。235份源码/项目文件逐项哈希比对，只改变 `src/Yukkuri.Simulation/ShelterLife.cs`。
- 分支：`alice/20261106-shelter-entry-priority`。本报告对应基线加补丁，并非新的已提交提交号。本工作者未操作 Git 索引、分支、提交或推送。
- 环境：获授权本地 Windows / .NET 10 独立规则测试，无游戏界面。Release 显式 Rebuild 后直接运行新 DLL，不用旧产物。禁止首次体验和开发证书生成；未更改整机设置。
- 设计依据：新区域 `03-技术与项目保障/项目管理/历史资料/planning/stages/shelter-completion-20261031/DELIVERY.md` 中“受惊且没有更紧急事务”“先到门前再经过门洞进入”“紧急需要要求出屋”。只修既有调度，不扩展多住户、夜间规则或新优先项。

## 问题和修复

原代码仅在 Approaching 时取消优先事务冲突的预约。身体到达 Entering 阶段、但尚未重叠屋内且门前被球挡住时，TryActiveShelter 提前返回，食水需要得不到调度。

SynchronizeShelters 与 TryActiveShelter 都增加同一窄条件：Entering 且身体尚未进入内部重叠范围时，既有 ShelterNeedsPriority 为真即释放预约。释放只改预约，不改坐标。身体已经重叠内部时仍走原来的真实出口路径，不能瞬移或提前空出容量。

所有阈值保持：正常饥饿<70、病中饥饿<55、Hydration<70、Pain>=40、Energy<22。家庭事项仅沿用既有 CompanionTargetId 等判断，没有新增“生病就取消”的规则。直接测试覆盖同步后才出现优先需要时的 TryActiveShelter 防线。

## 可复现实验与边界

固定种子20261031。公开 API 买屋并放在(0,0)，个体自然走至 Entering，身体 z=2.0199999713897707、半径0.7799999713897705，未重叠内部。买球、GiveBall 后用 MoveLooseObject 放在门口。食物/水通过 PlaceInteractionSupply 放在(0,4)。优先阈值、初始身体位置和无关计时器为合成夹具，不冒充玩家操作或自然长期饥饿。

- 主证据使用球 z=.9：身体与球初始间距1.12，大于身体半径+.32球半径，且下一步进门被挡。无优先需要对照30秒仍停在 Entering；存在饥饿/口渴时，基线120秒仍不释放。修复后实际走开并摄取公开放置的食水，饱食最高28.931、Hydration最高33.931（初始均10）。逐步检查移动不超过.25，优先需要尚在时不重入。
- 保留原 z=1 复现：公开放球接口允许比居所运动判定更小的间隙，导致轻微初始身体/球重叠。修复后预约释放、行为进入觅食/找水，但此球位置仍可能阻挡移动。未修碰撞，也不把恢复调度称为此场景已成功摄取。
- 精确内部边界外侧+.001、刚接触0、重叠-.001：前两者释放，后者保留住户并要求真实退出。OverlapsInterior 仍使用严格小于；已有屋内及部分重叠身体都经过逐步真实出门。
- 同时覆盖无遮挡 Entering、Approaching、Inside 对照，暂停、近看、JSON合法状态往返、反复步进、痛苦/低体力/家庭事项，以及正常/病中食水阈值边界。
- 每个夹具使用 SaveStore.Validate。JSON仅为内存生成测试状态，未访问用户存档。

## 实际结果

- 新增 Interaction.ShelterEntryPriority：基线15通过/14失败；修复29通过/0失败。两边最终 Program.cs 的SHA256完全相同。
- 原 Interaction.Shelters：13/13。
- 原 Interaction.AutonomyPriority：12/12。
- 原 Interaction.CompletedNeeds：1/1。
- 原 Interaction.Continuity：基线与修复均128/129。相同失败：`suspended lowering and sliding bodies retain unmet excretion until grounded and still: settling did not resume and finish exactly one excretion after suspended`。未弱化断言。原套件1440次混合尝试、653次接受、12固定种子，仍不代表完整玩法覆盖。
- 所有列出的构建exit=0；专用基线和两次Continuity运行exit=1，其余exit=0。既有两处CS8604编译警告保留。

运行方式（分别从基线快照和修复工程执行）：`dotnet build checks/Interaction.ShelterEntryPriority -t:Rebuild -c Release --nologo`，随后 `dotnet checks/Interaction.ShelterEntryPriority/bin/Release/net10.0/Interaction.ShelterEntryPriority.dll`。原套件替换相应项目名。实际使用预装工具，不下载、不安装工具。

## 证据、哈希和未测

原始日志留在 `.local/testing/shelter-entry-priority-20261106/`；最终配对为 `baseline-build-final.txt`、`baseline-run-final.txt`、`fixed-build-final.txt`、`fixed-run-final.txt`。其余套件、基线连续性和早期失败日志保留，不覆盖。逐份日志哈希、235文件快照清单哈希、规范/请求哈希见同名JSON报告。原始日志不入提交。

- 原 ShelterLife.cs：`5220be7cf08302f842806662aaebb9b8c3117675a72760629e4772ec415c73c9`
- 修复 ShelterLife.cs：`81e4b0137b4bcf1ac0648424941ad15d9eed92d74a45065fc2fbb2b85c6c7263`
- 最终 Program.cs：`a7f791d805052b7f3e03b69ae8e07e9cbaf8d0c272a25d31f145edf8c658330f`
- 新项目文件：`af16fc5ffcedb208f8352fa7824eb96d8f6720d3bef6cc03980ad9752855ca8a`

早期测试错误保留：第一版屋内断言只推进.05秒，未到.25秒世界tick；已修正完整tick后，对照基线与修复均通过。原z=1夹具要求实际移动/摄取过宽，诊断确认仍有独立碰撞限制，最终将其调度验证与z=.9非交叠实际摄取验证分开。不是修改产品碰撞让检查通过。

未测：原生游戏界面、画面/声音/手感、真实用户存档及历史样本兼容、完整请求矩阵、长期平衡、自然病程。独立组合验证由协调者另行记录；本报告不替代组合结果或用户玩法验收。

## 协调组合复核

独立干净main用同一29项程序复现15通过、14失败。将本补丁与前24项在一次性副本组合后，76个纯检查工程及游戏Debug Rebuild全部通过，新专项29/29。418份源码执行前后哈希一致；173条递归来源记录无漂移。两处ShelterLife最小补丁可精确反向还原前24项版本，保留已有屋内排泄/出屋修正。

独立分支仍基于main，不依赖其他迭代；组合结果不掩盖上文单分支旧Continuity失败。此次没有重跑headless，不挪用上一轮48项场景结果。没有合并正式分支、玩家存档或视觉验收。第一次审计仅因Windows路径规范化误报，失败记录保留，修正审计后通过，未修改产品。结构化结果见shelter-entry-priority-combined25-20261106.json。最终发布身份以本报告所在提交为准。

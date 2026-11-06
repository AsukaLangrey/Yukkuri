# Poke 道具遮挡释放修复

结论：修复了按住身体、在前景软球遮挡处松手仍戳到身体的问题。修复后真实 Godot 几何与回调 12/12、Input.Intent 43/43、Interaction.StateBoundaries 10/10 通过；4 项诊断入口保护正确拒绝。总体为 partial，未做人眼与原生鼠标验收，且最终退出仍有 2 个 ObjectDB 实例泄漏警告。

## 版本和依赖

分支 alice/20261106-poke-prop-occlusion。以本地父提交 ef84a2cfc17f4a7a56160d91fd2d4546d7ac8e3c 为起点，明确依赖已发布的 poke-release-contact 提交 2d0a6be4f68247e287c181b928a10686ac985d8e。不是独立基于 main 的修复。本报告针对上述父提交加 source-sha256.json 所列最终字节。测试阶段尚未提交；发布状态由后续协调记录。未修改 main。

运行环境为当前本机 Windows，不是远程测试机。工具为 Godot 4.7.2 stable mono 和 .NET SDK 10.0.402。

## 最小产品改动

仅在 InteractionRoom.Gestures.cs 的短按释放条件追加 ObjectAt(mouse.Position)==null，并更新一行注释。现有原对象限制、当前未阻挡 Body 接触、当前部位/位置/法线与持续拉拽、扫击逻辑保持不变。

ObjectAt 已按当前射线距离排除身体后方的道具。本次沿用它，不新增第二套遮挡规则。InteractionRoom.PokeAudit.cs 仅增加调用新增 PokePropAudit 的一行；原 8 项、断言及守卫与父版本逐字比较（统一换行后）不变。新增文件 InteractionRoom.PokePropAudit.cs 及其 .uid 提供 4 项永久回归。

## 修复前失败证据

先加入诊断但不改产品逻辑，在本分支重现 11/12、退出 1，仅 prop-covered-release 失败，见 evidence/baseline-audit.json。

普通新庭院通过 BuyShopProduct 购买 12 元软球，再用生产按下/释放回调放到约 (-2.7, 0.82) 的合法空地。冻结世界时间，不改健康、情绪或接触数据。从无遮挡头顶按下，在软球覆盖的嘴边松手：
- 实际释放点约 (515.12506, 489.00653)。球距离 18.517397，身体距离 18.75929，球确在前景。
- 修复前仍产生 Poke：恐惧 +1.075、信任 -0.45、怨恨 +0.55、反应序号 +1。健康与疼痛不变，不能把这次缺陷说成额外扣血。
- 修复后同一几何条件下上述状态和反应序号均不变，待处理戳碰被清除。

每项新增用例分别记录自己的 pressRay 和 releaseRay，包含实际坐标、身体与球距离、球是否相交、前景对象。清晰释放对照的释放射线没有球交点，不借用遮挡候选点的距离。

## 最终覆盖

原 8 项全部保留并通过：有效头部点击、快速拖离、另一只身上松手、画布外松手、同对象新部位、指针不动而对象移走、持续拉拽后不追加点击、空处起始扫击后不重复结算。

新增 4 项全部通过：无遮挡释放仍有效、直接按球不建立戳碰、球遮挡处释放不戳身体、射线中球在身体后方不误拦。最后一项为明确的对象位移夹具：球保留合法购入和放置结果，对象 Z 移动 0.6 并刷新实际 Actor。释放射线身体距离 18.0932 小于球 18.517397，ObjectAt 为 null，正常点击使用当前身体接触。它验证距离顺序，不冒充自主移动测试。

诊断调用真实 _UnhandledInput、_Input、ContactAt 与 ObjectAt；原拉拽/扫击测试用 Input.ParseInputEvent 提供合成引擎按钮/指针状态。不伪造 Contact，也不直接设置 pulled/swing 使断言通过。没有覆盖原生鼠标或视口事件传播。

最终游戏构建和 headless 导入退出 0，最终构建 0 warning、0 error。两项纯规则检查分别 43/43、10/10，退出 0。复用档/输出目录、未知参数、混合 --legacy、直接 Main 四项均退出 2；没有世界启动标记，后三项没有生成结果目录。现有 headless、新档、新绝对输出路径守卫及执行位置未更改。

## 证据与限制

修复前失败、修复后完整结果、两项规则结果、保护结果及脱敏进程记录在 evidence/。源码和证据 SHA256 在本报告相邻 JSON 中。所有最终测试发生在最后一次产品/诊断源码改动之后；测试前后哈希一致，无自有测试进程残留。

首次恢复启动时固定 1800 秒总期限：22:53:24.9247966 至 23:23:24.9247966 UTC。最后检查于 22:56:01.5347689 UTC 完成，约 157 秒。每进程最多 300 秒并实际等待；若超时仅可结束 PID 与启动时间匹配的自有进程。本轮未超时、未重置期限。使用离线 Godot nupkgs、独立缓存/新测试档，关闭首次体验、ASP.NET 证书生成、共享编译和节点复用。

保留的中间问题：首个恢复因本地运行器参数拼接错误触发 MSB1008，修正后成功，未覆盖失败记录。初构建有两条既有 CS8604 警告，最终增量构建为零。最终正式场景退出报告 2 个 ObjectDB 实例泄漏；本次未修复该问题，不宣称生命周期无泄漏。

本轮只跑上述场景与两项相关规则检查，未跑完整项目套件、长期压力、真实历史存档兼容、音频或画面/手感验收。没有读取玩家存档。生成缓存和与本补丁无关的导入元数据不作为源码交付。

## 复现

使用获准的独立副本及锁定工具链，离线恢复并构建 src/Yukkuri.Game/Yukkuri.Game.csproj，先 headless --editor --import --quit。

Godot 参数：--headless --path src/Yukkuri.Game --audio-driver Dummy -- --interaction-test-profile poke-audit-<新后缀> --poke-contact-audit-output <工程 .local/testing 下新目录的绝对 result.json 路径>。

分别构建并执行 checks/Input.Intent/Input.Intent.csproj 和 checks/Interaction.StateBoundaries/Interaction.StateBoundaries.csproj；Input.Intent 传入独立结果 JSON 路径。实际执行参数已在 evidence/process-summary.json 中用 <project> 与 <toolchain> 代替本机绝对路径。
## 独立组合核验

在既有组合源码的新副本加入当前戳碰修复及按压画布修复，两处产品hunk逆向还原原文件。716个最终文件和Game DLL运行前后哈希不变，710个来源输入未改。Poke12、Pressure6、Input43、State10、Company31、Selection24，共126/126；另9项隔离入口负例正确拒绝。约175.2秒，无超时或残留自有进程。

前方球遮挡拦截、后方球不误挡均由实际射线验证。保留两条既有CS8604构建警告及Pressure、Company各2个ObjectDB退出实例警告。仍未验收原生输入、画面、长期稳定性或完整项目。组合验证不改变本分支依赖已发布poke-release-contact的事实。

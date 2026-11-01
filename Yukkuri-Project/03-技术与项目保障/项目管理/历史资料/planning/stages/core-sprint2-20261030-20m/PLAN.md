# 第二轮并行复核与修改

用户于2026-10-30 00:11左右追加：再检查一轮，分派多个修改任务，继续约20分钟。基线 f02b0fcfbb3e9649b5f94d35201c4c22661158b9；优先完成真实缺陷修补、交叉复核和明确范围整合，不以空转消耗额度。预计00:12—00:32。用户另已明确允许联系既有“启动项目最新版本”统一协调。

## 所有任务共同约束

只做源码/文档读写、静态文本审查及Git/GitHub文件管理。此次协调消息明确不授权任何本机编译、运行、测试、模拟、Godot/headless、项目脚本、钩子、渲染、预览或桌面操作。任何其他聊天历史运行许可不适用于本次派发。保留冻结art03、启动器、旧证据与全部已有工作。

共享main；各工作聊天不暂存、不提交、不推送、不切分支、不重置、不清理，由主协调统一处理。Git使用 -c core.hooksPath=NUL -c gc.auto=0 -c maintenance.auto=false。发现跨范围缺陷先写交接，由主协调重新分配；不可覆盖并行变更。检查源码能区分旧错/新对才新增，禁止执行。未运行就明确未运行。

## 文件分工（本轮覆盖旧轮同名文件的历史分工）

1. 既有试玩聊天：InteractionPhysiology.cs、InteractionRoom.Vitals.cs、InteractionRoom.Ui.cs 的饱渴/生理段落、InteractionPropContact.cs；仅上述源码可写。复核排泄开始/完成/中断、状态有效性、清理与上限，以及生命值隐蔽的展示一致性。交接本目录 physiology.md。旧 playtest-additions.md 仍可更新归属。不得改他人下列文件。
2. contact：CarryReleaseMotion.cs、InteractionRoom.Input.cs、InteractionRoom.cs 中仅拖拽/相机/自动保存相关段落、checks/Interaction.Continuity/Program.cs。复核抓取松手、急停/反向/失焦/取消/遮挡、多对象与存档门槛；保留音频及生理接线。交接 contact.md。该检查文件全部新增检查由contact统一处理，其他任务仅提供建议。
3. save：SaveStore.cs、InteractionValidation.cs、checks/Simulation.Smoke/Program.cs。复核新增生理/排泄物的序列化、旧字段缺失与非法值、对象和全局上限、写失败保全。交接 save.md。不改InteractionPhysiology；向其负责人提问题。
4. life：NaturalLife.cs、InteractionRest.cs、InteractionBehavior.cs、InteractionWorld.cs、checks/NaturalLife.Smoke/Program.cs。复核排泄和自主吃喝/休息、紧急需求、固定装置、场景切换相互中断的优先级。交接 life.md。不改InteractionPhysiology，跨入口修补由协调者统一接线。
5. audio：InteractionRoom.Audio.cs、Main.MilestoneAudio.cs、assets-source/audio-polish-20261029.txt。针对实际播放优先级、静音、读档/重开/退出、旧检查与默认入口差异做第二遍边界审查，可修确定问题。交接 audio.md。无权改其他partial，跨文件接线给协调者。
6. art：CreatureActor.cs、CreatureActor.Interaction.cs、InteractionFoodArt.cs、InteractionFoodSurface.cs、InteractionToolArt.cs、InteractionToolSurface.cs。重点已有未提交排泄动作、选中圈移除与受伤/死亡/托举显示叠加，兼顾默认工具几何。不能运行渲染或依据猜想调风格，交接 art.md。不改Vitals的排泄物构造，需要时交生理负责人。
7. review：只读全部源码与暂存差异，只写review.md。独立检查本轮和既有未提交功能，优先可达高影响回归；不写产品代码。先指出证据链，再复核修补。
8. 协调者：本轮PLAN/SUMMARY、测试请求和入口文档；WorldSimulation.cs、InteractionState.cs只做确有必要的跨模块接线。统一检查完整依赖、暂存、提交推送，保留所有用户工作。

## 交付

前8—10分钟查明并修复，随后逐步停止写入交独立复核。交接包含触发、文件、改动、静态依据、远程待测及未解决问题。没有确定新缺陷就结束，不制造无用改动。主聊天通过等待工具收取，不主动给外部人员/其他聊天发消息。当前共享未提交功能须经本轮审查后才能纳入最终提交，不能把旧编译记录当本轮通过。

9. design追加：只写 planning/game-design-20261029.txt 和本目录design.md，同步既有试玩聊天最近明确的中键平移、去选中圈、近看环境/个体输入区分、可见饱渴、排泄和隐藏生命要求，区分用户要求、共享待整合实现和未验证项。不写产品代码或原始需求，不根据助手概述伪造用户原话。


00:18交叉复核：audio原任务结束后只读复核Actor，新增actor-review.md；save原任务结束后只读复核Physiology/Vitals/PropContact，新增physiology-review.md。两者不得修改被复核源码，review主任务重点输入与自主生活及整体依赖。


00:22追加确定缺陷：actor-review发现半空暂停后切近看会抹去跳跃变换，交art最小修补；额外授权ActorHopMotion.cs及checks/ActorGeometry.Smoke/Program.cs相关段落，检查只写不运行。audio继续独立只读复核。协调者另发现自主排泄物默认Guid使同档相同推进的产物ID不同，交physiology最小确定性ID修补，save独立复核；不扩改旧手动物品创建。

# 核心问题约 20 分钟并行处理

用户于 2026-10-29 晚明确要求加速、分拆多个聊天检查并处理核心问题。工作时段约 23:47—次日 00:07（北京时间），优先用前 12 分钟查明和修补，余下时间交回并独立静态复核。不要求空等或机械消耗额度；无法保证耗尽额度。所有未实际运行的检查明确写未运行。

## 统一限制

先读根 AGENTS.md。原开发机及本机副本禁止游戏、Godot/headless、启动器、模拟、测试/验证程序、编译器、渲染器、预览程序、项目脚本或 Git 钩子，不通过任何入口绕过；禁止 computer-use、截图、窗口读取和桌面操作。仅文件读写、静态文本审查及 Git/GitHub 操作。Git 每条命令使用 `-c core.hooksPath=NUL -c gc.auto=0 -c maintenance.auto=false`。不修改冻结 art03 或启动器，不降低断言，不把静态结论写成通过。

共享 main，不切分支、不暂存、不提交、不推送、不重置、不清理。由协调聊天统一处理明确属于本轮且完成复核的文件。先读旧阶段交接，已修复的问题不重复提出。发现跨范围问题写交接，不抢改。

## 接手时已有改动：本轮全部保留且不修改、不提交

- checks/Interaction.Continuity/Program.cs
- src/Yukkuri.Game/CreatureActor.Interaction.cs
- src/Yukkuri.Game/CreatureActor.cs
- src/Yukkuri.Game/InteractionRoom.Input.cs
- src/Yukkuri.Game/InteractionRoom.Ui.cs
- src/Yukkuri.Game/InteractionRoom.cs
- src/Yukkuri.Simulation/CarryReleaseMotion.cs（新增）

以上托举/落地等改动来自另一聊天。允许只读查调用关系，不接管其实现。不将其混入本轮提交。

## 独占文件归属

1. life：NaturalLife.cs、InteractionBehavior.cs、InteractionRest.cs（均位于 src/Yukkuri.Simulation/），checks/NaturalLife.Smoke/Program.cs，以及本目录 life.md。重点需求优先级、低信任、自主寻找/休息、来源中断后能否恢复；避免重做已审资源预留退款。
2. contact：src/Yukkuri.Simulation/InteractionGesture.cs、ContactMotion.cs、InteractionSensitivity.cs，checks/Input.Intent/Program.cs、fixtures/contact-traces.json，以及本目录 contact.md。重点真实接触、急停/抽手、持续动作完成/拒绝后控制权，关注默认入口可达路径，不碰本轮禁改的托举接线。
3. save：src/Yukkuri.Simulation/SaveStore.cs、InteractionValidation.cs，checks/Simulation.Smoke/Program.cs，以及本目录 save.md。重点有效档损坏保护、备份回退、写失败、异常数据校验；没有授权历史样本不能编造历史兼容通过。
4. feedback：src/Yukkuri.Simulation/InteractionPresentation.cs、RoomResourceText.cs、OilDialogue.cs，以及本目录 feedback.md。重点真实行为与姿态/油话/提示冲突、拒绝和疾病优先级。不盲调美术或扩写大量对白，不改 Game UI。
5. review：所有源码只读，仅可写本目录 review.md。独立寻找高影响可达问题，复核以上聊天交回差异，指出调用次序和远程验收案例。不能编辑产品/检查源码。
6. 协调聊天：本目录 PLAN.md、SUMMARY.md；原阶段 PLAN.md 的本轮入口；testing/request.json、testing/report-template.json；进度入口文档；按本轮文件清单统一提交和普通推送。其他源码不抢改。

## 交付要求

## 用户追加与并行避让（23:50 +08）

用户追加独立美术、音效音乐、游戏策划书三个聊天。美术独占 `src/Yukkuri.Game/Assets/Icons/*.svg`、`src/Yukkuri.Game/Assets/Art/*.import`、`src/Yukkuri.Game/Assets/pipeline-probe.glb.import`、`assets-source/art-polish-20261029.txt` 及本目录 art.md；其他美术源码/二进制只读，不盲调模型几何，不运行生成器。音频独占 `src/Yukkuri.Game/Main.MilestoneAudio.cs`、`src/Yukkuri.Game/Assets/Audio/*.import`、`tools/make-m01-audio.ps1`（只编辑不执行）、新增 `src/Yukkuri.Game/InteractionAudioPolicy.cs`、`assets-source/audio-polish-20261029.txt` 及本目录 audio.md；默认小院实际接线涉及他人正在编辑的 InteractionRoom.cs，只给出具体接线建议，待协调者处理。不得为了写新类而留下无用未接入实现。

策划书聊天只写 `planning/game-design-20261029.txt` 与本目录 design.md，读取全部已确认需求及设定，完成正式策划书，不改产品源码或其他需求原件。

已发现既有“启动项目最新版本”聊天正在新增饱渴显示、排泄和隐性生命/伤势功能。life 对 NaturalLife.cs、InteractionBehavior.cs、InteractionRest.cs、NaturalLife.Smoke 暂停写入，转为只读审查；save 暂停写 InteractionValidation.cs，继续独占 SaveStore.cs 与 Simulation.Smoke。等待明确协调后才能恢复这些共享文件写权限，不覆盖任何已有差异。

23:54 美术范围纠正：上列旧图标/标题资源并非默认小院主美术。追加美术独占 `src/Yukkuri.Game/InteractionFoodArt.cs`、`InteractionFoodSurface.cs`、`InteractionToolArt.cs`、`InteractionToolSurface.cs`、`WoodlandArt.cs`、`WoodlandSurface.cs`，以这些默认小院实际使用的资源构造源码为主。只修可由静态几何/材料/资源逻辑证明的问题，不凭猜测调画风；不改 CreatureActor、SceneArt、InteractionRoom 或已有保留差异，不运行构造器、渲染器或预览。

23:53 life 小范围收尾：只授权精确修正其自己新增的 urgent food and water detours 检查块中的资源距离与扫描时机，其他产品/检查修改仍暂停。它在冲突通知前已落盘的休息搜索修复保留并交协调者逐段归属，不撤销或混入他人的新功能。

交接必须给出：具体触发条件、依据、改动文件、静态核对结果、尚未运行的远程案例及未完成事项。查不到新确定缺陷就明确结束，不为凑改动扩范围。需要写回归检查时，断言必须能区分旧错/新对；只保存源码，禁止执行。每个聊天只最终汇报，不另建子任务或自动化，不主动给其他聊天发消息。协调聊天通过状态工具收取结果。

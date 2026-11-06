# 抓取食水后残留放置动作：修复与回归

日期：2026-11-06 UTC。
基础提交：`e8d96a78f49182eb17e269e2b62a738c7a9bb7ea`。
工作分支：`alice/20261106-grab-supply-action`。本报告验证基础提交加本次列明源码；最终提交由协调者统一生成，不冒称尚未生成的提交已通过。

## 结论

修复前，本分支独立复现了 6 个目标失败：清水、饭饭、零食在普通松手或暂停取消后，标题仍为“空手 · 抓取”，但下一次空地点按会新建补给并再次扣库存。48 条断言中 42 条通过，0 个夹具失败。原实物的 ID、份额仍在，故这是额外购买，不是物体移动或份额消失。

修复后，针对性引擎检查 192/192 通过，0 个夹具失败；既有 Input.Intent 43/43、Interaction.StateBoundaries 10/10 通过。8 种非法诊断入口全部在创建世界、旧场景路由和 SaveStore 前拒绝。所有进程正常结束，无超时。

这是获授权本机独立测试副本的 headless 与规则验证，不是远程机器、原生鼠标操作、画面/音效验收或完整玩法验收。

## 最小改动及设计依据

抓取已有补给会临时借用 FeedMeal / FeedSweet / Drink，供同一手势继续递到嘴边。原结束路径清理了 `_heldSupplyId`，却未恢复 Grab 的 Lift 动作，随后空地点按误入库存放置入口。

产品改动仅位于 `InteractionRoom.Input.cs`：在取消与正常释放清理手持补给后，通过同一小函数将 Grab 借用的食水动作恢复为 Lift。持有期间的喂食动作不改，原物体移动、落地和真实入口消费仍走既有代码；显式食物工具不被重置。

静态检查确认 `_heldSupplyId` 只有上述两处赋空；ProcessHeldSupply 的对象失效分支调用 CancelGesture。耗尽份额后的取消亦有实际回归，不依赖把份额或库存直接写成目标结果。

设计依据：`02-基础资源与内容设计/界面管理/布局与组件规范.txt` 第四节要求安全取消不额外使用道具，第八节要求二级工具、标题与选中状态一致。本修复没有新增玩法规则。

## 覆盖范围

- 三种补给分别覆盖普通释放、Space 暂停/继续、实际 FocusExited/FocusEntered 回调、画布外松手、中键镜头操作取消、右键取消和切换到抚摸。
- 普通释放验证原 ID 确实移动，最多 60 个模拟帧等待原物体落地；取消验证原位置、份额、库存不变。
- 每条路径再次抓取同一个原物体，再取消、点空地，验证不会新增补给或再次扣库存。
- 每条路径随后显式选择食物工具，验证仍能正确创建一个新补给并只扣一份库存。
- 三种补给经真实投影嘴部命中、引擎鼠标状态和 ProcessHeldSupply 消费原有份额；第一帧不提前消费，连续接触只消费一次，库存不额外扣；耗尽后取消并点空地仍是 Grab。
- 守卫拒绝未知参数、旧场景混用、直接 Main 场景、重复输出参数、相对路径、缺失参数、复用输出和复用存档配置。拒绝退出码均为 2，无正常世界就绪标记，无普通存档目录；已有输出哨兵未被覆盖。

检查使用真实按钮信号、生产输入回调和投影命中。焦点覆盖是引擎信号，不是 OS 焦点自动化。运行中不读写用户存档。

## 构建与未解决警告

使用已安装 .NET SDK 10.0.402、Godot 4.7.2 Mono 和其离线包，无下载或工具安装。完整初始构建成功，有两条已有 CS8604：SupplyStationValidation.cs(12,21)、InteractionValidation.cs(15,19)。最后增量构建成功、0 新警告，不能据此声称原警告消失。

修复前及最终运行保留“2 ObjectDB instances were leaked at exit”警告，本次没有修复或隐藏。Godot 导入生成的缓存和导入元数据不纳入源码修复。

## 复跑与证据

新增 `tools/check-grab-supply-offline.ps1`。只在获授权的新独立副本中使用 PowerShell 7 调用；`-ToolRoot` 必须指向获准使用、已安装的准确离线工具根目录，不会下载安装。

按顺序调用 `-Stage build`、`-Stage import`、`-Stage run`，每次提供新的 `-Attempt`；规则检查使用 `checkbuild` / `checkrun` 和 `-CheckName Input.Intent` 或 `Interaction.StateBoundaries`；入口检查使用 `-Stage guard -GuardCase unknown|legacy|direct|duplicate|relative|missing|reuse-output|reuse-profile`，每次选择其中一项。脚本在运行前为每次进程创建独立应用数据、临时目录和配置，不覆盖旧证据。

脚本不是仅作语法审查：最终脚本实际完成 build、unknown 守卫和完整 run，复跑仍为 192/192，JSON 与退出码同时核对。早先原型脚本执行了完整导入、两个规则项目和八类守卫；这些阶段的实测证据也保留，不将它们冒称为最终脚本全部阶段重新运行。

从第一次构建起固定总预算 1800 秒，单进程最多 300 秒，真实 WaitForExit 超时后仅终止 PID、启动时间和路径均匹配的自有进程树。预算不重置；超时分支本次未执行。共享编译器关闭、nodeReuse=false、maxcpucount=1，禁用 ASP.NET 开发证书生成。最终脚本将临时离线 NuGet 配置由 finally 恢复原始字节，已核对一致。

可提交的逐项结果、基础和最终源码 SHA-256、退出码、耗时、原始证据 SHA-256 见同目录 `grab-supply-action-20261106.evidence.json`。原始日志、失败基线和完整检查点保留在测试副本，不提交私人路径、机器标识或用户数据。

未执行：原生输入、视觉/音效、历史私人存档、长期压力测试及完整 aggregate 检查。合并和最终验收仍由用户决定。

## 独立组合验证

当前 assembled 基线仅融合三处 Input 差异和新增诊断入口，未覆盖其既有 Room/Main 处理器。反向移除产品差异后，Input SHA-256 精确恢复为 9D7A1A689E2296C28C385EA01188898A63D35887DC815C6F1C24196710E69559。

组合共 364/364：Grab 192、Input.Intent 43、StateBoundaries 10、HungerFeeding 14、Inventory 12、Company 31、Selection 24、Shop drafts 38；另有 8/8 启动拒绝守卫。27 个进程共 254.3 秒，无超时。838 个基线输入及4个修复源码输入保持不变。独立报告 SHA-256：AACEFA9F48CE0BF30671A914A1967BA17F9D99573B5635A959913C5A00AF51D8。

组合 Game DLL SHA-256：D007E2DBA7DA2190C232BAB660D1A39077B3F0631FD94CF07D797411C37603CD。保留两条 CS8604；Grab、Company、Shop 各有2个 ObjectDB 退出泄漏警告。此验证仍非原生输入或视觉验收。

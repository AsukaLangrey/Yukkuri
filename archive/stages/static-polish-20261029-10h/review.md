# 独立静态审查交接

结论：基线中发现 1 项可能丢失当前进度的问题、5 项状态或存档边界问题。09:47 后复读时，其他任务已修改其中的休息与失焦路径；具体状态见下表。仅审查源码，未修改产品代码或检查代码，未运行游戏、编译器、测试、验证脚本或桌面操作。以下均为静态推导，不是运行复现或远程验收结果。

## 最新复读状态（北京时间 09:47—09:48）

此时 HEAD 已由主对话更新为 `4a7acd621c2b37d201e0cf196aab63ed83a6eb04`，源码仍有并行未提交修改。下面是观察到的工作区内容，不是对该 HEAD 已包含修复的断言。

| 编号 | 当前观察 |
| --- | --- |
| R1 | `SaveRoom` 已由主对话改为返回 bool，退出路径已检查结果；当前读到的“保存并重新开始”回调仍忽略结果。必须补上按钮门槛才完整解决原问题。 |
| R2 | life 已在 `UpdateInteractionRest` 后增加 `ActiveCue==null && RestRemaining>0` 时清 `FollowPlayer`。静态上可打破死循环：首次建立休息后清跟随，下一步可实际休息。未运行。 |
| R3 | 主对话及 input 已接入 `_windowFocused` 初始化、进出焦点、处理帧与输入入口门控；失焦不再持续刷新手。保留原有最多 3 秒世界时间的短暂感知记忆是当前明确选择，不再把“未立即清空”单独当作未修复缺陷。须远端确认失焦后有效期不续长，期满不再驱动行为，返回不补发手势。 |
| R4 | 尚待主对话结合 life 交回内容处理区域过滤。 |
| R5 | 与主对话指出的 pending 来源查询遇到 null 元素是同一缺陷，已合并为一项，勿重复计数。 |
| R6 | 当前读取的验证器仍缺进行中练习与字典条目的关联约束。 |

下面保留最初的触发路径和修复建议，方便主对话追踪。R2/R3 不应再作为“当前完全未处理”对外汇报；是否修复通过只能看整合提交的远端结果。

## 审查身份与内容版本

- 工作目录：原开发机 `D:\Project\Yukkuri`。
- Git 基线：`d729036ccfa4773c92b2f8794f804e26fc9c02a1`。
- 观察窗口：北京时间 2026-10-29 09:39 后至 09:44:22。09:44:22 复读过本文涉及的关键分支。
- 开始时 `src/`、`checks/` 无已跟踪文件修改。审查期间 input、presentation 任务开始改动；09:44:22 已读取 `InteractionRoom.Input.cs`、`InteractionRoom.Ui.cs`、`InteractionPresentation.cs` 的当前差异。下文使用函数名和代码条件定位，不能把基线行号套到最终整合版本。
- 09:44:22 时，`InteractionBehavior.cs`、`NaturalLife.cs`、`InteractionRest.cs`、`InteractionValidation.cs`、`SaveStore.cs`、`InteractionRoom.cs`、`checks/Save.Compatibility/Program.cs` 仍对应上述 Git 基线。若 life 或主对话随后改动，请按条件复核并注明解决提交。
- 本任务唯一写入文件：本 `review.md`。没有暂存、提交、推送或切分支；没有向其他聊天发消息。

关键并行文件的 SHA256（09:44:22 读到的内容）：

| 文件 | SHA256 |
| --- | --- |
| `src/Yukkuri.Game/InteractionRoom.Input.cs` | `6B706D40B5CAE3BA145541742A573A247D646BE58822F285CBC52FAA1EF61C7B` |
| `src/Yukkuri.Game/InteractionRoom.Ui.cs` | `F18E82FC9AB1AEAA5B05D4C302A7F340B456F705091E006CFDC593E2541EFFE4` |

## 待主对话处理的具体问题

### R1 · P1 · 保存失败后仍重新开始，当前进度被替换

- 位置：`src/Yukkuri.Game/InteractionRoom.cs` 的 `SaveRoom`；`src/Yukkuri.Game/InteractionRoom.Ui.cs` 的 `ShowSettings` → “保存并重新开始”回调。
- 条件：当前小院有未保存进度，自动槽位主档损坏，或保存发生磁盘/权限异常，随后点击“保存并重新开始”。主档损坏是已有 `SaveStore.Save` 明确拒绝覆盖的情况，不需要假设异常线程行为。
- 依据：`SaveRoom` 返回 `void`，捕获异常后只写 `_message`；调用方随后无条件执行 `Sim=WorldSimulation.CreateInteractionPlaytest()`、`BuildActors()`、`CloseModal()`。保存失败不会阻止旧世界被替换。“当前状态先保存到自动存档”的承诺因此不成立，进度不能靠重试保存找回。
- 建议：主对话将保存结果显式返回；仅在成功后替换 `Sim`。失败时保留原 `Sim` 和重新开始对话，并显示错误。恢复备份后的 `_recovered` 情况也走同一成功门槛，不应借重新开始绕过原档保留政策。`QuitRoom` 使用同一保存方法，可一并核对退出策略，但本文不把其他退出设计自动列为同一个已确认缺陷。
- 分工交界：`InteractionRoom.cs` 属主对话，UI 属 presentation；需待该任务交回后统一接线。
- 远端案例：在隔离测试存档目录中先存有效备份，再把槽位 0 主档截断；让当前世界产生可辨识的新进度，点击该按钮。预期仍是原世界，失败信息可见，主档与备份字节不变。另测成功保存才重新开始；读回保存档应能恢复该进度。

### R2 · P2 · 成功跟随后，自主休息可能永久卡住

- 位置：`src/Yukkuri.Simulation/InteractionBehavior.cs` 的 `CompleteCue`、`TickInteractionWorld`；`src/Yukkuri.Simulation/InteractionRest.cs` 的 `UpdateInteractionRest`、`ApproachInteractionRest`。
- 条件：成功完成跟随，留下 `FollowPlayer=true`、`ActiveCue=null`；之后精力低于 22，食水均不紧急。手离开、切区或读档后仍可保留这个跟随状态。
- 依据：成功 `CompleteCue` 不清 `FollowPlayer`；`UpdateInteractionRest` 先建立 30 秒休息，随后遇到 `i.FollowPlayer` 直接返回，既不给休息恢复，也不扣休息剩余时间。`TickInteractionWorld` 又在持续跟随分支之前优先进入 `RestRemaining>0` 的休息分支。到达垫子或没有垫子时，`ApproachInteractionRest` 只显示“想休息，但还没有放松下来”，不会解除循环。显式休息指令会清跟随，但自主休息没有这一步。
- 建议：明确“跟随意图”和“当前正在跟随”的关系。自主休息需要接管时，暂停/清除已完成练习留下的持续跟随，或者让休息判断只阻止实际在走的跟随；同时保留未完成训练的成功判定，不要靠删除检查修复。
- 分工交界：behavior 属 life；rest 属主对话。由主对话与 life 交回内容一起整合。
- 远端案例：实际成功完成跟随后，把精力安排到 21、饱腹和水分各 90；分别在有垫/无垫、手在场/离场、读档后三种组合中继续普通时间。预期能进入真实休息、精力回升、休息时间递减，不无限停在“没有放松”。如选择暂停后恢复跟随的设计，再核对休息结束后的恢复条件。
- 旧覆盖缺口：`NaturalLife.Smoke` 分别检查了无手不跟随、失败跟随清理，以及低精力休息；未找到“成功跟随遗留状态 + 自主休息”的组合。`Interaction.Continuity` 的显式休息中断训练不能覆盖该条件。

### R3 · P2 · 失焦取消手势后，跟随逻辑仍能重新制造在场的手

- 位置：`src/Yukkuri.Game/InteractionRoom.cs` 的 `FocusExited`、`_Process`、`UpdateInteractionHand`；`src/Yukkuri.Game/InteractionRoom.Input.cs` 的 `CancelGesture`；`src/Yukkuri.Simulation/NaturalLife.cs` 的 `SetInteractionHand`。
- 条件：已开始跟随或已成功跟随，鼠标最后位置在场景内，通过键盘切到别的窗口；游戏仍继续处理帧。
- 依据：失焦事件只调用 `CancelGesture`；该方法清接触/拖动状态，但不记录窗口焦点，也不清 `FollowPlayer` 或手感知。后续 `UpdateInteractionHand` 无焦点判断，只要任一个当前区个体 `FollowPlayer=true` 且旧鼠标位置在场景内，就继续 `SetInteractionHand(...,true)`。即使没有持续跟随，`SetInteractionHand(...,false)` 也仅在原 3 秒有效期过后清除，不能表达强制离场。并行 input 改动统一清除了接触力度/方向，但没有修复这个跨文件入口。
- 影响：失焦时仍可能继续跟随、求助手或避手；“取消接触”不等于“人已离场”。具体后台帧行为须远端实机确认，源码入口缺少焦点约束已确认。
- 原建议：由主对话在房间生命周期中维护焦点/退出状态，失焦期间禁止通过鼠标位置刷新感知。短暂记忆是否立即撤销需与行为设计统一；最新实现明确保留原有效期，状态更新见文首，门控本身不能缺失。
- 远端案例：完成跟随、保持鼠标在场景中央后键盘切走，观察普通时间继续运行时手标记不得刷新，也不得增加跟随练习行程；返回窗口后需按约定恢复。补测暂停→失焦→恢复、近看→失焦→回小院，确认没有陈旧输入急停或自动补做动作。
- 旧覆盖缺口：`Input.Intent` 中名为 “release and focus reset discard the pending strike” 的检查实际只调用追踪器 `Reset()`，没有触发房间 `FocusExited` 或执行 `UpdateInteractionHand`。`NaturalLife.Smoke` 的无手案例也没有接入后台鼠标刷新。

### R4 · P2 · 收起物品会打断另一区域同坐标的个体

- 位置：`src/Yukkuri.Simulation/InteractionBehavior.cs` 的 `PutAwayRoomObject`；`src/Yukkuri.Simulation/InteractionWorld.cs` 的 `Feedback`。
- 条件：世界至少两个区域；区域 A 有要收起的饭/水/宝物，区域 B 的个体在相近 X/Z 坐标，正在预备取食或咀嚼；收起 A 的物品。
- 依据：收起时发送 `cold/withdraw` 的循环只筛距离和未死亡，没有 `c.RegionId==obj.RegionId`。`Feedback` 对 `withdraw` 会解除尚在使用的自然食水来源；入口前会退还 B 的份量并取消动作。即使没有进食，B 也会收到不属于当地的反应。后面的宝物主人专用反馈已有区域过滤，不能抵消前面的跨区循环。
- 建议：通知和物品可操作范围统一按区域过滤。至少把反应接收者限定在物品所在区域；另外核对公开 `PutAwayRoomObject` 是否应拒绝非当前区 ID。不能仅在 UI 过滤，因为模拟入口和其他调用仍会经过这里。
- 远端案例：两区同坐标安排个体，在 B 分别处于入口前/入口后时收起 A 的物品。B 的来源、剩余时间、份量、暴露计数与反应序号都不应因此变化。A 仍应按原规则结算/退回。默认小院只有一个区域，故此项属于多区域状态交界，并非声称已在默认界面复现。
- 旧覆盖缺口：已有异区资源不可见/不可吃检查；未找到“收起物品的广播不能跨区”的案例。

### R5 · P2 · 某类结构损坏主档不会回退到有效备份

- 位置：`src/Yukkuri.Simulation/InteractionValidation.cs` 的 `NaturalIntakePending` 来源验证；`src/Yukkuri.Simulation/SaveStore.cs` 的 `Read`、`Load`。
- 条件：可解析的 JSON 主档含有效的预备进食个体，但 `RoomObjects` 数组前部有 `null` 元素；备份有效。
- 依据：验证器先执行 `s.RoomObjects.Any(o=>o.Id==... && o.Kind==...)`，后面才逐项检查 `o!=null`。读取来源时会先触发 `NullReferenceException`；`Load` 仅捕获 `IOException`、`JsonException`、`InvalidDataException`，因此不会尝试 `.bak`。界面只能报读取失败，已有好备份用不上。
- 建议：先做房间物品的结构/身份校验，再做引用校验；所有不合法存档内容应由验证器明确转成 `InvalidDataException`。不要简单把 `Load` 改成吞掉任意程序异常，以免掩盖真正的代码错误。
- 远端案例：在隔离目录里保留有效 `.bak`，主档使用上述 JSON 结构损坏。预期加载结果明确 `RecoveredBackup=true`，两份文件保持不变。另测主档和备份都结构损坏时得到既有聚合失败路径。
- 旧覆盖缺口：`Simulation.Smoke` 检查截断 JSON 的回退；`NaturalLife.Smoke` 检查无来源等字段错误；二者没有覆盖“引用验证先遇到 null 元素”的顺序。

### R6 · P2 · 缺失训练记录的进行中存档被接受，继续后会异常

- 位置：`src/Yukkuri.Simulation/InteractionValidation.cs` 的 `ActiveCue`/`Learning` 校验；`src/Yukkuri.Simulation/InteractionBehavior.cs` 的 `CompleteCue`。
- 条件：存档有 `ActiveCue=Wait`（或其他有效枚举），但 `Learning` 缺少该键。可以是结构损坏的档案；尚未发现正常游戏生成该组合的路径，因此不声称正常保存必然损坏。
- 依据：验证只要求 `ActiveCue` 是有效枚举、`Learning` 非空对象且已有条目合法，不要求当前练习的条目存在。`CompleteCue` 直接执行 `i.Learning[cue]`。此档会作为有效主档加载，等完成/失败/紧急需求打断训练时抛出键不存在异常。
- 建议：读档验证增加当前练习与学习条目的关联约束，作为非法主档进入备份回退；不要凭空补成功次数或把损坏档伪装成正常兼容默认值。
- 远端案例：主档从有效练习状态移除当前 `Learning` 键，保留有效备份；加载必须拒绝主档并回退。正常缺省 `ActiveCue=null`、空字典的旧档仍应可读。

## 本轮已核对的正确路径与尚未覆盖处

以下只说明源码能支持的结论，不代表这些检查已运行。

- 自主吃喝的正常路径是 `BeginNaturalIntake` 先减一份并置 pending；到 `BiteContactSeconds=.45` 后 `CompleteNaturalIntake` 先清 pending，再增加饱腹/水分、登记一次暴露。之后每帧不会再次走结算。河水不减份量。
- `HoldSupply` 和 `PutAwayRoomObject` 都先解除食水来源；`DetachNaturalIntake` 只在 pending 时退一份。入口后只断开来源，不退还已吃份量，不回滚已有暴露。`PruneConsumedSupplies` 保留仍被预留的零份来源，避免入口前被删除。
- 手喂使用手势的 `Applied` 标记限制一手势一口；地上拿起的食物减物品份量，直接库存喂食才减库存。松手/取消不能退回已经入口的食物。
- 这三条已有 `NaturalLife.Smoke` 对准备、入口、暂停、读档、中断、收起、死亡和多人竞争的具体断言。不能把它们概括成完全没有结算检查。建议整合后对食品/水各自重跑，特别是 life 改动碰到反馈或动作优先级时。
- 自然资源/消化字段有属性默认值，`HeldByHand`、手感知和即时反应姿态不序列化。`WorldSimulation` 构造时清除手按约束。存档兼容检查必须区分“遗漏新增字段的旧档”与“显式 null 或内部引用损坏”；后者不应该享用默认值放行。
- 当前旧字段缺省案例移除了 `Resources`、`Digestion`、若干来源几何字段，却没有移除 `NaturalIntakePending`。应另设缺少 pending 的历史式入口后状态，确认不重复发放养分/暴露；预备状态是较新的保存语义，不能简单删字段后还要求预留自动重放。
- `checks/Save.Compatibility/Program.cs` 加载并重存后，仅调用 `simulation.Advance(120)`。对互动小院来说，实际游戏帧还先调用 `AdvanceInteractionPresentation`，吞咽、预备入口、下落等都在那里推进。因此该兼容检查中的“继续 120 秒”不能证明预备食水可完成、只结算一次或动作可恢复。建议对互动档使用等效的固定小步双入口，并在独立样本上断言一口前后资源守恒、暴露次数和后续状态；非互动档保持原适用路径。
- `Save.Compatibility` 对照保存前后全部既有字段是有价值的，不应为修复默认值或姿态问题放宽该断言。真实历史样本仍按 `testing/PROTOCOL.md`：远端缺获准样本就记 blocked，不用本轮合成样本冒充历史兼容通过。
- 暂停按钮、切个体、切近看、弹窗、存档入口都有 `CancelGesture`；模拟结束手势按原 TargetId 查人，而不是按当前区域查人，已有这一点正确清理。直接 `Travel` 本身没有统一清理手势/持有物品，默认小院没有切区 UI；需要主对话决定多区入口的契约，并在公开切区入口测试，而不是凭 UI 假定已覆盖。

## 三个并行任务的整合核对点

1. input：本轮已看到结束/失效手势统一清追踪器、力度、方向和姿态的改动。它不替代 R3 的焦点与手感知生命周期；应将房间、模拟两侧接起来。
2. life：若修改自然需求或反馈顺序，保留入口前退款、入口后不可退款的界线。优先核对 R2 的跟随/休息交界、R4 的异区反馈，并让“动作打断”和“纯状态文字”各自走明确路径。
3. presentation：已看到吞咽姿态残留、声音与状态文案调整。显示 `NaturalIntakePending` 和 `EatingRemaining` 时使用模拟的真实状态，不能为文案主动结算或清资源；R1 的按钮保存门槛需主对话同步修改返回值。
4. 主对话：R1、R3、R5、R6 涉及共享文件；R2 跨 life/rest；R4 在 life 文件。请等待所属任务交回再改，避免覆盖进行中的工作。本审查任务没有修改任何源码；其他任务的已见修正见文首。

## 远端交付要求

建议顺序：R1 防丢进度 → R2/R3 行为与失焦 → R4 区域隔离 → R5/R6 损坏存档回退，再跑已有食水结算回归。新增案例由主对话写入允许修改的检查/测试要求。

只由用户安排的独立 Alice/dot 按 `testing/PROTOCOL.md` 执行。报告写实际机器、完整 40 位源码提交、测试规范版本、每项状态、失败证据和未测项。针对该基线的分析不覆盖之后的并行修改；旧提交的通过记录不能证明整合提交通过。本任务未代跑远端，也没有编译、运行或人眼效果结论。

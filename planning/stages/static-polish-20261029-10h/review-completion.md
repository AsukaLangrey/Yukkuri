# 已入口旧档案例与完成动作交界审查

最新结论（10:50）：已复核落盘的完成动作核心和 Continuity 新案例，在下文最终 blob 范围内未发现仍需修正的实质问题。过程中发现修补松手案例只推进 0.2 秒就断言移动；后续版本已改为 0.3 秒，此误报已静态关闭。没有运行程序、测试或编译。旧档案例原结论保留；下文早期“待实现”内容是 10:24 的历史预审，最新复核见末节。

观察时间：北京时间 2026-10-29 10:24—10:25。HEAD 为 `fc3a77578bd77efa34d964f692aa1d885ae9c157`，当时唯一已跟踪 diff 为 `checks/NaturalLife.Smoke/Program.cs`。关于动作完成的描述基于该提交的现有 `InteractionGesture.cs`、游戏输入和检查辅助函数，不能当作 input 任务最终实现的复核。

本任务仅新增本文件，没有修改其他文件或历史交接，没有暂存、提交或推送。全部为文件阅读和静态推导，未运行项目、检查、模拟、编译、渲染或 computer-use。

## 新旧档案例核对

对象：`checks/NaturalLife.Smoke/Program.cs` 的 `missing pending field resumes an already accepted mouthful without replaying its portion`，以及紧邻的旧默认值案例。

### 夹具达到的状态是已经入口，而不是仍在预留

- `Fresh()` 只留一只健康、可移动个体，清房间物品，位置/目标为原点，关闭近期自主杂项；`Source` 放一份污染值为 0 的 Meal 或 Water，在 X=.5、Z=0。
- 饭场景初始饱腹 65，水场景初始水分 60，都低于自主需求 70 阈值；其他需求为 90，不会抢先找另一种资源或休息。没有手感知干预低信任选择。
- `Tick(original,.75)` 每步 `.05` 秒，先表现、再世界；世界步长 `.25`。正常路径第一次世界步开始取一口，后续表现步跨过 `.45` 秒入口点，`.75` 秒时这口仍未咽完。Meal 全程 1.6 秒、Water 全程 1.2 秒，因此留有充分的吞咽余量。
- 最关键的是随后先断言 `pending=false`、`EatingRemaining>0`、`Exposures==1`、`source.Portions==0`，全部满足才序列化和删除字段。若几何或调用顺序以后改变、未能真正接受一口，案例会在前置条件失败，不会悄悄把预留状态作为旧档继续测试。
- 实际养分、剩余时间是在这一步后读取，不是按期望常数伪造。第一口后饭/水需求均回到约 84，续接窗口不会因为仍强烈饥渴而主动开始第二份；场景也已无剩余来源。

以上是按当前分支和时序的源码推导，尚未实跑夹具。

### 字段删除与观察点符合目标

共 8 组：Meal/Water × 保留/省略来源几何字段 × 普通/近看。

- 必删字段只有新增 `NaturalIntakePending`；可选再删 GroundIntakeKind、Distance、SourceHeight、SourceId、NaturalBitePause。既有饱腹、水分、暴露、EatingRemaining 和物品份量都保留。
- 当前 bool 缺省为 false；这正是“已经结算过，不应重新当作预留入口”的旧形状。可选几何缺失时，默认距离 `.792`、来源为空等仍走既有验证，而不是补造可再次吃的来源。
- 反序列化后立即 `SaveStore.Validate`，再核对养分、暴露和剩余时间与保存前完全相同，能识别读取阶段重置吞咽或重发养分。
- 之后 80 个 `.05` 秒双入口共 4 秒，每帧核对没有养分新增、暴露仍为 1、资源总份量为 0，末尾要求吞咽结束及状态仍可保存。空来源可以被正常清理掉，所以用总份量而不强求物品列表仍保留原空杯/空饭，是正确的观察契约。
- 普通时间用 `<=` 是为了允许自然食水消耗；不能改成无条件相等，否则正常推进会误报。4 秒普通消耗远小于一口 Meal 的 19 或 Water 的 24，且每帧观察，因此一次正常大小的重复结算不会被此比较掩盖。
- 近看末尾另要求饱腹、水分完全相等，能识别不该发生的被动消耗。它没有只以“没有重复补充”为由放过近看推进被动生活。
- 旧默认值案例补删 pending 后，新增 `!NaturalIntakePending` 断言；其 Fresh 状态没有在吃，故是在验证缺省值，不是删除一个正在等待结算的 true。

没有发现本次断言不当弱化。案例明确标为合成旧形状，不能代替获准历史存档样本的兼容报告；这点现有注释表达正确。

## 完成后持续按住：后续实现必须核对的具体交界

以下是待实现交回时的检查清单，不是声称当前任务已修复。

### 1. 不要用所有动作共用的 Applied 直接判断“已经结束”

`InteractionGesture.cs::ContinueInteractionGesture` 中：

- 食物/水的 Applied 在真正入口、扣一次份量并 BeginIntake 后设 true；它防止同一按住手势第二次入口。
- Care 的 Applied 在有效接触达到 1.6 秒、消耗护理包并实施护理后设 true；再次 Continue 直接返回，不重复用药。
- RepairAccessory 的 Applied 在第一段修补就设 true；完整修补另有 `RepairFinished`，并在 Integrity 到 100 的边界增加一次 CompletedRepairs。不能用 Applied 让半成品修补提前交回或冻结。
- Hold/Lift 等也可能一开始就 Applied=true。新增完成判断必须限于指定动作，不能破坏持续按住、按压和托举。

### 2. 放开姿态控制，不等于结束/重建手势或清掉一次性结算标志

`InteractionIntakeAccepted` 依赖当前手势 Applied，游戏 `ProcessHeldSupply` 在 Continue 返回后读取它，写 `_supplyBiteTaken`。如果内部直接 EndInteractionGesture 后才返回，游戏可能观察不到这次接受标记，剩余物品再次碰嘴就可能重开喂食。应检查这个真实调用顺序，不能只测纯模拟的饱腹终值。

手持物品的 HeldByHand 是“手仍拿着容器”，不等于“角色还必须被动作控制”。完成一口后允许角色活动，不能偷偷归还全部食物、退款已入口部分，或让自主取食吃到仍拿在手里的剩余份量。松手/取消仍由既有 ReleaseSupply 流程处理。

### 3. 咀嚼中、完成回应当帧、回应结束后三个边界分开

`AdvanceInteractionPresentation` 推进 EatingRemaining，并在满足条件时生成独立的 `mouthful-done/swallow` 反应。保留已有“放下空勺不清新吞咽回应”“后来的疼痛优先”的案例。

动作完成的同一帧必须在“仍短暂占用，暂不自主位移”和“已清旧身体姿态，允许位移”之间保持一致，避免重现 I1 的一帧滑动窗口。只在数秒后的终点检查不再占用，不能证明这个边界正确。

清理只能针对该手势自己的反应序号，不得抹掉更新的自主吃喝、求助、疼痛或拒绝。RepairAccessory 的完成分支当前直接调用 Feedback，和普通 Say 的反应序号登记路径不同；如果实现依赖序号清理，要特别检查完成分支是否也登记。

### 4. 护理完成后能自理，不等于所有被护理者立刻能走

Care 不会立即治好脚底或解除 Critical→Recovering 的恢复期。移动案例宜使用 Condition=Active、脚底完整、可行走但确有待护理部位的角色；不能对重伤、脚底不足支撑或固定装置中的个体强求护理后立刻走动，再把失败误认为仍被工具锁住。

在有限资源模式中检查护理包只少一份，照护值不再重复增加；持续按住改到另一部位也不应复用同一次护理结算。中途离开/换部位的有效接触时长重置仍按旧约束，完成释放不应跳过它。

### 5. 修补完整与无须修补的起始状态

完整修补后继续按住，Integrity、RepairProgress、CompletedRepairs 不应重复增加；结束手势只记录本次实际完成/部分完成经历。检查还应区分“一开始就 100，Continue 只提示已整理好”和“本次从不足 100 修到完整”：前者没有实际修补收益，也不应永久锁住自主生活。

重新损坏、开始新手势的案例沿用既有原饰品身份、历史完成次数及新修补进度规则，不因为释放自主行为重置历史。

### 6. 检查辅助函数可能掩盖“仍按住”问题

`checks/Interaction.Continuity/Program.cs` 的 `Feed(...)` 辅助函数最后自动 `EndInteractionGesture()`。它适合现有喂食检查，不能直接拿来证明“完成后还按着也能自理”。新案例应自己 Begin/Continue，并在关键断言前确认手势仍存在。

同文件 `Tick(sim,int ticks)` 是每次 `.1` 秒的步数，和 NaturalLife.Smoke 的 `Tick(sim,double seconds)` 不同。新案例写 80 是 8 秒，不是 80 秒；需要按真实时长设置预期，避免把恢复等待或需求变化看错。

## 本次交付状态

旧档新增案例：可交主对话保留并进入远端执行，没有本次静态发现要求先修。

完成动作实现：只完成上述预审要点，尚未复核最终差异；本任务按要求到此结束。后续报告应写实际机器与完整源码提交，合成案例通过、真实历史兼容和实机动作效果分开陈述。

## 10:50 补充：完成动作核心与 Continuity 实现复核

结论：对以下最终读到的文件版本，没有未关闭的静态发现。该结论不是编译、测试或实机验证通过，也不覆盖 life 尚在处理的无手势 RoomMove 吞咽收尾。

### 版本边界

本轮观察 HEAD：`c6fc7d89795501c26057b991276b4657d62dc122`。完成动作核心和检查当时均为工作区差异；不能只用 HEAD 代表本次所读实现。最后完整重读差异与 hash 时间为北京时间 2026-10-29 10:50:27。

| 文件 | 最后所读 Git blob hash |
| --- | --- |
| src/Yukkuri.Simulation/InteractionGesture.cs | `ae251f6fd7b897a8938d72a13b717e7bddc4e5ae` |
| checks/Interaction.Continuity/Program.cs | `64b11ebb4a407bf651b0d0ded61084acfc6f0147` |
| src/Yukkuri.Simulation/InteractionBehavior.cs | `5477462f10cc544351fe1f4a5946760ca636da27` |
| src/Yukkuri.Simulation/NaturalLife.cs | `ad25c59e6d98ef02e87b8e17e6aa6716461f29e9` |
| src/Yukkuri.Simulation/InteractionRest.cs | `9c0553598afadb3f7f69733005c3a58039658953` |
| src/Yukkuri.Simulation/InteractionPresentation.cs | `64e5daa160d5ae2dd4c87b4b17ecac61e27dc2c5` |
| src/Yukkuri.Simulation/InteractionWorld.cs | `bc2f707bcdae9250546e8246c689a1ab5757fc2a` |
| src/Yukkuri.Game/InteractionRoom.Input.cs | `ee529eb5acf4c1afa20d88e103ffe78346b8c875` |
| src/Yukkuri.Game/InteractionRoom.cs | `f0b0b838e678e77654c08ea72c37d10df6569ab6` |
| src/Yukkuri.Simulation/WorldSimulation.cs | `e5c27a64b7ae32bb7090bba09966ab344eba0cc0` |
| src/Yukkuri.Simulation/WorldState.cs | `d976eaeb79369eb76f2c2a15331263b634c3879d` |

最初读到核心 `0ba369a2bebb1a842b13ee843b4e93be155beb2a`、检查 `65ba5f8cdc59d42b0b5c382970b408e994006c57`。审查期间发生两个实际变化，均已重读：核心去掉独立 CompletionReactionSerial，改用既有 g.ReactionSerial，并补显式括号；检查 Tick(sim,2) 改为 Tick(sim,3)。因此本报告不沿用“登记两种序号”的旧描述，最终实现只维护一个正确归属的序号。若以上 hash 之后再变更，新增差异尚未由本报告审查。

### 真实帧顺序与后续自主进食

游戏先 ProcessRoomGesture/ProcessHeldSupply，再 UpdateInteractionHand，再 AdvanceInteractionPresentation、Advance，最后根据当前 OwnsPose 选姿态。HeldFrame 对应同样的输入→表现→世界→姿态顺序；其夹具没有模拟鼠标命中与手感知，但没有把这两者声称为已验证。

喂食真正入口后 Applied 保持为 true，OwnsPose 立即为 false，画面读取最新身体表现；自主行为仍由 CompletedGestureStillSettling 暂时阻挡。表现步在 EatingRemaining 到零的同一步建立 swallow/.6 秒，世界步随后查询时仍看到收尾，因此不会在正常完成回应当帧先行走动。计时结束后，第一次查询把 CompletionReleased 锁存为 true，之后即使自主进食再次令 EatingRemaining 大于零，也不会重新被旧手势阻挡。

护理与修补等待自己的反应序号及剩余计时；新疼痛产生新序号后，旧动作不再延长占用。完成后 Continue 返回当前表现，不再强行覆盖旧 nursing/eat/sip。ResetInterruptedNaturalSearch、UpdateInteractionRest 和世界动作分支使用同一阻挡判断；自主开始吃喝之前已经经过释放判断，之后表现步也不会因旧完成动作 OwnsPose 而拆掉新来源。

完成标记仅适用于 FeedMeal/FeedSweet/Drink、Care、RepairAccessory。修补以 RepairFinished 判断，不会因第一段 Applied 就放弃半成品；开始时已经完整的饰品直接标记释放，没有假造修补收益。完成后再损坏也不会复用旧按住手势自动修好。Hold、Press、Lift 仍走原 OwnsPose 分支，完成判断对它们为 false，其手限制、持续施压、托举与松手逻辑未改。

### 未入口、已入口及松手边界

未入口手喂离开嘴时不控制姿态，允许自主找食物。再次接嘴若已有自主预留、已入口的一口或 swallow 收尾，Continue 先等待并清零 ContactElapsed；OwnsPose 同时为 false，故不会在随后表现步调用 DetachNaturalIntake 撤销原来源。原一口和吞咽回应结束后才重新积累 .65 秒驻留。Begin 本来就拒绝嘴里已有一口；本次防线处理的是已存在手势离嘴等待后再返回的真实路径。

已入口手喂保持 Applied，不内部 End/重建手势。ProcessHeldSupply 在 Continue 后读取 InteractionIntakeAccepted 的顺序仍成立，_supplyBiteTaken 能记录一次已接受。实际来源扣一份、库存不重复扣、HeldByHand 不被完成释放顺带清除；只有明确 ReleaseSupply 负责放下剩余容器。取消已入口手势不退食水，取消未入口手喂也没有产生份量扣除。自主预留仍由原来的 DetachNaturalIntake 负责退还，已入口来源只脱离几何引用而不退款。

修补完成直接 Feedback 后，现在更新 g.ReactionSerial，所以立即 End（正常或取消）能够清掉自己的 accessory-watch，后来的 Poke 则因序号不同保留。护理完成 Say 已登记同一序号；完成后换部位 Continue 提前返回，不重复用药也不覆盖新疼痛。手喂立即松开清理自身反应仍由 EatingRemaining 的表现回退保持咀嚼；已生成的 swallow 是更新序号，旧手势结束不会清掉它。离开接触面的通用清理仍只处理自身序号。

### 新案例及发现的误报

初始版本的 `releasing or cancelling completed repair clears only its own body response` 在 Begin/Continue/End 后执行 Tick(sim,2)，即只 Advance 共 .2 秒。Fresh 的 TickRemainder 为零，Config.TickSeconds 默认 .25；这些动作本身不推进世界。因此无新疼痛的两组 c.X>0 必定会在尚未产生世界更新时失败，不能据此判产品仍被锁住。最终版本改为 Tick(sim,3)，.3 秒跨过一个世界步，保留原位置和姿态断言，没有弱化所查契约。此问题已关闭。

其余新增夹具和断言的静态核对：

- 三种手喂动作分别对食物、水、休息需求观察；每帧要求手势仍存在，首次位移检查实际渲染姿态，持续核对已接受标记、手持容器与份量，避免 Feed 辅助函数自动松手掩盖问题。后续自主入口和来源份量下降均有观察。这里证明到达自主取食阶段，不能单凭份量下降声称后续每一口都完成结算。
- 自主到手喂的 8 组组合先靠实际表现/世界推进抵达 pending=true/false，而非手写预留状态；分别检查地面来源零份、手持仍两份、原来源不脱离、一次暴露。吞咽后先再走 .6 秒不接受，再给最多 .2 秒跨 .65 阈值；此前等待不断清零驻留，所以窗口按现有顺序合理。后续暴露总数为二、手持仅少一份，覆盖重复入口。
- 护理夹具 Health=80、脚底完整且 Active，确有护理需求又能行走；有限药包只少一份，CareContinuity 只增加 5，不以重伤者立即恢复行走作要求。
- 修补分别从 90 与 100 开始，区分本次完成一次和没有实际修补；保持进度/次数不重复，再损坏仍不重启。单独立即松开案例与新疼痛案例补上仅观察 12 秒终点不足以覆盖的边界。
- 12 秒观察窗口内，低食水需求与两米目的地能维持真实自主需求；有限来源五份不会被满足需求所需的几口吃空而导致末尾 Find 空引用。休息夹具能到垫旁并在剩余窗口增能量，没有把站立等候当作休息。

本轮只更新本交接文件，无源码修改、暂存、提交或推送。明确静态复核已完成；远端执行和 life 后续 RoomMove 改动不在上述已审版本结论内。

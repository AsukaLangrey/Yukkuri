# 首轮整合后二次静态审查

最终静态结论（10:04）：原 R1—R6 的修复已接入当前工作区；本次发现的 I1 及其 Nuzzle 首次回应当帧窗口也已在最新修改中收口。当前未再发现本轮指定范围内的实质接线遗漏。未发现所读新增检查中的明显语法、签名或夹具逻辑错误；这不是编译或测试通过，仍待远端运行及效果观察。

## 10:04 最终复核：I1 静态收口

复读基线仍为 `4a7acd621c2b37d201e0cf196aab63ed83a6eb04` 加未提交修改，仅核对 `InteractionGesture.cs` 与对应 `Input.Intent` 案例，没有扩大范围。

- 首次产生 nuzzle-ready 时，先设 `NuzzleWelcomed=true`、`FeedbackAt=Elapsed`，随后返回。此时新增 `Elapsed-FeedbackAt < NuzzleReplyHoldSeconds` 为真，且接触仍在脸侧，因此 OwnsPose 与阻止自主位移在同一帧都为真。原先“新姿态产生了但身体已能走”的窗口已关闭。
- `.35` 秒短回应结束后的 Continue 先更新 Elapsed，再判断 OwnsPose；静止条件下它转为 false。若反应序号仍属于原手势，前置清理把旧姿态与时长一起清掉，随后普通世界步才可自行移动。静止分支不会再次调用 Feedback，因此不会每帧续长回应。
- 新保护未移除 `!g.Refused`、反应序号匹配或动作范围条件；急停伤害/拒绝、新的独立反应以及 Hold/Press/Lift 的边界保持前次复核结论。
- 对应检查已去掉 `n>=12` 豁免；第一次看到 nuzzle-ready 就要求 OwnsPose 为真，之后每个不占用姿态的帧都检查没有残留摩擦姿态，末尾仍要求 8 秒内实际走动并吃到食物。没有发现本次断言接线的明显逻辑错误。

I1 状态更新为“源码修改已静态核对，未运行”。`.35` 秒是待远端观察的衔接初值，不是已确认手感或画面最佳值。以下保留此前发现和中间复核过程，便于追踪；其中“尚未闭合”仅描述当时版本，不再代表最终工作区结论。

## 10:01—10:03 追加复核：清理保护正确，Nuzzle 仍有首次回应当帧窗口

已读主对话新增的静止姿态清理，以及 Nuzzle 直接 `Feedback` 后记录 `g.ReactionSerial` 的改动。以下仅更新 I1 状态，不把原来的两秒多残留说成当前仍完整存在。

**急停、拒绝和新反应保护没有发现误清路径：**

- 清理只在 `!g.Refused` 时进入。急停分支当次仍正常调用 `Distress`、写伤害/敏感、设置 `g.StopImpact=true` 和 `g.Refused=true`，再产生 slap。后续静止输入跳过清理，不回滚伤害，也不清拒绝。
- 第一次急停帧即使先清掉上一条温和抚摸姿态，随后仍会产生新的 slap，不会吞掉这次冲击。已经拒绝后仍允许一次真实急停的现有例外没有被删除。
- 外部 Poke、自然吃喝或求助产生新 `ReactionSerial` 后，与手势记录不相等，不会进入这段清理。新增 Poke 案例对应的入口没有自动结束旧手势，因此它确实检查到这个保护，而不是靠“手势已经没了”绕过断言。
- Hold/Press/Lift 不在清理动作集合里；Nuzzle 邀请未满 `.45` 秒时仍 OwnsPose，初始邀请不会被这段清理提前取消。

**剩余 I1 窗口：首次生成 Nuzzle 回应的同一帧。**

当前清理位于 `ContinueInteractionGesture` 的 switch 之前；首次达到邀请时长时，还没有本次 nuzzle-ready 反应可清。随后 Nuzzle 分支又新写 `ReactionPose="nuzzle-ready"`、时长 2.2 并返回，而 OwnsPose 已因接触时长达到 `.45` 且 Motion=0 为 false。房间在同一帧按“继续手势 → 表现步 → 世界步 → 显示”推进，因此该帧若恰好跨过 `.25` 秒世界步，就能先走出一步，但显示仍关闭跳步。下一次 Continue 才清该姿态。

可直接在当前新增 Nuzzle 夹具中逐帧观察：它每帧 `.05` 秒，在跨过邀请时长时若与世界步重合，会走出 `.18` 世界单位后才在下一帧交接姿态。为避免依赖浮点累计恰好落在哪一帧，可以在即将触发 nuzzle-ready 前安排 `TickRemainder=.20`，然后执行 `.05` 的 Continue/表现/世界步；该值在当前合法范围内。在检测到 X/Z 实际变化的同一帧断言姿态已经可行走，而不是延后到固定帧数。

当前新断言使用 `if(n>=12)`，只从约 `.65` 秒后检查，故能覆盖原来长时间残留，却刻意或无意地略过上述首次交接。建议调整清理/短回应与自主位移的先后关系，使它们在同一帧一致；可以让短回应暂时继续占用直至明确释放，也可以在返回新回应前做针对该手势的交接，但继续保留拒绝及反应序号保护。只给现有前置清理增加条件无法清掉它之后刚生成的姿态。

未运行任何案例。原 I1 的长残留已在静态上解决；剩余范围缩小为首次回应与世界步同帧的显示交接，仍建议在整合提交前收口。

审查时间：北京时间 2026-10-29 09:56—09:59。基线为 `4a7acd621c2b37d201e0cf196aab63ed83a6eb04`，审查对象是该基线上的当前未提交 diff。最后读到的退出逻辑已包含主对话补充的“仍然退出，不保存”按钮。定位以方法和条件为准，行号仅对应这次观察，后续修改需重新对齐。

本任务只写本文件；没有改历史 `review.md`、产品、检查或测试要求。没有暂存、提交、执行测试、编译、游戏、模拟、渲染或 computer-use。下列为静态推导，未代跑远端。

## I1 · P2 · 归还自主行为时残留接触反应姿态，走动仍会关闭跳步

位置与证据：

- `src/Yukkuri.Simulation/InteractionGesture.cs:39` 的 `InteractionGestureOwnsPose`：Nuzzle 的脸侧接触满 `.45` 秒且不移动后不再占用姿态；Stroke/Clean/Dry/CleanAccessory 没有移动也不再占用。`InteractionGestureBlocksAutonomy` 随之变为 false。
- 同文件 `ContinueInteractionGesture` 的 Nuzzle 分支（当前约 255—260 行）：接触满邀请时长后的第一次回应调用 `Feedback(...,"nuzzle-ready")`。这是正常静止邀请也会产生的状态，不依赖此前有摩擦。
- `src/Yukkuri.Simulation/InteractionWorld.cs:76` 的 `Feedback` 把 `ReactionPose` 设为该姿态，`ReactionPoseRemaining=2.2`。
- `src/Yukkuri.Simulation/InteractionPresentation.cs:45` 仍优先返回未过期的 `ReactionPose`；其新增过滤只针对已经吃完的 eat/sip，不处理已经归还自主移动的 nuzzle-ready、stroke 或 wipe。
- `src/Yukkuri.Game/CreatureActor.Interaction.cs:84—98` 的 `canHop` 只允许 idle/guard/stomach/careful-step。nuzzle-ready/stroke/wipe 会使 `_hopMotion.Advance(...,canHop:false,...)`，但角色主坐标已经直接更新为模拟的 X/Z。因此模拟开始走时，显示仍使用接触姿态且关闭正常跳步和对应朝向处理。

最小触发条件：普通时间，单个可移动角色，饱腹 35、水分和精力 90、信任 60，饭放 X=2。开始 Nuzzle，持续真实脸侧接触、Motion=0，跨过 `.45` 秒。下一次普通时间步允许自主去找饭，同时 `InteractionPresentation.Pose(c)` 仍是 `nuzzle-ready`、反应时间仍大于零。角色到嘴部距离之前的一段移动会走上述“坐标改变、跳步关闭”路径。新检查 `idle rubbing tools release autonomous needs after a brief nuzzle invitation` 的 Nuzzle 夹具本身就能覆盖这个条件，但目前只断言最终 X、饱腹及姿态占用标记。

另一条实际路径是轻摸或擦洗先产生 stroke/wipe 反馈，再保持接触但停止移动。新规则归还自主需求时，之前的 2.2 秒反应也没有同步交接。它不是食水份数重复结算问题，而是本轮新增自主移动与原显示优先级的交界问题。实际画面仍待远端确认，不能写成已目测滑行。

建议：在接触动作交回自主行为时明确处理该手势留下的短反应，或在短邀请回应结束前暂缓自主位移，再一起归还。不要简单清空所有 `ReactionPose`，也不要把 nuzzle-ready 无条件加入能跳步的姿态列表；这会误清其他伤痛/求助或保留不合适的身体变形。应按反应来源/序号识别真正属于该手势的姿态，保留 Nuzzle 的短邀请与 Hold/Press/Lift 的持续支撑。

需补的精确回归：

1. 静止 Nuzzle 邀请结束后，一旦 X/Z 开始变化，应已交回可行走的显示姿态；或者在回应尚需保留时保证 X/Z 暂不变化。不能只在 8 秒后的终点断言姿态占用已解除。
2. Stroke/Clean/Dry 先真实移动产生反馈，再 Motion=0，检查同一交界。
3. 在交界附近触发新的伤痛/求助或已经入口的咀嚼，确认清理只处理原手势姿态，不抹去新反馈，不退款、不重复暴露。
4. 由 Alice/dot 在允许的实机环境核对实际跳步/朝向，再注明完整源码提交与未测项。

## 原 R1—R6 接线复核

| 原编号 | 当前静态结论 |
| --- | --- |
| R1 保存失败仍重新开始 | `SaveRoom` 返回 bool；普通保存失败调用错误弹窗，重新开始在 `!SaveRoom` 时直接 return，替换 Sim 位于成功分支之后。退出失败保留当前世界，并提供返回/明确不保存退出两个按钮；未自动丢弃进度，也不会因不可写而完全失去退出入口。 |
| R2 成功跟随挡住休息 | `UpdateInteractionRest` 产生休息请求后，在 `ActiveCue==null && RestRemaining>0` 时清 `FollowPlayer`。首次普通步可能尚未恢复精力，下一步可真正休息；这不改变未结束训练的成功记录。新增检查覆盖有/无手、有/无垫、读档组合。 |
| R3 失焦产生在场的手 | 失焦回调取消手势，并立即清 Present、Until、RegionId 三项；`_Input`、`_UnhandledInput`、帧手势处理、手感知刷新均有 `_windowFocused` 门槛。暂停/近看先返回不再妨碍失焦清理，因为清理在事件本身完成。落点/悬停亦隐藏。原审查中“保留三秒”的中间方案已被替代，不适用于这次版本。 |
| R4 跨区收物品反馈 | 普通目击循环已加同区和 `CanSeeResource`；宝物主人分支也检查视线。原来另一区同坐标的个体不会再收到该反馈。 |
| R5 null 物品绕过备份 | 房间物品结构检查移到个体来源引用查询之前，null 会先触发 `InvalidDataException`，可进入现有备份回退。新增合成损坏检查覆盖直接拒绝、加载备份、禁止覆盖和原文件保留。 |
| R6 当前训练缺学习条目 | 增加非空 ActiveCue 必须在 Learning 中存在的约束；已有条目本身仍先验证非 null 和数值。新增检查同时保留 ActiveCue=null、空字典合法的默认状态。 |

以上是入口和控制流核对，不是宣称六项运行复现均已通过。

## 其余指定交界的核对结果

声音：`PlayRoomReactionVoice` 在本帧 actor.Visible、仍有有效反应/台词的角色中选候选；间隔、无素材和未选中的候选不会提前写入 `_heardReactions`。实际调用播放后才记录选中候选。静音分支有意把当前反应记为忽略，不在解除静音时补播，这是整合文档中明确的策略，不当作“漏播”另列问题。别名 `OilExpression` 避免了 Godot 与模拟枚举同名；没有发现这段明显的名称冲突。尚未听音、验证播放成功或声音时序。

资源搜索清理：`ResetInterruptedNaturalSearch` 只清 ResourceTargetId、ResourceBestDistance、ResourceStuckSeconds、ExploreUntil、HelpWaitingUntil，以及残留 help-look 的时长；没有改 GroundIntakeSourceId、NaturalIntakePending、EatingRemaining、Portions 或 Digestion。它位于表现步原中断/结算流程之前，不会自己退款。真正退款仍由 `DetachNaturalIntake` 在 pending 为真时进行，并先清 pending，后续步不会再退同一份；入口后的 `CompleteNaturalIntake` 仍先清 pending 再登记养分/暴露。未发现新增清理与原份数守恒直接冲突。

静止工具范围：当前差异保留 Hold/Press/Lift 的占用；Nuzzle 的初始脸侧邀请仍有 `.45` 秒条件。新 `Input.Intent` 对普通饥饿能自行吃饭和三类持续支撑的对照本身没有明显夹具错误；但它未检查 I1 的中间姿态。切勿以该终点断言通过替代中途跳步衔接。

新增检查源码：读取了 ActorGeometry.Smoke、Input.Intent、NaturalLife.Smoke、Simulation.Smoke、Save.Compatibility 的 diff 及相关辅助函数。命名参数 `previousLocalPoint` 与当前追踪器签名对应；合成坏档的 `Reject` 只接受受控数据异常，没有把空引用等程序错误吞成成功；互动兼容推进改为 1200 次 `.1` 秒、先表现后世界，原字段保留断言没有放宽。没有发现足够确定的新增编译/夹具问题，不能据此声称实际可编译或测试通过。

远端仍需按已有交接与 `testing/request.json` 跑 UI 保存失败、焦点、声音及生命行为案例；本轮没有用阅读替代这些执行证据。主对话整合 I1 后应让报告对应最终完整提交。

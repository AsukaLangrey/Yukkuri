# 第二轮美术与角色显示交接

结论：已修补两处显示优先级问题（排泄覆盖明确动作、重伤体形被重置），并在00:22扩权后补修 R-ACTOR-1：半空暂停切近看会抹去跳跃变换。产品增量在 `CreatureActor.Interaction.cs`、`ActorHopMotion.cs`；新增一项相关几何检查源码但未运行。保留接手的排泄跳跃衔接和选中圈移除，`CreatureActor.cs` 的既有差异原样保留。没有编译、运行、测试、渲染、预览或桌面操作。

基线为本轮 PLAN 的 `f02b0fcfbb3e9649b5f94d35201c4c22661158b9`，本次修改尚在共享工作区，不能用该基线或此前其他聊天的编译记录冒充验证结果。

## 1. 排泄姿态覆盖明确动作

触发：在小院排泄开始后进入近看，再按压或挤压。普通世界步尚未清除排泄状态时也存在短暂窗口。

静态依据：

- `WorldSimulation.Advance` 在 CloseUp 时返回，`TickPhysiology` 只在普通世界步里调用。接手时 `Physiology.Active` 可以在近看继续保留。
- `InteractionRoom._Process` 仍处理手势和 `AdvanceInteractionPresentation`，并在手势拥有姿态时把 `_gesturePose` 传给 `RefreshInteraction`。
- `RefreshInteraction` 先通过 `pressure`、`squeeze`、`touch-press` 等分支计算真实形变，但接手的尾部 `if(life.Physiology.Active...)` 又无条件写入 `(1.04,.91,1.04)` 和 urinate/defecate 标签。这不是只有标签不一致，缩放也被覆盖。

修改：排泄只在传入姿态为 idle、非死亡/重伤、非托举/下落/仰躺/翻滚/休息/进食时，转成局部 urinate/defecate 姿态。缩放移入原有姿态 switch；移除尾部覆盖。保留原排泄缩放值，不调整风格。guard、stomach、受伤短反应和明确接触动作优先，排泄计数、计时及产物均不由显示层改写。

转换在 `ActorPoseClock.Advance` 前完成，所以排泄开始/结束会进入既有姿态切换流程。每帧仍从基础变换重算，结束后由 `ActorPoseSmoothing` 过渡恢复，未新增持久缩放状态。保留原作者的 naturalHandoff：跳跃中开始排泄时从上一帧已呈现变换衔接，不直接抹去跳跃偏移。`CanSelfMove` 已排除 Physiology.Active，本轮没有重复修“排泄时继续自主跳跃”。

## 2. 重伤体形被默认小院重置

触发：默认小院经 `Hurt` 进入 Critical，或读取含重伤角色的档案；不要求排泄状态同时存在。

静态依据：`CreatureActor.Refresh` 对非 Active 状态设置 `(1.13,.63,1.07)`，但 `RefreshInteraction` 调用它后又把 `_visual.Scale` 重置为 BodyScale，仅单独处理 Dead。`InteractionPresentation.Pose` 没有 Critical 专属返回，因而 idle/残余反应会盖掉重伤体形。

修改：在小院姿态计算后、平滑处理前，为 Critical 恢复基础刷新已使用的 `(1.13,.63,1.07)`，并设 PoseTag=critical。只覆盖体形和标签，保留前面算好的仰躺、翻滚、下落、支撑相关旋转及偏移，随后继续原有地面/软垫/托举支撑处理。Dead 仍走原死亡分支，未改死亡归一化或生命值规则。

解除 Critical 后这条覆盖自然停止，恢复中的体形继续由现有小院姿态决定。未把 Recovering 全部强制当作死亡或重伤，也未改治疗入口。

## 已核对且未新增问题

- 选中圈：接手的 `_ring` 字段、构造和两处 Visible 赋值已成套移除，源码搜索没有遗留 `_ring` 引用。`InteractionRoom.ContactAt` 调用 `CreatureActor.RayContact`，后者明确测试皮肤、头发、饰品和装置，并不测试选中圈。角色点击/双击/托举的入口未被删除；选择按钮、SelectedId、右栏角色名仍在。是否更容易辨认当前对象属于实机观感，本轮未评价，也未加回圈。
- 托举/下落：排泄姿态入口显式排除；真实高度仍取 life.Height；托举手由最终体形的最低点计算位置。重伤覆盖没有绕过支撑代码。
- 死亡：排泄映射排除 Dead，既有死亡体形和 intake 禁用保留，未改死亡装置归一化。Critical 体形不会覆盖 Dead。
- 重开/读档：BuildActors 对旧 actor QueueFree 并新建 CreatureActor，姿态平滑、跳跃状态和时钟是实例字段；没有新增跨角色静态缓存。近看切换保留同一实例，但变换目标每帧重算，不需要凭猜测手动清空平滑状态。
- Food/Tool 四份授权文件接手时无差异，本轮未改。第一轮勺沿中点和饭团底面64段已在基线中，不重复记作第二轮成果。没有接触 Vitals 的排泄物构造或其他聊天源码。

## 跨范围缺口：交生理负责人确认

接手时生理取消仅发生在 `TickPhysiology`。近看暂停普通世界步后，身体动作能继续处理，但排泄 Active 未必立即清除。本次只保证明确动作不被排泄形变覆盖，不能据此宣称生理动作已经取消；若松手后状态仍 Active，显示会如实回到排泄姿态。

请协调者结合 physiology 的本轮最终差异确认：在近看开始有效身体互动、抓取/下落或变为重伤/死亡时，是否应在可推进的交互入口中立即取消排泄而保留尿意/便意。不得借取消逻辑推进暂停的计时或生成排泄物。本任务未修改生理源文件，不假称已解决计时/产物层的问题。

## 远程待测（全部未运行）

按 testing/PROTOCOL.md 写实际机器、最终整合后的完整源码提交及未测项。最终集成与本机旧编译分开记录。

1. 正常尿/便开始→持续→结束，包含走动跳跃时开始：没有跳变或残留蹲缩，结束后恢复自主行为。guard、stomach 和受伤反应期间优先保留对应姿态。
2. 排泄中进入近看，分别按压、挤压、触摸、翻滚、仰躺、抓起、抛出；明确动作的形变和标签不被 urinate/defecate 覆盖。松手/中断后的生理状态按负责人最终规则核对。
3. 活跃→Critical→治疗后的 Recovering，以及 Critical→Dead：重伤体形按原 `(1.13,.63,1.07)` 过渡，死亡使用原死亡体形；另查重伤时托举、仰躺、翻滚/下落的旋转、支撑手与地面接触。不得用截图标签代替真实体形判断。
4. 排泄/重伤状态下暂停、近看切换、改选对象、读档和重开：不串对象、不留下旧缩放；暂停仍保持现有动画暂停语义。
5. 去圈后依次点击两只角色、双击近看、点击皮肤与饰品、抓起放下，确认右侧名字和真实接触对象一致。不要把没有圈当作命中失败。

前两项姿态优先级修补没有新增检查源码：它们依赖 Godot 的最终 actor 变换，纯模拟断言无法覆盖最后写入 `_visual.Scale` 的层次。建议远程增加实际 actor 的姿态优先级断言：Physiology.Active+idle 为排泄，Physiology.Active+pressure/squeeze 保留手势，Critical+idle 为 critical，Dead 为 dead，结束后回到正常；同时读取真实缩放并等待既有平滑收敛。旧实现在排泄与手势冲突及 Critical 情况应与新实现产生不同结果。

## 00:22追加：R-ACTOR-1 暂停切近看

已读 `actor-review.md` 完整证据链。半空暂停→Tab近看时，旧 `!close` 令 canHop=false，ActorHopMotion.Advance 在 paused 之前按 !allowed 重置，末尾也丢失跳跃叠加；平滑器只持有叠加前的基础变换，故角色突然落回模拟位置。未改动 Advance 的重置顺序。

修补：

- `ActorHopMotion.AllowsView(close,paused,wasPresented)` 仅集中表达视图条件：普通小院允许；暂停中若上一运行帧已经呈现跳跃，则进出近看也保留这个显示资格。它不引入新时间或新实例状态。
- `RefreshInteraction` 的 canHop 同时检查该视图条件和原有 CanSelfMove、托举/下落/仰躺/翻滚/休息/装置/高度/速度/姿态条件。死亡或真实支撑变化仍产生 allowed=false，按原逻辑 Reset；大于1.5的位移突变也保留原重置规则，没有简单把 paused 提前。
- 暂停进出近看时，Advance、朝向锁定、最后的跳跃形变与位置叠加都使用同一 canHop，因此保持被冻结的完整跳跃呈现，而非只保留时钟。既有平滑器的基础状态、hop.Frame、hop.Position、Heading 都不推进；之后的地面/装置支撑仍照常处理。
- 恢复运行且仍在近看时，增加 close 为平滑交接条件：在 Advance 清跳跃前，把上一帧完整位置/缩放/旋转交给 BeginFrom；随后 hop 停止叠加，平滑器朝近看姿态收敛。恢复运行时已回小院则继续原冻结跳跃，不重启跳跃年龄。

新增检查源码：`checks/ActorGeometry.Smoke/Program.cs` 中 “paused camera changes preserve an airborne hop but not a real support loss”。先明确建立 Launch 与 Touchdown 之间的半空帧（Lift>.1、Travel介于0和1），反复切换视图，要求完整 HopFrame、位置、朝向不变；再查恢复近看时清跳跃、既有平滑器承接并收敛；另保留真实 allowed=false、位置突变和未初始化近看的边界。若把生产 AllowsView 退回旧 `!close`，第一次暂停进近看便会清 Active，断言能区分旧错/新对。

这项检查只覆盖共享视图资格、跳跃状态和现有平滑器；没有实例化 Godot actor，也不能替代实际 Tab/UI 接线或最终视觉变换检查。源码已保存，未编译或执行，没有声称旧实现实测失败或新实现通过。

补充远程案例：在真实半空帧记录 actor Position+VisualMotionOffset、视觉 Scale/Quaternion，空格暂停后反复 Tab 进出近看，摄像机可以变化但角色最终变换不得变化；分别在近看/小院恢复运行检查平顺衔接。另覆盖暂停期间发生真实死亡/托举/高度或位置变更的受控状态案例，确认不是无条件冻结；不能用已经落地的帧替代半空案例。

已交回。最终产品增量为 `CreatureActor.Interaction.cs`、`ActorHopMotion.cs`，新增相关检查源码在 `checks/ActorGeometry.Smoke/Program.cs`；保留 `CreatureActor.cs` 的原作者去圈差异。未暂存、提交、推送、重置或新建任务。

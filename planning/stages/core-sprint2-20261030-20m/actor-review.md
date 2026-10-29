# Actor 独立静态复核

结论：本轮 art 的排泄优先级、Critical 体形及追加 R-ACTOR-1 修补均已独立静态复核，未发现阻止限定差异整合的问题。先前发现的“跳跃中暂停后按 Tab 近看会丢失跳跃变换”已由 art 修补源码；本文件末节记录续审与稳定哈希。全部仍未编译、运行或渲染，不能称实际画面通过。

按协调者通知，在 art.md 声明交回后重新读取稳定源码。只新增本文件，没有修改 Actor、音频或任何产品/检查源码；没有编译、运行、模拟、渲染、截图或桌面操作，没有暂存/提交/推送。

## 稳定快照

基线 `f02b0fcfbb3e9649b5f94d35201c4c22661158b9`；以下为已读工作区文本的 SHA256，不是运行证据：

- `src/Yukkuri.Game/CreatureActor.cs`：`E9DEF26492113791BA0C04951D6D7675F4BC8AB97C3909587612CB6E6D01CF19`
- `src/Yukkuri.Game/CreatureActor.Interaction.cs`：`4BA309A6E716731EB0D3A4C03375A73F40150FCF47A6C915AE1A41164588B4F1`

独立读了实际调用入口、InteractionPresentation、ActorPoseClock、ActorPoseSmoothing、ActorHopMotion、ActorNaturalPose、支撑与悬挂刷新、Critical/Dead 状态变更，以及本轮生理取消接线。没有只依赖作者交接推断正确性。

## 两处新增修补的复核

### 排泄、按压和疼痛

默认 `_Process` 先处理手势，再 `AdvanceInteractionPresentation` 和普通世界步；选择 InteractionPresentation.Pose，若手势仍拥有姿态则用 `_gesturePose`，最后调用 RefreshInteraction。

RefreshInteraction 的排泄映射现在只接管 `pose=="idle"`。pressure/squeeze/touch-press、guard、stomach、进食、休息等明确姿态不会被换成 urinate/defecate。排泄缩放放在普通 switch 中，不再于尾部覆盖手势形变。本地变量 pose 在 ActorPoseClock 前转换，开始与结束会走原有换姿计时。

每帧先将旋转、位置与缩放归零/基础尺寸，再算姿态目标；没有累乘上帧的排泄缩放。ActorPoseSmoothing 保存的是过渡状态，排泄结束后目标恢复，按既有指数插值收敛，不是永久残留。不能在远端只看刚结束一帧就要求立即精确回到1倍；应同时检查目标与平滑收敛。

当前共享源码的 InteractionBehavior 已在表现入口及普通自主入口调用 CancelInterruptedExcretion，近看中的有效身体互动不再只能等普通时间清除 Active。该方法由生理聊天维护，不在本次 Actor 修补内；最终整合必须包含这些依赖，不能仅凭显示层正确声称生理中断已完成。

### Critical、Dead 和每帧复位

Refresh 原本设置非 Active 的塌伏体形，但 RefreshInteraction 会重置基础变换。新增 Critical 覆盖位于小院自身姿态计算之后、平滑之前，恢复相同 `(1.13,.63,1.07)*BodyScale`，解决了这次覆写。

只覆盖缩放和 PoseTag，不清除已经计算的仰躺/翻滚/下落旋转与偏移；之后仍经过平滑、地面/软垫抬升、支撑手和悬挂刷新。Critical 不能 CanSelfMove，正常状态不会走后面的 canHop 分支再乘一次跳跃缩放。

Dead 在排泄映射中显式排除，并走既有独立死亡分支 `(1.12,.64,1.08)`；Critical 判断不会再次覆盖 Dead。死亡入口会清 Speech，InteractionGestureOwnsPose 排除死者，手持死亡的支撑归一化也保留。没有发现本轮新增排泄或 Critical 逻辑让死亡恢复活体形变的路径。

Critical 治疗后成为 Recovering，覆盖分支自然停止，由当前小院姿态作为目标。这是作者明确保留的恢复表现，不把所有 Recovering 强制塌伏。静态核对不能说明过渡时长或观感合格。

### 托举、悬挂与暂停

排泄映射排除 Carried/Falling/Supine/Rolling；实际高度仍在 actor Position 中取 life.Height，托举手按最终体形最低点定位。Critical 覆盖未跳过此定位。

悬挂绳端由 `_visual.ToGlobal(Anchor)` 经过支架局部转换计算，因此新的体形会同步绳端。正常悬挂最低高度由现有 `.35*BodyScale` 控制，幼体 `.73` 时也高于 `.25`；本轮生理中断按高度阻止 Active，正常入口下不会把悬挂变成地面排泄。低矮 Box/Frame/Wrap 是否允许排泄沿用生理规则，没有在本次显示改动中另加禁令。

不改变状态或视图的普通暂停路径中：基础 Refresh 的 delta=0、PoseClock 不增加年龄、PoseSmoothing 不推进插值，当前跳跃在 allowed 不变时也暂停。本轮新排泄目标和 Critical 目标没有新增独立的时间推进器。涉及视图切换时存在下面单列的旧问题，不能宣称所有暂停切换都冻结最终变换。

### 去选中圈

CreatureActor.cs 的去圈差异来自既有试玩聊天，art 没重写。字段、构造及两条显示赋值均已删除，检索没有剩余 `_ring` 引用。默认 RayContact 依据皮肤/头发/饰品/装置求交，不依据圈；不能把移除圈直接当成输入命中丢失。角色可辨识程度仍待远端观察。

## R-ACTOR-1：暂停后切近看会抹去半空跳跃（P2，原发现；现已补源码）

默认可达触发：在小院等选中角色跳跃至半空，按空格暂停，然后按 Tab 或点“靠近看看”。不用修改状态或启用隐藏入口。

确定的调用链：

1. `InteractionRoom.Input.cs:176-177` 允许非模态状态下空格暂停、Tab 切换近看；Tab 不以 Settings.Pause 为禁入条件。Ui 的 ToggleClose 也没有取消 Pause。
2. `CreatureActor.Interaction.cs:89` 的 canHop 首项为 `!close`，进入近看后变成 false；`103` 行仍以 `paused=true, allowed=false` 调用 ActorHopMotion.Advance。
3. `ActorHopMotion.cs:26` 在检查 paused 之前，对 `!allowed` 调用 Reset；Reset 清 `_active`、`_age` 并把位置改到模拟目标。下一行的 `if(paused)return` 来不及保留半空帧。
4. `CreatureActor.Interaction.cs:94` 的 BeginFrom 衔接要求 `!paused`，普通 idle 跳跃也不满足 naturalHandoff。没有把前一帧含跳跃的最终变换转交给平滑器。
5. 平滑器记录的基础目标在跳跃叠加之前；该帧 paused 使它保留基础目标。末尾 `_hopMotion.Active` 已为 false，不再叠加原有 lift/横向滞后/缩放，冻结在半空的角色因此瞬间回落并可能横移到模拟位置。

这些重置与调用顺序均在本轮 art 差异之外，不归因于新排泄或 Critical 修补。暂停后不切换近看不会触发这条重置链。

建议由协调者另行授权最小修补：视图变化不应消耗或抹掉暂停中的最终跳跃变换；可在 actor 层保留被冻结的最终呈现，恢复运行时再按近看/小院状态做平稳交接。不要仅为修这个案例取消真实死亡、支撑变化或未初始化状态所需的重置，也不要推进模拟时间。涉及 ActorHopMotion 的策略调整目前不在本复核写权限内。

有意义的远端断言：暂停前后记录 actor 世界位置加 VisualMotionOffset、视觉 Scale/Quaternion；暂停状态下进出近看后应保持角色本身的最终变换（摄像机变化另算），恢复运行再正常落下/过渡。旧实现会因 Active 被清空而失去抬升。须选在 Launch 与 Touchdown 之间的实际半空帧，不能在已着地帧验证后声称覆盖。

## 远端待测与整合结论

本轮两处 art 修补可按限定范围整合，不能把本审查等同于画面验收。R-ACTOR-1 后由协调者交 art 修补，未由本只读任务写产品源码，续审见下节。

获授权 Alice/dot 应使用最终完整源码提交，记录机器及未测项，覆盖：正常排泄开始/结束、排泄中明确手势、Critical→Recovering/Dead、重伤托举/仰躺/悬挂、普通暂停，以及 R-ACTOR-1 的半空暂停切近看。应核对真实变换和支撑接触，不只读 PoseTag 或 UI 文字。全部未运行。

## R-ACTOR-1 修补续审：无静态阻断，待远端

art 追加修补并在 art.md 声明最终交回后，已重新读取实际方法、完整 diff、新增检查及其项目引用。稳定快照：

- CreatureActor.cs 仍为前文 `E9DEF26492113791BA0C04951D6D7675F4BC8AB97C3909587612CB6E6D01CF19`。
- CreatureActor.Interaction.cs：`7B02C454C46CA6DF78BE284D8D485DA4E402F1E1D543497ED8555A1B16BD6488`
- ActorHopMotion.cs：`B5FCA37EAD2950BBA147F97B9A049AC01772704A2C29F420F7093A43680FC30C`
- checks/ActorGeometry.Smoke/Program.cs：`A088573E54A0723C8CFAA4E12224ED1A5EB4A478E97B648562DCF1A93CBDC0C8`

### 暂停切换的完整呈现

新增 AllowsView 返回 `!close || paused && wasPresented`，调用方把它与原有身体资格相与。暂停期间 `_hadHopPresentation` 保留上一运行帧资格；处于半空且身体未变时，进出近看都保持 canHop=true，因此不会进 !allowed 重置。

这不只保留 hop 时钟：同一个 canHop 继续参与朝向锁定、Face 和末尾的 hop.Scale、hop.Lift、位置差与 Pitch 叠加。PoseSmoothing 在 paused 时保持叠加前的基础变换；HopMotion 在 paused 时保持 Frame/Position/Heading；两部分组合方式与切换前相同，因此选中角色的冻结最终变换不会仅因视图改变而丢掉半空层。没有新增另一份时间或静态角色缓存。

### 恢复的两条路径

- 已回小院再恢复：AllowsView 由 !close 保持 true，原 hop 继续以原年龄推进，没有 Reset 或重新起跳。
- 仍在近看恢复：AllowsView 为 false。新增 smoothHandoff 包含 close，且该帧 `_hadHopPresentation` 还为 true；先把 previousBodyPosition 相对新 actor 原点的偏移、previousBodyScale、previousBodyRotation 交给 BeginFrom，再更新 hadHop 并调用 Advance 清 hop。平滑器随后向近看姿态过渡，末尾不再额外加 hop。未发现双重叠加或丢掉横向滞后的路径。

### 死亡及真实支撑变化

这次没有把 ActorHopMotion.Advance 的 paused 判断提到 Reset 前。CanSelfMove 的身体状态、Carried/Falling/Rolling/Supine/Resting/Restraint/Height/速度/姿态门槛全部保留；死亡、重伤或真实支撑不允许跳跃时，最终 allowed 仍为 false，冻结的 hop 被清除。位置距离超过1.5仍按原顺序重置，未初始化仍先初始化。

因此新增视图许可不会让死者或手持对象继续保留旧半空 hop。这里确认的是跳跃层不被无条件冻结；没有宣称外部强行在暂停中改状态后，所有基础姿态插值都即时完成。原 PoseSmoothing 的 paused 行为未改，默认游戏暂停期间也不推进生理/伤害世界步。

### 新增检查的有效性与限度

新检查明确使用向前目标，从两次0.1秒推进取得实际半空帧，并先要求 Active、Lift>.1、Travel位于0..1。随后多次视图切换要求整个 HopFrame、位置与朝向相等，不能在已落地帧假通过。把共享视图条件退回旧 !close 时，首次暂停切近看会进入 Reset，Active/Frame 断言能区分差异。

恢复近看的部分先把完整 hop 变换给真实 ActorPoseSmoothing，再要求 hop 清除、变换被保留并在后续90帧收敛。对 allowed=false、远距离位置变化、没有上一帧资格的近看也有单独断言。旧暂停/托举/传送等检查保留，没有降低断言。

ActorGeometry.Smoke.csproj 已链接真实 ActorHopMotion.cs 和 ActorPoseSmoothing.cs，不需要另添文件依赖。这个检查没有实例化 Godot CreatureActor；实际 canHop 身体门槛、BeginFrom 调用时序和末尾视觉叠加仍依赖本次静态审查，并须远端用真正的 Tab/UI 和实际最终变换复验。检查源码未编译或执行。

最终结论：追加修补可与本轮 art 的前两项一起整合，R-ACTOR-1 的源码缺陷已处理；实际暂停切近看与恢复效果仍未确认。本只读复核始终只更新 actor-review.md，没有改被审源码。

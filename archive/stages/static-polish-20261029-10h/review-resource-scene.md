# 第三轮资源场景只读审查

基线：`ee65c8041f4cfd66eb17f533690358798a13e9ec`。本轮只写此文件，产品源码和检查文件均未修改；未运行项目、编译、模拟、检查、渲染、预览或 computer-use，未暂存、提交或推送。

结论：发现 3 项具体的显示/命中不一致，均可沿默认小院的正常操作和自主取食路径到达。以下是源码推导，实际像素位置与画面效果未经远程复现。没有把已修的树冠大包围盒遮挡、异区感知等问题重新列为新发现。

## 1. P2：近看隐藏了树木和河水，但仍保留它们的鼠标作用

**依据**

- `src/Yukkuri.Game/InteractionRoom.cs:107` 把 `_woodland` 放在 `_yard` 下面，树木和整条河流都由 `WoodlandArt.Build` 放进这个子树。
- 同文件 `:196` 在近看时设置 `_yard.Visible=false`。`:197` 单独把 `_woodland.Visible` 设为 HasRiver，不能抵消父节点的隐藏。
- `src/Yukkuri.Game/InteractionRoom.Input.cs:97` 的 `SceneOcclusionDistance` 只检查 HasRiver，不检查近看模式或地景实际可见性。`ContactAt`（`:70`）和 `ObjectAt`（`:85`）仍用这些隐藏树木截断射线。
- 河水又存在相反的入口：`InteractionRoom.cs:145` 的 RiverWater 物品根节点没有网格，画面依靠共享地景；`:190` 只按物品状态/距离把这个空根节点设为 Visible。近看时只要它距离所选角色不足 2，`ObjectAt` 仍可通过 `InteractionPropContact.cs:10` 的河岸椭球选中它，`:184` 点击后显示河水说明，尽管共享水面已随地景隐藏。

**正常可达路径与影响**

1. 托举角色到北边树旁的空处，放下后按 Tab 近看，中键转到树原本位于镜头与角色之间的方向。树已经不可见，但那部分角色/附近资源仍被隐藏树遮挡，导致看得到却选不到。
2. 源码中可用于远程定位的布局：北侧树在 `(2.7,-6.9)`；角色可站在其南侧约 `(2.7,-5.0)` 的空地，近看转向北侧。这里没有要求角色进入树干，具体点击像素需远程确定。
3. 把角色放在正常河岸取水位置附近，再近看。附近 RiverWater 空根节点仍可命中，平地上会出现河水标签/说明；此处不是另一个地区或坏存档构造。

**最小建议**

让鼠标地景遮挡与当前渲染使用同一可见条件。若保持近看隐藏地景的现有设计，则近看不再计算隐藏树的遮挡，并关闭依赖该隐藏水面展示的河岸物品命中。若选择近看仍显示局部地景，则同时接通显示与命中，不能只开其中一侧。无需改树网格、冠形或遮挡三角形算法。

现有 `checks/ActorGeometry.Smoke/WoodlandOcclusionAudit.cs` 检查了树三角形与物品的前后顺序，但没有读取 `_yard` 的父子可见状态，所以不能覆盖这项接线问题。

## 2. P2：从地上拿起的最后一块甜甜吃掉后，手里仍画着完整甜甜

**依据**

- `src/Yukkuri.Game/InteractionRoom.Input.cs:165` 起的空手分支允许拿起地上的 Sweet，并选择 `FeedSweet`，后续走 `ProcessHeldSupply`。
- `src/Yukkuri.Simulation/InteractionGesture.cs:367` 接受这一口时扣掉 `supply.Portions`；默认放下的一份甜甜只有 1 份（`InteractionState.cs:143`），因此接食后为 0。
- `src/Yukkuri.Game/InteractionRoom.Tools.cs:48` 在 FeedSweet/Tease 分支直接把甜甜加到 `_hand`，没有像勺上食物一样保存 `_toolFood` 引用。
- `src/Yukkuri.Game/InteractionRoom.Input.cs:383` 的 `ProcessHeldSupply` 始终根据鼠标是否在场景内显示 `_hand`；其后只隐藏 `_toolFood` 和 `_cupWater`，甜甜不在这两个引用中。因此份量已经为 0，甜甜网格仍一直存在，直到松开/取消使手工具隐藏。
- 普通工具喂食路径 `:343` 会在甜甜已接受时隐藏手，但直接拿地面食物走的是另一条路径，没有使用这项条件。

**正常可达路径与影响**

从“食物与清水”放下甜甜，右键回到空手，按住拿起并递到愿意接食的角色嘴边，接食后不松开，把鼠标移到旁边的空地。角色已经吃到这一口，画面仍显示手里拿着完整甜甜，误导玩家以为还可递下一口。这不是重复结算漏洞：`obj.Portions>0` 与 `_supplyBiteTaken` 会阻止再喂。

**最小建议**

让手持甜甜与勺上食物一样有可单独控制的节点，或在这条手持分支明确隐藏已经耗尽的甜甜。不要仅改普通工具路径，也不要重新生成份量。远程同时核对未吃、接食后、移开后、取消、下一份拿起的显示。

## 3. P2：自主吃掉最后一块甜甜后，已经没有地面图形，原位置却仍可选中并“拿起”

**依据**

- `src/Yukkuri.Simulation/NaturalLife.cs:354` 准备取食时预留最后一份，`:370` 实际接食后清除 NaturalIntakePending。
- `VisibleFoodPortions`（`:68–72`）只在 pending 阶段补回那一份可见食物。因此接食后、这一口尚未咽完时，它返回 0。
- `src/Yukkuri.Game/InteractionRoom.cs:138` 在 Sweet 的 visibleFood 为 0 时生成完全没有子网格的根节点。`:190` 仍把这个根节点设为 Visible。
- `NaturalLife.cs:394` 保留有 Consumer 的耗尽物品，直到这一口结束；这能保留来源上下文，本身不应删除。
- `src/Yukkuri.Game/InteractionRoom.Input.cs:88` 的 `ObjectAt` 只核对根节点 Visible 等通用条件，然后按 Sweet 的固定椭球命中，未核对实际还有没有甜甜图形。
- `src/Yukkuri.Simulation/InteractionSupply.cs:16` 的 `HoldSupply` 允许 `Portions<=0` 但仍有 Consumer 的来源被拿起。因此这是可到达后续动作的透明命中目标，不只是旧标签残留。

**正常可达路径与影响**

放下一份甜甜让角色自主取食。最后一份进入嘴里后，地面甜甜消失，但这一口未结束前，原地仍可能出现“甜甜 · 最后一口还没吃完”的悬停标签，并可被空手拿起。选取范围中没有被角色自身遮挡的部分会命中；临近吞咽末尾角色抬回身体后尤其适合远程定位。拿起这个 0 份来源后还会触发第 2 项的手持甜甜假象。该窗口由正常进食计时产生，不需要编辑存档。

**最小建议**

在鼠标物品选择层排除没有可见食物的 Sweet，例如使用与渲染相同的 `FoodVisualCount`。不要用 `Portions<=0` 统一屏蔽所有来源：pending 的最后一份仍有可见食物；空碗、空杯和耗尽叶簇仍有各自的容器或残叶图形。也不要为修命中而提前删除 Consumer 所需来源。

## 本轮未判为新问题的内容

- 手持资源的地面根节点由 `HeldByHand` 隐藏；收起物品和异区物品也被显示条件排除。`ObjectAt` 又检查 Stored、RegionId 和根节点 Visible。未发现正常稳定帧下它们继续被地面选中的新证据。
- 预留期间 VisibleFoodPortions 临时加回一份，与“还没接到嘴里”的动作一致，不是份量复活。耗尽叶簇生成残叶并提示吃空；空碗和空杯保留容器，不应等同于无图形甜甜。
- 普通小院里的河面高度和实际 SourceTarget 共用 `NaturalIntakeGeometry.WaterHeight`；默认河岸资源 X 与河面西边界共用 RiverEdge(BankZ)。未发现默认位置的取水目标整体错位。河水是固定岸边取水点，未将整条河都不能点击判成错误。
- 碗、杯等物品命中使用简化椭球，身体命中与物品命中入口也不完全统一；仅凭这一点没有把“可见碗必然穿透”列为已证实缺陷。双击入口先取 ContactAt 的顺序值得后续实机用前景球/食水遮住角色时核对，本轮没有得到对应像素证据，不增加正式问题。
- 切换地区、手工给河水设 Stored 或人为修改来源坐标不属于默认小院现有 UI 可达路径，本轮没有用这些人工状态扩大结论。

任务已结束，可交主对话决定修复范围。以上 3 项均未做远程运行验证。

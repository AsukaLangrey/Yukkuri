# Game 表现与输入交接

本阶段在原七小时窗口结束后继续，原公开固定源码包不改。本机只做源码和文档静态读写；未运行游戏、Godot、编译、检查、导入、渲染、预览、项目脚本或 Git。

## 1. 有限食水供给器：接线前静审

状态：已按 A/root 统一契约写入 Game 接线并冻结，等待主线整合与 A 的独立检查源码；尚未运行验证。

- 现有自然摄食从真实 `RoomObject` 取用。`NaturalIntakeGeometry.SourceTarget` 的饭饭入口为 `Height + .107`，清水入口为 `Height + .15`、水平向杯缘减 `.12`。`CanReachRoomFood` 复用真实嘴部、身体支撑和包扎限制；不能把这些入口藏进更大的器具外壳。
- `NaturalLife` 准备取用时预留实际一份，最终入口仍复核同一对象、距离、高度与可达性。新器具应生成原 Meal/Water 对象，沿用这条路径、原手持食水和原瓶养补给，不添加直接加饱食/水分的通道。
- 默认自然取用半径为 `.8 × 年龄尺度`，幼体为 `.584`；找站位采这一区间的多个方向。建议低矮后置储料仓，前方独立餐盘/水杯；机体占位与出料托位分开，保证前方实际站位和口部空间。
- 当前 `MakeObject` 已按实际份额显隐饭团/零食、按真实取用进度显示杯水。出料沿用这些模型；新仓体只根据模拟的库存、容量和空仓读数改变显示。
- `ContactAt`、`ObjectAt` 以射线最近目标裁决。`SceneOcclusionDistance` 同时用于点击与 `MouthOfferContact` 的递嘴遮挡。新增器具要以实际仓体网格参与遮挡，不用大球覆盖前方独立食水。
- 原地面食水由 `PlaceInteractionSupply` 和 `RoomSupplySupport.Height` 决定库存与实际支撑。新器具放置及出料坐标/占位由 A 提供同源常量与只读状态，Game 不自行判定成功或修正落点。

拟范围：新增器具 Actor 与 `InteractionRoom.SupplyStations.cs`；在 Input.cs 接真实命中/鼠标分流，在 InteractionRoom.cs 接 Actor 同步与清理（不改 SaveRoom/LoadRoom）。器具面板/补充入口仍由 root 编写。具体文件和接口以统一契约为准。

### 已落地文件与契约

- `src/Yukkuri.Game/RoomSupplyStationActor.cs`：固定朝 +Z 的低面开口仓体。机体实体位于半径 .38、最高 .565 的范围内，不超模拟 .40 占位；前方短出口止于 Z=.378，实际独立盘杯在模拟的 Z+.85。仓内填充高度只由 `StoredPortions/Capacity` 驱动，空仓不画填充物。水与粮使用不同内料/标记，未加人体手模型。真实各网格参与射线，缓存每个固定 Mesh 的三角树；退出只处置自己的三角树。
- `InteractionRoom.SupplyStations.cs`：按真实当前区列表建立/清除器具 Actor，显示实际地面 X/Z，无托举高度或周期摇摆。`ArmSupplyStationPlacement(kind)` 先取消原手势再进入地面放置；两个重复使用的轮廓圈分别提示仓体与前方盘位，是否可放读取 `CanPlaceSupplyStationAt`，最终库存与落点仍由 `PlaceSupplyStation` 决定。近看隐藏器具；换世界 BuildActors 清旧节点。
- `InteractionRoom.Input.cs`：实际 `SupplyStation` Contact 与身体/独立窝比较最近射线，并尊重前方独立食水遮挡。新器具也进入 `SceneOcclusionDistance`，避免绕过仓体递嘴。选中器具不把 station Id 当作母体 Id；普通身体/物品点击清器具观察。现有独立瓶口、身体嘴边和球的类型分流保持。
- `InteractionRoom.Tools.cs`：`RefillSupplyStation` 按实际 Food/Water 类型显示袋口或壶嘴贴合，工具缓存键同时含类型，不误用护理垫或沿用上一种器具的工具。模型只说明实际接触，不画自动倾倒成功、额外食水或余量结算。
- `InteractionRoom.cs`：仅增加器具 Actor 清理/同步与放置轮廓更新；未修改 SaveRoom/LoadRoom。

### 连续输入与状态边界

- 独立目标使用 `TargetKind.SupplyStation`、`ObjectId=实际器具Id`、`Part.Side`。`BeginSupplyStationGesture` 与 Continue/End 仍共用唯一手势，真实外表接触才累计补充时间。鼠标移开、换目标、UI遮挡和超长采样送 `inContact=false` 清连续时长；回原器具先建立新接触基线。首帧 duration 不超过按下以来的实际时间，不借按下之前整帧。
- 一次补充成功后 End，隐藏袋/壶并清鼠标保持状态；必须重新按下才能再尝试。取消不退款已完成补充。非器具目标不会回退为护理或喂食。
- Lift 使用已有 `SupportDrag` 的地面位移，模拟按合法分步接受；当前 DTO 位置就是实际位置。松手/取消只沿模拟结束，没有 Game 侧回起点或额外抛掷。若真实出口 Consumer 正在接取/吞咽，由模拟拒绝搬动。
- `CancelGesture` 同时取消放置态，立即收起轮廓；切工具、暂停、失焦、模态、镜头拖动和读档沿既有 Cancel 入口。放置成功按实际新器具 Id 选中，不伪造实例。
- 输出仍完全是原 Meal/Water `RoomObject`。空份额 Meal 保留原盘、Water 保留杯身，只隐藏食物/水面；仍可射线遮挡。食水拿走、单份入口、自然低头、有限仓补下一份及存档均由 A 的原物件链驱动，Game 没有第二套摄食时钟。

root 独占器具观察/购买/补充库存按钮、快捷工具选择、参数面板与进度条；A 独占规则、持久化和检查源码。以上 Game 文件没有新增外部资源依赖，也没有运行或导入模型。

静态收口修正：仓壁参数 u 向上、v 绕 Y 转动，通用 Surface 的 `dv.Cross(du)` 朝内；外壁使用 flip、内壁不翻，保证法线与实际朝外/朝内面一致。修正仅交换两个构造参数，未改变顶点位置或真实射线网格。Arm 同步实际工具组/标题并隐藏旧选项；无空器具库存转用品购买入口。新五个产品文件与本交接冻结，不跨写下一功能。

仍需授权远端确认：成人/幼体与包扎个体在器具前的实际口部可达、多人抢用与盘杯遮挡、空仓和余量可辨认、拿走盘杯后的出料规则、暂停/换区/读档的显示与鼠标取消。以上不是实机测试结果。

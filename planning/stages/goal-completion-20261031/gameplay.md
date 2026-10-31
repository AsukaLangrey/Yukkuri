# 功能玩法续作

## 有限食水供给器：锁定契约

本轮只静态读写源码；未运行游戏、编译、检查、脚本或预览。既有公开源码包保持不变。

- `SupplyStationKind { Food, Water }`；`WorldState.SupplyStations` 为 `List<SupplyStationState>`。实例保存 Id、Kind、RegionId、X/Z、StoredPortions、OutletObjectId、OutputSequence、LastEvent；HeldByHand 为瞬时状态。
- 空器具库存为 `FoodStationStock/WaterStationStock`；批量清水库存 `StationWaterPortions`。FoodStationPrice=35、WaterStationPrice=30；容量分别 8/12；StationWaterBundlePrice=6、StationWaterBundleCount=12。测试免金钱，实物仍有限。
- `BuySupplyStation(kind)`、`BuyStationWater()` 返回 bool。`PlaceSupplyStation(kind,x,z)` 返回 InteractionFeedback。`SupplyStationStatus(id)` 返回可空 SupplyStationView；`SupplyStations(regionId=null)` 返回只读视图列表。
- 视图字段顺序：Id、Kind、RegionId、X、Z、Capacity、StoredPortions、OutletPortions、OutletObjectId、OutletX、OutletZ、HeldByHand、CanRefill、CanMove、RefusalReason、LastEvent。
- TargetKind 末尾追加 SupplyStation；Action 末尾追加 PlaceFoodStation、PlaceWaterStation、RefillSupplyStation。`BeginSupplyStationGesture(id,input)` 共用唯一手势，ObjectId=id、Part.Side、SupplyObjectId=null。真实补充接触 1.6 秒成功才转移库存；完成一次不重复结算。Lift 沿合法地面搬动，取消落在当前真实位置，不退款。
- 固定朝 +Z，机体半径 .40，出口中心 Z+.85。出口是原 Meal/Water RoomObject，实际高度沿 RoomSupplySupport；独立食水仍走原预留、入口、吞咽及家庭搬运。机体阻挡移动与视线，出口前侧接近与家庭取料共用。
- 容量包含仓内和当前出口未入口份额。正在预留或吞咽时不再出料；被拿走的份额保持原对象，不回仓。输出先检查落点，成功才扣仓内一份。
- 自动出料采用持久确定序号，禁止随机 Guid；序号耗尽停止新建，现有份额可继续使用。旧档默认无供给器，不赠库存或读档自动填仓。
- 本批不做回收或退款；保留完整搬动。主线负责用品与状态 UI，B 负责可见袋/壶、实体及实际命中。

### 第一批功能线冻结交接

已写完整有限仓储链：购买空器具与批量清水、合法空地摆放、真实连续补充、确定身份出料、自然食水入口与家庭取份、实际低托搬动和取消。P22 追加在目录末尾，不改变原启动组。只读进度共用 ContactElapsed/1.6。

出口空盘/杯保持原 RoomObject 和身份；预留未入口的一份计容量，吞咽期间不补料。吞咽结束可在同盘补下一份，不要求消费者先走开。拿起时归还原预留后解除器具绑定；该手持份额的原取消锚点只暂时阻止新出盘，实际释放到别处才腾空，不阻挡油移动。家庭搬运份额不作为原锚点阻挡。出料前地面小物（含软球）、身体、独立窝、仓体与软垫均参与检查，失败保留仓内份额。仓体同时接入身体移动、食水视线、实际嘴部路径、球碰撞、普通放食与教学放食，前侧站位由自然接近和家庭取料共用。

生产新增 5 文件：`SupplyStationState.cs`、`SupplyStations.cs`、`SupplyStationSpatial.cs`、`SupplyStationGesture.cs`、`SupplyStationValidation.cs`（均在 src/Yukkuri.Simulation）。修改 15 文件：`InteractionState.cs`、`InteractionCatalog.cs`、`InteractionGesture.cs`、`InteractionCompletion.cs`、`WorldSimulation.cs`、`InteractionSpatial.cs`、`DetachedStemLife.cs`、`DetachedStemGesture.cs`、`InteractionSupply.cs`、`InteractionWorld.cs`、`NaturalLife.cs`、`FamilyCare.cs`、`InteractionValidation.cs`、`InteractionBehavior.cs`、`InteractionBallDynamics.cs`。

新增检查项目 `checks/Interaction.SupplyStations`（csproj + Program.cs），8 组源码：有限购买/接触取消/一次补充；确定出料和旧档默认/库存溢出；侧后方食水实际接近（幼体、成人、长者、真实侧伤）；预留份额/拿走归还；同 ID 空盘连续两口；真实搬动/占位/换区取消；家庭取份递送幼体并实际入口；软球阻挡/空仓/空选择/暂停购买。初始隔离场景只设需求、体型、位置与亲缘，关闭与此无关的自主闲逛/相处选择；不证明默认自由经营全程。未降低旧检查或改变旧食水营养/入口门槛。

本机仅完成源码和调用静审，**上述检查全部未运行，也未编译**。尚需远端验证实际仓体射线、不同体型的嘴部接触与侧后绕行是否如预期，以及连续搬动/失焦/读档的可见衔接。当前静读未发现仍待修复的确定源码问题；不将这一结论当成运行通过。

交付静审补正：供给器校验移到个体互动校验之后；先拒绝显式空 Interaction，并在跨类型身份枚举前拒绝空独立窝列表/元素。原存档组补首/末个体 Interaction、独立窝列表/元素、供给器列表/元素显式 null 的 `InvalidDataException` 拒绝断言。正常缺字段旧档默认不变，仍为 8 组检查源码，未运行。

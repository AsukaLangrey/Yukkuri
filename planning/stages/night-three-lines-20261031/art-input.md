# B 美术、物理与手感线

范围：src/Yukkuri.Game/，排除主协调所有 Ui/Vitals/Debug 文件；assets-source 本轮新增候选记录。仅静态源码工作，未启动编译、测试、游戏、渲染或预览。

## 首批增量（进行中）

- 移除工具手、托举手、持饰品手的人类网格，保留接触和感知锚点。托举仍有落点标记，身体和鼠标反馈表达接触。
- Lift 不论空手还是工具入口都在松手时调用既有 ReleaseDraggedCreature；同一次点击可选中并操作另一只，不再吞掉第一次点击。
- 食物工具点油：自动把这一次供给递向实际嘴部，通过嘴前的短距离射线检查身体/固定具/同伴/场景遮挡，仍走原 Begin/Continue 接食、拒绝、消耗规则。点地映射 PlaceMeal/PlaceSweet/PlaceWater；单击只供给一份，不循环进食。
- Punch/Burn 使用 A 新动作；旧 Slap/BurnFeet 保留。已确认生效的击打计入 1.5 秒同目标 hits，未命中和拒绝不计数。新火机是小道具，不重建人类手。
- F3 调用主协调 ToggleDebugPanel；Escape 优先收起 Debug；输入框聚焦不把键盘事件变为游戏操作；DebugContainsPoint 使扩展面板区域不穿透点击。
- 救援工具 ArmWaterRescueTool / _waterRescueToolArmed 提供主协调 UI 入口；点油调用 A 的 RescueFromWater，库存不足由模拟拒绝，不绕过付费/道具库存。切工具、暂停、右键收回。
- 眼睑和眉形在短时间内连续过渡，恐惧仍影响吃东西时的眼眉；嘴部状态短淡入淡出；脸红逐步消退。缓存量化并限制眉形缓存，避免长时间插值产生无界浮点键。
- 水中身体下沉、挣扎起伏/偏转随 Struggle 变化，水面涟漪和水滴；精疲力竭/死亡减弱或停止挣扎。救后 RecoverySeconds 保留余震。水中不再由地面支撑补偿把身体抬回岸上高度。

## 接口与归属

- 主协调：Ui快捷按钮映射 Lift / Stroke / Punch / Burn / FeedSweet / FeedMeal / Drink；救援按钮调用 ArmWaterRescueTool()；救援标题优先于空手标题。
- A：已确认 Punch/Burn、Interaction.Water（InWater/Struggle/ExposureSeconds/RecoverySeconds）、RescueFromWater、BuyWaterRescueTool、WaterRescueTools/WaterRescuePurchases。游戏表现不擅自创造模拟状态。
- 新增 Offering / HitFeedback / RescueTool、FaceMotion / Water 均为本轮 Game partial 文件。

## 待继续

- 静态审查鼠标取消、暂停/失焦、连点、手喂和救援工具切换边界。
- 茎生/胎生从现有旁置图标补为成长过程及身体表现；连续操作与长期状态的视觉优先级。
- 自然资源候选完整来源/许可/格式研究见 asset-research.md。下载/静态读取不等于已集成或已看过游戏画面。
- 原有接触检查可能引用被用户要求移除的人类手外观；不删除或降低原检查，交主协调/A调整对应需求断言并远端核实。

## 未验证

本机禁止运行，因此未编译、未执行检查、未验证真实帧率/画面/鼠标手感。新增源码需独立远端覆盖。视觉效果这里只描述实现意图。

## 首批静态边界检查

- 失焦沿用 Ready 的 CancelGesture；暂停/换工具/存读档沿用同一取消入口。新递食和救援字段也在此清空，不留隐藏供给。
- 原连续操作的左键释放仍经 EndPointerGesture；UI外释放走取消，不应用已存抛掷力度。自动单份递食是用户“点击手喂”要求的独立分支，不用于火机等持续工具。
- 递食目标移动超过0.55世界单位、嘴部被遮挡或超过3秒时收回；成功/拒绝后结束本次手势。供给次数和资源扣减仍由模拟管理。
- 模态面板存在时 Escape 不先进入 Debug 关闭分支，避免调试面板方法拒绝后却吞键。文本框聚焦时 Space/Tab等不误触小院，Escape仍可关闭面板。
- 水中跳动不调用陆地跳跃表现；水面高度引用既有 NaturalIntakeGeometry.WaterHeight，不自造另一水位。地面和软垫的抬高补偿在水中停用。
- 允许落入水域时落点环显示蓝色，与普通地面绿色区分；蓝色只表示水域位置，不擅自将落点挪回岸上。
- 新热僵硬只读取 A 的 HeatTension；固定时细颤/收紧，移开后随模拟衰减。水/死亡优先于此表现。
- 首批审查小修：水表现统一通过 HasWaterBodyPresentation（InWater且未托举、未下落、高度<=0.1、未冷藏/安置）；陆地跳跃和支撑补偿也使用同一条件。缓存不能单独覆盖真实支撑。临危/死亡不再叠加热形体变化，救后余震也不覆盖临危或托举姿态。
- 所有 Game 下原验证文件保持原状。此次只检查源码调用与分支，不等同远端鼠标验收。

## 首批文件清单

修改：InteractionRoom.cs、InteractionRoom.Input.cs、InteractionRoom.Tools.cs、CreatureActor.cs、CreatureActor.Interaction.cs、CreatureActor.Face.cs、CreatureActor.Contact.cs。

新增：InteractionRoom.Offering.cs、InteractionRoom.HitFeedback.cs、InteractionRoom.RescueTool.cs、CreatureActor.FaceMotion.cs、CreatureActor.Water.cs、CreatureActor.Heat.cs。

文档：本文件、asset-research.md。候选素材：assets-source/night-20261031/candidates/ 下 grass.glb、rock_largeA.glb、flower_yellowA.glb、Kenney-Nature-License.txt。完整kenney-nature-kit.zip为本地来源存证，不纳入首批Git提交。

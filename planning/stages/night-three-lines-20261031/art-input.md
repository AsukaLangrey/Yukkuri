# B 美术、物理与手感线

范围：src/Yukkuri.Game/，排除主协调所有 Ui/Vitals/Debug 文件；assets-source 本轮新增候选记录。仅静态源码工作，未启动编译、测试、游戏、渲染或预览。

## 第九批：离体茎世界实体与真实工具接触（交接）

- 新增 DetachedStemActor.cs。Loose 是低伏离体茎，Bottled 是有开口的培养瓶，Failed 保留枯损实体。瓶中实际含水量、营养余量、完整度、进度决定水位、沉积物、裂痕和芽体状态。全部根位置直接读取 DetachedStemView 的 X/Z；托举只显示低位选择圈，不额外抬高或视觉穿过障碍。Born 按主协调最终口径仅留贴地小枯梗，不显示完整大瓶、不增加物理占位；Cleared 隐藏实体和命中。首次读取只显示当前状态，没有重播旧出生。
- 新增 InteractionRoom.DetachedStemInput.cs，并接入 Room 构建/每帧同步、Input 选择/按下/继续/松手/取消。目标严格使用 DetachedStem + EventId，不把 EventId 传 SelectOil，不读母体位置。实际瓶体、茎、幼芽、瓶口及终态枯梗参加三角形射线最近排序；身体、附着茎、场景和前方地面食水/软球可以遮挡。瓶口只接收从上方穿过真实开口的射线，瓶壁或枝芽更近时不能当作瓶口供给。
- CutStem 沿原附着 Stem 输入链，新增剪具外观，尖端对齐真实茎接触。BottleStem/Drink/FeedMeal/ClearStem 均走模拟唯一手势的 1.6 秒连续接触与单次结算。装瓶工具只显示手边小空瓶，世界瓶只在 DTO 真正装瓶后出现。ClearStem 完成立即结束输入、藏工具，保留模拟历史。独立 Punch 不增加母体 Hits；Burn 使用实际独立接触标志控制原火焰，离开即停。
- 实际食水从原 HoldSupply 路径进入，传 SupplyObjectId=手中 RoomObject.Id，ObjectId=窝 EventId。只对开口建立接触；新入口或明显跳动先建立工具位置基线，不提前累计。移到瓶壁/空处归零，换油/换窝结束旧手势，同次拿起成功或拒绝后不自动重复；食水剩余外观仍读真实 Portions。仅目标改变时调用 SelectDetachedStem(id,false)，不重选/放掉手中实物；转回油嘴时清理独立观察，原身体递食继续工作。
- Lift 使用真实鼠标相对拖动，模拟每步合法位置直接显示；松手、右键取消、暂停、读档沿模拟当前最后合法位置放稳，不回原点、不补位移。跨 UI/卡帧重新建立指针基线，不追补之前的移动。所有新增表现无模拟推进时钟；暂停/失焦/换工具/存读档沿现有统一取消链。母体死亡或新孕育不影响独立 EventId。
- 修复身体手势中鼠标移到瓶身时旧代码按 contact.Id 强取 _actors 的越界路径。单击递食的嘴前遮挡检查也纳入独立实体。离体茎出生只给真实新生幼体触发原出生反馈，不给母体误播新的身体分娩，避免母体死亡、异区或再次孕育时串状态。

本批文件：DetachedStemActor.cs、InteractionRoom.DetachedStemInput.cs（均新增）；InteractionRoom.Input.cs、InteractionRoom.Tools.cs、InteractionRoom.Offering.cs、InteractionRoom.Family.cs、InteractionRoom.cs；本交接。未改 Simulation、checks 或根专属 UI/Debug/Vitals/Supplies/Intervention/DetachedStemUi。只进行源码读取、rg 检索和补丁编辑；未运行游戏、Godot、编译、测试、模拟、渲染、预览、转换、导入、项目脚本、钩子或 Git。

远端待测：实际剪取→整窝离体→装瓶→拿真实水/正餐递瓶口→低托搬放→出生与清理；瓶口从不同视角是否易对准、玻璃透明排序/芽体与瓶口遮挡；移开重计时、换窝/换油、松手/暂停/失焦/存读档/跨区；食水耗尽、不足库存、满水/满营养拒绝不扣错；障碍与其他瓶占位、取消不回起点；母体死亡/再次孕育后独立照护；失败不能复活、Born 仅贴地残留、清理后历史仍在；击打不污染母体 Hits。以上均未在本机执行，静态接线不代表实机效果验收。

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

## 第二批：孕育本体与材质（01:40）

- 新增 CreatureActor.Gestation.cs：茎生按照模拟的孕育进度长出连接头表面的弯茎、叶片和2—3颗幼芽，芽的大小连续增加；胎生按照进度和Burden缓慢扩张身体、增加负担呼吸和轻微前倾，恢复期渐退。临危、死亡、有效落水身体优先，不叠加胎生形变。
- 分娩短反馈只在 SynchronizeFamilyActors 发现带有效 BirthEventId 的真实新增幼体时触发；母体短收缩、幼体短落稳。加载存档通过 BuildActors 一起创建，不播放假出生。Recovering本身不会触发分娩，因此失败恢复不会伪装成生产成功。
- 时序复核：实际生产不清旧ReactionPose，母体短反馈保留最多4秒等待窗口，仅安全settled姿势时消耗1.25秒动作时间；暂停同时冻结窗口和动作，过期不补播，不抢临危/死亡/托举/固定/水中的身体控制。
- 茎体是随本体运动的表现层，不参与地面支撑判定，不改变现有头/嘴/底部接触表面，也不声称已经提供单独触碰幼芽的交互。落水/临危姿态仍由其原优先级控制。
- Family面板接已存在的 BreedingInductionReason / InduceBreedingInterest / CourtshipTools；展示兴趣、剩余时间、资格理由和实际用品库存。执行走原关闭临时暂停包装，原本暂停时禁用；购买调用主协调的 ShowCourtshipSupplies。
- 皮肤保留原 SphereMesh，仅在独立材质上使用纵向奶油色渐变、较高粗糙度和低镜面反射；后发壳增加细微发束明暗，发束根部和末端渐暗。未改变原外轮廓、射线或地面支撑算法。
- API静态查证：Godot官方 GradientTexture2D 与 BaseMaterial3D 文档；未运行引擎或渲染。头饰与茎叶在各姿态下的可见关系、真实材质色彩、出生动效仍需远机观察，不能据源码宣称画面已通过。

本批文件：修改 CreatureActor.cs、CreatureActor.Interaction.cs、CreatureActor.Support.cs、CreatureActor.Sculpt.cs、InteractionRoom.Family.cs；新增 CreatureActor.Gestation.cs、CreatureActor.Materials.cs；文档为本文件。未改主协调Ui/Debug/Vitals/Supplies，未改Simulation和检查文件。

接口文档来源：
- https://docs.godotengine.org/en/4.4/classes/class_gradienttexture2d.html
- https://docs.godotengine.org/en/4.4/classes/class_basematerial3d.html

## 第三批：水域围观性格与计数一致性（接替线 art_input_2）

- Room 在每帧向 Actor 提供 A 新增的 `Sim.WaterConcernTarget(c)`。此查询复用真实落水、存活、观察资格和视线规则；旧 `WaterConcernId` 本身不能驱动画面。目标离水、死亡、隔离、遮挡或观察者失去资格时，情境表现退出。
- 新增 CreatureActor.WaterConcern.cs。善良者在安全自主姿态中伸展、急切抬眉，站稳后在落水同伴与人类之间回望；中性者观察；恶劣者只有实际 Defiant 反应仍在持续时才偏头、挑眉嘲弄。表情复用原平滑过渡，身体权重渐变。临危、死亡、托举、固定、翻滚、本人落水、进食、排泄、受伤疼痛及直接接触姿态优先，不由围观覆盖。
- 走动仍使用原起跳与落地朝向；只有身体已站稳、未转向/跳跃时才回头求助。不新增模拟位移，不从台词字符串推断事件。
- Hits 删除本机微秒计时与本地加一，直接显示模拟每个目标的 `HitCount` / `HitRemaining`，上限999。换目标后返回前一目标，在其模拟有效时间内接续该目标原链；暂停冻结模拟计时，因此显示不再自己过期。只在命中反馈成功、发生变更且未拒绝时选择显示目标。
- 水中“有体力挣扎”与“有能力跳逃”分开。主动抬身尝试严格由 `WaterEnvironment.CanStruggle` 门控，固定者保留原方向只轻微颤动，残障但仍有体力者不伪装成正常跳逃，临危/死亡无主动动作。
- 只读复核了工具选择、目标切换、释放鼠标、移出场景、失焦/暂停调用链：已有 `CancelGesture` / `EndPointerGesture` 负责收尾，本批不额外改变输入语义。单击递一份后释放仍继续递送；换工具/右键仍取消。

本批改动：CreatureActor.WaterConcern.cs（新增）、CreatureActor.cs、CreatureActor.Interaction.cs、CreatureActor.FaceMotion.cs、CreatureActor.Water.cs、InteractionRoom.cs、InteractionRoom.HitFeedback.cs，以及本交接。未改主协调专属文件、Simulation 或 checks。只作源码静态审查，未运行游戏、编译、测试、模拟、渲染、预览、转换或项目脚本；还需授权远端核对实际表情节奏、视线遮挡后渐退、暂停/切换目标的 Hits 以及固定/残障水中姿态。

## 第四批：真实主动陪伴的持续表现

- 新增 CreatureActor.HumanCompany.cs，Room 使用 A 的只读 `HasLiveHumanCompany(c)` 提供真实陪伴有效性。没有从保存的 Until 单独推断正在陪伴，也没有在画面层另写信任/安全/饥渴资格规则。
- 有效陪伴区分主动靠近、到达后安心相处、寻求安慰。相处放松量读取实际 `ContactSeconds/6`；站稳后朝向人类，增加轻微呼吸、身体放松与缓慢眨眼。移动时保留原起跳朝向和接触支撑。时钟仅在有效表现阶段累积，StartedAt 改变才开始新一轮，不随每帧或短反应重启。
- `SeekingComfort`、悲伤剩余时间或 `EmotionalReadiness.Settled` 未满足时保持忧虑口型与眼眉。陪伴不是立刻快乐；表现不修改恐惧、信任、伤痛或记忆。
- 开心/好奇嘴型下的警惕眼眉同时读取 Fear 与 Emotion.Alarm。移除了旧的较大警惕表现起跳阈值，小幅余波也渐变呈现；嘴型允许喜欢食物，眼眉和腮红仍保留警惕，不伪装成完全恢复。
- 暂停只冻结已经存在的陪伴表现，不从旧存档时间戳新生成表现。近看、本人水中、受伤疼痛、强烈恐惧、逃避、临危死亡、固定托举翻滚、进食排泄和直接操作姿态优先。当前水域求助也优先于普通陪伴。

本批文件：CreatureActor.HumanCompany.cs（新增）、CreatureActor.cs、CreatureActor.Interaction.cs、CreatureActor.FaceMotion.cs、InteractionRoom.cs，以及本交接。未改主协调 Ui/Vitals/Debug/Supplies/Intervention，未改 Simulation/checks，未使用 Git。源码静态核对已覆盖接口存在、调用次序、暂停时钟和身体优先级；未编译或执行。远端需看陪伴起止/转身自然度、悲伤与放松区分、手离开或遮挡后的退出、暂停恢复，以及手喂后惊惧余波的连续表情。

## 第五批：眼嘴细节与工具接触反馈

- 眼睛沿用全部原顶点和层次轮廓，只给虹膜原顶点增加上部暗色、下部暖色、较深外缘。嘴腔仍使用原 FaceOvalMesh、原尺寸和偏移，在同一网格上加入中心暗色与暖色嘴缘，保留表情过渡的独立透明材质。未新增眼嘴几何或修改接触/支撑函数，实际近看效果待远端渲染观察。
- 火机只在真实持续接触、模拟仍占有动作且反馈为 burn-flee / burn-tense 时显示火焰。两层火焰轻微燃动，火尖保持原局部坐标 .0115，不随缩放穿入身体；松手、移开、暂停、失焦或换工具都不延续旧火焰。主协调静态审查指出 SceneArt.Ball 使用单位网格加尺寸Scale，初稿燃动直接赋单位比例会放大火焰；已修为分别乘回 (.027,.065,.027) / (.019,.025,.019) 原尺寸，保留此发现记录。
- 单击递食先沿已核对无遮挡的嘴外法线接近 .16 秒，再累计真实接触。跨越接近完成的那帧只向模拟传入到嘴之后的时间，接近过程不调用 ContinueInteractionGesture。接受/拒绝清掉点击目标并结束模拟手势一次；成功后空勺/空杯用 .22 秒收回，收回阶段不再调用模拟命令。饼干被接受后从嘴边消失，不绘制人类手。
- CancelGesture 同时清除接近与收回；右键、换工具、目标切换、再次点击、暂停、失焦按原取消链立即收手，不能续回旧递送。遮挡/死亡/明显移位亦停止。下一次同工具递送会恢复新一份食物或杯水，避免复用前次空工具。
- 甩动提示只复用 CarryReleaseMotion.Velocity 与 ThrowSpeed：达到原抛出阈值后指针变色并画短方向箭头。不另算速度，不修改松手判定，也不画假落点预测。
- 同步 A 的 PreferredHumanDistance：陪伴到达判断不再写死1.15，直接读取模拟当前舒适距离；必须已有实际 ContactSeconds 且仍在该范围内，才表现为已到达。整合时同步了 A 到达判定的 .01 距离容差，避免模拟已开始相处而画面仍显示接近。

本批文件：CreatureActor.Face.cs、CreatureActor.cs、CreatureActor.HumanCompany.cs、InteractionRoom.Tools.cs、InteractionRoom.Offering.cs、InteractionRoom.Input.cs、InteractionRoom.cs，以及本交接。未改 Simulation/checks、主协调专属文件，未使用 Git。只作源码审查，未启动项目/编译/测试/模拟/渲染/预览/转换/项目脚本。远端应检查：眼嘴颜色与淡出材质；火焰实际尺寸和贴合；接近期间取消不消耗、接近完成才开始接触；成功仅一份、拒绝不重复；收回期间切换/暂停/失焦不残留；连续同工具递送有新份；慢放/快甩提示与实际松手结果一致。

## 第六批：真实入离水过渡与湿透后的连续滴水

- 新增 CreatureActor.WaterTransitions.cs。Room 每帧在身体刷新前传入 `WaterEnvironment.IsInWater(Sim.State,c)` 的真实几何结果。首次观察、读档重建、模型引用替换、换区、隐藏后重新出现只建立当前基线，不把已有落水状态当成新落水。正常可见期间真实进入/离开水域才触发约 .65 秒水花/滴水；离水被抓起时按实际高度表现，不补画虚构的跳岸。
- 人类救援在 Input.cs 唯一结算入口检查 `RescueFromWater` 的 Success、Changed 和实际已离水后，再通知 Actor。成功事件用约 1.1 秒岸边水滴与淡圈辨认。没有读取历史 LastRescueAt 触发重播，也不依赖世界秒变化，因此近看同一世界秒内再次实际救援仍能表现。通知只对同一 Creature 对象有效；隐藏、换到其他模型或重新入水会丢弃无效通知。当前规则仍是模拟直接救回岸边，没有加入或伪装成真实拖救物理。
- 暂停使短表现和滴水时钟为零；成功通知若尚未播放就暂停，会保留到继续。近看允许播放纯画面反馈，但新增文件不写入 ExposureSeconds、RecoverySeconds、Wetness、LastRescueAt 或 WorldSeconds。实际恢复颤抖仍沿用原 Water.cs 与模拟恢复量。
- 湿透后的持续滴水分别读取头部、左右脸、左右侧的 WetnessAt，局部擦干立即减弱或停止对应水滴。每滴从实际姿态的接触点开始后向下掉落，不随角色平移拖着走；实际重新入水后隐藏岸上滴水并重设来源。死亡不会触发新的入离水或救援过渡；临危者实际入水和成功救援仍有被动反馈，原水中循环表现已有零挣扎门控。仍湿的遗体可以自然滴水，不表达康复。此处由主协调按最终代码纠正表述，不删除临危者的真实成功救援反馈。
- 新增网格挂在 Actor 根节点，避开 `_visual` 的支撑网格收集以及原显式接触射线对象；不改变身体顶点、支撑高度或命中。所有 Ball 缩放均乘回原尺寸，未重犯第五批单位球放大问题。现有持续波纹/挣扎水滴保留，不另造模拟状态。

本批文件：CreatureActor.WaterTransitions.cs（新增）、CreatureActor.Interaction.cs、InteractionRoom.cs、InteractionRoom.Input.cs，以及本交接。只进行了文件读取、rg 检索和补丁编辑；未运行项目、编译、测试、模拟、渲染、预览、转换、项目脚本、钩子或 Git。源码核对覆盖实际水域查询、成功反馈 Changed 默认值、读取存档重建 Actor 路径、效果与接触/支撑树隔离。远端仍需核对：初载水中与旧救援存档不重播；正常丢入/托举离水/自行逃出/成功救援的区别；无库存、固定、岸边无空位等失败不出现成功圈；近看救援不走自然时间；暂停中途冻结再续；换区/隐藏/Actor 重建不残留；救起后再入水覆盖旧信号；分区擦干与水中死亡表现。

## 第七批：附着母体的外茎照护与独立命中

- SurfaceContact 和 Room.Contact 增加独立 `TargetKind` / `ObjectId`，沿用 `BodyPart`，没有把外茎当头部。现有茎、叶、幼芽与新增支护绑带以真实网格参与同一条射线的最近命中排序，身体、发饰、固定具和场景遮挡仍可挡住它们。茎仍是 `_visual` 的附着子节点，随母体实际姿态移动；KeepBodyAboveSupport 继续排除整棵茎，不把柔软外茎当刚性地面支撑。
- Begin/Continue 传 A 的 `InteractionTargetKind.Stem`，ObjectId 绑定本窝 EventId。连续操作保留初始身份，射线移到母体、另一窝或旧孕育身份时 touching=false；不能降级为对母体的动作。鼠标移出、换工具、暂停、失焦沿用原取消链。食物点击、地面放置的提前分支也保留 Stem 身份交给模拟拒绝；救援用品点击茎只提示重新对准身体，不误救母体。嘴边接食显式要求 Body。
- 支护工具复用护理垫，无人手外观。对准实际茎网格才向模拟提交连续接触；1.6 秒、一次扣一份 Medicine、同手势只结算一次由 A 的模拟代码负责。完成后悬浮提示已护理，有限支护状态为正才显示附着绑带；没有预播成功。火机增加 stem-burn 的真实持续接触火焰，离开即停止；拳击外茎不调用母体 Hits 登记，同时清掉旧母体连击提示，避免混淆对象。
- Room 每帧传只读 `StemCareStatus` 给 Actor。Progress 0–1、计划幼芽数、Integrity/Hydration 0–100 和 SupportRemaining 秒直接来自模拟。真实完整度/含水下降时，茎逐渐下垂、叶色枯黄、幼芽肤色变暗、眼睛疲惫，护理后随数值改善逐渐缓解；支护减少摇晃并显示绑带。材质逐份复制后调整，不污染共享颜色；幼芽眼嘴缩放保留单位球原尺寸。
- 初载当前窝只建立表现基线，不生成出生事件。换 EventId 重设该窝的外观基线；失败、出生结束、死亡、冷藏/纪念时立即移除旧茎命中与可见节点，不残留可点击旧芽。实际母体/幼体出生短反馈仍沿用已有 CompletedBirths 链，本批没有用恢复阶段或护理文字推断出生。
- 主协调静态审查发现软球点击最初可能取到后方茎的 TargetKind，已改为球分支显式 Body、ObjectId 保持球 ID。真实 touching 与悬浮施力提示统一使用 MatchesGestureContact，避免鼠标已移到身体还显示外茎正在施力。
- A 后续将完成支护的公开 InteractionGestureTarget 释放为 null，内部惰性手势仍保留到松手。B 已同步：成功即收起药垫；完成标签仅用本次保存的 EventId 与 stem-supported 状态识别，不重新占有母体。持续支护路径没有按公开 target 查找并解引用母体，正常松手/取消仍结束内部手势，成熟出生可以继续。
- 整合修复：A 也在外茎失去接触时释放公开 target。B 原先依赖该 target 判断接触，移开后会无法重新命中。现保存真实起始母体 ID，与 Kind/EventId 同时建立并在 ResetContactMotion 清除；外茎接触使用 HasInteractionGesture + 母体 ID + Kind + EventId 判断，因此移回原窝可从零重新累计。身体/软球仍沿用公开 target，完成支护的模拟占有仍为 null；本地身份不写回模拟。此项仅静态追踪了初始接触→移开→原窝重新接触、另一窝拒绝和取消清理路径，远端仍需实测。

本批文件：CreatureActor.Contact.cs、CreatureActor.Gestation.cs、InteractionRoom.Input.cs、InteractionRoom.Offering.cs、InteractionRoom.Tools.cs、InteractionRoom.cs，以及本交接。未改 Simulation/checks 或主协调专属 Ui/Vitals/Intervention/Debug/Supplies，未使用 Git。仅文件读取、rg 和补丁编辑，未启动项目/编译/测试/模拟/渲染/预览/转换/项目脚本/钩子。静态核对了 A 的精确查询字段、接口范围、接触丢失归零、单手势成功一次及独立损伤路径。远端需核对细茎可点性、遮挡排序、护理垫贴合、移到母体/另窝归零、暂停失焦取消、同次按住不重复扣药、出生/失败后旧命中消失、外茎击打不增加母体 Hits/头部伤害、球位于茎前时仍可推球、支护颜色/萎顿程度及不同体型姿态的附着表现。

## 第八批：跳跃转托举的局部接续与低面草花接入

- 原跳跃的形变与俯仰在通用姿态平滑之后叠加，因此突然抓起时，平滑器没有记住刚显示的最终跳跃形状。现 canHop 转实际 Carried/Falling 时，先接回上一帧最终局部 Scale/Rotation，再沿原平滑器进入托举/落下姿态。没有传入跳跃的水平显示滞后、跳高或上一帧世界位置；根位置和 Height 仍完全由模拟决定。无效固定、翻身、翻滚、入水、临危/死亡不使用此转接。暂停不启动转接，近看原有相机交接保留；已有缓放 settle 和真实跌落 land 回弹不重复添加。
- 新增 WoodlandDecorations.cs，WoodlandArt.Build 在每棵既有树根添加 Kenney grass / flower_yellowA 的真实导入模型实例。加载使用官方文档中的 ResourceLoader.Exists / Load<PackedScene>、PackedScene.CanInstantiate / Instantiate。导入必须由授权远端执行，本机没有调用加载器、导入器或游戏。模型保留原网格与材质，不强转未知材质类型。
- 运行时按层级局部变换收集网格包围盒，只接受纯 Node3D 分组与 MeshInstance3D；拒绝额外行为节点、TopLevel 或无效范围。统一缩放、居中及贴地后，草花中心距离约 .176 × tree.Scale，半径 .043 × tree.Scale，内缘约 .133 超过树干基部 .13，外缘约 .219 小于原移动排除半径 .22。没有改变树木、河道、饮水入口、碰撞、可选对象或遮挡规则，未接石头与食品。
- 资源缺失、尚未导入或模型无效时画程序化草花，每资源仅一次中文 GD.PushWarning，明确外部模型未加载；成功每资源仅一次加载日志，便于远端分辨真实接入与降级。包围盒和降级都只是新增的运行源码，未在本机执行。
- 原字节复制到 Game/Assets/ThirdParty/KenneyNature：grass.glb（11496 字节）、flower_yellowA.glb（7120 字节）、Kenney-Nature-License.txt（611 字节）。新增 SOURCE.md 记录作者、官方来源、原采集 SHA256、CC0、无修改、运行缩放、导入前提与未验证范围。没有复制整个压缩包、食品、石头或贴图。GLB 沿用 .gitattributes 的 Git LFS 规则，LFS 上传由主协调统一处理。
- 已读取现有 README/.gitattributes/.gitignore/资源台账脚本源码及导出配置，未运行脚本。现有 Windows 导出非资源过滤项只含 Data JSON；本批补入 KenneyNature 的 .txt/.md，以保留许可及来源。中央 asset-manifest.json 留主协调整合，没有运行 catalog-assets.py 或改写已有历史清单。
- 整合时主协调发现初稿草花中心位于或贴近实际 .13 树干内部。已仅静态读取原 GLB JSON 中节点变换与 POSITION min/max，草宽/高/深约 .381/.254/.392，花约 .159/.1925/.181；按原宽高比将两者半径收为 .043、中心偏移改为 (±.17,.045)，避免为抬高而拉伸模型。此尺寸下草约 .040、花约 .069 × tree.Scale 高，属于贴地小装饰；实际可见性仍待远端。原GLB未改，没有执行导入、转换或资源检查程序。

本批文件：CreatureActor.Interaction.cs、WoodlandArt.cs、WoodlandDecorations.cs（新增）、export_presets.cfg；Game/Assets/ThirdParty/KenneyNature/grass.glb、flower_yellowA.glb、Kenney-Nature-License.txt、SOURCE.md；本交接与 asset-research.md。工具仅源码/文档读取、官方文档网页读取、补丁编辑、New-Item/Copy-Item 原文件复制及目录元数据读取；没有启动任何项目、编译、测试、模拟、渲染、预览、转换、资源检查程序、导入脚本、Git 或钩子。远端需确认：跳跃上升/顶点/落地压缩时抓起无形变跳变；放下与抛出仍按真实高度/速度；暂停/近看交接；两个真实模型各有成功日志且可见；缺资源时有明确降级日志；四处树根范围、材质、尺度、遮挡；导出包包含原许可与来源且 GLB 已正常导入。实际画面精细度尚未验收。

## 第十批：眼部连续层次与泪痕余韵（六文件冻结）

眼弧加入连续平滑，虹膜/瞳孔/高光在开合0.20—0.42/0.50区间按实际几何收缩，完全闭合时不生成内层，不以透明掩盖穿模；眼角和贴面边界保持，新增上眼缘、虹膜及高光细色层。眼网格缓存限制96对，未做运行性能测量。涉及ActorEyeSurface、CreatureActor.Face/FaceMotion。

泪量从真实Pain/Fear/Alarm和当前痛苦表情读取目标，初次绑定个体直接建立当前基线，不重播旧伤起泪；后续快起慢退，泪珠亮度先软化，湿痕后退。进食不写入或清除情绪；死亡无新泪，临危目标限幅，暂停delta为0不推进。保留旧纯接口供已有检查使用。涉及ActorTearSurface、CreatureActor.Tears、CreatureActor.cs。

B声明仅六文件静态读写，未运行项目、检查、编译、渲染、预览、导入或Git；主协调已核对引用、闭眼几何、平滑、暂停传参和缓存边界。A已在ActorGeometry.Smoke新增3组检查源码：眼层连续收回及规范圆约束、连续眼弧端点/外眼角、泪目标与不同步长/读入/暂停；保留旧断言，全部未运行。实际近看、受伤后递食和安慰余泪、长期网格切换及暂停表现待独立远端。

## 第十一批：真实离水后关切（两文件冻结）

CreatureActor.WaterConcern.cs和InteractionRoom.cs接WaterSupportStatus的真实TargetId/EventId/Phase；Approaching只在站稳时看向目标，实际走路仍由模拟决定；Watching按已完成接触及观察者自身Pain/Fear/Alarm限制缓和，保留Hurt/Frightened/Exhausted优先，不治疗或修改位移。落水时原求助/恶意旁观表现保留；岸上不继续交替向人类急切呼救。

按当前Creature引用、目标引用和水事件建立基线，近看/隐藏/死亡/跨区/失效立即清理，暂停不启新事件；初观察直接读取当前阶段。B仅静态写上述两文件，未运行/Git；root已检查唯一调用和各状态优先。远端需观察真实救起后靠近/守候、不同体型距离、食水/抓起/疼痛中断、再次落水新事件、高Alarm观察者、暂停及读档。B10眼部六文件未在本批修改。

## 第十二批：连续进食口腔（四文件冻结）

ActorIntakePresentation新增纯形状记录及SampleMouth，直接采样EatingRemaining，保留旧Sample、FrameCount和五帧Layers；准备接触不提前咀嚼，嘴伤沿原实际摄食时钟减速，没有表现层第二倍率。ActorIntakeMouthSurface以统一椭圆拓扑生成口腔及舌面，保留原中心、.012/.018贴面偏移；舌面规范半径限制.88，避免闭口时伸出口腔。

CreatureActor.Intake增加口腔阴影、唇缘暖色及舌面渐层，形状量化缓存上限128。CreatureActor.Interaction改为实际连续样本；进入.08秒、结束/取消.10秒只交接嘴型透明度和几何，不推进任何摄食/吞咽。新Creature引用直接建立当前进度基线，暂停冻结，临危/死亡立即让出进食嘴，原恐惧/疼痛表情权重保留。

A在ActorGeometry.Smoke新增3组纯检查源码，覆盖实际接食前静止、同进度采样不重播、摄食/吞咽边界连续、同一贴面口腔内舌面包络；旧断言保持。主协调核对程序集链接边界、调用及网格/透明交接；没有运行编译、检查、游戏或预览。真实近看、两口交接、嘴伤减速、渲染透明排序和长期缓存性能均待独立远端。

## 第十三批：袭咬准备的连续眼眉（四文件冻结）

新增CreatureActor.HungerFeeding.cs，CreatureActor.cs、CreatureActor.FaceMotion.cs、InteractionRoom.cs接只读HungerFeedingStatus。仅实际Pursuing/Preparing才附加紧盯眼眉，ContactProgress控制准备压力；本体Pain/Fear/Alarm削弱敌意并保留忧虑，显式痛苦、害怕、疲惫和求助不被覆盖。Swallowing交回原真实进食嘴型。

按本体/目标引用及EventId/阶段保留当前观察；换目标、失效、近看、工具介入、水域优先时撤销本预告，暂停不启新预告也不推进准备。原有面部平滑正常进行，暂停中失效可撤去旧附加眼眉压力；不清真实Angry/Hurt状态。没有新箭头、浮标、位置、身体姿态或摄食时钟。目标名字和准备进度由root普通面板显示。

B声明四文件仅静态读写，root已核对当前DTO与表现优先；未运行项目/编译/检查/预览。远端需观察完整追击、可中断准备、入口后的嘴型交接、制止/食物/目标改变以及袭击者自身伤痛。当前沿用原近身关系距离，不能据此宣称已完成嘴部精确物理接触验收。

## 第十四、十五批：布结与发束几何

B14已独立提交16b1f9251e0fb3f4457a92d05084646dbbebf5e8：ActorAccessorySurface/Support、CreatureActor.Sculpt把结心从椭球换为24×64封闭收腰布褶，原包络不扩大；同一BowKnot用于真实表面和支撑，帽侧先乘.38再贴合。ActorGeometry保留原头结/帽结可见与间隙断言但改采真实表面，新增2组封闭/包络/全支撑顶点来源检查，未运行。

B15仅ActorHairSurface、CreatureActor.Sculpt：原椭圆函数保留SmoothPoint，新Point只在前半内收Z，X/Y投影、背面、根尖及外包络保持。两道浅槽分出三束，侧发上限.004、刘海.0025；刘海受原FaceHeight(.012)可用间隙软限制，不超过余隙一半。移除原高光圆管，同一凹槽决定网格与颜色，不增加20×32段数。后脑Shell/Contains和包扎原规则不改，实际射线与支撑读同一发束几何。

A独立新增2组ActorGeometry检查源码，覆盖七种发束的原投影/背面/包络、前跨度与皮肤间隙、接缝/根尖及真实网格行的有限非退化法线；旧刘海末段.01间隙和摄食嘴位/支撑断言保持。root已静态审阅软限制公式和构建引用，没有运行、渲染或预览。近看发束、额头间隙、变形与食水支撑仍待独立远端验收。

## 第十六批：防护姿态中的实际命中

高痛guard原本遮掉已结算的新slap/flick/poke，B16在不替换主防护姿态的前提下，用实际ReactionSerial和ReactionPose叠加.22秒短压缩/偏转；React会先清旧姿态，因此谈话序号不能重播旧命中。首次观察、读档、隐藏、暂停的新身份只建基线，失能、水中、托举、侧倒等不触发；固定状态仅保留较小压缩。方向转换到当前局部朝向，无XZ或根位置写入，旧命中主姿态不重复叠加。ActorGeometry新增2组方向、边界、衰减及非法输入检查源码，未运行；连续拳击高痛、固定、失能和支撑的实际手感仍待远测。

## 第十七批：真实水中尝试反馈

CreatureActor.Water.cs移除独立sin周期的够岸抬身；新WaterAttempt文件只读A12复合落水/代号/序号事实，实际Advanced/Blocked后.55秒短抬身回落，受阻幅度较小，同时轻微增强现有波纹和水滴。Escaped不重复增加成功动画，沿原离水过渡。首次观察、换区、近看、隐藏及新落水事件只建当前基线，暂停冻结且不补播暂停中已出现的新身份；不额外写XZ位置、危险或救助规则。root静态核对dt暂停传递及真实查询资格；画面与输入手感待独立远端验证。

## 第十八批：持物递食的连续接触

Input将原始sampleSeconds传到持物身体入口，新HeldSupplyContact仅存上一实际source与actor引用。先显示工具并定位真实嘴部，再按两帧同源同对象接触累计；首次、换源/actor、离开重接、工具隐藏或超长帧只建基线。dt和原始sampleSeconds均须有限正数且不超过既有.15秒上限，断开通过Continue(false)清ContactElapsed，模拟在Elapsed增加之前返回，不借用伪造时间。无强制接近动画和世界点阈值，正常跟随移动嘴部仍可连续递送。

拒绝、空源、遮挡、暂停/失焦/切工具/取消和瓶口分流清身体基线；原离体瓶口基线不改。实际入口后不再Continue，原EatingRemaining时钟继续吞咽，保持手势到原松手链，不重复扣料或清已发生反馈。root已读相关入口与模拟返回顺序，未运行；检查低帧率/卡顿、重新进入、同ID新actor、新source、拒食及身体瓶口往返的实机表现。

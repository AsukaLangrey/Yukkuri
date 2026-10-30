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

## 第十九批：帽檐布层

ActorAccessorySurface新增HatBrimBase保留原壳、HatBrimRecess只在.66<u<1内以.0025*sin²径向窗*sin⁶(4角度)形成八道浅凹褶；顶面内收Y，原XZ、内外沿和底面不动，厚度至少.0135。原帽冠包络以外才起褶，原头部净空保持。颜色同一凹深轻暗，不加节点、圆管或缓存；上下网格与支撑共同使用BrimRows24/BrimSides96。

A独立新增ActorGeometry三组检查源码，覆盖有限域、原壳/底面/投影/厚度、周期/边界/封口及真实网格支撑；旧帽头间隙与封口断言保留。ActorArt.Preview仅上下帽檐两处采样常量同步，不运行预览。root已静态核对同源公式与采样顺序；八道布褶的实际清晰度、光照、法线和压扁/掉落后的点击与支撑仍待远测。

## 第二十批：实际热逃与紧绷余波

新增纯ActorHeatPresentation.Sample(heatTension,restrained,fleeing,actualTravel)，输出Scale/Pitch/可空Heading，没有位置、偏移或时钟。固定沿原缩放；自由余热仅小幅收缩，只有当前burn-flee且真实XZ推进才给前倾与朝向。Heat.cs每帧读真实位置建立对象/区基线，首次、隐藏/近看/暂停及上位失能状态不借旧运动；受阻保持本次真实移动取得的朝向，不抬身假跳。停止动作后HeatTension原本的缓降仍可见，不重复产生反应或改变伤害。

删除原固定/自由热姿态的两处sin周期位置叠加，保留主姿态、原平滑与真实装饰/地面支撑。水中、临危死亡、托举跌落、仰倒滚动休息、冷藏/干预优先，工具火焰取消链不改。A新增ActorGeometry三组纯源码检查并显式链接新纯文件，覆盖有限值、幅度边界、受阻/非法运动无朝向、余热不重放及真实方向；root核对调用与依赖，未运行或渲染。

## 第二十一批：高痛防护中的真实落地

ActorReactionMotion.SampleGuardedImpact新增land分支，.22秒内局部压缩后单调恢复，固定/普通压缩分别.03/.06；Offset和Rotation均零，Y从不超过1，与方向无关。ImpactFeedback仅将当前有效ReactionPose=land纳入原真实serial资格，最终guard才叠加，原主land不双叠；普通放下没有land则不伪造冲击。初见/暂停新事件/载入、水中/托举跌落中/临危死亡等原门控保持，之后仍按实际网格计算支撑。A新增两组纯源码检查，旧拳击/弹额/戳脸及主落地断言保持；root审查真实Falling落地到Feedback的接线，未运行。

## 第二十二批：独立瓶养的分枝几何

仅DetachedStemActor将三根原方枝换为封口10边渐细圆柱，底半径.009/顶.005/高.22，截面位于原.018方柱内部；中心、±45度旋转、长度和三份Mesh引用保持。芽数显隐、实际Mesh.GetFaces射线、EventId及失败/出生/清理链不变，不动模拟占位或加节点/时钟。按可直接核对的几何边界静态审查，未新增复刻常量的测试；实际低面观感和枝芽连接留远端观察。原先提出的旧出生事件重播因没有现有可达路径证据而撤回，没有新增账本。

## 第二十三批：眉毛浅圆脊（三文件冻结）

实际修改ActorBrowSurface.cs、CreatureActor.Face.cs、CreatureActor.cs。保持原X/Y及中心脊Z，在原前包络内以.0015*sin²(pi*u)*(2v-1)²收回两侧边缘，贴面偏移仍至少.0095。真实网格使用同一Point与有限差分Normal，端点/退化法线回到原脸部法线并保持朝前；同一凹深控制轻微颜色层次。两眉独用一次复制的材质，不修改共享头发材质；原两节点、24×4网格和192项缓存上限保持，射线与支撑仍来自真实网格。

本批先核对真实手喂、自然吃喝与陪伴后的脸部优先级，未找到食物瞬间清空余痛/惊惧的缺陷：Feedback优先临危、疼痛、Fear/Alarm与尚未安定状态，摄食完成也走此链；陪伴保留悲伤/不安，眼眉继续叠真实戒备，泪水独立读Pain/Fear/Alarm，咀嚼只接普通嘴型。因此没有改模拟或情绪优先级，也不把几何细化描述为情绪修复。

A已新增ActorGeometry.Smoke三组纯检查源码，覆盖原XY/旧前包络/最小间隙、中心端点和对称浅凹、实际24×4网格在±30度正常域的有限单位朝前法线与横向切线正交；旧断言保留。B三文件已冻结交root整合，未执行编译、检查、项目、预览或渲染。正面/斜看圆脊清晰度、端点光照、表情旋转时与眼部的间隙及头发材质不受影响，均待独立远端确认。

## 第二十四批：中断托举后用手掌直接点地放置

仅InteractionRoom.Input.cs与Tools.cs，新增共享CanPlaceWithIdlePalm资格：已选Lift、真实Carried、无鼠标或模拟手势、无持物/救援分流、未暂停失焦或打开面板。修复选中手掌抓起→空格暂停取消手势但保留托举→恢复后点击空地被原“仅未选工具”分支漏掉的实际路径。普通视角空地点放，近看按下预约后仅未转镜头的松开可以执行，并复验空地和真实资格；旧甩速不参与PutDown。

候选落点标记使用相同资格，暂停或拖镜头回实际支撑点；地面/水域保持y=0投影，最终仍由原CanOccupyRoom allowWater裁决并在实际水域EnterWater，不改身体位移/高度/伤害/入水规则。根协调静态核对CancelGesture清预约与运动记录、原食品/独立窝优先分流和暂停/失焦路径。没有输入自动化或渲染实测，远端按键链已写testing请求。

## 第二十五批：火机封口火滴几何

本轮先只读核对拳击与火机输入链，未找到新的漏发、双发或换目标借用证据。拳击为非连续动作，仅按下执行一次，成功Changed才登记；松开和每帧不再结算，hits读取各目标真实HitCount/HitRemaining而非独立累加。火机先核对真实Id/Kind/ObjectId接触，离开清ContactActive；本次Continue返回的姿态在同帧进入Actor，固定与自由切换不依赖台词刷新。暂停/失焦/切工具清手势，火焰显示仍要求实际手势、接触姿态和工具可见。本批不声称修复这些已正确的链路。

实际修改InteractionRoom.Tools.cs，新增纯InteractionLighterSurface.cs。原两个轴向椭球替换为同一封口火滴网格，规范域Y=u-.5、r=2u(1-u)(1-.45u)，腹部偏下，上端连续收尖；因r≤2u(1-u)≤sqrt(u(1-u))，每点仍在原半径.5单位球内。极点为(0,±.5,0)，v=0/1接缝同点；解析法线朝外，极点用±Y。16×16网格跳过两极退化半片，共480非退化三角，两节点共享一个局部ArrayMesh，不加全局缓存。

外层(.027,.065,.027)、内芯(.019,.025,.019)、原位置与材质保持，局部+Y仍是接触方向；现有燃动后外层顶点恒为.0115-.0325*length+.5*.065*length=.0115。没有额外余火、位置、伤害、输入或表现时钟。三角绕序沿Godot官方[SurfaceTool顺时针正面约定](https://docs.godotengine.org/en/stable/classes/class_surfacetool.html)；源码设置法线后再加入顶点，与项目原做法一致。

精确纯接口及包络/封口/法线检查需求已发A，等待其独立检查源码；没有降低原断言。B两代码文件冻结，交接文档随批收取。未运行、编译、检查、渲染、预览或导入；火滴轮廓和两层可辨性、明暗、侧面接触顶点、长按固定/自由对象及松开立即熄灭的实际观感仍待独立远端。

## 第二十六批：泪面浅凸与透明收边

仅ActorTearSurface.cs与CreatureActor.Tears.cs。原XY轮廓和眼下范围不变，主泪面偏移改为.015+.002*max(0,1-|q|²)²；高光先沿原缩放/平移转换到主面的真实q，再取同一偏移+.002。高光q的保守平方半径≤.6032，始终在主泪面域内，同XY间距固定.002；主面最高.017、高光最高不越旧.019。四节点、8×48拓扑、原SculptSurface同源差分法线保持，未加缓存或其它装饰。

主片外圈从规范半径.70、高光从.60开始以平滑函数把顶点alpha降到0，中心保留1。SculptSurface已有tint路径会复制材质并启用VertexColorUseAsAlbedo，随后EnableSurfaceFade复制并启用Alpha；原SetSurfaceOpacity仍只调整该私有材质AlbedoColor.A。[Godot材质源码](https://github.com/godotengine/godot/blob/master/scene/resources/material.cpp)中的顶点色与透明输出乘法支持两层alpha相乘，因此边缘随原Amount同步淡入淡出，不会把整片时间状态覆盖掉，也不污染共享材质。此为官方源码与本项目接线依据，不是本机渲染验收。

ActorTearPresentation的痛/Fear/Alarm来源、初始读档基线、死亡归零和推进函数未改，没有新增情绪重播。原泪面gap .0149..0191及眼下范围断言保持，纯坐标/浅凸/边缘接口已发A，在其A17真实旅程后排队补针对性检查。B两代码文件冻结；未运行、编译、检查、渲染或预览，近看浅凸光照、半透明排序、边缘柔和程度及惊惧后进食余泪仍待独立远端。

## 第二十七批：伤痛与失能表现只读核对

核对本帧手势→最终GuardPose、ExpressionFor、摄食嘴禁用、身体瘪形、非Active眼层收闭、伤痕与同部位包扎、残障跳步资格及读档重建基线，未找到确定的新漏接，未修改代码。陪伴/关切/孕育/热表现均有失能门控；死亡清真实食物/热/孕育和手持支撑，固定装置仍单独保留。与A18互通后也没有新增Game接口需求；这些是静态覆盖范围，不是完整虐线输入或实机验证。

## 第二十八批：巧克力碎块平顶与折面

仅InteractionFoodSurface.cs与InteractionFoodArt.cs。原饼干七颗巧克力使用米粒同类椭球参数面，本批仅chips分支换为八边四环封闭倒角碎块。各块原Center/Radii/Yaw不变，规范环为(y,r)=(-.9,.4)、(-.35,.7)、(.35,.7)、(.9,.4)，上下扇面及三段侧壁每块64三角。顶点规范平方范数最大.97，整面片也位于原凸椭球内；上下真平顶、侧面按实际变换后的平面生成朝外法线，不沿用球面法线。

公开FoodChipTriangle(A,B,C,Normal)和ChipTriangles(index)只读枚举，Game与A纯检查共享同一实际拓扑。固定七组数组一次构建，不产生无界缓存；仍合并为原一个碎屑Mesh节点。chips=false的饭粒、勺中饭形状和法线保持，饼干主壳、地面入口高度、SweetOffer/手喂前缘、射线包络、真实份额及入口后的隐藏时机不变，没有导入外部素材。

A已收到真实枚举接口，待其排队补闭合/平面法线/旧包络检查；B两代码文件冻结。源码发出的每块三角数从原144降为64，这只是拓扑数量，不代表画面质量已验证。未运行、编译、检查、渲染或预览；近看平顶和折角是否清晰、巧克力与饼干连接、正面/斜面手喂接触和地面食物的实际观感仍待独立远端。

## 第二十九批：包扎底布浅折

只读核对未发现伤痕被假护理遮蔽：伤痕隐藏与对应护理层显示均依真实HasDressing(part,side)，本批不改这条规则。实际仅改ActorDressingSurface.cs与CreatureActor.Dressing.cs，把纱布两侧原等距底布增加浅内凹布折；中央、纱布垫和原外沿倒角不动。

新增BackingFoldDepth(radius,angle)，r≤.56和r≥.88严格归零，两条折痕仅位于原圆角规范坐标|x|约.70、宽窗.14内；两端用平滑窗和沿y衰减，深度上限.0015。折区原偏移恒.019，改后至少.0175；其余原倒角保持，因此全底布最小皮肤/后脑壳间距仍.007、最高偏移不越.019。纱布垫最高.023及所有坐标/色纹不变。原Point仍经ActorSurfaceMarks.Project贴合，真实网格差分法线与Samples/LowestY/CushionLift同源，不改身体射线或增加支撑外包络。

底布顶点色按相同凹深从1轻暗至.94，SculptSurface只在BuildDressings首次构建时复制一次私有材质并启用顶点色，未新增每帧分配、节点或缓存。A已收到精确纯接口；原包扎外缘、皮肤净空及垫子支撑断言保持，待其独立检查。B两代码文件冻结，未运行、编译、检查、预览或渲染；近看左右浅折、不同部位/体型包扎及翻身休息后的真实贴合仍待远端确认。

## 第三十批：救援用品光标与真实悬停提示

仅InteractionRoom.Input.cs与InteractionRoom.RescueTool.cs。原RoomPointer已会在命中对象时变蓝，本批没有声称此前完全无救援反馈；实际补足装备后空场景也可辨识的双色四段救援圈。沿用同一Control与即时绘制，不增世界节点、缓存或计时器。只有未遮挡的场景内且窗口有焦点、没有平移/转镜头时显示；暂停时若重新装备也只显示非候选状态。未装备时保留原Active条件和普通圈绘制。

只读WaterRescueHint按真实命令核对暂停、Body目标、InteractionPlaytest和当前区、非死亡、WaterEnvironment真实入水、用品库存、固定装置。临危不额外拒绝，也不引入普通ValidateInteraction才有的冷藏/干预限制。全部前提满足仍仅说“点击尝试救起”，不复制私有岸边落点搜索；真实成功、库存消耗、位置、失败原因继续由RescueFromWater决定。没有改点击分流、射线/遮挡、成功后的NotifyActualWaterRescue或取消链。

救援装备时悬停提示优先沿真实ContactAt，附近同时采样到的食物不再覆盖落水对象的救援原因；非救援时原食物提示覆盖顺序保持。无落水、空库存、仍固定、暂停及非身体目标使用弱色圈；满足已知前提时圈中心加点，仅表示可尝试。救援实际成功后对象已离水，下帧自然显示非落水原因；拒绝不播成功。切工具、失焦、暂停、存读档仍经原CancelGesture清装备状态。

B两源文件冻结，未运行或做输入/渲染验证。远端需核对装备后空地与水面圈、落水体与旁边食物重叠、固定/临危/死亡目标、空库存、岸边无位失败、实际救起后原因刷新，以及暂停后重新装备、失焦、面板遮挡和切工具时圈的消失与原光标恢复。

## 第三十一批：贴身滴水封口几何

先静态复核Water、WaterAttempt和WaterTransitions，未找到新的真实水域事件断链；本批不修改普通挣扎、实际受阻/推进或成功离水。仅新增ActorWaterDropletSurface.cs并修改CreatureActor.WaterTransitions.cs中5颗贴身滴水的构建，原6颗挣扎溅滴及8颗入离水散滴不变。

原5滴为(.018,.035,.018)缩放的对称SphereMesh，现沿规范Y=u-.5保留两极，半径改为sqrt(u(1-u))*(1-.42u²)。半径始终不超过原球相同高度的截面，所有顶点及其三角片均在旧凸球包络内；下腹较圆、上部收窄。解析法线直接来自同一半径导数，两极为±Y，v=0/1同缝。12行×16边共352个非退化封口三角，极点不生成退化半片，绕序沿现有Godot顺时针正面约定。

公开WaterDropletVertex(Position,Normal)、WaterDropletTriangle(A,B,C)和Triangles()只读枚举，游戏逐点使用同一固定数组，不重抄近似拓扑；5滴在首次BuildWaterTransitionEffects时共享一个局部ArrayMesh，不增节点或逐帧缓存。原材质/透明、原缩放、真实WetnessAt、ContactPoint、捕获起点后的重力轨迹及所有时钟保持。最小包络不改变身体支撑或命中；这些装饰仍挂原Actor根节点。

精确纯接口和包络/封口/同源法线检查需求已发A。B两源文件冻结，未运行、编译、检查、导入、渲染或预览。待远端确认近看下腹/上肩轮廓、顶部光照、不同身体姿态的滴落起点，以及擦干对应部位、暂停、重新入水、读档时原显隐与计时保持；没有将源码形状变化称为观感已验证。

## 第三十二批：手持护理垫上层纱布浅折

先静态复核实际抓起/拖移/松手、取消后仍托举、水中合法落点、真实甩速及姿态/射线接续，未发现新的确定缺口，不修改这些输入与规则。本批仅InteractionCareSurface.cs和InteractionToolArt.cs：手持Care/SupportStem工具的上层纱布增加两道浅内折，与B29油身上的包扎底布不同。

GauzeFoldDepth只在上半面生效，沿规范|x|=.55两侧、半宽.20形成平滑窗，|z|从.68至.88淡出，再乘cos²(uπ)。最大内收.0009；上半面原Y=.024+.006c，新Y至少.024+.0051c，因此仍在原壳上下半面之间。原XY、u≥.5下半面、两极、中心最高点.03、基底、PadBottom=-.022及缝线全保留，原PlaceContactTool的+.003接触间隙不变。

最终没有增加采样：公开GauzeRows=12、GauzeSides=48沿用旧值，仍为13×49参数点、576格、1152个提交三角（不改原极点重复）。旧采样已穿过两道折窗，因此不为浅折改变下半面的实际顶点和拓扑。ToolArt仅改为引用这两个同值常量，继续由同一Pad坐标进行既有差分法线计算；节点数、材质和工具位置不变。

已读原护理垫几何检查，现有有限值/PadBottom下界/XY范围/闭缝断言保留，精确纯接口交A仅在原组内补上半内收、下半面/中心/边缘不动的小量断言。B两源码冻结，未执行任何项目、编译、检查、渲染或预览；护理垫贴面、纱布折痕的可辨性与光照仍待远端确认，属于工具表现细化而非抓放缺陷修复。

## 第三十三批：独立窝食水地面入口接线

A21确认存活独立瓶的补给会被死母体/空选择阻断：原普通点地仍调用选中身体的ExecuteInteraction。B仅修改InteractionRoom.Input.cs，快捷饭饭/零食/清水点地和显式PlaceMeal/PlaceSweet/PlaceWater共用PlaceGroundSupply；Management调用A新增PlaceInteractionSupply，以当前真实小院为归属，不借母体资格。库存、干岸/瓶占位、对象上限、供给高度仍由模拟共享核心裁决。

CloseUp及其他原模式保留Selected→ExecuteInteraction，近看左键地面预约与转镜头/松开门控不改；PutDown/GiveBall保持原对象资格。暂停/失焦/面板/真实地面命中沿原入口，不新增虚构入口点。外茎与独立窝命中仍按旧分流明确处理，真实Body才调用BeginClickOffer并选择那只油；没有选中对象时可以放地面，不会发明嘴部目标。

只读交叉审root的Ui/DetachedStemUi空选择修正：公共时间、力度、反馈和实际HeldByHand提示先刷新；无Selected清旧对象文字后仍刷实际独立窝DTO与真实进度，进度最后覆盖提示。独立窝空手提示排除救援/真实持物；正常身体分支原规则保持。本代理未编辑root文件。普通地面物体构建与显示不依赖Selected，供给可进入原实物递送链。

B单源码与交接冻结，未运行或做鼠标验证。远端需以死母体选择/Management空选择分别放饭水并拿到真实瓶口补给；核对库存耗尽、河道/瓶占位拒绝、暂停与失焦不新增份额、近看仍沿原对象资格、普通身体点喂和PutDown/GiveBall不受影响，以及空选择下物品/反馈/独立窝进度显示。A负责模拟和相关旅程检查源码。

## 第三十四批：固定具仰躺时接通真实热紧绷

先复核进食时受击/灼烧、救后与受威胁时接甜食的最终表情链，未找到新的确定错配：Feedback与最终ExpressionFor保痛/恐惧/未安定优先，实际进食嘴不驱动快乐眼眉；余泪独立读取痛/Fear/Alarm。ReactionPose不入档，guard命中与泪水/进食首次观察以当前状态建立基线，未添加重复防重系统。

本轮身体核对发现真实漏接：Pin把Restraint设为Frame且Supine=true；Burn实际累积HeatTension并返回burn-tense，但Heat.cs的资格一律排除Supine，ActorReactionMotion也没有另一条burn-tense身体缩紧。于是固定框中的油保留痛脸，却漏掉已有固定热紧绷形变。

仅修改CreatureActor.Heat.cs的资格，让真实Frame中的Supine可以进入原固定热分支；自由仰躺仍排除。继续排除水中、托举、跌落、滚动、休息、冷藏、干预和临危/死亡。固定Sample仍只有有界局部缩放，Pitch=0/Heading=null；已有Supine角度/偏移、框结构、真实位置与碰撞规则不变，最终KeepBodyAboveSupport继续按实际变形网格补支撑。不新增热时钟、逃跳、事件或模拟伤害。

其它身体状态已静态确认实接：Damage/PermanentLoss各自驱动瘀痕/疤痕，真实包扎才遮同部位；永久足损约束FootIntegrity恢复上限并减弱/禁止跳步；临危/死亡使用最终瘪形并停止摄食嘴；水中Struggle随体力和痛恶化，精疲/死亡削弱或停止主动水滴。近看沿同一RefreshInteraction链，饰品随身体逆缩放并重新计算帽间隙。这是源码覆盖结论，不是实机验收。

B单源码冻结，既有纯热检查已覆盖固定分支无朝向/零Pitch及有界形变，不增重复大检查。未执行项目、编译、检查、渲染、预览或Git。远端应以Pin固定→火机真实接触→移开→冷却/解除固定核对身体缩紧、框内仰躺与净空；对照自由仰躺、落水、临危死亡仍保原优先，不出现热逃跑或旧朝向。

## 第三十五批：真实新瘀伤与永久旧伤形状区分

先只读核对护理叠层：敷料、伤痕、泪水和湿痕均挂在同一_visual下，没有额外重复身体缩放；敷料在首次支撑采样前创建，显隐先于最终地面/框底支撑。帽饰保留逆缩放和帽冠净空；托举锚沿最终皮肤最低点并考虑底部敷料。未找到新的确定错配，因此没有改姿态、输入或支撑规则，也不把源码判断称为实机无遮挡。

本批仅CreatureActor.Injury.cs和新增纯ActorInjurySurface.cs。原新伤是统一椭圆，旧伤是同椭圆乘(.7,.3)，均只有单色；现在新伤以.88至1的有界径向系数向内收成轻微不规则斑块，并用中心向外的连续颜色层次。旧伤保留横向端点，纵向乘.55+.45cos²(a)收成细长痕，以颜色形成浅脊层次，不增加表面高度或血腥细节。

原source.Size*.82、旧伤额外(.7,.3)、ActorSurfaceMarks.Project以及法线偏移.011/.012不变；规范表面坐标长度不超过旧单位圆，中心不动。原5×32采样、每部位两节点和实际SculptSurface差分法线保留，不增加缓存。身体命中与支撑路径不改。

Point/Surface/Tint/EdgeOpacity公开给A；Tint在极点颜色一致，RGB有界，边缘alpha沿用sqrt(1-r²)。SculptSurface在创建时复制独立材质并启用VertexColorUseAsAlbedo，EnableSurfaceFade保留顶点色并启用Alpha；原SetSurfaceOpacity仍只写材质alpha，故顶点收边乘真实Damage/PermanentLoss强度。HasDressing同部位遮蔽、永久旧痕和新伤的各自显隐完全不变，零伤势不造痕迹。

两源码冻结，A仅补一组纯边界检查源码，未运行任何程序/编译/测试/渲染/预览/Git。远端仍需确认新伤与旧痕在近看、躺倒、固定和托举时可辨，包扎遮蔽/到期显露、真实湿身和余泪叠层不影响眼口辨识；未见实机，不能声明视觉已验收。

## 第三十六批：独立窝幼芽随真实进度长大

先只读复核生育链，未发现提前出生或加载重播的确定缺口：附着茎需真实StemCareStatus与Gestating，胎生读取孕育/恢复阶段；新增幼体且BirthEventId属于CompletedBirths才触发短反馈，读档BuildActors整体建立对象不播出生。独立窝从当前区DetachedStems按EventId/未清理同步，不依赖Selected或母体存活；真实整窝出生完成才显示Born枯梗，离体窝不触发母体身体分娩提示。

原DetachedStemActor仅按Progress拉长整株Y，芽体宽度与眼嘴始终固定。本批只修改该文件并新增ActorStemBudPresentation纯接口：Sample(progress)用真实0至1进度的smoothstep把芽尺寸从.18平滑增至1，OffsetY=-.065*(1-size)，保留旧芽下极点。SceneArt.Ball原单位球半径.5/高1，成熟芽尺寸仍(.15,.13,.14)，故缩小后的椭球始终在旧芽椭球内，成熟时原位置和尺寸完全恢复。

保存原三个芽中心，不增加节点；眼嘴相对芽中心的位置和各自原尺寸同乘size，眼睛再乘原完整度/含水/失败疲惫因子。旧枝条位置/长度/旋转不动，最小芽尺寸仍覆盖原中央及两侧枝尖；真实射线继续读取当前Mesh.GlobalTransform。整株高度、瓶口/水位/营养/破损、根位置和模拟占位全不变。

数量门控静态复核：原n<PlannedChildren同时控制芽、嘴、枝与双眼，本批完整保留，固定分配3节点不会把不存在的第三芽显示出来。Loose/Bottled读取同一真实Progress；Failed沿停止时的进度保留发育尺寸并用原枯损姿态，Born隐藏整株，Cleared沿原Room同步移除。没有计时器、出生事件、母体查找或暂停期间自行生长。

两源码冻结，纯StemBudGrowth(Scale,OffsetY)/Sample接口交A至多补一小组边界检查。未运行项目/编译/测试/渲染/预览/Git。远端需观察早中晚期幼芽与枝尖连接、五官贴合和可辨性，2/3芽正确数量、停育/失败保留阶段、读档与暂停不前进、母体死亡后持续瓶养、真实出生后幼芽消失及新幼体出现；本批是阶段表现细化，未宣称视觉验收。

## 后续只读审查：工具中断与取消

核对Input、Offering、HeldSupplyContact、DetachedStemInput、Tools，以及主线Ui/保存加载的调用入口。切工具、换对象、暂停、近看切换、弹窗和失焦都进入CancelGesture，清本地接触/甩速/递食返回/点击工具反馈后结束模拟手势并释放实际食水。释放时若最后一次采样已取消，不再借旧目标或甩速施加动作；火焰需要当前有效Burn接触、焦点及工具可见。保存先取消，读档换Sim前取消并重建Actor。未找到新的确定断链，未改这些源码；取消Lift保留托举是原语义，可经空地点放恢复。A独立核对模拟唯一手势的取消与单次结算。以上仅静态审查，快速切工具、窗口切换及实际按钮事件顺序仍待远端输入验证。

## 第三十七批：支撑采样不再留住历史表情网格

本晚资源生命周期审查发现明确的二级引用问题：眼/眉/摄食嘴的形状缓存分别有96/192/128上限，但Support原以Mesh为键缓存顶点，只在Actor退出时清空。真实翻滚会让脸部进入支撑采样；形状缓存清空后重新生成的Mesh成为新键，旧表情Mesh仍被支撑字典保留，绕过前者的数量上限。

仅改CreatureActor.Support.cs和CreatureActor.Contact.cs：支撑字典现在按已有MeshInstance3D节点存当前Mesh与顶点数组，同节点最多一项。节点换Mesh时立即丢弃旧项，即使隐藏或AABB无需细查也不保留旧形状；只有真正需要详细支撑采样才从当前Mesh.GetFaces创建数组。已释放节点从支撑列表/缓存移除，空或无效Mesh清该项。可见性、遍历顺序、球的解析最低点、非球真实顶点/矩阵、最终+.003地面修正均保留。Actor退出沿原支撑/射线清理，另显式清空本晚新增的摄食形状缓存，不手工Dispose仍由渲染节点拥有的Mesh。

同轮静读水滴/泪水/外茎/独立窝/火机/低面草花的创建路径：水效和外茎按首次需求创建，五滴共享一次局部网格；伤痕/敷料/泪水/芽皮/叶色/火焰的可变材质在创建时独立复制，更新只写本对象；工具仅动作种类变化才拆旧节点重建；草花资源缓存只含两条固定路径，不每帧导入。眼/眉/摄食嘴有量化及有界形状缓存，当前修正补上其支撑引用边界。水滴/出生/热/受击/面部各表现沿原暂停门控，未找到新的确定时钟误推进；实际状态失效的即时隐藏不当作时间推进。

两源码冻结，A只做交叉静读，不增加镜像测试。未运行任何程序、编译、测试、渲染、预览或Git；源码保证的是缓存引用数量受现有节点数约束，不是已经做过内存或性能测试。远端仍应长时间切换表情/摄食并翻滚，再反复读档/换世界，确认支撑位置与暂停表现不回退，并观察实际资源占用。

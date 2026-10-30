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

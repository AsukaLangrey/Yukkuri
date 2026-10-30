# A 功能玩法线

## 第一批：2026-10-31 01:25 提交协调审查

本批已写入源码和16项远测检查源码。本机没有编译、运行、测试、渲染、预览或执行项目脚本；以下只有源码静态审查结论。主协调收口期间A停止写本批文件，等明确恢复通知。

### 实际改动

- 新增连续情绪：独立馒馒来度 `Interaction.Emotion.Manmanlai`，以及警觉 `Alarm`、持续安全时间和近期威胁记录。伤害的记忆不会被一口食物重置。警觉约束愉快表情、玩球、主动蹭蹭和自愿配对；身体健康、当前恐惧、长期信任仍各自独立。
- 原共享 `RememberPlayerHarm` 接既有目击/听闻系统，覆盖旧掌掴、抛落、烤脚和新增拳头/火机。受害者连续累积警觉；公开事件按受害者2.5秒节流并保留未上报强度，不逐输入帧刷事件。
- 河域实际可进入。放置、抛落、受外力运动允许跨过河岸；自主普通陆行仍避水，落点碰撞仍检查身体/树木/设施。入水后挣扎、向岸移动、尝试跳出；消耗体力并逐渐受伤，经原危重/死亡路径结算。旧 `LethalInjuries` 开关保留。
- 救援道具初始1份、单价18、每次救援消耗1份。暂停的商店允许购买，救援本身拒绝暂停；无限资源免扣钱但不绕过库存交易。失败不扣库存。水中普通托起和直接冷藏不能绕过救援道具。
- 救上岸不补生命、不修脚、不清恐惧，保留湿透与恢复期。人为制造落水后立即救起不刷信任/安全感；旁观感激要求同区可视范围且有冷却。
- 新增善意、粗心、自负性格参数，按原性情初始化一次后保存。初始害羞油更谨慎善良；原自负油较自负且善意低。粗心油喝河水有概率滑落；善良油靠近岸边接应/呼救，实际接应需近岸、可视、身体能挣扎及可用落点；恶劣油嘲弄并增加被救者恐惧和怨恨。
- 极饿且善意低的油有概率追逐、袭击同伴；普通可用近旁食物优先，喂食/隔开/抓起可中断。已死个体的有限残躯可被摄食，不复活、不无限提供食物。本批没有制作露骨表现，也没有把“活体吞食全过程”声称为已完成。
- 新增 `Punch` 和 `Burn`，保留旧 `Slap`/`BurnFeet`。拳头按实际身体部位损伤并累计1.5秒连击；火机持续作用实际接触部位，未固定时逃离，固定时身体紧张。连击余时与热紧张在呈现时间推进，近看冻结普通时间时也能正常收束；暂停仍冻结。
- 入水打断普通陪伴、护伤、照护、繁育干预与排泄活动。支持变化/迁移/读档归一即时清除过期水中缓存，但不推进水暴露、不发放救援奖励。

### 已交B/主协调的公开接口

```csharp
bool WorldSimulation.BuyWaterRescueTool();
InteractionFeedback WorldSimulation.RescueFromWater(string id);
WaterEnvironment.RescueToolPrice // const int 18
WaterEnvironment.IsInWater(WorldState state, Creature c); // 即时几何+支撑判断
WaterEnvironment.CanStruggle(Creature c);
State.WaterRescueTools; State.WaterRescuePurchases;
c.Interaction.Water.InWater / ExposureSeconds / Struggle / RecoverySeconds;
c.Interaction.Emotion.Manmanlai / Alarm / SafeSeconds / LastThreatAt;
c.Personality.Kindness / Carelessness / Pride;
c.Interaction.HitCount / HitRemaining / LastHitAt / HeatTension;
InteractionAction.Punch; InteractionAction.Burn;
```

姿态：`water-struggle`、`water-recover`、`water-help`、`burn-flee`、`burn-tense`。水中 `CanSelfMove` 返回false表示不能普通陆行；水中逃出用 `CanStruggle`。救助接口立即清 `Water.InWater`，不需等下一普通tick。B未读取世界时仍应让Carried/Height优先于水姿态。

### 文件清单

新增源码：
- src/Yukkuri.Simulation/CreaturePersonality.cs
- src/Yukkuri.Simulation/EmotionalContinuity.cs
- src/Yukkuri.Simulation/InteractionWater.cs
- src/Yukkuri.Simulation/ToolContactState.cs

修改源码：
- src/Yukkuri.Simulation/WorldSimulation.cs
- src/Yukkuri.Simulation/WoodlandLayout.cs
- src/Yukkuri.Simulation/InteractionSpatial.cs
- src/Yukkuri.Simulation/InteractionWorld.cs
- src/Yukkuri.Simulation/InteractionBond.cs
- src/Yukkuri.Simulation/InteractionBehavior.cs
- src/Yukkuri.Simulation/InteractionSurface.cs
- src/Yukkuri.Simulation/InteractionState.cs
- src/Yukkuri.Simulation/InteractionCatalog.cs
- src/Yukkuri.Simulation/InteractionGesture.cs
- src/Yukkuri.Simulation/InteractionValidation.cs
- src/Yukkuri.Simulation/InteractionPresentation.cs
- src/Yukkuri.Simulation/InteractionLowering.cs
- src/Yukkuri.Simulation/InteractionPhysiology.cs
- src/Yukkuri.Simulation/NaturalLife.cs
- src/Yukkuri.Simulation/MilestoneInteraction.cs
- src/Yukkuri.Simulation/FamilyState.cs
- src/Yukkuri.Simulation/FamilyCare.cs
- src/Yukkuri.Simulation/FamilyRisk.cs
- src/Yukkuri.Simulation/FamilyColdStorage.cs
- src/Yukkuri.Simulation/HumanCompanionship.cs
- src/Yukkuri.Simulation/WildSelfCare.cs

新增检查源码：`checks/Interaction.WaterEmotion/Interaction.WaterEmotion.csproj`、`Program.cs`。16项覆盖水中真实放置、失能泡死、道具价格/库存/拒绝、暂停购物与危险冻结、存档延续、避免制造救援刷信任、伤后仍畏惧、连击、非法存档、目击跨区边界、救援感激重复、饥饿袭击被喂食中断、近看连击、火机固定/逃跑、旧烤脚目击节流、支持迁移缓存。本机均未执行。

### 静态审查与下一批

已逐项读调用入口、动作枚举/目录、状态序列化默认值与范围校验、正常tick/呈现tick、伤残/死亡、冷藏、护伤和照护交界；未发现需要删除原断言的理由。现有历史检查保留。

下一批优先：自主繁育的持续意愿、安全感和深关系条件，非露骨诱导入口；加强饥饿袭击的逃离/同伴保护与跨状态连续性；水域及性格更多远测场景。现有茎生/胎生/强制干预继续沿用，不重造已有孕育结算。B可继续读取现有 Pregnancy.Phase/Method/Elapsed/Burden/PlannedChildren/RecoveryRemaining 做表现；本批没有改变这些字段含义。

仍未完成：外部茎照护完整流程、完整器官/手术、内馅转移/身份延续、六种形态的全套玩法。八景对标沿用研究与覆盖表，不能把本批或11项实现称为全面对齐；真实手感、美术、编译与运行结论待授权远端验证。

## 第二批：安全繁育意愿、普通诱导用品与可干预饥饿冲突

2026-10-31 01:45 提前冻结交接批次。仅修改源码、检查源码和本交接文件；未在本机编译或运行。

### 接口与存档

- `FamilyLife.BreedingInterest`：0—100 的身体繁育兴趣，不替代信任、伴侣关系或接受意愿。安全且关系深的成熟伴侣自然累计；诱导暂时提高累计速度。
- `FamilyLife.InducedInterestRemaining`（0—90 秒）、`NextInductionAt`、`LastInterestEventAt` 保存剩余作用、冷却与表达节流。
- `WorldState.CourtshipTools` 初始1、上限99；`CourtshipToolPurchases` 保存购买次数。
- `WorldSimulation.CourtshipToolPrice = 24`；`bool BuyCourtshipTool()` 可在暂停商店交易。无限资源只免扣钱，不免道具库存消耗。
- `string? BreedingInductionReason(string id)` 只读资格检查，null 表示可进行；可供暂停面板显示中文原因。`InteractionFeedback InduceBreedingInterest(string id)` 实际执行，暂停拒绝；成熟、健康、没有孕育/恢复、吃喝体力均至少50、身体可接触才可用。每次消耗1份，兴趣+20，持续90秒，冷却180秒，少量消耗饱腹与体力。普通用品，不使用秘药，不凭空建立伴侣或跳过双方意愿。
- 新字段全部接入 `FamilyRules.ValidateFamily`；旧存档缺字段按默认值兼容，实际旧存档加载仍待远端验证。

### 规则变化

- 自主繁育要求双方持续安全至少45秒、馒馒来度至少60、惊惧低于12、疼痛低于20、恐惧低于30，无落水及落水恢复余波；双方为相互登记的伴侣，双向熟悉/好感均至少70、同伴恐惧/怨恨低于15，兴趣均至少40。附近可见的落水、入侵、冲突或饥饿袭击会阻止自主决定。继续复用普通靠近、接受和孕育流程，不直接生成幼体。
- 茎生/胎生在水中继续承担生理时间与危险负担，但不在水中生产。救回岸上、有干燥安全空间后可继续原出生事件；死亡、中止和出生防重复仍走已有规则。直接繁育请求、普通诱导与强制干预均检查真实水域几何，不等缓存更新后才拒绝。
- `InterruptFamilyConflict(id)` 扩展处理饥饿袭击。制止后留下30秒处理窗口，不消除饥饿、不治疗伤势。近看中抓起、喂食、隔离或身体无法继续时也清掉追击目标，不需推进普通时间。
- 饥饿袭击受害者会求助并退逃。近身、勇敢、有行动能力的好性格成年同伴可护住受害者；不能跨区隔空保护。仍保留喂食、带离和分隔作为人类介入方式。
- `Pregnancy.Phase/Method/Elapsed/Burden/PlannedChildren/RecoveryRemaining` 含义不变，已通知 B 线继续按真实状态呈现。

### 精确文件范围

新增：`src/Yukkuri.Simulation/FamilyDesire.cs`、`checks/Family.Desire/Family.Desire.csproj`、`checks/Family.Desire/Program.cs`。

修改：`src/Yukkuri.Simulation/FamilyReproduction.cs`、`FamilyRisk.cs`、`FamilyValidation.cs`、`CreaturePersonality.cs`、`InteractionBehavior.cs`、`FamilySocial.cs`；`checks/Interaction.WaterEmotion/Program.cs`；本文件。

### 静态审查与未执行检查

逐段核对新增接口与 UI 读取、消费失败不扣库存、暂停不推进生理、存档范围、双方深关系、自主准备与出生结算、水中风险和救后继续、饥饿追击的早退条件。新增 `Family.Desire` 共9项检查，覆盖暂停购物、有限消耗与意愿独立、身体资格、自然兴趣、安全深关系自主决定、周围可见危险、两种方式的水中延期与救后出生/读档不重复、非法新状态。`Interaction.WaterEmotion` 增加3项，覆盖人类制止、近看抓起打断、同伴保护的实际距离和受害者退逃；累计19项。均只写源码，未执行。

第三批优先：修正连续施害每个输入采样至少增加4点惊惧造成的帧率依赖，并用伤害/持续时间与事件强度累计保留情绪连续性；先向主协调给单次轻/重接触与连续接触的静态恢复窗口。之后继续爱线的安抚/重新接近与性格社会反馈。仍不把外部茎照护、完整器官/手术或内馅转移称为已完成。

## 第三批：真实伤害累计、连续目击与默认陌生起步旅程

2026-10-31 02:06 冻结交接。接替代理 `/root/gameplay_2` 只做源码读写与静态审查；未启动游戏、Godot、dotnet、编译器、检查/测试/模拟、渲染/预览、项目脚本或钩子，未使用Git命令。旧代理的执行历史不由接替代理担保。遵守主协调通知的避开逻辑CPU 4、5约束，不另行运行系统探测。

### 规则与公开接口

- `RememberPlayerHarm` 的受害者 Alarm 按实际伤害或非损伤惊吓强度 `amount * 2` 累加。去掉每个输入采样至少加4，零强度不会留下新威胁。身体伤害、疼痛、信任、部位戒备与照护预算仍按原规则承担后果。
- 2.5秒节流只控制公开事件数量。同一事件持续追加真实强度；可见目击者按增量变化 Fear/Trust/Alarm，合并同事件个人记忆，不每采样新增事件或重播惊叫。存档中既有 `LastWitnessThreatAt` 用于找到同一事件，不新增存档字段；旧 `UnreportedHarm` 在后续真实伤害时消费一次。
- 每次增量重新检查当前视听范围，旧知识不能造成离场后的隔空目击。听觉门槛仍为事件累计强度4，但只对超过门槛的份额作反应：`max(0,累计-4)-max(0,累计前-4)`，避免跨门槛样本大小造成差异。先听见、后来亲眼看见时，将事实来源升级为 Witness 并补一次真实目击反应；后续同事件不重复。
- 新增只读 `Creature? WorldSimulation.WaterConcernTarget(Creature observer)`。复用行为线的身体资格和 `CanWitnessWater`（包括真实入水、同区、隔离、盒子、视线），仅返回仍存活且实际可观察的落水目标。B线据此呈现救援/嘲弄，旧 `WaterConcernId` 不再被表现层当作当前危机证据。

### 静态恢复窗口（估算，不是实测）

末次威胁后至少15秒，且累计安全15秒，才以每秒0.045降低 Alarm；因此通常有约30秒延迟。饥饿时为每秒0.015。疼痛至少25、固定、落水、近处危险等会延后安全累计。食物和安慰不会直接清空 Alarm，也不会自动修复信任或身体损伤。

- 最轻拳头强度4、实际伤害3，对应 Alarm6；条件持续安全且不饥饿时，约163秒归零。
- 最重拳头强度13、实际伤害12，对应 Alarm24；约297秒降到12，约563秒归零。
- 连续火机实际伤害15对应 Alarm30；同等安全前提下约430秒降到12，归零约697秒。实际疼痛或未解除固定会延长。

### 文件与检查源码

修改源码：`src/Yukkuri.Simulation/InteractionBond.cs`、`EmotionalContinuity.cs`、`CreaturePersonality.cs`。

修改 `checks/Interaction.WaterEmotion/Program.cs`：新增4个检查组，总计23组。包括3种持续伤害工具、4种采样间隔、相同2秒真实接触的受害者/近处目击者/远处听见者数值对照；只读落水关注查询边界；同事件读档续接及移出视听范围；先听后看只补一次反应。旧事件数量和原有身体/状态断言保留。

新增 `checks/Interaction.AffectionJourney/Interaction.AffectionJourney.csproj`、`Program.cs`：一个贯通旅程，从默认小院的小铃（信任38、依恋18、对人熟悉/安全为0、无伴侣、繁育兴趣0）起步。全部原住民、天然资源和普通时间保留；用正常安慰、实际入口喂食/喝水、休息、必要救援、真实陪伴邀请和托起放稳API推进，直到主动人类陪伴记忆、双向深关系、双方结伴、自主孕育、出生及母体恢复。没有中途写满 Trust/Attachment/Affection/Alarm/SafeSeconds/BreedingInterest，也没有直接调用孕育开始/生成幼体。阶段超时会输出真实需求、关系、活动诊断并失败；不通过放宽原条件伪造旅程。

### 静态审查与未测项

已逐段核对默认创建入口、普通/近看时钟边界、输入时长上限、安慰回复与照护预算、实际入口与吞咽、家庭深关系阈值、自主繁育资格、出生事件对应字段、目击知识升级、同事件个人记忆与存档结构。旅程控制器只读状态并调用现有公开操作；所有数值形成仍交给规则。

全部新增检查仅写了源码，均未编译或运行；默认长流程能否在阶段窗口内稳定走通、情绪恢复是否符合真实手感、整体效果与表现须在授权独立远端验证。当前默认种群的性格与自主生活可能暴露旅程问题，不能把检查源码存在当作旅程已通过。

下一批优先：与B线共享实时人类陪伴资格只读查询，避免表现层重复或遗漏真实条件；继续检查伤后接近/安慰、性格介入和爱虐两条线的玩法缺口。外部茎照护、完整器官/手术及内馅转移仍未完成，不宣称全面对齐八景。

## 第四批：实时陪伴资格与真实危机的人类介入

2026-10-31 02:22 冻结交接。仍然只做源码与检查源码读写、静态文本审查；没有运行游戏、项目、测试/检查程序、编译器、模拟、渲染/预览、项目脚本或Git钩子，没有Git操作。CPU 4、5避让由主协调维持，本代理没有追加系统运行或探测。

### 与B及主界面共享的接口

- `bool HasLiveHumanCompany(Creature c)`：要求真实世界成员、尚未到期的当前陪伴，以及全部现有人类陪伴资格；直接排除近看，不依赖下一次输入更新才清理旧手位置。实际入水几何或正在受到有效威胁会立即使资格失效。暂停不删除既有状态，表现冻结由B处理；查询本身不增长任何时间、收益或记忆。
- `InteractionHelpRequest? InteractionHelpFor(Creature c)`：指定对象的当前真实求助资格。返回 `Target/Kind/Description/ThreateningCreature`；无资格返回null。`Kind` 为 `Critical/Water/Threat`。临危或落水优先，但仍保留正在威胁它的真实施害者。
- `IReadOnlyList<InteractionHelpRequest> InteractionHelpRequests()`：当前院求助列表，按临危→真实落水→有效威胁排序；临危内部先比较剩余救治时间，同级再参考旧 `HelpTargetId`。该旧ID不再决定危机是否存在。
- `Creature? CurrentInteractionHelpTarget()`：上述排序首项。只读，不切选择、不跨区域、不救援、不清掉旧ID。主界面自行观察与切镜头。
- `Creature? CurrentInteractionThreatTarget(Creature actor)`：按实际行为顺序重验施害者当前有效目标，不因离它较近的另一名施害者而漏掉此人。仅饥饿分支允许尚有真实剩余营养的死亡残躯，以保留中止啃食入口；死亡目标仍绝不进入求救列表。
- `string InterruptInteractionThreat(string actorId)`：执行时重验上述当前有效行为，再分派饥饿、幼体或家庭危险的中止。暂停原样拒绝。保留饥饿、伤势、记忆与死亡；旧失效Target字段不能优先抢走实际制止。停止啃食时文案明确是中止啃食，不称救助或复活。

观察范围：互动小院是全院镜头，沿用当前区域可直接观察个体的方式，不使用旧经营模式的步行玩家距离，不显示别区、冷藏或已安置对象。施害者能否继续行动仍复用其实际身体、当前视线/距离和相应危险条件；全院界面不会因此让油隔墙攻击。水域危机取实时几何，不把旧湿润缓存当入水。

### 规则修补与查询性能

提取 `CanHungerAggress/CanPursueHungerTarget/CanPursueFamilyConflict/CanPursueChildThreat`，行为和查询共用，不在界面复制一套门槛。幼体危险此前可能在压力已解除后先多打一次、随后才被后续更新清理；现在实际行动前也检查幼体年龄、剩余危险时间和饥饿/恐惧/疼痛/怨恨压力。真实入水和正在介入也不能凭旧缓存继续攻击。残躯营养已空会停止无效追逐。

空威胁ID直接返回，普通无危险个体不反复查找目标。列表每次只在局部预取一遍当前有效施害者/目标，不增加存档缓存或延迟失效标记。

### 精确文件与未执行检查

新增源码：`src/Yukkuri.Simulation/InteractionHelp.cs`。

修改源码：`src/Yukkuri.Simulation/HumanCompanionship.cs`、`CreaturePersonality.cs`、`FamilySocial.cs`、`FamilyRisk.cs`。

新增 `checks/Interaction.Help/Interaction.Help.csproj`、`Program.cs`，8组检查：危机真实优先级与旧ID；暂停/近看只读和区域边界；多施害者及拿起/饥饿解除；暂停中止无变化及实际中止保留后果；失效旧饥饿ID不遮蔽幼体威胁；压力解除后的多打一击边界；残躯中止且不求救；救上岸后仍有家庭威胁的正确优先级切换。

修改 `checks/Interaction.HumanCompany/Program.cs`，新增2组（总计19组）：实时查询不修改状态、切近看立即失效、暂停与旧手位置；实际水域几何/新有效威胁覆盖旧Until。保留原有关系和身体断言。

上述检查均未执行或编译。已静态核对API签名、默认第3只仍为幼体、暂停拒绝原文与完整状态快照断言、真实出生/死亡状态边界、威胁选择和中止顺序、界面与表现接线需求。不能把查询源码或测试源码存在当作游戏求助链已实机通过。

下一批：回到爱虐两线实际玩法缺口，尤其伤后重新接近、安慰与危险预警时的状态连续性，以及人类介入在不同性格中产生的真实后续。继续保留所有既有缺项说明，不因缺少本机执行条件降低断言或宣称完成验证。

## 第五批：伤后谨慎接近与保护后的实际关系

2026-10-31 02:40 冻结交接。本批仍仅源码、文档和检查源码读写与静态文本审查，工具为 Get-Content、Select-Object、rg、Get-Date 和 apply_patch；没有运行游戏、编译器、测试/检查、模拟、渲染、预览、项目脚本或钩子，没有 Git 操作。CPU 4、5 避让沿用主协调设置，未另行系统探测。

### 连续情绪与统一距离

- 修正轻伤 Alarm 小于 12 时，短反应结束后就被视为完全安定的断点。`EmotionalReadiness.Settled` 对实际仍有 Alarm 的个体再要求 SafeSeconds 至少 15；默认未受惊、新生或旧档 Alarm 为 0 者不会因初始 SafeSeconds 为 0 被拦住。原来的疼痛、恐惧、泡水恢复条件保持。
- 实际可见的警告/追击在第一击之前也按普通时间每秒 0.6 增加 Alarm，刷新当前威胁时间，不逐帧发事件或喊话。危险停止后保留余波。安全累计改用真实当前威胁资格和真实入水，失效旧目标不再持续续写不安全。
- 公开只读 `double PreferredHumanDistance(Creature c)`。默认 1.15；残余 Alarm 和最近 60 秒人类伤害连续扩大距离，最高 2.8；Fear 大于 60 且 Trust 小于 35 时，所有调用统一至少 2.2。B 已接同一距离，避免表现与规则各算一圈。
- 持续跟随、正在学习跟随、主动人类陪伴均把移动终点放在该距离的外围，不以人类中心为移动终点。静态核对 RoomMove 的步长会被终点距离截断，因此大步不会自行跨进停止圈再被推回。未受惊的跟随/学习仍保留原 1.25/1.1 门槛，陪伴为 1.15；到达容差 0.01，退避容差 0.08。主动退开不计入成功跟随路程。
- 普通分支仍先处理吃喝、休息和必要家庭照护，再判断避手。已经逃离危险、救援、受控身体状态继续用更早的分支。未安定者不会在空闲时随机闲逛、玩球或唱歌；仍可接受实际食物、照护与适当陪伴。
- 安慰、食物和陪伴带来的小幅缓和不得低于现有 Alarm 对应恐惧底值，不清空 Alarm。过近的手、当前威胁、实际落水和强制干预不能继续积累“安静安全的手”记忆。

### 制止后的真实后续

小院的 `InterruptInteractionThreat` 只有真实重验并停止危险行动后，才创建一个独立 Protection 事件；若此前已有该施害者对该对象的近期攻击，保留 ReasonIncidentId 链接。受保护者和能亲眼看见双方的合适围观者各记对应事件，重复点击已停止者不产生事件。死亡残躯中止不进入感激分支。

真实保护按性格改变 Trust、Attachment、Familiarity、Safety。善良受害者与傲慢恶劣受害者收益不同，恶劣围观者不凭空感激；这些数值直接进入现有主动求助/陪伴资格，而不是仅换台词。共享有限照护预算，新增保存的 `HumanBond.LastProtectionAt`（默认 -100）使同一个体 120 秒内不重复领取关系收益，并校验有限值和时间范围。近期伤害施害者/受害者后再制止，或近期由人类造成落水，不给这次关系奖励；事实记忆和已有后果仍保留。原水救助已有按性格变化的真实 Trust/Attachment 规则，本批未重复改写。

公开威胁查询仍只服务当前小院。内部真实威胁资格另行保留同区域行为，不受当前镜头在哪个区域影响，避免界面可见范围误伤普通生活判断。

### 默认陌生起步的静态可达性

没有修改爱线旅程及其断言，也没有填满关系或降低自主繁育条件。默认依恋 18 到主动陪伴要求 40，是人类关系链主要耗时：仅按 ImproveAffection 的依恋系数 0.6 计算，需要约 36.67 点照护量；扣除初始预算 3，理想补充下限约 1,347 秒（每秒 0.025），实际还受距离、身体、动作和饱和影响。此前口头的约 1,467 秒是未扣初始预算的资源时间，不能作为严格下限。Safety 从 0 到 8 还需约 533 秒实际可见安静近处时间；它可与照护资源恢复并行。原旅程每帧刷新手的真实位置，60 秒只是重选手的停放点，不是仅每 60 秒出现一次手。

这些是源码推算，不等于默认旅程通过。现有 3,600 秒首段窗口仍保留，后续双方深关系、自主孕育和出生条件都未放宽。

### 文件与未运行检查

修改模拟源码：`EmotionalContinuity.cs`、`InteractionBehavior.cs`、`HumanCompanionship.cs`、`InteractionBond.cs`、`InteractionWorld.cs`、`InteractionValidation.cs`、`InteractionHelp.cs`，均位于 `src/Yukkuri.Simulation/`。

`checks/Interaction.HumanCompany/Program.cs` 新增 4 组，总计 23 组：真实轻伤后接受甜食仍保持余波和距离；真实安全时间逐渐恢复且默认个体/旧恐惧距离边界；伤后重新建立谨慎陪伴而保留身体后果；单步跟随不越过舒适圈、不来回往返。

`checks/Interaction.Help/Program.cs` 新增 5 组，总计 13 组：不同性格真实收益与单事件身份；围观真实可见范围；实际人类施害后制止不刷感激；保护冷却读档续接与非法值拒绝；未受伤前的真实警告和失效旧目标。

所有新增检查只写源码，未执行、未编译。已静态核对轻拳真实伤害、吞咽入口、普通时钟、移动步长、共享距离、跟随路程、求助事件身份、冷却序列化与默认值、既有检查的默认距离条件。远端须验证连续动作与状态是否稳定，不能据此声称真实手感或长旅程已通过。

下一批继续实际玩法缺口，包括家庭伤害余波和保护后的安全安排、茎生表现之外的实际外部照护等。未完成的完整器官/手术及内馅转移仍不宣称完成。

## 第六批：同伴伤害身份、真实虐线旅程与茎生照护预查

2026-10-31 02:56 冻结交接。本批只读写源码/文档并做静态审查；没有编译或运行任何项目、测试、检查、模拟、游戏、渲染、预览、项目脚本或钩子，没有 Git 操作。没有新代理或系统探测；CPU 4、5 继续沿用主协调的避让设置。

### 同伴实际施害的连续后果

新增私有 `InflictPeerAttack`，由家庭冲突、亲代危险和饥饿袭击的活体伤害共同调用。每次只调用一次 Hurt、创建一次 Attack 事件、写一条受害记忆；删除这些原调用处的重复 Emit/Remember 和饥饿旧固定 Alarm。实际受害 Alarm 按该次 Hurt 伤害的两倍累计。家庭/亲代补对应身体伤害的全身恐惧；饥饿原有 Fear+12 保留。原双边关系的 Fear/Resentment/Affection/LastHarmAt 仍在原入口处理，不叠算。

原事件文字和严重度保持：家庭冲突为实际 damage，亲代与饥饿仍为原 Severity=8。记忆的事件身份先创建，再执行身体伤害，因此第一击就致死也能保留因果顺序及受害者生前知情。玩家对人的信任和 HumanBond.LastHarmAt 不会因油施害被改写；公共 Hurt 的其他用途也没有被强制改成同伴攻击事件。

Emit 增加可选 `visibleActor`，默认 null 保留其他调用语义。看见受害者不再自动等于认出隐藏在遮挡后或视野之外的施害油。旁观者按该事件真实获得的 Witness/Heard 来源留下惊惧；只有看清双方者才在对具体施害油的关系里增加恐惧/怨恨。只看见伤者或只听见者的个人记忆不泄露施害者身份。远处、别区、冷藏者不获得这次感知。已有死亡和亲属丧失机制继续单独承担死亡后果，不以攻击记忆替代死亡事实。

### 护理补给共享常量

按主协调接线需求，在原 BuyMedicine 所在类公开 `MedicineBundlePrice=15`、`MedicineBundleCount=3` 并由该购买方法复用。数值、有限/无限资源扣款方式和交易行为不变。主协调负责小院购买与危急工具入口，本代理没有修改 Game。

### 新增检查源码

新增 `checks/Interaction.PeerThreat/` 项目，7组：三种实际行为一击的伤害/Alarm/事件/记忆数量和人类身份隔离；听见但没看见；只看见伤者但施害者在视野外；远处与跨区；制止和读档保留余波不重播；实体盒子只遮住施害者；首次致死一击的 Attack→Death 次序、受害记忆和幸存目击者。组件夹具仅安排初始危险条件，实际一击由普通时间行为触发。致死边界以原有临危最后救治时间作初始夹具，不修改身体伤害规则。

新增 `checks/Interaction.AbuseJourney/` 项目，一个完整真实操作旅程：默认健康小院→真实固定和轻拳→真实连续烤脚接触形成永久脚底功能上限→真实解除后仍无法自移→实际持续护理不能抹掉永久损失→真实继续击打进入救治窗→不救助并推进普通时间至死亡→幸存目击者惊惧/信任损失/死亡知情→护理拒绝复活及读档不重复死亡。旁观者通过玩家可用的固定工具留在视线内，其比较基线在该独立操作后记录。全程没有直接赋值生命、疼痛、恐惧、伤势、残障、死亡或世界时间，未移除默认种群和自然环境。阶段会报告真实数值并在有界窗口未达标时失败。烤脚是现有 InteractionCatalog 中的连续工具，未新增绕过交互的伤害接口。

新增文件：`src/Yukkuri.Simulation/PeerAttackContinuity.cs`；上述两个检查项目的 `.csproj` 与 `Program.cs`。

修改文件：`src/Yukkuri.Simulation/MilestoneWorld.cs`、`FamilySocial.cs`、`FamilyRisk.cs`、`CreaturePersonality.cs`，以及本交接文件。

检查均未执行或编译。静态审查已核对实际伤害调用次数、原事件严重度、伤害与死亡次序、盒体遮挡几何、年轻个体接触距离、公共查询范围、已有吞咽/护理/死亡拒绝条件、持续工具时长上限、原救治时钟和读档入口。尚不能声称新虐线旅程或新同伴后果已实机通过。

### 茎生外部照护：现状与下一批最小完整链建议（尚未实现）

现状：Stem 与 Live 已有不同孕育时长、营养负担、出生数量和产后恢复；两者仍共同依赖母体数值和 `TryDeliverFamily`，成熟后一次性生成完整幼体。Game 的 `FamilyStatusMarker` 是侧面的茎/摇篮状态标记，不是随母体身体实际附着、可照护和可受损的外部茎。没有独立的茎体完整度、支护有效期、脱落或照护结算，不能把当前状态标记称为外部茎照护完成。

建议先做一整窝共享的外部茎状态，不在第一批引入每个未出生幼体的独立生活模拟。持久状态挂在现有 PregnancyState：茎完整度、茎含水/失养余量、支护剩余时间、最近护理时间、失败原因及其来源事件；整窝身份直接复用当前孕育 EventId，父母来源和 PlannedChildren 保持原字段。可见/成熟阶段由原 Method/Phase/Elapsed 推导，不另写一份会漂移的孕育计时。旧档按已有孕育进度补默认完好状态，不重抽数量、不新增已出生个体。

最小实际链：正常或既有强制入口开始茎生→随原计时出现并长大的外部茎→母体真实吃喝供应维持茎体，缺水/浸水/真实外力按普通时间或实际事件损伤→人类选择“茎体支护”工具，持续接触完成后消耗一份原护理用品并获得有限支护时间→原计时成熟且茎仍存活时，由既有干燥落点检查整窝出生→同一 EventId 只结算一次并进入原产后恢复。若茎完整度耗尽，则记录实际原因/施害来源，调用孕育失败和恢复流程；不能生成幽灵幼体、退回已用用品或把失败算作 CompletedBirths。第一版维持整窝成功/失败，暂不伪装成已经支持逐个果实脱落。

建议接口：只读 `StemCareStatus(motherId)` 返回本次 EventId、可见/成熟状态、原计划数量、真实完整度/余量/支护剩余与不可护理原因；新增一种连续交互 `InteractionAction.SupportStem`，复用单一手势生命周期及 1.6 秒真实接触，再扣现有 Medicine。输入 ObjectId 绑定当前孕育 EventId，使过期茎、换母体、换次孕育、暂停、近看错误对象或中断接触均无法继续旧进度。B 线建立真正随身体移动的茎节点与命中目标，主协调接工具选择/库存提示；模拟线负责资格、消耗、普通时间、损伤与出生失败结算。开始前需三线约定命中身份；本批没有擅自改跨线 UI、BodyPart 枚举或出生规则。

## 第七批：附着母体的一整窝外茎照护（源码冻结）

已实现上节建议的整窝附着链，未拆成独立脱落幼体。新增 `src/Yukkuri.Simulation/StemCare.cs`，`PregnancyState.Stem` 保存本窝 EventId、完整度、含水余量、有限支护时间、最近护理与损伤时间、实际损伤原因和施害来源。正常、强制孕育两个真实入口都初始化；旧档只在构造世界时补缺失的活动茎生状态，不改原 Elapsed、EventId、PlannedChildren，不重抽数量。只读 `StemCareStatus(string motherId)` 返回 `StemCareView(EventId, PlannedChildren, Progress, Integrity, Hydration, SupportRemaining, CanSupport, RefusalReason, LastDamageCause)`；Progress 为 0–1，完整度/余量为 0–100，支护为秒。非活动窝、死亡母体、错误身份和零完整度不提供可见状态，查询不修档。

输入新增 `InteractionTargetKind { Body, Stem }`；`InteractionInput` 末尾追加默认 Body 的 TargetKind，保留旧调用语义。茎输入必须绑定当前窝 ObjectId；Part 只是占位，绝不落回身体分支。支持 `SupportStem`、原 `Punch` 和 `Burn`，其余工具明确拒绝。拳击/灼烧外茎不调用母体 Hurt，不改母体 Head、局部伤势、身体部位戒备或 HitCount；真实人类威胁仍降低安全/信任并产生连续 Alarm、目击影响，停止自行护伤、取食、递送和同伴相处，未固定母体接触火焰时实际逃开。

支护要求连续 1.6 秒真实茎接触，失去接触累计清零；完成才扣实际 1 包 Medicine，即使 UnlimitedResources 也不免库存。一次手势最多一次；完成补完整度 12、含水 25，支护 60 秒，并有 20 秒观察冷却。支护充足且无明显损伤时拒绝重复包扎。手势保留内部起始母体/Kind/EventId 吞掉后续长按，但失去接触或完成后公开 InteractionGestureTarget 为空，不锁姿势、进食、照护或成熟出生。恢复接触仍用内部起始身份，不允许改对象。B 已获知 UI 必须保存起始母体，不能靠临时公开 target 恢复接触。

外茎按原生物时钟变化：母体吃喝充足可维持含水并缓慢修复；失养/缺水、真实浸水持续损伤，来源分别记录。支护按时间消退，冷藏使用原有生物减速。母体实际外力伤害传导 35% 至外茎，有支护时再乘 40%；支护不能抵消直接击打/火焰或泡损。无伤害的手势不凭采样次数捏造外茎损伤。完整度耗尽调用原失败和恢复流程，保留原因；母体死亡停止可见窝。成熟仍调用原干燥落点、计划数量、EventId 和 CompletedBirths 一次结算。验证拒绝错窝身份、越界/非有限数值、未来时间及零完整度活动窝；推进也防守处理零完整度，不能健康回长后出生。

新增 `checks/Interaction.StemCare/` 的项目和 Program.cs，共 12 组检查源码：默认 Body 和所有不支持工具拒绝；连续接触/移开/暂停；Kind/身份变化及过期窝；无限钱仍要求药包、单手势一次且不锁母体；外茎拳火独立损伤及不同采样率等实际接触时长；母体外力与有限支护；普通时间/暂停/近看/失养；完成长按仍能原事件成熟出生及读档不重复；真实水域泡损、实际解除并救出仍保留伤损、冷藏减速；失败恢复与母体死亡；旧档归一化、只读查询、错身份/NaN/零完整度拒绝；正常与强制两个真实准备入口初始化。组件场景明确采用既有孕育或已建立关系的初始夹具，不冒充从默认陌生开始的爱线旅程；两种入口的 EventId、数量与准备过程通过真实 API 生成。

修改文件：`InteractionState.cs`、`FamilyState.cs`、`WorldSimulation.cs`、`FamilyReproduction.cs`、`FamilyRisk.cs`、`InteractionCatalog.cs`、`MilestoneInteraction.cs`、`InteractionWorld.cs`、`InteractionGesture.cs`、`FamilyValidation.cs`，以及本交接文档；新增 StemCare.cs 和检查项目 2 个文件。没有改 Game、Git 或其他线分配文件。

本批仅源码读写和静态核对，未运行/编译任何项目或检查。静态已修正不存在的 PlayMode 名称，核对原时钟、射线身份接入约定、死亡/出生收尾、实际水域、库存在成功点扣减、完成后不占目标和无身体伤害回落。12 组检查尚待独立远程机器运行；茎模型实际命中、遮挡与表现由 B 接入后仍需远测。

## 第八批：整窝断茎与独立瓶养（模拟与检查源码冻结）

HK026/027 对应断茎、移茎入瓶；第七批附着支护不算瓶养。本批新增独立、可保存的 `WorldState.DetachedStemBroods : List<DetachedStemBrood>`，不是普通食物 RoomObject，避免按 Portions 清理、误吃或绑定食水 CarrierId。每窝继承原 EventId、Elapsed、PlannedChildren、Burden 与父母真实家谱身份，保存父母身体来源和物种/性格等稳定遗传快照；另有独立位置、Loose/Bottled/Failed/Born 阶段、完整度、水分、营养、瓶体完整度、损伤原因/来源/时间、事件及 Cleared。母体只保留历史，进入原恢复流程；死亡、冷藏或新一次孕育不会接管或抹掉已离体的一窝。

输入末尾追加 `SupplyObjectId=null`，TargetKind 末追加 DetachedStem，Action 末追加 CutStem/BottleStem/ClearStem。仍只有一个 ContactGesture：Begin/Continue/End 最前分派真实独立 EventId，不通过母体 InteractionTarget；便利入口 `BeginDetachedStemGesture(eventId,input)` 使用同一通道，离体对象不占母体 target 或姿势。ObjectId 一直表示窝，SupplyObjectId 单独表示实际手持水杯/正餐，二者不能在持续接触中换绑。未支持工具、错误身份、暂停、近看、跨区或变化来源都明确停止/拒绝，不落回身体。

CutStem 对附着茎连续接触 1.6 秒；先找母体旁可见、干燥、足够空间的落点，再结算剪离。剪离有真实茎损伤 3 和母体系统性恢复负担 2，不伪造 Head 局部伤势；母体和真实知情旁观者保留人类威胁余波。落地的一窝有实际救助窗口，未装瓶时持续失水，不能无限暂停生命。BottleStem 连续 1.6 秒，成功才扣 StemVessels 1；`StemVesselPrice=20`、`BuyStemVessel()`，测试购买免费但使用仍需实际库存。瓶里水分、营养持续消耗；Drink/FeedMeal 各需实际手持 RoomObject 的一份食水、连续 1.6 秒才加入 45 点，不凭无限钱创造份额。直接拳火损伤独立茎与瓶，破瓶持续漏水失养，可用新瓶替换但不会治好已有茎伤。一次手势最多一份资源，移开重新计时，完成继续长按不重复扣。

整瓶 Lift 沿真实干燥空间移动，每秒最多 6 单位、每 .06 单位检查一步；当前可见位置每步立即写入持久 X/Z，并持续占位，手持也不允许油/其他瓶/食水进入。取消、暂停或读档只在当前最后合法位置放下，不回旧位、不另找位置传送、不保留隐形旧占位。与手持食水和托着油互斥；只改变瞬时 HeldByHand 的保存归一化不改变生命时钟、进度和资源。B 已获知 DTO X/Z 就是当前真实位置，表现应低托近地。

原 `TryDeliverFamily` 抽出共用 `TryCreateFamilyChildren`：完整预留一窝干燥落点后再生成，保留原数量、ID、CompletedBirths、家谱和正常附着产伤/恢复条件；瓶养从瓶体位置使用同一结算，遗传来自剪离时快照，不要求父母还活着，不给已死或已重新孕育的母体追加第二次产伤/恢复。无空间时等待，不出生一半、不改计划数。出生后 Born 仅是平贴地面的小残留，不占新生/行走空间。Failed 未清理实体占位；ClearStem 连续接触 1.6 秒仅允许 Failed/Born，成功 Cleared=true 隐藏实体并释放占位，保留 EventId、失败原因、原阶段、家谱，不退款、不复活。活窝不能误清理。

独立物体威胁事件不把远处母体伪装成受击目标；目击/听见按实际瓶位置、区域、距离和遮挡，听见不认定人类施害，增量按真实强度而非采样次数。事件清理复用原历史裁剪。失败可见事实不向旁观者泄露未见过的旧原因；玩家仍可从持久记录读取真实失败原因。新状态验证覆盖身份唯一、亲代来源、独立/附着同时归属、数量/时钟/有限数值、零活性、出生账本、清理资格与单手互斥；旧档缺少列表使用默认空列表。

只读契约：`DetachedStemStatus(eventId)` 与 `DetachedStems(regionId)` 返回 `DetachedStemView(EventId,MotherId,FatherId,RegionId,X,Z,Phase,PlannedChildren,Progress,Integrity,Hydration,Nutrition,VesselIntegrity,HeldByHand,CanBottle,CanWater,CanFeed,RefusalReason,LastDamageCause,Cleared,CanClear)`。进度 0–1，其余生命指标 0–100。区域列表包含历史；B 过滤 Cleared，根 UI 可展示历史。`CanPlaceDetachedStemAt` 复用真实放置资格，`CurrentInteractionTargetKind` 用于统一输入区分。根接用品/状态/调试，B 接独立实体及实际接触；本冻结仅交模拟源码，不能把先前美术提交当作瓶养整体验证。

新增 `checks/Interaction.DetachedStem` 项目，共 12 组检查源码：真实剪离与原身份/进度/数量/母体恢复；瓶具付费和连续接触一次消费；实际食水来源绑定与份额；母体死亡后持续真实照护至一次出生与读档；离体救助窗口和持续失养失败；破瓶漏水与替换保留茎伤；不同采样率伤害/目击及只听见和远处不知情；动态搬放占位、旧位被其他油占据、取消/读档不回旧位；暂停/近看/跨区/错误身份；母体冷藏与新孕不接管；只读和坏状态；终态实际清理保留记录且不退款复活。现有 StemCare 的“不支持工具”枚举检查只将本批实际新增支持的 CutStem 排除，其余原断言保留。

新增模拟文件：`DetachedStemState.cs`、`DetachedStemLife.cs`、`DetachedStemGesture.cs`、`DetachedStemWitness.cs`、`DetachedStemValidation.cs`。修改模拟文件：`InteractionState.cs`、`InteractionCatalog.cs`、`FamilyReproduction.cs`、`StemCare.cs`、`InteractionGesture.cs`、`InteractionWorld.cs`、`WorldSimulation.cs`、`InteractionSpatial.cs`、`InteractionSupply.cs`、`MilestoneWorld.cs`、`FamilyValidation.cs`。另新增检查项目 2 文件、修改 StemCare/Program.cs 与本交接。未修改 Game，未操作 Git。

本批仅源码和静态审查，未启动项目、编译器、测试/检查、模拟或预览。12 组新检查和旧回归均未运行；独立对象实际命中、瓶口补给、拖放与普通场景表现仍需 B 接线后的独立远程验证。原生物阈值和正常爱线条件未降低。

## 第九批：水域因果、救助关系和岸上持续守候（源码冻结）

新增 `WaterRescueContinuity.cs`。每次实际入水保存独立 EventId、EnteredAt、PlayerCaused 和 HumanRescueCredited。人类造成的本次落水不会因超过 120 秒而洗掉原因；再次真实入水生成新事件，并清除针对该目标的旧关切和守候。旧档仅在构造世界时为正在落水且缺事件的对象补身份，按已有暴露时间和旧入水归因恢复；查询不迁移状态。NewFamilyId 同时避开已有水事件、关切和守候事件，存档校验拒绝两个对象共用同一水事件、错目标事件、非有限/未来时间及不可能的已结算状态。

善良者真正接应需要食水、体力、伤痛、恐惧和身体操作均允许。无余力时至多每 12 秒呼救一次，然后让出行为给真实取食、自行护伤、休息、逃离和指令。正在照护、亲代临危幼体，以及原规则会选择的幼体/失能同伴递送均优先；从 FamilyCare 提取并复用原候选资格，不降低原照护关系条件。新的落水危机打断旧岸上守候。恶劣者保留原嘲弄、怨恨和拒绝接应的实质差异。

真正看见并跟随本次落水事件的善良同伴，在目标实际离水后保留最多 30 秒岸上查看。目标活着、仍在离水恢复、同区可见，双方无介入和冲突且有余力时，实际走到双方身体半径之和加 .25 的外围，.08 为到达余量；不会向身体中心冲撞。连续近旁累计 6 秒后只获得一次小幅同伴亲近/熟悉关系记录，不治愈伤势或清除恐惧。失去资格或离开距离会在普通时钟中重置未完成累计，死亡、新一轮入水和期限结束会清除。发生离水的同一帧不能先领取整帧时间：累计严格不大于 WorldSeconds−StartedAt，原存档时间断言保持。

人类救助仍须原真实工具库存和合法岸边落点。观察者在搬离水域前捕获，并在实际救出位置再次验证可见；仅在事后看见湿透幸存者不会获知施救者。事件只结算一次，关系收益改用现有 CareBudget、LastProtectionAt 和近期人类伤害门控；不同性格获得不同实际关系变化，保留原恐惧和伤势，不直接填满信任/熟悉/安全，不降低原主动陪伴门槛。旁观描述只说亲眼看见的救出事实，不泄漏未见伤害来源。

公开只读契约：`WaterSupportPhase { Calling, Responding, Approaching, Watching }`；`WaterSupportView(TargetId, EventId, Phase, ContactSeconds)`；`WaterSupportStatus(Creature observer)`。ContactSeconds 是 0–6 的连续关系反馈窗口，不能显示为整段守候时长。查询重验实际资格，不依据旧 ID 生成姿势；近看为空，暂停仅观察既有状态。旧 `WaterConcernTarget` 仍只返回真实水中的目标，避免岸上继续被画成落水呼救。root 接普通说明、B11 接实际目标表现，模拟不改 Game。

新增 `checks/Interaction.WaterSupport/` 项目和 Program.cs，共 10 组源码检查：事件/归因/读档/序号回退；饥饿善良者实际进食；可见参与者岸上靠近和限时一次反馈；幼体照护和自身需求优先；真实救助共享预算/近期伤害/恐惧保留；善良和自负实际关系差异；超过旧归因窗口仍不能刷信任；纯查询/暂停/近看/遮挡；旧档与坏事件/重复事件；同一普通 tick 自救或同伴接应后立即保存。组件初始位置、既有失能或长时间落水存档边界均明确为夹具，后续救出、移动、接触和时间通过真实 API；不冒充默认陌生起步的全程爱线。

本批新增模拟 `WaterRescueContinuity.cs`，修改 `CreaturePersonality.cs`、`InteractionWater.cs`、`InteractionValidation.cs`、`WorldSimulation.cs`、`InteractionBehavior.cs`、`FamilyCare.cs`、`FamilyReproduction.cs`、`HumanCompanionship.cs`、`MilestoneInteraction.cs`；新增上述检查项目 2 文件及本交接。仅源码读写和静态审查，未运行/编译/预览或 Git。10 组新检查、旧 WaterEmotion 和其余回归均待独立远程机器运行。

本批期间另独立交付并已由 root 提交 `f08a25b119ea6fefc59ed104620ee593e559ce12`：`InteractionCompletion.cs` 只读真实长按进度及 `checks/Interaction.Completion/` 3 组；`checks/ActorGeometry.Smoke/Program.cs` 为 B10 增加 3 组连续眼层、眼弧、泪量检查，未修改旧断言。这两小单元不混入 A9 主生命周期结论，同样没有本机运行。

## 第十批：可打断袭咬、有限摄食来源和原口部入口（源码冻结）

保留原恶劣性格、极饿、概率和可行动门槛；补入口部严重失能、隔离、入侵及必要照护门控。实际看见且可接近的普通食物优先，在追击途中和只读威胁查询中也重验；手持、被搬运、占用、耗尽、隔区、遮挡或正在回避的食物不能凭空中止真实危险。原 2 秒新意图警告和 10 秒概率扫描保留。到近处之后累计 .8 秒真实连续准备，口部伤势按原摄食速率减慢；首个接触帧不借用事件发生前的时间。离开接触、换目标、换区、实际人类或同伴制止、身体失去资格均不能带走未完成的准备进度。

成功时活体每口最多取 2 单位，按实际可取份额缩放原 6.5（默认侵略性 .5）实际侧部伤害，生成一次原严重度事件、一次受害余波和真实知情旁观影响。遗体每口最多取 6 单位。二者共用原 RemainsNutrition 的 24 单位有限储备，死亡、护理和存读档不重置。入口前没有营养或伤害；入口后只扣一次份额并交给原 Meal 口部时钟，不会在咽下时再扣伤或来源。已入口的后果不会因停止、分离、死亡或换动作而退回。遗体摄食的旁观事实与记忆描述不再伪称死者正在疼痛，人类信任和 LastHarm 不承担同伴伤害。

公开 `HungerFeedingPhase { Pursuing, Preparing, Swallowing }` 和 `HungerFeedingView(TargetId, EventId, Phase, ContactProgress, TakenNutrition)`，只读 `HungerFeedingStatus(Creature)`。准备进度 0–1，吞咽仅说明实际已接受的一口，不属于仍在追击的威胁。root 接普通状态和调试，B13 接真实准备目标；嘴型仍完全用 B12 原 Meal 剩余时钟。入口来源绑定持久 IntakeSequence 与 IntakeGeneration：统一 BeginIntake 在 long.MaxValue 时换 GUID 代号并回卷到 1，旧来源不能错误绑定以后的一口；普通喂食入口也不会因合法最大序号抛溢出异常。

新增 `checks/Interaction.HungerFeeding/` 两文件，共 14 组检查源码：实际准备后一次伤害/来源；暂停及人类中止；准备中同伴护卫；实际分离和重新接近；真实手持供给放地优先；活体至实际死亡和遗体共用有限储备；最后零碎份额和耗尽；口部失能和减速；即时保存、纯查询及近看时钟；普通 FeedMeal 最大入口序号换代；旧来源序号碰撞隔离；非法时间/来源/重复身份；跨区或换目标；善良和非极饿不无差别袭击。死亡等待先实际移开施害者，避免把另一口真实摄食误当死亡重置。原 PeerThreat 第一组与 WaterEmotion 两处饥饿场景改为真实推进到准备完成，原伤害、关系、制止、知情断言不降低。

本批新增模拟 `HungerFeeding.cs`；修改 `CreaturePersonality.cs`、`InteractionState.cs`、`InteractionValidation.cs`、`FamilyReproduction.cs`、`InteractionWater.cs`、`InteractionInjury.cs`、`PeerAttackContinuity.cs`。检查为上述新项目两文件、`Interaction.PeerThreat/Program.cs`、`Interaction.WaterEmotion/Program.cs`，以及本交接。ActorGeometry 的 B12/B14 独立改动由 root 分批整合，不计入 A10。未修改 Game 或操作 Git。

本批仍只做源码读写和静态审查，14 组及旧回归未运行、未编译。接触仍使用既有 FamilyInContact（双方占地半径加 .28）的近身规则，本批没有验证嘴部精确物理接触；Godot 实际表现与远程运行结果尚未确认。不包含整只吞入或肢体拆分，不把这一有限袭咬链称为那些更大范围玩法的完成。

## 第十一批：测试免费购买、真实用品库存（源码冻结）

新增只读 `WorldSimulation.UsesConsumableStock => State.InteractionPlaytest || !State.UnlimitedResources`。本轮小院测试保留免费购买，但手喂、放地、教学等待使用的食物，以及普通护理/旧护理入口/雇员护理都需要并消耗真实库存；不改变非小院旧沙盒无限物资语义。金钱仍只由原 UnlimitedResources 控制，有限模式原价购买。公开 `FoodBundlePrice=10`、`FoodBundleCount=20`；原护理 15 元 3 包常量不变。水没有新增仓库数值，水域救援、外茎支护、培养瓶和诱导用品沿用本来已真实扣库存的路径。

BuyFood/BuyMedicine 在完整一包放不下 int 合法范围时先拒绝，钱、库存和日志均不部分结算；没有暗中夹掉旧档高库存。Refill 同样先确认食盆容量再扣实际仓库份额。PutAway 先计入自然摄食尚未入口的预约份额，确认所有退回份额均能容纳，再取消相关递送/预约、归还一次并移除实体；已入口份额不会退款，重复收取不复制。实际手持供应物入口只扣该实体，不再重复扣仓库。

教学等待在写入学习记录或抽取理解概率之前检查需要新增的食物、库存和可用放置位置；已有手持/搬运或不可见食物不充当眼前练习餐。取消练习不会另行退一份，实物仍须真实收起。Tease 只要求有真实未食用食物，持续过程中也重验；它仍不是喂食工具，不消费或凭空复制。护理开始时缺货即拒绝，不先取消繁育干预；持续护理仍在实际成功之后扣一次。雇员危急护理也改为身体护理成功才消费。

新增 `checks/Interaction.Inventory/` 两文件，共 12 组检查源码：测试免费补货与一次手喂；取消/暂停/缺货；放地后手喂实物不双扣；自然预约和已入口归还区别；满仓拒绝不先取消预约；真实护理前后及持续手势；旧 Feed/Contact 护理入口；等待教学与逗食；免费和付费完整交易及 int 边界；食盆实际补充与溢出拒绝；旧非小院沙盒兼容；有限模式真实雇员护理。AffectionJourney 仅在实际缺粮时通过 BuyFood 免费购买，原陌生起点、关系门槛、实际动作和时间断言全部保留，未直接填库存。

本批新增模拟 `ConsumableInventory.cs`；修改 `InteractionGesture.cs`、`InteractionWorld.cs`、`InteractionBehavior.cs`、`WorldSimulation.cs`、`MilestoneInteraction.cs`、`MilestoneWorld.cs`。新增上述检查项目两文件，修改 `checks/Interaction.AffectionJourney/Program.cs` 和本交接。root 接用品补给/库存/帮助，未由本分工修改 Game。B15/B16 的 ActorGeometry 新增检查单独冻结交 root，不属于库存交付。

只进行源码读写和静态审查，没有运行项目、编译器、检查、测试、渲染或 Git。12 组新检查、长爱线补货和旧物品/照护/自然取食回归均等待独立远程验证；没有把源码检查存在称为运行通过。

## 第十二批：真实水中尝试事实与表现契约（源码冻结）

新增 `WaterAttempts.cs`。WaterExposure 增加 AttemptSequence、AttemptGeneration、AttemptAt（默认 -100）和可空 AttemptOutcome，绑定原 Water.EventId。只有原 CanStruggle 且 Struggle>12 的实际尝试分支才记录：CanOccupyRoom 确实允许向岸位移为 Advanced；实际占位阻止这一步为 Blocked；原近岸落点及随机条件实际成功、走原 LeaveWater 分支才是 Escaped。NextAttemptAt 继续仅为检查日程，固定/失能/无力/翻倒时不能把日程更新冒充动作。没有新增位移、成功概率、救援结算或跳出捷径。

公开 `WaterAttemptOutcome { Advanced, Blocked, Escaped }`、`WaterAttemptView(EventId, Generation, Sequence, At, Outcome)` 和只读 `WaterAttemptStatus(Creature)`。完整身份是落水 EventId、可空代号和序号；long 耗尽时仅给尝试计数换 GUID 代号并从 1 开始，不替换落水因果身份。新一轮真实落水重置尝试字段，旧档缺字段默认没有尝试，不从 NextAttemptAt 推造事件。校验拒绝负序号、无来源结果、未来/非有限时间、无效枚举和错代号。

只读查询限实际当前小院、活着未临危且未冷藏/安置的对象；近看返回空，暂停只读既有事实。只暴露距普通世界时间 1 秒内的记录，水中推进/受阻还重验实际可挣扎资格，Escaped 要求本次已离水并在恢复、未被托起或再次落水。读数不抽随机、不改位置、不推进时钟。B17 首次观察、读档、新落水身份、近看或隐藏状态只建立当前完整身份基线；同一落水事件的新尝试才播放短反馈。死亡记录可以留存，但不会再表现成活着挣扎。普通向岸推进若跨过水线，仍由原支撑同步路径离水，不另伪造一次跳出事件。

新增 `checks/Interaction.WaterAttempts/` 两文件，共 9 组检查源码：实际水中释放及首次推进即时可存；实际身体占位阻挡与移走后推进；固定/底部失能/无力/翻倒只有日程无尝试；真实多种固定种子下原成功落点与恢复、无救援消耗、实际冷藏隐藏；纯查询/暂停/近看/过期；真实救助与再次托起丢入水中不复用身份；序号耗尽安全换代；死亡/跨区及读档；旧档与坏状态。未直接写入成功结果、移动判定或随机结果来通过检查。

本批新增 `WaterAttempts.cs`，修改 `InteractionWater.cs`、`InteractionValidation.cs`，新增上述检查项目两文件及本交接。没有修改 A11 冻结共享源码，没有改 Game 或 Git。9 组新检查与旧水域回归未运行、未编译；B17 接线的实际画面尚待独立远程验证。

## 第十三批：受伤后的同伴相处连续性（源码冻结）

修复 `FamilySocial.cs` 中实际决定与情绪记忆脱节：以前真实袭咬后仅有 5 秒逃离及感情收益的 30 秒限制，受害者仍可能在 Alarm 未消时接受施害者邀请、主动靠近，并由相处直接触发 Content。现在用对象特定的真实 `PeerBond.LastHarmAt` 做只读门控：原 30 秒关系恢复底线内拒绝该对象；30–120 秒内若受害者尚未安定仍拒绝。120 秒复用现有“观察安全行为后才缓慢恢复关系”的窗口，不增加新的持久字段。历史伤害不会因为后来无关恐惧而永久禁止该对象；原双方 Fear/Resentment、身体需要和生育条件继续生效。

手动邀请、自动候选、持续相处和关系收益统一复核资格。新近期伤害拒绝发生在关系创建、邀请冷却、反应和随机调用之前；重复点击不能覆盖眼前情绪。原一般恐惧/怨恨邀请拒绝仍保留真实 React 及清理旧动作元数据的语义，未削弱既有 InjuryPresentation 检查。旧档保留的过时相处目标会在行为/收益推进时退出。无关安全同伴仍可实际陪伴，未安定者用 Worried 与仍不安的回应，不因陪伴直接变开心；没有清除伤势、恐惧、Alarm 或改写人类受伤记忆，也没有降低结伴和孕育门槛。

新增 `checks/Interaction.PeerCompanyContinuity/Interaction.PeerCompanyContinuity.csproj` 与 `Program.cs`，共 8 组检查源码：真实袭咬后下一次实际警告被人类制止、真实四份喂食满足普通食物需要，再验证双向邀请只读拒绝；超过 30 秒但仍未安定；安全熟悉同伴真实搬到身旁及忧虑回应；自动选择；旧档过时相处无移动/收益；暂停和保存；无伤害陌生油实际相处积累；真实安全时间恢复后再发生无关人类惊扰不重判远古施害者。第一口既有袭击意图、初始底部失能以及既有安全同伴关系明确是组件前置；之后不直接填满伤势、情绪、时间或关系过关，不冒充默认全流程爱线。攻击、下一次威胁、制止、四份食物入口/消费、真实口部及世界时钟均走产品路径。

本批仅改上述模拟文件、新检查项目两文件和本交接，共 4 文件。未动 Game、未操作 Git。8 组及既有 Family.Social、Interaction.InjuryPresentation、Interaction.AffectionJourney 均尚未运行、未编译；只完成源码与静态审查，未声称实际画面或八景验收完成。

## 第十四批：照护实效与近期施害后的关系收益分开（源码冻结）

`FamilyCare.cs:RegisterFamilyCare` 复用第十三批的同对象近期伤害门控。最近伤害过受照护者的一方，暂时不能通过送达食水或贴近保暖增加双方 CareSeconds、Familiarity、Affection，也不会新建只为这些收益而存在的对向关系。CareSeconds 原本用于满 60 秒后的稳定照护资格，并非身体保暖时钟；现在与关系收益一起受门控，不能绕过刚受到的实际伤害而建立养亲资格。

实际身体照护保持原路径：仍允许在原双方身体/关系条件合格时取走有限来源的一份，真实搬运、放下，再由受照护者预留、入口、吞咽；实际近旁保暖继续增加 Warmth，照护动作与送达记录保留。没有把 `CareRelationshipSafe` 改为全局拒救，没有删除既有亲缘/养亲，没有更改份额、出生条件或后续 60 秒资格门槛。幼体与成年失能对象共享该收益规则；无关安全照护者继续正常累计，原近期窗口及真实安全恢复后也不会永久拒绝旧对象的后续照护。

新增 `checks/Family.CareContinuity/Family.CareContinuity.csproj` 与 `Program.cs`，共 6 组检查源码：亲代真实伤害→人类制止→实际补食后保暖有效但不增关系/不创建稳定照护资格，保留既有照护者与血缘；幼体及成年失能者的食物/饮水两类真实递送与一次入口；安全另一亲代实际搬到身旁正常照护；30 秒后仍未安定的余波；暂停与带实际运输份额读档；真实安全时间恢复后通过后续实际 60 秒照护形成稳定关系。初始亲缘、底部失能、危险意图及原有照护资格是明确组件夹具；之后不填伤害时间、情绪或关系数值过关。成年对象采用真实饥饿袭咬及再次警告后的制止，幼体采用原亲代伤害流程；两者都保留一击的身体与记忆后果。真实补食按实际库存消费，普通世界时间与口部时钟一起推进。

本批仅修改 `src/Yukkuri.Simulation/FamilyCare.cs`、上述新检查项目两文件和本交接，共 4 文件。未动 Game 或 Git；6 组新检查以及原 Family.CareLearning、Family.AssistedCare、Family.CareInterruption 均未编译、未运行，仅做源码静态审查。

## 第十五批：真实相处保暖与主动结束当前相处（源码冻结）

`FamilyCare.cs` 将原照护者条件拆为相同身体资格 `CaregiverBodyReady` 和递送占用资格 `CaregiverReady`。亲代或已经形成的养亲，与同一幼体双方准确互指为当前相处对象、且真实贴身接触时，可以继续原每秒 .18 的被动保暖；相处/教学不再被当作身体失去温暖。仍要求原伤痛、恐惧、食水、体力、姿态、干预、可见/隔离等条件，不给无关陌生同伴保暖，不让陪伴另一只油的照护者冒充本幼体的当前陪护。没有增加被动 CareSeconds 或关系收益，没有放开同时递送，原实际照护 .16 保暖和收益路径保留。

`FamilySocial.cs` 新增 `EndCompanionship(string id,string companionId)`。暂停、非当前区、无效个体先只读拒绝；双方必须仍互指这两个 ID。成功复用原 `ClearCompanionship`，让出当前任务并沿原 6 秒自动相处间隔；尚未听完的教导仍按原中断逻辑结束，不产生知识，保留原教学冷却。不会解除 PartnerIds、亲缘/养亲、过去关系或营养，不额外抽随机/React/增加感情。重复停止、过期按钮和一方已与别的对象相处时全状态不变。root 家庭面板捕获展示时的两个真实 ID，不能拿解除伴侣关系代替结束一次相处。

新增 `checks/Family.CompanyCare/Family.CompanyCare.csproj` 与 `Program.cs`，共 7 组检查源码：真实邀请后保暖但拒绝同时递送；原养亲和普通同伴的差异；身体食水/疼痛、距离及其他/单向相处边界；真实结束后实际安排照护、原伴侣亲缘与关系保留；重复与过期按钮不改任何状态；暂停、无效入口、保存重载；从零同伴关系经多次真实相处达到原教学信任门槛，发生真实教导后中断，未学完不得获得知识。最后一组只有亲代已有食物常识作为前置，不填幼体信任或教学时钟。体能与亲缘/养亲、初始行动不便是明确组件夹具，不冒充生育或伤害旅程。

本批仅改上述两份 Simulation、新检查项目两文件及本交接，共 5 文件。未改 Game、未运行 Git；只做源码与静态审查，7 组及原家庭照护/学习回归均尚未编译、未运行。

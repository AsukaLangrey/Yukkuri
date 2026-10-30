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

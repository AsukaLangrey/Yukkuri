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

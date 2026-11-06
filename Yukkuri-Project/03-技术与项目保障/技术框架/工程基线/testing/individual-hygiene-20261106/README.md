# 个体卫生要求与情绪目标切片

结论：批准的个体要求曲线、环境不适目标扣值与右侧即时反馈已接入。自动规则和真实 headless 场景检查通过。不是完整卫生玩法闭环，也不是画面或手感验收。

## 依据及实现

依据 `05-游戏设计/自由模式-环境卫生与排泄.txt`（2026-11-02）。依赖区域评分切片，本地基线提交 `2812619facf3bf6bc9de3d39e286ad2f303218a2`，对应已发布父提交 `3cde9b5aeaeec150a02437729132d1ca8102d261` 的同一源码树。此次测试对象为该基线加本切片工作区文件，完整内容身份见 identity.json，非声称已提交。

- 善意≤0.15要求0；0.15至0.65线性升至92；0.65至1线性升至99。
- 规则教育0至50线性升至90；50至100线性升至99。
- 复用现有牌照等级解释，铜银金白金下限24/48/72/96。三项取最大值，最高99，卫生熟练度不参与。
- 从个体所在区域实时评分计算差额，差额×0.65，最高60。只在已有馒馒来目标最终限幅前扣除，不直接修改信任和健康。
- 保留既有每世界秒下行最多1.2、上行最多0.15的渐变。没有新增时钟、存档字段或缓存。右侧现有标签展示当前区域评分、选中个体要求与“馒馒来目标扣值”，无选中隐藏个体行。

## 实测与边界

2026-11-06 UTC，authorized test PC，.NET 10.0.402，Godot 4.7.2 Mono。原始日志留在本机，随分支仅保存脱敏文本。

- 基线真实对照：固定同一健康个体，单独调用既有情绪更新1秒，干净与20份粪便环境均保持65；Requirement API不存在。不是虚构旧API失败。复现代码见 BaselineProbe.cs。
- Interaction.IndividualHygiene：12组通过（不是12条断言）。包含所有曲线断点/插值、单调性、全部现有牌照别名、三项取max、差值倍率与60上限、个体所在区域、属性和污物即时变化、容忍个体、无直接健康/信任修改、存档派生与情绪续接。
- 精确情绪下降/恢复速率通过反射单独调用 AdvanceEmotionalContinuity 隔离其他行为。公开 Advance / AdvanceInteractionPresentation 另行覆盖显式暂停、近看冻结和恢复运行，不能把隔离测试说成完整自主生活旅程。
- 旧 Interaction.EnvironmentHygiene 9组、Interaction.LifeDevelopment 13组、Interaction.WaterEmotion 23组、Simulation.Smoke 125项通过。
- 最终产品 Debug 编译通过，0警告0错误。干净依赖首次编译有2条既有CS8604；不是本切片修复。
- 临时真实 InteractionRoom headless harness：21项通过，覆盖选中/无选中、实时脏污与14.95不适、低善意0、清理恢复、区域切换、左右栏边界。请求1440×900与1920×1080，headless实际逻辑布局均1440×900，不证明桌面缩放、人眼渲染或鼠标手感。
- 两次UI退出均记录2个ObjectDB泄漏警告，显式释放场景后仍存在。功能断言和进程返回码通过，但未确定泄漏来源，也不将其隐藏或宣称无警告。
- 首次新增规则检查1组失败是测试污物X/Z=10超场景有效边界；改为3后全部通过，未放宽产品验证或断言，首次失败证据保留。

最终harness已移出生产源码，再次完成产品编译。harness仅在本目录供复现；运行时临时复制到游戏目录，按其场景启动，并使用独立 `--interaction-test-profile individual-hygiene-20261106`，不读取个人存档。

## 未完成与后续切片

本次没有改清洁资格与任务调度。当前 CleaningWorkReason 仅使用原资格/命令，尚未以当前卫生不适控制自主启动；FindCleaningWaste 也尚未统一排除手持、搬运、离地污物。需要独立切片保留手动命令语义、正在清洁一份的完成规则、身体/时间中断与真实接近后记劳动。不能宣称主动清洁闭环已按本设计完成。

未运行全部 testing/request.json、长时压力、历史个人存档或真实输入/视觉验收；没有改屋内排泄、昼夜规则、速度档位、信任/健康规则。最终验收及合并由用户决定。

## 复核

在授权测试机工程根目录，使用锁定SDK：

- dotnet run --project checks/Interaction.IndividualHygiene -c Debug
- dotnet run --project checks/Interaction.EnvironmentHygiene -c Debug
- dotnet run --project checks/Interaction.LifeDevelopment -c Debug
- dotnet run --project checks/Interaction.WaterEmotion -c Debug
- dotnet run --project checks/Simulation.Smoke -c Debug
- dotnet build src/Yukkuri.Game -c Debug

基线复现：把 BaselineProbe.cs 作为独立控制台入口引用基线的 Simulation 工程。UI复现：临时复制 IndividualHygieneAudit.cs/.tscn 至 src/Yukkuri.Game，设置锁定SDK的 DOTNET_ROOT，编译并Godot headless导入，再启动该测试场景及独立profile。测试后移出harness并重新编译产品。不要将自动生成import/uid或私人数据提交。

发布说明：本次作为依赖区域评分分支的独立后续提交 alice/20261106-individual-hygiene，不直接基于main。identity.json保留测试时工作源码字节哈希；Git按.gitattributes规范化换行。公开文本证据已脱敏并去除行末空格，原始本机日志不变。
最终暂存前移除新检查及归档harness的文件末尾空行；产品规则未改，最终检查源码再次运行12组全部通过。identity.json已更新最终检查字节哈希。

## 追加：卫生规则下的完整感情旅程适配

组合检查发现旧 Interaction.AffectionJourney 仅提供吃喝、休息、救援和每26秒伴侣邀请，没有日常地面清理。旧运行在t=3252秒无法自主怀孕；诊断母体环境20、个体要求95、目标扣值48.75、馒馒来31.96、13处污物，兴趣0。仍保留原繁育馒馒来≥60等门槛，不因旅程失败降低产品要求。

同一组合产品的因果对照仅在原每4秒照护周期加入真实 CleanRoomWaste，保留初始状态、超时和全部原断言后通过：29处清理，t=1926自主怀孕、t=2406出生、t=2706完成育幼和母体恢复。诊断英文数值摘录见 affection-unclean-diagnostic.txt / affection-clean-diagnostic.txt；原日志中的损坏中文标签未作为证据复制。摘录来自组合验证工作区，而非本次单分支最终执行。

正式适配只修改 checks/Interaction.AffectionJourney/Program.cs，不修改三份产品文件。原每4秒照护周期过滤当前区域实际地面污物，通过真实 CleanRoomWaste 清理，逐对象确认目标已移除且评分不下降；记录清理对象数和实际份数，最终新增至少发生一次真实清理断言。所有原断言、初始条件、超时和邀请频率保留，没有清空对象列表，没有直接写卫生分数、情绪、信任或繁育兴趣。

正式最终执行（本地提交537ba2e之上，仅上述测试适配）：Interaction.AffectionJourney返回0，完整陌生→信任→伴侣→自主怀孕→出生→真实育幼/母体恢复通过。实际清理29对象/29份，t=2706评分95、要求95、不适0、馒馒来81.11。Interaction.IndividualHygiene的12组再次返回0。输出见对应 integration-final.txt，身份清单新增旅程源码哈希。

这证明维护环境的玩家照护旅程可继续，不代表油的自主清洁调度已按不适门槛完成，也不替代真实鼠标或视觉验收。

最终十一项临时组合复核：64/64纯检查及强制Game.Build通过；6份JSON无失败，Continuity129、Soak24条3600模拟秒轨迹通过。389源码/检查文件测试前后不变，来源漂移0，新增警告0。见combined-eleven-final-audit.json。该结果针对一次性叠加此前全部补丁的测试副本，不代表本分支含其他独立修复，也没有合并main。排除缺授权历史样本的Save.Compatibility、3个Preview及2个Renderer项目；未做组合可视/真实输入验收。

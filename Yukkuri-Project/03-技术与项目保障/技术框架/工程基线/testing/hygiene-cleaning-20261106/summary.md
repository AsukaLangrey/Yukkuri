# 环境卫生驱动清洁：最小实现与专项验证

日期：2026-11-06。状态：专项及最终临时十二项组合回归、游戏构建通过。不是完整玩法或画面验收。

## 来源与依赖

依据：`05-游戏设计/自由模式-环境卫生与排泄.txt`（2026-11-02，含后续覆盖说明）。已批准的资格为善意≥0.65、规则教育≥50或铜牌以上；主动启动另需脏污不适，正在清洁的一份完成，身体/休息/危险/照护可中断。

本地父提交：`e6528dd1119f9f69c9105524626f1e545c63c0cf`。
父功能已发布提交：`44c0110be93ce8d2116e26fe72c85029f1f91f1c`。
两者依赖树：`23c02a2e933dec3db7fdbb179632fe3f42350a2b`。
本次工作树基于该依赖，不能视为直接基于main的独立补丁。

## 产品范围

- `CourtyardBehavior.cs`：仅新自主启动增加不适>0条件，已有指令不加此条件；已有目标仍按原进度完成。教学与持续目标重查使用现有真实地面污物谓词。
- `LifeDuties.cs`：教学前置和清洁候选采用同一地面谓词。
- `CleaningMotivation.cs`：同伴指令只在目标仍为地面污物时有效；派工候选继承地面过滤。
- 不改公共手动清理API、资格/效率/重试数值，不恢复手动分工界面，不增加命令或存档字段。

## 同一检查的前后对照

`Interaction.HygieneCleaning`最终源码完全相同。临时替换三个产品文件为父版本，逐文件Git blob确认后强制Rebuild，再执行；恢复修复后再次强制Rebuild再执行。

父版本blob：
- LifeDuties.cs：`863d6260c361d7caf29d567859087603bad3a6a4`
- CourtyardBehavior.cs：`6de7162956ff73b474c22448da9c3da9a47ea948`
- CleaningMotivation.cs：`034110ceb52e4132d44f165f091ec283a7c27651`

有效父版本：12组中7组失败，退出1。包括舒适仍启动、清完当前份又启动下一份、空份教学、非地面持续目标未中断、空中搜索、手持派工、飞行读档旧目标。
修复版本：12组全部通过，退出0。两次重建均0错误、原有2条CS8604警告（InteractionValidation、SupplyStationValidation）。证据见evidence/baseline-rebuilt-tests.txt、fixed-rebuilt-tests.txt及对应rebuild.txt。

运行方式（工程目录）：
- dotnet build checks/Interaction.HygieneCleaning -t:Rebuild
- dotnet run --no-build --project checks/Interaction.HygieneCleaning

覆盖：资格与不适双条件、低善意高教育、真实接近和耗时、只计一次劳动、环境改善不中断当前份且不再启动下一份、保留明确命令、饥渴/精力/疼痛/恐惧/休息/有效幼体照护中断、空份/仅运动记录手持/收纳/手持/携带/悬空/飞行搜索与教学及完成过滤、真实投掷落地、同伴指令失效、合成SaveStore工作中/完成后/飞行中加载。

## 原有检查保留与调整

原样Interaction.LifeDevelopment在修复后13组中1组失败：铜牌夹具仅一份粪便，评分92；善意0.3要求27.6、铜牌下限24，故无不适，应不启动。旧失败证据保留于evidence/life-development-original.txt。

仅在原资格矩阵增加远处10份真实粪便，令区域评分低于全部测试资格的要求；原有实际完成、只记一次、未写为玩家命令等断言保留。调整后13组全通过（Interaction.LifeDevelopment-restored-final.txt）。新专项另覆盖舒适铜牌不启动。
Interaction.IndividualHygiene 12组、Interaction.EnvironmentHygiene 9组已通过；最终组合结果见文末。

## 测试过程问题与限制

新专项最初有夹具失败，原日志保留：短于固定模拟tick的Advance没有实际运行；无效成年照护目标会被原有同步流程取消。夹具改用完整模拟tick与有效幼体，未改变产品中断规则。空份对象由既有物资同步移除，测试要求不启动、不计功，而不要求空对象持续存在。

两轮对照准备结果作废：一次PowerShell字符串捕获Git源码发生中文解码损坏，只造成临时编译失败；另一次复制父源码保留旧时间戳，增量构建复用了修复DLL。两者不计为产品失败或通过，原始诊断保留在私有测试日志。有效证据只使用逐blob确认、强制Rebuild后的结果；修复源文件已恢复。

无私人存档、原开发机未发布代码、截图、桌面操作、main改动、暂存、提交或推送。合成存档不能替代缺失历史样本兼容性或用户人工验收。
## 最终组合及发布说明

65/65纯检查与强制Game.Build通过。6份真实结果JSON成功、无失败字段；Continuity129/129，新专项12组；391源码/检查文件测试前后不变，397条来源记录无漂移，新增警告0。原63/65失败、因果对照及所有初始证据保留，不以诊断结果替代正式重跑。详见combined-twelve-final-audit.json。

组合通过仅指一次性叠加已发布迭代的测试副本，不代表本分支含其他独立修复。当前分支alice/20261106-hygiene-cleaning依赖individual-hygiene父提交44c0110，不直接基于main。没有合并main。排除缺授权历史样本的Save.Compatibility、3个Preview、2个Renderer；未做视觉/真实输入验收。

源码hash为测试工作文件原始字节；Git按.gitattributes规范化换行。公开证据去除私人环境路径并规范化行末空格，本机原始日志不变。

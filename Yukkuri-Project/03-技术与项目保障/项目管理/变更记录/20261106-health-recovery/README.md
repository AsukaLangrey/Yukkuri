# 缺水恢复与身体总览修复

基线：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea。分支：alice/20261106-design-regressions。
日期：2026-11-06。验证机器：Shiki-5060Ti-PC；.NET 10.0.402；规则检查 Release，游戏编译 Debug。

## 改动
- 恢复计时结束只恢复行动资格，不再凭空把生命抬到25。
- 普通回血要求水分大于20，并保留原食物、体力、消化症状条件；补水后仍逐渐恢复。
- 身体总览并列显示伤势、消化不适和缺水，保留重伤救治提示；死亡不会显示可治疗症状，潜伏期不泄露病情。派生文本不入存档。
- 依据最新策划阅读索引及20261103个体状态审计F02/F03。F01取暖逻辑不在干净远端，不移植用户未提交实现。

## 验证结果
- 新专项 Interaction.HealthRecovery：29/29通过。修复前首批22项为6通过、16失败，复现水分0/10/20仍回血及恢复边界生命8跳到25。
- 58个纯规则检查项目：56通过、2失败。不是全绿。
- 游戏编译通过，0错误、2条原有nullable警告（SupplyStationValidation.cs、InteractionValidation.cs）。
- 五个源码/专项文件测试前后SHA256一致；独立静态审阅通过，git diff --check通过。
- 连续性检查128/129：suspended lowering and sliding bodies retain unmet excretion until grounded and still失败。
- 长模拟23/24：woodland，种子20261029，松果 exhausted despite reachable rest。
- 上述两项在独立git archive导出的原始e8d96a7源码原样复跑，得到完全相同失败，确认不是本轮引入。保留失败作为后续任务，不改旧断言。
- Milestone.Exploration通过：64种子、38400操作、768次存读档。

## 重现
在工程基线目录，用锁定版本dotnet执行：
dotnet run --project checks/Interaction.HealthRecovery/Interaction.HealthRecovery.csproj -c Release -p:UseAppHost=false
其余纯规则项目同样逐项执行；有报告参数的项目传独立输出路径。排除Preview、Renderer和需要授权历史样本的Save.Compatibility。
dotnet build src/Yukkuri.Game/Yukkuri.Game.csproj -c Debug

完整本轮原始日志留在独立工作副本工程的.local/testing/health-recovery-20261106；下附项目退出结果及专项输出。

## 未测与后续
未启动Godot画面、未做人工手感/视觉验收、未测私人历史存档。短时/模拟检查不等于长期真实玩法验收。没有改main、合并或部署，也未恢复旧监控。
下一轮先复现并修复已确认设计下的屋内排泄、休息与出屋同帧冲突；本轮两个原有失败需继续定位。成长五阶段和新数值仍属提案，不擅自当作已确认设计。

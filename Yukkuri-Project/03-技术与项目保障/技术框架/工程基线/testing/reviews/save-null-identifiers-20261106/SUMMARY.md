# 家庭空引用标识存档恢复修复

结论：两处 null ID 现已按损坏存档拒绝，可恢复有效备份。专门检查32/32通过，家庭社交22、连续照护6、兼容性77项通过。以独立分支交付审阅，合并由用户确认执行。

## 独立基线与最小修复

- 机器：Shiki-5060Ti-PC；.NET SDK 10.0.402；Release。
- 源码基线：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea。独立工作树只增加本修复和检查，不含其他七项补丁。
- 原源码运行新检查：32项，12通过、20失败，退出码1。两种个体排列均以 ArgumentNullException 失败。
- 修复：FamilyValidation.cs:34 对 CaregiverIds 的条目判 null；FamilySocial.cs:81 对 ReactedBy 的条目判 null。仅2行产品变更。
- 不扩大 SaveStore.Load 的异常捕获范围；不修改存档格式、数值、正常亲缘/社交逻辑；不夹带 Water 预检。
- 修复后同一检查32/32通过，退出码0。两个已有 CS8604 警告位于 SupplyStationValidation.cs 和 InteractionValidation.cs，未新增警告修复。

## 精确复现

创建有效 CreateCourtyardEconomy 状态，WorldSeconds=10，给第一只油登记第二只为照护者，先通过 SaveStore.Validate。两种个体排列分别验证。

1. 仅将该个体 Family.CaregiverIds[0] 改成 null。Dictionary.ContainsKey 原本抛 ArgumentNullException。
2. 或仅将 FamilyRelationshipEvents[0].ReactedBy 改成 [null]。Dictionary.TryGetValue 原本抛 ArgumentNullException。

把损坏JSON写入独立合成槽位，完整未损坏JSON写入同槽.bak。原代码 Load 直接泄漏异常，不尝试有效备份。修复后校验抛 InvalidDataException，Load 精确返回备份并保留原主档和备份字节。

## 检查覆盖

每种缺陷 × 原序/倒序 × 8项：有效主档优先；null校验拒绝且无状态改写；有效备份精确恢复；损坏现有主档阻止Save覆盖；损坏候选阻止Save覆盖；双坏档保留两个受控错误；不存在ID继续拒绝并恢复；反复校验不改变40步×0.05秒的后续时间、RNG和序列化状态。所有文件操作检查均记录前后SHA256。

运行命令（工程目录下，dotnet为原工程.tools中已安装版本）：

    dotnet restore checks/Save.NullIdentifierRecovery/Save.NullIdentifierRecovery.csproj --configfile .local/testing/null-identifiers-20261106/offline.config
    dotnet run --project checks/Save.NullIdentifierRecovery/Save.NullIdentifierRecovery.csproj -c Release --no-restore -- <全新证据目录>

Family.Social、Family.CareContinuity、Family.Compatibility 分别通过同一SDK、离线还原和 Release dotnet run运行。日志与完整合成存档仅保留在本地 .local/testing/null-identifiers-20261106。这里仅收录结果摘要与哈希，不纳入完整合成存档。

## 与此前组合诊断的区别

此前在独立 alice-save-reference-matrix-20261106 诊断目录，对 e8d96a7 加七项补丁的组合代码做368例定界矩阵：水域关注、岸上陪护、饥饿取食、家庭4种有效场景，两种排列，每组46次单字段损坏。336例 InvalidDataException、16例反序列化 JsonException 正常恢复；16例暴露本次两缺口。没有错误接受的损坏主档。8例补充测试确认合法主档优先与保护性保存。组合源382个文件、复制的112个编译源哈希均未变。

组合诊断 results.json 的 escaped=32 包含16个安全的 JsonException；真正未恢复的Load案例为16。不要把32解释为32个恢复缺陷。组合诊断只用于发现问题，不冒充本独立修复分支的回归。

未运行完整自动套件、Godot画面、人工玩法或历史个人存档。未合并main，未创建自动监控。

提交前用最终文件另行复跑32/32，退出码0，见final-file-check.json。sha256.json记录Git中实际交付的源码与证据字节，不把工作目录换行格式差异当代码变化。

## 八补丁组合与原矩阵复测

独立 detached 测试副本以同一main为基线，叠加此前七项修复与本次两行校验；没有Git合并，也没有修改此前七补丁证据副本。62/62纯检查项目和强制游戏构建退出0，结果JSON无失败；384份源码/检查文件运行前后哈希一致。仅有两条既有nullable警告。结果见combined-project-results.json。此结论属于八补丁组合，不冒充本分支单独包含其他修复。

保持原harness字节，原368损坏存档案例复测全部恢复有效备份：352例受控InvalidDataException，16例正常JsonException，未处理的Load异常为0，原先16个逃逸案例全部消失。368组主档/备份哈希不变。原harness的escaped=16包含安全JsonException，不表示16个缺陷。详见matrix-fixed-summary.json及matrix-fixed-report.md。

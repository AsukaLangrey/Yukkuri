# 水域交叉引用损坏存档恢复修复（2026-11-06）

## 结论与范围

在固定 main `e8d96a78f49182eb17e269e2b62a738c7a9bb7ea` 的独立工作副本实现，不依赖此前其他修复分支。机器为 Shiki-5060Ti-PC，使用仓库锁定的 .NET SDK 10.0.402、Release、纯 DLL 运行。交付为独立审阅分支，未合并主分支。

若存档里一只油关注或陪护另一只油，而后者的 `Interaction.Water` 被损坏为显式 `null`，原验证器可能先追踪交叉引用，再校验目标结构。目标排在后面时产生 `NullReferenceException`，跳过既有有效备份恢复逻辑；顺序反过来则可以恢复。

只在 `InteractionValidation` 开头加强所有个体的 Water 非空预检。损坏数据通过 `InvalidDataException` 走已有恢复路径。未扩大 `SaveStore.Load` 的异常捕获，不修复或重写坏档，不改变正常水域行为或备份覆盖政策。旧格式省略 Water 时仍使用属性默认值。

## 验证结果

- 原始 7 项纯合成诊断：main 为 4 项符合预期、3 项暴露缺陷；修复后 7 项均符合预期。
- 新增 `Save.WaterReferenceRecovery`：最终同一份 32 项检查，main 为 22 通过、10 失败，修复后 32 通过、0 失败。
- 重点回归：`Interaction.WaterSupport` 12/12；`Interaction.WaterAttempts` 9/9；`Courtyard.Economy` 8/8；`Family.Compatibility` 77/77。
- 正式新增检查与上述重点回归共 138 项通过；另有 7 项原始诊断对照，不重复计入 138。
- 最终构建均退出 0。模拟项目干净构建仍有 2 条原有 CS8604 警告，未扩展本次修复范围。
- 新检查包含关注/陪护、观察者/目标前后顺序、坏主档有效备份、两档均坏受控错误、Save 拒绝覆盖坏主档、坏候选保护有效历史、合法状态只读验证与两份相同状态继续推进一致、缺省 Water 兼容和观察者自身 Water 损坏。24 对主档/备份的 SHA256 在测试前后均一致。

以上均为合成测试数据。未读取个人或历史存档，未执行实际读档 UI、图形、声音、长时压力测试或整套聚合检查。

## 分支内审阅证据

以下交付文件仅含结果、案例名称、日志身份和文件哈希，不含完整合成存档：

- [32 项 main 基线摘要](save-water-reference-recovery-20261106/baseline-summary.json)：22 通过、10 失败，保留每项失败类型与首个源码位置。
- [32 项修复后摘要](save-water-reference-recovery-20261106/fixed-summary.json)：32 通过，记录已测试源码身份；最终文件仅统一换行，规范化哈希与构建快照一致。
- [4 个重点回归结果](save-water-reference-recovery-20261106/focused-regressions.json)：106 通过，列出各项目、案例名称、退出码与日志 SHA256。
- [24 对主档与备份哈希](save-water-reference-recovery-20261106/file-preservation.json)：48 份原文件逐份前后匹配。

## 本地证据

以下目录相对于工程，均被 Git 忽略，不提交完整合成存档：

- `.local/testing/save-water-reference-baseline-20261106T024025Z/`：原始 7 项 main 基线。
- `.local/testing/save-ref-fixed7b-20261106T0247Z/`：原始 7 项修复后结果。
- `.local/testing/save-ref-base32-20261106T0244Z/`：最终 32 项 main 基线，含测试源码快照。
- `.local/testing/save-ref-fixed32-20261106T0245Z/`：32 项修复后结果，含源码快照、每对文件哈希及汇总。
- `.local/testing/save-ref-regressions-20261106T0245Z/`：4 项重点回归构建日志、运行日志和计数汇总。

初版检查对损坏对象调用完整序列化时，会访问 CanWalk 派生 getter；已改为仅对可写状态字段做前后快照，保留验证拒绝断言。初期输出路径中的 apphost 生成失败和临时 Probe 扫入旧 artifacts 的构建失败均保留日志；正式检查使用新输出目录、`UseAppHost=false` 和 dotnet DLL 运行，不绕过产品断言。

## 重跑专门检查

在工程目录中，使用已安装的锁定 dotnet。`$run` 必须是新的证据目录；检查拒绝覆盖已存在的 fixtures 目录。

```powershell
$dotnet = 'C:\Project\Yukkuri\repo\Yukkuri-Project\03-技术与项目保障\技术框架\工程基线\.tools\dotnet\dotnet.exe'
$run = Join-Path $PWD ('.local\testing\save-water-' + (Get-Date -Format 'yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $run | Out-Null
'<configuration><packageSources><clear /></packageSources></configuration>' | Set-Content (Join-Path $run 'NuGet.Config')
& $dotnet build checks/Save.WaterReferenceRecovery/Save.WaterReferenceRecovery.csproj -c Release -p:UseAppHost=false --artifacts-path (Join-Path $run 'artifacts') --configfile (Join-Path $run 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& $dotnet (Join-Path $run 'artifacts/bin/Save.WaterReferenceRecovery/release/Save.WaterReferenceRecovery.dll') (Join-Path $run 'fixtures')
if ($LASTEXITCODE -ne 0) { throw 'Checks failed; preserve the evidence' }
```

## 提交前独立复核

最终冻结文件再次通过 32/32，退出码 0。结果见 [最终文件复核](save-water-reference-recovery-20261106/final-file-check.json)。命令：dotnet run --project checks/Save.WaterReferenceRecovery/Save.WaterReferenceRecovery.csproj -c Release -p:UseAppHost=false -- .local/testing/parent-final-save32-20261106T0251Z。

# v0.1.60：冷藏测试契约对齐

基于已发布 v0.1.59，提交 da5085a477c0f3c166c24b721a50d173186a7162。仅更新冷藏测试及版本号，不修改生产玩法规则。用户随后要求推送当前更新并停止，扩展验证已中止；本版本不是全套通过或完整玩法验收。

## 设计依据与改动

已提交的《20261103-个体状态设计与实现审计》D02 明确：12 格冷藏冻结生理时间、无自动到期、无取出复苏等待，不能恢复旧十倍规则。testing/local-feature-recovery-20261107/README.md 同样明确恢复 12 格暂停生理时间的冷藏。本次不是仅按实现倒推新设计。

- Family.Presentation 的前两组旧预期明确被替换：保存时 0.1 倍、600 秒后恢复全速及旧到期文案不再有效。新检查通过真实 SetColdStorage 和正常 Advance 推进至 599、600、610、700 秒，验证身体冻结、说明一致、暂停、实际 SaveStore 存读档、持续身份和手动取回后的正常时间。没有设置 Elapsed 来冒充跨期限运行。
- 第三组历史存档的 RecoveryRemaining 复苏文案检查原样保留，不能据此解释成新取回流程会强加复苏等待。
- Family.Growth 只替换冷藏一组，使用 GrowthRules.Initialize 和当前成年年龄构造合法若油；真实保存 601 秒后仍未成长，暂停/读档保留身份，手动取回才自然跨过成年门槛。其余四组原样保留，包括失养成长失败。
- 12 格容量及第 13 个拒绝继续由未改动的 Courtyard.ColdStorage 覆盖，没有重复实现容量机制。

## 已完成验证

Windows，.NET SDK 10.0.402，Debug。串行、单逻辑处理器、BelowNormal；独立缓存与合成存档，固定总预算 1800 秒、单进程上限 300 秒。

基线 → 最终定向：
- Family.Presentation：1/3 → 3/3。
- Family.Growth：3/5 → 4/5；仍失败于“失养幼体只靠时间就成熟”，未修改该断言或规则。
- Courtyard.ColdStorage：7/7 → 7/7。
- Courtyard.SurvivalGrowth：14/14 → 14/14。
- Family.CareContinuity：6/6 → 6/6。
- Family.ColdCare：3/9 → 3/9，原失败保留，未顺带修夹具或场地预期。

主工程基线与最终构建均成功。首次完整构建有 4 条既有空引用警告；最终增量构建 0 错误。实际程序集版本为 0.1.60.0，产品版本为 0.1.60+da5085a477c0f3c166c24b721a50d173186a7162。Godot SDK 与依赖包的本轮包来源记录均指向本地官方发行包；不据此声称所有恢复网络请求均离线。

扩展计划共 66 个纯 .NET 项目。收到停止要求时，已完成 48 项运行：18 项退出码为 0（含 1 个观测程序），30 项失败。其余 18 项未运行，Interaction.MouthPresentation 的构建被中止；不是完整 66 项结果。全部已完成的扩展构建成功。未启动后续 Soak、Exploration 或额外强制重建。已停止本轮自有运行器和子进程。

首次日志中文解码问题及修正后同源码基线均保留。最终执行源码在验证期间 SHA256 不变；原始失败、进程/时间/参数和中止记录保留在本地，不提交缓存、存档或完整清单。

未测：未完成的扩展项目、真实历史存档兼容、额外可选模式、可见游戏、Godot/渲染/预览、画面音频与输入手感、真实长时间稳定性。

## 最终源码 SHA256

- checks/Family.Presentation/Program.cs：B46E519C3B20323D1AB907C6AA0F4BFC13A9733055927C795B6A81B38E1280FB
- checks/Family.Growth/Program.cs：4F119E5EE27B38ACB0C02B0222BE7F77C3F4F0DC8184D998E572F35D5829E81B
- src/Yukkuri.Game/Yukkuri.Game.csproj：F0DB117C4A4F8451235254DCD5D87C74CD80F4805B36BFA2AD359252D3C48753
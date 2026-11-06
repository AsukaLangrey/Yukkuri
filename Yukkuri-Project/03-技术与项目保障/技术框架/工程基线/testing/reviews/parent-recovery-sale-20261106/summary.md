# 父油恢复期收购限制修复

日期：2026-11-06 UTC。机器：Shiki-5060Ti-PC。
源码基线：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea（main）；结果针对该基线加本次未提交改动。
分支：alice/20261106-parent-recovery-sale。仅在独立工作副本开发，未合并主分支。

## 依据和范围

`Yukkuri-Project/05-游戏设计/自由模式-繁育经营闭环.txt` 第3行明确记录用户确认；第33行要求“当前孕育及恢复涉及的亲油需等待”。核对最新阅读索引及自由模式、个体成长资料，未找到覆盖这条交易限制的后续批准规则。

`src/Yukkuri.Simulation/CourtyardEconomy.cs` 的 `CreatureSaleReason` 已阻止母油在所有非 None 孕育阶段出售，但关联父油只匹配 Preparing / Gestating，漏掉 Recovering。修复只补入 Recovering，不增加冷藏恢复、收集材料冷却等新禁售规则，不改变价格、收入、存档格式或 UI。

`FamilyReproduction.cs` 的真实出生会进入 Recovering；恢复完成将 Phase 改为 None，但保留 FatherId 历史来源。本次不能按 FatherId 非空永久禁售。
人工干预会记录原恢复债务；及时中断后可重新回到 Recovering。本次测试覆盖真实出生后开始干预再主动中断，不凭空构造非法孕育状态。
取茎路径由 EndPregnancy 进入母油恢复，并另有现成 Loose/Bottled 离体茎拦截。本次不改变这条路径；现有离体茎完整检查通过。

## 基线复现与测试演进

1. 原8组 Courtyard.Economy 检查通过。新增真实出生恢复回归在父油 `interaction-oil-1` 的首个准入断言失败：`recovering parent preview allowed`，总 FAILURES 1。该组在此抛出，后续出售确认、无关个体、恢复完成等断言未在这一轮执行，不能称它们均在基线复现失败。
2. 补入 Recovering 后，人工干预回归通过；另一组在无关父体的固定投放点 (5,5) 被真实地形拒绝。该失败是测试夹具落点错误，不是产品回归。改用只读 NewCreaturePlacementReason 筛选合法地面，没有放宽产品检查或预期。
3. 最终10组 Courtyard.Economy 全部通过（组数，不是断言总数）。新增2组覆盖：真实出生恢复、母父准入、旧报价与现报价实际提交拒绝、序列化状态与随机数不变、后代和无关父体仍可出售、合成存读后拒绝、正常恢复后 FatherId 保留但可实际售出、账目/血缘留存，以及人工干预中断保留恢复限制。

4. 最终相同测试源码再次对照：测试 SHA256 在两轮均为 EE3C9CD603C018290049C53DA99218ACC175842D7BC5509E9DC0BFAB8ADCE48D。旧产品两组新增检查分别在父油准入、人工干预中断/存读后的恢复准入失败，FAILURES 2 / exit 1；修复产品10组全过，FAILURES 0 / exit 0。旧产品 SHA256 为 148D252C16B554D452231E6DC0107DD83BE3FFB1DB73F89F02D648F95526187A；修复产品 SHA256 见下。每组仍在第一个失败断言短路，不声称后续所有断言均在旧产品运行失败。原产品已恢复为修复版本，hash 再核一致。此对照不与 Godot 或其他编译并行切换源码。

## 最终验证

- Courtyard.Economy：10组，FAILURES 0，exit 0。
- Family.Reproduction、Family.PregnancyBoundaries、Family.Risk、Interaction.DetachedStem：全部执行完成，exit 0。
- Yukkuri.Game Debug 全工程构建：0错误，0警告。此前规则依赖首次编译有既存两处 CS8604，Family.Risk 首次编译另有一处 CS8604，均未在本修复范围修改。
- 原 Godot 实际场景 MarketVerification：PASS，Renderer 为 headless。包含已锁商品控件、确认刷新、实际售出到账、actor 移除、暂停保留、采购和保存验证。该旧 harness 未新增父油恢复期专属按钮断言；该边界由规则API检查覆盖，UI准入与确认按钮仍调用同一规则。
- Godot 导入 exit 0。Market harness exit 0，但退出时报告2个 ObjectDB 实例泄漏警告；未诊断为本修复造成，亦不把退出警告隐去。
- 未做可见画面/人工试玩、长时间压力或个人历史存档兼容验收。不访问用户存档，不更改用户未发布工程。

## 命令和证据

工程根目录内，SDK 为已安装 .NET 10.0.402；每个规则检查命令：
`dotnet run --project checks/<检查名>/<检查名>.csproj --configuration Debug --no-launch-profile`
完整构建：`dotnet build src/Yukkuri.Game/Yukkuri.Game.csproj --configuration Debug --nologo`。
Godot 4.7.2：先 `--headless --path <工程>/src/Yukkuri.Game --editor --import`；再 `--headless --path <工程>/src/Yukkuri.Game -- --interaction-test-profile parent-recovery-sale-20261106 --economy-check-output <证据目录>/market-result.txt`。
Godot 的 APPDATA、LOCALAPPDATA 均显式指向本工作副本 `.local/testing/parent-recovery-sale` 下独立目录；DOTNET_ROOT 指向已安装工具。工具缓存通过忽略的 `.tools/godot` 联接复用，不包含另一工作副本的源码。

本机证据目录：工程根 `.local/testing/parent-recovery-sale/`。
- baseline.txt：原产品源码加第一组新测试，预期失败。
- fixed.txt：最小修复后发现夹具非法投放点，保留失败。
- fixed-ground.txt：修正夹具后10组通过。
- same-tests-baseline.txt / same-tests-fixed.txt：最终相同测试源码对照，旧产品2组失败、修复产品全部通过；配套 identity.txt 保存两轮产品与测试 hash。
- Family.Reproduction.txt、Family.PregnancyBoundaries.txt、Family.Risk.txt、Interaction.DetachedStem.txt。
- game-build.txt、godot-import.txt、market-runtime.txt、market-result.txt。

最终文件 SHA256：
- src/Yukkuri.Simulation/CourtyardEconomy.cs：CB4FF385A8F799329962C71EC8468B3DA1DBAC54EEE8FD4E33A0368E4850F81F
- checks/Courtyard.Economy/Program.cs：EE3C9CD603C018290049C53DA99218ACC175842D7BC5509E9DC0BFAB8ADCE48D

原始日志、构建缓存和合成测试存档留在本机忽略目录，不随源码提交。由主协调者审核后提交到分支；未执行暂存、提交、推送或合并。
本目录 baseline-result.txt、fixed-result.txt 为最终相同测试源码的精简原始结果摘录；market-result.txt 为旧场景 harness 原始结果。完整日志保留在上列本机目录。

发布说明：以上哈希为测试时工作文件原始字节；Git 按 .gitattributes 规范化换行，远端发布以本地提交树一致性核对。此报告随独立审阅分支提交；不合并 main。

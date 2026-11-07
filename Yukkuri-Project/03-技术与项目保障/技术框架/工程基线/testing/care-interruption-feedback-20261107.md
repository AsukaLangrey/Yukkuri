# 护理中断反馈修复验证

结论：修复了护理尚未完成时，松开或取消后仍显示“正在处理伤处……保持接触，松开可中止。”的问题。产品仅增加 4 行结束反馈逻辑，不修改护理时长、治疗效果、库存结算或伤势规则。

## 版本与范围

- 基础提交：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea。
- 工作分支：alice/20261107-care-interruption-feedback。
- 产品文件：src/Yukkuri.Simulation/InteractionGesture.cs。
- 永久回归：checks/Interaction.Inventory/Program.cs，新增 8 个检查组，原有检查保留。
- 策划依据：人油互动 interaction-catalog-v003 第 15–20 行的松开/离开停止及结果反馈，第 84–88 行的已用物品与实际护理结果保留；界面交互准则第 11–12 行的就地反馈。
- 主干与提供的组装基线中，InteractionGesture.cs 修复前逐字节一致。修复可以独立应用，不依赖其它护理进度改动。

## 失败在先，修复在后

使用既有生产 Hurt API 在 Side 部位制造受控轻伤（12），确认不是危重或死亡、有真实伤处且库存有药，再调用真实 Begin/Continue/EndInteractionGesture。未宣称由自然玩法产生伤势。

1. 产品未改时，在主干上运行扩展后的 Interaction.Inventory：16 通过、4 失败，退出码 1。
2. 将完全相同的检查链接到只读组装基线的模拟工程，构建输出另存：同样 16 通过、4 失败，退出码 1。
3. 四个失败均是“松开/取消 × 仍接触/先移开”结束反馈错误；失败之前已确认库存、健康、伤势和护理标志没有被中断操作改写。
4. 加入 4 行产品修复后，在主干基础上重建并运行同一检查：20/20 通过，退出码 0。

新的结束提示为：“护理已停止，尚未使用护理包；已有伤势仍需继续照料。”只替换尚未生效、成功且非拒绝的 nursing 提示。最新的错部位提示、缺药拒绝和已完成护理结果继续保留。

永久检查另覆盖：重复结束不重放；已完成后继续按住、移开和松开/取消不重复扣药、不返还药品、不撤销敷料和护理；无伤、无药、无效目标的入口拒绝不改变世界状态。

## 最终回归

以下各工程均单独恢复、构建并执行，全部退出码 0：

- Interaction.Inventory：20/20。
- Interaction.Completion：3/3。
- Interaction.CompletedNeeds：1/1。
- Interaction.StateBoundaries：10/10。
- Interaction.Injury：15/15。
- Input.Intent：43/43，包括离开伤处重置接触进度及保留已完成护理的既有检查。
- 合计：6 套、92 项通过。

主干模拟工程完整编译有 2 条既有 CS8604 警告，来自 SupplyStationValidation.cs 和 InteractionValidation.cs；没有新增编译错误。后续增量构建不能用于声称这两条警告已经消除。

## 执行与证据

2026-11-07 UTC，在获准本机的独立工作副本执行，不是远程验证。采用锁定的 .NET SDK 10.0.402、Debug，离线包源。所有测试使用独立用户目录、缓存和临时目录；不读取玩家存档。关闭共享编译器和节点复用，最大构建并行数为 1。

每个进程上限 300 秒；总预算 1800 秒，从首次恢复尝试固定起算，未重置。预算锚点 00:34:13Z；实际首个恢复进程 00:34:37Z；最后检查 00:37:40Z 完成。单个最长执行阶段约 10.16 秒。初次运行器时间解析自检在启动 dotnet 前停止，修正解析后沿用原预算。原始失败与阶段记录留在独立本地测试目录，未纳入提交。

各项目采用以下命令形态，PACKAGE_FEED 为已安装工具自带的离线包目录，ARTIFACTS 为隔离输出目录：

```
dotnet restore checks/NAME/NAME.csproj --source PACKAGE_FEED --ignore-failed-sources --artifacts-path ARTIFACTS --disable-parallel -p:NuGetAudit=false -p:UseSharedCompilation=false -nodeReuse:false -maxcpucount:1
dotnet build checks/NAME/NAME.csproj --no-restore --artifacts-path ARTIFACTS -p:UseSharedCompilation=false -nodeReuse:false -maxcpucount:1
dotnet ARTIFACTS/bin/NAME/debug/NAME.dll
```

发布内容不包含可复用运行器；本轮运行器只用于本次隔离验证。等待使用 WaitForExit，保存自己启动的 PID 和启动时间；超时只允许结束身份匹配的本轮进程树。本轮未触发超时，未声称验证了超时分支。

## 文件身份（SHA256，工作副本字节）

- 修复前 InteractionGesture.cs：AD9C506F45295E69C149A2FE2D74A1671363A5B6B7C3392E9DDD61F4073807F9。
- 修复后 InteractionGesture.cs：F439F163AFB4F2530F9EBAEE2D3EF12BED75285B0025E15A6FE4501ED1627F1A。
- 最终 Interaction.Inventory/Program.cs：F1F28F787C4943C99D25DA606D530E2A71F074C675C10E34335D9CC27334ABC9。
- 两次基线执行输出各自哈希相同：4FEF148353520ADF982CC1976BC6822B12CA6C835B29BA771BDAFB213EC502F0。
- 修复后 Inventory 执行输出：1BE02648BD13110767CA006BE2BAD77727953D50F6EC2068D4EDDAFEA88F2144。

## 未测范围

本报告覆盖真实模拟 API 和既有输入规则检查。UI 仅静态确认 EndPointerGesture 将 EndInteractionGesture 的返回值交给 AcceptFeedback；没有执行 Godot 生产 UI 回调或原生窗口操作，没有人工手感或画面验收。未做整套全量测试、长时压力测试和历史玩家存档兼容验证。独立组合验收另行报告，本报告不代替合并后复测。
## 独立组合验收

当前组装源码精确融合4行产品补丁，12套188/188组通过：Inventory20、Depth4、Smoke48、Input.Intent43、StateBoundaries10、Injury15、Completion3、CompletedNeeds1、CareInterruption17、CareContactProgress12、CareContinuity6、独立边界9。独立边界覆盖重选、重入、最后缺药/错部位提示、非法输入、新受伤反应和死亡清理，作者测试未改。

36个进程全部退出0、无超时，单个最长4.876秒，沿用固定1800秒预算。初始运行器时间解析错误在SDK启动前修正并留证。143个组装输入收尾无漂移；精确撤销4行后恢复原始产品哈希。最终117个源码/项目输入清单 SHA-256 为 9611198C3DC6FBA6AE8D0A444FE2BB7E752E249BC36FA76BD011E920B10EDD36。保留两条既有CS8604警告，未做Godot UI、音频、长时性能或自然伤害生成验收。

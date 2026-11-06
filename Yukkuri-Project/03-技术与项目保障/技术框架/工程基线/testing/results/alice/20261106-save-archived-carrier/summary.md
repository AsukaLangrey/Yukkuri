# 已售个体不能继续归属当前食水搬运

本次只修存档校验。真实商店出售流程本来会取消照护、放下搬运份额；问题需要在合法出售后的序列化数据中损坏两个引用，不能声称正常出售会生成此状态。

基于 main e8d96a78f49182eb17e269e2b62a738c7a9bb7ea，分支 alice/20261106-save-archived-carrier。在获授权 Windows 测试环境 上运行，未使用原开发机、私人存档或原工作区未发布源码。只复用已安装 .NET SDK。

## 复现和影响

用 CreateCourtyardEconomy 建立场景，按实际报价 SellCreatureToShop 出售个体。增加同地区、单份 Meal/Water/Sweet，先验证原状态合法，再只改序列化物品 CarrierId 和已售档案 Family.CareParcelId。

未修源码接受该坏状态，Load 选择坏主档而不是合法备份。离场身份不再推进，三秒实际模拟中物品仍被离场身份占用，未完成递送。玩家 HoldSupply 可以找回，因此不是永久丢失。此检查不是完整自然摄食或长期运行验证。

最小修复：仅当前 RoomObject.CarrierId 查询使用当前 Creatures 字典。KnownCreatures 仍供历史关系和亲缘使用。没有放宽检查、清洗数据、扩大异常捕获或改变存档版本。

## 冻结检查和结果

同一最终 Program.cs，对原源码和修复源码分别执行 Release 强制 Rebuild，再直接运行对应 DLL。未使用增量构建旧 DLL 作为修复证据。

- 原源码：55 项，19 通过、36 预期失败。36 个文件场景中 24 不变、12 改动。改动来自非法候选保存与覆盖坏主档，各六种组合。只读 Load 虽未修改文件，但错误接受坏主档。
- 修复源码：55/55 通过；36 个文件场景全部不变。包括主档、备份、预存 .writing 哨兵的文件数量及 SHA256。
- 三类食水、正逆数组顺序：受控 InvalidDataException、合法备份恢复、无备份、双坏文件、好主档优先、拒绝非法保存候选、保护已坏主档。
- 合法出售历史同伴/关系事件往返保留。通过真实 PlaceInteractionSupply、ArrangeCare 和实际推进拾取份额，确认正常在场搬运可往返，出售搬运者会放下原份额，亲缘历史仍合法。
- 独立 PowerShell 解析结果与前后 hash，并复算修复后磁盘文件 hash；不是只看退出码。
- 关联回归：Family.CareInterruption 17/17、Family.CareContinuity 6/6、Family.AssistedCare 16/16、Courtyard.Economy 8/8，均独立强制重建成功、执行退出码 0。

## 源码身份和命令

路径以下均相对工程基线。

- 原 FamilyValidation.cs SHA256：6AC1E0B37926AD384C8E41B53019ACE70F4818DE9F27ABF8462C4F8A8772CE6C
- 修复 FamilyValidation.cs SHA256：AD184E35EF69020570C00D055469FC26D18E48AF05FB701DF390A5134D05F0AA
- checks/Save.ArchivedCarrierRecovery/Program.cs SHA256：8500D8FD5AB863F8A96764FDE069B1D94862FCBEB5D0697ECFB3587F9A88F566
- checks/Save.ArchivedCarrierRecovery/Save.ArchivedCarrierRecovery.csproj SHA256：AF16FC5FFCEDB208F8352FA7824EB96D8F6720D3BEF6CC03980AD9752855CA8A
- 完整规则源码文件 hash 见 evidence/source-manifest.json。

命令模式：dotnet build checks/<检查名>/<检查名>.csproj -c Release -t:Rebuild，然后 dotnet checks/<检查名>/bin/Release/net10.0/<检查名>.dll；新检查额外传入一个不存在的独立输出目录。存档全为该检查临时生成，不是用户存档。

## 证据与边界

evidence/baseline.json、fixed.json 保留每项结果以及36场景逐文件前后hash；independent-verification.json 为独立汇总，related.json 为既有回归结果。

原始输出保留在 .local/testing/archived-carrier，包含初次 dotnet run 参数误传导致的启动失败，以及一次 PowerShell git show 文本转码造成的21条编译错误。随后从保留源码字节恢复、核对原始 SHA256，再执行最终配对验证。两类工具使用错误不算产品缺陷；没有删除失败日志。最终构建仍有两条既有 CS8604 警告（InteractionValidation、SupplyStationValidation），无新增检查警告。

未运行 Godot、游戏界面、CUA、截图或长期性能压测。Save.Compatibility 需要明确历史样本，本次没有私人/历史样本授权输入，不宣称旧档全面兼容。没有暂存、提交、推送或合并；由主协调者统一处理。

十七项临时组合验证：70/70纯检查及Game强制重建通过，405源码文件稳定。FamilyValidation局部应用本修复并保留之前CaregiverIds空标识保护；403前序来源无漂移。55新检查、36文件场景通过，5处既有警告无新增。组合未运行Godot；排除项及审计见evidence/combined-seventeen.json。

# 供给器选中状态与右栏一致性

## 结论

已复现并修复：先观察地面饭盘或外茎，再放置或抓取供给器时，右栏仍显示旧对象标题和额外详情。产品修改只在 `SelectSupplyStation` 中清空旧地面对象和外茎观察标识。

修复前，同一最终诊断为 **44/60，16项失败**；修复后及最终全量重建后均为 **60/60通过**。频率操作始终只作用于当前供给器，选择不消耗库存；没有发现库存丢失或操作错对象。本报告不代表原生鼠标、实际画面或完整玩法验收。

- 基线：`e8d96a78f49182eb17e269e2b62a738c7a9bb7ea`。
- 分支：`alice/20261107-station-inspector-selection`。
- 本次验证身份为基线提交加 `evidence.json` 中五个源码文件的SHA256；基线提交本身不包含修复。
- 设计依据：`Yukkuri-Project/02-基础资源与内容设计/界面管理/布局与组件规范.txt` 第10、23、42行，右栏标题与完整属性对应当前对象。

## 复现与覆盖

1. 创建独立新局，用真实商店确认按钮出售两只初始个体，达到供给器解锁收入。
2. 通过实际购买接口购入食物供给器和饮水器；断言准确扣款、入库。
3. 通过食物工具栏及投影输入回调投放一份饭饭，再以观察工具实际命中该饭盘。
4. 分别放置新的供给器、用抓取工具拿起已有供给器。修复前两条路径都残留旧对象标识，右栏标题和额外详情被旧对象覆盖。
5. 对有效外茎观察重复上述两条路径。外茎使用经存档验证器检查的已有孕育状态夹具，不冒充从繁育开始的完整流程。
6. 食物与饮水两种器具各覆盖饭盘/外茎 → 放置、抓取、观察直接选择，共12个场景。每个场景检查标题详情、刷新、频率目标、库存、重新选择旧对象。

观察模式直接选择为对照，修复前已通过。放置只消耗一件对应空器具，不动另一种器具库存、资金、食物或清水。供给器频率通过真实内嵌选择按钮修改，并检查其他供给器保持原频率。旧对象操作按钮不会错误出现。

这里使用真实场景的按钮信号和 `_Input` / `_UnhandledInput` 投影坐标回调，未执行桌面鼠标自动化，也没有把headless称作视觉验收。诊断期间不推进自然时间，不读取玩家存档。

## 保留的失败与限制

- 前两次诊断各有10个夹具失败，原结果保留在证据中。主线没有坐垫命中几何分支，无法经投影选中坐垫；早期外茎夹具也缺少Stem子状态。没有绕过真实观察入口或直接填入选择标识来掩盖失败。
- 改用真实投放饭盘并补齐有效外茎状态后，第三次诊断为34/50、16项产品失败。增加详细状态证据与外茎观察对照后，最终修复前为44/60、16项产品失败。
- 未修改坐垫点击、经济规则、库存、补充频率规则、存档格式或弹窗行为。

## 实测结果

- 最终完整游戏Debug重建：通过，0错误、2条已有CS8604警告，位置为 `SupplyStationValidation.cs:12`、`InteractionValidation.cs:15`。首次完整基线构建也有相同警告。
- 供给器场景回归：60/60，最终全量重建后重复60/60，未把重复运行计作新增覆盖。
- Input.Intent：43/43；Interaction.StateBoundaries：10/10；Interaction.SupplyStations：8/8。三个原有检查项目未修改。
- 保护检查：8/8。缺少测试profile、直接进入Main、混用legacy、重复使用profile、重复使用输出、缺少隔离根均在创建世界和SaveStore之前退出；旧profile/输出字节保持，未生成普通存档目录。
- 初始离线恢复成功，但记录了两条相对工具包目录缺失提示；使用明确指定的已安装Godot离线包恢复，无工程配置改动。
- Godot导入退出码0，隔离新profile下记录4条 `get_system_dir` 错误；后续场景实际加载和诊断通过。没有把导入输出描述为无错误日志。

## 运行约束与身份

使用已安装.NET SDK 10.0.402和Godot 4.7.2 Mono，二者可执行文件SHA256及每阶段时间/退出码见证据。SDK首次使用前关闭ASP.NET证书生成及首次体验；APPDATA、LOCALAPPDATA、TEMP、TMP、USERPROFILE、DOTNET_CLI_HOME和NuGet缓存使用新隔离目录。

固定预算开始 `2026-11-07T01:00:13.0932769Z`，最后被测进程结束 `2026-11-07T01:10:10.8118508Z`，实际597.72秒，未重置1800秒总限额。每个进程使用实际WaitForExit，限额为300秒与总剩余时间的较小者；最长46.24秒，无超时。结束时逐个核对自身PID及启动时间，没有仍运行的匹配进程。禁用共享编译、节点复用，最大构建并行度1。

复验参数（路径以变量替换）：

```text
<DOTNET> restore src/Yukkuri.Game/Yukkuri.Game.csproj --configfile <OFFLINE_CONFIG> --disable-parallel -p:NuGetAudit=false -p:RestoreSources=<NUPKGS> -p:UseSharedCompilation=false -m:1 -nr:false
<DOTNET> build src/Yukkuri.Game/Yukkuri.Game.csproj --no-restore -p:UseSharedCompilation=false -m:1 -nr:false -t:Rebuild
<GODOT> --headless --path <GAME> --editor --import --quit --rendering-method gl_compatibility
<GODOT> --headless --path <GAME> --rendering-method gl_compatibility --quit-after 1800 -- --station-inspector-check-output <ROOT>/fresh-output.json --interaction-test-profile <fresh-name>
```

`YUKKURI_STATION_INSPECTOR_ROOT` 必须为新的隔离绝对路径；上述应用数据变量、Godot用户目录和输出必须在其中。profile目录和输出必须不存在。未传入诊断开关时保护函数直接返回。诊断仅允许headless且拒绝与其他诊断混用。临时限时执行脚本不作为可复用工具交付。

## 提交白名单

以下路径相对工程基线，只有这7个文件属于本次交付：

- `src/Yukkuri.Game/InteractionRoom.SupplyStationsUi.cs`
- `src/Yukkuri.Game/InteractionRoom.cs`
- `src/Yukkuri.Game/Main.cs`
- `src/Yukkuri.Game/StationInspectorVerification.cs`
- `src/Yukkuri.Game/InteractionRoom.StationInspectorVerification.cs`
- `testing/results/station-inspector-selection-20261107/summary.md`
- `testing/results/station-inspector-selection-20261107/evidence.json`

两个新增 `.cs.uid` 无须提交：诊断通过现有编译类型直接调用，没有场景或资源引用其UID。它们和其余自动生成的UID、import元数据、缓存、二进制、隔离数据、原始日志均不在白名单内，保持本地原位。

证据文件SHA256：`9A48A6EB5E40C371F38F4F316F2C154CDF2BE32A53B1E0C641341164D2D4A112`。

未测：原生桌面输入、视觉和音效验收、长时间玩法/性能、玩家或历史存档兼容。本次不合并主线。

## 独立组合验证

当前assembled测试副本按精确单行产品差异融合，既有诊断入口全部保留；逆向恢复的源码哈希与原始组合基线一致。该组合由多个既有分支构成，不能以main提交冒称完整组合身份。

9套共233/233通过：供给器60、Shop草稿38、Company31、Selection24、Input43、StateBoundaries10、SupplyStations8、Inventory12、CompanyCare7。另有6/6非法入口均exit2且未建世界或存档目录。28个进程全部结束，0超时，固定1800秒预算实际279.04秒。

产品SHA256：5A43EFB8DA19E2D1C7CACF776C2C65CDC2AE59B82F2BA95D9B8963D781BD6CB4。最终源码清单SHA256：886B0AD493C493663486D86615FB92FAEF8CAA45E36DBC47886B994A83C5B10C；独立证据SHA256：A8C0FB3F9ED63D989422275226CA9C3D3261D058AA8251BC78511D8D050BDB26。

两条既有CS8604、两条离线源提示和四条导入get_system_dir错误均保留，导入exit0后场景通过。两次外围PowerShell包装器错误（UTC重复解析、路径正则）保留并修正，未改C#、未重复成功进程或重置预算。原生输入、视觉和长测仍未覆盖。

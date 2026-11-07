# 小家购买页解锁反馈修复

## 结论与版本

修复新开局「工具 → 小家与藏身处」的购买反馈不一致。未到累计售油收入 500 元时，专页现在显示规则层给出的解锁原因，并禁用购买。执行购买时仍由原有规则重新检查；拒绝结果优先显示当前解锁原因。

未调整售价、500 元门槛、开局资源、库存限制或存档规则。未增加弹窗、暂停切换或新的玩法流程。

- 测试日期：2026-11-07 UTC。分支名的 20261106 是原任务命名，不是测试日期。
- 分支：`alice/20261106-shelter-unlock-feedback`。
- 源码基线：`e8d96a78f49182eb17e269e2b62a738c7a9bb7ea`，加下述源码哈希标识的改动。测试时尚未提交；最终合并由用户审阅决定。
- 环境：获授权本机隔离测试副本，不是远程机器。Debug、.NET SDK 10.0.402、Godot 4.7.2 Mono、headless。
- 设计依据：`05-游戏设计/自由模式-繁育经营闭环.txt`「道具解锁」；`02-基础资源与内容设计/界面管理/交互准则.txt` UI-001 就地反馈要求。

## 修复前确实发生的失败

先加隔离诊断，没有修改购买页行为。实际创建默认经营世界，经真实工具按钮打开专页：余额 240 元、小家库存 0、累计售油收入 0。

购买按钮当时可用，页面没有「累计售油收入达到 500 元解锁（当前 0 元）。」。发送真实按钮的 Pressed 信号后，规则层拒绝购买，但页面显示「未能购买，请检查金钱和库存。原有用品没有减少。」。余额仍为 240，库存仍为 0，没有发生扣款或库存丢失。

同样流程在普通商店的居所分类正确显示原因并禁用购买。基线诊断共 32 项，25 通过、7 失败；失败全部属于锁定状态的显示、禁用或重复打开检查。原始失败和检查点保留，未用修复后结果覆盖。

## 实际验证范围

最终生产源码的 headless 场景诊断：32/32 通过。

- 默认经营开局的禁用状态、原文解锁说明、三次关闭再打开、普通商店一致性、无扣款和库存变化。
- 使用生产 `SellCreatureToShop` 和必要的 `PlaceNewCreature` 路径取得累计 626 元售油收入，重新打开后解除锁定。没有直接赋值 626，也没有声称这是自然繁育或无买卖损耗的流程。
- 通过实际购买按钮购买一间：余额 386 → 341、库存 0 → 1、累计收入仍为 626。
- 摆放按钮启用并关闭侧栏；取消摆放不扣库存；生产摄像机落点映射和 `PlaceShelterAt` 路径消耗一间库存并生成居所。
- 精确 499/500 元为明确标注的受控账本夹具：调整最后一笔成交金额及累计金额，保持正数和账目合计一致，并通过 SaveStore.Validate。不是自然生产进度。
- 余额 44 元和库存 99 的受控夹具下，专页禁用购买，规则拒绝且账目不变。
- 旧目录行为使用 `CreateInteractionPlaytest` 的 EconomyProgression=false 夹具，验证零累计收入仍可买、正确扣款。不是读取或迁移真实历史存档。
- 面板打开、关闭、返回不改变暂停状态，没有玩法模态弹窗。

相关现有规则检查共 74 项，全通过：Interaction.Shelters 13、Courtyard.Economy 8、Input.Intent 43、Interaction.StateBoundaries 10。Courtyard.Economy 的既有繁育/成长检查本次确实执行，记录 4226.5 秒模拟时间，不是实时时长或长时间压力测试。

诊断保护共 9 种拒绝情形通过：错误旗标、缺 profile、只有 profile 的直接入口、额外/legacy 参数、复用输出目录、复用存档目录、相对输出、重复旗标、直接指定 Main.tscn。直接 Main 保护是在独立复核指出入口缺口后补上；最终源码重新构建并复跑了上述场景和四套规则。保护在创建世界及 SaveStore 前触发，直接 Main 的 APPDATA 仅含引擎日志目录。复用存档测试的哨兵文件保持不变。

以上是场景控件信号及规则层验证，不是原生鼠标操作、截图、人眼画面或完整游戏验收。没有复现生产中的旧状态回调竞态，也不将人工修改状态后强行发送禁用按钮信号算作自然流程证据。

## 运行隔离、失败和警告

首个 restore 前固定预算开始于 2026-11-07T00:03:57.1215190Z。整个验证共用该 1800 秒预算，任何单进程最多 300 秒；使用 WaitForExit，超时只按已记录 PID 和启动时间处理自己的进程树。本轮无超时。各次尝试使用独立目录，不重置预算或覆盖失败。

每个进程的 APPDATA、LOCALAPPDATA、TEMP、TMP、USERPROFILE、DOTNET_CLI_HOME 均在工程 `.local/testing` 内；禁用首次体验和 ASP.NET 证书生成，共享编译关闭、node reuse 关闭、最多一个构建节点。使用已安装的 dotnet/Godot 二进制和离线 bundled nupkgs，没有读取原仓库源码或用户存档。

首次基线构建有两条原有 CS8604（SupplyStationValidation.cs、InteractionValidation.cs），以及诊断临时引入的一条 CS8629。诊断的 CS8629 已修正。最终增量编译 0 警告、0 错误；这不表示两条既有警告已被修复。最终 headless 场景退出仍报告 2 个 ObjectDB 实例泄漏警告，未当作已解决或视觉通过。

最终脚本的 final 阶段 15 个子进程均退出 0，但一次外层交互命令误用 PowerShell 的 LASTEXITCODE，随后错误抛出「Final stage failed」，导致该命令未接着调用 guards。保留了包装器失败说明；核对每个 Process 退出码及场景 JSON 后，只另行执行尚未运行的 guards，未重跑或覆盖成功结果。

## 文件与复跑

生产行为仅改 `src/Yukkuri.Game/InteractionRoom.SheltersUi.cs` 三处。其余为诊断接入：InteractionRoom.cs 在建世界前守卫、完成场景后启动诊断；Main.cs 在建世界前拒绝小家诊断参数；新增 InteractionRoom.ShelterUnlockDiagnostic.cs 及其 uid；新增 tools/run-shelter-unlock-audit.ps1。

脚本必填 Stage、TestRoot、Dotnet、Godot、PackageSource。TestRoot 必须是本工程 `.local/testing` 下的独立目录，Stage=final 用于固定版复测，Stage=guards 用于已构建版本的前置保护。Stage=baseline 只用于尚未修复的源码，明确预期诊断退出 1。Attempt 用新后缀追加尝试，沿用同一预算。不得通过更换根目录重新计时来延长本轮。

最终脚本实际验证 final 和 guards 两阶段。baseline/首次导入是在先前脚本版本执行；最终脚本的全新根目录初始化分支没有重置本轮预算重跑，不声称已覆盖。

本机证据根：`.local/testing/shelter-unlock-20261107-01/`。
- `baseline/audit-output/result.json`、checkpoint.json：修复前失败。
- `final-runner-final/audit-output/result.json`：最终脚本和源码的场景结果。
- `final-runner-final/run-*/stdout.log`：四套规则的实际结果。
- `guards-runner-final/processes.json`：最终脚本的六种拒绝情形。
- `extra-guards/result.json`：复用存档、相对输出、重复旗标的额外验证。

Godot 导入产生的 `.import` 改动、其他历史源码的新增 uid、.godot、.tools、.local、bin、obj 均不属于交付源码。仅本诊断自身 uid 应随诊断交付。

关键测试源码 SHA-256：SheltersUi B15795B9950FB10B24FD52164EDFE06CEE93D8A1C4578073EAF4DA06EB41C3F0；Diagnostic A033C9677F2B8F51FF5587D8204914CB816D2AE84D15BF8392F7E98B367D17C9；Main 2C680CF34A46C377838A964D0FC23900054342E080D05234AE675861ADF7F3F0；Runner E9D58031F16B7D909C43CB73064018501116D909E7D16A0D72195D6E582D53FA。

## 独立组合验证

在当前 assembled 基线精确融合本修复后，场景 Shelter 32、Shop drafts 38、Company 31、Selection 24；规则 Economy 10、Shelters 13、Inventory 12、Input 43、State 10、CompanyCare 7，合计 220/220，通过8个非法入口守卫。838个输入文件仅预期3个发生变化，逆变换均恢复原始哈希。经济逻辑未改。

额外3个合成 stale 回调检查通过，属于人工构造状态，不算原生玩法复现。撤回扩展并强制 Rebuild 后，原冻结诊断再次32/32。固定1800秒预算实际用382.527秒，40次进程调用，0超时。

保留一次验证包装器参数命名错误：首个预期 guard 的子进程没有收到诊断参数，仍在全新隔离应用数据目录运行，经自有PID/启动时间核验后手动停止；其目录只有日志、无存档，不计为guard通过。另保留恢复时间戳导致的增量程序集复用，随后强制重建再验证，未将旧程序集结果算作最终证据。

独立报告 SHA-256：9F58ECEDCBE3ED85E4C521B25DDA171E80ACAE80CAC8324FE2DB53EB24555E9D；结构化记录 SHA-256：90993DB240D367856230C6F0B116EAB981D9070330BB677F0BD37B2C5449AF05。原生UI、视觉、长测及历史用户存档仍未测。

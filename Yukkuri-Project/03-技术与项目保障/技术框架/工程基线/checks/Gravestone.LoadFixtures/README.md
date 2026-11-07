# 墓碑读档回归检查

这是显式启用的隔离诊断，不是完整玩法或画面验收。正常启动没有诊断参数时不执行。

## 夹具范围

`Program.cs` 先准备健康、成熟、双向深关系的受控家庭。第四位成人是明确的测试夹具。
随后使用生产代码建立两对伴侣，从同一个已保存的出生前世界分叉，分别请求胎生、完成准备及完整孕期、出生，再调用生产 `Hurt` 和 `BuryCreature`。
两个分支产生相同出生序列 ID、不同母亲衍生名字。没有直接改写孩子的 ID、名字或墓碑状态。满关系夹具不代表自然恋爱全流程。

输出包含 22 项纯规则检查、阶段快照和 `slots/slot-0.json` 至 `slot-3.json`。槽位 1、2 是分叉墓碑，槽位 3 是无墓碑控制。

## 隔离要求

在获准执行测试的独立工作副本中操作。不要使用玩家存档目录。

1. 创建新的测试根目录，设置 `YUKKURI_GRAVE_TEST_ROOT` 为该目录的绝对路径。
2. 每个进程使用根目录下新的 `process-<stage>` 子目录。`APPDATA`、`LOCALAPPDATA`、`TEMP`、`TMP`、`USERPROFILE`、`DOTNET_CLI_HOME` 都指向该子目录中的独立目录，`HOME` 与隔离的 `USERPROFILE` 一致。
3. 指定已安装工具的 `DOTNET_ROOT`，在 PATH 前置同一 dotnet。设置 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`、`DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`、`MSBUILDDISABLENODEREUSE=1`，使用隔离的 NuGet 缓存与获准包源。
4. 从第一次恢复或构建开始固定总截止时间，不因失败重置。每个真实进程最长 300 秒，并受剩余总预算约束。监督进程应记录 PID 和启动时间，用实际 WaitForExit 监督，仅对核实归属的超时进程树结束执行。

本检查不是一键运行器。以下是各阶段入口，调用者仍需提供上述环境和监督机制。

## 阶段入口

从工程根目录构建夹具检查：

`dotnet build checks/Gravestone.LoadFixtures/Gravestone.LoadFixtures.csproj --configfile <isolated-config> -p:UseSharedCompilation=false -nodeReuse:false -maxcpucount:1`

运行已构建的 DLL，将唯一参数设为 `<root>/fixture-attempt-01`。该输出目录必须尚不存在。

`dotnet checks/Gravestone.LoadFixtures/bin/Debug/net10.0/Gravestone.LoadFixtures.dll <root>/fixture-attempt-01`

构建 `src/Yukkuri.Game/Yukkuri.Game.csproj`，使用同样的隔离包源和禁共享编译参数。需要完整重建时使用 `-t:Rebuild`。然后在游戏目录导入资源：

`godot --headless --editor --import --path . --rendering-method gl_compatibility`

运行诊断：

`godot --headless --path . --rendering-method gl_compatibility -- --gravestone-load-audit-output <root>/runtime-<attempt>/result.json --interaction-test-profile grave-load-<attempt>`

`runtime-<attempt>` 必须是测试根目录的直接子目录且尚不存在，存档 profile 也必须全新。profile 后缀仅允许小写字母、数字和连字符，长度 1 至 32。

诊断在创建世界和 SaveStore 前拒绝 Main 场景、非 headless、混入其他用户参数、缺少或重复参数、复用输出目录、复用存档 profile，以及未隔离的环境目录。

## 运行结果

通过条件同时包括进程退出码 0 和 `result.json` 中 `success=true`。保留失败结果、`stage.json`、`checkpoint.json` 和进程证据，不覆盖旧 attempt。

运行时检查通过真实系统槽位按钮的 pressed 信号进入 LoadRoom 和 BuildActors，再调用生产 `_Process(0)`。包括原 11 步对照和额外 11 次 A/B 交替，旧墓碑实际释放、各夹具稳定节点数、位置、标签、近看和区域可见性、存档字节保留及无自动存档。快照立即物化，后续节点释放不会使旧快照失效。

近看控制调用生产切换入口。跨区域只控制显示用活动区域，不编辑个体身份或保存该状态。检查不模拟原生鼠标，不代表画面、音效、手感、长期性能或全量历史存档兼容已验收。
# 测试

[整理验证结果](../project/reorganization-report.md) 对应新目录。[冷藏契约](cold-contract-0.1.60/README.md)、[原测试协议](PROTOCOL.md)、[测试请求](request.json) 和 [历史评审](reviews/) 保留各自的日期、路径和版本。它们提到的根目录 `testing/` 及包路径是历史引用。

当前检查程序位于 [game/checks](../../game/checks/)。要逐项复现，在 PowerShell 7 中从仓库根目录加载环境，再进入 game，分别保存输出和退出码。

```powershell
. ./game/tools/env.ps1
Set-Location game
dotnet run --project checks/Simulation.Smoke/Simulation.Smoke.csproj
```

启动烟测使用独立 profile。界面与建模反馈的 [原标注](问题记录/20261102-界面与建模反馈/建模问题-用户标注.png) 和 [原截图](问题记录/20261102-界面与建模反馈/属性栏拥挤-用户截图.png) 随问题记录保留。

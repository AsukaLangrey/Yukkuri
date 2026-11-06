# 证据与复现

这些证据对应基线 e8d96a78f49182eb17e269e2b62a738c7a9bb7ea 加本切片的未提交工作区，不对应一个已发布提交。机器 authorized test PC；日期2026-11-06 UTC。完整7文件身份见 identity.json。

- baseline.txt：改动前静态源码观察。缺少评分与UI是静态证据，不是旧API运行测试失败。
- Interaction.EnvironmentHygiene-final.txt：最终9组规则输出。
- Interaction.LifeDevelopment-final.txt：13项既有行为回归。
- Simulation.Smoke-final.txt：125项模拟回归。
- game-build-final.txt：移出临时harness后的最终产品编译。
- ui-final.txt：真实场景headless 15项结果。
- HygieneAudit.cs / HygieneAudit.tscn：仅测试用场景，不在产品源码编译范围。

## headless复现

仅在获准运行的独立测试工作区进行，不在原开发机执行。先使用锁定的.NET 10.0.402和Godot 4.7.2 Mono，将DOTNET_ROOT设为已安装SDK根目录，同时把它加入当前进程PATH。

1. 把本目录 HygieneAudit.cs 和 HygieneAudit.tscn 复制到该测试工作区 src/Yukkuri.Game/。
2. 执行 dotnet build src/Yukkuri.Game -c Debug。
3. 执行 godot --headless --path src/Yukkuri.Game --editor --import --quit。
4. 执行 godot --headless --path src/Yukkuri.Game res://HygieneAudit.tscn -- --interaction-test-profile hygiene-review-20261106。
5. 检查退出码0和 HYGIENE_UI_FAILURES: 0。harness只有提供独立profile参数才运行，不能用个人存档做验证。
6. 复核结束后把临时harness移回证据目录或在该可丢弃测试工作区归档，重新编译产品。不要提交Godot自动生成的cache/import/uid。

SDK和Godot使用授权测试机已安装的锁定工具；未复制用户未发布源码。harness通过反射调用现有UI刷新与读取控件，是场景行为/布局自动断言，不是物理鼠标操作或人眼画面验收。

发布说明：上述为测试当时身份，随后收录至独立审阅分支 alice/20261106-environment-hygiene。identity.json 保存测试时工作文件原始字节；Git 按 .gitattributes 规范化换行，远端树与本地提交树逐项核对。无 main 合并。
归档 ui-final.txt 仅移除一处行末空格；未改断言或结果。原始日志保留在本机忽略目录。
公开证据已删除测试机名称、账户和绝对本机路径。结果与相对复现步骤保留；本机原始记录不变。identity.json 为保留的工作文件字节哈希，报告字段随此隐私清理更新。

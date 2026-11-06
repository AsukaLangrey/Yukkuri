# 小院昼夜光照：局部检查通过，视觉未验收

基于已测日历提交 32cda3a4df4126570a78b1f998000f55c725193c。准确源码以 report.json 的六个 SHA256 为准，父协调对话统一提交。

- 仅使用已有 WorldSeconds。四季日出、日落前后各30游戏分钟做平滑过渡；白天暖色日光改变方向，夜间蓝色环境光和固定月光。午夜只重置已熄灭的太阳方向，夜间可见光照连续。
- 不增加存档状态，不改成长/收入/饥饱时速；暂停、近看、系统设置遵循已有停时。主时钟原本按0.25模拟秒更新，本实现不添加新时钟或额外整分钟截断。
- 保留原白天环境光0.30、日光0.34。夜间环境光0.20、月光0.16及调色/角度只是工程调试参数，不是策划已确认数值，仍需人工视觉评审。
- 场景只有3D灯光和环境变化；验证侧栏位于独立CanvasLayer且自身/父控件调制值未变化，不等同于像素验收。资源文字及图标的夜间对比增强仍未实现，本切片不表示整份策划完成。

验证：Debug Rebuild成功，0错误、2条既有可空警告。光照纯检查4组、日历9组、模拟Smoke125项通过；状态边界数量及完整7组场景结果见report.json。场景覆盖四季32个时刻、暂停/近看/设置停时、合成存读档、极端有限时间、恢复推进。独立解析JSON确认passed，非仅凭退出码。

首次场景检查恢复断言失败：20个headless帧不足现有0.25秒模拟tick。测试观察改为0.6秒后重跑通过，未因此修改产品算法；原失败证据在私有本机日志保留。第二次正常退出报告2个ObjectDB实例泄漏，第三次未出现；未作修复，仍列为既有未解决警告。

10万次普通光照采样约46.1ms，分配约19.2MB，即约192字节/采样。世界时间不变时不重新计算。单次微基准不代表帧率或长期性能。

安全保护：缺少测试profile或测试目录含任何已有条目时，在执行场景检查及存档前退出码2。复用已知合成profile被拒绝，slot-3.json前后SHA256完全相同；缺profile也被拒绝且不产出检查结果。新空profile重跑7组场景检查通过。

实际隔离边界：命令行 --user-data-dir 未被验证为有效重定位，合成档实际位于Godot标准用户数据内的 interaction-test-saves/<profile>。未读取或写入玩家 interaction-saves 文件。每次必须使用全新的测试profile，不能复用已有目录。

复现（在已授权测试环境、使用全新空测试profile）：
1. dotnet build src/Yukkuri.Game/Yukkuri.Game.csproj -t:Rebuild
2. dotnet run --project checks/Courtyard.Lighting/Courtyard.Lighting.csproj
3. 分别运行 checks/Courtyard.Calendar、checks/Interaction.StateBoundaries、checks/Simulation.Smoke 的同名csproj。
4. Godot --headless --path src/Yukkuri.Game --editor --import
5. Godot --headless --path src/Yukkuri.Game -- --interaction-test-profile <unique-new-profile> --day-night-check-output <output.json>

未运行：人工完整试玩、画面/原生鼠标效果、长期压力、完整仓库检查清单、私人或历史玩家存档。生成的import/uid、工具、缓存、原始日志和合成存档不纳入提交。
十四项临时组合验证：67/67纯检查与最终Game强制Rebuild通过，399源码文件哈希稳定、来源漂移0。初轮套件后仅测试profile保护harness更新，纯测试依赖未变，最终Game重新构建。组合没有另跑Godot场景，不将独立scene03视为组合场景实测。完整审计见combined-fourteen-audit.json。

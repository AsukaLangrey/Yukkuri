# contact 交接

结论：修补了两类真实入口可达的问题，已补 4 个回归检查块，等待协调者静态复核和远端运行。只做文件读写、源码审查和限定文件 Git 差异检查；没有编译、运行任何测试/游戏/模拟/预览，没有使用 computer-use，没有暂存、提交、推送或改分支。

阅读时 HEAD：`8238c6e6380fd605ec84cc5267f38de875f6d647`。这是阅读基线，新增改动尚未提交，不能用该提交号冒充已经包含本次修复。

## 1. 斜向抽手仍被模拟层算作抚摸

触发：在“摸摸 → 直接接触”工具中按住身体，主要向外、带少量切向分量收手，射线仍短暂命中同一身体表面。例如法线为 +Z，移动方向为归一化 `(0.4,0,1)`。

证据：

- `InteractionCatalog` 仍提供 Touch 工具；当前未提交输入改动已把未装备工具的普通左键改为 Lift，因此这里明确指选中的“直接接触”工具，不把默认空手托举混为 Touch。
- `InteractionRoom.Input.cs` 的 `ProcessRoomGesture` 把真实 `ContactMotion` 的 Distance、Force、TangentFraction、NormalSpeed 传给 Touch。
- 原 `ContactMotion` 正确把这类主要向外移动识别为抽手，清急停历史并给基础力度；但几何切向比例仍约为 0.371，符合真实几何。
- 原 `InteractionGesture.cs` 仅在切向比例小于 0.1 时跳过抚摸。上述抽手仍会走 `ImproveAffection`，置 Applied，并由非零 Motion 保持姿态占用；结束还会记一次抚摸经历。这不是前轮“慢抽手变急停”的重复问题，而是相同输入在采样层与模拟收益层解释不一致。

改动：在 `ContactMotion` 提取内部共享 `IsWithdrawing` 判断，正法向速度且切向比例平方小于 0.5 表示法向外移占主导，等价于原来的法向速度大于切向速度判断。几何比例仍原样返回。Touch 的姿态占用和收益结算都用同一判断；抽手清连续快揉计时，不增加亲密、动作完成数或抚摸记忆。主要沿表面的斜向移动仍能正常抚摸，向内轻抵的原有语义保留。

## 2. 护理、修补停在错误部位仍阻挡自主生活

触发：只有头部局部敏感、其他身体状态正常时，选护理包按在健康的身体侧面；或已取下的饰品仍有破损，选修补却按在身体上。也包括先修补一点，再把工具移到同一角色的身体部位，按钮一直不松。

证据：

- 游戏的 `CanUseContact` 检查装置遮挡，不替护理/修补过滤部位；普通 `ProcessRoomGesture` 把实际命中部位与 `touching=true` 传入模拟。
- 原 Continue 已正确拒绝在错误部位执行护理/修补，但 `InteractionGestureOwnsPose` 只检查尚未完成。因此提示“这里不是伤处”时，仍经 `InteractionGestureBlocksAutonomy` 跳过自主生活。
- 修补已有部分进度时还会保留自身 `accessory-watch` 反应。仅放开占用、却不清自身旧反应，会让自主移动继续带着旧姿态。

改动：未完成护理只有 `CanCareAt` 为真才占用身体；未完成修补只有命中 Accessory 才占用。移动到错误部位时只清与本手势 ReactionSerial 相符的自身旧姿态，保留更新的疼痛或自主反应。该清理明确排除 Applied 的护理与 RepairFinished 的修补，避免擦掉前轮已经修好的完成收尾。没有改消费、护理驻留计时、实际修补速率、完成锁存或一次结算逻辑。

## 修改文件

- `src/Yukkuri.Simulation/ContactMotion.cs`
- `src/Yukkuri.Simulation/InteractionGesture.cs`
- `checks/Input.Intent/Program.cs`
- 本文

`InteractionSensitivity.cs`、`fixtures/contact-traces.json` 无需修改。分配外源码只读；本轮要求保留的托举/放下文件、冻结 art03 与启动器未改。

## 新增检查源码：均未运行

1. `mostly outward diagonal travel neither pets nor owns the body`：使用真实 ContactMotion，30/60/144 fps 的斜向抽手与主要沿表面移动对照。核对抽手不占姿态、不改信任、不伤害、不记 Touch 次数；真实轻抚仍增加信任并只记一次。
2. `care and repair at the wrong site leave autonomous feeding available`：两种工具一直停在错误部位，手势持续存在，按 Continue → 表现 → 世界的真实帧顺序推进。核对能走向食物并进食、不耗护理包、不修饰品。
3. `moving care off a local injury resets dwell without losing completed care`：有效护理半程移开到健康部位，再回来必须重新等完整驻留；完成只消费一份、增加一次照护，之后移到别处不擦除自己的护理收尾。
4. `moving off partial repair releases its pose and switching settles it once`：部分修补移到身体时停工并释放旧姿态；后来独立 Poke 不被错误部位清理抹掉；回到饰品继续进度；切 Touch 后只结算一次部分修补，多次取消不重复记录、也不算完整修好。

旧源码在第 1 块会保留姿态/获得抚摸收益；第 2 块 Begin 即错误占用；第 3 块离开伤处仍占用；第 4 块错误部位仍占用并保留旧反应。没有降低旧断言或改旧案例预期。

## 静态复核与远端待测

已核对模拟入口校验、游戏采样字段传递、帧顺序、自然进食占用门禁及真实菜单入口；限定上述三个源码文件的 `git diff --check` 未报告空白错误。这不代表语法/编译/检查/实机效果通过。

独立 Alice/dot 按 `testing/PROTOCOL.md` 对协调者最终提交运行 Input.Intent、Interaction.Continuity 及相关 NaturalLife 检查，保留原有快移急停、慢抽手、方向变化、静止摩擦、拒绝后释放、完成后释放案例。报告必须记录实际机器、完整 `source_commit`、命令、结果和未测项。

实机待看：选“直接接触”后沿身体边缘斜向收手，与顺表面轻拖对照；局部伤情下护理包放错部位，角色仍能去吃饭；部分修补后移到身体上，角色不被旧姿态拴住；再移回正确位置能继续。涉及已有托举改动的默认左键路径未改，也未进行任何本机或远端运行。

没有额外跨范围补丁需要协调者接管。几何边界的手感和所有新增检查的实际结果尚未确认。

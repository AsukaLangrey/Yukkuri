# 空选择与失效对象的近看边界

## 结论

修复后同版 Godot 场景检查 24/24 通过。未修复基线 9/24 通过、15 个断言失败；这是一组选择边界问题，不是 15 个独立缺陷。相关已有检查 Input.Intent 43/43、Interaction.StateBoundaries 10/10 通过。

最短可达问题：把当前选中的一只油移入收容箱，按 Tab。未修复版本进入没有对象的 CloseUp，剩余油和小院场景隐藏、自然时间停住，保存报“特写缺少对象”。再次按 Tab 可以退出，属于误停时和存档阻断，不是不可恢复死锁。售出所有油后也能触发。已有槽位在失败保存后保留。

这是 SHIKI-5060TI-PC 上 Godot 4.7.2 Mono headless 的真实场景输入分发、按钮信号和存读档验证，非原生鼠标/键盘试玩、非画面或玩法验收，也不是全套回归通过声明。

## 身份与范围

- 基线 Git：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea。
- 独立工作目录：C:\Project\Yukkuri\isolated\alice-selection-boundary-20261106。
- 基线仅增加检查代码和调用入口，没有产品修复。修复侧只增加下述界面边界变更。
- 两侧最终均使用 Debug、`dotnet build --no-restore --no-incremental` 强制重编译。编译均为 0 错误、2 条已有 CS8604 警告（SupplyStationValidation、InteractionValidation）。
- 最终对照使用完全相同的检查源码，SHA256：2F4100648EB91E9268084EFDD657115230226151D8353C6C9A3F07C4D966F6DE。
- 完整源码、检查、DLL 哈希和 UTC 构建/运行时间见 `source-and-artifact-hashes.json`。
- 用户本周明确授权基于策划案开发和测试、仅在分支提交；该新授权覆盖旧 testing/request.json 中 product_code_changes_allowed=false 的当时范围。没有恢复其中的自动计划，没有改 main；修复按独立分支交付审阅。

## 设计依据

先读了 `05-游戏设计/策划案阅读索引.txt`，按其发布边界区分确认规则、提案和未发布本机实现记录。

- `02-基础资源与内容设计/界面管理/交互准则.txt` UI-001 第 3、7 条：保留显式暂停选择，系统存读档正确保留或恢复状态。
- `05-游戏设计/自由模式-时间与四季.txt`：沿用既有暂停和近看停时规则，以 WorldSeconds 为唯一时钟，存读档不按现实时间追赶。
- 本修复只保证既有近看入口和恢复路径合法，不把文档中尚未发布的日历实现或新增数值视作当前代码，也不实现待评审机制。

## 修复

1. ToggleClose 在进入前拒绝无对象、跨区域对象、地上物品和其他非身体选择。按钮已禁用的选择不能再由 Tab 绕过。
2. LeaveUnavailableCloseup 处理缺失对象和已移出当前区域的对象。失效选择清空，冷藏/安置但仍属于当前区域的历史身份保留，显式 Pause 不变。
3. Tab/Esc 可能先于下一帧到达。ToggleClose 先调用同一恢复路径；已经退出则直接返回，不会再次进入 CloseUp。
4. 自动恢复提示改为“当前对象无法近看”，不把缺失对象错误描述为冷藏或安置。

不改变经济数值、重开局工厂、模拟规则、玩家存档格式或真实用户存档。

## 24 项范围

- 正常对象 Tab 进入/退出，近看停时、回到小院继续推进。
- 公共收容入口移走当前油后，空选择 Tab、重复 Tab、时间继续推进和实际 SaveRoom/Load。
- 真实商店确认按钮售出三只油后，空小院 Tab 和存档。
- 选中地上物品时按钮和 Tab 的一致性。
- 显式运行/暂停两种状态下对象消失后的恢复。
- 冷藏、安置历史身份保留。
- 收容箱对象或跨区域对象不能作为当前小院近看身体。
- 下一帧之前 Tab/Esc 到达，分别覆盖缺失 Id、跨区域 Id 四种组合。

场景使用 viewport.PushInput 分发键事件，商店用实际按钮信号，收容用生产公共接口。消失、冷藏/安置、跨区域先后顺序是明确构造的合成边界夹具。测试固定手动调用场景帧推进，不依赖墙钟帧率。每次使用新的 --interaction-test-profile，拒绝已存在槽位；没有复制或上传完整合成存档。

## 最终证据

- `baseline.json`、`baseline-run.txt`：24 项，15 失败，退出码 1。
- `fixed.json`、`fixed-run.txt`：24 项，0 失败，退出码 0。
- `baseline-build.txt`、`fixed-build.txt`：最终强制重建日志。
- `related-regressions.json`、`Input.Intent.json` 及对应文本：已有规则检查。
- 基线 DLL：A65138691B4A23DFAE8375329C5EBD9162A5AA5EE64FAD06EFE8E5E2ED0B70E8。
- 修复 DLL：61867ED7745527BF6C16D60D3EFD49387F96029B80437C5EB3CE78A2BCAB4C52。

## 中间一次旧产物运行

`stale-artifact-build.txt` 保留了一次耗时 0.59 秒的增量构建，随后 `stale-artifact-run.txt` 仍为基线的 9/24。当时修复源码已还原，但复制保留较早时间戳，疑似没有触发重新编译。

随后记录的 DLL SHA（`stale-artifact-sha256.json`）与最终强制重建基线 DLL 完全相同，确认那次运行确实使用了基线产物。不能将其算作修复版本失败或通过。最终两侧都重新强制构建，已排除该产物身份问题。上述“复制时间戳导致增量跳过”是有日志与时间顺序支持的构建原因判断，不是 MSBuild 详细目标跟踪记录。

## 复跑

在工程基线目录，以已安装的 dotnet 和 Godot 4.7.2 Mono：

```powershell
dotnet build src/Yukkuri.Game/Yukkuri.Game.csproj -c Debug --no-restore --no-incremental
$profile = 'selection-review-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$godot = '.tools/godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $godot --headless --path src/Yukkuri.Game -- --interaction-test-profile $profile --selection-check-output .local/testing/selection-review.json
```

新检出目录先使用正式工具链恢复依赖并导入 Godot 资源。这里的输出路径可以替换；测试 profile 必须全新。不能省略隔离 profile，不能使用普通玩家存档。

## 整合文件

只整合以下源码变化和本目录证据，自动生成的 .import/.uid、.godot、bin/obj、.local 不纳入提交：

- src/Yukkuri.Game/InteractionRoom.Ui.cs：仅 ToggleClose 差异，不覆盖已另修的重开局段。
- src/Yukkuri.Game/InteractionRoom.Family.cs：仅 LeaveUnavailableCloseup 差异。
- src/Yukkuri.Game/InteractionRoom.cs：检查入口和一行恢复提示。
- src/Yukkuri.Game/InteractionRoom.SelectionVerification.cs：新检查。

代码已按最终证据冻结。组合回归和发布另行记录，用户自行确认合并。

证据清单 evidence-manifest.json 的哈希与大小对应 Git 中实际交付文件字节。source-and-artifact-hashes.json 记录运行时源码与 DLL 原始字节；Git 的换行规范化可能使源码文本字节不同，但不会改变代码内容。

## 七补丁临时组合回归

独立 detached 测试副本以同一 main 为基线，叠加健康恢复、连续性观察、小屋排泄/休息、水域通行、重开局、存档水域预检和本分支选择边界。未执行 Git 合并。入口调用的相邻上下文手工保留两项场景检查，未覆盖重开局修复。

61/61 个纯检查项目及强制游戏构建退出 0，JSON 结果无失败。重开场景19/19、选择场景24/24通过；382份源码与检查文件运行前后哈希一致。唯一场景警告是重开检查故意注入负余额时的保存拒绝，没有 ObjectDB 泄漏。详见 combined-project-results.json、combined-restart-scene.json、combined-selection-scene.json。合成存读档，不包含个人历史存档或原生画面/手感验收。

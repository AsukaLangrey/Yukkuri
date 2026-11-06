# 离体茎终态运动残留修复

## 结论

针对固定 main 提交 e8d96a78f49182eb17e269e2b62a738c7a9bb7ea 的隔离分支修复，聚焦检查通过。最终提交由协调者处理，本报告不是发布或合并证明。

同一份最终 Interaction.DetachedStem 检查源码，修复前 20 通过、10 失败，修复后 30 通过、0 失败。30 项由原有 17 项和新增 13 项组成；新增项基线为 3 通过、10 失败。另有 123 项相关回归通过。整体验证仍为 partial：没有界面实测、历史个人存档兼容或整套游戏验收。

## 复现与边界

显式合成既有茎生孕期，以及低活性/缺水缺营养起始条件；随后通过真实 CutStem、BottleStem、BeginPortableCarry、ReleasePortableCarry 和 presentation → Advance 帧顺序推进。手持和真实释放后空中失活均可留下 PortableMotions。失活后的原状态校验通过，加载构造时却恢复 HeldByHand=true，造成“离体茎手持状态无效”。暂停 100 帧不修复，下一次未暂停 presentation 会自愈。这不是立即加载崩溃或永久损坏，而是加载产生无效状态，暂停时不能再次保存。

界面源码链：InteractionRoom.SaveRoom 调用 CancelGesture；CancelGesture 调用 ResetRedesignGesture。若还在拖动，后者会 ReleasePortableCarry(cancel:true)，因此不能把手持 API 路径说成日常界面必现。HandleRedesignRelease 在真实释放后清空 _sceneDragId；后续取消不再清除这个空中 motion。该链只经过源码审计与模拟 API 验证，没有实际操作界面。

Release 将 motion.Held 清为 false，但 brood.HeldByHand 仍为 true，表示空中占用。出生条件检查后者。真实成熟手持/空中茎保持 Bottled，落地才出生一次。Born/已清除实体的旧残留 motion 是人为构造的兼容样本，不代表发现真实空中出生。

## 修改

- DetachedStemLife.cs：FailDetachedStem 立即取消该对象的携带/空中 motion。
- PortableMotion.cs：新增加载归一化，先过滤非 Bottled 或已清除茎的残留 motion，再按既有规则恢复健康运动。
- WorldSimulation.cs：构造时调用上述归一化。
- Interaction.DetachedStem/Program.cs：新增 13 个回归场景。

未调整出生、清除、瓶体、子代、库存、金钱或历史结算规则。校验器未放宽，没有 broad catch。各源码 SHA256 见同名 JSON。

## 基线证据来源

最初没有另存产品源码快照。测试完成后仅反向还原本次最小编辑，生成基线重构件；它们不是事前快照。协调者随后使用 git hash-object --path 按仓库 EOL 规则独立核对，确认与固定 main 的三个 blob 完全一致：

- WorldSimulation.cs：c78e2c43cf01fecb860c19f6dc44e52839a6a70a
- PortableMotion.cs：f23b07ca58fee858f06a79988dd41d18b3d28710
- DetachedStemLife.cs：3fed3dc165917a36a74a3f19ee0379f6eadeb36c

最终检查源码 SHA256：c3468e5dd95bcdabc4c49d51c1944c0d07973aad7373e36eb9f19bc4fbec4143。基线与修复后使用同一份最终检查。

## 执行结果

本轮在已授权本地 Windows 测试环境执行，机器标识已脱敏。Debug/net10.0。每项先显式 dotnet build checks/<名称> -t:Rebuild --nologo，再执行 checks/<名称>/bin/Debug/net10.0/<名称>.dll；没有使用 dotnet run -t:Rebuild。设置 DOTNET_GENERATE_ASPNET_CERTIFICATE=false、DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1。

- Interaction.DetachedStem 最终基线：build 0，run 1，20 PASS / 10 FAIL。
- Interaction.DetachedStem 修复后：build 0，run 0，30 PASS / 0 FAIL。
- Interaction.StemCare：build/run 0，12 PASS。
- Family.Reproduction：build/run 0，10 PASS。
- Family.Compatibility：build/run 0，77 PASS。
- Family.PregnancyBoundaries：build/run 0，12 PASS。
- Interaction.Inventory：build/run 0，12 PASS。

新增检查覆盖手持及空中失活即时清理、健康手持及抛出状态读档后正常落地、终态旧数据加载，以及成熟茎落地后只出生一次。8 个合成兼容场景为 Failed/Born × 已清除/未清除 × 暂停/未暂停。修复后使用 8 个独立临时目录，实际调用 SaveStore.Save 32 次、Load 8 次；包括加载后立即重存、100 帧后重存及恢复时间后重存。退出时清除这些合成临时目录。没有读取或修改个人存档。检查瓶体、原历史、子代数量、出生账本、库存及金钱不被意外改变。

所有成功重建均有 2 条既有 CS8604 警告，位置为 SupplyStationValidation.cs:12 和 InteractionValidation.cs:15；构建错误为 0。部分原始重定向日志中文有编码显示问题，ASCII 计数及退出状态可读，原日志不改写。

## 保留的准备过程

- 初次工具路径不存在，构建未执行；随后核实已安装的工具位置再执行。
- baseline-held / baseline-airborne 是诊断 probe，运行退出 0，但输出明确记录无效状态；不得将其视为产品通过。
- regression-baseline 初版检查只有一帧，未等到真正模拟 tick，终态前置断言失败。没有修改产品来消除失败。
- regression-baseline-2 修正等待 tick 后为 19 PASS / 10 FAIL，共 29 项。
- 最终基线加入成熟落地场景，成为 20 PASS / 10 FAIL，共 30 项；此最终源码用于修复前后对比。

原始日志、失败证据留在工程相对目录 .local/testing/stem-terminal-motion-20261106，不纳入提交。全部日志文件名及 SHA256 见同名 JSON；本报告不声称公开读者可取得本地日志。

## 未执行

没有可见 UI 操作或人工玩法验收；没有完整游戏/Godot 构建运行；没有长时间压力/性能测试。历史 Save.Compatibility 需要明确授权的历史样本，本轮未提供，因此未运行，不能用合成样本冒充历史兼容通过。组合分支回归及发布由协调者另行验证。

## 协调组合复核

独立保留的干净main源码重跑同一30项检查，20通过、10失败。将本补丁与前21项在一次性隔离副本组合后，74个纯检查项目全部通过，游戏Debug Rebuild通过；413个最终源码文件前后哈希不变。三个产品文件的最小补丁均能精确反向还原前21项版本，检查只追加。139条递归来源记录无漂移，仍为5处既有警告，无新增。

本分支独立基于main，不依赖其他迭代即可使用。组合验证没有创建合并提交或改变正式分支，不代表单分支完整验收、历史个人存档兼容、人工UI或远端CI通过。结构化审计见terminal-stem-motion-combined22-20261106.json。最终发布身份以本报告所在提交为准。

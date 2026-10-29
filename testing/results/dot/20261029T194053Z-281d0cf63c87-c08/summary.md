# dot 云测试：failed

**完整游戏构建 failed：0警告、3错误。** **33个项目调用中31个进入用例；实际断言918通过、2失败；另2个编译失败，28个计划案例未执行。**构建与纯Simulation检查是不同层次，后者不能抵消游戏编译失败；本轮是否实际运行/观察见下方，不借旧画面或纯规则声称验收。

- 被测提交：`281d0cf63c87adcbe14d5503d67713e430acba3c`；树：`b744d40f68c312910e6dcdecf1d291b2e262a612`。
- 请求：`dot-v15-interaction-depth-I-20261030`；本轮：`20261029T194053Z-281d0cf63c87-c08`。
- 开始UTC：2026-10-29T19:40:53Z；测试终态记录：2026-10-29T19:53:24.598848+00:00；报告所含核验全部结束：2026-10-29T19:56:18.734168+00:00。
- 测试进程实际结束：2026-10-29T19:53:24.598848+00:00；报告时独立LFS哈希核验结束：2026-10-29T19:56:18.734168+00:00。报告finished_at取所有检查结束时刻的最大值，不回写原始测试时间。
- 发布前main与被测提交一致。
- 只在自己的授权云环境执行；未使用原开发机，未改产品或断言。

## 游戏构建错误

- /workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/src/Yukkuri.Game/Main.MilestoneStates.cs(103,56): error CS1061: 'Dictionary<string, int>' does not contain a definition for 'GetValueOrDefault' and no accessible extension method 'GetValueOrDefault' accepting a first argument of type 'Dictionary<string, int>' could be found (are you missing a using directive or an assembly reference?) [/workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/src/Yukkuri.Game/Yukkuri.Game.csproj]
- /workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/src/Yukkuri.Game/Main.MilestoneStates.cs(112,40): error CS1061: 'Dictionary<string, int>' does not contain a definition for 'GetValueOrDefault' and no accessible extension method 'GetValueOrDefault' accepting a first argument of type 'Dictionary<string, int>' could be found (are you missing a using directive or an assembly reference?) [/workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/src/Yukkuri.Game/Yukkuri.Game.csproj]
- /workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/src/Yukkuri.Game/Main.MilestoneStates.cs(115,89): error CS1061: 'Dictionary<string, int>' does not contain a definition for 'GetValueOrDefault' and no accessible extension method 'GetValueOrDefault' accepting a first argument of type 'Dictionary<string, int>' could be found (are you missing a using directive or an assembly reference?) [/workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/src/Yukkuri.Game/Yukkuri.Game.csproj]

只保留本轮实际编译结果，不改源码或重复确定失败。

## 独立检查编译失败

- `Family.PregnancyBoundaries`：编译退出1；源码12个具名案例未执行，实际断言0/0不是通过。
  /workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/checks/Family.PregnancyBoundaries/Program.cs(172,29): error CS0200: Property or indexer 'InteractionLife.Lowering' cannot be assigned to -- it is read only [/workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/checks/Family.PregnancyBoundaries/Family.PregnancyBoundaries.csproj]
- `Family.AssistedCare`：编译退出1；源码16个具名案例未执行，实际断言0/0不是通过。
  /workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/checks/Family.AssistedCare/Program.cs(152,17): error CS0200: Property or indexer 'InteractionLife.Lowering' cannot be assigned to -- it is read only [/workspace/shared/yukkuri-cloud-20261029T194053-281d0cf/source/checks/Family.AssistedCare/Family.AssistedCare.csproj]

上面的编译失败与下方实际运行断言失败分开统计，不把未运行的源码计划案例算成失败或通过。

## 独立规则失败与当前诊断

- `Interaction.Continuity`：an off-contact pressed mouse does not freeze the oil's own movement or keep deforming it。完整错误、诊断及源码位置见 failure-extract / 原始日志。
- `Family.Protection`：保护者远离时不消除危险，实际走近后才能保护。完整错误、诊断及源码位置见 failure-extract / 原始日志。



具体分量仅以当前日志和独立只读诊断为依据；不把c07的旧错误、旧分量或静态假设复制为本轮结论。未经逐帧观测不得升格为产品根因。

失败只按实际运行的顶层结果计数，不按源码中Check字面次数，也不把嵌套组合额外计数。异常后的同一Check剩余语句未执行；源码片段、完整诊断和stack trace保留，未凭旧报告预判根因。


## 本轮检查

| 检查 | 状态 | 通过 / 失败 |
|---|---|---:|
| lfs-integrity | blocked | — |
| game-build | failed | — |
| Simulation.Smoke | passed | 125 / 0 |
| Milestone.Smoke | passed | 63 / 0 |
| Interaction.Smoke | passed | 48 / 0 |
| Input.Intent | passed | 43 / 0 |
| NaturalLife.Smoke | passed | 95 / 0 |
| Interaction.Continuity | failed | 123 / 1 |
| Interaction.AutonomyPriority | passed | 12 / 0 |
| Interaction.CompletedNeeds | passed | 1 / 0 |
| ActorGeometry.Smoke | passed | 93 / 0 |
| NaturalLife.Soak | passed | 24 / 0 |
| Interaction.Mouth | passed | 16 / 0 |
| Family.MouthCommunication | passed | 9 / 0 |
| Interaction.MouthPresentation | passed | 10 / 0 |
| Family.PregnancyBoundaries | failed | 0 / 0 |
| Interaction.EmergencyIntervention | passed | 6 / 0 |
| Save.Compatibility | blocked | — |
| Interaction.Depth | passed | 4 / 0 |
| Interaction.Injury | passed | 15 / 0 |
| Family.Social | passed | 22 / 0 |
| Family.Reproduction | passed | 8 / 0 |
| Family.ColdCare | passed | 9 / 0 |
| Family.Aftermath | passed | 17 / 0 |
| Family.Risk | passed | 14 / 0 |
| Interaction.InjuryPresentation | passed | 6 / 0 |
| Family.Compatibility | passed | 77 / 0 |
| Family.CareLearning | passed | 14 / 0 |
| Family.Teaching | passed | 10 / 0 |
| Family.AdoptionJourney | passed | 6 / 0 |
| Family.AssistedCare | failed | 0 / 0 |
| Family.Growth | passed | 5 / 0 |
| Interaction.StateBoundaries | passed | 10 / 0 |
| Interaction.HumanCompany | passed | 17 / 0 |
| Family.Protection | failed | 13 / 1 |
| Family.Presentation | passed | 3 / 0 |
| runtime-smoke | skipped | — |
| visual-interaction-review | skipped | — |
| asset-import | skipped | — |
| native-observed-interaction | skipped | — |
| engine-contact | skipped | — |
| engine-layout | skipped | — |
| audio-playback | skipped | — |

本轮按各检查实际JSON或stdout顶层PASS/FAIL核验，不数源码字面或把筛选样本当成功率。旧c07及人工复演结果不参与本轮统计。

## 实际画面与范围

当前完整游戏编译失败；导入、启动、原生UI、接触与渲染均依赖跳过。没有当前画面或听感通过记录。

完整visual-interaction-review仍未完成。v2-v15全部未观察场景仍留待实测；声音没有有效监听就不算通过。此轮权限与实际操作只按当前证据陈述，不把旧权限拒绝记作本轮事件。

## 环境、素材与证据

- 407物化文件独立核验，12原始LFS对象按OID/大小匹配；3原始对象本云尚未取得。无新受支持通道，不重复已确定的取件失败；不能写成开发侧未上传。导入状态：skipped，只以本轮实际证据为准。
- 本轮构建警告/错误数见上表和原始日志，不据警告单独断言运行崩溃。
- 引擎阶段因游戏编译失败未运行，没有当前引擎日志或警告结论；不沿用旧轮。
- 无获准历史存档样本；合成保存/恢复和当前隔离档不代替历史兼容。
- 仅Linux环境，不证明Windows；实际音频与完整视觉均按未测范围保留。
- Soak成功快照明确派生精简，失败/错误/结果/种子/度量保留，原始哈希和大小可追溯。

本轮构建失败，未运行图形/截图检查；没有当前图像、渲染或Library图包，不沿用旧轮。

下一步先处理游戏编译错误及本轮独立规则失败，再在新源码/配置触发下复测。H完成动作夹具隔离即使通过，也不是产品吞咽缺陷已修复的证据；PregnancyBoundaries包含内部反射边界隔离，不全部代表普通全流程。不能把旧诊断假设或新增检查存在当修复。补齐受支持的LFS原件传输、历史样本和实际UI/听感；整体验收仍由用户决定。

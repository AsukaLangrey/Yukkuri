# dot 云测试：failed

**完整游戏构建 passed：0警告、0错误。** **33/33个项目已调用，其中33个进入用例；实际顶层检查949通过、1失败；另0个编译失败，0个计划案例因编译阻断未执行。**其中确认测试诊断序列化异常1项，不把未执行的后续产品断言说成失败。构建与纯Simulation检查是不同层次，均不能替代实际画面/听感；本轮是否实际运行/观察见下方，不借旧画面或纯规则声称验收。

- 被测提交：`2da4423f00e2723722611ded8f2108980480c6bd`；树：`973bec65b3c6ce331583aa4f5be45331ccb106bf`。
- 请求：`dot-v16-interaction-depth-J-20261030`；本轮：`20261030T102053Z-2da4423f00e2-c10`。
- 开始UTC：2026-10-30T10:20:53Z；测试终态记录：2026-10-30T10:42:15.175719+00:00；报告所含核验全部结束：2026-10-30T10:52:48.987205+00:00。
- 测试进程实际结束：2026-10-30T10:40:45Z；报告时独立LFS哈希核验结束：2026-10-30T10:43:50.649053+00:00。报告finished_at取所有检查结束时刻的最大值，不回写原始测试时间。
- 发布前main与被测提交一致。
- 只在自己的授权云环境执行；未使用原开发机，未改产品或断言。

## 游戏构建错误

本轮构建未记录编译错误。

只保留本轮实际编译结果，不改源码或重复确定失败。

## c08 对应项在本轮的结果

- `Family.PregnancyBoundaries`：passed，12通过/0失败。
- `Family.AssistedCare`：passed，16通过/0失败。
- `Family.Protection`：passed，15通过/0失败。
- Continuity `an off-contact pressed mouse does not freeze the oil's own movement or keep deforming it`：passed。
- Continuity `a harmful press cancels following but an off-contact held mouse preserves independent movement`：failed。

只认本轮当前日志。J 修订了夹具与对照；通过不等于修改了产品伤害、自主或保护规则，新诊断异常也不能当作产品移动失败。c08 原始失败记录保持不变，c09 只是环境预检。

## 独立检查编译失败

当前未记录独立检查编译失败。

上面的编译失败与下方实际运行顶层检查失败分开统计，不把未运行的源码计划案例算成失败或通过。

## 独立规则失败与当前诊断

- `Interaction.Continuity`：a harmful press cancels following but an off-contact held mouse preserves independent movement。完整错误、诊断及源码位置见 failure-extract / 原始日志。

 当前新增负目标对照在 Program.cs1456–1457 序列化诊断匿名对象时抛异常：Health 与 health 不能同时绑定构造参数 Health。这是测试诊断故障，仍计1个顶层失败，但不是已执行的移动/健康产品断言失败。OFF_CONTACT_INDEPENDENT 未输出，1459–1462 的移动/健康/跟随/手势/姿态断言与1463 EndGesture未执行；此前前置与15帧执行，不据此推断末态通过或产品冻结。

具体分量仅以当前日志和独立只读诊断为依据；不把c08的旧错误、旧分量或静态假设复制为本轮结论。未经逐帧观测不得升格为产品根因。

失败只按实际运行的顶层结果计数，不按源码中Check字面次数，也不把嵌套组合额外计数。异常后的同一Check剩余语句未执行；源码片段、全部已有错误/诊断及日志中的stack trace（如有）保留，未凭旧报告预判根因。


## 本轮检查

| 检查 | 状态 | 通过 / 失败 |
|---|---|---:|
| lfs-integrity | blocked | — |
| game-build | passed | 1 / 0 |
| Simulation.Smoke | passed | 125 / 0 |
| Milestone.Smoke | passed | 63 / 0 |
| Interaction.Smoke | passed | 48 / 0 |
| Input.Intent | passed | 43 / 0 |
| NaturalLife.Smoke | passed | 95 / 0 |
| Interaction.Continuity | failed | 124 / 1 |
| Interaction.AutonomyPriority | passed | 12 / 0 |
| Interaction.CompletedNeeds | passed | 1 / 0 |
| ActorGeometry.Smoke | passed | 93 / 0 |
| NaturalLife.Soak | passed | 24 / 0 |
| Interaction.Mouth | passed | 16 / 0 |
| Family.MouthCommunication | passed | 9 / 0 |
| Interaction.MouthPresentation | passed | 10 / 0 |
| Family.PregnancyBoundaries | passed | 12 / 0 |
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
| Family.AssistedCare | passed | 16 / 0 |
| Family.Growth | passed | 5 / 0 |
| Interaction.StateBoundaries | passed | 10 / 0 |
| Interaction.HumanCompany | passed | 17 / 0 |
| Family.Protection | passed | 15 / 0 |
| Family.Presentation | passed | 3 / 0 |
| runtime-smoke | passed | 1 / 0 |
| visual-interaction-review | not_run | — |
| asset-import | blocked | — |
| native-observed-interaction | passed | — |
| engine-contact | passed | 4 / 0 |
| engine-layout | passed | 10 / 0 |
| audio-playback | blocked | — |

本轮按各检查实际JSON或stdout顶层PASS/FAIL核验，不数源码字面或把筛选样本当成功率。旧c08及人工复演、c09环境预检结果不参与本轮统计。

## 实际画面与范围

实际云桌面观察当前源码游戏启动、近看、饮水（口渴45→17与反馈）、帮助、返回小院、家庭面板关闭后保持暂停；当前窗口正常退出0。此为有限交互观察，不是v2-v16全部画面/手感/声音通过。

完整visual-interaction-review仍未完成。v2-v16全部未观察场景仍留待实测；声音没有有效监听就不算通过。此轮权限与实际操作只按当前证据陈述，不把旧权限拒绝记作本轮事件。

## 环境、素材与证据

- 412物化文件独立核验，12原始LFS对象按OID/大小匹配；3原始对象本云尚未取得。实际恢复/取件见当前审计；不能写成开发侧未上传。导入状态：blocked，只以本轮实际证据为准。
- LFS恢复记录中的旧c03/c06文件仅是合法原件恢复路线/来源依据，不是本轮测试结果。SDK首次初始化自动记录安装开发证书，未读取/导出/使用/信任；详见恢复provenance，不宣称零证书生成。
- 本轮构建警告/错误数见上表和原始日志，不据警告单独断言运行崩溃。
- 本轮engine警告：engine-contact: WARNING: Could not set V-Sync mode, as changing V-Sync mode is not supported by the graphics driver.；engine-render: WARNING: Could not set V-Sync mode, as changing V-Sync mode is not supported by the graphics driver.。退出0及断言通过不表示无警告；未为诊断重跑。
- 无获准历史存档样本；合成保存/恢复和当前隔离档不代替历史兼容。
- 仅Linux环境，不证明Windows；实际音频与完整视觉均按未测范围保留。当前runtime错误/警告见报告runtime_log_diagnostics；ALSA失败回退Dummy不构成试听通过。
- Soak成功快照明确派生精简，失败/错误/结果/种子/度量保留，原始哈希和大小可追溯。

**17张图片及元数据已存私有Library**：`libfile_c28dda8280f08191b3d774e06dbfe49a`，`c10-2da4423-native-and-engine-images.zip`；ZIP SHA256 `ef0a01adcd79b2fee58dc3263839aef3b8ca4c4ae316ba98ea0812bffc853bdf`。逐图哈希见image-manifest；仓库协作者不会自动获权，无公开分享，未返回独立到期时间，不保证永久保留。普通Git不含二进制。

下一步依据本轮实际未通过项处理；本轮是一次明确人工请求，自动计划保持暂停，不据本报告自行追加测试。J 对空按与保护夹具/对照的修订即使通过，也不能称修改了产品伤害、自主或保护规则。H完成动作夹具隔离即使通过，也不是产品吞咽缺陷已修复的证据；PregnancyBoundaries包含内部反射边界隔离，不全部代表普通全流程。不能把旧诊断假设或新增检查存在当修复。补齐受支持的LFS原件传输、历史样本和实际UI/听感；整体验收仍由用户决定。

## 发布文本脱敏

云主机标识以本轮代号 `alice-cloud-c10` 发布，不是真实主机名；省略本地ZIP路径。实际命令与工作目录按协议保留。原始证据与私有Library ZIP没有修改；image-manifest保留原ZIP成员哈希，Git中派生文本另由report.evidence登记新哈希，逐项原始→派生链见publication_redaction。发布核验结束：2026-10-30T10:48:02.023846+00:00。

## 必要证据发布范围

v3省略重复的仓库指令、协议、模板、配置、开发处理记录正文和本地报告组装脚本；固定提交、路径、哈希及不可变源码链接仍在publication_minimization中。实际日志、命令、退出码、计数、失败原文与必要源码片段保留，未修改测试结果或原始证据。发布核验结束：2026-10-30T10:52:48.987205+00:00。

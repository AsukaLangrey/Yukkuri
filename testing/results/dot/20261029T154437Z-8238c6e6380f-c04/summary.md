# dot 云测试：failed

**新增回归检查出现1项失败，不能判整轮通过。** 8套纯 .NET 检查共 **502通过、1失败**。`Interaction.Continuity` 为109/110；其他7套整体通过。当前源码游戏构建通过，0警告、0错误；真实运行与有限UI观察独立列出，不替代完整验收。

- 被测提交：`8238c6e6380fd605ec84cc5267f38de875f6d647`；源码树：`dc50f13c1d4a9ea2da7c48ae828b942eb612fb7c`。
- 请求：`dot-v4-static-polish-20261029`；本轮：`20261029T154437Z-8238c6e6380f-c04`。
- UTC开始：`2026-10-29T15:44:37Z`；测试结束：`2026-10-29T16:12:37Z`。
- **main 已变为 `f02b0fcfbb3e9649b5f94d35201c4c22661158b9`；本报告不覆盖该新提交。**
- 环境是自己的云Linux；未启动用户原开发机程序，未改产品或断言。

## 已确认失败与复现

`checks/Interaction.Continuity/Program.cs:112–150` 的“完成持续按住饭/甜/水后释放自主需求”案例，在 `Drink/water` 子组合的第143行失败：

> Drink/water: holding the finished tool trapped the oil or skipped swallowing

复现命令完整保存在 `evidence/Interaction.Continuity.txt` 和 [report.json](report.json)。默认互动种子20261028；fixture仅保留选中角色，位置/目标为(0,0)，水分20，目的地水源(2,0)、5份，手持水杯(-3,0)、2份。保持接触，每帧0.05秒，共240帧。检查退出1，JSON success=false、failures=1。失败没有重跑。

**根因未确认。** 失败条件是 `accepted && sawSwallow && moved && c.X > .5`；记录没有输出各分量。静态分析提示角色也可能在补水满足后返回原目标，使12秒终点X≤0.5；不能仅凭此错误文字断言产品“卡死/被困”。需要开发侧保留失败后分解观测，区分释放/吞咽问题与终点断言语义。

该结果计为**1个具名检查失败**，不是9个子组合全失败或已全覆盖。抛出异常后，`Drink/water` 后续的自主摄取完成、取消与明确放下断言未执行；下一项 `Drink/rest` 也未进入。固定循环顺序可推得前7子组合已完成，但不额外增加顶层通过计数。

## 本轮检查

| 检查 | 状态 | 通过 / 失败 |
|---|---|---:|
| lfs-integrity | blocked | — |
| game-build | passed | 1 / 0 |
| Simulation.Smoke | passed | 42 / 0 |
| Milestone.Smoke | passed | 63 / 0 |
| Interaction.Smoke | passed | 48 / 0 |
| Input.Intent | passed | 37 / 0 |
| NaturalLife.Smoke | passed | 87 / 0 |
| Interaction.Continuity | failed | 109 / 1 |
| ActorGeometry.Smoke | passed | 92 / 0 |
| NaturalLife.Soak | passed | 24 / 0 |
| Save.Compatibility | blocked | — |
| runtime-smoke | passed | 1 / 0 |
| visual-interaction-review | not_run | — |
| asset-import | blocked | — |
| native-observed-interaction | passed | — |
| engine-contact | passed | 4 / 0 |
| engine-layout | passed | 10 / 0 |
| audio-playback | blocked | — |

自动规则实际先各自构建运行，完整游戏构建随后完成；不改写执行顺序。NaturalLife.Smoke新增4组、连续性新增6组是本提交检查范围；统计只计当前顶层结果，未拿旧c02/c03结果充数。

## 真实运行与观察边界

固定8238c6e真实云桌面运行；一次饮水完成、帮助开关、正常退出实际观察成立。45秒实体杯持有及45秒中键静态持有期间未更新存档，释放后更新；只作有限时序证据，不能冒充完整v4连续视觉验收。

详细操作、时间、预期/实际和复现次数见 `evidence/native-observation.json`。没有被实际观察的v4保存期限、隐藏树/河水命中、最后一份甜甜pending与透明命中、水杯双向复用，以及既有完整动作/信任焦点均不能预填通过。纯逻辑或静态赋值链不能代替实际UI。

## 缺项与证据

**图片证据已保存**：18张原生云桌面截图 + 5张实际引擎PNG及元数据，私有ChatGPT Library：`libfile_c2b9921213d48191aacee517714bf878`，文件 `yukkuri-c04-8238c6e-native-and-render-images.zip`。ZIP SHA256：`7dae125e2b845902da093e9ff68d8f68cf5ba47563211fffba8ecb49102b932f`。普通Git不放图片二进制；逐图哈希见 `evidence/image-manifest.json`，存储身份见 `evidence/render-library.json`。该Library条目仅对有用户Library权限者可读，仓库协作者不会自动获权；没有公开分享，未返回独立到期时间，不保证永久保留。

- 12个原始LFS对象按SHA256和字节数恢复；标题PNG、probe GLB、源.blend同样3项仍缺。素材导入退出0但有明确ERROR，完整性/导入仍blocked。
- 无获准真实历史存档；当前隔离存档和合成案例均不冒充历史兼容，也不上传私人档。
- 声音未有有效可听验收；Linux结果不保证锁定Windows平台兼容。
- 241个物化文件在发布准备阶段独立重核；只有已知导入失败元数据可变，不是完整Git checkout。完整命令、退出码、时间、种子、文件哈希见报告。
- Soak的3600秒是模拟时长；大型成功快照发布为显式派生摘要，保留原始哈希/大小和全部结果、错误、度量、种子。不存在失败删减。

下一步先定位复合断言的失败分量，再由开发侧修复相应代码或纠正测试语义后复测；同时补齐素材、历史样本、音频及未覆盖真实场景。项目整体验收仍由用户决定。

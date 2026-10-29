# dot 云测试：failed

**本轮当前记录 849通过、6失败；不是整体验收通过。** 23套纯规则独立统计；完整游戏先构建，随后规则检查。游戏构建 passed，6警告、0错误；真实运行/有限UI另列，不借纯规则声称家庭、孕育、伤势或冷藏画面已验收。

- 被测提交：`f91146391e2a1629b86530eff5b82fbf0085ec23`；树：`ed1c7f6ad5f8034ae7071da577175bbcc476385e`。
- 请求：`dot-v11-interaction-depth-E-20261030`；本轮：`20261029T183752Z-f91146391e2a-c07`。
- 开始UTC：2026-10-29T18:37:52Z；测试/画面审阅完成：2026-10-29T19:03:19Z；报告所含核验全部结束：2026-10-29T19:05:19.565859+00:00。
- 测试进程实际结束：2026-10-29T19:01:42Z；报告时独立LFS哈希核验结束：2026-10-29T19:05:19.565859+00:00。报告finished_at取所有检查结束时刻的最大值，不回写原始测试时间。
- **main已推进至 `881458c93622444b565efc4a70c30223671835fb`；本报告不覆盖该新提交。**
- 只在自己的授权云环境执行；未使用原开发机，未改产品或断言。

## 失败与当前诊断

- `Interaction.Continuity`：a completed held meal sweet or drink releases needs after its actual swallowing response。完整错误、诊断及源码位置见 failure-extract / 原始日志。
- `Interaction.Continuity`：an autonomous invitation supplies the same body pose used by the renderer。完整错误、诊断及源码位置见 failure-extract / 原始日志。
- `Interaction.Continuity`：voice intent follows the oil's actual response instead of the last selected tool group。完整错误、诊断及源码位置见 failure-extract / 原始日志。
- `Interaction.Continuity`：stored treasure keeps ownership through save and cannot be displayed as present。完整错误、诊断及源码位置见 failure-extract / 原始日志。
- `Interaction.Continuity`：an off-contact pressed mouse does not freeze the oil's own movement or keep deforming it。完整错误、诊断及源码位置见 failure-extract / 原始日志。
- `Family.Protection`：保护者远离时不消除危险，实际走近后才能保护。完整错误、诊断及源码位置见 failure-extract / 原始日志。

Drink/water 分量 accepted=True、sawSwallow=True、moved=True，endX=-0.30000062845401326；sawDestinationIntake=True、returnedAfterDestination=True、peakX=1.2079999999999997、totalTravel=3.1880000000000006。这次记录显示已经吞咽/移动并完成目的地摄取后返回，原复合断言在末态 X>.5 失败；不能再把它解释为未吞咽或完全被困。断言仍失败，新增诊断不是修复。

新增off-contact检查若见本轮错误中的X=-1.08、health上升、pose=idle，只说明正X方向阈值未满足，不能说完全冻结或持续施压。与Family.Protection当前失败的静态解释边界见current-failure-context；未经逐帧观测不得升格为根因。

失败只按实际运行的顶层结果计数，不按源码中Check字面次数，也不把嵌套组合额外计数。异常后的同一Check剩余语句未执行；源码片段、完整诊断和stack trace保留，未凭旧报告预判根因。


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
| Interaction.Continuity | failed | 119 / 5 |
| ActorGeometry.Smoke | passed | 93 / 0 |
| NaturalLife.Soak | passed | 24 / 0 |
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
| Interaction.StateBoundaries | passed | 10 / 0 |
| Interaction.HumanCompany | passed | 17 / 0 |
| Family.Protection | failed | 13 / 1 |
| Family.Presentation | passed | 3 / 0 |
| runtime-smoke | passed | 1 / 0 |
| visual-interaction-review | not_run | — |
| asset-import | blocked | — |
| native-observed-interaction | passed | — |
| engine-contact | passed | 4 / 0 |
| engine-layout | passed | 10 / 0 |
| audio-playback | blocked | — |

当前仅6套按实际JSON核验，其余套件按stdout顶层PASS/FAIL核验；Family.Compatibility亦按实际运行输出计数，不数源码字面或把筛选样本当成功率。旧c06及v05结果不参与本轮统计。

## 实际画面与范围

本轮实际云桌面确认：安慰选中后普通小院里有很小的肤色手模型和投影，暂停及右键收手后消失；此前桌面截图的光标暗晕遮挡造成难辨，窗口级截图已排除‘手未显示’疑点，不列产品失败。家庭面板的‘关闭面板’保留近看与原暂停；特殊干预区默认收起。现有幼体豆豆与长辈判断/教导按钮可见；真实点击讲述树叶后因双方不愿安静相处被拒，未声称完成教学。游戏正常退出0。完整陪伴关系收益、全流程家庭/出生/伤势/声音等仍未验收。

完整visual-interaction-review仍未完成。v2-v11全部未观察场景仍留待实测；声音没有有效监听就不算通过。此轮权限与实际操作只按当前证据陈述，不把旧权限拒绝记作本轮事件。

## 环境、素材与证据

- 362物化文件独立核验，12原始LFS对象按OID/大小匹配；3原始对象本云尚未取得。无新受支持通道，不重复已确定的取件失败；不能写成开发侧未上传。完整导入状态按本轮退出码与ERROR日志记录。
- 本轮构建警告/错误数见上表和原始日志，不据警告单独断言运行崩溃。
- 本轮engine警告：engine-contact: WARNING: Could not set V-Sync mode, as changing V-Sync mode is not supported by the graphics driver.；engine-contact: WARNING: 2 ObjectDB instances were leaked at exit (run with `--verbose` for details).；engine-render: WARNING: Could not set V-Sync mode, as changing V-Sync mode is not supported by the graphics driver.。退出0及断言通过不表示无警告；未为诊断重跑。
- 无获准历史存档样本；合成保存/恢复和当前隔离档不代替历史兼容。
- 仅Linux环境，不证明Windows；实际音频与完整视觉均按未测范围保留。
- Soak成功快照明确派生精简，失败/错误/结果/种子/度量保留，原始哈希和大小可追溯。

**28张图片及元数据已存私有Library**：`libfile_8087d06fc7d4819198cb7b84108fc0ef`，`Yukkuri-c07-f911463-actual-ui-evidence.zip`；ZIP SHA256 `1129586e19cc88537a5aa90c8a76613e25323ac77f613c312f404c85a247ef3b`。逐图哈希见image-manifest；仓库协作者不会自动获权，无公开分享，未返回独立到期时间，不保证永久保留。普通Git不含二进制。

下一步按本轮实际失败与分量分别定位，不能把旧诊断假设或新增检查存在当修复。补齐受支持的LFS原件传输、历史样本和实际UI/听感；整体验收仍由用户决定。

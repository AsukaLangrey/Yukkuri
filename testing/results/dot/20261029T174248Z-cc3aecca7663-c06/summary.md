# dot 云测试：failed

**本轮当前记录 669通过、4失败；不是整体验收通过。** 13套纯规则独立统计；完整游戏先构建，随后规则检查。游戏构建 passed，6警告、0错误；真实运行/有限UI另列，不借纯规则声称家庭、孕育、伤势或冷藏画面已验收。

- 被测提交：`cc3aecca7663d10d37f5b5e2a1a1f1291f18f75d`；树：`79c54b3c949fd772bf11a2fa13317ff7c81b4e39`。
- 请求：`dot-v8-interaction-depth-B-20261030`；本轮：`20261029T174248Z-cc3aecca7663-c06`。
- 开始UTC：2026-10-29T17:42:48Z；测试/画面审阅完成：2026-10-29T18:04:22Z；报告所含核验全部结束：2026-10-29T18:09:36.952759+00:00。
- 测试进程实际结束：2026-10-29T18:03:08Z；报告时独立LFS哈希核验结束：2026-10-29T18:09:36.952759+00:00。报告finished_at取所有检查结束时刻的最大值，不回写原始测试时间。
- **main已推进至 `6ae81c9a45bac2cf003132f16bfd6984b6a4a8df`；本报告不覆盖该新提交。**
- 只在自己的授权云环境执行；未使用原开发机，未改产品或断言。

## 失败与当前诊断

- `Interaction.Continuity`：a completed held meal sweet or drink releases needs after its actual swallowing response。完整错误、诊断及源码位置见 failure-extract / 原始日志。
- `Interaction.Continuity`：an autonomous invitation supplies the same body pose used by the renderer。完整错误、诊断及源码位置见 failure-extract / 原始日志。
- `Interaction.Continuity`：voice intent follows the oil's actual response instead of the last selected tool group。完整错误、诊断及源码位置见 failure-extract / 原始日志。
- `Interaction.Continuity`：stored treasure keeps ownership through save and cannot be displayed as present。完整错误、诊断及源码位置见 failure-extract / 原始日志。

Drink/water 分量 accepted=True、sawSwallow=True、moved=True，endX=-0.30000062845401326；sawDestinationIntake=True、returnedAfterDestination=True、peakX=1.2079999999999997、totalTravel=3.1880000000000006。这次记录显示已经吞咽/移动并完成目的地摄取后返回，原复合断言在末态 X>.5 失败；不能再把它解释为未吞咽或完全被困。断言仍失败，新增诊断不是修复。

原Drink/water具名检查只算1项；抛出后其后续自然摄取、取消/放下断言未执行，Drink/rest也未进入。前7子组合由顺序推得完成，不增加顶层计数。另外3项分别在第610、665、1312行失败。前两项后续暂停/近看或拒绝/受伤声音断言未执行；宝物是所有权/收纳与反应文案的复合断言，不能据错误文本声称丢失所有权。静态疑点是新家庭活动优先级消耗短测试窗口，但没有逐帧证据，不能定为根因。所有失败不重跑，完整命令、退出码、时间、种子表达式、失败源片段与全文在evidence中。

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
| Interaction.Continuity | failed | 120 / 4 |
| ActorGeometry.Smoke | passed | 93 / 0 |
| NaturalLife.Soak | passed | 24 / 0 |
| Save.Compatibility | blocked | — |
| Interaction.Depth | passed | 4 / 0 |
| Interaction.Injury | passed | 15 / 0 |
| Family.Social | passed | 22 / 0 |
| Family.Reproduction | passed | 8 / 0 |
| Family.ColdCare | passed | 9 / 0 |
| runtime-smoke | passed | 1 / 0 |
| visual-interaction-review | not_run | — |
| asset-import | blocked | — |
| native-observed-interaction | passed | — |
| engine-contact | passed | 4 / 0 |
| engine-layout | passed | 10 / 0 |
| audio-playback | blocked | — |

Interaction.Depth、Interaction.Injury、Family.Social、Family.Reproduction、Family.ColdCare只输出PASS/FAIL，按当前stdout计数；没有虚构JSON。旧c04的502/1不参与本轮统计，v05不作为完整基线。

## 实际画面与范围

本轮在 Alice 自己云桌面实际操作 cc3aecc：家庭面板可读；默认成年角色孕育/邀请不满足条件时显示拒绝；面板保留原暂停且暂停时不安排活动；小铃短时入冷藏后隐藏，近看被拒，取回后恢复显示并禁止恢复期立即再冷藏；近看实际饮水完成，口渴 45→17/100；选择现有幼体豆豆显示幼体标签及较小体型；帮助打开、Esc 关闭、Tab 返回小院。随后正常关窗，退出码0。未把这些有限观察或纯规则当作完整 v8 验收。

完整visual-interaction-review仍未完成。v2-v8全部未观察场景仍留待实测；声音没有有效监听就不算通过。初始窗口读取被审查拒绝及后续同动作获准记录均保留，不隐瞒或绕过。

## 环境、素材与证据

- 317物化文件独立核验，12原始LFS对象按OID/大小匹配；3原始对象本云尚未取得。开发侧记录已推送，本云授权连接器返回指针的限制不能写成开发侧未上传。完整导入虽退出0，但PNG/GLB明确ERROR，仍blocked。
- 六处FamilyValidation.cs的CS8602为构建警告，不是零警告，也未据此断言运行崩溃。
- 两个engine检查均记录V-Sync不支持，以及各2个ObjectDB实例在退出时泄漏警告；退出0和接触/布局断言通过不等于无警告，未为诊断重复运行。
- 无获准历史存档样本；合成保存/恢复和当前隔离档不代替历史兼容。
- 仅Linux环境，不证明Windows；实际音频与完整视觉均按未测范围保留。
- Soak成功快照明确派生精简，失败/错误/结果/种子/度量保留，原始哈希和大小可追溯。

**32张图片及元数据已存私有Library**：`libfile_6a67eb948128819192bc778da9e8ff51`，`Yukkuri-c06-cc3aecc-actual-ui-evidence.zip`；ZIP SHA256 `2642e6df2d007975bb8f91f84a3eff38d88cb5236206e483314db19fc5884af7`。逐图哈希见image-manifest；仓库协作者不会自动获权，无公开分享，未返回独立到期时间，不保证永久保留。普通Git不含二进制。

下一步按当前失败分量分别定位，不能把诊断补充当修复。补齐受支持的LFS原件传输、历史样本和实际UI/听感；整体验收仍由用户决定。

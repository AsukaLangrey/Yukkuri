# save 第二轮交接

最新结论：初审的保存/恢复拒绝与保全路径未发现确定缺陷；追加交叉复核确认自动排泄物 GUID 破坏同档确定续接。作者已限范围修复，本聊天静态复核并按协调者追加授权补齐完整 JSON 双分支、避重与饱和边界检查。全部仅源码工作，未编译、未执行检查、未运行游戏、未操作桌面。

基线为 `f02b0fcfbb3e9649b5f94d35201c4c22661158b9` 加共享未提交生理功能；不把其他聊天旧编译记录当作本轮验证。没有暂存、提交、推送、切分支或清理文件。

## 本聊天改动归属

- 修改 `checks/Simulation.Smoke/Program.cs`：初审新增存储边界检查，追加自主废物确定续接检查。
- 新增本交接。
- `SaveStore.cs` 没有再修改，上一轮修复仍在基线内。
- `InteractionValidation.cs` 接手时已有的 5 行生理校验完整保留，本聊天没有改写；整合时应计入原功能作者的变更。
- `InteractionPhysiology.cs`、Interaction.Continuity 及其他分工文件只读，没有编辑。

## 实际调用与异常边界

`SaveStore.Save` 先 Validate 当前候选，再 Read 旧主档，再写临时档、Read 临时档，最后 Replace/Move。Read 先反序列化再 Validate；Validate 的尾部确实调用 `InteractionValidation.Validate`。默认互动小院设置 InteractionPlaytest=true，故新增校验不是未调用的孤立代码。

反序列化期间，Physiology 的数值和枚举 setter 抛 JsonException，显式 null 由 InteractionLife.Physiology setter 拒绝。对象完整读完后，IJsonOnDeserialized 检查 Excreting 与 Remaining 配对。它们都在 SaveStore 已有 JsonException 回退范围内；检查现有主档失败时则包装为受控 InvalidDataException，阻止好备份被替换。

只靠反序列化回调不足以保护内存状态：内存中可以先设 Excreting=Urine 而不设 Remaining。现有新增 InteractionValidation 能在 Save 开头拒绝这个组合。本轮新增案例专门检查这一入口，避免将来只保留回调而误删保存前检查。

## 缺省、非法字段与序列化

- 完全缺少 Physiology 时保留默认对象；对象存在但字段全缺少时，默认 Bladder=35、Bowel=20、None、Remaining=0、次数=0。没有凭空创建进行中的排泄。
- 显式 null 不等价于缺字段。非法有限数值、枚举、负次数、超过 int 的次数和动作时间不匹配都应拒绝。
- Physiology.Active 与 Creature.HealthStatus 标有 JsonIgnore；实际 Health、Condition、计时、需要程度和次数仍保存。新物品种类追加在 RoomObjectKind 末尾，没有改变已有种类的数值。
- 新检查使用真正的 JSON 删除或替换字段，通过 SaveStore.Load 读磁盘文件；不只是调用 setter 后声称覆盖了读档。

## 数量边界、清理与资源

静态依据为 InteractionPhysiology.AddRoomWaste：同区域同种类非 Stored 排泄物达到 64 后只合并到最近的同类，Portions 截至 1000；次数采用 int.MaxValue 饱和递增，不先溢出。当前默认生成路径不会因第 65 次而删掉其他对象，也不会生成超出保存校验份数上限的对象。

这里没有 RoomObjects 的全局硬数量限制；现有 24 份供给限制只针对 Meal/Sweet/Water，不包括排泄物。未添加仅在读档时生效的总数限制，避免把多区域有效档或既有对象多的档案突然判坏。当前量限制是每区域每种，不能对外说成所有场景总共最多 128 个对象。

Stored 排泄物若由人工构造存档进入，会被当前生成计数排除；默认持物/收纳入口不能把排泄物变成 Stored，因此本轮不把它报成真实无限增长缺陷，也不拒绝此类既有文件。若未来新增排泄物储存或动态区域生成功能，需同步重新定义上限。

默认 Del 入口先按 IsWaste 分流到 CleanRoomWaste；后者只删除当前区域可见排泄物，没有增加 Food/Money 或调用预留食水退款。IsFood/IsWater 的白名单也不包括新排泄物种类。没有找到“清理排泄物返还资源”的默认可达路径。

## 新增检查源码（均未运行）

1. 内存构造 Excreting=Urine、Remaining=0：Validate、保存前检查、坏主档回退、禁止覆盖好备份以及两个文件逐字保留。
2. 八种原始 JSON 损坏：null；负膀胱量；肠道量超过 100；计时超过 4；枚举 99；负次数；次数超过 int；只剩计时却没有排泄种类。每项通过真实 Load 检查有效备份回退，再检查坏主档不能覆盖备份及两份原文件未变。
3. 真实删除 Physiology，及替换为空对象：两种都须直接读取主档、使用精确默认值、不触发备份回退。只是合成缺省案例，不是历史样本兼容结果。
4. 满边界档：当前正排便、剩余 1.25 秒、尿便次数均为 int.MaxValue、每种 64 个排泄物、每个 Portions=1000；保存读回后整个世界序列化内容应相同。
5. 对上述读回档关闭无限资源后清理 128 个排泄物：Food 不增加，所有其他物品逐字保持，清理后仍能通过 Validate。关闭无限资源使错误退款无法被默认沙盒掩盖。

旧的锁定 .writing 写失败检查和非法候选保全两代文件检查保留，没有降低断言。本轮未再复制一份同样的锁检查。没有修改现有替换算法、临时档策略或回退提示。

## 远程待测及限制

- Alice/dot 按协调者最终完整源码提交编译并运行 Simulation.Smoke，逐项报告；本聊天没有执行任何检查。
- 实际排泄推进中的自动/手动保存、读回后完成一次、进食/托举/下放中断，以及生成第 65 个时合并不增长，应由 physiology/contact/life 的最终集成版本继续验证。本轮满边界检查覆盖保存和清理，不冒充已经执行“第 65 次生成”。
- 远端长时观察每区域两种排泄物接近上限时保存仍成功，读取后没有丢失无关物品、资源退款或次数溢出。当前源码饱和合并后超出 1000 的累计量不会继续精确记录，属于现有份数上限行为。
- 真实历史存档仍需获准样本；缺样本继续 blocked。文件系统权限、磁盘满、断电恢复和画面效果本轮未验证。

未向其他聊天或外部人员发消息；由协调者读取本文件。初审后的追加缺陷与修补状态如下。

## 追加授权：自主废物 ID 确定续接

主协调提出候选后，本聊天独立确认 AddRoomWaste 未指定 Id、默认 Guid.NewGuid 与保存随机状态无关。这是新增被动模拟路径的完整状态分歧，范围不扩大到所有手动创建物品。具体证据链与原/新整合判断见 physiology-review.md 的 P2。

作者稳定方案已复读：已保存的个体 Id、种类和 long 扩宽后的次数加一组成候选，全 RoomObjects 做 Ordinal 避重、固定顺序加后缀，Invariant 数值格式；不新增 schema、不改旧物体、不消耗 RandomState。作者产品哈希为 `117D9BC4117BDFE0B8DB1FB479F026025E3C58FDCF40CA4B64DC1C761D685113`。

本聊天只追加 Simulation.Smoke：尿/便 × 普通/int.MaxValue，四种组合各连续两轮实际 Save→双 Load→相同表现/世界推进。每轮比较完整 JSON，检查新增一件、所有现存 Id 唯一、计数恰好递增或饱和，以及既有对象原样保留。预先以异区 Stored 宝物占用候选、以软垫占用首个后缀，第二轮饱和计数继续检验不会复用仍存在的对象编号。只用候选格式布置冲突，不把生成结果必须等于某个格式作为主要断言。

另覆盖尿/便两种满上限分支：每种 64 件、份数 1000、计数 int.MaxValue 时临近完成的同档双分支，完整 JSON 相同，物品列表与推进前逐字相同，次数不溢出，动作完成且两份世界仍可 Validate。这覆盖实际“第 65 次合并”入口的检查源码，尚未执行。

检查文件最终 SHA-256：`FE7A75407DC7BF14D11D20E60D59004041F4CDF30C093EF609ABDD532242A594`。产品和检查均可交协调者整合静态复核；没有将任何新增断言标为通过。后续远程运行应使用最终完整源码提交，原 GUID 版本应在未满 64 时的完整 JSON 一致性断言暴露差异。

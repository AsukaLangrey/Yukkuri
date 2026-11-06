# 环境卫生：区域派生评分切片

## 范围与依据

依据 `05-游戏设计/自由模式-环境卫生与排泄.txt` 的 2026-11-02 用户第5—7项要求。本切片只实现区域评分和真实界面反馈，不代表完整卫生专题已完成。

从现存 RoomObjects 实时计算：100 减去每份粪便8、尿液5，最低0。Portions 为实际份数，0份不计；不同 RegionId 独立。Stored、HeldByHand、CarrierId 非空、Height 非0，以及仍手持/离地/有垂直速度的 PortableMotion 不计地面。没有新高度阈值。纯查询不删除污物、不改世界、不保存缓存。

已有 PortableMotion 落地流程会清除 HeldByHand 并移除运动记录；只有接地且不手持、不运动的残留记录不排除。现有加载构造会恢复在途占用。只有瞬态 HeldByHand、没有运动记录的对象，加载后按既有行为回到地面，重新计分，不改存档语义。

右侧现有滚动属性区始终显示当前区域评分，有/无选中均刷新。悬停/普通点选通过现有左侧反馈显示名称、份数和每份扣分；观察模式通过原有右侧对象详情显示相同规则。文案说明每份扣分与0分下限，不谎称清理一堆一定能回升其全部名义扣分。

未实现：个体卫生要求、善意/教育/牌照曲线、情绪惩罚、新自主清洁触发、屋内排泄、共享小屋和世界时钟改造。没有引入弹窗或布局重构。

## 验证

机器：authorized test PC。基线：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea，加本切片工作区改动。2026-11-06 UTC。

- Interaction.EnvironmentHygiene：9组通过。覆盖精确份数、合并/拆分等价、区域隔离、0份、0分下限、乘法溢出防护、收纳/手持/搬运/离地、运动与加载、真实抛落恢复、真实清理、暂停查询、无存档评分缓存、旧瞬态手持加载、左右栏文本来源。
- Interaction.LifeDevelopment：13项通过，既有主动清理/劳动记录回归。
- Simulation.Smoke：125项通过，含旧污物/份数/排泄续接与存档回归。
- Yukkuri.Game Debug：最终编译通过，0错误。干净依赖编译期间仍有既有2条 CS8604，最终增量编译0警告。
- Godot 4.7.2 headless 真实 InteractionRoom 场景：15项通过。选中/无选中评分可见、右栏尺寸、暂停刷新、观察详情、悬停反馈、1000份最长反馈左栏容纳且忽略鼠标、真实清理恢复、切区域更新。请求窗口1440×900和1920×1080；headless逻辑布局均为1440×900，不能当作桌面缩放或人眼渲染验收。
- git diff --check 无空白错误。

新检查首次1项失败为夹具没有清理加载恢复的 HeldByHand；已修夹具模拟真实落地状态，保留首次日志。首次 Godot 导入因未设置 DOTNET_ROOT 缺 hostfxr，结束该次运行后设置授权SDK路径重试成功。首轮UI退出有2个 ObjectDB泄漏警告，最终轮未出现。

本机原始证据与临时 harness 源码在工程 `.local/testing/environment-hygiene/`，不提交工具缓存与自动生成 uid/import。最终源码已移除临时场景 harness 并重新编译。测试只使用 `--interaction-test-profile hygiene-20261106`，不读取用户存档。

## 复核命令

在授权测试机器，使用锁定 .NET 10.0.402：

- dotnet run --project checks/Interaction.EnvironmentHygiene -c Debug
- dotnet run --project checks/Interaction.LifeDevelopment -c Debug
- dotnet run --project checks/Simulation.Smoke -c Debug
- dotnet build src/Yukkuri.Game -c Debug

手工复核：干净区域显示100；加入3份粪便显示76，观察/悬停显示3份、每份扣8；清理后立刻100。切换至2份尿液的另一区域显示90。无选中时仍显示当前区域分数。

未运行全量 testing/request.json、长期压力测试、人眼画面/真实鼠标操作验收与个人历史存档兼容。最终整体验收由用户决定。

## 随分支保存的审阅证据

[复现说明与实际执行身份](environment-hygiene-20261106/README.md)、[7文件SHA256身份清单](environment-hygiene-20261106/identity.json)。

[改动前静态证据](environment-hygiene-20261106/baseline.txt)只证明源码未实现评分/反馈，不是旧API测试失败。

[最终9组规则输出](environment-hygiene-20261106/Interaction.EnvironmentHygiene-final.txt)、[13项行为回归](environment-hygiene-20261106/Interaction.LifeDevelopment-final.txt)、[125项模拟回归](environment-hygiene-20261106/Simulation.Smoke-final.txt)、[最终产品编译](environment-hygiene-20261106/game-build-final.txt)。

[15项headless场景结果](environment-hygiene-20261106/ui-final.txt)、[测试harness源码](environment-hygiene-20261106/HygieneAudit.cs)、[测试场景](environment-hygiene-20261106/HygieneAudit.tscn)。harness保存在非生产路径，默认不参与游戏编译。
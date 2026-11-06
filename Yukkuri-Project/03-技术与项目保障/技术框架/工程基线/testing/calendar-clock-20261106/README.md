# 小院日历投影与现有时钟标签

结论：最小切片完成。9组日历检查、125项 Simulation.Smoke、10项 Interaction.StateBoundaries 和48例真实场景 headless 标签检查通过；移出临时检查代码后强制 Rebuild 通过。不是光照实现、完整人工试玩或长期性能验收。

## 版本与范围

基线 main：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea（本轮 fetch 后核实）。独立分支 alice/20261106-courtyard-calendar。未暂存、提交、推送或合并；没有读取原工作区未发布实现作为源码。
正式产品与检查仅4文件，以 source-manifest.json 为白名单及原始字节SHA256。此目录是证据白名单；导入生成的 .import/.uid 和全部本机缓存、测试存档不属于交付。

基线缺口：基线不存在 src/Yukkuri.Simulation/CourtyardCalendar.cs 或 checks/Courtyard.Calendar。InteractionRoom.Ui.cs 原213行：

    _clockLabel.Text=Sim.State.Settings.Pause?"小院暂停中":Sim.State.Mode==PlayMode.CloseUp?"近看 · 自然时间暂停":"小院时间  "+TimeSpan.FromSeconds(Sim.State.WorldSeconds).ToString(@"mm\:ss");

这是源码基线证据，不是对旧版画面的实测。当前云端源码的旧顶部栏仍在；本切片保留其位置、190最小宽、按钮与布局，只替换此文本接线，没有搬入文档所述透明顶部组件。

## 规则

依据已确认的《自由模式-时间与四季》及当前阅读索引。唯一输入仍是 WorldSeconds：600秒/日、0秒=第1年春1日08:00、7日/季、28日/年。投影只读，无新增存档字段，不改模拟推进、成长、饥饱、繁育、收入或离线时间。

春秋06:00/22:00，夏05:30/22:30，冬06:30/21:30，日出含端点、日落开始夜间。界面只称“日间/夜间”。日出日落前后半小时的过渡属于未来光照切片，此次未实现渐变、晨昏阶段、灯光、睡眠、自主住宿、天气或季节美术。

暂停/近看继续显示日期时刻并附原提示；二者同时存在仍优先暂停提示，与原逻辑一致。全局 WorldSimulation.FormatClock 日志格式不变。

SaveStore 原来接受任何有限非负 double，未修改该校验。纯API拒绝负数、NaN、无穷，负零正常。用输入double的精确二进制数值和BigInteger整数除法向下取分钟，避免浮点邻界提前进位以及极大时间溢出。Year精确；超过4位的年份只在显示时以带“约”的截断科学计数法缩写，非新的游戏数值规则。

## 验证与证据

运行环境：授权Windows测试电脑，.NET 10.0.402 / Godot 4.7.2，Debug；只用生成的独立测试数据。日期为2026-11-06 UTC。

- dotnet build src/Yukkuri.Game -t:Rebuild -v minimal：退出0，0错误、两条既有CS8604警告（SupplyStationValidation、InteractionValidation）。临时harness已移出，InteractionRoom.cs与基线字节一致。见 build.txt。
- dotnet run --project checks/Courtyard.Calendar：退出0，9组通过。见 calendar-checks.txt。覆盖0/400/4000/16600、四年逐日边界、相邻double、微小分数、四季日出日落、负零/subnormal、整个存档有效数值范围、状态/RNG不变、暂停/近看、存读档及原模拟秒推进。
- dotnet run --project checks/Simulation.Smoke：退出0，125项通过，含原FormatClock、近看冻结/无追赶、完整存档/RNG连续性。见 simulation-smoke.txt。基线没有单列 Time 项目，以此实际时间回归覆盖，不声称执行不存在的项目。
- dotnet run --project checks/Interaction.StateBoundaries：退出0，10项通过。见 state-boundaries.txt。
- 实际Godot场景：headless退出0，headless-ui.json success=true，48例；分别请求窗口尺寸1440×900与1600×1000，8个时间样本各运行/暂停/近看。JSON rows.size记录的是请求变量，不是读取到的实际窗口或viewport尺寸。project.godot配置逻辑viewport为1440×900，stretch为canvas_items/keep；两批对应案例的Label bounds相同。harness未记录实际逻辑viewport或物理窗口尺寸，因此这些结果只证明所记录的headless逻辑控件几何，不能称为两种实际布局或物理缩放验证。每例断言真实Label精确文字、两行、原顶部栏包含、与所有兄弟控件无重叠、未阻挡输入，RefreshRoomUi前后完整世界序列化一致。样本包含9999年最长普通年份、缩写年份与double.MaxValue。独立测试profile实际SaveStore保存并重载16600.25，日期保持。见 headless-ui.json、headless-runtime.txt。

临时harness源码保留为 UiHarness.cs.txt，未编入产品。复现须在独立副本复制为 src/Yukkuri.Game/InteractionRoom.CalendarTemporary.cs，并仅在 _Ready 的 StartMarketVerificationIfRequested(args) 后调用 _=VerifyCalendarTemporary(args)。编译和资源导入后，以 --headless --path <GAME> -- --interaction-test-profile <NEW_TEST_PROFILE> --calendar-test-output <TEST_OUTPUT_JSON> 启动。必须新建测试profile，不用玩家存档。完成后移出harness、恢复调用文件并强制Rebuild。记录仅证明headless控件树和几何，不证明像素外观或鼠标手感。

运行退出时有“2 ObjectDB instances were leaked”警告。检查结果成功，但此退出警告未定位来源，不把它解释为零泄漏或视觉验收。原始失败/警告与本机证据保留；公开文本已移除机器名、账户名和绝对路径。

## 单次诊断

暖进程Debug，每类100000次“纯投影+日期阶段字符串”：正常时间234.119ms、448B/次；double.MaxValue 557.643ms、约2311.115B/次。见 performance-diagnostic.txt。这是单次CPU工作循环的墙钟与线程分配量，不是FPS、门槛、长期压力测试或优化成果。未据此添加缓存或改变规则。

## 发布边界

合并到其他功能分支时，只应用 Ui.cs 中那一处时钟赋值差异，不能用整个文件覆盖其他人的卫生/选择改动。4正式文件与此目录作为已验证审阅分支交付；全部 .import/.uid、缓存、工具连接与临时测试数据排除。
最终十三项临时组合：66/66纯检查及强制Game.Build通过，394源码/检查文件测试前后不变，来源漂移0，新增警告0；详见combined-thirteen-audit.json。仅组合副本使用此前全部修复，本独立分支仍以main为父，不含其他独立补丁。未合并main；Preview/Renderer与缺历史样本的Save.Compatibility未执行。

发布后以本地提交树与远端树一致性核对；source-manifest.json为测试时原始工作文件字节，evidence-sha256.json为Git规范化后的提交证据文件字节（不含其自身）。原始本机记录不变。

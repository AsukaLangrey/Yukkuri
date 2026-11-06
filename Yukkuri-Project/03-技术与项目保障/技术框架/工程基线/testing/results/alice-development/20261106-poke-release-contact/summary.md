# Poke 松手接触修复

结论：普通小院中，按下身体后在空处或另一只身上松手，旧逻辑仍伤害原对象。最小修复现已通过 8 项真实 Godot 几何与输入回调检查、43 项 Input.Intent 和 10 项 Interaction.StateBoundaries。3 项启动保护正确拒绝。总体为 partial：未做原生鼠标、视口事件传播或人眼手感验收，不能代表全部玩法通过。

## 源码与改动

基于 main 提交 e8d96a78f49182eb17e269e2b62a738c7a9bb7ea，在独立分支工作区验证；本报告生成时尚未提交。没有合并或上传。

- InteractionRoom.Gestures.cs：松手时重新 ContactAt；只接受原对象的当前未遮挡 Body 接触，使用当前部位、法线和位置；不重定向另一只。持续拉拽和扫击处理未改动。
- InteractionRoom.cs：在创建世界和 SaveStore 前调用选择性诊断守卫，正常初始化后启动诊断。
- Main.cs：直接 Main 入口遇到本诊断参数时，在世界和 SaveStore 前拒绝。
- InteractionRoom.PokeAudit.cs 及其 .uid：永久可选诊断。仅 headless、精确两组参数、新测试档名和新绝对输出目录可启动；默认游戏不启动诊断。
- 上述文件均在 src/Yukkuri.Game/。最终字节 SHA256 见 source-sha256.json。没有复制组合分支的生产源码。

规划依据：planning/stages/interaction-redesign-20261031/REMOTE-CASES.md 第 10 项要求真实接触、不得隔空命中；第 11 项要求保留持续拉拽。

## 基线与复测

先仅移入诊断及启动保护，未改手势产品逻辑。在独立 main 基线上复现：
- 有效头部轻点、画布外松手通过。
- 空画布松手、另一只身上松手失败：原对象健康 100→99.44，疼痛 0→0.84，出现 Flick。

最终 8/8：有效头部轻点、空画布松手、另一只身上松手、画布外松手、同对象 Head→Side 使用当前完整接触、指针不动但对象移走、真实持续拉拽后松手不追加点击、空处起始扫击后松手不重复结算。

最后两项通过 Input.ParseInputEvent 设置引擎按钮/指针状态，再调用实际 ProcessRedesignGestures；未直接伪造 Contact 或设置 pulled/swing 状态。拉拽实际产生 PullAmount=1/PullRemaining=0.3；扫击实际伤害目标且松手不追加反应。普通轻点用生产 _UnhandledInput/_Input 回调。准备工具通过真实按钮信号完成；不冒充原生鼠标操作。

测试使用普通 CreateCourtyardEconomy 新世界。关闭自动过程以冻结模拟时钟，展示层用零时间步刷新。移动用例显式改变对象位置并刷新同一真实 Actor，再查询射线；没有声称覆盖自主行走。每用例重置世界与 UI 会话。诊断使用独立新测试档，不读取玩家存档。

纯检查：Input.Intent 43/43、Interaction.StateBoundaries 10/10，退出码均 0。构建与 headless import 成功；最终构建 0 warning、0 error。混合 --legacy、直接 Main、复用测试档均进程退出码 2，未输出世界启动标记，也未创建诊断结果目录。

## 中间失败及限制

- 首次扩展诊断 5/8：两个断言错误地要求受伤对象旁观者的 ReactionSerial 不变。实际当前接触与动作正确，旁观者健康不变；现有 RecordPlayerThreat 会触发旁观反应。最终仍严格要求无效释放时双方没有新反应。
- 同次远距离扫击夹具未命中指定对象。原因未确认，不能追认是穿过别的对象。改成同一对象附近明确空点后，实际扫击回归通过。
- 扩充本地运行器时误改复用档选择条件，首次所谓 guardreuse 实际用了新测试档并重复通过 8 项，不能计为守卫通过。修正运行器后，真正复用既有测试档被正确拒绝。原始记录保留。
- 初次构建有 2 个现存 nullable 警告和 2 个本地相对包源不存在警告；实际使用获准安装工具链的离线 nupkgs。后续构建无警告。
- 基线及一次重复诊断退出时各报告 2 个 ObjectDB 实例泄漏；最终正式 8 项运行未出现该警告。未修复泄漏，不能声明资源生命周期完全通过。

## 重现方式与预算

在独立授权副本内，先用锁定 SDK 离线构建 src/Yukkuri.Game/Yukkuri.Game.csproj，设置 DOTNET_GENERATE_ASPNET_CERTIFICATE=false、UseSharedCompilation=false、nodeReuse=false；工具缓存、CLI home 和日志均隔离。先 headless --editor --import --quit。

诊断命令参数：Godot --headless --path src/Yukkuri.Game --audio-driver Dummy -- --poke-contact-audit-output <绝对且尚不存在的工程 .local/testing/新目录/result.json> --interaction-test-profile poke-audit-<唯一后缀>。

构建并执行 checks/Input.Intent/Input.Intent.csproj 与 checks/Interaction.StateBoundaries/Interaction.StateBoundaries.csproj；Input.Intent 接收独立结果 JSON 路径。完整本地参数、进程身份、退出码、日志及中间失败留在工程 .local/testing，不作为公开原始日志提交。

首次构建起固定 1800 秒截止：2026-11-06T22:22:33.4874179Z；每进程最多 300 秒，实际 WaitForExit，超时只终止 PID/启动时间匹配的自有进程。预算未重置，无超时。实际测试跨度 21:52:33.522–21:58:47.404 UTC，约 374 秒；最长单进程 26.549 秒。全部最终运行发生在最后一次产品源码修改之后。
## 独立组合核验

在既有组合源码的新隔离副本中应用同一释放接触补丁及诊断入口，逆补丁精确还原原手势文件。698个输入文件核查只出现预期三处源码差异及新增诊断。戳碰8/8、三个入口保护、相处31/31、选择24/24、Input.Intent43/43、StateBoundaries10/10均通过。选择测试未临时修改守卫或替换程序集；全部场景使用同一次正式构建。

组合构建保留两条已有CS8604警告；本次组合运行无ObjectDB警告，但不否认此前保留的退出警告。范围仍是真实场景回调/射线接触与合成输入，不代表原生鼠标、画面或策划10/11全部验收。未修改主分支或玩家运行包。

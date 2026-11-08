当前开发阶段：M01「小园初晴」（2026-10-27）
优先阅读 planning/stages/M01/PLAYER-GUIDE.txt 和 KNOWN-ISSUES.txt。
双击 Play Game.cmd 可启动当前Windows独立导出；完整包必须保留exe、pck和data目录。
阶段状态与运行证据：planning/stages/M01/acceptance.json。当前仍在修复和复验，用户试玩验收pending。
历史框架说明保留在下方；其中早期任务和音效数量不代表M01当前功能。

--- 以下为2026-10-26框架历史说明 ---
Yukkuri 乐园 · Windows 基础框架

当前版本：2026-10-26。可运行的基础框架，不是完整游戏或 Steam 发行版。
已确认设定见 planning/CURRENT.txt；本轮目标与验收见 planning/framework-goal-2026-10-26.txt。
完整设定读取入口：planning/settings/README.txt；手册：planning/settings/handbook-v004.txt（截至需求v051，2026-10-27整理）。
可检索规则与原始出处：planning/settings/setting-catalog-v004.json、source-map-v004.json；数值建议仍在planning/balance-draft-v001.txt，未确认/未验证，不是运行时配置。此设定汇编不改变下列基础框架的实际完成范围。
完整v1.0分期：planning/milestones-v003.txt与JSON；夜间Goal至少6小时，单项不足4小时达标自动接续；独立玩法和用户验收。开工时间由用户指定，目前未启动新Goal。交验入口tools/check-milestone.ps1，具体用法见planning/stages/README.txt。

开始体验
1. 双击 builds/windows/Yukkuri.exe，直接运行自包含 Windows x64 版本。
2. Run Preview.cmd 会编译源码再运行；Open Editor.cmd 打开 Godot 工程。
3. 左侧“玩法提示 / 素材预览”提供操作提示和无限资源开关。
4. 分发时复制整个 builds/windows 文件夹，不能只复制 exe。

目前能做什么
管理与人类视角切换；选中团子、观察档案、喂食；特写中以弹簧力拿起、拖动、放下，并在嘴部附近接触喂食。
食盆与睡床按网格放置；食物库存、设施库存、资金和工资实际变化；个体自主寻食和休息，员工按开关完成基本照护。
两个地区往返，未访问地区继续简化模拟；野生个体可带回观察区，再由玩家决定接纳。
照护记录作为示例委托：已接纳、饱腹≥70、身体≥80、观察2次、玩家喂食2次，可领取40资金，每只仅一次。
个体、身体和登记身份独立；观察判断可被后续证据修正，不等于真实属性。
特写期间所有地区被动时间冻结，动作仍消耗资源；退出不会补算时间。
三个手动存档槽位与自动槽位；JSON schema版本校验、原子替换和上次有效备份；不结算离线变化。

操作
鼠标左键：选择；建造模式中点地面放置；特写中按住拖拽、松开放下。
鼠标右键：取消建造或放回手中食物。滚轮：拿取高度。
Tab：管理 / 人类视角；WASD：人类移动；E：进入 / 退出特写；Esc：退出或取消。
O：观察记录；F：普通模式喂食，特写模式拿取食物，再到嘴部附近点击。
1 / 2：选择食盆 / 睡床；F5 / F9：保存 / 读取当前手动槽位。
需要保存近景摆放结果时，先退出特写。自动存档每约60秒运行时间尝试一次。

存档位置和保护
Godot user://saves，通常为：
%APPDATA%\Godot\app_userdata\油库里乐园 · 基础框架\saves
slot-1.json 到 slot-3.json 为手动槽，slot-0.json 为自动槽，.bak 是上一个有效版本。
主档损坏时读取备份，但不覆盖损坏主档；请另存其他槽位。自动写入在备份恢复后暂时关闭，避免覆盖证据。
不把操作系统现实时间写成离线收益或年龄增量。
自动验证使用独立 verification-saves 目录，不操作正常存档。

素材
assets-source/asset-manifest.json：逐项来源、许可、文件校验和、用途。
一张 ImageGen 生成的乐园氛围图，仅用于提示面板，并非已实现的游戏截图。
两段24秒原创合成循环音乐（乐园 / 林缘），三个交互音效；无外部采样或借用旋律。
四个项目自制 SVG 图标；运行时程序生成临时角色、树木、设施。角色造型尚非最终定稿。
Noto Sans SC 字体来自 Google Fonts，OFL 1.1 许可保存在 third-party/NotoSansSC-OFL.txt。
图像提示词：assets-source/sanctuary-morning-prompt.txt；音乐源：tools/make-audio.py；图标源：tools/make-icons.py。

开发工具
Godot 4.7.2 .NET / .NET SDK 10.0.402 / Blender 4.5.15 LTS，锁定于 toolchain.lock.json。
工具位于 .tools；tools/env.ps1 只设置当前进程环境；未修改全局 PATH。
Git/LFS 已初始化，尚未设置作者、提交或推送远程。大二进制资产使用 LFS。

PowerShell（在项目目录）
.\tools\setup.ps1                         下载并校验锁定工具
.\tools\build.ps1                         编译并运行核心规则/坏档检查
.\tools\export-windows.ps1                导出 Windows 版本
.\tools\verify-framework.ps1 -Integration  驱动真实 Godot GUI/输入/物理流程并截图
.\tools\verify-framework.ps1 -Exported -Integration
.\tools\verify-framework.ps1 -Exported -Population 50
.\tools\verify-framework.ps1 -Exported -Population 100
. .\tools\env.ps1                         为当前终端设置工具路径

验证驱动通过 Godot Input.ParseInputEvent 进入实际 UI 和输入处理，不是 OS 级鼠标自动化或用户亲自试玩。
证据在 artifacts/framework；性能为本机简化场景、1440×900、垂直同步开启，不能外推完整游戏或其他电脑。

结构
src/Yukkuri.Simulation：纯 C# 世界状态、规则与文件持久化；不依赖 Godot。被动时间入口统一于 WorldSimulation.Advance。
src/Yukkuri.Game：Main 协调器；Main.Ui / Input / Verification 分别负责界面、控制与自动验证；SceneArt 提供临时表现。
Data/simulation.json：原型调参；Data/species-routes.json：未决路线的空数据扩展点。
checks/Simulation.Smoke：核心行为、状态一致性、档案修正、资源拒绝、隔离范围、坏档与备份保护。

限制和后续
完整繁衍、遗传、物种升级、牌照流程、白金身份玩法、重伤/转生/永久死亡、章节内容、复杂关系与员工系统均待实施。
当前疾病/死亡未实现，饥饿仅缓慢损伤至最低1点；此原型保护不改变最终死亡设计。
近景形变为整体现有网格的力度缩放，尚无精细局部软体、工具碰撞伤害或最终手感调校。
自主移动暂为直线趋向，已加入简单个体间分离；尚无复杂导航与正式排队，家具未全部加入复杂物理碰撞。
未实现建造撤销/拆除、完整建筑系统、日英翻译、正式语音、Steam、签名或安装器。
游戏可在本机运行和自动验证；用户试玩与其他电脑兼容性仍待验证。




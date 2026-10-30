# Yukkuri 乐园

使用 Godot .NET / C# 开发的 3D 油库里养成与互动项目。本仓库保存当前工作源码、必要素材、设计需求、研究索引及开发记录；当前仍在开发，M01 玩法验收尚未通过。

远程测试入口：[今晚改动先测什么](testing/night-20261031-first-pass.md)、[协作规范](testing/PROTOCOL.md)、[当前测试要求](testing/request.json)。开发源码在 `main`，测试结果写入 `codex/dot-test-results` 分支。运行只限用户安排的独立远程机器及当地授权范围；此次交付不启动或恢复已经暂停的周期测试。[dot 接入说明](testing/DOT-HANDOFF.md)保留定时接入方式。

2026-10-31 夜间[源码与真实素材包](https://github.com/AsukaLangrey/Yukkuri/releases/tag/codex/alice-night-20261031-4a2adc6c4c13)已公开，固定提交 `4a2adc6c4c137e7e98387def474177f0bab3d034`。请下载 Release 附件中的 `Yukkuri-alice-night-source-4a2adc6c4c13.zip`；[交付记录](planning/stages/night-three-lines-20261031/DELIVERY.md)提供校验值与范围。该包未编译或运行，不是游戏成品。

## 当前状态

- 当前工程入口为 `src/Yukkuri.Game/InteractionRoom.tscn`。2026-10-31 夜间需求与进度见[三线计划](planning/stages/night-three-lines-20261031/PLAN.md)、[当前状态](planning/stages/night-three-lines-20261031/STATUS.md)和[逐项证据](planning/stages/night-three-lines-20261031/requirement-evidence.tsv)。`planning/CURRENT.txt` 连接最新入口；`START-HERE.txt`、`README.txt` 和旧阶段记录保留历史背景。
- 工作源码含冻结 art03 之后的改动。`builds/playtest/HI01-art03-20261028` 及原启动器保持原样，试玩包没有随此次迁移重新打包，也不在 Git 仓库中。
- 今晚仅做源码改动、静态审查和 Git 交付，没有在原开发机启动游戏、Godot、headless、编译器、测试或渲染程序。新增检查源码不等于检查通过；历史局部通过记录不能证明当前源码稳定或玩法已验收。

## 当前操作入口

以下已接入源码，实际手感和完整旅程待独立远端验证。

- 手掌拖拽抓起移动，慢松手放下，快速甩动后松手抛出；右键收回当前操作。
- 拳头连续点击命中可连击；火机长按灼烧，固定与自由状态分别有紧绷和逃离反馈。
- 食物点击油递嘴，点击空地放下；地面食水可以拿起，再递给油或适用的培养瓶。
- 用品补给查看和购买真实库存。测试购买不扣钱，成功使用仍消耗份数；救援用品点击落水的油救起。
- 同伴、家庭与照护入口处理关系、育幼和繁育干预；外茎可支护、剪下、装瓶并独立补给，瓶中活窝可用撤茎支护工具取出。
- “调试 · F3”观察完整状态，搜索、分页、记下读数和比较变化。生命、信任、恐惧、馒馒来度及瞬时反应均可观测。

## 获取源码和素材

需要 Git 和 Git LFS。字体、PNG、WAV、GLB、Blend 原文件使用 Git LFS 保存；克隆后需要下载这些素材，不能只使用指针文件。

```powershell
git lfs install
git clone https://github.com/AsukaLangrey/Yukkuri.git
cd Yukkuri
git lfs pull
```

仓库为公开仓库，读取源码不需要私有仓库权限；向结果分支提交报告仍需写入权限。发布页中明确标注含实际 LFS 素材的源码 ZIP 可直接解压取得素材；不要把 GitHub 自动生成的 Source code ZIP 当作同一个交付包。源码目录可放在任意合适位置；历史记录中的 `D:\Project\Yukkuri` 等路径是原开发机证据位置。少数独立诊断工具（例如 `checks/Renderer.Probe`）仍含本机输出路径，不属于默认启动步骤。

## 依赖与启动方法

以下命令仅供独立远程机器在当地授权范围内使用；原开发机及其本机副本仍禁止运行。本轮没有执行这些命令，完整测试范围以 `testing/request.json` 为准。

锁定依赖见 `toolchain.lock.json` 与 `global.json`：Windows x64、Godot 4.7.2 .NET、.NET SDK 10.0.402、Blender 4.5.15。版本来自项目现有锁定文件；本次未重新验证下载地址或新机器安装过程。PowerShell 脚本将工具下载到 `.tools`，校验文件哈希，不要求上传工具链。

```powershell
# 下载并解压锁定的工具链。
.\tools\setup.ps1

# 编译并运行基础规则检查；不替代 request.json 中的完整测试范围。
.\tools\build.ps1

# 从当前源码启动预览；脚本也会执行构建和检查。
.\tools\run.ps1

# 如需导出 Windows 程序。
.\tools\export-windows.ps1 -OutputDirectory builds/rebuilt
```

`Open Editor.cmd` 打开工程。`Play Game.cmd`、`Play Interaction Preview.cmd` 和本机候选启动器依赖本地 `builds` 内容，单独克隆源码不会获得那些冻结试玩包。自编译引擎候选另需遵循 `assets-source/engine-provenance/README.txt`，普通导出不等于重建原候选。

## 项目结构

| 路径 | 内容 |
| --- | --- |
| `src/Yukkuri.Game` | Godot 场景、交互、表现、UI 和运行所需素材 |
| `src/Yukkuri.Simulation` | 世界状态、行为、需求、伤势及存档规则 |
| `src` 下其他项目 | 供生产与离线检查共用的代码，按项目引用使用 |
| `checks` | 规则、输入、几何和存档等检查源码；上传不代表已经运行 |
| `assets-source` | 素材源文件、生成清单和引擎来源记录 |
| `tools` | 安装、构建、导出、诊断及归档脚本 |
| `planning` | 原始需求、方案、阶段状态与历史源码快照 |
| `research` | 研究记录、来源链接、证据边界与索引 |
| `third-party` | 第三方许可和声明 |

## 仅保留本地的内容

`.tools`、`.cache`、`.local`、`.godot`、`bin`、`obj`、`builds`、`artifacts`、临时日志、崩溃转储、个人存档及凭据文件均由 `.gitignore` 排除，未因迁移删除。文档对本地证据、截图、冻结构建和存档的引用可能仍指向这些目录；GitHub 仓库不是整台开发机或全部证据目录的备份。

保留 `third-party` 中已有许可与来源说明。本次迁移未给整个项目添加开源许可证，研究参考的收录也不代表获得商用或再分发授权。

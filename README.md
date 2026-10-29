# Yukkuri 乐园

使用 Godot .NET / C# 开发的 3D 油库里养成与互动项目。本仓库保存当前工作源码、必要素材、设计需求、研究索引及开发记录；当前仍在开发，M01 玩法验收尚未通过。

## 当前状态

- 当前工程入口为 `src/Yukkuri.Game/InteractionRoom.tscn`。最新需求与进度先读 `planning/CURRENT.txt` 和 `planning/stages/night-repair-20261029-7h/STATUS.txt`；`START-HERE.txt` 和 `README.txt` 保留历史交接背景，较早状态不能替代最新记录。
- 工作源码含冻结 art03 之后的改动。`builds/playtest/HI01-art03-20261028` 及原启动器保持原样，试玩包没有随此次迁移重新打包，也不在 Git 仓库中。
- 2026-10-29 此次迁移仅做文件、Git 和上传核验，没有启动游戏、Godot、headless、编译器、测试或渲染程序。独立测试曾出现系统错误/运行时崩溃，原因尚未确认；历史局部通过记录不能证明当前源码稳定或全部功能通过。

## 获取源码和素材

需要 Git 和 Git LFS。字体、PNG、WAV、GLB、Blend 原文件使用 Git LFS 保存；克隆后需要下载这些素材，不能只使用指针文件。

```powershell
git lfs install
git clone https://github.com/AsukaLangrey/Yukkuri.git
cd Yukkuri
git lfs pull
```

仓库为私有，需要对应的 GitHub 访问权限。源码目录可放在任意合适位置；历史记录中的 `D:\Project\Yukkuri` 等路径是原开发机证据位置。少数独立诊断工具（例如 `checks/Renderer.Probe`）仍含本机输出路径，不属于默认启动步骤；迁移未改写这些历史工具或记录。

## 依赖与启动方法

以下是供后续获准运行时使用的说明，本次迁移没有执行这些命令。当前禁止运行的约束继续有效。

锁定依赖见 `toolchain.lock.json` 与 `global.json`：Windows x64、Godot 4.7.2 .NET、.NET SDK 10.0.402、Blender 4.5.15。版本来自项目现有锁定文件；本次未重新验证下载地址或新机器安装过程。PowerShell 脚本将工具下载到 `.tools`，校验文件哈希，不要求上传工具链。

```powershell
# 下载并解压锁定的工具链。
.\tools\setup.ps1

# 编译并运行规则检查；这一步会执行测试。
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

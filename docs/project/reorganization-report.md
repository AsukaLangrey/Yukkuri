# Yukkuri 项目结构整理报告

2026-11-09 · Windows · v0.1.60

## 来源与隔离

- 来源为 `C:/Project/Yukkuri/source` 的固定提交 `4acb53f3be7be7975ee464491a6aa6009a4c8ed2`。
- 成果为 `C:/Project/Yukkuri-Reorganized`，独立 `git clone --no-hardlinks`，本地分支 `chore/reorganize-project`。本报告随整理提交，可用 `git rev-parse HEAD` 获取交付身份。保留原 Git 历史及 GitHub origin 地址。
- 原 `source`、`play-current`、`merge-main` 的 HEAD、Git 状态输出和 stash 列表前后相同。原 source 的 2,611 个跟踪文件重新取 SHA-256，与验收基线逐一一致。验证仅启动本轮创建的进程，独立应用数据均位于新目录。最初几项检查使用系统临时目录中各自的随机子目录，最终复跑使用新目录 `.local/tmp`。
- DeepSeek 未使用。分类、比对和校验使用本地脚本。整理提交只保存在本地。

## 交付结构

```text
README.md / Play.cmd / Editor.cmd
Yukkuri-Project/  原案、世界观、玩法、场景、交互、美术音频、参考资料
assets/           art、audio、fonts、references、manifests
game/             src、checks、tools、third-party、SDK 和工具链配置
docs/             architecture、development、project、testing、releases
archive/          legacy-engine、stages、superseded-docs
.tools/           已校验工具包与安装文件，本地忽略
.cache/           NuGet 等本地缓存
.local/           应用数据、合成存档、构建包、验证日志
```

[逐文件清单](migration-manifest.csv) 每个原路径一行，记录处置类型、实际目标和两个实体 SHA-256。基准 2,611 个文件中 2 个原位保留，1,568 个迁移并保持字节，995 个以相同 SHA-256 去重，46 个改写。目标 SHA-256 取 Windows 检出实体，文本检出使用 `core.autocrlf=true` 和仓库属性。LFS 校验基于实体。对不同内容的同名材料保留差异副本，见 `archive/superseded-docs/variants`。曾指向改写文件的历史副本改为指向同 SHA-256 原件，其中 39 个不同原件保存在 `archive/superseded-docs/originals`。历史路径、日期、失败结果与素材许可可按清单追溯。

运行源码和测试断言沿用工程基线。改写集中于启动、工具路径、输出隔离、NuGet、Windows 导出配置、测试入口链接及原案媒体链接。Godot 正常首次导入重新序列化了 `project.godot`，主场景、应用标识、渲染器与物理设置保留，默认过滤设置由引擎省略。20 个 `.import` 文件统一 LF，保留原 UID 和设置。原有 C# 等源码 sidecar 随工程迁移。新生成 sidecar、导入缓存、编译缓存和验证输出由忽略规则排除。旧启动批处理使用 `.cmd.disabled` 后缀。

新增文件单独列于此处。根目录的 README 和两个 CMD 为新入口，原 README 和旧启动器可通过清单查到。新增各区域 README 负责阅读导航。新增 `game/tools/build-game.ps1` 负责现行 C# 构建，`sync-assets.ps1` 校验并接入共享素材。新增 `assets/manifests/runtime-assets.json` 和 `asset-provenance.json` 分别提供运行映射和更新后的许可来源路径。其余新增交付是本报告与逐文件清单。历史记录里的机器路径和历史引用继续按原文保存，当前导航中的相对链接已检查。

## 资源与工具链

- [接入清单](../../assets/manifests/runtime-assets.json) 共 68 条，涵盖媒体、导入设置和随运行资源分发的来源文件。Godot 内部 `res://` 位置沿用工程基线。资产实体 SHA-256 在复制前验证，缺失 PNG 的测试在复制前报错，恢复后同步成功。自包含的 Kenney Nature GLB 保留许可证及来源，候选 Survival Kit 保留同目录外部纹理和原始包，制作端 Blend 探针与当前已提交的 GLB 输出分别维护。
- 基准 LFS 对象校验通过。另有 10 张原方案 PNG 以普通 Git 对象保存却被标记为 LFS，迁移后统一为 LFS，实体字节保留。历史素材的原许可与作者记录见 [资产来源索引](../../assets/manifests/README.md)。
- 实际工具链为 Godot `4.7.2.stable.mono.official.ed1daf0bf`、.NET SDK `10.0.402`，均从原 source 已下载的 ZIP 读取副本，由 [锁定清单](../../game/toolchain.lock.json) 校验后提取到新目录，未共享工具安装或旧缓存。干净暂存区快照同样独立提取锁定工具包。新检出安装和使用步骤见 [开发环境](../development/README.md)。网络重新下载、Windows 导出模板安装与产物导出本轮未执行。

## 实际验证

机器 `Shiki-5060Ti-PC`，Windows build 26200，窗口验证使用 NVIDIA RTX 5060 Ti OpenGL Compatibility。源码身份由固定基准和本提交的逐文件清单确定。

| 命令或检查 | 退出码及观察 |
| --- | --- |
| `game/tools/setup.ps1 -Package godot,dotnet` | 0。锁定的 Godot 和 .NET ZIP 校验并提取成功。 |
| `Play.cmd -Headless -QuitAfter 180 -Profile migration-smoke` | 0。首次完整构建 0 错误、4 条既有 CS8604/CS8602 警告，随后 Godot 导入结束并进入主场景。 |
| `Play.cmd -QuitAfter 360 -Profile migration-visible` | 0。可见窗口启动，日志显示 `YUKKURI_VERSION: v0.1.60`、`INTERACTION_ROOM_READY` 和两个 Kenney GLB 加载成功。 |
| `Editor.cmd -QuitAfter 180` | 0。编辑器入口完成编译并打开工程。 |
| 干净暂存区快照执行 `Play.cmd -Headless -QuitAfter 180 -Profile clean-checkout` | 0。从空缓存构建、导入并进入主场景。该次限帧退出时 Godot 控制台报告 4 个 ObjectDB 对象和 1 个资源仍在使用。 |
| `game/tools/render-starter-garden.ps1 -RunName migration-sample` | 0。独立庭院美术样板生成 5 张 1920×1200 图与 8 条几何检查记录。 |
| `game/checks/verify-launcher-offline.ps1` | 0。接受合法包身份，拒绝已更改 PCK 和越界路径。 |
| `dotnet run --project checks/Milestone.Smoke/Milestone.Smoke.csproj` | 0，63 项通过。 |
| `dotnet run --project checks/ActorGeometry.Smoke/ActorGeometry.Smoke.csproj` | 0。 |
| `dotnet run --project checks/Family.Presentation/Family.Presentation.csproj` | 0，3/3。 |
| `dotnet run --project checks/Courtyard.ColdStorage/Courtyard.ColdStorage.csproj` | 0，7/7。 |
| `dotnet run --project checks/Simulation.Smoke/Simulation.Smoke.csproj` | -532462766。在 Program.cs 第 79 行发生 `Sequence contains no matching element`。 |
| `dotnet run --project checks/Interaction.Smoke/Interaction.Smoke.csproj` | 1，47/48。喂食手势重复消耗。 |
| `dotnet run --project checks/Family.Growth/Family.Growth.csproj` | 1，4/5。失养幼体到龄后成熟。 |
| `dotnet run --project checks/Family.ColdCare/Family.ColdCare.csproj` | 1，3/9。旧冷藏与亲代照料预期仍失败。 |

四项失败均在从固定提交用 `git archive` 提取的独立基准副本上复现，PASS/FAIL 文本和退出码一致。主入口验证创建独立应用数据和测试存档目录。真实历史档兼容检查需要显式指定档案，本轮未读旧试玩档。`Save.Compatibility` 无参数探测返回参数错误，未进行兼容验证。未测全量玩法、长时间运行、Windows 导出包、交互手感和音频主观效果。

本机详细日志位于 `.local/testing/`、`.local/migration/` 和干净快照的 `.local/testing/`，包括编译、导入、启动、源基准重跑和退出码。历史原件中的既有空白格式保留，因此整体迁移的 `git diff --check` 会报告部分历史行的空白问题。原聊天独立验收仍待完成。

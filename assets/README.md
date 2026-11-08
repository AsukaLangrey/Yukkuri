# 共享资产库

| 目录 | 维护内容 |
| --- | --- |
| [art/runtime](art/runtime/) | 供现行 Godot 接入的美术、图标和第三方模型 |
| [art/production](art/production/) | 制作源与庭院美术样板 |
| [art/candidates](art/candidates/) | 候选素材、外部纹理、原始压缩包与许可 |
| [audio/runtime](audio/runtime/) | 音乐、音效与生成分析 |
| [fonts](fonts/) | Noto Sans SC 及 [OFL 许可证](fonts/NotoSansSC-OFL.txt) |
| [references](references/) | 原案使用的概念图与设计参考 |
| [manifests](manifests/) | 同步清单、历史来源账本和制作说明 |

[运行接入清单](manifests/runtime-assets.json) 将文件复制到 Godot 项目中原有的 `res://` 位置。现行模型 [Kenney Nature 子集](art/runtime/third-party/KenneyNature/SOURCE.md) 的 GLB 自包含。候选 Survival Kit 的 GLB 需要同目录下的 `Textures/colormap.png`，参见 [来源记录](art/candidates/survival-kit-20261031/SOURCE.md)。[Blend 探针](art/production/pipeline/pipeline-probe.blend) 保留为制作源，运行时使用已提交的 [GLB 输出](art/runtime/pipeline/pipeline-probe.glb)。

[来源与许可索引](manifests/README.md) 对应现行路径。历史候选收录清单及历史来源账本中的旧路径可按 [迁移清单](../docs/project/migration-manifest.csv) 对照。

# Windows 开发环境

在仓库根目录打开 PowerShell 7。工具包版本与哈希由 [game/toolchain.lock.json](../../game/toolchain.lock.json) 固定。

```powershell
git lfs install --local
git lfs pull
./game/tools/setup.ps1 -Package godot,dotnet
./Play.cmd
```

工具、下载包、NuGet 缓存分别落在仓库的 `.tools` 和 `.cache`。安装脚本支持复用 `.tools/downloads` 内通过哈希核对的 ZIP。需要编辑时运行 [Editor.cmd](../../Editor.cmd)，它会先准备素材并编译，然后打开编辑器。

```powershell
./game/tools/sync-assets.ps1
./game/tools/build-game.ps1
./game/tools/import-assets.ps1
./game/tools/build.ps1
```

最后一条运行现有的四个回归入口，遇到失败即停止。[测试入口](../testing/README.md) 说明怎样分别运行和记录退出码。

[资源清单](../../assets/manifests/runtime-assets.json) 记录仓库相对源路径、`game/` 相对目标路径和实体 SHA-256。修改素材时同步更新清单哈希。Godot 的 `.godot/` 与新生成的 sidecar 属于本地缓存。已跟踪的资源 sidecar 作为导入设置随素材分发。删改映射后如遇到过期派生文件，关闭编辑器和游戏，清除 `game/src/Yukkuri.Game/Assets` 和 `game/src/Yukkuri.Game/ArtPreview/Textures`，再同步和导入。

Windows 导出先运行 `./game/tools/setup.ps1 -Package godot-templates`，再运行 `./game/tools/export-windows.ps1`。输出在 `.local/builds/windows`。旧 M01 包、诊断工具和历史脚本见 [archive](../../archive/README.md)。

启动器使用 `.local/play-current` 内的应用数据。限帧无界面检查可运行 `./Play.cmd -Headless -QuitAfter 180 -Profile migration-smoke`。启动日志按次写入 `.local/testing`。

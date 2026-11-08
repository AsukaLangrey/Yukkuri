Yukkuri 乐园 · Windows 包与独立源码包

Windows 试玩包
解压整个压缩包，双击 Yukkuri.exe。保留同目录的 .pck 文件、data 和 third-party 等随包内容。直接试玩无需安装 Godot 或 .NET SDK。

独立源码包
在 Windows x64 的 PowerShell 中进入解压后的源码目录，运行 .\tools\setup.ps1 准备锁定工具链，运行 .\tools\build.ps1 编译并执行脚本内置检查，再用 .\tools\run.ps1 从源码启动。引擎自编译候选的重建要求见 assets-source/engine-provenance/README.txt。第三方许可随包附在 third-party。

完整 Git 仓库
仓库首页 https://github.com/AsukaLangrey/Yukkuri
从 Git 克隆的工程请先读 Yukkuri-Project/START-HERE.txt，按现行工程说明准备 Git LFS、工具链和试玩入口。

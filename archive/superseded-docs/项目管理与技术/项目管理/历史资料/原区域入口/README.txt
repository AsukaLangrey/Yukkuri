Yukkuri 乐园 · M01「小园初晴」

这是首个独立玩法里程碑，不是完整v1.0。完整需求、后续范围与验收约定保留在 planning/milestones-v003.txt 和 planning/settings/README.txt。
当前状态、候选路径和未完成检查先看 planning/CURRENT.txt；用户试玩验收与开发验证分开。

试玩
交付Windows压缩包解压后运行Yukkuri.exe，保留同目录的pck、data和third-party。无需安装Godot、.NET SDK或编辑器。
操作与六项目标见 planning/stages/M01/PLAYER-GUIDE.txt；当前限制见 KNOWN-ISSUES.txt。
当前工作区的 Play Game.cmd 优先启动 builds/playtest/M01-CLANG-20261027-074048 的本机兼容入口；只有未附该试玩目录时才回退 builds/windows。最终包、证据与源码见 builds/releases/M01-CLANG-20261027-074048。

从源码重建
1. 在Windows x64、PowerShell中进入本源码目录，运行 .\tools\setup.ps1。它把锁定的Godot、.NET和Blender下载到本地.tools并校验；不修改全局PATH。
2. 运行 .\tools\build.ps1，编译游戏并执行模拟与M01规则检查。
3. 运行 .\tools\export-windows.ps1 -OutputDirectory builds/rebuilt，导入资源并导出自包含Windows程序。单独导出成功不代表玩法验证通过。
4. Run Preview.cmd 用当前源码运行，Open Editor.cmd 打开Godot工程。初次运行需要资源导入。

当前自编译引擎候选
上面的普通导出使用锁定的官方模板；后续正式版长测曾出现原生渲染崩溃，尚未定位根因。
当前隔离候选使用同版本官方源码的LLVM-MinGW UCRT release引擎。其来源、编译参数、二进制身份、关闭功能和重建步骤位于 assets-source/engine-provenance/README.txt；运行包另附 engine-provenance。
必须先准备该说明中的引擎源码、LLVM工具链和SCons，再运行 tools/build-candidate-template.ps1；tools/new-engine-candidate.ps1将匹配游戏导出复制为独立候选。这个步骤不是setup.ps1当前自动完成的部分。
独立干净重建可运行 tools/rebuild-engine-isolated.ps1 -OutputDirectory artifacts/native-rebuild；它校验两份官方来源压缩包、全新解压源码和编译器，并拒绝覆盖旧目录。需先按来源记录下载压缩包，并在.tools/engine-diagnostics/python-packages准备SCons4.11.1。-CheckOnly只检查前提，不构建。实测独立构建与完整GUI通过，但hash不同，不冒充原候选身份。
原生符号引擎保留在开发机诊断目录，不在试玩zip中；来源记录不是完整符号文件。不要把官方模板重导出冒充自编译候选逐字节重建。

验证和记录
本机独立源码复制、空编译/导入缓存的真实重建及重建后完整GUI验证记录在 artifacts/m01/source-rebuild-20261027-052152；使用已有锁定工具链，不代表新机器从零安装已验证。
用户试玩记录 planning/stages/M01/feedback.txt；阶段证据与待办 planning/stages/M01/acceptance.json、IMPLEMENTATION.txt。
GUI自动检查通过Godot真实输入路径驱动；原生键鼠试玩另行记录，二者均不代替用户确认。

源码结构
src/Yukkuri.Simulation：不依赖Godot的世界状态、行为、伤势、知情与持久化规则。
src/Yukkuri.Game：场景表现、键鼠交互、UI、音频以及实际GUI验证入口。
checks：模拟与M01规则回归；assets-source：素材生成来源、清单和引擎来源；third-party：许可证。
planning：原需求、设定依据、分期、阶段记录；tools：环境、构建、导出、验证、打包。



独立引擎候选说明 / 2026-10-27

当前不是正式稳定性承诺。游戏使用 Godot 官方 4.7.2-stable 源码本地编译的 .NET Windows template_release；原官方发布程序曾在后续候选自然游玩中原生崩溃。此候选用于验证构建工具链差异，并未证明已找到或根治崩溃原因。

来源和重建
1. 按 godot-source-provenance.json 与 llvm-provenance.json 中的官方来源下载源码和 LLVM-MinGW UCRT 工具链，并核对 SHA256。
2. 源码目录为 .tools/engine-diagnostics/godot-4.7.2-stable，编译器目录为 .tools/engine-diagnostics/llvm-mingw-20261020-ucrt-x86_64。
3. 构建使用 Python 3.11、SCons 4.11.1；完整参数及实际构建结果分别见 build-request.json、build-result.json。源码快照 tools/build-candidate-template.ps1 提供相同构建入口。
4. 使用 C# / Mono 模块、优化 release、保留调试符号。关闭 D3D12 与 AccessKit；当前默认使用 OpenGL。此候选未提供 AccessKit 屏幕阅读器接入，不能当作已经验证无障碍支持。
5. 两处 ASan 专用诊断源码改动见 asan-only-source.patch；本次 use_asan=no，相关分支不参与编译。GLES3 和 Mobile 对象池头文件已恢复官方原版，哈希对应记录随附。
6. 完整符号引擎仍在本地诊断目录，交付候选由 llvm-objcopy --strip-debug 生成。引擎文件哈希记在外层 build-info.json 的 engine_candidate 中。游戏 .pck、托管代码和自包含 .NET 运行时保持对应游戏导出一致。
7. 引擎许可证和第三方声明位于上级 third-party；运行证据与用户验收状态分开记录。构建成功不能替代运行验证。

8. 自编译运行库声明随third-party/LLVM-LICENSE.txt及MinGW-w64-*原样提供，来源为锁定工具链中的LICENSE.TXT及x86_64-w64-mingw32/share/mingw32。缺少关键声明会被本地打包入口拒绝。全新引擎重建后如二进制身份变化，应保存新构建的实际来源与身份，不复用本次binary-identity.json冒充原构建。

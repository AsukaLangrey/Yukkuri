# 相处工具待选对象取消与会话隔离

修复范围：真实工具切换、Escape/场景右键取消、读档或重新开院后，清除尚未提交的相处首对象。救援用品、投放新油的“选择落点”、侧栏“拿小家”的直接工具入口也清除待选。

不改变：正常第二次点选、同一工具重选、普通玩法页面开关、保存而不替换世界。CancelGesture 未作全局清空，现有社交规则和无效对象拒绝逻辑不变。

验证：基线12项中7通过、5失败，5失败都有实际双方相处对象改变的证据。修复后扩展31/31通过；Family.Social 22/22通过；最终显式Game Rebuild 0错误、2条既有CS8604警告（SupplyStationValidation、InteractionValidation）。初次构建另外有2条旧相对NuGet源警告，增量构建的0警告不抹去前述结果。五个安全负例（缺少profile、并发其他验证flag、legacy、integration、m01模式）均正确拒绝，exit2，无输出文件、未进入ROOM_READY。

全部是headless检查：真实按钮信号、ContactAt投射体表点击走_UnhandledInput，Escape/右键走_Input。不是系统键鼠或可见UI验收。救援入口使用回调级控制。重开只验证待选清理，不验证main已知旧工厂经济初始化；完整选择/重开回归由组合层另验。

可重跑：在工程源码目录完成Godot .NET构建及headless导入后，运行Godot：
--headless --path <E/src/Yukkuri.Game> -- --interaction-test-profile <fresh-unique-profile> --company-selection-check-output <absolute-E/.local/testing/fresh-empty-directory/result.json>

输出参数是JSON文件路径，不是目录。profile必须唯一且未使用；输出目录必须新建或为空。并发旗检查在任何场景路由之前；完整路径/profile guard在SaveStore之前，要求headless、绝对输出、两旗各一次、隔离profile和E/.local/testing下的输出；拒绝所有其他--用户开关。普通游戏无此flag不启动检查。源码不含固定个人路径。

提交仅包含source-sha256.json列出的7个源码文件及本脱敏报告目录。不得加入Godot导入生成的.uid/.import、.godot、bin/obj或原始日志。完整前后hash、导入差异、运行PID/参数、日志保存在隔离副本E/.local/testing/company-reset-evidence。此前失败audit副本保持原样。

所有Git暂存、提交、推送和组合整合由协调者进行；本执行者未操作Git、main、PR或上传。

最终run2为显式Rebuild后31/31通过、exit0；退出时Godot报告2个ObjectDB实例泄漏，保留原始stderr，不声明资源生命周期问题已解决。run1无此警告。此警告不作为未通过的玩法断言，但仍属未解决运行警告。


## 协调者组合复核

2026-11-06：一次性组合副本83/83纯检查通过，真实headless相处31/31、选择24/24、重开19/19、自动存档48/48通过。旧选择/重开测试仅在诊断构建中补充了入口隔离保护；还原这些临时差异后，正式源码重新显式Rebuild，并再次通过相处31项与自动存档48项。两种源码与产物身份已分别记录。

总验证654.38秒，无超时或残留进程；434个源码/检查文件、874份证据、258条继承链均已核验。相处新测试使用最终冻结文件。组合保留此前的选择边界、经济重开和自动存档修复，不用本分支整文件覆盖其他补丁。

本分支独立基于main e8d96a78f49182eb17e269e2b62a738c7a9bb7ea。未创建整合提交、PR或改动main。组合回归未出现ObjectDB退出警告，但独立分支运行曾出现的警告仍保留，不能称资源问题已解决。完整限制见combined-audit.json。

阶段记录和交验入口（2026-10-27）
当前：所有阶段未开始，等待用户指定时间。不创建虚假Goal ID、开始时间、工作时长或通过记录。

计划检查：
  .\tools\check-milestone.ps1 -Mode Plan -Stage M01
实际交验前：
  .\tools\check-milestone.ps1 -Mode Ready -Stage M01
不足4小时达标，接续下一项前：
  .\tools\check-milestone.ps1 -Mode Advance -Stage M01
整轮夜间交验（使用该轮记录目录）：
  .\tools\check-milestone.ps1 -Mode NightReady -Stage M01 -RecordDirectory planning/stages/round-实际编号
用户试玩通过后、完成Goal前：
  .\tools\check-milestone.ps1 -Mode Accepted -Stage M01
后续阶段替换ID。失败应修正工作或记录，不能删验收项通过。

每阶段首次启动建立 planning/stages/M01/ 等目录：
goal.json字段：stage_id、真实goal_id、started_at（该里程碑开始，带时区ISO时间）、ready_at（实际达到交验标准的时间）、minimum_work_hours（夜间Goal安排>=6，不是单项最低耗时）、manifest_sha256、catalog_sha256。多个阶段可引用同一个真实夜间Goal ID，不能伪造多个活动Goal。哈希从启动时实际里程碑JSON和设定JSON获得。
夜间round目录额外保留goal.json中的stage_records（本轮交验阶段记录目录数组）；started_at为整轮开始，work-log为整轮不重叠实际工作段。stage_id和acceptance.json使用该轮起始阶段作为交验锚点，各阶段仍有各自独立记录。未完成项另列工作报告，不能为通过NightReady假称整轮Goal目标已全部实现。
work-log.jsonl：每行一个实际工作段，started_at、ended_at、activity、evidence（工作区内实际产物路径数组）。不得未来记账、重叠计时、空等记工作；工具运行/编译/加工/测试在真实发生时可记录。
acceptance.json字段：stage_id、build_id、build_artifact、runtime_evidence、feedback_log、known_issues数组（每项id/description/blocking）、checks数组（与阶段全部验收ID一一对应，status/result/evidence）、user_acceptance。
user_acceptance字段：status=pending或accepted、build_id、message_id、quote、evidence。引用真实用户反馈；模型自评、测试脚本或空白模板都不算用户通过。
feedback.txt：逐条记录原话/消息ID、重现步骤、期望/实际、受影响规则、修复版本、原案例复验结果、用户复验情况；旧记录不覆盖。
每次交验保留旧acceptance/goal/日志快照和修正回合版本，不能换build_id沿用旧用户验收。

检查范围与限制
Ready检查实际工作记录、证据、版本基线、阻塞项和全部验收项，不硬等6小时。Advance额外要求实际里程碑完成用时严格不足4小时，且工作记录不晚于ready_at。NightReady核对整轮至少6小时及列出阶段的Ready检查。Accepted另检查真实用户反馈，不以自动接续替代用户验收。
后续阶段的实施和Ready允许前项处于可交验/用户待验收状态；最终Accepted仍需前项通过。前项被用户否决后优先修复，并回归受影响后项。
文件存在/字段通过不证明游戏好玩、证据真实或用户授权真实；执行者仍须查看真实运行与原始用户消息。
这不是Codex宿主钩子，不会自动调用Goal工具、自动唤醒或不可绕过地禁止完成；完成Goal前须主动执行Accepted并实际核对。
tools/test-milestone-gate.ps1只用显式合成夹具测试拒绝条件，不生成真实阶段通过记录，也不代表游戏玩法验收。
用户尚未指定开工时间，因此Ready/Accepted当前应因缺记录被拒绝，这是预期状态。


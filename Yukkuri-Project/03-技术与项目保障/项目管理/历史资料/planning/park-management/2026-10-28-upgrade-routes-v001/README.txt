油库里乐园升级设计包 / v001
日期：2026-10-28 / Asia/Shanghai
本包目标：给后续经营阶段提供可审查、可实施的升级路线。当前设计状态见STATUS.txt。
覆盖：5类区域×3级、7类房屋×3级，共36节点；每级前置、费用与施工、人员、空间、行为成果、新玩法、油自治、异常、可见变化、玩家取舍和容量。
数字是候选，未实现、未调平、未实际试玩。用户明确要求先完成互动基础，本轮不开发经营。

推荐阅读
TASK.txt：用户原话、范围、验收和阶段约束。
01-route-overview.txt：路线一览和三个具体例子。
02-rules-and-economy.txt：每级共用G01—G06规则、施工、经济与开局。
03-area-routes.txt：15个区域等级完整条件与玩法。
04-house-routes.txt：21个房屋等级完整条件与玩法。
05-autonomy-and-workload.txt：油如何执行、协作、替补、拒绝、降载以及玩家如何指导。
06-acceptance-and-failures.txt：25项未来案例及当前真实失败基线。
07-implementation-handoff.txt：等互动与奖牌阶段稳健后的实施交接。
upgrade-catalog.json：与路线正文一致的结构化设计目录，非当前游戏可直接加载配置。
balance-assumptions.json：仅用于核对示例计算的参数。
level-acceptance.tsv：36节点未来验收目录；全部planned_not_run。
audit-design.ps1 / design-audit.json：可重跑的结构与算术检查；不验证游戏行为或乐趣。

确认与提案分开
已确认：三段经营成长，油为主体，宏观指导/局部修正，区域与房屋可升级，工房DLC，先互动后经营。
本次具体提案：五类/七类三级划分、角色职责标签、所有费用/容量/时长、功能前置、能力证据与未来验收案例。
项目整体目标及完整v1.0原范围不缩减，样板/设计包不替代整体验收。当前M01 needs_rework。
对外部聊天仅通过本地入口交接；没有自动监控、消息派发或启动Goal。

版本与存续
本目录是本次详细设计v001。修改时保留TASK原话并新增版本记录；结构目录与正文同时变更，重跑审计。
经营需求使用requirements-park系列，避免与另一聊天共用requirements-v序号；这不是跨聊天锁。
根需求入口：../../requirements-park-v003-upgrade-design.txt。
基础确认：../../requirements-park-v001-autonomy.txt、../../requirements-park-v002-area-building-upgrades.txt。
原经营论证：../../park-management-loop-v005-oil-autonomy.txt。
阶段顺序：../../requirements-v055.txt、../../stages/M01/gameplay-rework-v003.txt。


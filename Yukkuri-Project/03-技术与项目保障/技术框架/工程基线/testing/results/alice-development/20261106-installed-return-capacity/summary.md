# 已安装物品回仓容量保护

## 结论

状态：partial。独立 main 基线上已复现并修复模拟 API 的已安装物品回仓整数溢出。专用检查通过，既有 Continuity 检查有一项基线失败，不能宣称全套通过。未提交、推送或合并。

源码基线：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea。测试对象是该基线加本次未提交补丁，不含其他试验分支。环境为当前获授权的 Windows 本机，不是远程机器。

## 基线证据

仅人为设置 CreateInteractionPlaytest 的初始 wrap 库存为 int.MaxValue；其他默认状态和标志保留，不推进世界或呈现时间。随后真实 Wrap 消耗一件，真实购买补足到上限，保存后独立从磁盘加载两次，每次严格校验。

修复前，Unwrap 和 RecycleCourtyardObject 均将库存从 2147483647 变为 -2147483648，清除已安装包裹，随后严格校验报“商店库存无效”。回收还移动角色到收容箱并清除选择。两条不补购的 2147483646→2147483647 对照均正常。基线探针：2 个缺陷复现、2 个正常对照、0 个探针错误，预期退出码 1。

这是合成整数边界验证，不代表普通玩法可自然积累到该数量，也不是实际玩家存档故障率。

## 修改范围

- CourtyardShop.cs：复用已安装物品映射，增加只读容量判断，仅匹配真实装置及对应解除动作。
- InteractionWorld.cs：ExecuteInteraction 在任何动作变更前拒绝满仓回收。
- InteractionGesture.cs：BeginInteractionGesture 在取消旧手势前执行同一预检，保留正在进行的模拟手势。
- CourtyardObjects.cs：角色回收在释放搬运、归还库存、清理角色状态和移动收容箱前预检。
- 新增 checks/Interaction.InstalledReturnCapacity 永久检查项目。

拒绝反馈为“仓库已满，装置仍保留，请先腾出仓库空间。”无截断、丢弃、退款、容量设计变更或事后捕获异常。SettleShopAction 只有 ExecuteInteraction 一个调用点，保持现有成功后结算逻辑。

## 验证结果

- Interaction.InstalledReturnCapacity：54/54，退出 0。
- Interaction.Inventory：12/12，退出 0。
- Interaction.StateBoundaries：10/10，退出 0。
- Courtyard.Economy：8/8，退出 0。
- Interaction.Continuity：128/129，退出 1。
- 使用本轮修复前保留的模拟程序集再次执行相同 Continuity 检查：128/129，同一项失败。

唯一既有失败：suspended lowering and sliding bodies retain unmet excretion until grounded and still；失败细节为 settling did not resume and finish exactly one excretion after suspended。未扩大本次修复范围处理这一问题。

专用检查覆盖 wrap/frame/box/hanger 四类型，ExecuteInteraction、BeginInteractionGesture、回收三入口；满仓拒绝的完整序列化世界状态不变；精确剩余一格成功；重复退回不重复加库存；12 组错配动作与非满仓对照的完整结果一致；普通模式和错误目标不被容量条件误拦；既有手势在拒绝后仍保留；两条磁盘往返路径再次保存并严格读取通过。

初版测试错误地要求所有错配动作完全不写状态，12 条失败。现有动作核心会保留接触状态更新，修正为同一世界满仓与非满仓错配动作的完整状态及反馈对照，未修改生产行为或放宽容量拒绝原子性断言。初始失败证据仍保留。

构建为 Release、离线空包源、禁用共享编译和节点重用；首次执行前关闭证书生成和首次使用体验。首次构建统一开始于 2026-11-06T21:27:09.4571596Z，硬截止 2026-11-06T21:57:09.4571596Z。每进程真实等待最多 300 秒，且不超过剩余总预算；超时只对本轮拥有且 PID/启动时间一致的进程终止。预算未重置，无超时。初始模拟构建有两项既存 CS8604 警告，没有编译错误。

## 边界与未测

Godot UI 的拖入回收处理在调用模拟回收 API 前会先 CancelGesture；因此本报告只保证上述模拟 API 的原子拒绝，不声称真实 UI 全状态保持不变。未运行 Godot、截图、人工操作、渲染、长期压力测试或普通玩法满仓复现。

本基线无独立 Safety 或 Restraint 检查项目；现有 Continuity 包含悬挂解下、支持下降、取消手势和保存支持状态等相关检查。未擅自引入其他分支代码。

原始日志、合成存档、预算与进程记录只保留在本分支忽略的本地测试目录，不纳入提交；本摘要不含机器/账户绝对路径、原始日志或既有历史运行记录。

## 文件 SHA256

- src/Yukkuri.Simulation/CourtyardShop.cs: F4052108097BD5C6C53B6666367B605B5E295F26AD7AE524519526FE039D1EA1
- src/Yukkuri.Simulation/CourtyardObjects.cs: A599D5716C946DCFA3D148C06C664B6412C4532EFCED8B4CB5AFEB1B5638886E
- src/Yukkuri.Simulation/InteractionWorld.cs: 70BFD3CBB4413156CCAC6764A6A2C5D5F124C41A0E051DF630D686B49D3B6DAD
- src/Yukkuri.Simulation/InteractionGesture.cs: 48DCE9A3DE9BACB8ADF54623EF2F7E39348CDFFAB0497BC5A57F1F870363A760
- checks/Interaction.InstalledReturnCapacity/Program.cs: 91119695F911F3CA65E62358C9C65B2CBF3FE05878702D64C1C880ADB8790400
- checks/Interaction.InstalledReturnCapacity/Interaction.InstalledReturnCapacity.csproj: AF16FC5FFCEDB208F8352FA7824EB96D8F6720D3BEF6CC03980AD9752855CA8A

## 独立组合核验

在既有组合源码的独立副本中精确添加14行，逆变换还原四个原始文件哈希。138个源输入核验一致。11套检查308/308通过：专项54、Inventory12、StateBoundaries10、Economy10、Continuity129、Smoke48、Completion3、Depth4、SupplyStations8、WasteCapacity22及独立死亡个体返还8。实际144.835秒，无超时。

组合Continuity129/129不能归功于本补丁；独立main分支及修复前对照仍为128/129，既有排泄恢复失败仍需其他分支修复。组合准备阶段遗漏链接测试源及必需存档目录参数的失败保留，修正准备后通过，未弱化产品断言。

容量检查可能优先于其他拒绝原因，因此满仓且动作匹配时提示优先级会变化；未发现状态修改缺陷。这里只保证模拟API拒绝原子性，不保证Godot UI此前取消手势的整个操作原子性。未测Godot、画面或用户存档。

# 自动保存等待投放完成（2026-11-06）

结论：已在真实 Godot headless 场景复现并修复。选择新油、小家、食物供给器或饮水器落点时，后台自动保存不再打断投放。手动保存仍按原有逻辑取消操作。

## 版本和改动

- 基准：main 的 e8d96a78f49182eb17e269e2b62a738c7a9bb7ea。
- 独立分支：alice/20261106-autosave-placement；测试时为基准加本报告所述未提交改动。
- 产品改动只在 InteractionRoom.cs：自动保存条件等待 _arrivingCreature、_placingShelter、_placingSupplyStationKind 均为空。
- 不改存档格式、投放收费、手动保存或其他取消操作。
- 原始 InteractionRoom.cs SHA256：8CC1882B9678F172DE0CBCBC6BEC5E0974125C9A65D9F66E0328116A88EDEEF4。
- 冻结测试文件 SHA256：EA952C3392F4BCDD3264FB63162383F788584C8B25B348031341598D180C63F3。

原注释要求后台保存等待完整指针操作，但三个落点选择状态未加入条件。SaveRoom(0,false) 调用 CancelGesture，清掉投放状态又保留旧提示。问题是投放中断，不是钱或库存损失。既有新油验收文档把右键、Esc、切工具、菜单、近看、失焦、读档和重开列为取消操作，没有把后台自动保存列入。

## 实际验证

授权本地 Windows 机器，Godot 4.7.2 mono headless、.NET 10、Debug。未开启可见窗口或截图。

1. 先保存原源码快照和哈希清单，再增加专用测试入口。
2. 未修守卫的基线：游戏 Rebuild 成功，0 错误、2 条既有 CS8604；headless 20/48 通过、28 条失败、exit 1。
3. 最小守卫修复后：再次 Rebuild 成功，0 错误、同样 2 条警告；同一测试文件 headless 48/48 通过、exit 0。
4. 六项相关规则套件均先 Rebuild、再执行当次 DLL：CreatureArrival 6、Inventory 12、Shelters 13、SupplyStations 8、Courtyard.Economy 8、Input.Intent 43，共 90/90，所有构建及运行 exit 0。

四条场景路径均调用真实界面按钮信号，然后把自动保存计时设为 59.99 秒，再调用真实 _Process 推进 0.05 秒。断言覆盖：
- 投放标志、提示、金钱与库存保留，自动槽文件逐字节不变，计时仍逾期。
- Esc 取消后下一帧恢复自动保存，不扣费或耗库存。
- 未被中断的实际画布输入回调可成功放置，只扣一次；再次点击不重复收费。
- 放置完成后下一帧恢复保存。
- 手动保存仍取消投放且不收费；正常空闲自动保存不受影响。

基线的 28 条失败包含因投放提前取消而失败的后续完成断言，不代表 28 个独立缺陷。

## 隔离和复现入口

新增 InteractionRoom.AutosavePlacementVerification.cs。调用：

godot --headless --path <game-project> -- --interaction-test-profile <fresh-unique-profile> --autosave-placement-check-output <absolute-new-output>

首次需要 godot --headless --path <game-project> --editor --import；每个被测版本都先执行 dotnet build src/Yukkuri.Game/Yukkuri.Game.csproj -t:Rebuild -c Debug。运行环境设置已安装工具链的 DOTNET_ROOT/PATH，并关闭首次体验和开发证书生成。

入口在创建 SaveStore 前校验：仅 headless、明确测试 profile、绝对且未存在的输出、空测试 profile 目录。存档仅进入 Godot 的 interaction-test-saves 子目录，不能用 --user-data-dir 代替隔离。没有读取或复制个人存档。

负向实测：缺少 profile、已有测试 profile、相对输出分别 exit 2；前两项未生成结果文件，已有测试 profile 全部文件哈希保持不变。非 headless 拒绝条件只做静态审查，未为验证它打开可见窗口。

结构化报告保留基线和修复全部断言。原始构建、导入、场景及规则日志仅保存在忽略目录的本地测试记录中。

## 边界

这是实际场景节点、按钮信号、输入回调、保存文件和帧更新的自动检查，不是原生鼠标/键盘、画面或手感验收。计时边界是加速设置，不是 60 秒墙钟压力测试。未使用真实历史存档，未声称历史存档兼容通过；这里只运行六项针对性规则检查，组合分支完整回归另行记录。

## 协调组合复核

在一次性隔离副本最小合入前23项之后，75个纯检查项目和游戏Debug Rebuild全部通过；另用新合成profile独立运行同一真实headless场景，48/48通过、exit 0。再次使用已填充profile被拒绝、exit 2，四个测试存档逐字节不变。416份最终源码/检查文件在构建与场景后哈希稳定；163条递归来源记录无漂移。成功场景和导入没有运行时退出警告。

共享InteractionRoom修改能精确反向还原前23项版本，保留已有重开、选择、日历和光照改动。只有本次守卫和显式验证入口是本分支内容；本分支独立基于main，不依赖其它迭代。生成的其它资源import/uid不纳入提交。结构化审计见autosave-placement-combined24-20261106.json。组合通过不代表人工视觉、个人历史存档或远端CI通过，最终发布身份以本报告所在提交为准。

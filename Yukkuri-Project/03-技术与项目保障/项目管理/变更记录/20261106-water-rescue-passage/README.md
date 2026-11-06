# 同伴为落水者保留上岸通道

日期：2026-11-06。基线：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea。
分支：alice/20261106-water-rescue-passage。验证机器：Shiki-5060Ti-PC，.NET 10.0.402，Release。

## 已复现问题
原NaturalLife.Soak的woodland场景、seed 20261029中，松果第二次落水后，同伴占据全部三个上岸落点以及近岸推进点。
1676秒冻结状态原样重放在1792.5秒死亡；只移动两同伴的坐标/目标点，其他状态与起始RNG相同，则在1683.5秒自行上岸。这证明本次同伴占位造成可避免的阻塞，不等于任何落水都必须获救。

## 修复边界
- 善意、实际有行动余力的同伴以原RoomMove逐步让出中央落点及最后推进走廊。照护职责保留，不以照护为理由永远站在落点上。
- 主动接应目标按双方身体半径留出空间，避开其他可见落水者的通道。实际接应也检查是否抵达同一安全点，不再使用可能小于身体半径和的固定1.5中心距；近岸限制、冷却和随机成功率不变。
- 使用原全身碰撞、地形与移动速度限制。不能移动、休息、重伤、手控、饥渴、严重疼痛/恐惧或自保逃离等优先状态不强制让路。
- 恶意同伴不自动变为善意救援者。没有取消碰撞、瞬移同伴、修改事故概率、伤害或随机脱险成功率，没有保证必然生存。
- 通道意图为派生查询，不新增存档字段。

## 本轮专项证据
同一最终16项专项在原基线为9通过、7失败；修复为16/16通过。
覆盖单/多同伴、两个落水目标、实际有界位移、不穿身体/地形、照护职责保留、存读档、手控、暂停、休息、重伤、不可行动、非善意及自身生存需求。
真实1676秒快照在最终修复后1681秒上岸，生命99.8684375，体力26.72425；仍承受水中伤害。
原24场景NaturalLife.Soak：修复后24/24通过。原Interaction.WaterSupport：12/12通过，未修改其断言。
独立审阅发现初版安全站位会使旧1.5接应距离不可达，已同步修正实际接应条件。新增幼体和成人从远处导航后真正接应成功、远处不能援助、目标移动使旧站位失效的回归。幼体原路径在基线通过，成人原几何矛盾在基线失败；均在最终候选通过。
早期候选曾覆盖饥饿助手优先级，已通过限制让路资格修正，失败证据保留，没有修改旧检查来迁就。

专项的公开入口：工程基线checks/Interaction.WaterPassage。
WaterBlockedSnapshot.json是本仓库固定种子模拟生成的合成测试状态，不是用户私人存档。完整原始日志及源码清单保留在独立工程.local/testing/water-passage-20261106。

重现：在工程基线执行锁定dotnet版本：
dotnet run --project checks/Interaction.WaterPassage/Interaction.WaterPassage.csproj -c Release -p:UseAppHost=false
dotnet run --project checks/NaturalLife.Soak/NaturalLife.Soak.csproj -c Release -p:UseAppHost=false -- .local/testing/water-soak.json

## 最终独立与组合验证
冻结后的broad-v2独立水险分支共运行58个纯逻辑检查项目：57通过，1个Interaction.Continuity失败与原基线完全一致，为既有失败，不归为本分支全绿。Game Debug编译0错误、2警告；运行前后374项源码清单全部一致。
四项修复另在detached测试副本中组合验证，60/60纯逻辑检查项目及Game编译通过；378项源码清单前后一致。组合只用于兼容性检查，没有执行真实git合并，也没有改变main。
组合副本另以Godot 4.7.2执行headless editor import及真实InteractionRoom场景，均exit 0；场景报告PASS，覆盖商店锁定控件、确认刷新、出售结算、角色移除、暂停保持、食物购买和存档验证。相关引擎日志无ERROR/WARNING/Exception，之后378项源码仍一致。这不是人工画面、鼠标操作或手感验收。
汇总见本目录check-results.json。独立工程原始证据位于工程基线.local/testing/water-passage-20261106/broad-v2/results.json；组合测试副本原始证据位于其工程基线.local/testing/combined-20261106-v2/results.json、godot-import.txt、godot-scene.txt及economy-scene.txt。
源码保持最终v2冻结。git diff --check仍有CreaturePersonality.cs文件尾额外空行提示；未为样式修改已验证源码，不宣称diff检查全通过。独立分支交付，不改main，是否合并由用户决定。
## 证据限制
输入采样间隔0.1秒不等于每次实际推进0.1秒。默认模拟tick是0.25秒；位移上限按WorldSeconds实际差计算，不放宽速度约束。
调整同伴位置/行为后，后续随机调用可自然分歧。冻结快照与原种子完整场景分别验证，不将不同轨迹冒充完全相同随机序列。
未进行人工Godot画面、原生输入或手感验收；组合副本的headless原生场景范围见上文。不保证所有地形和拥挤情形均可救援。独立分支交付，不改main，不替用户合并。


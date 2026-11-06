# 排泄连续性测试观测修正

日期：2026-11-06。
源码基线：e8d96a78f49182eb17e269e2b62a738c7a9bb7ea。
分支：alice/20261106-continuity-observation。
验证环境：Shiki-5060Ti-PC，.NET SDK 10.0.402，Release。

## 结论
本轮只修改工程基线 checks/Interaction.Continuity/Program.cs，不修改产品代码。
原始完整连续性检查为128/129通过；修改后129/129通过。原失败不是没有排泄，而是测试在较晚时刻仍要求尿物存在，未考虑已经正常发生的自主清洁。

## 实际观测
原有前置步骤仍保留：先推进3个0.1秒步长，验证悬空、下降或滑动时未开始排泄、膀胱需要未减少、无尿物。
随后从继续观察窗口起逐步采样，每步0.1秒：
- suspended：第37步（窗口内3.7秒）完成一次排泄且存在唯一尿物；第55步（5.5秒）尿物已被移除，活动为“自己打扫卫生”。
- lowering：第35步（3.5秒）完成一次排泄且存在唯一尿物；第52步（5.2秒）尿物已被移除，活动为“自己打扫卫生”。
- sliding：第27步（2.7秒）完成一次排泄且存在唯一尿物。
这些时间是测试继续窗口内的采样时刻，不是从游戏启动起计时，也不是精确到子步长的发生时刻。

## 新断言
在原80步继续窗口内，逐步验证未落稳不能开始排泄或消耗需要；首次完成时身体已落地静止、计数恰好增加一次、存在唯一尿物且负担已释放；随后不能重复排泄或生成替代尿物。
正常自主清洁可以移除该尿物，不再要求其在第80步仍存在。没有关闭清洁、缩短观察总时长或改动产品规则来迁就测试。

## 关联验证
- Interaction.Smoke：48/48。
- Interaction.AutonomyPriority：12/12。
- Interaction.CompletedNeeds：1/1。
- Interaction.Shelters：13/13。
- git diff --check通过。测试源码在关联验证前后SHA256相同：8FDAA24F182A474997C89C8F579BE98679821C110289151694B19014DD72F7B0。

## 重现命令
在工程基线目录，使用锁定版本dotnet：

dotnet run --project checks/Interaction.Continuity/Interaction.Continuity.csproj -c Release -p:UseAppHost=false -- .local/testing/continuity-observation-20261106/fixed.json

关联项目使用同样参数，将项目名分别替换为Interaction.Smoke、Interaction.AutonomyPriority、Interaction.CompletedNeeds、Interaction.Shelters，并给各自独立输出路径。
原始源码结果保存在同一证据目录的baseline.json、baseline.txt；修复后为fixed.json、fixed.txt。报告旁的diagnostics.txt保留本轮关键采样输出。

## 边界
本轮是纯.NET规则与测试修复，未启动Godot游戏或进行原生输入、画面、手感验证，不是全游戏验收。没有改主分支、合并或部署。

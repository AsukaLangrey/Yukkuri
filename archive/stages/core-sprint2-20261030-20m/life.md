# life 第二轮交接

已修复排泄期间错误冻结疾病、护理、受伤恢复和装置计时的问题，并接入生理作者提供的统一中断清理。新增六个检查块（共十三种场景），全部未运行。只修改 `InteractionBehavior.cs`、`InteractionRest.cs`、`checks/NaturalLife.Smoke/Program.cs` 和本文档；未写 `InteractionPhysiology.cs`、`WorldSimulation.cs` 或他人文件，未暂存、提交、推送、运行或桌面操作。

## 具体问题与修补

默认小院普通更新为 `WorldSimulation.Tick -> TickInteractionWorld -> TickPhysiology`。新增生理接线原在 `TickInteractionWorld` 的 `UpdateInteractionRest` 之前检查 `Physiology.Active` 并直接 `continue`。所以排泄的 2.5/3.5 秒期间，饥饿体力消耗仍进行，但整个疾病更新、护理时效、底部伤恢复延迟、受伤恢复、表面干燥与固定装置计时全部暂停。

现在将该分支下移到身体状态与固定装置的被动效果更新之后、求助和自主动作之前。排泄仍阻止自主移动、吃喝、练习和普通求助，但不冻结身体时间。装置累计时间、包裹原有的被动体力变化、非包裹的恐惧增长只移动执行位置，数值不变。

为了不因分支下移而获得睡眠收益，`UpdateInteractionRest` 在清掉 `Resting` 后立即排除正在排泄的情况：不增加睡眠体力，不消耗待完成的休息时长，也不建立新的休息请求。同时保留排泄期间原有跟随意图，等排泄结束后再按既有休息优先级决定是否取消持续跟随。

`NaturalLife.cs` 和 `InteractionWorld.cs` 本轮未修改。原有急需食水阈值、摄入规则、身体可移动条件、资源库存结算、上一轮目标恢复修复均保留。

## 生理作者协同与更新顺序边界

按协调者最终统一签名 **`private bool SynchronizeInterruptedPhysiology(Creature c)`** 接线。已在 `AdvanceInteractionPresentation` 的角色循环取得 `Interaction` 后立即调用；普通 `TickInteractionWorld` 的角色循环也在死亡跳过前调用，忽略返回值。定义由 physiology 作者维护（由其旧名 `CancelInterruptedExcretion` 重命名），没有复制中断条件。调用在展示吞咽倒计时之前，避免最后一点进食时间先归零而漏掉中断。普通生理循环也使用同一方法。

协调者已提醒世界更新早于生理更新；以下是在当前读取版本中确认的边界，请转交生理作者，不宜在多个文件复制一套中断条件：

1. **排泄开始的同一步仍残留 `Resting=true`：生理作者已修**。疲劳角色先在 `TickInteractionWorld` 休息，随后 `TickPhysiology` 达阈值开始排泄。已静态确认生理作者在开始分支设置 `i.Resting=false`；本任务的休息入口检查阻止后续世界步继续睡眠。前面已完成的时间步休息收益属于开始前的区间，没有倒扣体力或重算时间。新增检查也明确要求第一排泄帧不保留睡眠状态。
2. **中断后的首个世界步会读取旧 Active：已接线修复**。例如排泄中已经开始进食或托起，现在先调用共享清理再选择行为，旧 Active 不再抢占这一世界步。特写也会执行同一清理，不需要等待回到普通时间；重伤/死亡在展示和普通入口均覆盖。生理作者维护的 Lowering/Height/速度条件通过该方法统一复用。

两点的代码协同均已落盘，静态确认方法定义、三个调用及开始帧清理存在。没有擅自写入生理文件。生理期间的站姿、真实手势和特写中断画面均未验证。

## 新增检查源码

六个新增 `Check` 块位于 `NaturalLife.Smoke` 的 `Reload` 辅助方法之后、原跟随检查之前，不修改已有断言：

1. `excretion keeps digestive care and injury recovery advancing in ordinary time`：恢复期角色有症状、护理、底部治疗与恢复倒计时；自然启动排泄后经过一秒，症状与护理时效、底部治疗延迟、伤势恢复时间均继续下降，身体坐标不变且无休息状态。旧提前跳过逻辑会冻结这些计时，能区分旧错新对。
2. `boxed excretion retains restraint time and fear without repeating help over the body action`：箱中角色自然开始排泄，装置时间及恐惧继续积累；把求助计时设为已到期也不应让普通求助抢占排泄。这同样区分旧冻结逻辑。
3. `excretion suspends sleep benefits and then resumes the pending rest`：疲劳、正在地面休息时自然启动排泄，期间体力只消耗且休息剩余时间保持；完成后继续实际休息并恢复体力，防止移动分支时引入同时睡眠的回归。
4. `urgent nearby food and water resume after excretion for free and boxed creatures`：自由/箱中 × 急需食物/水四种组合。在排泄中途把需求降到 10 并加入嘴边真实来源；期间不得同时入口，完成后需实际摄入一份且需求超过 15，身体坐标保持。场景使用前方 (0,.4) 的可达来源，不依赖寻找或随机探索。

5. `close-up cancels interrupted excretion without advancing or satisfying its needs`：特写中托起、实际饮水入口接受一口水、仰面、重伤、死亡五种组合；一帧展示后清掉排泄动作，尿意便意、世界时间和完成次数保持，无排泄物凭空生成。
6. `ordinary world updates cancel stale excretion before choosing the active mouthful activity`：排泄后出现已经启动的一口水，直接调用普通时间入口、不调用展示入口；第一步就应选择喝水活动文字，不能残留排泄或“刚中断”文字，没有满足排泄需求。

总计十三种组合，均未运行。原 `Interaction.Continuity` 的生理与托举检查属于 contact，不在本任务修改范围。

## 静态复核和远程待测

已核对相处场景 .25 秒世界步、展示推进摄入时长、疾病护理恢复倍率、固定装置求助分支、嘴边摄入路径和普通时间生理调用顺序。新检查通过真实阈值启动排泄，没有直接预设已完成状态。现有露天/箱中前方嘴边取食路径被原检查覆盖；本轮把它与排泄结束后的控制权衔接组合起来。

Alice/dot 需记录最终整合的完整源码提交与实际机器，运行 NaturalLife.Smoke，并联检 Interaction.Continuity 的排泄、特写冻结、中断、托举与落地案例。重点复测已接线的首帧同步：休息转排泄同一步 `Resting=false`；排泄中托起/开始进食，首个输出帧不再声称仍在排泄；松开后未满足需求可重试。

本任务精确差异：Behavior 两处同步调用、排泄期间保留跟随意图、装置被动效果前移、在身体更新后阻断自主动作；Rest 一处正在排泄时返回；NaturalLife.Smoke 开头六个新增检查块。基线之后已有的 `InteractionWorld.CanSelfMove` 排泄条件属于原生理作者，life 本轮未改；不要把原先工作区整体都归入本任务。

编译、模拟检查、游戏、真实输入及画面全部未运行。旧试玩聊天的编译记录不覆盖本轮。

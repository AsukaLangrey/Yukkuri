using System.Text.Json;
using Yukkuri.Simulation;

// Source only here. Execute solely on an independently authorised test host.
int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception error){failures++;Console.WriteLine("FAIL "+name+": "+error);}}
void Tick(WorldSimulation sim,double seconds,bool keepHand=false)
{
    for(int n=0;n<(int)Math.Round(seconds*20);n++)
    {
        if(keepHand)sim.SetInteractionHand(sim.Selected!.X,sim.Selected.Z+.5,true);
        sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);
    }
}
WorldSimulation Fresh(bool readyPeers=false)
{
    var sim=WorldSimulation.CreateInteractionPlaytest();sim.State.RoomObjects.Clear();var c=sim.Selected!;
    c.Fullness=c.Interaction.Hydration=90;c.Energy=80;c.Fear=0;c.Trust=80;
    c.Interaction.NextAutonomyAt=0;c.Interaction.NuzzleFamiliarity=30;
    if(readyPeers)
        foreach(var other in sim.State.Creatures.Where(x=>x!=c))
        {
            other.Fullness=other.Interaction.Hydration=90;other.Energy=80;other.Fear=0;
            other.Interaction.NextAutonomyAt=10000;other.Interaction.ToyInterest=0;
        }
    sim.SetInteractionHand(c.X,c.Z+.5,true);return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void ExpectNuzzle(WorldSimulation sim)
{
    var c=sim.Selected!;
    Require(c.Interaction.ReactionSituation=="request-nuzzle"&&InteractionPresentation.Pose(c)=="invite"&&
        InteractionPresentation.VoiceCue(c)=="coo-nuzzle"&&c.Speech.Length>0,"未实际发出一致的自主蹭脸回应");
}
void WaitForCompanion(WorldSimulation sim)
{
    // A failed candidate search may have scheduled its normal 12-second retry.
    // Leave that cooldown intact; do not assume selection on one exact frame.
    for(int n=0;n<80&&sim.Selected!.Family.CompanionTargetId==null;n++)Tick(sim,.25,true);
    Require(sim.Selected!.Family.CompanionTargetId!=null,"20秒内未恢复正常自动相处");
}

Check("默认三油仍能在半秒内发出到期的自主蹭脸回应",()=>
{
    var sim=Fresh();Require(sim.State.Creatures.Count==3,"意外隔离掉默认同伴");Tick(sim,.5);ExpectNuzzle(sim);
    Require(sim.Selected!.Family.CompanionTargetId==null,"短回应当帧被自动同伴抢走");
});
Check("反转遍历顺序也不能让另一只抢走待回应者",()=>
{
    foreach(bool reverse in new[]{false,true})
    {
        var sim=Fresh(true);if(reverse)sim.State.Creatures.Reverse();
        Tick(sim,.5);ExpectNuzzle(sim);
        Require(sim.Selected!.Family.CompanionTargetId==null,"候选端没有保护已到期的回应："+reverse);
    }
});
Check("短回应窗口完整保留，结束后手仍在也可以自动结伴",()=>
{
    var sim=Fresh(true);var c=sim.Selected!;Tick(sim,.5,true);ExpectNuzzle(sim);
    double next=c.Interaction.NextAutonomyAt;Tick(sim,1,true);ExpectNuzzle(sim);
    Require(c.Family.CompanionTargetId==null&&c.Interaction.NextAutonomyAt==next,"下一帧覆盖或重发短回应");
    WaitForCompanion(sim);
    Require(c.Family.CompanionTargetId!=null,"短回应结束后永远拒绝同伴或把手持续在场当成永久占用");
    Require(c.Interaction.NextAutonomyAt==next,"自动结伴靠重置旧自主冷却达到");
});
Check("显式相处及孕育准备保持优先，不被到期短回应拆散",()=>
{
    foreach(bool pregnancy in new[]{false,true})
    {
        var sim=Fresh(true);var c=sim.Selected!;var other=sim.State.Creatures.First(x=>x!=c&&!x.IsYoung);
        c.Family.Peers[other.Id]=new(){Familiarity=90,Affection=90};other.Family.Peers[c.Id]=new(){Familiarity=90,Affection=90};
        if(pregnancy)sim.RequestPregnancy(c.Id,other.Id,BirthMethod.Stem);else sim.InviteCompanion(c.Id,other.Id);
        Require(c.Family.CompanionTargetId==other.Id,"既定相处前置失败");Tick(sim,.5,true);
        Require(c.Family.CompanionTargetId==other.Id&&other.Family.CompanionTargetId==c.Id&&
            c.Interaction.ReactionSituation!="request-nuzzle"&&c.Interaction.NextAutonomyAt==0,"短自主回应抢断既定家庭任务");
        if(pregnancy)Require(c.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"孕育准备被短回应取消");
    }
});
Check("普通时间已经开始的相处不会因自主计时到期而被抢断",()=>
{
    var sim=Fresh(true);var c=sim.Selected!;c.Interaction.NextAutonomyAt=10000;
    Tick(sim,.5,true);string? companion=c.Family.CompanionTargetId;Require(companion!=null,"自动相处前置失败");
    c.Interaction.NextAutonomyAt=0;Tick(sim,.5,true);
    Require(c.Family.CompanionTargetId==companion&&c.Interaction.ReactionSituation!="request-nuzzle","到期回应打断已有自动相处");
});
Check("吃喝休息和危险逃离优先于短回应",()=>
{
    foreach(string mode in new[]{"meal","water","rest","flight"})
    {
        var sim=Fresh(true);var c=sim.Selected!;
        if(mode=="meal"||mode=="water")c.Interaction.BeginIntake(mode=="meal"?IntakeKind.Meal:IntakeKind.Water);
        if(mode=="rest"){c.Interaction.RestRemaining=4;c.Interaction.Resting=true;}
        if(mode=="flight"){c.Family.FlightRemaining=4;c.Family.FlightX=-5;c.Family.FlightZ=-3;}
        Tick(sim,.5,true);
        Require(c.Interaction.ReactionSituation!="request-nuzzle"&&c.Interaction.NextAutonomyAt==0,"短回应抢占需要或危险："+mode);
        Require(c.Family.CompanionTargetId==null,"需要或危险期间反而新开自动相处："+mode);
    }
});
Check("收纳宝物读档后归属仍在且自主惦记真正有机会表达",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveTreasure)).Success,"宝物前置失败");
    var item=sim.State.RoomObjects.Single(o=>o.Kind==RoomObjectKind.Treasure);Require(sim.PutAwayRoomObject(item.Id),"收纳失败");
    string id=item.Id;sim=Reload(sim);c=sim.Selected!;c.Fullness=c.Interaction.Hydration=90;c.Interaction.NextAutonomyAt=0;
    Tick(sim,.5);
    Require(sim.State.RoomObjects.Any(o=>o.Id==id&&o.Stored&&o.OwnerId==c.Id),"宝物身份、归属或收纳状态丢失");
    Require(c.Interaction.ReactionSituation=="treasure-missing","宝物状态保留但自主反应被抢占");
});
Check("暂停保留短回应窗口，恢复后才轮到后续家庭行为",()=>
{
    var sim=Fresh(true);Tick(sim,.5,true);ExpectNuzzle(sim);var c=sim.Selected!;
    double window=c.Interaction.ReactionPoseRemaining,world=sim.State.WorldSeconds;sim.State.Settings.Pause=true;
    Tick(sim,5,true);Require(c.Interaction.ReactionPoseRemaining==window&&sim.State.WorldSeconds==world&&c.Family.CompanionTargetId==null,"暂停推进短回应或新建相处");
    sim.State.Settings.Pause=false;WaitForCompanion(sim);Require(c.Family.CompanionTargetId!=null,"恢复时间后仍无法回到正常相处");
});
Check("读档不重放已经发出的短回应，但到期未发出的回应仍可发生",()=>
{
    var sim=Fresh(true);Tick(sim,.5,true);ExpectNuzzle(sim);double next=sim.Selected!.Interaction.NextAutonomyAt;
    var loaded=Reload(sim);var c=loaded.Selected!;
    Require(c.Interaction.ReactionSituation==""&&c.Interaction.ReactionPoseRemaining==0&&c.Interaction.NextAutonomyAt==next,"读档重放短回应或丢失自主冷却");
    Tick(loaded,.5,true);Require(c.Interaction.ReactionSituation!="request-nuzzle"&&c.Family.CompanionTargetId!=null,"读档凭空重发回应或永久占用相处");
    var pending=Reload(Fresh(true));Tick(pending,.5,true);ExpectNuzzle(pending);
});
Check("已有的人油陪伴完成实际相处前不被自动选伴抢走",()=>
{
    foreach(bool reverse in new[]{false,true})
    {
        var sim=Fresh(true);var c=sim.Selected!;c.Attachment=55;
        c.Interaction.HumanBond.Familiarity=c.Interaction.HumanBond.Safety=15;
        c.Interaction.HumanBond.LastHarmAt=-100;if(reverse)sim.State.Creatures.Reverse();
        Tick(sim,.5,true);Require(c.Interaction.HumanCompany.Until>0,"主动人油陪伴前置失败");
        Tick(sim,7,true);
        Require(c.Interaction.HumanCompany.Remembered&&c.Interaction.HumanCompany.ContactSeconds==6&&
            c.Family.CompanionTargetId==null,"实际陪伴尚未完成就被自动同伴截走："+reverse);
        Tick(sim,13,true);WaitForCompanion(sim);
        Require(c.Interaction.HumanCompany.Until==0,"新同伴建立后仍遗留过期的人油陪伴占用");
        SaveStore.Validate(sim.State);
    }
});
Check("第一颗球很远时第二颗近球不能虚构一个优先球动作",()=>
{
    var sim=Fresh(true);var c=sim.Selected!;
    sim.State.RoomObjects.Add(new(){Id="far-first-ball",Kind=RoomObjectKind.Ball,RegionId=c.RegionId,X=5,Z=5});
    sim.State.RoomObjects.Add(new(){Id="near-second-ball",Kind=RoomObjectKind.Ball,RegionId=c.RegionId,X=c.X,Z=c.Z+1.3});
    Tick(sim,.5,true);ExpectNuzzle(sim);
    Require(c.Family.CompanionTargetId==null,"把实际球分支不会处理的第二颗球当成短回应不可执行");
});
Check("真正近处球和水面球使短回应不可执行时不凭空预约回应",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var sim=Fresh(true);var c=sim.Selected!;var other=sim.State.Creatures.First(x=>x!=c&&!x.IsYoung);
        if(water)
        {
            c.X=c.TargetX=4.5;c.Z=c.TargetZ=2.3;other.X=other.TargetX=2.5;other.Z=other.TargetZ=2.3;
            sim.State.RoomObjects.Add(new(){Id="river-marker",Kind=RoomObjectKind.RiverWater,RegionId=c.RegionId,X=6.5,Z=2.3});
        }
        var ball=new RoomObject{Id="actual-ball",Kind=RoomObjectKind.Ball,RegionId=c.RegionId,
            X=water?WoodlandLayout.RiverEdge(2.3)+.3:c.X,Z=water?2.3:c.Z+1.3};
        sim.State.RoomObjects.Add(ball);
        Require(RoomBallEnvironment.InWater(sim.State,ball)==water&&WorldSimulation.Distance(c.X,c.Z,ball.X,ball.Z)<3,
            "近球的水陆分支前置条件不符："+water);
        Tick(sim,.5,true);
        Require(c.Family.CompanionTargetId==other.Id&&c.Interaction.ReactionSituation!="request-nuzzle", "为到不了的短回应挡住原有家庭选择："+water);
    }
});

Environment.ExitCode=failures==0?0:1;

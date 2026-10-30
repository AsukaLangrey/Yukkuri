using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

// Remote-only source. Do not run on the original development workstation.
// Initial needs/scars isolate the behavior; every fresh wound uses real Hurt and
// every attempt/result comes from ordinary Advance. No fixture sets Success.
int failures=0;
void Require(bool value,string message){if(!value)throw new Exception(message);}
void Near(double a,double b,string message,double tolerance=1e-7)=>Require(Math.Abs(a-b)<=tolerance,message+$" ({a} != {b})");
void Check(string name,Action check)
{try{check();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture(ulong seed=1,bool legacy=false)
{
    ulong distributed=unchecked(seed*11400714819323198485UL);
    var sim=legacy?WorldSimulation.CreateMilestone(seed:distributed):WorldSimulation.CreateInteractionPlaytest(distributed);
    var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);
    sim.State.UnlimitedResources=false;sim.State.RoomObjects.Clear();
    sim.State.Settings.Accidents=false;sim.State.Settings.Invasions=false;
    sim.State.AutonomousBreeding=false;
    foreach(var region in sim.State.Regions){region.Facilities.Clear();region.StaffEnabled=false;}
    sim.ActiveRegion.Kind=RegionKind.Wilderness;
    c.Fullness=c.Energy=c.Interaction.Hydration=100;c.Health=100;c.Pain=c.Fear=0;c.Trust=80;
    c.X=c.Z=c.TargetX=c.TargetZ=0;c.IsYoung=false;c.IsElder=false;
    c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
    c.DecisionIn=c.BehaviorCooldown=c.Interaction.NextAutonomyAt=10000;
    c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    c.Interaction.ToyInterest=0;
    return sim;
}
void Wound(WorldSimulation sim,BodyPart part=BodyPart.Side,SurfaceSide side=SurfaceSide.Whole)
{
    var c=sim.Selected!;sim.Hurt(c,8,"野外表面擦伤",null,part,side);
    Require(c.Interaction.Injuries.At(part,side) is {Damage:>0,SelfCareAttempted:false},"真实受伤入口未创建新伤口");
}
void Start(WorldSimulation sim)
{
    for(int n=0;n<20&&!sim.Selected!.Interaction.Injuries.SelfCare.Active;n++)Tick(sim,.25);
    Require(sim.Selected!.Interaction.Injuries.SelfCare.Active,"健康食水与野外表面伤口未在五秒内开始自主护伤");
}
WorldSimulation Branch(bool success,BodyPart part=BodyPart.Side,bool scar=false)
{
    // Search only deterministic initial seeds. It is a failure if either branch
    // cannot be reached; no roll/result is overwritten to manufacture coverage.
    for(ulong seed=1;seed<=512;seed++)
    {
        var sim=Fixture(seed);var c=sim.Selected!;
        if(scar)
        {
            c.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Bottom,PermanentLoss=40,Cause="旧伤留下的损失"});
            c.Interaction.FootIntegrity=60;
        }
        Wound(sim,part);Start(sim);
        if(c.Interaction.Injuries.SelfCare.Success==success)return sim;
    }
    throw new Exception("512个初始种子未找到"+(success?"成功":"失败")+"分支");
}
WorldState Copy(WorldState state)=>JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(state,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
WorldSimulation Reload(WorldSimulation sim)
{var state=Copy(sim.State);SaveStore.Validate(state);return new(state);}
void Reject(WorldState state,string message)
{
    try{SaveStore.Validate(state);}catch(InvalidDataException){return;}
    throw new Exception(message);
}

Check("真实受伤后停下来尝试，付精力但不立即护理或造物",()=>
{
    var sim=Fixture();var c=sim.Selected!;Wound(sim);double energy=c.Energy;
    int food=sim.State.Food,medicine=sim.State.Medicine;Start(sim);
    var wound=c.Interaction.Injuries.At(BodyPart.Side)!;var care=c.Interaction.Injuries.SelfCare;
    Near(care.Remaining,6,"开始帧偷扣了操作时间");
    Near(energy-c.Energy,3+sim.State.WorldSeconds*sim.Config.EnergyPerSecond,"启动没有一次支付精力");
    Require(wound.SelfCareAttempted&&wound.CareRemaining==0&&wound.Damage==8,"启动立即治疗或没有记住已尝试");
    Require(sim.State.Food==food&&sim.State.Medicine==medicine&&sim.State.RoomObjects.Count==0&&c.Interaction.Dressings.Count==0,"野外尝试创造或消耗了外部用品");
    double x=c.X,z=c.Z;Tick(sim,5);
    Require(care.Active&&wound.CareRemaining==0,"完成之前提前给护理效果");
    Near(c.X,x,"尝试期间仍走动");Near(c.Z,z,"尝试期间仍走动");
    SaveStore.Validate(sim.State);
});
Check("成功只有有限局部护理，伤口随后随时间恢复",()=>
{
    var sim=Branch(true);var c=sim.Selected!;var wound=c.Interaction.Injuries.At(BodyPart.Side)!;
    double damage=wound.Damage,health=c.Health;int medicine=sim.State.Medicine;
    Tick(sim,6);
    Require(!c.Interaction.Injuries.SelfCare.Active&&wound.CareRemaining>0&&wound.CareRemaining<=180,"成功没有有限护理或未结束");
    Near(wound.Damage,damage,"完成瞬间直接抹掉局部损伤");
    Require(c.Health-health<1&&c.Health<100,"自行护伤结算了全身回血");
    Require(sim.State.Medicine==medicine&&c.Interaction.Dressings.Count==0&&!c.Interaction.FootTreated,"自行护伤冒充专业护理");
    Tick(sim,10);Require(wound.Damage<damage&&wound.Damage>0&&wound.CareRemaining<180,"之后没有缓慢局部恢复");
    Tick(sim,180);
    Require(wound.CareRemaining==0&&wound.Damage>0&&!c.Interaction.Injuries.SelfCare.Active,"有限护理无限续期或修复全部伤势");
    SaveStore.Validate(sim.State);
});
Check("失败留下结果，旧伤再次受击也不能刷同次机会",()=>
{
    var sim=Branch(false);var c=sim.Selected!;var wound=c.Interaction.Injuries.At(BodyPart.Side)!;Tick(sim,6);
    Require(!c.Interaction.Injuries.SelfCare.Active&&wound.CareRemaining==0&&wound.SelfCareAttempted,"失败仍奖励护理或退回机会");
    Require(c.Interaction.Injuries.SelfCare.LastEvent.Contains("没有成功"),"失败结果不清楚");
    double energy=c.Energy;Tick(sim,15);
    Near(energy-c.Energy,15*sim.Config.EnergyPerSecond,"同一伤口重复付费或恢复了精力");
    sim.Hurt(c,1,"同一未愈伤口再次碰撞",null,BodyPart.Side);Tick(sim,2);
    Require(wound.SelfCareAttempted&&!c.Interaction.Injuries.SelfCare.Active&&wound.CareRemaining==0,"重复碰伤刷新了旧伤随机机会");
});
Check("保存中途锁存成败，继续不重抽不重复扣费",()=>
{
    foreach(bool success in new[]{false,true})
    {
        var original=Branch(success);Tick(original,2);var copy=Reload(original);
        var a=original.Selected!;var b=copy.Selected!;
        Require(a.Interaction.Injuries.SelfCare.Success==success&&b.Interaction.Injuries.SelfCare.Success==success,"存读重抽结果");
        Near(a.Interaction.Injuries.SelfCare.Remaining,b.Interaction.Injuries.SelfCare.Remaining,"存读重置剩余时间");
        Tick(original,7);Tick(copy,7);
        Near(a.Energy,b.Energy,"读档额外扣精力");Near(a.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining,b.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining,"同档护理时长分叉");
        Require(original.State.RandomState==copy.State.RandomState&&a.Interaction.Injuries.SelfCare.LastEvent==b.Interaction.Injuries.SelfCare.LastEvent,"同档随机连续或结果记录分叉");
        SaveStore.Validate(copy.State);
    }
});
Check("暂停和近看不推进，恢复普通时间才继续",()=>
{
    var sim=Branch(true);var c=sim.Selected!;Tick(sim,1);
    double remaining=c.Interaction.Injuries.SelfCare.Remaining,time=sim.State.WorldSeconds,energy=c.Energy;ulong rng=sim.State.RandomState;
    sim.State.Settings.Pause=true;Tick(sim,8);
    Near(c.Interaction.Injuries.SelfCare.Remaining,remaining,"暂停仍护伤");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;Tick(sim,8);
    Near(c.Interaction.Injuries.SelfCare.Remaining,remaining,"近看仍护伤");Near(sim.State.WorldSeconds,time,"冻结时推进了世界");
    Near(c.Energy,energy,"冻结仍花费精力");Require(sim.State.RandomState==rng,"冻结时重抽随机");
    var loaded=Reload(sim);loaded.State.Mode=PlayMode.Human;Tick(loaded,remaining);
    var resumed=loaded.Selected!;
    Require(!resumed.Interaction.Injuries.SelfCare.Active&&resumed.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining>0,"回到普通时间没有继续");
});
Check("未访问的野外仍推进，园内与未启用旧原型不启动",()=>
{
    foreach(bool legacy in new[]{false,true})
    {
        var sim=Fixture(7,legacy);Wound(sim);Start(sim);var c=sim.Selected!;
        sim.State.Regions.Add(new(){Id="elsewhere",Name="另一地区",Kind=RegionKind.Sanctuary});
        sim.State.ActiveRegionId="elsewhere";sim.State.SelectedId=null;Tick(sim,6);
        Require(!c.Interaction.Injuries.SelfCare.Active&&c.Interaction.Injuries.At(BodyPart.Side)!.SelfCareAttempted,"离开画面就冻结野外个体");
        SaveStore.Validate(sim.State);
    }
    var yard=Fixture();yard.ActiveRegion.Kind=RegionKind.Sanctuary;Wound(yard);Tick(yard,3);
    var resident=yard.Selected!;
    Require(!resident.Interaction.Injuries.SelfCare.Active&&!resident.Interaction.Injuries.At(BodyPart.Side)!.SelfCareAttempted,"园内误用野外自护");
    var prototype=WorldSimulation.Create(population:1);var first=prototype.Selected!;prototype.ActiveRegion.Kind=RegionKind.Wilderness;
    first.Fullness=first.Energy=100;prototype.Hurt(first,8,"旧原型伤口",null,BodyPart.Side);Tick(prototype,2);
    Require(!first.Interaction.Injuries.SelfCare.Active,"不具备完整伤势时钟的旧原型启动自护");
});
Check("食水与口内动作优先，嘴部伤口不当成可自行处置表面",()=>
{
    foreach(string need in new[]{"food","water","mouthful","mouth-wound"})
    {
        var sim=Fixture();var c=sim.Selected!;Wound(sim,need=="mouth-wound"?BodyPart.Mouth:BodyPart.Side);
        if(need=="food")c.Fullness=50;
        if(need=="water")c.Interaction.Hydration=50;
        if(need=="mouthful")c.Interaction.BeginIntake(IntakeKind.Meal);
        Tick(sim,need=="mouthful"?.5:2);
        Require(!c.Interaction.Injuries.SelfCare.Active&&c.Interaction.Injuries.Sites.All(s=>!s.SelfCareAttempted),"饥渴或口部状态被自行护伤抢占："+need);
    }
    var legacy=Fixture(3,true);legacy.Selected!.Interaction.Hydration=0;Wound(legacy);Start(legacy);
    Require(legacy.Selected!.Interaction.Injuries.SelfCare.Active,"未启用口渴系统的旧模式被默认水分锁死");
});
Check("进行中身体需要会中断，暂停的拒绝操作不会取消",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var sim=Branch(true);var c=sim.Selected!;
        if(water)c.Interaction.Hydration=50;else c.Fullness=50;Tick(sim,.25);
        Require(!c.Interaction.Injuries.SelfCare.Active&&c.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining==0,"身体需要出现仍完成护伤");
    }
    var paused=Branch(true);var p=paused.Selected!;paused.State.Settings.Pause=true;
    Require(!paused.BeginInteractionGesture(p.Id,new(InteractionAction.Lift,BodyPart.Side,Height:1)).Success,"暂停接受新托举");
    Require(p.Interaction.Injuries.SelfCare.Active,"被拒绝的暂停操作打断既有护伤");
});
Check("离开原野外地区取消，回去也不能退回本次机会",()=>
{
    var sim=Branch(true);var c=sim.Selected!;
    sim.State.Regions.Add(new(){Id="shelter",Name="临时庇护处",Kind=RegionKind.Sanctuary});
    // Region transfer is a world-state fixture; cancellation still uses the real tick.
    c.RegionId="shelter";sim.State.ActiveRegionId="shelter";Tick(sim,.25);
    Require(!c.Interaction.Injuries.SelfCare.Active&&c.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining==0,"移出原地区仍保留自护");
    c.RegionId="home";sim.State.ActiveRegionId="home";Tick(sim,7);
    Require(!c.Interaction.Injuries.SelfCare.Active&&c.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining==0,"回原地区刷新已付出的尝试");
    SaveStore.Validate(sim.State);
});
Check("真实托举、冷藏、新伤与死亡立即取消未完成效果",()=>
{
    foreach(string interruption in new[]{"lift","cold","injury","death"})
    {
        var sim=Branch(true);var c=sim.Selected!;var wound=c.Interaction.Injuries.At(BodyPart.Side)!;
        if(interruption=="lift")Require(sim.BeginInteractionGesture(c.Id,new(InteractionAction.Lift,BodyPart.Side,Height:1)).Success&&c.Interaction.Carried,"前置托举失败");
        if(interruption=="cold"){sim.SetColdStorage(c.Id,true);Require(c.Family.ColdStorage.Stored,"前置冷藏失败");}
        if(interruption=="injury")sim.Hurt(c,1,"再次擦碰",null,BodyPart.Side);
        if(interruption=="death")
        {c.FillingIntegrity=1;sim.Hurt(c,30,"致命创伤",null);Require(c.Condition==PhysicalCondition.Dead,"前置未真实死亡");}
        Require(!c.Interaction.Injuries.SelfCare.Active&&wound.CareRemaining==0&&wound.SelfCareAttempted,"外部状态未立即取消自护："+interruption);
        SaveStore.Validate(sim.State);Tick(sim,7);
        Require(wound.CareRemaining==0,"中断后暗中结算了护理："+interruption);
    }
});
Check("外部护理接手才使用护理包与敷料",()=>
{
    var sim=Branch(true);var c=sim.Selected!;int medicine=sim.State.Medicine;
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.1);
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"真实护理入口失败");
    Require(!c.Interaction.Injuries.SelfCare.Active,"护理手势没有接管自护");
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(c.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining>0&&c.Interaction.HasDressing(BodyPart.Side),"外部护理没有真实照护伤处");
    Require(sim.State.Medicine==medicine-1,"外部护理未使用恰好一份护理包");
    SaveStore.Validate(sim.State);
});
Check("原有永久损失和足底功能不会被表面自护修复",()=>
{
    var sim=Branch(true,BodyPart.Bottom,scar:true);var c=sim.Selected!;
    double foot=c.Interaction.FootIntegrity,permanent=c.Interaction.Injuries.PermanentFootLoss;
    Tick(sim,12);
    Require(c.Interaction.Injuries.At(BodyPart.Bottom)!.CareRemaining>0,"前置未完成底部表面护伤");
    Near(c.Interaction.Injuries.PermanentFootLoss,permanent,"永久损伤被修复");Near(c.Interaction.FootIntegrity,foot,"自行护理冒充足底康复");
    Require(!c.Interaction.FootTreated&&c.Interaction.FootRecovery==0&&!c.Interaction.HasDressing(BodyPart.Bottom),"自护生成了专业护理或敷料");
});
Check("护理中的亲代不能被新照护请求抢走，拒绝不退自护机会",()=>
{
    var sim=Branch(true);var adult=sim.Selected!;
    var child=WorldSimulation.CreateInteractionPlaytest().State.Creatures[2];
    child.ParentId=adult.Id;child.Family.MotherId=adult.Id;child.X=1;child.Z=0;child.TargetX=1;child.TargetZ=0;
    child.Fullness=child.Interaction.Hydration=100;sim.State.Creatures.Add(child);
    string? reason=sim.FamilyCareReason(adult.Id,child.Id);double remaining=adult.Interaction.Injuries.SelfCare.Remaining;ulong rng=sim.State.RandomState;
    Require(reason!=null&&reason.Contains("护伤"),"照护入口没有解释自身护伤阻碍");
    sim.ArrangeCare(adult.Id,child.Id);
    Require(adult.Family.CareTargetId==null&&adult.Interaction.Injuries.SelfCare.Active,"被拒绝照护抢走或取消自护");
    Near(adult.Interaction.Injuries.SelfCare.Remaining,remaining,"被拒绝照护推进自护时间");Require(sim.State.RandomState==rng,"被拒绝照护重新抽签");
});
Check("表现查询纯读取，成败未完成时同姿势，真实动作和濒危优先",()=>
{
    foreach(bool success in new[]{false,true})
    {
        var sim=Branch(success);var c=sim.Selected!;string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
        for(int n=0;n<20;n++)Require(InteractionPresentation.Pose(c)=="guard","正在自行护伤没有护住伤处姿势");
        Require(before==JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),"表现查询推进或修改了模拟");
        // Deliberately conflicting presentation fixture tests priority only.
        c.Interaction.ReactionPose="carry";c.Interaction.ReactionPoseRemaining=1;
        Require(InteractionPresentation.Pose(c)=="carry","自护盖过尚在进行的真实反应");
        c.Condition=PhysicalCondition.Critical;Require(InteractionPresentation.Pose(c)=="help","自护盖过濒危求救");
        c.Condition=PhysicalCondition.Dead;Require(InteractionPresentation.Pose(c)=="dead","自护盖过死亡");
    }
});
Check("旧档缺省可读，损坏或不相容的进行状态拒绝",()=>
{
    var old=Fixture();Wound(old);
    var json=JsonNode.Parse(JsonSerializer.Serialize(old.State,SaveStore.JsonOptions))!.AsObject();
    var injury=json["Creatures"]![0]!["Interaction"]!["Injuries"]!.AsObject();
    injury.Remove("SelfCare");foreach(var site in injury["Sites"]!.AsArray())site!.AsObject().Remove("SelfCareAttempted");
    var loaded=json.Deserialize<WorldState>(SaveStore.JsonOptions)!;SaveStore.Validate(loaded);
    Require(!loaded.Creatures[0].Interaction.Injuries.SelfCare.Active&&!loaded.Creatures[0].Interaction.Injuries.Sites[0].SelfCareAttempted,"旧档无字段却生成已尝试或进行状态");
    var resumed=new WorldSimulation(loaded);Start(resumed);SaveStore.Validate(resumed.State);
    injury["SelfCare"]=null;Reject(json.Deserialize<WorldState>(SaveStore.JsonOptions)!,"显式空自护结构被接受");
    var active=Branch(true);
    foreach(string corruption in new[]{"time","attempt","site","region","cold","prototype"})
    {
        var state=Copy(active.State);var c=state.Creatures[0];var care=c.Interaction.Injuries.SelfCare;
        if(corruption=="time")care.Remaining=7;
        if(corruption=="attempt")c.Interaction.Injuries.Sites[0].SelfCareAttempted=false;
        if(corruption=="site")care.Part=BodyPart.Mouth;
        if(corruption=="region")state.Regions[0].Kind=RegionKind.Sanctuary;
        if(corruption=="cold")c.Family.ColdStorage.Stored=true;
        if(corruption=="prototype"){state.InteractionPlaytest=false;state.MilestoneScenario=false;}
        Reject(state,"不相容自护坏档被接受："+corruption);
    }
});

Console.WriteLine($"Wild self-care checks: {failures} failure(s).");
return failures==0?0:1;

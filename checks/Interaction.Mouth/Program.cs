using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Near(double actual,double expected,string reason)=>Require(Math.Abs(actual-expected)<.000001,reason+$" ({actual} != {expected})");
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
WorldSimulation Create()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    sim.State.Creatures.RemoveAll(x=>x.Id!=c.Id);sim.State.RoomObjects.Clear();sim.State.UnlimitedResources=false;
    c.X=0;c.Z=0;c.TargetX=0;c.TargetZ=0;c.Fullness=65;c.Energy=90;c.Trust=80;c.Interaction.Hydration=80;
    c.Interaction.NextAutonomyAt=10000;return sim;
}
LocalInjury Mouth(Creature c,double damage,double permanent=0)
{
    var wound=new LocalInjury{Part=BodyPart.Mouth,Damage=damage,PermanentLoss=permanent,CareRemaining=180,Cause="口部旧伤"};
    c.Interaction.Injuries.Sites.Add(wound);return wound;
}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
InteractionInput Offer(IntakeKind kind)=>new(kind==IntakeKind.Water?InteractionAction.Drink:
    kind==IntakeKind.Sweet?InteractionAction.FeedSweet:InteractionAction.FeedMeal,BodyPart.Mouth,Duration:.1);
int Accept(WorldSimulation sim,IntakeKind kind)
{
    var c=sim.Selected!;var input=Offer(kind);
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"递食开始被拒");
    int frames=0;
    while(!sim.InteractionIntakeAccepted&&frames<50)
    {Require(sim.ContinueInteractionGesture(input,true).Success,"等待入口时动作被拒");frames++;}
    Require(sim.InteractionIntakeAccepted,"持续递到嘴边后仍未接食");sim.EndInteractionGesture();return frames;
}
RoomObject Reserve(WorldSimulation sim,RoomObjectKind kind)
{
    var c=sim.Selected!;
    if(NaturalResources.IsWater(kind)){c.Interaction.Hydration=60;c.Fullness=90;}else{c.Fullness=65;c.Interaction.Hydration=90;}
    var obj=new RoomObject{Id="mouth-source",Kind=kind,RegionId=c.RegionId,X=.5,Z=0,Portions=1};
    sim.State.RoomObjects.Add(obj);sim.Advance(.25);
    Require(c.Interaction.NaturalIntakePending&&obj.Portions==0&&c.Interaction.Digestion.Exposures==0,"没有经自主入口预约真实份量");
    Require(sim.EnterCloseUp(),"无法进入近看隔离被动需求");return obj;
}

Check("健康与轻伤保持原速，严重口伤有有限功能而非全部禁食",()=>
{
    var sim=Create();var c=sim.Selected!;
    foreach(var kind in Enum.GetValues<IntakeKind>())Near(MouthFunction.IntakeRate(c,kind),1,"无伤改变了原速");
    var wound=Mouth(c,20);Near(MouthFunction.IntakeRate(c,IntakeKind.Meal),1,"轻伤门槛变慢");
    wound.Damage=100;
    Near(MouthFunction.IntakeRate(c,IntakeKind.Meal),.25,"食物最低速度错误");
    Near(MouthFunction.IntakeRate(c,IntakeKind.Sweet),.25,"甜食绕过功能限制");
    Near(MouthFunction.IntakeRate(c,IntakeKind.Water),.4,"水最低速度错误");
    Require(MouthFunction.IntakeRate(c,IntakeKind.Water)>0,"表面口伤被解释成完全无法获得水分");
});
Check("暂时与永久伤取较大值，恢复总血量不能覆盖永久口部限制",()=>
{
    var sim=Create();var c=sim.Selected!;var wound=Mouth(c,40,70);
    Near(MouthFunction.Impairment(c),70,"重复相加同一部位的临时与永久损伤");
    double rate=MouthFunction.IntakeRate(c,IntakeKind.Meal);wound.Damage=0;c.Health=100;c.Fullness=100;c.Trust=100;
    Near(MouthFunction.IntakeRate(c,IntakeKind.Meal),rate,"回血饱食或信任清掉永久限制");
    var loaded=Reload(sim).Selected!;Near(MouthFunction.Impairment(loaded),70,"永久口伤读档丢失");
});
Check("严重受限只影响表达清晰度，不凭空生成空台词或抹知识",()=>
{
    var sim=Create();var c=sim.Selected!;var wound=Mouth(c,59.9);
    Require(MouthFunction.CanSpeakClearly(c)&&MouthFunction.FormatSpeech(c,"你好。 ")=="你好。 ","尚能清晰说话时改写台词");
    wound.Damage=60;string text=MouthFunction.FormatSpeech(c,"你好。 ");
    Require(!MouthFunction.CanSpeakClearly(c)&&text.Length>0&&text!="你好。 ","严重受限仍完整发言或完全无表达");
    Require(MouthFunction.FormatSpeech(c,"")==""&&!MouthFunction.Describe(c).Any(char.IsDigit),"无台词时凭空出声或观察文案泄露数值");
    string family=JsonSerializer.Serialize(c.Family,SaveStore.JsonOptions);string memories=JsonSerializer.Serialize(c.Memories);
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.Greet)).Success,"口伤被误作不能听懂问候");
    Require(c.Speech==MouthFunction.FormatSpeech(c,c.Speech)&&c.Speech.Length>0,"实际回应绕过口部发音格式");
    Require(JsonSerializer.Serialize(c.Family,SaveStore.JsonOptions)==family&&JsonSerializer.Serialize(c.Memories)==memories,"口伤表达改变了知识亲缘或已有记忆");
});
Check("实际主食甜食饮水均延长接食等待且每次只消费一份",()=>
{
    foreach(var kind in Enum.GetValues<IntakeKind>())
    {
        var healthy=Create();var injured=Create();Mouth(injured.Selected!,80);healthy.EnterCloseUp();injured.EnterCloseUp();
        int healthyFrames=Accept(healthy,kind);int food=injured.State.Food;double full=injured.Selected!.Fullness,water=injured.Selected.Interaction.Hydration;
        var input=Offer(kind);Require(injured.BeginInteractionGesture(injured.Selected.Id,input).Success,"口伤递食开始失败");
        for(int n=0;n<healthyFrames;n++)injured.ContinueInteractionGesture(input,true);
        Require(!injured.InteractionIntakeAccepted&&injured.State.Food==food&&injured.Selected.Fullness==full&&injured.Selected.Interaction.Hydration==water,
            "口伤在健康入口时刻已消费或获得营养");
        for(int n=healthyFrames;n<50&&!injured.InteractionIntakeAccepted;n++)injured.ContinueInteractionGesture(input,true);
        Require(injured.InteractionIntakeAccepted,"口伤被永久拒绝进食");
        Near(injured.Selected.Fullness,full+(kind==IntakeKind.Meal?19:kind==IntakeKind.Sweet?9:0),"一份食物营养结算错误");
        Near(injured.Selected.Interaction.Hydration,Math.Min(100,water+(kind==IntakeKind.Water?28:0)),"一份饮水结算错误");
        Require(injured.State.Food==food-(kind==IntakeKind.Water?0:1),"入口份量消费错误");
        for(int n=0;n<20;n++)injured.ContinueInteractionGesture(input,true);
        Require(injured.State.Food==food-(kind==IntakeKind.Water?0:1),"保持手势重复扣份");injured.EndInteractionGesture();
    }
});
Check("手喂已入口后慢速吞咽不可覆盖，也不因收手退款",()=>
{
    foreach(var kind in new[]{IntakeKind.Meal,IntakeKind.Water})
    {
        var healthy=Create();var injured=Create();Mouth(injured.Selected!,80);healthy.EnterCloseUp();injured.EnterCloseUp();
        Accept(healthy,kind);Accept(injured,kind);var c=injured.Selected!;double full=c.Fullness,water=c.Interaction.Hydration;int food=injured.State.Food;
        Tick(healthy,1.8);Tick(injured,1.8);
        Require(healthy.Selected!.Interaction.EatingRemaining==0&&c.Interaction.EatingRemaining>0,"实际吞咽没有按口部功能减慢");
        Require(!injured.BeginInteractionGesture(c.Id,Offer(kind)).Success,"尚有一口时接受了下一份");
        Tick(injured,5);
        Require(c.Interaction.EatingRemaining==0&&c.Fullness==full&&c.Interaction.Hydration==water&&injured.State.Food==food,"吞咽完成重复结算或收手退食");
    }
});
Check("口伤等待接食时收手不消费，重新递来不能借旧停留时间",()=>
{
    var sim=Create();var c=sim.Selected!;Mouth(c,80);sim.EnterCloseUp();var input=Offer(IntakeKind.Meal);
    int stock=sim.State.Food;double full=c.Fullness;
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"递食夹具失败");
    for(int n=0;n<10;n++)sim.ContinueInteractionGesture(input,true);
    Require(!sim.InteractionIntakeAccepted,"严重口伤过早接食");sim.EndInteractionGesture();
    Require(sim.State.Food==stock&&c.Fullness==full&&c.Interaction.EatingRemaining==0,"入口前收手仍消费或产生嘴内动作");
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"重递夹具失败");
    for(int n=0;n<7;n++)sim.ContinueInteractionGesture(input,true);
    Require(!sim.InteractionIntakeAccepted&&sim.State.Food==stock&&c.Fullness==full,"重递复用了取消前的接触进度");
    sim.EndInteractionGesture();
});
Check("自主取食在真实口部接触前保持预约且取消只返还一次",()=>
{
    foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Water})
    {
        var sim=Create();var c=sim.Selected!;Mouth(c,80);var source=Reserve(sim,kind);double full=c.Fullness,water=c.Interaction.Hydration;
        Tick(sim,.5);
        Require(c.Interaction.NaturalIntakePending&&c.Interaction.Digestion.Exposures==0&&c.Fullness==full&&c.Interaction.Hydration==water,
            "口部尚未接到自主份量就结算营养");
        Require(sim.HoldSupply(source.Id),"无法拿走尚未入口的真实份量");
        Require(source.Portions==1&&!c.Interaction.NaturalIntakePending&&c.Interaction.EatingRemaining==0,"预约取消没有归还一次");
        Tick(sim,2);Require(source.Portions==1&&c.Interaction.Digestion.Exposures==0&&c.Fullness==full&&c.Interaction.Hydration==water,"取消后重放消费或退款");
        Reload(sim);
    }
});
Check("慢速自主入口读档续接只消费一次，几何进度不被加长成负年龄",()=>
{
    foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Water})
    {
        var sim=Create();var c=sim.Selected!;Mouth(c,80);Reserve(sim,kind);Tick(sim,.5);
        double remaining=c.Interaction.EatingRemaining,full=c.Fullness,water=c.Interaction.Hydration;
        Require(remaining>0&&remaining<=InteractionLife.IntakeDuration(c.Interaction.EatingKind)&&c.Interaction.NaturalIntakePending,
            "慢速使用了超出标准时长的负年龄进度");
        var copy=Reload(sim);Near(copy.Selected!.Interaction.EatingRemaining,remaining,"读档重置口部动作阶段");
        Tick(sim,5);Tick(copy,5);
        foreach(var world in new[]{sim,copy})
        {
            var oil=world.Selected!;Require(oil.Interaction.Digestion.Exposures==1&&oil.Interaction.EatingRemaining==0,"续接重复入口或无法咽完");
            Near(oil.Fullness,full+NaturalResources.FoodValue(kind),"续接食物营养守恒失败");
            Near(oil.Interaction.Hydration,water+NaturalResources.WaterValue(kind),"续接水分守恒失败");
        }
        Require(sim.State.RandomState==copy.State.RandomState,"同档续接额外结算了污染或其他摄入随机事件");
    }
});
Check("临时口伤通过真实护理和普通时间改善，近看不会修好",()=>
{
    var sim=Create();var c=sim.Selected!;c.Fullness=95;c.Interaction.Hydration=95;var wound=Mouth(c,25);wound.CareRemaining=0;
    var care=new InteractionInput(InteractionAction.Care,BodyPart.Mouth,Duration:.1);
    Require(sim.BeginInteractionGesture(c.Id,care).Success,"口部适用护理被拒");
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(care,true);sim.EndInteractionGesture();
    Require(wound.CareRemaining>0,"护理没有覆盖真实口部伤处");double before=wound.Damage,rate=MouthFunction.IntakeRate(c,IntakeKind.Meal);
    sim.EnterCloseUp();Tick(sim,5);Near(wound.Damage,before,"近看偷走伤口恢复时间");
    sim.ExitCloseUp();Tick(sim,30);
    Require(wound.Damage<before&&MouthFunction.IntakeRate(c,IntakeKind.Meal)>rate&&wound.PermanentLoss==0,"护理与普通时间未改善临时口部功能");
});
Check("慢速嘴内动作仍受暂停和冷藏入口约束",()=>
{
    var sim=Create();var c=sim.Selected!;Mouth(c,80);sim.EnterCloseUp();Accept(sim,IntakeKind.Meal);
    double remaining=c.Interaction.EatingRemaining,full=c.Fullness;sim.State.Settings.Pause=true;Tick(sim,5);
    Require(c.Interaction.EatingRemaining==remaining&&c.Fullness==full,"暂停推进了慢速口部动作");
    sim.State.Settings.Pause=false;sim.ExitCloseUp();sim.SetColdStorage(c.Id,true);
    Require(!c.Family.ColdStorage.Stored,"嘴里已有一口仍能直接进入冷藏");
    sim.EnterCloseUp();Tick(sim,5);sim.ExitCloseUp();sim.SetColdStorage(c.Id,true);
    Require(c.Family.ColdStorage.Stored,"咽完后无法正常冷藏");
    double rate=MouthFunction.IntakeRate(c,IntakeKind.Meal);int food=sim.State.Food;
    Require(!sim.BeginInteractionGesture(c.Id,Offer(IntakeKind.Meal)).Success&&!sim.Feed(c.Id)&&!sim.Feed(c.Id,true),"冷藏能绕过进食入口");
    Require(sim.State.Food==food&&c.Interaction.EatingRemaining==0,"冷藏拒绝仍消费或启动动作");
    Near(MouthFunction.IntakeRate(Reload(sim).Selected!,IntakeKind.Meal),rate,"冷藏保存抹掉口部受限");
});
Check("慢速自主预约在真实死亡时清理且保留未吃份量",()=>
{
    var sim=Create();var c=sim.Selected!;Mouth(c,80);var source=Reserve(sim,RoomObjectKind.Meal);Tick(sim,.5);
    c.FillingIntegrity=1;sim.Hurt(c,30,"致命身体撞击","player",BodyPart.Side);
    Require(c.Condition==PhysicalCondition.Dead&&!c.Interaction.NaturalIntakePending&&c.Interaction.EatingRemaining==0&&source.Portions==1,
        "死亡保留口部预约或吞掉未入口食物");
    double full=c.Fullness;Tick(sim,5);
    Require(source.Portions==1&&c.Fullness==full&&c.Interaction.Digestion.Exposures==0&&!sim.Feed(c.Id),"死亡后仍完成口部摄入");Reload(sim);
});
Check("单独底部永久失能不干扰口部速度和嘴边自主取食",()=>
{
    var sim=Create();var c=sim.Selected!;c.Interaction.FootIntegrity=20;
    c.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Bottom,PermanentLoss=75,Cause="底部旧伤"});
    Near(MouthFunction.IntakeRate(c,IntakeKind.Meal),1,"底部伤残错误影响口部功能");
    Require(MouthFunction.CanSpeakClearly(c),"不能走被当作不能说话");Reserve(sim,RoomObjectKind.Meal);Tick(sim,.5);
    Require(c.Interaction.Digestion.Exposures==1&&!c.Interaction.NaturalIntakePending&&c.X==0&&c.Z==0,"失能者无法吃到真实嘴边食物");
    Require(c.Interaction.FootIntegrity==20&&!WorldSimulation.CanSelfMove(c),"口部吃到食物直接修好了脚底");
});
Check("两种模式玩家与员工旧Feed都拒绝忙嘴连喂且不额外消费",()=>
{
    foreach(bool oldMode in new[]{false,true})foreach(bool staff in new[]{false,true})
    {
        var sim=oldMode?WorldSimulation.CreateMilestone():Create();var c=sim.Selected!;
        sim.State.Creatures.RemoveAll(x=>x.Id!=c.Id);foreach(var region in sim.State.Regions)region.Facilities.Clear();
        sim.State.UnlimitedResources=false;c.Fullness=40;c.Energy=90;c.Pain=0;c.BehaviorCooldown=10000;
        Mouth(c,0,70);int stock=sim.State.Food;double full=c.Fullness;
        Require(sim.Feed(c.Id,staff),"空嘴旧Feed拒绝有效喂食");
        Near(c.Interaction.EatingRemaining,1.6,"旧Feed没有启动标准嘴内进度");
        Near(c.Fullness,full+sim.Config.FeedGain,"旧Feed改变已确认的营养结算时点");
        double trust=c.Trust;
        Require(!sim.Feed(c.Id,staff)&&!sim.Feed(c.Id,!staff),"换玩家或员工绕过忙嘴");
        Require(sim.State.Food==stock-1&&c.Fullness==full+sim.Config.FeedGain&&c.Trust==trust,"忙嘴拒绝仍消费、加营养或加关系");
        Tick(sim,1.75);Require(c.Interaction.EatingRemaining>0&&!sim.Feed(c.Id,staff),"永久口伤按健康时长解锁连喂");
        Tick(sim,2);Require(c.Interaction.EatingRemaining==0&&sim.Feed(c.Id,staff),"慢速咽完后旧Feed无法继续");
        Require(sim.State.Food==stock-2,"下一份没有单独消费一次");
    }
});
Check("旧模式普通时间只推进一次嘴内动作，健康保留标准时长",()=>
{
    foreach(bool injured in new[]{false,true})
    {
        var sim=WorldSimulation.CreateMilestone();var c=sim.Selected!;
        sim.State.Creatures.RemoveAll(x=>x.Id!=c.Id);foreach(var region in sim.State.Regions)region.Facilities.Clear();
        c.Fullness=40;c.Energy=90;c.Pain=0;c.BehaviorCooldown=10000;
        if(injured)Mouth(c,0,70);
        Require(sim.Feed(c.Id),"普通时间夹具未喂食");double rate=MouthFunction.IntakeRate(c,IntakeKind.Meal);
        // This is the real caller pattern: presentation plus world time per frame.
        Tick(sim,.5);Near(c.Interaction.EatingRemaining,1.6-.5*rate,"旧普通时间吞咽被重复推进或完全不推进");
        Tick(sim,1);Near(c.Interaction.EatingRemaining,1.6-1.5*rate,"跨帧普通时间口部进度不守恒");
        Tick(sim,.25);
        Require(injured?c.Interaction.EatingRemaining>0:c.Interaction.EatingRemaining==0,"健康标准时长或永久受限时长错误");
    }
});
Check("旧模式近看能完成真实嘴内动作且不推进世界或伤口恢复",()=>
{
    var sim=WorldSimulation.CreateMilestone();var c=sim.Selected!;
    sim.State.Creatures.RemoveAll(x=>x.Id!=c.Id);foreach(var region in sim.State.Regions)region.Facilities.Clear();
    c.Fullness=40;c.Energy=90;c.Pain=0;var wound=Mouth(c,80);Require(sim.Feed(c.Id),"近看夹具未喂食");
    Require(sim.EnterCloseUp(),"旧模式无法进入近看");double now=sim.State.WorldSeconds,damage=wound.Damage,full=c.Fullness,energy=c.Energy;
    Tick(sim,.5);Near(c.Interaction.EatingRemaining,1.6-.5*MouthFunction.IntakeRate(c,IntakeKind.Meal),"旧近看不推进嘴内动作或重复推进");
    Tick(sim,4);
    Require(c.Interaction.EatingRemaining==0&&sim.State.WorldSeconds==now&&wound.Damage==damage&&c.Fullness==full&&c.Energy==energy,
        "旧近看吞咽不能完成或顺带推进了普通世界");
});
Check("入侵与守护真实喂食暂停主动行动但不冻结事件，咽完后恢复",()=>
{
    WorldSimulation Encounter()
    {
        var world=WorldSimulation.CreateMilestone();world.State.Settings.Invasions=true;
        foreach(var region in world.State.Regions){region.StaffEnabled=false;region.Facilities.Clear();}
        foreach(var oil in world.State.Creatures)
        {oil.GuardDuty=false;oil.BehaviorCooldown=9999;oil.DecisionIn=9999;oil.Fullness=90;oil.Energy=90;}
        var guardian=world.State.Creatures[1];guardian.GuardDuty=true;guardian.GuardSkill=0;
        world.State.Threat.NextAt=0;world.Advance(10.25);
        Require(world.State.Threat.Phase==ThreatPhase.Intruding&&world.State.Threat.DefenderId==guardian.Id,
            "真实入侵过程没有进入守护对峙");
        var intruder=world.State.Creatures.Single(c=>c.Id==world.State.Threat.IntruderId);
        foreach(var other in world.State.Creatures.Where(c=>c.RegionId=="home"&&c!=intruder&&c!=guardian))
        {other.X=other.TargetX=7;other.Z=other.TargetZ=-7;}
        intruder.X=intruder.TargetX=0;intruder.Z=intruder.TargetZ=0;
        guardian.X=guardian.TargetX=3;guardian.Z=guardian.TargetZ=0;
        world.State.Threat.EncounterCooldown=0;return world;
    }
    foreach(bool feedIntruder in new[]{true,false})
    {
        var sim=Encounter();var threat=sim.State.Threat;
        var intruder=sim.State.Creatures.Single(c=>c.Id==threat.IntruderId);
        var defender=sim.State.Creatures.Single(c=>c.Id==threat.DefenderId);
        var fed=feedIntruder?intruder:defender;Mouth(fed,0,70);fed.Fullness=40;
        if(feedIntruder){defender.X=defender.TargetX=1.2;}
        Require(sim.Feed(fed.Id),"对峙中的空嘴个体不能通过真实入口喂食");
        double x=fed.X,z=fed.Z,phase=threat.PhaseTime,defenderHealth=defender.Health;
        int attacks=sim.State.Incidents.Count(e=>e.Kind==IncidentKind.Attack&&e.ActorId==intruder.Id);
        Tick(sim,.5);
        Require(fed.Interaction.EatingRemaining>0&&fed.X==x&&fed.Z==z,"嘴仍忙时事件驱动器主动移动了被喂个体");
        Near(threat.PhaseTime,phase+.5,"嘴忙冻结了入侵事件时间");
        if(feedIntruder)
            Require(defender.Health==defenderHealth&&sim.State.Incidents.Count(e=>e.Kind==IncidentKind.Attack&&e.ActorId==intruder.Id)==attacks,
                "入侵者嘴仍忙时继续攻击守护者");
        for(int frame=0;frame<40&&fed.Interaction.EatingRemaining>0;frame++)sim.Advance(.25);
        Require(fed.Interaction.EatingRemaining==0,"事件中的真实嘴内动作无法咽完");
        sim.Advance(.25);
        Require(fed.X!=x||fed.Z!=z,"咽完后主动入侵或守护行为没有恢复");
    }
    var deadline=Encounter();var hostile=deadline.State.Creatures.Single(c=>c.Id==deadline.State.Threat.IntruderId);
    Mouth(hostile,0,70);hostile.Fullness=40;Require(deadline.Feed(hostile.Id),"事件截止夹具未实际喂食");
    deadline.State.Threat.PhaseTime=99.75;deadline.Advance(.5);
    Require(hostile.Interaction.EatingRemaining>0&&deadline.State.Threat.Phase==ThreatPhase.Retreating,
        "嘴忙分支提前返回，吞掉了原有事件超时退出");
});

Console.WriteLine($"Mouth: {failures} failure(s)");
return failures==0?0:1;

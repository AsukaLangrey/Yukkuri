using System.Text.Json;
using Yukkuri.Simulation;

// Independent authorised test host only. Never run on the development workstation.
int failures=0;
void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception error){failures++;Console.WriteLine("FAIL "+name+": "+error);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture(double gap=1.65)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261103);sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAt(2);
    sim.State.PlayerX=0;sim.State.PlayerZ=7;
    for(int n=0;n<2;n++)
    {
        var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Health=c.Interaction.Hydration=100;c.Pain=c.Fear=0;c.Trust=90;
        c.TrueDisposition="gentle";c.Patience=.8;c.Aggression=.05;c.Courage=.2;c.Body.SpeciesId="reimu";
        c.Body.ReproductionRole=n==0?ReproductionRole.Maternal:ReproductionRole.Paternal;
        c.X=c.TargetX=n*gap;c.Z=c.TargetZ=0;
        c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    }
    var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    m.Family.Peers[f.Id]=new(){Familiarity=90,Affection=90};f.Family.Peers[m.Id]=new(){Familiarity=90,Affection=90};
    m.Family.PartnerIds.Add(f.Id);f.Family.PartnerIds.Add(m.Id);return sim;
}
void Begin(WorldSimulation sim)
{
    string result=sim.RequestPregnancy(sim.State.Creatures[0].Id,sim.State.Creatures[1].Id,BirthMethod.Stem);
    Require(sim.State.Creatures[0].Family.Pregnancy.Phase==PregnancyPhase.Preparing,"准备入口失败："+result);
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
WorldSimulation Due()
{
    var sim=Fixture();var m=sim.State.Creatures[0];
    m.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,FatherId=sim.State.Creatures[1].Id,
        Elapsed=FamilyRules.GestationSeconds(BirthMethod.Stem),EventId="pregnancy-boundary-birth",PlannedChildren=2};
    return sim;
}
void Deliver(WorldSimulation sim)
{
    // Isolate the delivery boundary: ordinary behaviour may otherwise clear a
    // competing action before delivery gets a chance to enforce its own rule.
    typeof(WorldSimulation).GetMethod("TryDeliverFamily",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
        .Invoke(sim,new object?[]{sim.State.Creatures[0],sim.State.Creatures[1]});
}
int Babies(WorldSimulation sim)=>sim.State.Creatures.Count(c=>c.Family.BirthEventId=="pregnancy-boundary-birth");

Check("正常父母实际靠近后完整相处才能开始孕育",()=>
{
    var sim=Fixture(4);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];Begin(sim);
    Tick(sim,.25);Require(m.Family.Pregnancy.Elapsed==0,"远处只接到邀请就累计准备");
    for(int n=0;n<80&&m.Family.Pregnancy.Phase==PregnancyPhase.Preparing;n++)Tick(sim,.25);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Gestating,"正常实际靠近未能完成孕育准备");
    Require(Math.Abs(m.X-f.X)<2,"开始孕育时仍隔着远处");
});
Check("脚部失能但已在近处且吃喝充足仍可安静完成准备",()=>
{
    var sim=Fixture();foreach(var c in sim.State.Creatures)c.Interaction.FootIntegrity=20;
    Begin(sim);Tick(sim,6.5);
    Require(sim.State.Creatures[0].Family.Pregnancy.Phase==PregnancyPhase.Gestating,"把不能走路错误等同于不能在近处相处");
});
Check("脚部失能且需要吃饭时不能用邻近空等代替准备",()=>
{
    var sim=Fixture();foreach(var c in sim.State.Creatures){c.Interaction.FootIntegrity=20;c.Fullness=65;}
    Begin(sim);Tick(sim,8);var m=sim.State.Creatures[0];
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing&&m.Family.Pregnancy.Elapsed==0,"没有实际相处却完成准备");
    Require(sim.State.Creatures.All(c=>c.Family.LastCompanionActivityAt<0),"用例应处于优先照顾吃喝而非实际相处的状态");
    foreach(var c in sim.State.Creatures)c.Fullness=100;
    Tick(sim,1);Require(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing&&m.Family.Pregnancy.Elapsed<2,"空等时间被带入准备");
    Tick(sim,6);Require(m.Family.Pregnancy.Phase==PregnancyPhase.Gestating,"补足需要后未恢复正常相处准备");
});
Check("高处和第三只箱体遮挡不累计准备",()=>
{
    foreach(string mode in new[]{"height","box"})
    {
        var sim=Fixture();Begin(sim);Tick(sim,2);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
        Require(m.Family.Pregnancy.Elapsed>1,"正常准备前置没有累计");
        if(mode=="height"){f.Interaction.Restraint=RestraintKind.Hanging;f.Interaction.Height=2;}
        else
        {
            m.X=m.TargetX=-2;f.X=f.TargetX=2;
            sim.State.Creatures.Add(new(){Id="pregnancy-box",Name="箱体占位",RegionId=m.RegionId,X=0,Z=0,TargetX=0,TargetZ=0,
                Body=new(){Id="pregnancy-box-body",SpeciesId="reimu"},Registration=new(){IdentityId="pregnancy-box-identity"},
                Interaction=new(){Enabled=true,Restraint=RestraintKind.Box,Accessory=new(){Id="pregnancy-box-accessory"}},
                Family=new(){NextSocialAt=10000,NextCareAt=10000}});
        }
        Tick(sim,.25);
        Require(m.Family.Pregnancy.Elapsed==0&&m.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"准备未因接触中断而清零："+mode);
        Tick(sim,5);Require(m.Family.Pregnancy.Phase!=PregnancyPhase.Gestating,"阻挡期间仍开始孕育："+mode);
    }
});
Check("吃喝、手势与被动滑动会中断已经累计的准备",()=>
{
    foreach(string mode in new[]{"meal","water","hand","sliding"})
    {
        var sim=Fixture();Begin(sim);Tick(sim,2);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
        Require(m.Family.Pregnancy.Elapsed>1,"正常准备前置没有累计");
        if(mode=="hand")Require(sim.BeginInteractionGesture(f.Id,new(InteractionAction.Stroke,BodyPart.Head)).Success,"接触手势前置失败");
        else if(mode=="sliding")f.Interaction.VelocityX=-.4;
        else f.Interaction.BeginIntake(mode=="meal"?IntakeKind.Meal:IntakeKind.Water);
        Tick(sim,.25);Require(m.Family.Pregnancy.Elapsed==0&&m.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"占用未中断准备："+mode);
        if(mode=="hand")sim.EndInteractionGesture(true);
    }
});
Check("暂停和近看不借用上一帧相处时间推进准备",()=>
{
    foreach(bool pause in new[]{false,true})
    {
        var sim=Fixture();Begin(sim);Tick(sim,2);var p=sim.State.Creatures[0].Family.Pregnancy;double elapsed=p.Elapsed,world=sim.State.WorldSeconds;
        if(pause)sim.State.Settings.Pause=true;else sim.State.Mode=PlayMode.CloseUp;
        Tick(sim,8);Require(p.Elapsed==elapsed&&sim.State.WorldSeconds==world,"冻结期间偷偷积累准备时间");
    }
});
Check("部分准备读档续接仍需要剩余实际相处时间",()=>
{
    var sim=Fixture();Begin(sim);Tick(sim,2);double elapsed=sim.State.Creatures[0].Family.Pregnancy.Elapsed;
    var loaded=Reload(sim);var p=loaded.State.Creatures[0].Family.Pregnancy;
    Require(p.Elapsed==elapsed,"读档丢失已有准备进度");Tick(loaded,1);
    Require(p.Phase==PregnancyPhase.Preparing&&p.Elapsed>elapsed&&p.Elapsed<4,"读档重放或跳过剩余准备");
    Tick(loaded,4);Require(p.Phase==PregnancyPhase.Gestating,"实际相处后读档准备无法完成");
});
Check("分娩优先使练习跟随等待休息与人油陪伴让位",()=>
{
    foreach(string mode in new[]{"cue","follow","wait","rest","human","companion"})
    {
        var sim=Due();var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];var i=m.Interaction;
        if(mode=="cue"){i.ActiveCue=LearnedCue.Wait;i.CueElapsed=1;i.CueTravel=1;i.CueUnderstood=true;i.CueDemonstration=true;}
        if(mode=="follow")i.FollowPlayer=true;
        if(mode=="wait")i.WaitRemaining=4;
        if(mode=="rest"){i.Resting=true;i.RestRemaining=4;}
        if(mode=="human"){i.HumanCompany.StartedAt=0;i.HumanCompany.Until=20;}
        if(mode=="companion")
        {m.Family.CompanionTargetId=f.Id;f.Family.CompanionTargetId=m.Id;m.Family.CompanionUntil=f.Family.CompanionUntil=24;}
        Deliver(sim);
        Require(Babies(sim)==2&&m.Family.Pregnancy.Phase==PregnancyPhase.Recovering,"可让位活动使分娩卡住："+mode);
        Require(i.ActiveCue==null&&!i.FollowPlayer&&i.WaitRemaining==0&&!i.Resting&&i.RestRemaining==0&&i.HumanCompany.Until==0&&
            m.Family.CompanionTargetId==null&&f.Family.CompanionTargetId==null,"出生后仍保留相冲突活动："+mode);
        Deliver(sim);Require(Babies(sim)==2&&sim.State.CompletedBirths.Count==1,"再次结算复制宝宝身份");
    }
});
Check("分娩放下照护份额且保留食物与血缘身份",()=>
{
    var sim=Due();var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    var parcel=new RoomObject{Id="delivery-parcel",RegionId=m.RegionId,Kind=RoomObjectKind.Meal,Portions=1,CarrierId=m.Id,X=.65,Z=0,Height=.45};
    sim.State.RoomObjects.Add(parcel);m.Family.CareParcelId=parcel.Id;m.Family.CareTargetId=f.Id;m.Family.CareApproachSeconds=3;
    int portions=sim.State.RoomObjects.Where(o=>o.Kind==RoomObjectKind.Meal).Sum(o=>o.Portions);
    Deliver(sim);
    Require(Babies(sim)==2&&m.Family.CareParcelId==null&&m.Family.CareTargetId==null&&m.Family.CareApproachSeconds==0,"仍一边搬运一边分娩");
    Require(parcel.CarrierId==null&&parcel.Portions==1&&parcel.X==m.X&&parcel.Z==m.Z,"分娩吞掉、复制或远送照护份额");
    Require(sim.State.RoomObjects.Where(o=>o.Kind==RoomObjectKind.Meal).Sum(o=>o.Portions)==portions,"分娩让位改变了食物总份额");
    Require(sim.State.Creatures.Where(c=>c.Family.BirthEventId!=null).All(c=>c.Family.MotherId==m.Id&&c.Family.FatherId==f.Id),"让位清理破坏出生血缘");
});
Check("逃离冲突伤幼嘴忙排泄手势及支撑不稳时等待分娩",()=>
{
    foreach(string mode in new[]{"flight","retreat","conflict","child-threat","meal","water","excretion","hand","lowering","sliding","carried","falling","height","hanging","supine"})
    {
        var sim=Due();var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];var i=m.Interaction;
        if(mode=="flight")m.Family.FlightRemaining=4;
        if(mode=="retreat")m.RetreatRemaining=4;
        if(mode=="conflict")m.Family.Conflict.TargetId=f.Id;
        if(mode=="child-threat")m.Family.ChildThreat.TargetId=f.Id;
        if(mode=="meal"||mode=="water")i.BeginIntake(mode=="meal"?IntakeKind.Meal:IntakeKind.Water);
        if(mode=="excretion"){i.Physiology.Excreting=ExcretionKind.Urine;i.Physiology.Remaining=2;}
        if(mode=="hand")Require(sim.BeginInteractionGesture(m.Id,new(InteractionAction.Stroke,BodyPart.Head)).Success,"分娩手势前置失败");
        if(mode=="lowering")i.Lowering=true;
        if(mode=="sliding")i.VelocityX=.8;
        if(mode=="carried")i.Carried=true;
        if(mode=="falling")i.Falling=true;
        if(mode=="height")i.Height=1;
        if(mode=="hanging")i.Restraint=RestraintKind.Hanging;
        if(mode=="supine")i.Supine=true;
        Deliver(sim);
        Require(Babies(sim)==0&&m.Family.Pregnancy.Phase==PregnancyPhase.Gestating&&sim.State.CompletedBirths.Count==0,"不适合分娩时仍结算："+mode);
    }
});
Check("空间不足不提前打断母体活动，空位恢复后正常让位",()=>
{
    var sim=Due();var m=sim.State.Creatures[0];m.Interaction.FollowPlayer=true;
    // Cover every candidate in the delivery ring; these are static occupancy fixtures.
    int ordinal=0;
    for(int x=-5;x<=5;x++)for(int z=-5;z<=5;z++)
        sim.State.Creatures.Add(new(){Id="delivery-blocker-"+ordinal++,RegionId=m.RegionId,X=x,Z=z,TargetX=x,TargetZ=z,
            Interaction=new(){Restraint=RestraintKind.Box}});
    Deliver(sim);Require(Babies(sim)==0&&m.Interaction.FollowPlayer,"尚无出生空位却先取消母体活动");
    sim.State.Creatures.RemoveAll(c=>c.Id.StartsWith("delivery-blocker-",StringComparison.Ordinal));
    Deliver(sim);Require(Babies(sim)==2&&!m.Interaction.FollowPlayer,"释放空位后未实际分娩并让位");
});
Check("脚伤不能走但地面安稳仍可分娩且普通后续不重复出生",()=>
{
    var sim=Due();var m=sim.State.Creatures[0];m.Interaction.FootIntegrity=20;double x=m.X,z=m.Z;
    Tick(sim,.25);Require(Babies(sim)==2&&m.X==x&&m.Z==z,"分娩错误要求母体能走或瞬移母体");
    var ids=sim.State.Creatures.Where(c=>c.Family.BirthEventId!=null).Select(c=>c.Id).Order().ToArray();
    Tick(sim,.25);Require(Babies(sim)==2&&ids.SequenceEqual(sim.State.Creatures.Where(c=>c.Family.BirthEventId!=null).Select(c=>c.Id).Order()),"普通后续重复出生身份");
});

if(failures>0)Environment.Exit(1);

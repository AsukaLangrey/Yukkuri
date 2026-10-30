using System.Text.Json;
using Yukkuri.Simulation;

// Source for the independently authorized remote machine. Never run locally.
int failures=0;
void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
(WorldSimulation Sim,Creature Giver,Creature Child,RoomObject Source) Fixture(bool water=false)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261105);sim.State.RoomObjects.Clear();sim.State.UnlimitedResources=false;
    sim.State.PlayerX=0;sim.State.PlayerZ=7;
    for(int n=0;n<3;n++)
    {
        var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=c.Health=100;c.Trust=80;c.Fear=c.Pain=0;
        c.Aggression=.08;c.Patience=.8;c.Courage=.2;c.TrueDisposition="gentle";
        c.DecisionIn=10000;c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.NextSocialAt=c.Family.NextCareAt=10000;
        c.X=c.TargetX=n==0?0:n==1?-5:4;c.Z=c.TargetZ=n==1?-5:0;
    }
    var giver=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    child.IsYoung=true;child.Body.AgeSeconds=child.InnerAgeSeconds=0;child.ParentId=child.Family.MotherId=giver.Id;
    child.Interaction.FootIntegrity=20;
    child.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Bottom,PermanentLoss=80,Cause="既有底部伤残"});
    child.Fullness=water?100:20;child.Interaction.Hydration=water?20:100;
    var source=new RoomObject{Id="interruption-source",Kind=water?RoomObjectKind.Water:RoomObjectKind.Meal,
        RegionId=giver.RegionId,X=0,Z=.95,Portions=3};sim.State.RoomObjects.Add(source);
    Require(sim.FamilyCareReason(giver.Id,child.Id)==null,"实际照护资格不成立");
    sim.ArrangeCare(giver.Id,child.Id);Require(giver.Family.CareTargetId==child.Id,"没有从公开入口建立照护");
    return(sim,giver,child,source);
}
RoomObject TakePortion(WorldSimulation sim,Creature giver,RoomObject source)
{
    for(int n=0;n<120;n++)
    {
        Tick(sim,.25);
        if(giver.Family.CareParcelId is { } id&&sim.State.RoomObjects.Find(o=>o.Id==id&&o.CarrierId==giver.Id) is { } parcel)
        {
            Require(source.Portions==2&&parcel.Portions==1&&Total(sim,source.Kind)==3,"实际取份没有保持原有总量");
            Tick(sim,.25); // Ordinary approach makes CareBestDistance a real task value.
            Require(giver.Family.CareParcelId==id&&giver.Family.CareBestDistance<100,"没有经历取份后的真实递送");
            return parcel;
        }
    }
    throw new Exception("未取得实物份额："+giver.Family.CareLastEvent);
}
int Total(WorldSimulation sim,RoomObjectKind kind)=>sim.State.RoomObjects.Where(o=>o.Kind==kind).Sum(o=>o.Portions);
string StateOf(Creature giver,Creature child,RoomObject parcel)=>JsonSerializer.Serialize(new
{
    giver.Family.CareTargetId,giver.Family.CareParcelId,giver.Family.CareApproachSeconds,giver.Family.CareBestDistance,
    giver.Family.CareLastEvent,childRecord=child.Family.CareLastEvent,parcel.Id,parcel.Portions,parcel.CarrierId,parcel.X,parcel.Z,parcel.RegionId
});
void Cleared(Creature giver,Creature child,RoomObject parcel)
{
    Require(giver.Family.CareTargetId==null&&giver.Family.CareParcelId==null&&giver.Family.CareApproachSeconds==0&&
        giver.Family.CareBestDistance==100&&parcel.CarrierId==null,"中断残留照护状态："+StateOf(giver,child,parcel));
    Require(giver.Family.CareLastEvent.Length>0&&child.Family.CareLastEvent.Contains("照护已中断"),"双方中断原因不完整："+StateOf(giver,child,parcel));
}
void Conserved(WorldSimulation sim,RoomObject source,RoomObject parcel)
{
    Require(source.Portions==2&&parcel.Portions==1&&Total(sim,source.Kind)==3&&sim.State.RoomObjects.Count(o=>o.Id==parcel.Id)==1,
        "中断退款、克隆或遗失了实际份额");
}
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}

Check("正常递送保留成功结果，实际入口才消费食水",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var (sim,giver,child,source)=Fixture(water);var parcel=TakePortion(sim,giver,source);
        double need=water?child.Interaction.Hydration:child.Fullness;bool placed=false,pending=false,accepted=false;
        for(int n=0;n<600;n++)
        {
            Tick(sim,.05);
            placed|=parcel.CarrierId==null&&WorldSimulation.Distance(child.X,child.Z,parcel.X,parcel.Z)<=NaturalResources.Reach(child);
            if(child.Interaction.GroundIntakeSourceId==parcel.Id&&child.Interaction.EatingRemaining>0)
            {pending|=child.Interaction.NaturalIntakePending;accepted|=!child.Interaction.NaturalIntakePending;}
            if(accepted&&child.Interaction.EatingRemaining==0)break;
        }
        Require(placed&&pending&&accepted&&child.Interaction.EatingRemaining==0&&parcel.Portions==0&&Total(sim,source.Kind)==2,"没有完成实际放下、预留、入口与吞咽");
        Require((water?child.Interaction.Hydration:child.Fullness)>need&&child.Interaction.Digestion.Exposures==1,"实际消费未恰好结算一次");
        Require(giver.Family.CareTargetId==null&&giver.Family.CareParcelId==null&&giver.Family.CareApproachSeconds==0&&giver.Family.CareBestDistance==100,
            "正常完成留下进行中的照护状态");
        Require(giver.Family.CareLastEvent.Contains("已把一份")&&!child.Family.CareLastEvent.Contains("中断"),"成功递送被机械改成中断");
    }
});
Check("受伤或濒危立即放下唯一口粮并清理双方任务",()=>
{
    foreach(double damage in new[]{2.0,100.0})foreach(bool water in new[]{false,true})
    {
        var (sim,giver,child,source)=Fixture(water);var parcel=TakePortion(sim,giver,source);double x=giver.X,z=giver.Z;
        sim.Hurt(giver,damage,"照护途中受伤","player",BodyPart.Side);
        Cleared(giver,child,parcel);Conserved(sim,source,parcel);
        Require(WorldSimulation.Distance(parcel.X,parcel.Z,x,z)<.001&&parcel.Height==0,"口粮未留在实际受伤落点");
        string record=giver.Family.CareLastEvent;sim.AdvanceInteractionPresentation(.05);
        Require(giver.Family.CareLastEvent==record&&child.Interaction.Digestion.Exposures==0,"重复清理改写记录或隔空入口");
        SaveStore.Validate(sim.State);
    }
});
Check("实际托举照护者或对象中断运输，暂停普通时钟也释放口粮",()=>
{
    foreach(bool liftGiver in new[]{false,true})
    {
        var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);double x=giver.X,z=giver.Z;
        var lifted=liftGiver?giver:child;
        Require(sim.Select(lifted.Id)&&sim.EnterCloseUp()&&sim.State.SelectedId==lifted.Id&&sim.State.Mode==PlayMode.CloseUp,
            "没有实际选中托举对象并进入近看");
        double time=sim.State.WorldSeconds;
        Require(sim.BeginInteractionGesture(lifted.Id,new(InteractionAction.Lift,X:lifted.X,Z:lifted.Z,Height:1)).Success,"托举入口拒绝");
        sim.AdvanceInteractionPresentation(.05);
        Require(lifted.Interaction.Carried&&sim.State.WorldSeconds==time,"没有真实托举或偷偷推进普通时钟");
        Cleared(giver,child,parcel);Conserved(sim,source,parcel);
        Require(WorldSimulation.Distance(parcel.X,parcel.Z,x,z)<.001,"托举把运输口粮带去别处");
    }
});
Check("冷藏双方任意一只都中断递送且不把食物藏入冷藏",()=>
{
    foreach(bool storeGiver in new[]{false,true})
    {
        var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);var stored=storeGiver?giver:child;
        sim.SetColdStorage(stored.Id,true);Require(stored.Family.ColdStorage.Stored,"冷藏入口没有生效");
        Cleared(giver,child,parcel);Conserved(sim,source,parcel);
        Require(!parcel.Stored&&!parcel.HeldByHand&&parcel.Height==0,"口粮也被冷藏或留在空中");
        var restored=Reload(sim);Require(Total(restored,source.Kind)==3,"冷藏读档复制或遗失口粮");
    }
});
Check("强制准备让正在递送的母体放下实物，材料仅消费一次",()=>
{
    var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);var father=sim.State.Creatures[1];
    sim.CollectBreedingMaterial(father.Id);Require(sim.State.BreedingMaterials.Count==1,"材料采集前置失败");
    sim.RequestForcedPregnancy(giver.Id,father.Id,BirthMethod.Stem);
    Require(FamilyRules.UnderIntervention(giver)&&sim.State.BreedingMaterials.Count==0,"强制准备未真实开始或材料未消费");
    Cleared(giver,child,parcel);Conserved(sim,source,parcel);
    sim.RequestForcedPregnancy(giver.Id,father.Id,BirthMethod.Stem);sim.AdvanceInteractionPresentation(.05);
    Require(sim.State.BreedingMaterials.Count==0&&giver.Activity=="强制干预进行中","重复安排改写了强制准备动作");
    Conserved(sim,source,parcel);
});
Check("手接走运输份额保持同一实物和位置，放下不退款或复制",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var (sim,giver,child,source)=Fixture(water);var parcel=TakePortion(sim,giver,source);
        double x=parcel.X,z=parcel.Z;long sequence=sim.State.FamilySequence;
        Require(sim.HoldSupply(parcel.Id),"手未接走实际运输份额");
        Cleared(giver,child,parcel);Conserved(sim,source,parcel);
        Require(parcel.HeldByHand&&parcel.X==x&&parcel.Z==z&&sim.State.FamilySequence==sequence,"接走时重定位或新造份额");
        Require(!sim.HoldSupply(parcel.Id),"同一份额被重复拿起");
        Require(sim.ReleaseSupply(parcel.Id)&&parcel.Height==0&&!parcel.HeldByHand,"原处放下未落到实际支撑面");
        sim.AdvanceInteractionPresentation(.05);Conserved(sim,source,parcel);
        Require(child.Interaction.Digestion.Exposures==0,"被手拿走仍远程喂给了对象");
    }
});
Check("收起运输食物只退回实物一份，重复收起不重复退款",()=>
{
    var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);double food=sim.State.Food;
    Require(sim.PutAwayRoomObject(parcel.Id),"收起实际运输口粮失败");Cleared(giver,child,parcel);
    Require(sim.State.Food==food+1&&source.Portions==2&&Total(sim,source.Kind)==2&&!sim.State.RoomObjects.Contains(parcel),"收起没有把唯一一份转回仓库");
    Require(!sim.PutAwayRoomObject(parcel.Id)&&sim.State.Food==food+1,"重复收起重复退款");
});
Check("改派照护对象通知旧对象，旧份额原处保留且不立即再分一份",()=>
{
    var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);var next=sim.State.Creatures[1];
    next.IsYoung=true;next.Body.AgeSeconds=next.InnerAgeSeconds=0;next.ParentId=next.Family.MotherId=giver.Id;
    next.X=next.TargetX=0;next.Z=next.TargetZ=-4;next.Fullness=40;
    double x=giver.X,z=giver.Z;long sequence=sim.State.FamilySequence;
    sim.ArrangeCare(giver.Id,next.Id);
    Require(giver.Family.CareTargetId==next.Id&&giver.Family.CareParcelId==null&&giver.Family.CareApproachSeconds==0&&giver.Family.CareBestDistance==100,"改派未建立新的干净任务");
    Require(child.Family.CareLastEvent.Contains("改派")&&giver.Family.CareLastEvent.Contains("已安排"),"旧对象或新安排没有独立记录");
    Require(parcel.CarrierId==null&&WorldSimulation.Distance(parcel.X,parcel.Z,x,z)<.001&&sim.State.FamilySequence==sequence,"改派重复分装或移动旧份额");
    Conserved(sim,source,parcel);
});
Check("对象离世保留死因与身份，释放照护不冒充实际送食",()=>
{
    var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);
    sim.Hurt(child,200,"照护中对象致命伤",null);string result=child.Family.LastEvent;
    Require(child.Condition==PhysicalCondition.Dead&&child.Interaction.Injuries.DeathCause=="照护中对象致命伤","实际死亡未结算");
    Cleared(giver,child,parcel);Conserved(sim,source,parcel);sim.AdvanceInteractionPresentation(.05);
    Require(child.Family.LastEvent==result&&sim.State.Creatures.Contains(child)&&child.Family.MotherId==giver.Id&&child.Interaction.Digestion.Exposures==0,
        "取消照护覆盖死亡事实、删除身份或虚构送食");
});
Check("地区变化只中断递送，已取实物留在原地区而不跨区瞬移",()=>
{
    foreach(bool movesGiver in new[]{false,true})
    {
        var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);string region=parcel.RegionId;
        double x=movesGiver?parcel.X:giver.X,z=movesGiver?parcel.Z:giver.Z;
        // Region transition fixture: no public courtyard region-transfer command.
        sim.State.Regions.Add(new(){Id="care-other-region",Name="另一地区",Kind=RegionKind.Wilderness});
        var moved=movesGiver?giver:child;moved.RegionId="care-other-region";moved.X=moved.TargetX=-3;moved.Z=moved.TargetZ=3;
        sim.AdvanceInteractionPresentation(.05);Cleared(giver,child,parcel);Conserved(sim,source,parcel);
        Require(parcel.RegionId==region&&WorldSimulation.Distance(parcel.X,parcel.Z,x,z)<.001&&parcel.Height==0,"原实物被瞬移到新地区或对象身旁");
    }
});
Check("实际携物遇到到期分娩正常让位，产后结论不被取消照护覆盖",()=>
{
    var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);var father=sim.State.Creatures[1];
    // Explicit pre-existing late gestation; this is not a conception-flow claim.
    giver.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,FatherId=father.Id,
        EventId="care-interruption-birth",PlannedChildren=2,Elapsed=FamilyRules.GestationSeconds(BirthMethod.Stem)};
    Tick(sim,.25);
    Require(sim.State.CompletedBirths.Contains("care-interruption-birth")&&sim.State.Creatures.Count(c=>c.Family.BirthEventId=="care-interruption-birth")==2,"真实普通帧没有结算一次出生");
    Cleared(giver,child,parcel);Conserved(sim,source,parcel);
    string outcome=giver.Family.Pregnancy.Outcome;sim.AdvanceInteractionPresentation(.05);
    Require(outcome.Contains("出生")&&giver.Family.LastEvent==outcome&&giver.Family.Pregnancy.Phase==PregnancyPhase.Recovering,"照护取消覆盖了实际产后结果");
    var restored=Reload(sim);Require(restored.State.Creatures.Count(c=>c.Family.BirthEventId=="care-interruption-birth")==2,"读档重复出生");
});
Check("携物母体死亡的孕育中止结果不被两次动作清理覆盖",()=>
{
    var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);
    giver.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,FatherId=sim.State.Creatures[1].Id,
        EventId="care-unborn",PlannedChildren=2};
    sim.Hurt(giver,200,"照护母体致命伤",null);Cleared(giver,child,parcel);Conserved(sim,source,parcel);
    Require(giver.Condition==PhysicalCondition.Dead&&giver.Family.Pregnancy.Outcome.Contains("母体离世")&&giver.Family.LastEvent==giver.Family.Pregnancy.Outcome,"死亡动作清理覆盖孕育结果");
    var restored=Reload(sim);var dead=restored.State.Creatures.Find(c=>c.Id==giver.Id)!;
    Require(dead.Family.LastEvent==giver.Family.LastEvent&&dead.Family.CareLastEvent==giver.Family.CareLastEvent&&!restored.State.CompletedBirths.Contains("care-unborn"),"死亡读档改写照护或结算未出生子代");
});
Check("暂停与读档保留唯一运输份额，中断后双方记录可保存",()=>
{
    var (sim,giver,child,source)=Fixture(true);var parcel=TakePortion(sim,giver,source);double time=sim.State.WorldSeconds;
    sim.State.Settings.Pause=true;Tick(sim,5);
    Require(sim.State.WorldSeconds==time&&giver.Family.CareParcelId==parcel.Id&&parcel.CarrierId==giver.Id,"暂停推进或丢失运输任务");
    var restored=Reload(sim);restored.State.Settings.Pause=false;
    var copyGiver=restored.State.Creatures.Find(c=>c.Id==giver.Id)!;var copyChild=restored.State.Creatures.Find(c=>c.Id==child.Id)!;
    var copyParcel=restored.State.RoomObjects.Find(o=>o.Id==parcel.Id)!;
    Require(restored.HoldSupply(copyParcel.Id),"读档后的实际份额无法接走");Cleared(copyGiver,copyChild,copyParcel);
    var again=Reload(restored);
    Require(again.State.Creatures.Find(c=>c.Id==giver.Id)!.Family.CareLastEvent==copyGiver.Family.CareLastEvent&&
        again.State.Creatures.Find(c=>c.Id==child.Id)!.Family.CareLastEvent==copyChild.Family.CareLastEvent&&Total(again,source.Kind)==3,"读档丢记录或复制份额");
    Require(JsonSerializer.Deserialize<FamilyLife>("{}",SaveStore.JsonOptions)!.CareLastEvent=="","缺字段读档默认值不是空记录");
});

Check("主动结束实际递送后，人类捡起同一份食水并真实送入口",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var (sim,giver,child,source)=Fixture(water);var parcel=TakePortion(sim,giver,source);
        int food=sim.State.Food;double money=sim.State.Money,x=giver.X,z=giver.Z;
        string bonds=JsonSerializer.Serialize(new[]{giver.Family.Peers,child.Family.Peers},SaveStore.JsonOptions);
        string id=parcel.Id,region=parcel.RegionId;string? owner=parcel.OwnerId;double contamination=parcel.Contamination;
        Require(sim.EndFamilyCare(giver.Id,child.Id).StartsWith("已结束"),"真实照护不能主动结束");
        Cleared(giver,child,parcel);Conserved(sim,source,parcel);
        Require(sim.State.Food==food&&sim.State.Money==money&&parcel.Id==id&&parcel.RegionId==region&&parcel.OwnerId==owner&&
            parcel.Contamination==contamination&&WorldSimulation.Distance(parcel.X,parcel.Z,x,z)<.001&&parcel.Height==0,
            "停止照护改变库存、钱、实物来源或实际落点");
        Require(JsonSerializer.Serialize(new[]{giver.Family.Peers,child.Family.Peers},SaveStore.JsonOptions)==bonds&&child.ParentId==giver.Id,
            "停止照护改写关系或亲缘");
        double need=water?child.Interaction.Hydration:child.Fullness;int exposures=child.Interaction.Digestion.Exposures;
        Require(sim.HoldSupply(id),"落地的同一份食水无法捡起");
        var input=new InteractionInput(water?InteractionAction.Drink:InteractionAction.FeedMeal,BodyPart.Mouth,Duration:.05,ObjectId:id);
        Require(sim.BeginInteractionGesture(child.Id,input).Success,"已拾取实物不能实际递给对象");
        for(int n=0;n<80&&!sim.InteractionIntakeAccepted;n++){sim.ContinueInteractionGesture(input,true);Tick(sim,.05);}
        Require(sim.InteractionIntakeAccepted&&parcel.Portions==0,"真实手喂没有恰好消费原来的一份");
        sim.EndInteractionGesture();sim.ReleaseSupply(id);
        for(int n=0;n<200&&child.Interaction.EatingRemaining>0;n++)Tick(sim,.05);
        Require(child.Interaction.EatingRemaining==0&&(water?child.Interaction.Hydration:child.Fullness)>need&&
            child.Interaction.Digestion.Exposures==exposures+1&&source.Portions==2&&Total(sim,source.Kind)==2,
            "交接食水没有完成一次真实入口/吞咽，或原来源多扣了一份");
        Require(sim.State.Food==food&&sim.State.Money==money,"接手原实物又消费库存或钱");
        SaveStore.Validate(sim.State);
    }
});
Check("未取份前可主动停止，重复按钮不推进冷却、不改记录或份额",()=>
{
    var (sim,giver,child,source)=Fixture();int food=sim.State.Food;double money=sim.State.Money;
    Require(giver.Family.CareParcelId==null&&sim.EndFamilyCare(giver.Id,child.Id).StartsWith("已结束"),"尚未取份的照护无法停止");
    Require(giver.Family.CareTargetId==null&&source.Portions==3&&Total(sim,source.Kind)==3&&sim.State.Food==food&&sim.State.Money==money,
        "尚未取份却凭空落物或退款");
    string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    sim.EndFamilyCare(giver.Id,child.Id);sim.EndFamilyCare(giver.Id,child.Id);
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==before,"重复停止推进了冷却、记录或状态");
});
Check("旧照护按钮不能停止实际改派的新对象，错误与跨区请求不改状态",()=>
{
    var (sim,giver,child,source)=Fixture();var parcel=TakePortion(sim,giver,source);var next=sim.State.Creatures[1];
    // Existing alternative dependent child, matching the original reassignment fixture above.
    next.IsYoung=true;next.Body.AgeSeconds=next.InnerAgeSeconds=0;next.ParentId=next.Family.MotherId=giver.Id;
    next.X=next.TargetX=0;next.Z=next.TargetZ=-4;next.Fullness=40;
    sim.ArrangeCare(giver.Id,next.Id);Require(giver.Family.CareTargetId==next.Id,"实际改派未成功");
    string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    sim.EndFamilyCare(giver.Id,child.Id);sim.EndFamilyCare("missing",next.Id);sim.EndFamilyCare(giver.Id,giver.Id);
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==before&&giver.Family.CareTargetId==next.Id,"旧或错误按钮改变了新任务");
    Conserved(sim,source,parcel);
    sim.State.Regions.Add(new(){Id="care-command-other",Name="另一地区",Kind=RegionKind.Wilderness});
    sim.State.ActiveRegionId="care-command-other";before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    sim.EndFamilyCare(giver.Id,next.Id);
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==before,"非当前区命令改变了照护");
    sim.State.ActiveRegionId=giver.RegionId;next.RegionId="care-command-other";
    // Same explicit region-transition fixture used above, before ordinary task synchronization.
    Require(sim.EndFamilyCare(giver.Id,next.Id).StartsWith("已结束")&&giver.Family.CareTargetId==null,
        "对象离区后，仍在当前区的照护者反而无法停止旧任务");
    Conserved(sim,source,parcel);
});
Check("暂停拒绝主动结束，携份保存恢复后仍可停止且只留下同一实物",()=>
{
    var (sim,giver,child,source)=Fixture(true);var parcel=TakePortion(sim,giver,source);
    sim.State.Settings.Pause=true;string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    Require(sim.EndFamilyCare(giver.Id,child.Id).Contains("恢复时间"),"暂停中没有拒绝命令");
    Tick(sim,1);Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==before,"暂停停止操作改变了世界");
    var restored=Reload(sim);restored.State.Settings.Pause=false;
    var a=restored.State.Creatures.Find(c=>c.Id==giver.Id)!;var b=restored.State.Creatures.Find(c=>c.Id==child.Id)!;
    var p=restored.State.RoomObjects.Find(o=>o.Id==parcel.Id)!;var origin=restored.State.RoomObjects.Find(o=>o.Id==source.Id)!;
    Require(restored.EndFamilyCare(a.Id,b.Id).StartsWith("已结束"),"真实读档后的照护不能结束");
    Cleared(a,b,p);Conserved(restored,origin,p);SaveStore.Validate(restored.State);
    before=JsonSerializer.Serialize(restored.State,SaveStore.JsonOptions);restored.EndFamilyCare(a.Id,b.Id);
    Require(JsonSerializer.Serialize(restored.State,SaveStore.JsonOptions)==before,"读档后重复停止改变了记录");
});

Environment.ExitCode=failures==0?0:1;

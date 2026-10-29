using System.Text.Json;
using Yukkuri.Simulation;

// Remote authorised machine only. Do not execute on the original development workstation.
int failures=0;
void Require(bool condition,string reason){if(!condition)throw new Exception(reason);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception error){failures++;Console.WriteLine("FAIL "+name+": "+error);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture(bool maternal=false)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261101);sim.State.RoomObjects.Clear();
    sim.State.PlayerX=0;sim.State.PlayerZ=7;
    for(int n=0;n<3;n++)
    {
        var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=c.Health=100;c.Trust=90;c.Fear=c.Pain=0;
        c.Aggression=.05;c.Patience=.8;c.Courage=.2;c.TrueDisposition="gentle";
        c.Family.NextSocialAt=c.Family.NextCareAt=10000;c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.X=c.TargetX=n==0?0:n==1?.825:1.65;c.Z=c.TargetZ=n==1?1.43:0;
    }
    var actor=sim.State.Creatures[0];var protector=sim.State.Creatures[1];var target=sim.State.Creatures[2];
    actor.Aggression=1;actor.Patience=0;actor.TrueDisposition="greedy";protector.Courage=.8;
    target.Family.CaregiverIds.Add(protector.Id);
    if(maternal)
    {
        target.IsYoung=true;target.Body.AgeSeconds=target.InnerAgeSeconds=0;
        target.X=target.TargetX=1.5;protector.X=protector.TargetX=.75;protector.Z=protector.TargetZ=1.4;
        target.Family.MotherId=target.ParentId=actor.Id;target.Family.BirthMethod=BirthMethod.Stem;
        actor.Fullness=19;
        actor.Family.ChildThreat=new(){TargetId=target.Id,WarningRemaining=8,Remaining=45,NextCheckAt=120,Cause="饥饿与求生压力"};
    }
    else
    {
        actor.Family.PartnerIds.Add(protector.Id);protector.Family.PartnerIds.Add(actor.Id);
        protector.Family.PartnerIds.Add(target.Id);target.Family.PartnerIds.Add(protector.Id);
        var cause=new FamilyRelationshipEvent{Id="protection-betrayal",FirstId=protector.Id,SecondId=target.Id,
            BetrayedPartners=new(){[actor.Id]=protector.Id},KnownBy=new(){[actor.Id]=KnowledgeSource.Witness},ReactedBy=new(){actor.Id}};
        sim.State.FamilyRelationshipEvents.Add(cause);sim.State.FamilySequence=1;
        actor.Family.Conflict=new(){TargetId=target.Id,CauseEventId=cause.Id,WarningRemaining=8,Remaining=60};
        actor.Family.ConflictCooldownUntil=180;
    }
    return sim;
}
bool HasThreat(Creature actor,bool maternal)=>maternal?actor.Family.ChildThreat.TargetId!=null:actor.Family.Conflict.TargetId!=null;
void Busy(WorldSimulation sim,string mode)
{
    var protector=sim.State.Creatures[1];
    if(mode=="eat")protector.Interaction.BeginIntake(IntakeKind.Meal);
    else if(mode=="drink")protector.Interaction.BeginIntake(IntakeKind.Water);
    else if(mode=="cue")
    {protector.Interaction.ActiveCue=LearnedCue.Wait;protector.Interaction.Learning[LearnedCue.Wait]=new();protector.Interaction.CueUnderstood=true;}
    else if(mode=="wait")protector.Interaction.WaitRemaining=4;
    else if(mode=="flight")
    {protector.Family.FlightRemaining=5;protector.Family.FlightX=4;protector.Family.FlightZ=4;}
    else if(mode=="rest")protector.Interaction.RestRemaining=4;
    else if(mode=="forced")
    {
        protector.Body.ReproductionRole=ReproductionRole.Maternal;sim.State.Creatures[0].Body.ReproductionRole=ReproductionRole.Paternal;
        protector.Family.Pregnancy=new(){Phase=PregnancyPhase.Preparing,Forced=true,InterventionRemaining=4,FatherId=sim.State.Creatures[0].Id};
    }
    else if(mode=="hand")Require(sim.BeginInteractionGesture(protector.Id,new(InteractionAction.Stroke,BodyPart.Head)).Success,"手势前置失败");
    else if(mode=="cold")sim.SetColdStorage(protector.Id,true);
    else if(mode=="high")protector.Interaction.Height=2;
}

Check("正常地面近处有余力者可以护伴和护幼",()=>
{
    foreach(bool maternal in new[]{false,true})
    {
        var sim=Fixture(maternal);var actor=sim.State.Creatures[0];var target=sim.State.Creatures[2];
        Tick(sim,.25);
        Require(!HasThreat(actor,maternal)&&target.Health==100,"正常保护未生效："+maternal);
        Require(actor.Family.LastEvent.Contains("护住")||sim.State.Creatures[1].Family.LastEvent.Contains("护住"),"缺少实际保护反馈");
    }
});
Check("吃喝、训练、手占用、逃离等状态不能同时保护",()=>
{
    foreach(bool maternal in new[]{false,true})
    foreach(string mode in new[]{"eat","drink","cue","wait","flight","rest","forced","hand","cold","high"})
    {
        var sim=Fixture(maternal);Busy(sim,mode);Tick(sim,.25);
        Require(HasThreat(sim.State.Creatures[0],maternal),"忙碌或受限者隔空护住目标："+maternal+"/"+mode);
        Require(sim.State.Creatures[2].Health==100,"预警阶段提前伤害");
    }
});
Check("保护者远离时不消除危险，实际走近后才能保护",()=>
{
    foreach(bool maternal in new[]{false,true})
    {
        var sim=Fixture(maternal);var protector=sim.State.Creatures[1];
        protector.X=protector.TargetX=-5;protector.Z=protector.TargetZ=-5;Tick(sim,.25);
        Require(HasThreat(sim.State.Creatures[0],maternal),"远处亲友自动保护");
        protector.X=protector.TargetX=maternal?.75:.825;protector.Z=protector.TargetZ=maternal?1.4:1.43;
        Tick(sim,.25);Require(!HasThreat(sim.State.Creatures[0],maternal),"实际在近处仍不保护");
    }
});
Check("悬挂高处目标不能被地面冲突或母体攻击命中",()=>
{
    foreach(bool maternal in new[]{false,true})
    {
        var sim=Fixture(maternal);var protector=sim.State.Creatures[1];var target=sim.State.Creatures[2];
        protector.X=protector.TargetX=-5;protector.Z=protector.TargetZ=-5;
        target.Interaction.Restraint=RestraintKind.Hanging;target.Interaction.Height=2;
        double health=target.Health;Tick(sim,10);
        Require(target.Health==health,"只有XZ接近就击中了高处目标："+maternal);
    }
});
Check("同样距离的地面目标仍会受到共用伤害流程处理",()=>
{
    foreach(bool maternal in new[]{false,true})
    {
        var sim=Fixture(maternal);var protector=sim.State.Creatures[1];var target=sim.State.Creatures[2];
        protector.X=protector.TargetX=-5;protector.Z=protector.TargetZ=-5;
        Tick(sim,10);Require(target.Health<100&&target.Pain>0,"高度限制误阻止地面实际接触："+maternal);
    }
});
Check("第三方箱体遮挡死亡，绕开以后才记后来发现",()=>
{
    var sim=Fixture();var deceased=sim.State.Creatures[0];var observer=sim.State.Creatures[2];var boxed=sim.State.Creatures[1];
    deceased.Family.Conflict=new();observer.Family.PartnerIds.Clear();boxed.Family.PartnerIds.Remove(observer.Id);
    deceased.X=deceased.TargetX=-2;observer.X=observer.TargetX=2;
    boxed.X=boxed.TargetX=0;boxed.Z=boxed.TargetZ=0;boxed.Interaction.Restraint=RestraintKind.Box;
    sim.Hurt(deceased,200,"视线审查死亡",null);
    Require(!observer.Family.KnownLosses.ContainsKey(deceased.Id),"隔着第三只箱体目击死亡");
    observer.X=observer.TargetX=-2;observer.Z=observer.TargetZ=-2;Tick(sim,.25);
    Require(observer.Family.KnownLosses[deceased.Id].Source==KnowledgeSource.Discovered,"绕到可见处被追认为死亡目击");
});
Check("第三方箱体遮挡亲近时不能据此获知背叛",()=>
{
    var sim=Fixture();var observer=sim.State.Creatures[0];var a=sim.State.Creatures[1];var b=sim.State.Creatures[2];
    observer.Family.Conflict=new();observer.Aggression=.05;observer.X=observer.TargetX=-2.3;observer.Z=observer.TargetZ=0;
    a.X=a.TargetX=1.5;a.Z=a.TargetZ=-.8;b.X=b.TargetX=1.5;b.Z=b.TargetZ=.8;
    var box=new Creature{Id="protection-box",Name="箱体占位",RegionId=observer.RegionId,X=0,Z=0,TargetX=0,TargetZ=0,
        Body=new(){Id="protection-box-body",SpeciesId="reimu"},Registration=new(){IdentityId="protection-box-identity"},
        Interaction=new(){Enabled=true,Restraint=RestraintKind.Box,Accessory=new(){Id="protection-box-accessory"}},
        Family=new(){NextSocialAt=10000,NextCareAt=10000}};
    sim.State.Creatures.Add(box);
    var cause=sim.State.FamilyRelationshipEvents[0];cause.KnownBy.Clear();cause.ReactedBy.Clear();
    a.Family.Peers[b.Id]=new(){Familiarity=60,Affection=60};b.Family.Peers[a.Id]=new(){Familiarity=60,Affection=60};
    sim.InviteCompanion(a.Id,b.Id);Tick(sim,1);
    Require(a.Family.LastCompanionActivityAt>0&&b.Family.LastCompanionActivityAt>0&&cause.KnownBy.ContainsKey(a.Id),"前置未实际相处并观察关系");
    Require(!cause.KnownBy.ContainsKey(observer.Id)&&!cause.ReactedBy.Contains(observer.Id),"隔箱知情并结算背叛");
});
Check("母体受到攻击正在逃离时先停下伤幼行为",()=>
{
    var sim=Fixture(true);var mother=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    sim.State.Creatures[1].X=sim.State.Creatures[1].TargetX=-5;sim.State.Creatures[1].Z=sim.State.Creatures[1].TargetZ=-5;
    mother.Family.ChildThreat.WarningRemaining=0;mother.Family.FlightRemaining=5;mother.Family.FlightX=-3;mother.Family.FlightZ=0;
    Tick(sim,.25);
    Require(mother.Family.ChildThreat.TargetId==null&&child.Health==100&&mother.X<0,"伤幼优先级压过受伤退避");
});
Check("近看中目标死亡立即清其他照护、冲突和伤幼引用，不推进时钟",()=>
{
    foreach(bool maternal in new[]{false,true})
    {
        var sim=Fixture(maternal);var actor=sim.State.Creatures[0];var caregiver=sim.State.Creatures[1];var target=sim.State.Creatures[2];
        caregiver.Family.CareTargetId=target.Id;
        var parcel=new RoomObject{Id="death-care-parcel",Kind=RoomObjectKind.Meal,RegionId=target.RegionId,CarrierId=caregiver.Id,OwnerId=caregiver.Id,
            X=caregiver.X,Z=caregiver.Z,Height=.45,Portions=1};sim.State.RoomObjects.Add(parcel);caregiver.Family.CareParcelId=parcel.Id;
        sim.State.Mode=PlayMode.CloseUp;double time=sim.State.WorldSeconds;
        sim.Hurt(target,200,"近看致命伤",null);
        Require(target.Condition==PhysicalCondition.Dead&&!HasThreat(actor,maternal)&&caregiver.Family.CareTargetId==null&&
            caregiver.Family.CareParcelId==null&&parcel.CarrierId==null&&parcel.Portions==1,"死亡仍占用其他油目标或实物");
        Require(sim.State.WorldSeconds==time,"引用清理偷偷推进了普通时间");
    }
});
Check("安置旧遗体也清残留伤幼引用，不补造远处目击",()=>
{
    var sim=Fixture(true);var mother=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    mother.X=mother.TargetX=-5;mother.Z=mother.TargetZ=-5;
    sim.Hurt(child,200,"旧记录遗体",null);
    // A legacy pending reference can be cleaned at the explicit aftermath entry.
    mother.Family.ChildThreat=new(){TargetId=child.Id,WarningRemaining=8,Remaining=45,Cause="旧记录"};
    sim.LayToRest(child.Id);
    Require(child.Family.Memorialized&&mother.Family.ChildThreat.TargetId==null,"安置未清旧危险引用");
    Require(!mother.Family.KnownLosses.ContainsKey(child.Id),"清引用被当成远处死亡目击");
});
Check("死亡清引用只影响死者，不删除其他仍有效目标",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var caregiver=sim.State.Creatures[1];var deceased=sim.State.Creatures[2];
    actor.Family.Conflict.TargetId=caregiver.Id;caregiver.Family.CareTargetId=actor.Id;
    sim.State.Mode=PlayMode.CloseUp;sim.Hurt(deceased,200,"无关目标死亡",null);
    Require(actor.Family.Conflict.TargetId==caregiver.Id&&caregiver.Family.CareTargetId==actor.Id,"死亡清理删掉了指向其他个体的新目标");
});
Check("冷藏打断双方危险状态，暂停近看不推进预警和出手间隔",()=>
{
    foreach(bool maternal in new[]{false,true})
    {
        var sim=Fixture(maternal);var actor=sim.State.Creatures[0];var target=sim.State.Creatures[2];
        double warning=maternal?actor.Family.ChildThreat.WarningRemaining:actor.Family.Conflict.WarningRemaining;
        sim.State.Mode=PlayMode.CloseUp;Tick(sim,10);
        Require((maternal?actor.Family.ChildThreat.WarningRemaining:actor.Family.Conflict.WarningRemaining)==warning&&target.Health==100,"近看推进预警");
        sim.State.Mode=PlayMode.Management;sim.State.Settings.Pause=true;Tick(sim,10);
        Require((maternal?actor.Family.ChildThreat.WarningRemaining:actor.Family.Conflict.WarningRemaining)==warning&&target.Health==100,"暂停推进预警");
        sim.State.Settings.Pause=false;sim.SetColdStorage(target.Id,true);Tick(sim,.25);
        Require(target.Family.ColdStorage.Stored&&!HasThreat(actor,maternal)&&target.Health==100,"冷藏仍受追击");
    }
});
Check("同档继续的保护和危险结算一致",()=>
{
    var sim=Fixture(true);sim.State.Creatures[1].X=sim.State.Creatures[1].TargetX=-5;sim.State.Creatures[1].Z=sim.State.Creatures[1].TargetZ=-5;
    Tick(sim,2);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);var restored=new WorldSimulation(state);Tick(sim,8);Tick(restored,8);
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(restored.State,SaveStore.JsonOptions),"读档改变保护/冲突结果");
});
Check("孕育中的母体离世即时停止孕育，现有亲缘与身份仍保留",()=>
{
    var sim=Fixture(true);var mother=sim.State.Creatures[0];var father=sim.State.Creatures[1];var child=sim.State.Creatures[2];
    mother.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,FatherId=father.Id,EventId="unborn-protection-test",PlannedChildren=2};
    int count=sim.State.Creatures.Count,births=sim.State.CompletedBirths.Count;sim.State.Mode=PlayMode.CloseUp;
    sim.Hurt(mother,200,"孕育期间致命伤",null);
    Require(mother.Condition==PhysicalCondition.Dead&&mother.Family.Pregnancy.Phase==PregnancyPhase.None&&
        mother.Family.Pregnancy.RecoveryRemaining==0&&mother.Family.ChildThreat.TargetId==null,"死亡遗留孕育或危险行动");
    Require(child.Family.MotherId==mother.Id&&child.ParentId==mother.Id&&sim.State.Creatures.Contains(mother),"母体死亡删除了身份亲缘");
    sim.State.Mode=PlayMode.Management;Tick(sim,2);
    Require(sim.State.Creatures.Count==count&&sim.State.CompletedBirths.Count==births,"母体死亡后仍结算出生");
});

Environment.ExitCode=failures==0?0:1;

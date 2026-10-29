using System.Text.Json;
using Yukkuri.Simulation;

// Authorised independent remote host only. Never run on the original development workstation.
int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture(ulong seed=1)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(unchecked(seed*11400714819323198485UL));
    sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAt(2);
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=100;c.Trust=80;c.Health=100;c.Fear=c.Pain=c.Resentment=0;
        c.Aggression=.1;c.Patience=.5;c.Courage=.2;c.TrueDisposition="gentle";
        c.X=c.TargetX=n==0?0:-5;c.Z=c.TargetZ=n==0?0:-5;
        c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    }
    return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void Collect(WorldSimulation sim)
{
    var f=sim.State.Creatures[1];string response=sim.CollectBreedingMaterial(f.Id);
    Require(sim.State.BreedingMaterials.Count==1,"材料收集前置失败："+response);
}
void Force(WorldSimulation sim,BirthMethod method=BirthMethod.Stem)
{
    Collect(sim);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    string response=sim.RequestForcedPregnancy(m.Id,f.Id,method);
    Require(FamilyRules.UnderIntervention(m),"强制入口没有进入独立准备过程："+response);
}
WorldSimulation PregnancyDanger(ulong seed,bool physiological=false)
{
    var sim=Fixture(seed);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    // Focused prenatal fixture: a valid normal live pregnancy, not a forced request.
    sim.State.FamilySequence=1;
    m.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Live,FatherId=f.Id,
        EventId="risk-live-pregnancy",PlannedChildren=1,Burden=physiological?79.99:45,
        PrenatalAttachment=50,NextDecisionAt=physiological?10000:0};
    if(physiological){m.Fullness=10;m.Interaction.Hydration=10;}
    return sim;
}
WorldSimulation MaternalFixture(ulong seed,bool lethal=true)
{
    var sim=Fixture(seed);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    sim.State.Settings.LethalInjuries=lethal;m.Fullness=19;m.Aggression=1;m.Patience=0;
    m.TrueDisposition="greedy";m.Family.Pregnancy.Forced=false;
    var child=new Creature{Id="risk-child",Name="茎生幼体",IsYoung=true,RegionId=m.RegionId,X=1.45,Z=0,TargetX=1.45,TargetZ=0,
        Body=new(){Id="risk-child-body",SpeciesId="reimu",ReproductionRole=ReproductionRole.Maternal},
        Registration=new(){IdentityId="risk-child-identity"},Fullness=100,Energy=100,Health=100,
        Family=new(){MotherId=m.Id,FatherId=f.Id,BirthMethod=BirthMethod.Stem,BirthEventId="risk-old-birth",NextSocialAt=10000,NextCareAt=10000},
        Interaction=new(){Enabled=true,Hydration=100,NextAutonomyAt=10000,ToyInterest=0,Accessory=new(){Id="risk-child-accessory"}}};
    sim.State.FamilySequence=1;sim.State.CompletedBirths.Add("risk-old-birth");sim.State.Creatures.Add(child);
    m.Family.Peers[child.Id]=new(){Familiarity=10,Affection=0};child.Family.Peers[m.Id]=new(){Familiarity=10,Affection=10};
    return sim;
}
WorldSimulation FindThreat(bool lethal=true)
{
    for(ulong seed=1;seed<=256;seed++)
    {
        var sim=MaternalFixture(seed,lethal);Tick(sim,.25);
        if(sim.State.Creatures[0].Family.ChildThreat.TargetId!=null)return sim;
    }
    throw new Exception("多个独立种子下始终没有母体危机分支");
}

Check("材料有实际父体和身体来源，一次只收一份且会过期",()=>
{
    var sim=Fixture();var f=sim.State.Creatures[1];double energy=f.Energy,full=f.Fullness;Collect(sim);
    var material=sim.State.BreedingMaterials.Single();
    Require(material.FatherId==f.Id&&material.BodyId==f.Body.Id&&material.CollectedAt==sim.State.WorldSeconds&&material.ExpiresAt>material.CollectedAt,"材料没有有效来源或有限期限");
    Require(f.Energy<energy&&f.Fullness<full,"材料没有真实采集负担");
    for(int n=0;n<20;n++)sim.CollectBreedingMaterial(f.Id);
    Require(sim.State.BreedingMaterials.Count==1,"连续点击复制材料");
    double until=material.ExpiresAt-sim.State.WorldSeconds;Tick(sim,until+.5);
    Require(sim.State.BreedingMaterials.Count==0,"过期材料仍留在有效库存");
    var m=sim.State.Creatures[0];sim.RequestForcedPregnancy(m.Id,f.Id,BirthMethod.Stem);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.None,"用过期材料开始干预");
});
Check("采集尊重父体成熟身体意愿，强制也不绕母体成熟与角色",()=>
{
    foreach(int reason in new[]{0,1,2,3})
    {
        var sim=Fixture();var f=sim.State.Creatures[1];
        if(reason==0)f.IsYoung=true;if(reason==1)f.Body.ReproductionRole=ReproductionRole.Maternal;
        if(reason==2)f.Trust=0;if(reason==3)f.Pain=60;
        sim.CollectBreedingMaterial(f.Id);Require(sim.State.BreedingMaterials.Count==0,"采集未拒绝条件："+reason);
    }
    foreach(int reason in new[]{0,1,2,3})
    {
        var sim=Fixture();Collect(sim);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
        if(reason==0)m.IsYoung=true;if(reason==1)m.Body.ReproductionRole=ReproductionRole.Paternal;
        if(reason==2)m.Condition=PhysicalCondition.Critical;if(reason==3)m.Interaction.Hydration=0;
        sim.RequestForcedPregnancy(m.Id,f.Id,BirthMethod.Live);
        Require(m.Family.Pregnancy.Phase==PregnancyPhase.None&&sim.State.BreedingMaterials.Count==1,"无效母体仍开始或扣材料："+reason);
    }
});
Check("材料不能冒用另一具身体或不存在的来源",()=>
{
    var sim=Fixture();Collect(sim);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    sim.RequestForcedPregnancy(m.Id,"missing-father",BirthMethod.Stem);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.None&&sim.State.BreedingMaterials.Count==1,"不存在来源可用");
    f.Body.Id="replacement-body";sim.RequestForcedPregnancy(m.Id,f.Id,BirthMethod.Stem);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.None&&sim.State.BreedingMaterials.Count==1,"旧身体材料冒充新身体来源");
});
Check("普通安排仍拒绝，强制独立绕过意愿但非立即孕育且只扣一次",()=>
{
    var sim=Fixture();var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    m.Family.AllowsBreeding=false;double trust=m.Trust;
    sim.RequestPregnancy(m.Id,f.Id,BirthMethod.Stem);Require(m.Family.Pregnancy.Phase==PregnancyPhase.None,"普通入口绕过意愿");
    Force(sim);Require(sim.State.BreedingMaterials.Count==0&&m.Trust<trust&&m.Resentment>0,"强制没有立即材料与关系代价");
    Require(m.Family.Pregnancy.EventId==null&&m.Family.Pregnancy.PlannedChildren==0&&sim.State.Creatures.Count==2,"强制点击立即受孕或出生");
    double afterTrust=m.Trust;sim.RequestForcedPregnancy(m.Id,f.Id,BirthMethod.Live);
    Require(m.Family.Pregnancy.Method==BirthMethod.Stem&&m.Trust==afterTrust,"重复请求换方式或重复结算信任");
    Tick(sim,3);Require(FamilyRules.UnderIntervention(m)&&m.Family.Pregnancy.InterventionRemaining>0,"4秒过程提前结束");
    Tick(sim,1.25);Require(!FamilyRules.UnderIntervention(m)&&sim.State.Creatures.Count==2,"过程不结算或提前出生");
    Require(sim.State.BreedingMaterials.Count==0,"结算后材料重新出现");
});
Check("恢复不足可以强行开始但保留欠下的负担，停止不退材料",()=>
{
    var sim=Fixture();var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    m.Family.Pregnancy.Phase=PregnancyPhase.Recovering;m.Family.Pregnancy.RecoveryRemaining=90;m.Family.Pregnancy.RetryAt=90;
    sim.RequestPregnancy(m.Id,f.Id,BirthMethod.Live);Require(m.Family.Pregnancy.Phase==PregnancyPhase.Recovering,"普通入口忽略恢复期");
    Force(sim,BirthMethod.Live);Require(m.Family.Pregnancy.RecoveryDebt>0&&m.Family.Pregnancy.Burden>0,"强制重开清掉原恢复负担");
    double trust=m.Trust;Tick(sim,1);sim.InterruptPregnancyIntervention(m.Id);
    Require(!FamilyRules.UnderIntervention(m)&&m.Family.Pregnancy.Phase==PregnancyPhase.Recovering&&m.Family.Pregnancy.RecoveryRemaining>0,"停止后原恢复需求消失");
    Require(sim.State.BreedingMaterials.Count==0&&m.Trust<=trust&&m.Family.Pregnancy.EventId==null,"停止退回材料、关系损失或生成孕育");
});
Check("胎生恢复不足强改茎生，停止与存读都保留原恢复负担",()=>
{
    var sim=Fixture();var m=sim.State.Creatures[0];
    m.Family.Pregnancy.Phase=PregnancyPhase.Recovering;m.Family.Pregnancy.Method=BirthMethod.Live;
    m.Family.Pregnancy.RecoveryRemaining=250;m.Family.Pregnancy.RetryAt=250;
    Force(sim,BirthMethod.Stem);
    Require(m.Family.Pregnancy.Method==BirthMethod.Stem&&m.Family.Pregnancy.RecoveryDebt==250,"改为茎生清掉原胎生恢复负担");
    sim.InterruptPregnancyIntervention(m.Id);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Recovering&&m.Family.Pregnancy.RecoveryRemaining==250,"停止后恢复需求被较短方式截断");
    var restored=Reload(sim);var p=restored.State.Creatures[0].Family.Pregnancy;
    Require(p.Phase==PregnancyPhase.Recovering&&p.RecoveryRemaining==250&&p.RecoveryDebt==250,"读档改变或拒绝合法的原恢复负担");
    Require(restored.State.BreedingMaterials.Count==0&&restored.State.Creatures[0].Trust==m.Trust,"改方式或读档退回材料与关系代价");
});
Check("同档干预继续抽签一致且方式固定，既有孕育不能用停止按钮取消",()=>
{
    bool foundSuccess=false,foundFailure=false;
    for(ulong seed=1;seed<=256&&(!foundSuccess||!foundFailure);seed++)
    {
        var sim=Fixture(seed);Force(sim,BirthMethod.Live);Tick(sim,1);var copy=Reload(sim);
        Tick(sim,4);Tick(copy,4);
        Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(copy.State,SaveStore.JsonOptions),"读档重抽强制结果");
        var m=sim.State.Creatures[0];Require(m.Family.Pregnancy.Method==BirthMethod.Live,"方式被结果改变");
        if(m.Family.Pregnancy.Phase==PregnancyPhase.Gestating)
        {
            foundSuccess=true;string? eventId=m.Family.Pregnancy.EventId;sim.InterruptPregnancyIntervention(m.Id);
            Require(m.Family.Pregnancy.Phase==PregnancyPhase.Gestating&&m.Family.Pregnancy.EventId==eventId,"停止按钮抹掉已经开始的孕育");
        }
        else foundFailure=true;
    }
    Require(foundSuccess&&foundFailure,"强制操作被写成必成或必败");
});
Check("危险胎生可主动自保或坚持保胎，决定分别记录且存读不重抽",()=>
{
    bool self=false,protect=false;
    for(ulong seed=1;seed<=256&&(!self||!protect);seed++)
    {
        var sim=PregnancyDanger(seed);Tick(sim,.25);var m=sim.State.Creatures[0];var p=m.Family.Pregnancy;
        if(p.Decision==PregnancyDecision.PreserveSelf)
        {
            self=true;Require(p.Phase==PregnancyPhase.Recovering&&p.Outcome.Contains("主动",StringComparison.Ordinal),"主动自保没有单独记录或没有结束孕育");
        }
        if(p.Decision==PregnancyDecision.ProtectYoung)
        {
            protect=true;double health=m.Health;Tick(sim,2);
            Require(p.Phase==PregnancyPhase.Gestating&&m.Health<health,"坚持保胎没有持续的自身负担");
        }
        if(p.Decision!=PregnancyDecision.Undecided)
        {
            var decision=p.Decision;var restored=Reload(sim);Tick(restored,1);
            Require(restored.State.Creatures[0].Family.Pregnancy.Decision==decision,"读档改换已经作出的决定");
        }
    }
    Require(self&&protect,"缺少主动自保或坚持保胎分支");
});
Check("生理性吸收与主动自保分开，不凭空记录主动选择",()=>
{
    var sim=PregnancyDanger(5,true);var m=sim.State.Creatures[0];Tick(sim,1);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Recovering&&m.Family.Pregnancy.Outcome.Contains("生理",StringComparison.Ordinal),"持续危险没有生理吸收结果");
    Require(m.Family.Pregnancy.Decision==PregnancyDecision.Undecided,"生理吸收被冒充母体主动决定");
    Require(sim.State.Creatures.Count==2&&sim.State.CompletedBirths.Count==0,"吸收仍生成宝宝或出生结算");
});
Check("非强制茎生后代也可能出现危机，但不是每次压力都必然伤害",()=>
{
    bool threatening=false,peaceful=false;
    for(ulong seed=1;seed<=256&&(!threatening||!peaceful);seed++)
    {
        var sim=MaternalFixture(seed);Tick(sim,.25);var m=sim.State.Creatures[0];var child=sim.State.Creatures[2];
        Require(!m.Family.Pregnancy.Forced,"夹具错误地使用强制背景");
        if(m.Family.ChildThreat.TargetId!=null)
        {
            threatening=true;Require(m.Family.ChildThreat.TargetId==child.Id&&m.Family.ChildThreat.WarningRemaining>0&&child.Health==100,"危机跳过实际目标或预警");
        }
        else peaceful=true;
    }
    Require(threatening&&peaceful,"茎生危机被固定为必发或只允许强制背景");
});
Check("吃饱健康但存在家庭怨恨，危险预警不会下一帧误消失",()=>
{
    WorldSimulation? threatened=null;
    for(ulong seed=1;seed<=256;seed++)
    {
        var sim=MaternalFixture(seed);var m=sim.State.Creatures[0];var child=sim.State.Creatures[2];
        m.Fullness=100;m.Fear=m.Pain=m.Resentment=0;m.Family.Peers[child.Id].Resentment=60;
        Tick(sim,.25);
        if(m.Family.ChildThreat.TargetId==child.Id){threatened=sim;break;}
    }
    Require(threatened!=null,"有家庭怨恨却没有可达的危险预警");
    var mother=threatened!.State.Creatures[0];var young=threatened.State.Creatures[2];
    double warning=mother.Family.ChildThreat.WarningRemaining;Tick(threatened,.5);
    Require(mother.Fullness>90&&mother.Fear<45&&mother.Pain<25&&mother.Resentment<35,"夹具不是仅由家庭怨恨触发");
    Require(mother.Family.Peers[young.Id].Resentment>55,"家庭怨恨前置意外消失");
    Require(mother.Family.ChildThreat.TargetId==young.Id&&mother.Family.ChildThreat.WarningRemaining>0&&
        mother.Family.ChildThreat.WarningRemaining<warning&&young.Health==100,"预警下一帧误消失或跳过等待伤害幼体");
});
Check("危机有实际预警与公开干预，制止不清空长期后果",()=>
{
    var sim=FindThreat();var m=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    m.Family.Peers[child.Id].Resentment=60;Tick(sim,6);
    Require(child.Health==100&&m.Family.ChildThreat.TargetId==child.Id,"预警期提前动手或危险无故消失");
    double resentment=m.Family.Peers[child.Id].Resentment;sim.ProtectFamilyChild(m.Id);
    Require(m.Family.Peers[child.Id].Resentment==resentment,"保护按钮即时改写了已有怨恨");Tick(sim,5);
    Require(child.Health==100&&m.Family.ChildThreat.TargetId==null,"及时保护未阻止攻击");
    Require(m.Family.Peers[child.Id].Resentment>0,"保护后没有保留长期关系后果");
});
Check("危机必须真正靠近才能伤害，抱走幼体会中断",()=>
{
    var sim=FindThreat();var m=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    child.X=child.TargetX=4;Tick(sim,8);
    Require(child.Health==100,"未实际靠近就隔空伤害");
    child.Interaction.Carried=true;child.Interaction.Height=.5;Tick(sim,.5);
    Require(m.Family.ChildThreat.TargetId==null&&child.Health==100,"幼体被抱走仍追踪伤害");
});
Check("实际近身伤害走共同身体流程，致死开关分别生效",()=>
{
    foreach(bool lethal in new[]{false,true})
    {
        var sim=FindThreat(lethal);var m=sim.State.Creatures[0];var child=sim.State.Creatures[2];
        child.Health=0;child.Condition=PhysicalCondition.Critical;child.CriticalRemaining=90;child.FillingIntegrity=1;
        Tick(sim,10);
        Require(sim.State.Incidents.Any(e=>e.Kind==IncidentKind.Attack&&e.TargetId==child.Id),"没有发生真实近身攻击");
        Require(child.FillingIntegrity==0&&child.Interaction.Injuries.Sites.Any(s=>s.Part==BodyPart.Side),"危机伤害绕过共同身体与局部伤势");
        Require((child.Condition==PhysicalCondition.Dead)==lethal,"危机伤害绕过致死开关");
        Require(child.Family.Peers[m.Id].Resentment>0,"亲代伤害没有留下独立关系后果");
    }
});
Environment.ExitCode=failures==0?0:1;

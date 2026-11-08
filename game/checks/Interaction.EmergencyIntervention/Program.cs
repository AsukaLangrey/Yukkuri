using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
WorldSimulation Preparing(double debt=0,ulong seed=20261028,double health=100)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(seed);sim.State.UnlimitedResources=false;
    var mother=sim.Selected!;var father=sim.State.Creatures[1];
    mother.Health=health;mother.Fullness=90;mother.Energy=90;mother.Interaction.Hydration=90;mother.Trust=80;
    if(debt>0)
    {
        // Existing recovery is the explicit starting fixture. The new material
        // collection and intervention use real public transitions below.
        mother.Family.Pregnancy.Phase=PregnancyPhase.Recovering;
        mother.Family.Pregnancy.RecoveryRemaining=debt;mother.Family.Pregnancy.RetryAt=debt;
    }
    sim.CollectBreedingMaterial(father.Id);
    Require(sim.State.BreedingMaterials.Count==1,"夹具没有经真实入口取得材料");
    sim.RequestForcedPregnancy(mother.Id,father.Id,BirthMethod.Live);
    Require(FamilyRules.UnderIntervention(mother)&&mother.Family.Pregnancy.InterventionRemaining==4&&
        sim.State.BreedingMaterials.Count==0,"夹具没有开始准备并消费材料");
    SaveStore.Validate(sim.State);return sim;
}
WorldSimulation Gestating()
{
    // Take a reachable successful branch without fabricating EventId/PlannedChildren.
    for(ulong seed=1;seed<=64;seed++)
    {
        var sim=Preparing(seed:seed);sim.Advance(4);
        if(sim.Selected!.Family.Pregnancy.Phase==PregnancyPhase.Gestating)
        {SaveStore.Validate(sim.State);return sim;}
    }
    throw new Exception("真实准备过程在固定种子集合里没有到达孕育分支");
}
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
double EndCount(Creature c)=>c.Interaction.Memories.GetValueOrDefault("intervention-ended");
void RequireEnded(WorldSimulation sim,double debt,double trust,double attachment,double fear,double resentment,double retry)
{
    var c=sim.Selected!;var p=c.Family.Pregnancy;
    Require(c.Condition==PhysicalCondition.Critical&&!FamilyRules.UnderIntervention(c)&&p.InterventionRemaining==0,
        "濒危没有立即取消尚未完成的强制准备");
    Require(p.Phase==(debt>0?PregnancyPhase.Recovering:PregnancyPhase.None)&&p.RecoveryRemaining==debt&&p.RecoveryDebt==debt,
        "中断丢失原有恢复负担或新造了孕育");
    Require(p.EventId==null&&p.PlannedChildren==0&&sim.State.CompletedBirths.Count==0&&sim.State.BreedingMaterials.Count==0,
        "中断生成孩子、结算出生或返还已用材料");
    Require(c.Trust==trust&&c.Attachment==attachment&&c.Fear==fear&&c.Resentment==resentment&&c.Family.NextForcedAttemptAt==retry,
        "濒危中断重复扣关系或清掉已发生的关系/重试代价");
    Require(EndCount(c)==2,"中断结算不是一次既有权重的记忆");
}

Check("真实准备在近看濒危时立即结束并恢复托举救助入口",()=>
{
    foreach(double debt in new[]{0d,120d})
    {
        var sim=Preparing(debt);var mother=sim.Selected!;
        double trust=mother.Trust,attachment=mother.Attachment,fear=mother.Fear,resentment=mother.Resentment,retry=mother.Family.NextForcedAttemptAt;
        Require(sim.EnterCloseUp(),"未进入近看");double time=sim.State.WorldSeconds;
        sim.Hurt(mother,100,"突然的全身重伤",null);
        RequireEnded(sim,debt,trust,attachment,fear,resentment,retry);
        Require(sim.State.WorldSeconds==time,"必须推进普通时间才能结束准备");
        SaveStore.Validate(sim.State);
        Require(sim.BeginInteractionGesture(mother.Id,new(InteractionAction.Lift,Height:.8,X:mother.X,Z:mother.Z)).Success,
            "旧准备标志仍挡住濒危托举");
        sim.EndInteractionGesture(true);
        Require(mother.Interaction.Carried&&!mother.Interaction.Falling,"救助托举没有保持支撑");
        Require(sim.ExecuteInteraction(mother.Id,new(InteractionAction.PutDown,X:mother.X,Z:mother.Z)).Success,
            "濒危托举后不能放稳");
        Require(!mother.Interaction.Carried&&!mother.Interaction.Falling&&mother.Condition==PhysicalCondition.Critical,
            "放稳误治好了濒危或留下悬空动作");
    }
});
Check("普通时间与暂停中的外部重伤都同步清理，读档不会重启操作或重复扣账",()=>
{
    foreach(bool paused in new[]{false,true})
    {
        var sim=Preparing(120);var mother=sim.Selected!;sim.State.Settings.Pause=paused;
        double trust=mother.Trust,attachment=mother.Attachment,fear=mother.Fear,resentment=mother.Resentment,retry=mother.Family.NextForcedAttemptAt;
        sim.Hurt(mother,100,"外部重伤事件",null);RequireEnded(sim,120,trust,attachment,fear,resentment,retry);
        double budget=mother.CriticalRemaining;string outcome=mother.Family.Pregnancy.Outcome;
        var copy=Reload(sim);RequireEnded(copy,120,trust,attachment,fear,resentment,retry);
        Require(copy.Selected!.CriticalRemaining==budget&&copy.Selected.Family.Pregnancy.Outcome==outcome,"读档重置救治期限或中断结果");
        if(paused)
        {
            copy.AdvanceInteractionPresentation(.1);copy.Advance(5);
            Require(copy.Selected.CriticalRemaining==budget&&EndCount(copy.Selected)==2,"暂停仍推进身体或重结中断");
        }
        copy.State.Settings.Pause=false;copy.Advance(.25);
        Require(!FamilyRules.UnderIntervention(copy.Selected)&&copy.Selected.Family.Pregnancy.RecoveryDebt==120&&
            copy.Selected.Family.Pregnancy.RecoveryRemaining==120&&copy.State.BreedingMaterials.Count==0&&EndCount(copy.Selected)==2,
            "恢复普通时间重启准备、归还材料或再次记录中断");
    }
});
Check("准备完成时自身负担致濒危，只结算一次中断",()=>
{
    var sim=Preparing(900,health:35);var mother=sim.Selected!;
    double trust=mother.Trust,attachment=mother.Attachment,retry=mother.Family.NextForcedAttemptAt;
    sim.Advance(4);
    Require(mother.Condition==PhysicalCondition.Critical&&!FamilyRules.UnderIntervention(mother)&&
        mother.Family.Pregnancy.Phase==PregnancyPhase.Recovering&&mother.Family.Pregnancy.RecoveryRemaining==900,
        "真实完成伤害未保留恢复债务并中断准备");
    Require(EndCount(mother)==2,"Stop与完成分支重复调用取消，记了两次中断");
    Require(mother.Trust==trust&&mother.Attachment==attachment&&mother.Family.NextForcedAttemptAt==retry&&
        sim.State.BreedingMaterials.Count==0&&mother.Family.Pregnancy.EventId==null,"完成失败重复扣账或仍创建孕育事件");
    Reload(sim);
});
Check("已有孕育在濒危时不被未完成操作的清理误取消",()=>
{
    var sim=Gestating();var mother=sim.Selected!;var p=mother.Family.Pregnancy;
    string? eventId=p.EventId;int children=p.PlannedChildren;double elapsed=p.Elapsed;
    Require(sim.EnterCloseUp(),"未进入近看");sim.Hurt(mother,mother.Health,"孕育中的外部重伤",null);
    Require(mother.Condition==PhysicalCondition.Critical&&p.Phase==PregnancyPhase.Gestating&&p.EventId==eventId&&
        p.PlannedChildren==children&&p.Elapsed==elapsed&&EndCount(mother)==0,"误把已有孕育当成准备操作取消");
    var copy=Reload(sim);Require(copy.Selected!.Family.Pregnancy.Phase==PregnancyPhase.Gestating&&
        copy.Selected.Family.Pregnancy.EventId==eventId,"保存续接丢掉正在孕育的事件");
    copy.ExitCloseUp();copy.Advance(.25);
    Require(copy.Selected.Family.Pregnancy.Phase==PregnancyPhase.Gestating&&!copy.State.CompletedBirths.Contains(eventId!),
        "恢复普通时间误取消或直接结算未到期孕育");
});
Check("致命设置关闭仍取消重伤准备，不把终末指标误当已死亡",()=>
{
    var sim=Preparing();var mother=sim.Selected!;sim.State.Settings.LethalInjuries=false;
    sim.Hurt(mother,150,"保护模式下的极重伤害",null);
    Require(mother.Condition==PhysicalCondition.Critical&&mother.FillingIntegrity==0&&!FamilyRules.UnderIntervention(mother)&&
        mother.Family.Pregnancy.InterventionRemaining==0&&EndCount(mother)==2,"关闭致命伤害时仍保留幽灵准备或直接死亡");
    Require(sim.BeginInteractionGesture(mother.Id,new(InteractionAction.Lift,Height:.8,X:mother.X,Z:mother.Z)).Success,
        "保护模式仍不能托举救助");sim.EndInteractionGesture(true);
});
Check("当次致命伤和死亡读档保持死因及孕育死亡结果",()=>
{
    foreach(bool alreadyGestating in new[]{false,true})
    {
        var sim=alreadyGestating?Gestating():Preparing(120);var mother=sim.Selected!;
        sim.Hurt(mother,200,"确定的致命身体伤害",null);
        Require(mother.Condition==PhysicalCondition.Dead&&mother.Interaction.Injuries.DeathCause=="确定的致命身体伤害"&&
            mother.Family.Pregnancy.Phase==PregnancyPhase.None&&mother.Family.Pregnancy.Outcome=="母体离世，孕育中止。",
            "濒危清理把当次死亡的孕育结果覆盖成普通操作中断");
        Require(EndCount(mother)==0&&sim.State.BreedingMaterials.Count==0,"立即死亡前重复记录普通中断或退回材料");
        string outcome=mother.Family.Pregnancy.Outcome;int deaths=sim.State.Incidents.Count(e=>e.Kind==IncidentKind.Death&&e.TargetId==mother.Id);
        var copy=Reload(sim);copy.Advance(.5);var deceased=copy.State.Creatures.Single(c=>c.Id==mother.Id);
        Require(deceased.Interaction.Injuries.DeathCause=="确定的致命身体伤害"&&deceased.Family.Pregnancy.Outcome==outcome&&
            EndCount(deceased)==0&&deaths==1&&copy.State.Incidents.Count(e=>e.Kind==IncidentKind.Death&&e.TargetId==mother.Id)==1,
            "死亡清理或读档改写死因、孕育结果或重复事件");
    }
});

Console.WriteLine($"Emergency intervention: {failures} failure(s)");
return failures==0?0:1;

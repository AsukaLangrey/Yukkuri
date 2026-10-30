namespace Yukkuri.Simulation;

public enum PregnancyDecision { Undecided, PreserveSelf, ProtectYoung }
public sealed class BreedingMaterial
{
    public string FatherId { get; set; } = "";
    public string BodyId { get; set; } = "";
    public double CollectedAt { get; set; }
    public double ExpiresAt { get; set; }
}
public sealed class MaternalChildThreat
{
    public string? TargetId { get; set; }
    public double WarningRemaining { get; set; }
    public double Remaining { get; set; }
    public double StrikeRemaining { get; set; }
    public double NextCheckAt { get; set; }
    public string Cause { get; set; } = "";
}
public sealed partial class PregnancyState
{
    public bool Forced { get; set; }
    public double InterventionRemaining { get; set; }
    public double RecoveryDebt { get; set; }
    public double PrenatalAttachment { get; set; }
    public PregnancyDecision Decision { get; set; }
    public double NextDecisionAt { get; set; }
}
public sealed partial class FamilyLife
{
    public double NextMaterialAt { get; set; }
    public double NextForcedAttemptAt { get; set; }
    public MaternalChildThreat ChildThreat { get; set; } = new();
}
public sealed partial class WorldState
{
    public List<BreedingMaterial> BreedingMaterials { get; set; } = new();
}

public static partial class FamilyRules
{
    public static bool UnderIntervention(Creature c)=>c.Family.Pregnancy.Phase==PregnancyPhase.Preparing&&c.Family.Pregnancy.Forced;
    public static string DescribeRisk(Creature c)
    {
        var p=c.Family.Pregnancy;
        if(c.Family.ChildThreat.TargetId!=null)return "对幼体表现出危险敌意，需要立即隔开并处理它的饥饿、压力或冲突。";
        if(UnderIntervention(c))return "强制干预进行中，可停止；这不代表个体愿意，也不保证孕育成功。";
        if(p.Phase!=PregnancyPhase.Gestating)return "";
        return (p.Forced?"这次孕育源于强制干预；":"")+(p.Decision switch
        {
            PregnancyDecision.ProtectYoung=>"它在危险中选择继续保住孩子，自身负担加重，仍可能需要救治。",
            _=>p.RecoveryDebt>0?"上次恢复不足的负担仍在。":""
        });
    }
    public static void ValidateRisk(WorldState s)
    {
        static bool N(double v,double max=double.MaxValue)=>double.IsFinite(v)&&v>=0&&v<=max;
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        var ids=s.Creatures.Select(c=>c.Id).ToHashSet();
        Require(s.BreedingMaterials!=null&&s.BreedingMaterials.Count<=8,"繁育材料列表无效");
        foreach(var material in s.BreedingMaterials!)
            Require(material!=null&&ids.Contains(material.FatherId)&&!string.IsNullOrWhiteSpace(material.BodyId)&&
                N(material.CollectedAt,s.WorldSeconds+.001)&&N(material.ExpiresAt)&&
                Math.Abs(material.ExpiresAt-material.CollectedAt-180)<.001,"繁育材料来源或有效时间无效");
        Require(s.BreedingMaterials.Select(m=>m.FatherId).Distinct().Count()==s.BreedingMaterials.Count,"同一来源材料重复");
        foreach(var c in s.Creatures)
        {
            var f=c.Family;var p=f.Pregnancy;var threat=f.ChildThreat;
            Require(N(f.NextMaterialAt)&&N(f.NextForcedAttemptAt)&&N(p.InterventionRemaining,4)&&N(p.RecoveryDebt,900)&&
                N(p.PrenatalAttachment,100)&&N(p.NextDecisionAt)&&Enum.IsDefined(p.Decision),"孕育风险状态无效");
            Require(p.InterventionRemaining==0||p.Forced&&p.Phase==PregnancyPhase.Preparing,"干预计时脱离准备状态");
            Require(threat!=null&&N(threat.WarningRemaining,8)&&N(threat.Remaining,45)&&N(threat.StrikeRemaining,3)&&
                N(threat.NextCheckAt)&&threat.Cause!=null&&threat.Cause.Length<=256,"幼体威胁状态无效");
            Require(threat!.TargetId==null?threat.Remaining==0&&threat.WarningRemaining==0&&threat.StrikeRemaining==0:
                threat.TargetId!=c.Id&&ids.Contains(threat.TargetId)&&threat.Remaining>0&&
                s.Creatures.Any(x=>x.Id==threat.TargetId&&x.IsYoung&&x.Family.MotherId==c.Id&&x.Family.BirthMethod==BirthMethod.Stem),
                "幼体威胁缺少实际亲缘目标");
        }
    }
}

public sealed partial class WorldSimulation
{
    public string CollectBreedingMaterial(string fatherId)
    {
        var father=State.Creatures.Find(c=>c.Id==fatherId);var error=FamilyCommandError(father);
        if(error!=null)return error;
        if(father!.IsYoung||father.Body.ReproductionRole!=ReproductionRole.Paternal||!FamilyRules.SupportedSpecies(father.Body.SpeciesId))
            return "需要一只已成熟、具备父体角色的兼容个体。";
        if(!FamilyActionReady(father)||father.Trust<35||father.Fear>=35||father.Fullness<50||father.Energy<50)
            return "它没有接受这次材料采集，或身体还没有准备好。";
        if(State.WorldSeconds<father.Family.NextMaterialAt)return "刚采集过，需要休息，不能反复索取。";
        State.BreedingMaterials.RemoveAll(m=>m.ExpiresAt<=State.WorldSeconds);
        if(State.BreedingMaterials.Any(m=>m.FatherId==fatherId))return "已有这一来源的材料，先使用或等它失效。";
        if(State.BreedingMaterials.Count>=8)return "已有材料需要先妥善处理。";
        State.BreedingMaterials.Add(new(){FatherId=fatherId,BodyId=father.Body.Id,CollectedAt=State.WorldSeconds,ExpiresAt=State.WorldSeconds+180});
        father.Family.NextMaterialAt=State.WorldSeconds+180;father.Fullness-=5;father.Energy-=5;
        RecordInteraction(father,"breeding-material","接受了一次材料采集，接下来需要休息。",1);
        return State.LastActionMessage="已保存一份有来源的繁育材料；有效期有限，使用不会保证成功。";
    }

    public string RequestForcedPregnancy(string motherId,string fatherId,BirthMethod method)
    {
        var mother=State.Creatures.Find(c=>c.Id==motherId);var father=State.Creatures.Find(c=>c.Id==fatherId);
        var error=FamilyCommandError(mother);if(error!=null)return error;
        if(father==null||motherId==fatherId||!Enum.IsDefined(method)||mother!.IsYoung||father.IsYoung||
            mother.Body.ReproductionRole!=ReproductionRole.Maternal||father.Body.ReproductionRole!=ReproductionRole.Paternal||
            !FamilyRules.SupportedSpecies(mother.Body.SpeciesId)||!FamilyRules.SupportedSpecies(father.Body.SpeciesId))
            return "强制干预也不能改变成熟条件、固定角色或物种兼容。";
        var p=mother.Family.Pregnancy;
        if(p.Phase is PregnancyPhase.Gestating or PregnancyPhase.Preparing)return "已有孕育或操作进行中，不能重开或更换方式。";
        if(State.WorldSeconds<mother.Family.NextForcedAttemptAt||State.WorldSeconds<p.RetryAt&&p.Phase!=PregnancyPhase.Recovering)
            return "身体刚经历过一次尝试，需要先恢复，不能连续重试。";
        if(!InterventionBodyAvailable(mother)||mother.Energy<10||mother.Health<(method==BirthMethod.Live?35:20)||mother.Fullness<(method==BirthMethod.Live?30:15)||
            mother.Interaction.Hydration<(method==BirthMethod.Live?30:15))
            return "当前身体状态无法进行这项操作，先处理危急伤势、支撑和眼前需要。";
        var material=State.BreedingMaterials.Find(m=>m.FatherId==fatherId&&m.BodyId==father.Body.Id&&m.ExpiresAt>State.WorldSeconds);
        if(material==null)return "缺少这一父体来源的有效材料。";
        double debt=p.Phase==PregnancyPhase.Recovering?p.RecoveryRemaining:0;
        State.BreedingMaterials.Remove(material);
        CancelWildSelfCare(mother,"开始繁育干预，自行护伤已中断；尚未获得护理效果。");
        ClearCompanionship(mother);EndHumanCompany(mother);CancelFamilyCare(mother,reason:"开始繁育干预，递送已中断；已取份额留在身旁。");
        mother.Interaction.Resting=false;mother.Interaction.RestRemaining=0;
        mother.Interaction.PendingConversation=null;mother.Interaction.ConversationPraise=null;mother.Interaction.ConversationDelay=0;
        mother.Interaction.PendingPraise=null;mother.Interaction.PraiseRemaining=0;
        mother.Interaction.SocialResponseRemaining=0;mother.Interaction.ReactionPose="idle";mother.Interaction.ReactionPoseRemaining=0;
        p.Phase=PregnancyPhase.Preparing;p.Method=method;p.FatherId=fatherId;p.EventId=null;p.PlannedChildren=0;
        p.Forced=true;p.InterventionRemaining=4;p.RecoveryDebt=debt;p.RecoveryRemaining=0;
        p.Elapsed=0;p.WaitingSeconds=0;p.Burden=Math.Min(60,10+debt*.15);p.Outcome="";
        p.Decision=PregnancyDecision.Undecided;p.PrenatalAttachment=mother.Patience*30;p.NextDecisionAt=State.WorldSeconds+30;
        mother.Family.NextForcedAttemptAt=State.WorldSeconds+120;
        mother.Trust=Math.Max(0,mother.Trust-15);mother.Attachment=Math.Max(0,mother.Attachment-8);
        mother.Fear=Clamp100(mother.Fear+12);mother.Resentment=Clamp100(mother.Resentment+18);
        RememberPlayerHarm(mother,5,cause:"强制繁育干预");
        RecordInteraction(mother,"forced-breeding","玩家无视意愿开始强制干预，损害了信任；材料已经使用。",6);
        mother.Activity="强制干预进行中";
        return State.LastActionMessage="已开始强制干预，可及时停止。材料已经使用，信任受损；恢复不足会加重风险。";
    }

    private bool InterventionBodyAvailable(Creature c)=>(c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering)&&
        !c.Family.ColdStorage.Stored&&!c.Interaction.Water.InWater&&!WaterEnvironment.IsInWater(State,c)&&
        !c.Interaction.Carried&&!c.Interaction.Falling&&!c.Interaction.Lowering&&c.Interaction.Height<=.05&&
        c.Interaction.Restraint==RestraintKind.None&&!c.Interaction.Supine&&!c.Interaction.Physiology.Active&&
        c.Interaction.EatingRemaining<=0&&c.Interaction.ActiveCue==null&&c.Family.ColdStorage.RecoveryRemaining<=0&&
        c.Family.Conflict.TargetId==null&&c.Family.ChildThreat.TargetId==null&&InteractionGestureTarget!=c.Id;

    public string InterruptPregnancyIntervention(string motherId)
    {
        var c=State.Creatures.Find(c=>c.Id==motherId);var error=FamilyCommandError(c);if(error!=null)return error;
        if(!FamilyRules.UnderIntervention(c!))return "没有正在进行的强制干预；已经开始的孕育不能由此取消。";
        CancelIntervention(c!,"干预及时停止，没有开始新的孕育；已用材料和信任损失保留。");
        return State.LastActionMessage=c!.Family.Pregnancy.Outcome;
    }
    private void CancelIntervention(Creature c,string outcome)
    {
        var p=c.Family.Pregnancy;p.InterventionRemaining=0;p.Elapsed=0;
        p.RecoveryRemaining=p.RecoveryDebt;p.Phase=p.RecoveryRemaining>0?PregnancyPhase.Recovering:PregnancyPhase.None;
        if(c.Condition==PhysicalCondition.Dead){p.RecoveryRemaining=0;p.Phase=PregnancyPhase.None;}
        p.RetryAt=Math.Max(p.RetryAt,State.WorldSeconds+60);p.Outcome=outcome;c.Family.LastEvent=outcome;
        RecordInteraction(c,"intervention-ended",outcome,2);
    }
    private void AdvanceForcedIntervention(Creature mother,Creature? father,double dt)
    {
        var p=mother.Family.Pregnancy;
        if(father==null||!InterventionBodyAvailable(mother))
        {CancelIntervention(mother,"身体状态改变，干预中断；没有开始新的孕育。");return;}
        p.InterventionRemaining=Math.Max(0,p.InterventionRemaining-dt);
        if(p.InterventionRemaining>0)return;
        // One physical settlement and one random decision; neither is repeated on load.
        Hurt(mother,4+p.RecoveryDebt*.06,"强制繁育负担","player",BodyPart.Side);
        if(mother.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical)
        {
            // Critical/death cleanup may already have ended preparation during Hurt.
            // Do not emit a second cancellation or overwrite the recorded death outcome.
            if(FamilyRules.UnderIntervention(mother))CancelIntervention(mother,"强制干预造成严重后果，新的孕育未能开始。");
            return;
        }
        double chance=FamilyRules.CrossEcology(mother,father)?.02:.65;
        if(Next()>=chance){CancelIntervention(mother,"强制干预没有使孕育开始，身体与关系后果仍在。");return;}
        p.Phase=PregnancyPhase.Gestating;p.EventId=NewFamilyId("birth");p.Elapsed=0;p.WaitingSeconds=0;
        SettleBreedingInterest(mother,father);
        p.PlannedChildren=p.Method==BirthMethod.Live?1:2+(Next()<.25?1:0);
        p.NextDecisionAt=State.WorldSeconds+30;mother.Family.LastEvent="强制干预后开始孕育，需要密切照护身体与情绪。";
        RecordInteraction(mother,"forced-pregnancy",mother.Family.LastEvent,5);
    }

    private bool ConsiderPregnancyRisk(Creature mother,double dt)
    {
        var p=mother.Family.Pregnancy;
        if(p.Method!=BirthMethod.Live)return false;
        bool danger=p.Burden>=40||mother.Health<35||mother.Fullness<18;
        if(danger&&p.Decision==PregnancyDecision.Undecided&&!mother.Family.ColdStorage.Stored&&
            mother.Condition==PhysicalCondition.Active&&State.WorldSeconds>=p.NextDecisionAt)
        {
            p.NextDecisionAt=State.WorldSeconds+30;
            double protection=Math.Clamp(.12+mother.Patience*.35+p.PrenatalAttachment*.004-mother.Aggression*.15,0,.85);
            double choice=Next();
            if(choice<protection)
            {
                p.Decision=PregnancyDecision.ProtectYoung;
                mother.Family.LastEvent="危险中仍选择保住孩子；这份坚持会增加自身负担，并不能保证双方平安。";
                RecordInteraction(mother,"protect-pregnancy",mother.Family.LastEvent,5);
            }
            else if(choice<protection+Math.Clamp(.12+(1-mother.Patience)*.25+(p.Forced?.12:0),0,.55))
            {
                p.Decision=PregnancyDecision.PreserveSelf;
                EndPregnancy(mother,"母体在危险中主动选择保全自己，结束了这次胎生孕育；这与身体自行吸收分别记录。");
                return true;
            }
        }
        if(danger&&p.Decision==PregnancyDecision.ProtectYoung)
            Hurt(mother,dt*.06,"坚持保胎的身体负担",null);
        return mother.Condition==PhysicalCondition.Dead;
    }

    public string ProtectFamilyChild(string motherId)
    {
        var mother=State.Creatures.Find(c=>c.Id==motherId);var error=FamilyCommandError(mother);if(error!=null)return error;
        if(mother!.Family.ChildThreat.TargetId==null)return "它当前没有针对幼体的危险行动。";
        StopChildThreat(mother,"危险行动被及时制止；仍需处理饥饿、压力与家庭关系。",120);
        return State.LastActionMessage=mother.Family.LastEvent;
    }
    private void StopChildThreat(Creature c,string outcome,double cooldown=60)
    {
        if(c.Family.ChildThreat.TargetId==null)return;
        c.Family.ChildThreat=new(){NextCheckAt=State.WorldSeconds+cooldown};c.Family.LastEvent=outcome;
        c.TargetX=c.X;c.TargetZ=c.Z;RecordInteraction(c,"child-threat-ended",outcome,2);
    }
    private void TickMaternalRisks(double dt)
    {
        State.BreedingMaterials.RemoveAll(m=>m.ExpiresAt<=State.WorldSeconds);
        foreach(var c in State.Creatures)
        {
            var risk=c.Family.ChildThreat;
            if(risk.TargetId!=null)
            {
                risk.Remaining=Math.Max(0,risk.Remaining-dt);risk.WarningRemaining=Math.Max(0,risk.WarningRemaining-dt);
                risk.StrikeRemaining=Math.Max(0,risk.StrikeRemaining-dt);
                var child=State.Creatures.Find(x=>x.Id==risk.TargetId);
                if(risk.Remaining<=0||!CanSelfMove(c)||c.Family.ColdStorage.Stored||child==null||!child.IsYoung||
                    child.Condition==PhysicalCondition.Dead||child.Family.ColdStorage.Stored||child.Interaction.Carried||
                    child.RegionId!=c.RegionId||c.Fullness>40&&c.Fear<45&&c.Pain<25&&
                    !c.Family.Peers.Values.Any(b=>b.Resentment>55))
                    StopChildThreat(c,"与幼体的危险状态已中断，仍需继续照护。",120);
                continue;
            }
            if(c.IsYoung||!CanSelfMove(c)||c.Family.ColdStorage.Stored||FamilyRules.UnderIntervention(c)||
                c.Family.Conflict.TargetId!=null||c.Family.FlightRemaining>0||c.Interaction.EatingRemaining>0||
                c.Interaction.ActiveCue!=null||InteractionGestureTarget==c.Id||State.WorldSeconds<risk.NextCheckAt)continue;
            risk.NextCheckAt=State.WorldSeconds+30;
            double pressure=(c.Fullness<20?.25:0)+(c.Fear>60||c.Pain>45?.18:0)+
                (c.Family.Peers.Values.Any(b=>b.Resentment>55)?.2:0);
            if(pressure<=0||c.Aggression<.35||c.Patience>.8)continue;
            var target=State.Creatures.Where(x=>x.IsYoung&&x.Condition!=PhysicalCondition.Dead&&x.Family.MotherId==c.Id&&
                x.Family.BirthMethod==BirthMethod.Stem&&FamilyVisible(c,x,3)&&!x.Interaction.Carried)
                .OrderBy(x=>x.Id,StringComparer.Ordinal).FirstOrDefault();
            if(target==null)continue;
            double affection=c.Family.Peers.GetValueOrDefault(target.Id)?.Affection??0;
            if(Next()>=Math.Clamp(pressure*c.Aggression*(1-affection*.007),0,.5))continue;
            CancelWildSelfCare(c,"危险冲动打断了自行护伤；尚未获得护理效果。");
            ClearCompanionship(c);CancelFamilyCare(c,reason:"对幼体显露危险敌意，原来的照护已中断；已取份额留在身旁。");
            c.Family.ChildThreat=new(){TargetId=target.Id,WarningRemaining=8,Remaining=45,NextCheckAt=State.WorldSeconds+120,
                Cause=c.Fullness<20?"饥饿与求生压力":c.Family.Peers.Values.Any(b=>b.Resentment>55)?"家庭冲突与怨恨":"持续恐惧或疼痛压力"};
            c.Family.LastEvent="对幼体显露危险敌意，尚未动手；可以及时制止或隔开。";
            React(c,Expression.Angry,"离开……！",c.Family.LastEvent,6);RecordInteraction(c,"child-threat",c.Family.LastEvent,4);
        }
    }
    private bool CanPursueChildThreat(Creature c,Creature child) => c!=child&&CanSelfMove(c)&&
        c.Family.ChildThreat.Remaining>0&&!FamilyRules.UnderIntervention(c)&&!WaterEnvironment.IsInWater(State,c)&&child.IsYoung&&
        c.Pain<70&&c.Fear<90&&c.Family.FlightRemaining<=0&&c.RetreatRemaining<=0&&
        c.Interaction.EatingRemaining<=0&&c.Interaction.ActiveCue==null&&InteractionGestureTarget!=c.Id&&
        !(c.Fullness>40&&c.Fear<45&&c.Pain<25&&!c.Family.Peers.Values.Any(b=>b.Resentment>55))&&
        child.Condition!=PhysicalCondition.Dead&&FamilyVisible(c,child,5)&&!child.Interaction.Carried;

    private bool TryMaternalCrisis(Creature c,double dt)
    {
        var risk=c.Family.ChildThreat;if(risk.TargetId==null)return false;
        var child=State.Creatures.Find(x=>x.Id==risk.TargetId);
        if(child==null||!CanPursueChildThreat(c,child))
        {StopChildThreat(c,"身体做不到或双方已经隔开，危险行动停止。",120);return false;}
        var protector=State.Creatures.OrderBy(a=>a.Id,StringComparer.Ordinal).FirstOrDefault(a=>a.Id!=c.Id&&a.Id!=child.Id&&CanProtectFamily(a)&&a.Courage>=.55&&
            child.Family.CaregiverIds.Contains(a.Id)&&FamilyInContact(a,child)&&FamilyVisible(a,c,2.5));
        if(protector!=null){StopChildThreat(c,$"{protector.Name}及时护住了幼体，双方被隔开。",120);return false;}
        if(risk.WarningRemaining>0){c.Activity="紧盯幼体，显露危险敌意";return true;}
        if(!FamilyInContact(c,child)){RoomMove(c,child.X,child.Z,dt);c.Activity="危险地靠近幼体";return true;}
        c.Activity="正在伤害幼体";if(risk.StrikeRemaining>0)return true;
        risk.StrikeRemaining=3;Hurt(child,8+c.Aggression*8,"母体在压力下伤害幼体",c.Id,BodyPart.Side);
        var incident=Emit(IncidentKind.Attack,child,c.Id,$"{c.Name}在{risk.Cause}下伤害幼体",8);
        Remember(child,incident,"受到亲代伤害，需要保护。",8);
        var bond=FamilyBond(child,c);bond.Fear=Clamp100(bond.Fear+15);bond.Resentment=Clamp100(bond.Resentment+10);bond.LastHarmAt=State.WorldSeconds;
        if(child.Condition==PhysicalCondition.Dead)StopChildThreat(c,"幼体在伤害中离世，已发生的后果不会被取消。",300);
        else
        {
            React(child,Expression.Calling,"救救……", "受到亲代伤害，正在求助。",4);
            if(CanSelfMove(child))
            {
                double dx=child.X-c.X,dz=child.Z-c.Z;
                if(Math.Abs(dx)+Math.Abs(dz)<.001)dx=1;
                double distance=Math.Sqrt(dx*dx+dz*dz);
                child.Family.FlightX=Math.Clamp(child.X+dx/distance*3,-7,7);
                child.Family.FlightZ=Math.Clamp(child.Z+dz/distance*3,-7,7);
                child.Family.FlightRemaining=5;
            }
        }
        return true;
    }
}

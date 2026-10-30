namespace Yukkuri.Simulation;

public enum InteractionHelpKind { Critical, Water, Threat }
public sealed record InteractionHelpRequest(Creature Target,InteractionHelpKind Kind,string Description,
    Creature? ThreateningCreature=null);

public sealed partial class WorldSimulation
{
    public Creature? CurrentInteractionThreatTarget(Creature actor) =>
        actor.RegionId==State.ActiveRegionId?ValidInteractionThreatTarget(actor):null;

    // Ordinary life may also advance outside the currently viewed courtyard.
    // UI scope must not erase a danger there from the behavioural rules.
    private Creature? ValidInteractionThreatTarget(Creature actor)
    {
        if(actor.Personality.HungerTargetId==null&&actor.Family.ChildThreat.TargetId==null&&actor.Family.Conflict.TargetId==null)return null;
        if(!State.InteractionPlaytest||!State.Creatures.Contains(actor)||
            actor.Condition==PhysicalCondition.Dead||actor.Family.ColdStorage.Stored||actor.Family.Memorialized)return null;
        Creature? Eligible(string? id,bool allowRemains=false) => id==null?null:State.Creatures.Find(c=>c.Id==id&&c!=actor&&c.RegionId==actor.RegionId&&
            (c.Condition!=PhysicalCondition.Dead||allowRemains&&c.Personality.RemainsNutrition>0)&&
            !c.Family.ColdStorage.Stored&&!c.Family.Memorialized);
        // Match the ordinary behaviour order, rather than exposing every stale
        // target field as a simultaneous action by the same body.
        var target=Eligible(actor.Personality.HungerTargetId,true);
        if(target!=null&&CanPursueHungerTarget(actor,target))return target;
        target=Eligible(actor.Family.ChildThreat.TargetId);
        if(target!=null&&CanPursueChildThreat(actor,target))return target;
        target=Eligible(actor.Family.Conflict.TargetId);
        return target!=null&&CanPursueFamilyConflict(actor,target)?target:null;
    }

    public string InterruptInteractionThreat(string actorId)
    {
        var found=State.Creatures.Find(c=>c.Id==actorId);
        var error=FamilyCommandError(found);
        if(error!=null)return error;
        var actor=found!;
        var target=CurrentInteractionThreatTarget(actor);
        if(target==null)return "它当前没有仍可进行的危险行动，先查看实际情况。";
        string message;
        if(actor.Personality.HungerTargetId==target.Id&&CanPursueHungerTarget(actor,target))
        {
            bool remains=target.Condition==PhysicalCondition.Dead;
            StopHungerAggression(actor,remains?"被人类制止了啃食残躯；仍需补充正常食物或隔开照看":
                "被人类及时制止饥饿袭击；仍需补充食物或隔开照看");
            React(actor,Expression.Worried,"……好饿。","停止逼近同伴，仍需解决实际饥饿。",5);
            message=remains?"已制止啃食残躯；仍需喂食或隔开，死亡和既有后果不会改变。":
                "已制止这次饥饿袭击；请喂食或隔开，已有伤势与关系保留。";
        }
        else if(actor.Family.ChildThreat.TargetId==target.Id&&CanPursueChildThreat(actor,target))
        {
            StopChildThreat(actor,"危险行动被及时制止；仍需处理饥饿、压力与家庭关系。",120);
            message="已制止这次对幼体的危险行动，已有伤势与关系保留。";
        }
        else
        {
            StopFamilyConflict(actor,"被及时制止，暂时退开；受伤的关系仍需处理");
            React(actor,Expression.Worried,"……先走开。","停止靠近，留出了安全距离。",5);
            message="已阻止这次冲突，伤势、记忆和关系不会被清空。";
        }
        if(target.Condition!=PhysicalCondition.Dead)AcknowledgeHumanThreatInterruption(actor,target);
        return State.LastActionMessage=message;
    }

    private void AcknowledgeHumanThreatInterruption(Creature actor,Creature target)
    {
        // Reaching this method requires a real, revalidated danger which the
        // command has actually stopped. A second click cannot create an event.
        var attack=State.Incidents.LastOrDefault(e=>e.Kind==IncidentKind.Attack&&e.ActorId==actor.Id&&
            e.TargetId==target.Id&&State.WorldSeconds-e.At<=120);
        var incident=Emit(IncidentKind.Protection,target,"player",$"人类及时制止了{actor.Name}对{target.Name}的危险行动",0,attack?.Id);
        bool RecentHarm(Creature c)=>c.Interaction.HumanBond.LastHarmAt>=0&&
            State.WorldSeconds-c.Interaction.HumanBond.LastHarmAt<120;
        bool manufactured=RecentHarm(actor)||RecentHarm(target)||
            target.Interaction.Water.LastPlayerSubmersionAt>=0&&State.WorldSeconds-target.Interaction.Water.LastPlayerSubmersionAt<120;
        foreach(var observer in State.Creatures.Where(c=>c!=actor&&c.Condition!=PhysicalCondition.Dead&&
            !c.Family.ColdStorage.Stored&&(c==target||FamilyVisible(c,target)&&FamilyVisible(c,actor))))
        {
            InitializePersonality(observer);
            bool direct=observer==target;
            if(!direct&&observer.Personality.Kindness<.25)continue;
            bool entitled=observer.Personality.Kindness<.25&&observer.Personality.Pride>=.75;
            string memory=manufactured?"人类制止了眼前的危险，但刚才受到的人类伤害仍然记得。":
                direct?entitled?"人类替自己挡住了危险，觉得这是理所当然。":"人类替自己挡住了危险，更愿意在不安时寻求帮助。":
                $"亲眼看见人类保护了{target.Name}，更愿意相信这只手。";
            Remember(observer,incident,memory,direct?2:1);
            observer.Family.LastEvent=memory;
            var bond=observer.Interaction.HumanBond;
            if(manufactured||RecentHarm(observer)||bond.LastProtectionAt>=0&&State.WorldSeconds-bond.LastProtectionAt<120)continue;
            // Shared care budget and a saved cooldown bound repeated rescues.
            // Neither gratitude nor its dialogue removes danger memories.
            bond.LastProtectionAt=State.WorldSeconds;
            double budget=bond.CareBudget;
            ImproveAffection(observer,direct?(entitled?.12:.9):.35);
            bond.Safety=Clamp100(bond.Safety+(budget-bond.CareBudget)*.6);
        }
    }

    // The courtyard is observed through an overview camera, not a walking
    // avatar. Restrict to the current courtyard; peers still need real sight
    // and the same action eligibility as their behavioural rules.
    public InteractionHelpRequest? InteractionHelpFor(Creature c)
    {
        if(!State.Creatures.Contains(c)||!CanObserveInteractionHelp(c))return null;
        var attacker=State.Creatures.Where(other=>other!=c&&CurrentInteractionThreatTarget(other)==c)
            .OrderBy(other=>Distance(other.X,other.Z,c.X,c.Z)).ThenBy(other=>other.Id,StringComparer.Ordinal).FirstOrDefault();
        return DescribeInteractionHelp(c,attacker);
    }

    private bool CanObserveInteractionHelp(Creature c) => State.InteractionPlaytest&&c.RegionId==State.ActiveRegionId&&
        c.Condition!=PhysicalCondition.Dead&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized;

    private InteractionHelpRequest? DescribeInteractionHelp(Creature c,Creature? attacker)
    {
        if(c.Condition==PhysicalCondition.Critical)
            return new(c,InteractionHelpKind.Critical,"伤势危急，需要及时稳定伤势",attacker);
        if(WaterEnvironment.IsInWater(State,c))
            return new(c,InteractionHelpKind.Water,"正在水里，需要脱险或救援",attacker);
        return attacker==null?null:new(c,InteractionHelpKind.Threat,$"正受到{attacker.Name}的威胁，需要及时隔开",attacker);
    }

    public IReadOnlyList<InteractionHelpRequest> InteractionHelpRequests()
    {
        if(!State.InteractionPlaytest)return Array.Empty<InteractionHelpRequest>();
        // A local snapshot only: no persistent cache or delayed invalidation.
        // Resolve each active actor once instead of rescanning every target id
        // for every possible victim on each panel refresh.
        var threats=State.Creatures.Select(actor=>(Actor:actor,Target:CurrentInteractionThreatTarget(actor)))
            .Where(threat=>threat.Target!=null).ToArray();
        return State.Creatures.Where(CanObserveInteractionHelp).Select(c=>DescribeInteractionHelp(c,
                threats.Where(threat=>threat.Target==c).OrderBy(threat=>Distance(threat.Actor.X,threat.Actor.Z,c.X,c.Z))
                    .ThenBy(threat=>threat.Actor.Id,StringComparer.Ordinal).Select(threat=>threat.Actor).FirstOrDefault()))
            .OfType<InteractionHelpRequest>().OrderBy(help=>help.Kind)
            .ThenBy(help=>help.Kind==InteractionHelpKind.Critical?help.Target.CriticalRemaining:0)
            .ThenByDescending(help=>help.Target.Id==State.HelpTargetId)
            .ThenBy(help=>help.Target.Id,StringComparer.Ordinal).ToArray();
    }

    public Creature? CurrentInteractionHelpTarget() => InteractionHelpRequests().FirstOrDefault()?.Target;
}

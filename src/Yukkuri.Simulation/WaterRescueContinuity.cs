namespace Yukkuri.Simulation;

public enum WaterSupportPhase { Calling, Responding, Approaching, Watching }
public sealed record WaterSupportView(string TargetId,string EventId,WaterSupportPhase Phase,double ContactSeconds);

public sealed class WaterAftercareState
{
    public string? TargetId { get; set; }
    public string? EventId { get; set; }
    public double StartedAt { get; set; }
    public double Until { get; set; }
    public double ContactSeconds { get; set; }
    public bool Remembered { get; set; }
}

public sealed partial class WorldSimulation
{
    private bool RecentlyHarmedByHuman(Creature c) => c.Interaction.HumanBond.LastHarmAt>=0&&
        State.WorldSeconds-c.Interaction.HumanBond.LastHarmAt<120;

    private void RewardWaterProtection(Creature c,double amount)
    {
        var b=c.Interaction.HumanBond;
        if(RecentlyHarmedByHuman(c)||b.LastProtectionAt>=0&&State.WorldSeconds-b.LastProtectionAt<120)return;
        b.LastProtectionAt=State.WorldSeconds;
        double before=b.CareBudget,fear=c.Fear;
        ImproveAffection(c,amount);
        b.Safety=Clamp100(b.Safety+(before-b.CareBudget)*.6);
        // Rescue changes the relationship, not the fear already experienced.
        c.Fear=Math.Max(c.Fear,fear);
    }

    private void NormalizeWaterEpisode(Creature c)
    {
        var w=c.Interaction.Water;
        if(!w.InWater||w.EventId!=null)return;
        w.EventId=NewFamilyId("water");
        w.EnteredAt=Math.Max(0,State.WorldSeconds-w.ExposureSeconds);
        w.PlayerCaused=w.LastPlayerSubmersionAt>=w.EnteredAt-.001&&w.LastPlayerSubmersionAt>=0;
        w.HumanRescueCredited=false;
    }

    private bool HasPriorityWaterDuty(Creature c) => c.Family.CareTargetId!=null||c.Family.CareParcelId!=null||
        State.Creatures.Any(child=>CanChooseAutonomousFamilyCare(c,child))||
        State.Creatures.Any(child=>child!=c&&child.IsYoung&&IsCareParent(c,child)&&
            child.Condition!=PhysicalCondition.Dead&&!child.Family.ColdStorage.Stored&&FamilyVisible(c,child,5)&&
            (child.Condition==PhysicalCondition.Critical||child.Fullness<55||child.Interaction.Hydration<55||child.Family.Warmth<60));

    private bool CanActivelySupportWater(Creature c)
    {
        var i=c.Interaction;
        return CanRespondToWater(c)&&c.Pain<25&&c.Fear<55&&c.Energy>30&&
            !c.Quarantine&&!c.IsIntruder&&c.Family.ColdStorage.RecoveryRemaining<=0&&
            !NaturalResources.WantsFood(c)&&!NaturalResources.WantsWater(c)&&
            !i.Digestion.Symptomatic&&!i.Physiology.Active&&!i.Injuries.SelfCare.Active&&
            !i.Resting&&i.RestRemaining<=0&&!i.FollowPlayer&&i.ActiveCue==null&&i.WaitRemaining<=0&&
            !i.Lowering&&i.Height<=.05&&InteractionGestureTarget!=c.Id&&
            c.Family.FlightRemaining<=0&&c.RetreatRemaining<=0&&c.Family.Conflict.TargetId==null&&
            c.Family.ChildThreat.TargetId==null&&c.Personality.HungerTargetId==null&&
            c.Family.CompanionTargetId==null&&!HasPriorityWaterDuty(c)&&
            !State.Creatures.Any(actor=>actor!=c&&ValidInteractionThreatTarget(actor)==c);
    }

    private static void ClearWaterAftercare(Creature c) => c.Personality.WaterAftercare=new();

    private void SynchronizeWaterAftercare(Creature c)
    {
        var care=c.Personality.WaterAftercare;
        if(care.TargetId==null)return;
        if(care.Until<=State.WorldSeconds||!State.Creatures.Any(target=>target.Id==care.TargetId&&
            target.Condition!=PhysicalCondition.Dead&&target.Interaction.Water.EventId==care.EventId))
        {ClearWaterAftercare(c);return;}
        var target=ValidWaterAftercareTarget(c);
        if(!care.Remembered&&(target==null||Distance(c.X,c.Z,target.X,target.Z)>WaterWatchingDistance(c,target)+.08))
            care.ContactSeconds=0;
    }

    private void BeginWaterAftercare(Creature target)
    {
        var w=target.Interaction.Water;
        if(w.EventId==null||target.Condition==PhysicalCondition.Dead)return;
        foreach(var observer in State.Creatures)
        {
            var p=observer.Personality;
            // Only someone who actually followed this immersion can carry its
            // concern onto land. A stale id or an unseen rescue is insufficient.
            if(p.Kindness<.5||p.WaterConcernId!=target.Id||p.WaterConcernEventId!=w.EventId||
                !CanWitnessWater(observer,target))continue;
            p.WaterAftercare=new(){TargetId=target.Id,EventId=w.EventId,StartedAt=State.WorldSeconds,Until=State.WorldSeconds+30};
            p.WaterConcernId=null;p.WaterConcernEventId=null;
        }
    }

    private Creature? ValidWaterAftercareTarget(Creature observer)
    {
        var care=observer.Personality.WaterAftercare;
        if(care.TargetId==null||care.Until<=State.WorldSeconds||!CanActivelySupportWater(observer))return null;
        var target=State.Creatures.Find(c=>c.Id==care.TargetId);
        return target!=null&&target.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering&&
            target.Interaction.Water.EventId==care.EventId&&target.Interaction.Water.RecoverySeconds>0&&
            !WaterEnvironment.IsInWater(State,target)&&!target.Interaction.Carried&&!target.Interaction.Falling&&
            !target.Interaction.Lowering&&target.Interaction.Height<=.05&&!FamilyRules.UnderIntervention(target)&&
            !target.Interaction.Physiology.Active&&!target.Interaction.Injuries.SelfCare.Active&&
            InteractionGestureTarget!=target.Id&&CareRelationshipSafe(observer,target)&&
            CanWitnessWater(observer,target)?target:null;
    }

    private static double WaterWatchingDistance(Creature observer,Creature target) =>
        InteractionFootprint(observer)+InteractionFootprint(target)+.25;

    // Pure query: no migration, event creation, reaction, or clock advancement.
    // Old WaterConcernTarget remains strictly a submerged-target query.
    public WaterSupportView? WaterSupportStatus(Creature observer)
    {
        if(!State.InteractionPlaytest||State.Mode==PlayMode.CloseUp||!State.Creatures.Contains(observer))return null;
        var wet=WaterConcernTarget(observer);
        if(wet!=null&&observer.Personality.Kindness>=.5&&wet.Interaction.Water.EventId is { } eventId&&
            observer.Personality.WaterConcernEventId==eventId)
            return new(wet.Id,eventId,CanActivelySupportWater(observer)?WaterSupportPhase.Responding:WaterSupportPhase.Calling,0);
        var target=ValidWaterAftercareTarget(observer);
        if(target==null)return null;
        return new(target.Id,observer.Personality.WaterAftercare.EventId!,
            Distance(observer.X,observer.Z,target.X,target.Z)<=WaterWatchingDistance(observer,target)+.08?
                WaterSupportPhase.Watching:WaterSupportPhase.Approaching,observer.Personality.WaterAftercare.ContactSeconds);
    }

    private bool TryWaterAftercare(Creature observer,double dt)
    {
        var care=observer.Personality.WaterAftercare;
        if(care.TargetId==null)return false;
        var target=ValidWaterAftercareTarget(observer);
        if(target==null)
        {
            // Interrupted contact earns nothing. Keep the event until its short
            // deadline so feeding or a brief tool touch can be followed by care.
            if(!care.Remembered)care.ContactSeconds=0;
            if(care.Until<=State.WorldSeconds||!State.Creatures.Any(c=>c.Id==care.TargetId&&
                c.Condition!=PhysicalCondition.Dead&&c.Interaction.Water.EventId==care.EventId))ClearWaterAftercare(observer);
            return false;
        }
        double dx=observer.X-target.X,dz=observer.Z-target.Z,distance=Math.Sqrt(dx*dx+dz*dz);
        double stop=WaterWatchingDistance(observer,target);
        if(distance>stop+.08)
        {
            if(!care.Remembered)care.ContactSeconds=0;
            RoomMove(observer,target.X+dx/Math.Max(.001,distance)*stop,target.Z+dz/Math.Max(.001,distance)*stop,dt);
            observer.Activity=$"慢慢靠近刚脱险的{target.Name}，查看它的情况";
            return true;
        }
        observer.TargetX=observer.X;observer.TargetZ=observer.Z;
        observer.Activity=$"留在{target.Name}身旁，担心地守候";
        // The target can leave water earlier in this very same world tick.
        // Its observers cannot receive a full tick of post-rescue contact yet.
        care.ContactSeconds=Math.Min(6,Math.Min(State.WorldSeconds-care.StartedAt,care.ContactSeconds+dt));
        if(care.ContactSeconds>=6&&!care.Remembered)
        {
            care.Remembered=true;
            var bond=FamilyBond(target,observer);
            bond.Affection=Clamp100(bond.Affection+2);bond.Familiarity=Clamp100(bond.Familiarity+1);
            bond.LastEvent="落水脱险后，仍在身旁守候了一会儿";
            RecordInteraction(target,"water-aftercare",$"{observer.Name}在脱险后留在近处查看自己。",1);
        }
        return true;
    }
}

public static class WaterRescueValidation
{
    public static void Validate(Creature c,WorldState state,ISet<string> ids)
    {
        static bool Id(string? id) => id is { Length:>0 and <=256 }&&!string.IsNullOrWhiteSpace(id);
        static bool Time(double t,double now) => double.IsFinite(t)&&t>=0&&t<=now+.001;
        var water=c.Interaction.Water;var p=c.Personality;var care=p.WaterAftercare;
        bool episode=water.EventId==null?water.EnteredAt==-100&&!water.PlayerCaused&&!water.HumanRescueCredited:
            Id(water.EventId)&&!state.Creatures.Any(other=>other!=c&&other.Interaction?.Water?.EventId==water.EventId)&&
            Time(water.EnteredAt,state.WorldSeconds)&&
            (!water.HumanRescueCredited||!water.InWater&&water.LastRescueAt>=water.EnteredAt);
        bool concern=p.WaterConcernEventId==null||Id(p.WaterConcernEventId)&&
            state.Creatures.Any(target=>target.Id==p.WaterConcernId&&target.Interaction.Water.EventId==p.WaterConcernEventId);
        bool followup=care!=null&&double.IsFinite(care.ContactSeconds)&&care.ContactSeconds is >=0 and <=6&&
            (care.TargetId==null?care.EventId==null&&care.StartedAt==0&&care.Until==0&&care.ContactSeconds==0&&!care.Remembered:
                care.TargetId!=c.Id&&ids.Contains(care.TargetId)&&Id(care.EventId)&&
                state.Creatures.Any(target=>target.Id==care.TargetId&&target.Interaction.Water.EventId==care.EventId)&&Time(care.StartedAt,state.WorldSeconds)&&
                double.IsFinite(care.Until)&&Math.Abs(care.Until-care.StartedAt-30)<.001&&
                care.ContactSeconds<=state.WorldSeconds-care.StartedAt+.001&&(!care.Remembered||care.ContactSeconds>=6));
        if(!episode||!concern||!followup)throw new InvalidDataException("水域事件或岸上陪护状态无效。");
    }
}

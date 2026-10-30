namespace Yukkuri.Simulation;

public enum HungerFeedingPhase { Pursuing, Preparing, Swallowing }
public sealed record HungerFeedingView(string TargetId,string EventId,HungerFeedingPhase Phase,double ContactProgress,double TakenNutrition);

public sealed class HungerFeedingState
{
    public string? TargetId { get; set; }
    public string? EventId { get; set; }
    public double StartedAt { get; set; }
    public double ContactStartedAt { get; set; } = -100;
    public double LastContactAt { get; set; } = -100;
    public double ContactSeconds { get; set; }
    public bool Committed { get; set; }
    public double TakenNutrition { get; set; }
    public double EntryAt { get; set; } = -100;
    public string? IncidentId { get; set; }
    public long IntakeSequence { get; set; }
    public string? IntakeGeneration { get; set; }
}

public sealed partial class WorldSimulation
{
    public const double HungerBiteContactSeconds=.8;

    private bool ReachableOrdinaryHungerFood(Creature c)
    {
        foreach(var source in State.RoomObjects)
        {
            if(source.Stored||source.HeldByHand||source.CarrierId!=null||source.RegionId!=c.RegionId||source.Portions<=0||
                !NaturalResources.IsFood(source.Kind)||NaturalResources.Consumer(State,source)!=null||
                Distance(c.X,c.Z,source.X,source.Z)>NaturalResources.SenseRange||!CanSeeResource(c,source))continue;
            if(c.Interaction.Resources.TryGetValue(source.Id,out var memory)&&memory.AvoidUntil>State.WorldSeconds)continue;
            if(Distance(c.X,c.Z,source.X,source.Z)<=NaturalResources.Reach(c)&&CanReachRoomFood(c,source))return true;
            if(TryResourceStandingPoint(c,source,out var point)&&HasGroundSightLine(c,point.X,point.Z))return true;
        }
        return false;
    }

    private void EnsureHungerFeeding(Creature c,Creature target)
    {
        var f=c.Personality.HungerFeeding;
        if(f.TargetId==target.Id&&f.EventId!=null&&!f.Committed)return;
        c.Personality.HungerFeeding=new(){TargetId=target.Id,EventId=NewFamilyId("feeding"),StartedAt=State.WorldSeconds};
    }

    private static void ResetHungerContact(Creature c)
    {
        var f=c.Personality.HungerFeeding;
        if(f.Committed)return;
        f.ContactSeconds=0;f.ContactStartedAt=-100;f.LastContactAt=-100;
    }

    private void SynchronizeHungerFeeding(Creature c)
    {
        var f=c.Personality.HungerFeeding;
        if(f.EventId==null)return;
        if(f.Committed)
        {
            // The portion was already taken. Finishing, losing the mouthful or
            // dying cannot return it to the victim or apply its injury again.
            if(c.Interaction.EatingRemaining<=0||c.Interaction.IntakeSequence!=f.IntakeSequence||c.Interaction.IntakeGeneration!=f.IntakeGeneration||
                c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical)
                c.Personality.HungerFeeding=new();
            return;
        }
        var target=State.Creatures.Find(o=>o.Id==f.TargetId);
        if(c.Personality.HungerTargetId!=f.TargetId||target==null||!CanPursueHungerTarget(c,target))
        {c.Personality.HungerFeeding=new();return;}
        if(!FamilyInContact(c,target))ResetHungerContact(c);
    }

    private bool AdvanceHungerContact(Creature c,Creature target,double dt)
    {
        var f=c.Personality.HungerFeeding;
        if(f.ContactStartedAt<0)
        {
            f.ContactStartedAt=State.WorldSeconds;f.LastContactAt=State.WorldSeconds;
            React(c,Expression.Angry,"饿……",$"正在近处尝试袭咬{target.Name}，现在仍可及时隔开。",2);
        }
        double elapsed=Math.Min(dt,Math.Max(0,State.WorldSeconds-f.LastContactAt));
        f.LastContactAt=State.WorldSeconds;
        f.ContactSeconds=Math.Min(HungerBiteContactSeconds,f.ContactSeconds+elapsed*MouthFunction.IntakeRate(c,IntakeKind.Meal));
        c.TargetX=c.X;c.TargetZ=c.Z;c.Activity=$"在{target.Name}近处准备袭咬";
        c.Interaction.SetReactionDirection(target.X-c.X,target.Z-c.Z);
        return f.ContactSeconds+1e-9>=HungerBiteContactSeconds;
    }

    private void CommitHungerIntake(Creature actor,Creature source,double taken,string incidentId)
    {
        var f=actor.Personality.HungerFeeding;
        if(f.Committed||f.TargetId!=source.Id||taken<=0)return;
        taken=Math.Min(taken,source.Personality.RemainsNutrition);
        if(taken<=0)return;
        source.Personality.RemainsNutrition-=taken;
        f.Committed=true;f.TakenNutrition=taken;f.EntryAt=State.WorldSeconds;f.IncidentId=incidentId;
        actor.Fullness=Clamp100(actor.Fullness+taken);
        BeginLegacyIntake(actor,recordMealExperience:false);
        f.IntakeSequence=actor.Interaction.IntakeSequence;
        f.IntakeGeneration=actor.Interaction.IntakeGeneration;
        RememberRecentIntake(actor,source.Id);
        actor.Interaction.Digestion.Exposures++;
        actor.Personality.HungerTargetId=null;
        actor.Personality.NextHungerConflictAt=Math.Max(actor.Personality.NextHungerConflictAt,State.WorldSeconds+3);
        actor.TargetX=actor.X;actor.TargetZ=actor.Z;
        actor.Activity="咽下刚刚入口的一口";
        Feedback(actor,"hunger-intake",Expression.Angry,"刚才的一口已经入口，正在慢慢咽下；造成的伤害不会撤回。","eat");
    }

    // Swallowing reports the remembered source of an accepted mouthful. It does
    // not imply that source is still in contact or that another attack is active.
    public HungerFeedingView? HungerFeedingStatus(Creature actor)
    {
        if(!State.InteractionPlaytest||!State.Creatures.Contains(actor)||actor.Family.ColdStorage.Stored||
            actor.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical)return null;
        var f=actor.Personality.HungerFeeding;
        if(f.EventId==null||f.TargetId==null)return null;
        if(f.Committed)
            return actor.Interaction.EatingRemaining>0&&actor.Interaction.EatingKind==IntakeKind.Meal&&
                actor.Interaction.IntakeSequence==f.IntakeSequence&&actor.Interaction.IntakeGeneration==f.IntakeGeneration?
                new(f.TargetId,f.EventId,HungerFeedingPhase.Swallowing,1,f.TakenNutrition):null;
        var target=State.Creatures.Find(c=>c.Id==actor.Personality.HungerTargetId&&c.Id==f.TargetId);
        if(target==null||!CanPursueHungerTarget(actor,target))return null;
        bool contact=FamilyInContact(actor,target)&&f.ContactStartedAt>=0;
        return new(f.TargetId,f.EventId,contact?HungerFeedingPhase.Preparing:HungerFeedingPhase.Pursuing,
            contact?Math.Clamp(f.ContactSeconds/HungerBiteContactSeconds,0,1):0,0);
    }
}

public static class HungerFeedingValidation
{
    public static void Validate(Creature actor,WorldState state)
    {
        static bool Id(string? id)=>id is { Length:>0 and <=256 }&&!string.IsNullOrWhiteSpace(id);
        static bool Time(double t,double now)=>double.IsFinite(t)&&t>=0&&t<=now+.001;
        var f=actor.Personality.HungerFeeding;
        bool valid=f!=null&&double.IsFinite(f.ContactSeconds)&&f.ContactSeconds is >=0 and <=WorldSimulation.HungerBiteContactSeconds&&
            double.IsFinite(f.TakenNutrition)&&f.TakenNutrition is >=0 and <=6;
        if(valid&&f!.EventId==null)
            valid=f.TargetId==null&&f.StartedAt==0&&f.ContactStartedAt==-100&&f.LastContactAt==-100&&f.ContactSeconds==0&&
                !f.Committed&&f.TakenNutrition==0&&f.EntryAt==-100&&f.IncidentId==null&&f.IntakeSequence==0&&f.IntakeGeneration==null;
        else if(valid)
            valid=Id(f!.EventId)&&f.TargetId!=actor.Id&&state.Creatures.Any(c=>c.Id==f.TargetId)&&Time(f.StartedAt,state.WorldSeconds)&&
                !state.Creatures.Any(c=>c!=actor&&c.Personality?.HungerFeeding?.EventId==f.EventId)&&
                (f.ContactStartedAt==-100?f.LastContactAt==-100&&f.ContactSeconds==0:
                    Time(f.ContactStartedAt,state.WorldSeconds)&&f.ContactStartedAt>=f.StartedAt&&Time(f.LastContactAt,state.WorldSeconds)&&
                    f.LastContactAt>=f.ContactStartedAt&&f.ContactSeconds<=f.LastContactAt-f.ContactStartedAt+.001)&&
                (f.Committed?f.TakenNutrition>0&&Time(f.EntryAt,state.WorldSeconds)&&f.EntryAt>=f.StartedAt&&Id(f.IncidentId)&&
                    f.ContactSeconds>=WorldSimulation.HungerBiteContactSeconds-.000001&&f.IntakeSequence>0&&
                    (f.IntakeGeneration==null||Guid.TryParseExact(f.IntakeGeneration,"N",out _))&&
                    (f.IntakeGeneration!=actor.Interaction.IntakeGeneration||f.IntakeSequence<=actor.Interaction.IntakeSequence):
                    f.TakenNutrition==0&&f.EntryAt==-100&&f.IncidentId==null&&f.IntakeSequence==0&&f.IntakeGeneration==null&&actor.Personality.HungerTargetId==f.TargetId);
        if(!valid)throw new InvalidDataException("饥饿袭咬或已入口来源状态无效。");
    }
}

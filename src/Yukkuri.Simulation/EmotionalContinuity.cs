namespace Yukkuri.Simulation;

// Comfort is a changing feeling, not another name for health or human trust.
public sealed class EmotionalContinuity
{
    public double Manmanlai { get; set; } = 55;
    public double Alarm { get; set; }
    public double SafeSeconds { get; set; }
    public double LastThreatAt { get; set; } = -100;
}

public sealed partial class InteractionLife
{
    public EmotionalContinuity Emotion { get; set; } = new();
}

public static class EmotionalReadiness
{
    public static bool Settled(Creature c) => c.Interaction.Emotion.Alarm < 12 &&
        c.Interaction.Water.RecoverySeconds <= 0 && c.Fear < 35 && c.Pain < 20;
    public static double FearFloor(Creature c) => c.Interaction.Emotion.Alarm * .55;
}

public sealed partial class WorldSimulation
{
    private void RecordPlayerThreat(Creature target,string description,double severity)
    {
        var incident=Emit(IncidentKind.Attack,target,"player",description,severity);
        WitnessCorrection(incident,target,severity);
        foreach(var witness in State.Creatures.Where(c=>c!=target&&c.RegionId==target.RegionId&&
            c.Condition==PhysicalCondition.Active&&!c.Family.ColdStorage.Stored))
        {
            var knowledge=witness.Knowledge.Find(k=>k.IncidentId==incident.Id);
            if(knowledge!=null)RememberThreat(witness,severity*(knowledge.KnowsActor?.8:.25));
        }
    }

    private void RememberThreat(Creature c,double severity)
    {
        if(severity<=0||c.Condition==PhysicalCondition.Dead)return;
        var e=c.Interaction.Emotion;
        e.Alarm=Clamp100(e.Alarm+severity);
        e.SafeSeconds=0;e.LastThreatAt=State.WorldSeconds;
        e.Manmanlai=Math.Max(0,e.Manmanlai-severity*.8);
    }

    private void AdvanceEmotionalContinuity(Creature c,double dt)
    {
        if(dt<=0||c.Family.ColdStorage.Stored)return;
        var e=c.Interaction.Emotion;
        bool safe=c.Pain<25&&!WaterEnvironment.IsInWater(State,c)&&
            c.Interaction.Restraint==RestraintKind.None&&!c.Interaction.Falling&&
            !FamilyRules.UnderIntervention(c)&&State.WorldSeconds-e.LastThreatAt>=15&&
            !State.Creatures.Any(other=>other!=c&&other.Condition==PhysicalCondition.Active&&FamilyVisible(c,other,4)&&
                (other.Interaction.Water.InWater||other.Personality.HungerTargetId!=null||
                 other.Family.Conflict.TargetId!=null||other.Family.ChildThreat.TargetId!=null||
                 InteractionGestureTarget==other.Id&&CurrentInteractionAction is InteractionAction.Burn or InteractionAction.BurnFeet));
        e.SafeSeconds=safe?Math.Min(600,e.SafeSeconds+dt):0;
        // A bite can attract it immediately. Recovering a feeling of safety takes time.
        if(e.SafeSeconds>=15)e.Alarm=Math.Max(0,e.Alarm-dt*(c.Fullness>20?.045:.015));
        c.Fear=Math.Max(c.Fear,EmotionalReadiness.FearFloor(c));
        if(e.SafeSeconds>=30)c.Fear=Math.Max(EmotionalReadiness.FearFloor(c),c.Fear-dt*.025);
        double target=Math.Clamp(65+c.Trust*.15+c.Attachment*.1-c.Pain*.55-c.Fear*.45-
            e.Alarm*.25-Math.Max(0,30-c.Fullness)*.8-Math.Max(0,25-c.Energy)*.5,0,100);
        e.Manmanlai+=Math.Clamp(target-e.Manmanlai,-dt*1.2,dt*.15);
    }
}

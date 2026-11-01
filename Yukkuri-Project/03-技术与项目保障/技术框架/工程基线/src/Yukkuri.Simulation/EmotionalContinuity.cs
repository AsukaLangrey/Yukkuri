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
        (c.Interaction.Emotion.Alarm <= 0 || c.Interaction.Emotion.SafeSeconds >= 15) &&
        c.Interaction.Water.RecoverySeconds <= 0 && c.Fear < 35 && c.Pain < 20;
    public static double FearFloor(Creature c) => c.Interaction.Emotion.Alarm * .55;
}

public sealed partial class WorldSimulation
{
    private void RecordPlayerThreat(Creature target,string description,double severity)
    {
        if(severity<=0)return;
        var bond=target.Interaction.HumanBond;
        var incident=State.WorldSeconds-bond.LastWitnessThreatAt<2.5?
            State.Incidents.LastOrDefault(e=>e.Kind==IncidentKind.Attack&&e.ActorId=="player"&&
                e.TargetId==target.Id&&e.At==bond.LastWitnessThreatAt&&e.RegionId==target.RegionId):null;
        if(incident==null)
        {
            incident=Emit(IncidentKind.Attack,target,"player",description,severity);
            bond.LastWitnessThreatAt=State.WorldSeconds;
        }
        else incident.Severity+=severity;
        // Match the unjustified-harm response of WitnessCorrection, applying
        // only this increment. Knowledge of an earlier moment is not current
        // sight: someone who has walked away cannot see through a wall.
        foreach(var witness in State.Creatures.Where(c=>c!=target&&c.RegionId==target.RegionId&&
            IsAvailable(c)&&!c.Family.ColdStorage.Stored))
        {
            bool sees=FamilyVisible(witness,target);
            // Only the fraction beyond the audible threshold is heard. The
            // threshold-crossing sample cannot retroactively count its quiet
            // portion, which would make hearing depend on sample size again.
            double audible=Math.Max(0,incident.Severity-4)-Math.Max(0,incident.Severity-severity-4);
            bool hears=!sees&&audible>0&&Distance(witness.X,witness.Z,target.X,target.Z)<=7;
            if(!sees&&!hears)continue;
            var previousKnowledge=witness.Knowledge.Find(k=>k.IncidentId==incident.Id);
            bool firstSight=sees&&previousKnowledge?.Source!=KnowledgeSource.Witness;
            Learn(witness,incident,sees?KnowledgeSource.Witness:KnowledgeSource.Heard,sees);
            if(sees&&witness.Knowledge.Find(k=>k.IncidentId==incident.Id) is { } knowledge)
            {
                // Seeing it oneself is stronger evidence than an earlier
                // account. This transition happens once, not once per sample.
                knowledge.Source=KnowledgeSource.Witness;knowledge.InformantId=null;
            }
            double perceived=sees?severity:audible;
            bool family=witness.Id==target.ParentId||witness.ParentId==target.Id;
            double intensity=family?1.45:1;
            witness.Fear=Clamp100(witness.Fear+perceived*(sees?.65*intensity:.18));
            if(sees)witness.Trust=Clamp100(witness.Trust-perceived*.22*intensity);
            RememberThreat(witness,perceived*(sees?.8:.25));
            var memory=witness.PersonalMemories.Find(m=>m.IncidentId==incident.Id);
            if(memory==null)
            {
                Remember(witness,incident,sees?"不知道它做错了什么，却看见它受到伤害。":
                    "听到了不安的声音，还不知道发生了什么。",perceived*(sees?1:.2));
            }
            else
            {
                memory.EmotionalWeight+=perceived*(sees?1:.2);
                if(sees)memory.Text="看见它持续受到伤害，仍然感到害怕。";
            }
            if(sees&&(memory==null||firstSight))
                React(witness,Expression.Frightened,family?"不要再伤害它了！":"为什么……？",
                    "害怕地退开，注意着主人的动作。",5);
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
        bool threatened=State.Creatures.Any(other=>other!=c&&other.RegionId==c.RegionId&&
            ValidInteractionThreatTarget(other)==c&&FamilyVisible(c,other,7));
        if(threatened)
        {
            // Awareness develops while the real warning/pursuit continues,
            // including before the first strike. No repeated dialogue/event.
            RememberThreat(c,dt*.6);
            if(c.Expression is Expression.Content or Expression.Delighted)c.Expression=Expression.Worried;
        }
        bool safe=c.Pain<25&&!WaterEnvironment.IsInWater(State,c)&&
            c.Interaction.Restraint==RestraintKind.None&&!c.Interaction.Falling&&
            !FamilyRules.UnderIntervention(c)&&State.WorldSeconds-e.LastThreatAt>=15&&
            !State.Creatures.Any(other=>other!=c&&other.Condition==PhysicalCondition.Active&&FamilyVisible(c,other,4)&&
                (WaterEnvironment.IsInWater(State,other)||ValidInteractionThreatTarget(other)!=null||
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

    // This is a comfortable listening distance, not a reach check for a tool.
    // Even a trusting creature needs space after harm. An untouched newcomer
    // has no extra distance merely because its safe-time counter starts at zero.
    public double PreferredHumanDistance(Creature c)
    {
        double sinceHarm=State.WorldSeconds-c.Interaction.HumanBond.LastHarmAt;
        double recent=c.Interaction.HumanBond.LastHarmAt>=0?Math.Max(0,1-sinceHarm/60)*.8:0;
        double desired=1.15+Math.Min(1.65,c.Interaction.Emotion.Alarm*.035+recent);
        return c.Fear>60&&c.Trust<35?Math.Max(2.2,desired):desired;
    }

    private void ApproachHumanAtDistance(Creature c,double dt,double desired)
    {
        double dx=c.X-State.PlayerX,dz=c.Z-State.PlayerZ;
        double distance=Math.Sqrt(dx*dx+dz*dz);
        if(distance<=desired+.01)return;
        // RoomMove caps a step at its destination. Aim at the perimeter so a
        // large ordinary tick cannot step inside and trigger retreat next tick.
        RoomMove(c,State.PlayerX+dx/distance*desired,State.PlayerZ+dz/distance*desired,dt);
    }

    private bool TryKeepHumanDistance(Creature c,double dt)
    {
        if(!_interactionHandLive||!SeesHelpingHand(c)||!CanSelfMove(c))return false;
        double desired=PreferredHumanDistance(c);
        if(desired<=1.15||Distance(c.X,c.Z,State.PlayerX,State.PlayerZ)>=desired-.08)return false;
        double dx=c.X-State.PlayerX,dz=c.Z-State.PlayerZ;
        if(Math.Abs(dx)+Math.Abs(dz)<.001)dx=1;
        double length=Math.Sqrt(dx*dx+dz*dz);
        RoomMove(c,State.PlayerX+dx/length*desired,State.PlayerZ+dz/length*desired,dt);
        c.Activity="仍记得刚才的不安，先留一点距离";
        return true;
    }
}

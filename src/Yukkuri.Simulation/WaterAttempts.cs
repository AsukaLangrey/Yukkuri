namespace Yukkuri.Simulation;

public enum WaterAttemptOutcome { Advanced, Blocked, Escaped }
public sealed record WaterAttemptView(string EventId,string? Generation,long Sequence,double At,WaterAttemptOutcome Outcome);

public sealed partial class WorldSimulation
{
    public const double WaterAttemptVisibleSeconds=1;

    private static void ResetWaterAttempt(WaterExposure water)
    {
        water.AttemptSequence=0;water.AttemptGeneration=null;
        water.AttemptAt=-100;water.AttemptOutcome=null;
    }

    private void RecordWaterAttempt(Creature c,WaterAttemptOutcome outcome)
    {
        var w=c.Interaction.Water;
        // Keep the immersion's causal identity. Only the attempt counter changes
        // generation at exhaustion, so it never collides with a displayed event.
        if(w.AttemptSequence==long.MaxValue)
        {w.AttemptGeneration=Guid.NewGuid().ToString("N");w.AttemptSequence=0;}
        w.AttemptSequence++;w.AttemptAt=State.WorldSeconds;w.AttemptOutcome=outcome;
    }

    // This is a recent fact, never an instruction to move or draw a new jump.
    // Consumers baseline its full identity on first observation/load/view change.
    public WaterAttemptView? WaterAttemptStatus(Creature c)
    {
        if(!State.InteractionPlaytest||State.Mode==PlayMode.CloseUp||!State.Creatures.Contains(c)||
            c.RegionId!=State.ActiveRegionId||c.Family.ColdStorage.Stored||c.Family.Memorialized||
            c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical)return null;
        var w=c.Interaction.Water;
        if(w.EventId==null||w.AttemptSequence<=0||w.AttemptOutcome is not { } outcome||
            w.AttemptAt<0||State.WorldSeconds<w.AttemptAt||State.WorldSeconds-w.AttemptAt>WaterAttemptVisibleSeconds)return null;
        if(outcome==WaterAttemptOutcome.Escaped)
        {
            if(w.InWater||WaterEnvironment.IsInWater(State,c)||w.RecoverySeconds<=0||c.Interaction.Carried||
                c.Interaction.Falling||c.Interaction.Height>.1)return null;
        }
        else if(!w.InWater||!WaterEnvironment.IsInWater(State,c)||!WaterEnvironment.CanStruggle(c)||w.Struggle<=12)return null;
        return new(w.EventId,w.AttemptGeneration,w.AttemptSequence,w.AttemptAt,outcome);
    }
}

public static class WaterAttemptValidation
{
    public static void Validate(WaterExposure water,double worldSeconds)
    {
        bool valid=water.AttemptSequence>=0;
        if(water.AttemptSequence==0)
            valid=water.AttemptGeneration==null&&water.AttemptAt==-100&&water.AttemptOutcome==null;
        else if(valid)
            valid=water.EventId!=null&&water.AttemptOutcome is { } outcome&&Enum.IsDefined(outcome)&&
                double.IsFinite(water.AttemptAt)&&water.AttemptAt>=0&&water.AttemptAt>=water.EnteredAt&&water.AttemptAt<=worldSeconds+.001&&
                (water.AttemptGeneration==null||Guid.TryParseExact(water.AttemptGeneration,"N",out _));
        if(!valid)throw new InvalidDataException("落水尝试的身份、时间或结果无效。");
    }
}

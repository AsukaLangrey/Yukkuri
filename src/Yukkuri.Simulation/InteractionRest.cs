namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    public string? RestTargetId { get; set; }
    public double RestBestDistance { get; set; } = 100;
    public double RestStuckSeconds { get; set; }
    public double RestOnGroundUntil { get; set; }
}

public sealed partial class WorldSimulation
{
    private RoomObject? NearestInteractionCushion(Creature c)=>State.RoomObjects
        .Where(o=>o.Kind==RoomObjectKind.Cushion&&!o.Stored&&o.RegionId==c.RegionId)
        .MinBy(o=>Distance(c.X,c.Z,o.X,o.Z));
    private bool HasCushionedRestSupport(Creature c)=>c.Interaction.Restraint==RestraintKind.Wrap||
        NearestInteractionCushion(c) is { } bed&&Distance(c.X,c.Z,bed.X,bed.Z)<=.65;

    private void UpdateInteractionRest(Creature c,double dt)
    {
        var i=c.Interaction;i.Resting=false;
        if(i.RestRemaining<=0)
        {
            bool recovering=i.Digestion.Symptomatic&&c.Energy<55&&c.Fullness>=35&&i.Hydration>=35;
            if(c.Energy>=22&&!recovering){i.RestTargetId=null;i.RestStuckSeconds=0;return;}
            // Finish a short rest rather than oscillating across the tired threshold.
            i.RestRemaining=recovering?45:30;
        }
        if(CanSelfMove(c)&&(c.Fullness<15||i.Hydration<15))return;
        if(i.Carried||i.Falling||i.EatingRemaining>0||i.Height>.25||c.Fear>=60||c.Pain>=65||
            i.Restraint is not (RestraintKind.None or RestraintKind.Wrap or RestraintKind.Box)||
            i.ActiveCue!=null||i.FollowPlayer||InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy||Math.Abs(i.VelocityX)+Math.Abs(i.VelocityZ)>.05)return;
        var bed=NearestInteractionCushion(c);
        if(i.RestOnGroundUntil<=State.WorldSeconds&&i.Restraint==RestraintKind.None&&CanSelfMove(c)&&bed!=null&&Distance(c.X,c.Z,bed.X,bed.Z)>.65)return;
        // Bare ground can be rested on, but only a nearby cushion or soft wrapping
        // provides the additional support used by the foot recovery model.
        i.Resting=true;i.RestRemaining=Math.Max(0,i.RestRemaining-dt);
        c.Energy=Clamp100(c.Energy+dt*(HasCushionedRestSupport(c)?.7:.4));
        if(c.ReactionRemaining<=0&&i.EatingRemaining<=0)c.Expression=Expression.Sleeping;
    }

    private void ApproachInteractionRest(Creature c,double dt)
    {
        var i=c.Interaction;var bed=NearestInteractionCushion(c);
        if(i.RestOnGroundUntil>State.WorldSeconds||bed==null||Distance(c.X,c.Z,bed.X,bed.Z)<=.65||!CanSelfMove(c))
        {c.Activity=i.Resting?"慢慢休息":"想休息，但还没有放松下来";return;}
        if(i.RestTargetId!=bed.Id)
        {i.RestTargetId=bed.Id;i.RestBestDistance=Distance(c.X,c.Z,bed.X,bed.Z);i.RestStuckSeconds=0;}
        RoomMove(c,bed.X,bed.Z,dt);c.Activity="回到软垫旁";
        double remaining=Distance(c.X,c.Z,bed.X,bed.Z);
        if(remaining<i.RestBestDistance-.04){i.RestBestDistance=remaining;i.RestStuckSeconds=0;}
        else i.RestStuckSeconds+=dt;
        if(i.RestStuckSeconds<3)return;
        // An occupied or inaccessible cushion must not trap a tired creature forever.
        // Bare ground supports ordinary rest, without the cushion's healing bonus.
        i.RestOnGroundUntil=State.WorldSeconds+Math.Max(30,i.RestRemaining+1);
        i.RestTargetId=null;i.RestStuckSeconds=0;c.TargetX=c.X;c.TargetZ=c.Z;
        c.Activity="垫子旁过不去，先在这里歇一会儿";
    }
}

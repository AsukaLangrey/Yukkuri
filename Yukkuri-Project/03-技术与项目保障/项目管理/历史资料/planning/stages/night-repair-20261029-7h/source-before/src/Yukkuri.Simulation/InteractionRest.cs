namespace Yukkuri.Simulation;

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
        if(i.RestRemaining<=0&&c.Energy>=22)return;
        if(i.Carried||i.Falling||i.Height>.25||c.Fear>=60||c.Pain>=65||
            i.Restraint is not (RestraintKind.None or RestraintKind.Wrap or RestraintKind.Box)||
            i.ActiveCue!=null||i.FollowPlayer||InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy||Math.Abs(i.VelocityX)+Math.Abs(i.VelocityZ)>.05)return;
        var bed=NearestInteractionCushion(c);
        if(i.Restraint==RestraintKind.None&&CanSelfMove(c)&&bed!=null&&Distance(c.X,c.Z,bed.X,bed.Z)>.65)return;
        // Bare ground can be rested on, but only a nearby cushion or soft wrapping
        // provides the additional support used by the foot recovery model.
        i.Resting=true;i.RestRemaining=Math.Max(0,i.RestRemaining-dt);
        c.Energy=Clamp100(c.Energy+dt*(HasCushionedRestSupport(c)?.7:.4));
        if(c.ReactionRemaining<=0&&i.EatingRemaining<=0)c.Expression=Expression.Sleeping;
    }
}

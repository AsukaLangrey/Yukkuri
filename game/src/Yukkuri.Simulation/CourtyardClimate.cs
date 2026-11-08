namespace Yukkuri.Simulation;

// Derived from the saved world clock; querying weather never advances time or consumes RNG.
public static class CourtyardClimate
{
    public const double ComfortableTemperature=18,SeekWarmth=45,StopSeeking=70;
    public static double Temperature(double seconds)
    {
        var date=CourtyardCalendar.At(seconds);
        double day=date.DayOfYear-1+date.Hour/24;
        double seasonal=17+12*Math.Cos(Math.Tau*(day-10.5)/CourtyardCalendar.DaysPerYear);
        return seasonal+5*Math.Cos(Math.Tau*(date.Hour-15)/24);
    }
}
public partial class FamilyLife
{
    public bool SeekingWarmth {get;set;}
}
public sealed partial class WorldSimulation
{
    private bool WarmCompanion(Creature c,Creature other)=>c.Id!=other.Id&&c.RegionId==other.RegionId&&
        other.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering&&!other.Family.ColdStorage.Stored&&
        !other.Family.Memorialized&&!other.Interaction.Carried&&!other.Interaction.Falling&&other.Interaction.Height<.1&&
        !other.IsIntruder&&other.Family.Conflict.TargetId!=c.Id&&c.Family.Conflict.TargetId!=other.Id;
    public double EffectiveTemperature(Creature c)
    {
        double ambient=CourtyardClimate.Temperature(State.WorldSeconds);
        bool home=State.Shelters.Any(s=>s.RegionId==c.RegionId&&ShelterFullyInside(c,s));
        bool huddle=State.Creatures.Any(o=>WarmCompanion(c,o)&&Distance(c.X,c.Z,o.X,o.Z)<=InteractionFootprint(c)+InteractionFootprint(o)+.18);
        return ambient+(home?18:0)+(c.Interaction.Restraint==RestraintKind.Wrap?12:0)+(HasCushionedRestSupport(c)?4:0)+(huddle?8:0);
    }
    private void TickWarmth(Creature c,double dt)
    {
        if(c.Family.ColdStorage.Stored||c.Family.Memorialized||c.Condition==PhysicalCondition.Dead)return;
        double deficit=CourtyardClimate.ComfortableTemperature-EffectiveTemperature(c);
        double change=deficit>0?-.012*deficit*(c.IsYoung?1.35:1):.10+Math.Min(.1,-deficit*.008);
        c.Family.Warmth=Math.Clamp(c.Family.Warmth+dt*change,0,100);
        if(c.Family.Warmth<CourtyardClimate.SeekWarmth)c.Family.SeekingWarmth=true;
        else if(c.Family.Warmth>=CourtyardClimate.StopSeeking)c.Family.SeekingWarmth=false;
        if(c.Family.Warmth<12)Hurt(c,dt*.012*(c.IsYoung?1.5:1),"持续失温",null);
        if(c.Family.Warmth<30&&c.ReactionRemaining<=0)React(c,Expression.Calling,"冷冷……挤一挤……","身体发冷，想靠近同伴或进屋。",5);
    }
    private bool TrySeekWarmth(Creature c,double dt)
    {
        if(!c.Family.SeekingWarmth||!CanSelfMove(c)||NightUrgent(c))return false;
        if(TrySeekSharedShelter(c,dt))return true;
        var friend=State.Creatures.Where(o=>WarmCompanion(c,o)&&!State.Shelters.Any(s=>s.RegionId==o.RegionId&&ShelterFullyInside(o,s)))
            .OrderBy(o=>Distance(c.X,c.Z,o.X,o.Z)).FirstOrDefault();
        if(friend==null)return false;
        double distance=Distance(c.X,c.Z,friend.X,friend.Z);
        double gap=InteractionFootprint(c)+InteractionFootprint(friend)+.07;
        if(distance>gap+.04)
        {RoomMove(c,friend.X,friend.Z,dt);c.Activity="身体发冷，靠近同伴取暖";}
        else {c.TargetX=c.X;c.TargetZ=c.Z;c.Activity="挨着同伴取暖";}
        return true;
    }
}

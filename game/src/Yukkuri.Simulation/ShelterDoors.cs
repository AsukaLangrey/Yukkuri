namespace Yukkuri.Simulation;
public sealed partial class WorldSimulation
{
    private static double ShelterTransitHeight(Creature c,ShelterState s)=>ShelterBodyEnvelope.Height(c);
    // Derived from the real visit and position: loading, cancellation, carrying,
    // death and leaving cannot leave a permanent collision-disable flag behind.
    public bool IsShelterCollisionSuppressed(Creature c)
    {
        if(c.Condition==PhysicalCondition.Dead||c.Family.ColdStorage.Stored||c.Family.Memorialized||
            c.Interaction.Carried||c.Interaction.Falling||c.Interaction.Restraint!=RestraintKind.None)return false;
        return State.Shelters.Any(s=>s.RegionId==c.RegionId&&
            (s.Capacity==5?s.Residents.Any(v=>v.CreatureId==c.Id&&
                (!v.ExitRequested&&v.Phase is ShelterVisitPhase.Approaching or ShelterVisitPhase.Entering||ShelterBodyPresent(c,s))):
                s.GuestId==c.Id&&(!s.ExitRequested&&s.Phase is ShelterVisitPhase.Approaching or ShelterVisitPhase.Entering||ShelterBodyPresent(c,s))));
    }
    public bool IsShelteredHidden(Creature c)=>State.Shelters.Any(s=>s.RegionId==c.RegionId&&ShelterFullyInside(c,s)&&
        (s.Capacity==5?s.Residents.Any(v=>v.CreatureId==c.Id&&(v.Phase==ShelterVisitPhase.Inside||v.VisualHidden)):
        s.GuestId==c.Id&&s.Phase==ShelterVisitPhase.Inside));
    public string RequestShelterResidentExit(string id)
    {
        var c=State.Creatures.Find(c=>c.Id==id);if(c==null||FamilyCommandError(c) is { })return "当前无法邀请它出屋。";
        if(SharedHome(c) is not { } home)return "它当前没有入住共享小屋。";
        var visit=home.Residents.Single(v=>v.CreatureId==id);visit.ExitRequested=true;home.DoorOpen=true;
        return "它准备出屋，等门帘完全打开，再沿门口走出。";
    }
    private void TickShelterDoors(double dt)
    {
        foreach(var c in State.Creatures)
        {
            double target=SharedHome(c)!=null?1:0;
            c.Interaction.StemFold+=Math.Clamp(target-c.Interaction.StemFold,-dt/.65,dt/.65);
        }
        foreach(var s in State.Shelters)
        {
            if(!s.DoorAnimationInitialized){s.DoorProgress=s.DoorOpen?1:0;s.DoorAnimationInitialized=true;}
            // The original one-resident hideaway has a manually operated door.
            // Only the shared sleeping hut opens/closes automatically for traffic.
            if(s.Capacity==1)
            {
                if(!ShelterDoorClear(s))s.DoorOpen=true;
                s.DoorProgress=Math.Clamp(s.DoorProgress+(s.DoorOpen?1:-1)*dt/.65,0,1);
                continue;
            }
            bool traffic=s.Capacity==5?s.Residents.Any(v=>v.Phase is ShelterVisitPhase.Entering or ShelterVisitPhase.Exiting||v.ExitRequested||
                v.Phase==ShelterVisitPhase.Approaching&&State.Creatures.Find(body=>body.Id==v.CreatureId) is { } entrant&&
                Distance(entrant.X,entrant.Z,s.X,s.Z+ShelterGeometry.ApproachDepth(ShelterGeometry.Scale(s)))<2):
                s.GuestId!=null&&s.Phase!=ShelterVisitPhase.Inside||s.ExitRequested;
            s.DoorHoldRemaining=Math.Max(0,s.DoorHoldRemaining-dt);
            if(traffic){s.DoorOpen=true;s.DoorIdleSeconds=0;}
            else s.DoorIdleSeconds=Math.Min(2,s.DoorIdleSeconds+dt);
            bool clear=ShelterDoorClear(s);
            if(!clear){s.DoorOpen=true;s.DoorIdleSeconds=0;}
            else if(!traffic&&s.DoorIdleSeconds>=.8&&s.DoorHoldRemaining<=0)s.DoorOpen=false;
            s.DoorProgress=Math.Clamp(s.DoorProgress+(s.DoorOpen?1:-1)*dt/.65,0,1);
        }
    }
}

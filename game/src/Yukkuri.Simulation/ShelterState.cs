namespace Yukkuri.Simulation;

public enum ShelterVisitPhase { Empty, Approaching, Entering, Inside, Exiting }
public sealed class ShelterState
{
    // Missing in old saves means the original single-resident hideaway.
    public int Capacity { get; set; }=1;
    public List<ShelterResident> Residents { get; set; }=[];
    public string Id { get; set; }="";
    public string RegionId { get; set; }="home";
    public double X { get; set; }
    public double Z { get; set; }
    public bool DoorOpen { get; set; }=true;
    public double DoorProgress { get; set; }=1;
    public bool DoorAnimationInitialized { get; set; }
    public double DoorIdleSeconds { get; set; }
    public double DoorHoldRemaining { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool DoorPassable=>DoorProgress>=.999999;
    public string? GuestId { get; set; }
    public ShelterVisitPhase Phase { get; set; }
    public bool ExitRequested { get; set; }
    public double BlockedSeconds { get; set; }
    public double LastHelpAt { get; set; }=-100;
    public string LastEvent { get; set; }="空屋，入口朝向小院前方。";
}
public sealed class ShelterResident
{
    public string CreatureId { get; set; }="";
    public bool VisualHidden { get; set; }
    public int Slot { get; set; }
    public ShelterVisitPhase Phase { get; set; }=ShelterVisitPhase.Approaching;
    public bool ExitRequested { get; set; }
    public double BlockedSeconds { get; set; }
}
public sealed partial class InteractionLife
{
    public double StemFold { get; set; }
    public double ShelterSearchAfter { get; set; }
    public double NightExposureSeconds { get; set; }
    public double NightHousingDiscomfort { get; set; }
}
public sealed partial class WorldState
{
    public List<ShelterState> Shelters { get; set; }=[];
    public int ShelterStock { get; set; }
    public long ShelterSequence { get; set; }
}
public sealed record ShelterView(string Id,string RegionId,double X,double Z,bool DoorOpen,ShelterVisitPhase Phase,
    string? ReservedById,string? OccupantId,bool ExitRequested,double BlockedSeconds,bool CanOpen,bool CanClose,
    bool CanCall,string? RefusalReason,string LastEvent,string? CallRefusalReason)
{
    public double DoorProgress { get; init; }=1;
    public int Capacity { get; init; }=1;
    public string[] OccupantIds { get; init; }=[];
    public string[] IncomingIds { get; init; }=[];
    public string Comfort { get; init; }="空屋";
}
public sealed record CreatureShelterView(string ShelterId,ShelterVisitPhase Phase,bool ExitRequested,bool Blocked,string? RefusalReason);

namespace Yukkuri.Simulation;

public enum ShelterVisitPhase { Empty, Approaching, Entering, Inside, Exiting }
public sealed class ShelterState
{
    public string Id { get; set; }="";
    public string RegionId { get; set; }="home";
    public double X { get; set; }
    public double Z { get; set; }
    public bool DoorOpen { get; set; }=true;
    public string? GuestId { get; set; }
    public ShelterVisitPhase Phase { get; set; }
    public bool ExitRequested { get; set; }
    public double BlockedSeconds { get; set; }
    public double LastHelpAt { get; set; }=-100;
    public string LastEvent { get; set; }="空屋，入口朝向小院前方。";
}
public sealed partial class InteractionLife
{
    public double ShelterSearchAfter { get; set; }
}
public sealed partial class WorldState
{
    public List<ShelterState> Shelters { get; set; }=[];
    public int ShelterStock { get; set; }
    public long ShelterSequence { get; set; }
}
public sealed record ShelterView(string Id,string RegionId,double X,double Z,bool DoorOpen,ShelterVisitPhase Phase,
    string? ReservedById,string? OccupantId,bool ExitRequested,double BlockedSeconds,bool CanOpen,bool CanClose,
    bool CanCall,string? RefusalReason,string LastEvent,string? CallRefusalReason);
public sealed record CreatureShelterView(string ShelterId,ShelterVisitPhase Phase,bool ExitRequested,bool Blocked,string? RefusalReason);

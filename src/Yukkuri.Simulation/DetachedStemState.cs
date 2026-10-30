namespace Yukkuri.Simulation;

public enum DetachedStemPhase { Loose, Bottled, Failed, Born }
public sealed class StemParentSource
{
    public string Id { get; set; }="";
    public string BodyId { get; set; }="";
    public string Name { get; set; }="";
    public string SpeciesId { get; set; }="";
    public string Disposition { get; set; }="";
    public double Courage { get; set; }
    public double Patience { get; set; }
    public double Aggression { get; set; }
    public static StemParentSource Capture(Creature c)=>new(){Id=c.Id,BodyId=c.Body.Id,Name=c.Name,
        SpeciesId=c.Body.SpeciesId,Disposition=c.TrueDisposition,Courage=c.Courage,Patience=c.Patience,Aggression=c.Aggression};
}
public sealed class DetachedStemBrood
{
    public string EventId { get; set; }="";
    public StemParentSource Mother { get; set; }=new();
    public StemParentSource Father { get; set; }=new();
    public string RegionId { get; set; }="home";
    public double X { get; set; }
    public double Z { get; set; }
    public DetachedStemPhase Phase { get; set; }
    public int PlannedChildren { get; set; }
    public double Elapsed { get; set; }
    public double Burden { get; set; }
    public double Integrity { get; set; }=100;
    public double Hydration { get; set; }=100;
    public double Nutrition { get; set; }
    public double VesselIntegrity { get; set; }
    public double DetachedAt { get; set; }
    public double LastDamageAt { get; set; }=-100;
    public string LastDamageCause { get; set; }="";
    public string? LastDamageActorId { get; set; }
    public string LastEvent { get; set; }="";
    public bool Cleared { get; set; }
    public string? ThreatIncidentId { get; set; }
    public double ThreatIncidentAt { get; set; }=-100;
    // Every real drag step updates the persistent, occupied ground position.
    // Loading releases the transient hand here, without teleporting or rewinding.
    [System.Text.Json.Serialization.JsonIgnore] public bool HeldByHand { get; set; }
}
public sealed partial class WorldState
{
    public List<DetachedStemBrood> DetachedStemBroods { get; set; }=[];
    public int StemVessels { get; set; }
    public int StemVesselPurchases { get; set; }
}
public sealed record DetachedStemView(string EventId,string MotherId,string FatherId,string RegionId,double X,double Z,
    DetachedStemPhase Phase,int PlannedChildren,double Progress,double Integrity,double Hydration,double Nutrition,
    double VesselIntegrity,bool HeldByHand,bool CanBottle,bool CanWater,bool CanFeed,string? RefusalReason,string LastDamageCause,
    bool Cleared,bool CanClear);

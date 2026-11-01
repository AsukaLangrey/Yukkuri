namespace Yukkuri.Simulation;

public enum SupplyStationKind { Food, Water }
public sealed class SupplyStationState
{
    public string Id { get; set; }="";
    public SupplyStationKind Kind { get; set; }
    public string RegionId { get; set; }="home";
    public double X { get; set; }
    public double Z { get; set; }
    public int StoredPortions { get; set; }
    public string? OutletObjectId { get; set; }
    public long OutputSequence { get; set; }
    public int RefillIntervalSeconds { get; set; } = 30;
    public double NextRefillAt { get; set; }
    public string LastEvent { get; set; }="空仓，先补充实际食水。";
    // The last legal ground position remains occupied while dragging.
    [System.Text.Json.Serialization.JsonIgnore] public bool HeldByHand { get; set; }
}
public sealed partial class WorldState
{
    public List<SupplyStationState> SupplyStations { get; set; }=[];
    public long SupplyStationSequence { get; set; }
    public int FoodStationStock { get; set; }
    public int WaterStationStock { get; set; }
    public int StationWaterPortions { get; set; }
}
public sealed record SupplyStationView(string Id,SupplyStationKind Kind,string RegionId,double X,double Z,
    int Capacity,int StoredPortions,int OutletPortions,string? OutletObjectId,double OutletX,double OutletZ,
    bool HeldByHand,bool CanRefill,bool CanMove,string? RefusalReason,string LastEvent);

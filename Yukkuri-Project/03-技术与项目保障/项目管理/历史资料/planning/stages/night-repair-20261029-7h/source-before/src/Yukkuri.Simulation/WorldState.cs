namespace Yukkuri.Simulation;

public enum PlayMode { Management, Human, CloseUp }
public enum RegionKind { Sanctuary, Wilderness, PublicPark, Residential }
public enum FacilityKind { Feeder, Bed }
public enum ReproductionRole { Paternal, Maternal }

public sealed class BodyState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SpeciesId { get; set; } = "prototype-common";
    public double AgeSeconds { get; set; }
    public ReproductionRole ReproductionRole { get; set; }
    public string? UpgradeRoute { get; set; }
}
public sealed class Registration
{
    public string IdentityId { get; set; } = Guid.NewGuid().ToString("N");
    public string Grade { get; set; } = "未评级";
    public bool IndependentStatus { get; set; }
    public bool NeedsReassessment { get; set; }
}
public sealed class ObservationRecord
{
    public string Hypothesis { get; set; } = "尚无性格判断";
    public int Samples { get; set; }
    public int FeedingEvidence { get; set; }
    public double Confidence { get; set; }
    public List<string> Notes { get; set; } = [];
}
public sealed partial class Creature
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "未命名";
    public BodyState Body { get; set; } = new();
    public Registration Registration { get; set; } = new();
    public double InnerAgeSeconds { get; set; }
    public string RegionId { get; set; } = "home";
    public bool Quarantine { get; set; }
    public double X { get; set; }
    public double Z { get; set; }
    public double TargetX { get; set; }
    public double TargetZ { get; set; }
    public double Fullness { get; set; } = 70;
    public double Energy { get; set; } = 80;
    public double Health { get; set; } = 100;
    public double Trust { get; set; } = 20;
    public string TrueDisposition { get; set; } = "curious";
    public string Activity { get; set; } = "闲逛";
    public double DecisionIn { get; set; }
    public Dictionary<string, double> Relationships { get; set; } = [];
    public List<string> Memories { get; set; } = [];
    public ObservationRecord Observation { get; set; } = new();
}
public sealed class Facility
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public FacilityKind Kind { get; set; }
    public double X { get; set; }
    public double Z { get; set; }
    public int Stock { get; set; }
}
public sealed class RegionState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public RegionKind Kind { get; set; }
    public List<Facility> Facilities { get; set; } = [];
    public bool StaffEnabled { get; set; }
    public double StaffCooldown { get; set; }
}
public sealed partial class WorldState
{
    public const int CurrentVersion = 1;
    public int SchemaVersion { get; set; } = CurrentVersion;
    public ulong RandomState { get; set; } = 92137;
    public double WorldSeconds { get; set; }
    public double TickRemainder { get; set; }
    public PlayMode Mode { get; set; }
    public string ActiveRegionId { get; set; } = "home";
    public string? SelectedId { get; set; }
    public double PlayerX { get; set; } = 0;
    public double PlayerZ { get; set; } = 4;
    public int Money { get; set; } = 240;
    public int Food { get; set; } = 40;
    public double ObservationSkill { get; set; } = 1;
    public double WageTimer { get; set; }
    public bool UnlimitedResources { get; set; }
    public List<RegionState> Regions { get; set; } = [];
    public List<Creature> Creatures { get; set; } = [];
    public List<string> Journal { get; set; } = [];
    public HashSet<string> CompletedCareRecords { get; set; } = [];
}
public sealed record CreatureView(string Id, string Name, string BodyId, string RegionId,
    double Fullness, double Energy, double Health, string Activity, string Observation, double Confidence, bool Quarantine);

// Prototype tuning only. Species upgrades and reproduction are intentionally unconfigured.
public sealed class SimulationConfig
{
    public double TickSeconds { get; set; } = 0.25;
    public double HungerPerSecond { get; set; } = 0.22;
    public double EnergyPerSecond { get; set; } = 0.12;
    public double MoveSpeed { get; set; } = 0.8;
    public double FeedGain { get; set; } = 30;
    public int FeederCost { get; set; } = 25;
    public int BedCost { get; set; } = 20;
    public double RegionHalfSize { get; set; } = 9;
    public static SimulationConfig Parse(string json)
    {
        var result = System.Text.Json.JsonSerializer.Deserialize<SimulationConfig>(json)
            ?? throw new InvalidDataException("模拟配置为空");
        if (!double.IsFinite(result.TickSeconds) || result.TickSeconds is < 0.05 or > 1 ||
            !double.IsFinite(result.HungerPerSecond) || result.HungerPerSecond is < 0 or > 10 ||
            !double.IsFinite(result.EnergyPerSecond) || result.EnergyPerSecond is < 0 or > 10 ||
            !double.IsFinite(result.MoveSpeed) || result.MoveSpeed is <= 0 or > 10 ||
            !double.IsFinite(result.FeedGain) || result.FeedGain is <= 0 or > 100 ||
            result.FeederCost < 0 || result.BedCost < 0 ||
            !double.IsFinite(result.RegionHalfSize) || result.RegionHalfSize is < 5 or > 50)
            throw new InvalidDataException("模拟配置数值越界");
        return result;
    }
}

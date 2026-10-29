namespace Yukkuri.Simulation;

public enum BirthMethod { Stem, Live }
public enum PregnancyPhase { None, Preparing, Gestating, Recovering }
public sealed partial class PeerBond
{
    public double Familiarity { get; set; }
    public double Affection { get; set; }
    public double Fear { get; set; }
    public double Resentment { get; set; }
    public double CareSeconds { get; set; }
    public string LastEvent { get; set; } = "";
}
public sealed partial class PregnancyState
{
    public PregnancyPhase Phase { get; set; }
    public BirthMethod Method { get; set; }
    public string? FatherId { get; set; }
    public string? EventId { get; set; }
    public double Elapsed { get; set; }
    public double WaitingSeconds { get; set; }
    public double Burden { get; set; }
    public double RecoveryRemaining { get; set; }
    public double RetryAt { get; set; }
    public int PlannedChildren { get; set; }
    public string Outcome { get; set; } = "";
}
public sealed partial class FamilyLife
{
    public Dictionary<string, PeerBond> Peers { get; set; } = new();
    public string? MotherId { get; set; }
    public string? FatherId { get; set; }
    public HashSet<string> PartnerIds { get; set; } = new();
    public string? CompanionTargetId { get; set; }
    public List<string> CaregiverIds { get; set; } = new();
    public PregnancyState Pregnancy { get; set; } = new();
    public string? BirthEventId { get; set; }
    public string? CareTargetId { get; set; }
    public string? CareParcelId { get; set; }
    public double CareApproachSeconds { get; set; }
    public double CareBestDistance { get; set; } = 100;
    public Dictionary<string, bool> LifeKnowledge { get; set; } = new();
    public ColdStorageState ColdStorage { get; set; } = new();
    public BirthMethod BirthMethod { get; set; }
    public double Warmth { get; set; } = 80;
    public double NextCareAt { get; set; }
    public double NextSocialAt { get; set; }
    public double NextBreedingDecisionAt { get; set; } = 120;
    public bool AllowsBreeding { get; set; } = true;
    public string LastEvent { get; set; } = "";
}
public sealed partial class Creature
{
    public FamilyLife Family { get; set; } = new();
}
public sealed partial class WorldState
{
    public long FamilySequence { get; set; }
    public HashSet<string> CompletedBirths { get; set; } = new();
    public bool AutonomousBreeding { get; set; }
}

// Prototype tuning, not historical Hakkei formulas or final balance.
public static partial class FamilyRules
{
    public const double YoungSeconds = 1800;
    public static double GestationSeconds(BirthMethod method) => method == BirthMethod.Live ? 480 : 240;
    public static double RecoverySeconds(BirthMethod method) => method == BirthMethod.Live ? 300 : 180;
    public static bool CommonSpecies(string species) => species is "reimu" or "marisa" or "alice" or "patchouli";
    public static bool WillSocialize(Creature c) => !UnderIntervention(c)&&c.Family.ChildThreat.TargetId==null&&
        !c.Family.ColdStorage.Stored && c.Family.ColdStorage.RecoveryRemaining<=0 && c.Condition == PhysicalCondition.Active && c.Pain < 25 &&
        c.Fear < 55 && c.Fullness > 25 && c.Interaction.Hydration > 25 && c.Energy > 25 &&
        !c.Interaction.Digestion.Symptomatic && c.Interaction.Restraint == RestraintKind.None &&
        !c.Interaction.Carried && !c.Interaction.Falling && !c.Interaction.Supine &&
        !c.Interaction.Physiology.Active && c.Interaction.EatingRemaining <= 0 && c.Interaction.ActiveCue == null;
    public static bool Predator(string species) => species is "remilia" or "flandre";
    public static bool SupportedSpecies(string species) => CommonSpecies(species)||Predator(species);
    public static bool CrossEcology(Creature a,Creature b) => Predator(a.Body.SpeciesId)!=Predator(b.Body.SpeciesId);
    public static bool WillPair(Creature c, Creature other) => WillSocialize(c) && !c.IsYoung &&
        c.Family.Peers.TryGetValue(other.Id, out var bond) &&
        bond.Familiarity >= (CrossEcology(c,other)?75:30) && bond.Affection >= (CrossEcology(c,other)?80:35) && bond.Fear < 25 && bond.Resentment < 25;
    public static string Describe(Creature c)
    {
        var p=c.Family.Pregnancy;
        if(c.Family.ColdStorage.Stored)return "冷藏保存中 · 生命过程按十倍延缓；需要及时取出";
        string stage=p.Phase switch
        {
            PregnancyPhase.Preparing => UnderIntervention(c)?"繁育干预进行中，可及时停止":"正在等待双方靠近与接受",
            PregnancyPhase.Gestating => p.Method==BirthMethod.Stem?"茎生孕育中，需要安稳的照护":"胎生孕育中，需要更多吃喝与休息",
            PregnancyPhase.Recovering => "孕育后的恢复期",
            _ => ""
        };
        if(c.IsYoung)stage="幼体，需要吃喝、保暖与陪伴"+(c.Family.Warmth<35?"；正在发冷":"");
        return string.Join("；",new[]{stage,p.Burden>=40?"身体负担加重，需要及时照护":"",c.Family.ColdStorage.RecoveryRemaining>0?"取出冷藏后正在缓慢恢复":"",c.Family.LastEvent}.Where(x=>x.Length>0));
    }
}

using System.Text.Json.Serialization;
namespace Yukkuri.Simulation;

public enum GrowthStage { Infant, Child, Youth, Adult }
public sealed partial class Creature
{
    public int GrowthVersion { get; set; }
    // Persistent constitution, independent of current health and temperament.
    public double Robustness { get; set; } = .5;
    [JsonIgnore] public GrowthStage GrowthStage => GrowthRules.Stage(this);
}
public sealed partial class InteractionLife
{
    public double Sleepiness { get; set; } = 20;
}
public sealed partial class WorldState
{
    public int CourtyardLifeVersion { get; set; }
}
public static class GrowthRules
{
    public const double Day=600, InfantEnd=7*Day, ChildEnd=21*Day, AdultAge=35*Day;
    public static GrowthStage Stage(Creature c) => c.Body.AgeSeconds<InfantEnd?GrowthStage.Infant:
        c.Body.AgeSeconds<ChildEnd?GrowthStage.Child:c.Body.AgeSeconds<AdultAge?GrowthStage.Youth:GrowthStage.Adult;
    public static bool IsAdult(Creature c)=>Stage(c)==GrowthStage.Adult;
    public static string Name(Creature c)=>Stage(c) switch {GrowthStage.Infant=>"幼油",GrowthStage.Child=>"子油",GrowthStage.Youth=>"若油",_=>"成油"};
    public static double Scale(Creature c)
    {
        double age=c.Body.AgeSeconds;
        return age<InfantEnd?.73+.07*Math.Clamp(age/InfantEnd,0,1):
            age<ChildEnd?.8+.1*(age-InfantEnd)/(ChildEnd-InfantEnd):
            age<AdultAge?.9+.1*(age-ChildEnd)/(AdultAge-ChildEnd):1;
    }
    public static double NeedsFactor(Creature c)=>.45+.55*Math.Clamp(c.Body.AgeSeconds/AdultAge,0,1);
    public static void Initialize(Creature c,bool young)
    {
        c.GrowthVersion=1;c.IsElder=false;c.IsYoung=young;
        c.Body.AgeSeconds=young?0:AdultAge;c.InnerAgeSeconds=c.Body.AgeSeconds;
    }
    public static void Migrate(Creature c)
    {
        if(!double.IsFinite(c.Body.AgeSeconds)||!double.IsFinite(c.InnerAgeSeconds)||c.Body.AgeSeconds<0||c.InnerAgeSeconds<0)throw new InvalidDataException("旧档年龄数据无效");
        if(c.GrowthVersion!=0)return;
        // Legacy adults never become minors. Legacy infant progress maps to its first seven days.
        c.Body.AgeSeconds=c.IsYoung?Math.Clamp(c.Body.AgeSeconds/1800,0,1)*InfantEnd:Math.Max(AdultAge,c.Body.AgeSeconds);
        c.InnerAgeSeconds=Math.Max(c.InnerAgeSeconds,c.Body.AgeSeconds);
        c.IsYoung=c.Body.AgeSeconds<AdultAge;c.IsElder=false;c.GrowthVersion=1;
    }
    public static void MigrateWorld(WorldState s)
    {
        if(s.Creatures==null||s.CreatureSales==null||s.DetachedStemBroods==null||s.RoomObjects==null||s.Shelters==null)throw new InvalidDataException("旧档迁移所需列表缺失");
        if(s.CreatureSales.Any(sale=>sale==null)||s.DetachedStemBroods.Any(stem=>stem==null)||s.RoomObjects.Any(obj=>obj==null))throw new InvalidDataException("旧档列表含无效记录");
        foreach(var c in s.KnownCreatures)
        {
            if(c==null||c.Body==null||c.Family==null||c.Family.Pregnancy==null||c.Interaction==null||c.Interaction.Development==null)throw new InvalidDataException("旧档个体数据缺失");
            Migrate(c);var d=c.Interaction.Development;
            if(!Enum.IsDefined(d.Duty))throw new InvalidDataException("旧档分工数据无效");
            if(d.Duty==LifeDuty.Care&&c.Family.CareTargetId==d.RecipientId)
            {
                if(c.Family.CareParcelId is { } parcel&&s.RoomObjects.Find(o=>o.Id==parcel) is { } supply){supply.CarrierId=null;supply.Height=0;}
                c.Family.CareTargetId=c.Family.CareParcelId=null;c.Family.CareApproachSeconds=0;c.Family.CareBestDistance=100;
            }
            if(d.Duty==LifeDuty.Cleaning){c.Interaction.CleaningTargetId=null;c.Interaction.CleaningSeconds=0;}
            d.Duty=LifeDuty.None;d.RecipientId=null;
        }
        foreach(var home in s.Shelters)
        {
            if(home==null)throw new InvalidDataException("旧档小屋记录缺失");
            if(!home.DoorAnimationInitialized){home.DoorProgress=home.DoorOpen?1:0;home.DoorAnimationInitialized=true;}
        }
        if(s.CourtyardLifeVersion!=0)return;
        foreach(var c in s.KnownCreatures)
        {
            var pregnancy=c.Family.Pregnancy;
            if(pregnancy.Phase==PregnancyPhase.Gestating)
                pregnancy.Elapsed=Math.Clamp(pregnancy.Elapsed/(pregnancy.Method==BirthMethod.Live?480:240),0,1)*FamilyRules.GestationSeconds(pregnancy.Method);
        }
        foreach(var stem in s.DetachedStemBroods)stem.Elapsed=Math.Clamp(stem.Elapsed/240,0,1)*FamilyRules.GestationSeconds(BirthMethod.Stem);
        foreach(var river in s.RoomObjects.Where(o=>o.Kind==RoomObjectKind.RiverWater))
        {river.X=WoodlandLayout.DrinkX;river.Z=WoodlandLayout.BankZ;}
        s.CourtyardLifeVersion=1;
    }
}
// First tuning pass. These are hypotheses for playtesting, not a validated balance.
public static class CourtyardSurvival
{
    public const double HungerPerSecond=.1, HydrationPerSecond=.1, ManualWaterGain=60;
    public const double MealStart=60,MealSatisfied=85,DrinkStart=40,DehydrationWarning=20;
    public static double DehydrationMovementFactor(Creature c)=>.55+.45*Math.Clamp(c.Interaction.Hydration/DehydrationWarning,0,1);
    public static string DehydrationStatus(Creature c)=>c.Condition==PhysicalCondition.Dead||c.Family.ColdStorage.Stored?"":
        c.Interaction.Hydration<=0?"严重脱水：生命持续下降，行动变慢。请及时补水。":
        c.Interaction.Hydration<DehydrationWarning?"缺水：行动变慢，体力额外消耗；生命不再自然恢复。":"";
    public const double UrgentFood=30,UrgentWater=35,WakeFood=20,WakeWater=20;
    public const double DaytimeSleepiness=90, NapSatisfied=35, ExhaustedEnergy=15;
    // Even the longest summer day fits before exceptional daytime exhaustion;
    // a short summer night can repay that sleep debt in the most crowded hut.
    public const double AwakeSleepinessPerSecond=.18, ActivitySleepinessPerSecond=.02, SleepRecoveryPerSecond=.85;
    public static bool Urgent(Creature c,bool sleeping=false)=>c.Fullness<(sleeping?WakeFood:UrgentFood)||c.Interaction.Hydration<(sleeping?WakeWater:UrgentWater);
    // Nominal daily exposure probability at contamination 1 and robustness .5.
    // Dose integrates to the same daily hazard even when bite frequency changes.
    public static double InfectionChance(Creature c,RoomObjectKind kind,double contamination)
    {
        double dose=NaturalResources.IsWater(kind)?NaturalResources.WaterValue(kind)/(HydrationPerSecond*GrowthRules.Day):
            NaturalResources.FoodValue(kind)/(HungerPerSecond*GrowthRules.Day);
        double constitution=Math.Clamp(1.5-c.Robustness,.5,1.5);
        double vulnerability=1+(100-c.Health)/100+(c.GrowthStage==GrowthStage.Infant?.2:0);
        double daily=NaturalResources.IsWater(kind)?.22:.12;
        return 1-Math.Exp(Math.Log(1-daily)*Math.Clamp(contamination,0,1)*dose*constitution*vulnerability);
    }
}

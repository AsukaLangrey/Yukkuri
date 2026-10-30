namespace Yukkuri.Simulation;

public enum BodyPart { Head, Cheek, Side, Bottom, Mouth, Accessory }
public enum InteractionTargetKind { Body, Stem }
public enum SurfaceSide { Whole, Left, Right }
public enum RestraintKind { None, Hand, Frame, Box, Hanging, Wrap }
public enum AccessoryPlace { Worn, Hand, Ground, Stored, Hung }
public enum RoomObjectKind { Meal, Sweet, Water, Ball, Treasure, Cushion, Leaves, RiverWater, Urine, Feces }
public enum IntakeKind { Meal, Sweet, Water }
public enum LearnedCue { Follow, Stop, Wait }
public enum InteractionAction
{
    Greet, Praise, Comfort, Stroke, Nuzzle, Lift, PutDown, Wrap, Rock, Unwrap,
    FeedMeal, FeedSweet, Drink, PlaceMeal, PlaceSweet, Clean, Dry, TurnDry,
    TakeAccessoryCare, CleanAccessory, RepairAccessory, ReturnAccessory, ReplaceAccessory,
    GiveBall, PlayBall, GiveTreasure, Sing, Stretch,
    TeachFollow, TeachStop, TeachWait, Follow, Stop, Wait,
    Inspect, Care, Rest, Poke, Block, Tease, Scold, Cold, Belittle,
    Flick, Push, Slap, Squeeze, Press, Roll, Throw, Drop,
    Flip, Hold, Pin, Release, Box, OpenBox,
    TakeAccessory, StoreAccessory, HangAccessory, DamageAccessory, BurnFeet, Hang, AdjustHanging, Lower,
    Touch, PlaceWater, Punch, Burn, SupportStem
}

public sealed class AccessoryState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public AccessoryPlace Place { get; set; }
    public double Integrity { get; set; } = 100;
    public double Dirt { get; set; } = 12;
    public double RepairProgress { get; set; }
    public int CompletedRepairs { get; set; }
    public double X { get; set; }
    public double Z { get; set; }
    public double Height { get; set; }
    public bool WasStolen { get; set; }
    public bool BorrowedForCare { get; set; }
    public int Losses { get; set; }
    public int Replacements { get; set; }
}

public sealed class CueLearning
{
    public double Familiarity { get; set; }
    public int Attempts { get; set; }
    public int Successes { get; set; }
    public int Applications { get; set; }
    public bool HasDemonstration { get; set; }
    public double LastPracticeAt { get; set; } = -100;
    public double DemonstrationX { get; set; }
    public double DemonstrationZ { get; set; }
}

public sealed partial class InteractionLife
{
    public bool Enabled { get; set; }
    public double FootIntegrity { get; set; } = 100;
    public double FootRecovery { get; set; }
    public bool FootTreated { get; set; }
    public double Dirt { get; set; } = 18;
    public double Wetness { get; set; }
    public double HeadSensitivity { get; set; }
    public double CheekSensitivity { get; set; }
    public double AffectionSaturation { get; set; }
    public double FoodSuspicion { get; set; }
    public double FireFear { get; set; }
    public double ContainerFear { get; set; }
    public double TouchPreference { get; set; } = .5;
    public double NuzzleFamiliarity { get; set; }
    public double WrapFamiliarity { get; set; }
    public double ToyInterest { get; set; } = .6;
    public double ToySkill { get; set; }
    public double CareContinuity { get; set; }
    public BodyPart? DressingPart { get; set; }
    public SurfaceSide DressingSide { get; set; }
    public double DressingRemaining { get; set; }
    public double EatingRemaining { get; set; }
    public IntakeKind EatingKind { get; set; }
    public bool GroundIntake { get; set; }
    public double IntakeFacing { get; set; }
    public RoomObjectKind? GroundIntakeKind { get; set; }
    public string? GroundIntakeSourceId { get; set; }
    public bool NaturalIntakePending { get; set; }
    public double GroundIntakeDistance { get; set; } = .792;
    public double GroundIntakeSourceHeight { get; set; }
    public double NaturalBitePause { get; set; }
    public double Hydration { get; set; } = 75;
    public double LastThirstRequestAt { get; set; } = -100;
    public double SocialResponseRemaining { get; set; }
    public double WetRestRemaining { get; set; }
    public double RestRemaining { get; set; }
    public bool Resting { get; set; }
    public double SupineRemaining { get; set; }
    public double RestraintSeconds { get; set; }
    public double Height { get; set; }
    public double Facing { get; set; }
    public double VelocityX { get; set; }
    public double VelocityZ { get; set; }
    public double VerticalVelocity { get; set; }
    public bool Falling { get; set; }
    public bool Carried { get; set; }
    public bool Supine { get; set; }
    public bool FollowPlayer { get; set; }
    public double WaitRemaining { get; set; }
    public LearnedCue? ActiveCue { get; set; }
    public bool CueDemonstration { get; set; }
    public double CueElapsed { get; set; }
    public double CueOriginX { get; set; }
    public double CueOriginZ { get; set; }
    public double CueTravel { get; set; }
    public bool CueUnderstood { get; set; }
    public RestraintKind Restraint { get; set; }
    public AccessoryState Accessory { get; set; } = new();
    public Dictionary<LearnedCue, CueLearning> Learning { get; set; } = new();
    public string? PendingPraise { get; set; }
    public double PraiseRemaining { get; set; }
    public double NextAutonomyAt { get; set; } = 8;
    public double LastHelpAt { get; set; } = -100;
    public double LastRequestAt { get; set; } = -100;
    public double LastWalkAt { get; set; } = -100;
    public string LastMeaningfulEvent { get; set; } = "";
    public string LastContact { get; set; } = "";
    public string LastObservedCondition { get; set; } = "尚未检查";
    public int DialogueSequence { get; set; }
    public Dictionary<string, double> Memories { get; set; } = new();
    public bool CanWalk => FootIntegrity > 25 && Restraint == RestraintKind.None && !Carried && !Falling && !Supine && !Water.InWater;
    public bool HasAccessory => Accessory.Place == AccessoryPlace.Worn && Accessory.Integrity > 0;
    public static double IntakeDuration(IntakeKind kind)=>kind==IntakeKind.Water?1.2:1.6;
    public void BeginIntake(IntakeKind kind)
    {EatingKind=kind;EatingRemaining=IntakeDuration(kind);GroundIntake=false;GroundIntakeKind=null;GroundIntakeSourceId=null;NaturalIntakePending=false;GroundIntakeDistance=.792;GroundIntakeSourceHeight=0;}
}

public sealed class RoomObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public RoomObjectKind Kind { get; set; }
    public string RegionId { get; set; } = "home";
    public string? OwnerId { get; set; }
    public string? CarrierId { get; set; }
    public double X { get; set; }
    public double Z { get; set; }
    public double Height { get; set; }
    public double VelocityX { get; set; }
    public double VelocityZ { get; set; }
    public int Portions { get; set; } = 1;
    public bool Stored { get; set; }
    public double Contamination { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool HeldByHand { get; set; }
}

public sealed partial class Creature
{
    public InteractionLife Interaction { get; set; } = new();
}

public sealed partial class WorldState
{
    public bool InteractionPlaytest { get; set; }
    public List<RoomObject> RoomObjects { get; set; } = [];
    public long InteractionSequence { get; set; }
}

public sealed record InteractionInput(InteractionAction Action, BodyPart Part = BodyPart.Head,
    double Force = .3, double Duration = .1, double Motion = 0,
    double X = 0, double Z = 0, double Height = 0, double DirectionX = 1, double DirectionZ = 0,SurfaceSide Side=SurfaceSide.Whole,string? ObjectId=null,
    double StopImpact=0,double TouchTangentFraction=1,double TouchNormalSpeed=0,
    InteractionTargetKind TargetKind=InteractionTargetKind.Body);

public sealed record InteractionFeedback(bool Success, string Message, string Speech,
    string Pose, bool Refused = false, bool Changed = false, double Effect = 0);

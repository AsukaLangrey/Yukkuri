namespace Yukkuri.Simulation;

public enum ContactAction { Stroke, Hug, Nuzzle, Train, Warn, Discipline, Treat }
public enum ContactArea { Forehead, Cheek, Body }
public enum ContactTool { Hand, SoftGlove, TrainingPaddle }
public enum PhysicalCondition { Active, Critical, Recovering, Dead }
public enum Expression { Neutral, Curious, Content, Delighted, Shy, Defiant, Frightened, Angry, Hurt, Calling, Sleeping, Exhausted }
public enum IncidentKind { Intrusion, Theft, Attack, Correction, Care, Protection, Help, Death, Rescue }
public enum KnowledgeSource { Witness, Heard, Told, Discovered }
public enum ThreatPhase { Quiet, Approaching, Intruding, Retreating, Resolved }

public sealed record ContactRequest(ContactAction Action, ContactArea Area=ContactArea.Forehead,
    ContactTool Tool=ContactTool.Hand, double Force=.25, double Duration=1.5);
public sealed record InteractionResult(bool Success,string Message,string Reaction,Expression Expression,
    double Damage=0,double TrustChange=0,bool Justified=false,string? IncidentId=null);

public sealed class Incident
{
    public string Id { get; set; } = "";
    public IncidentKind Kind { get; set; }
    public string? ActorId { get; set; }
    public string? TargetId { get; set; }
    public string RegionId { get; set; } = "home";
    public double X { get; set; }
    public double Z { get; set; }
    public double At { get; set; }
    public string Description { get; set; } = "";
    public string? ReasonIncidentId { get; set; }
    public double Severity { get; set; }
    public bool Resolved { get; set; }
}
public sealed class KnownIncident
{
    public string IncidentId { get; set; } = "";
    public KnowledgeSource Source { get; set; }
    public bool KnowsActor { get; set; }
    public double LearnedAt { get; set; }
    public string? InformantId { get; set; }
}
public sealed class PersonalMemory
{
    public string IncidentId { get; set; } = "";
    public string Text { get; set; } = "";
    public double At { get; set; }
    public double EmotionalWeight { get; set; }
}
public sealed class ThreatState
{
    public ThreatPhase Phase { get; set; }
    public string? IntruderId { get; set; }
    public string? DefenderId { get; set; }
    public string? IncidentId { get; set; }
    public double NextAt { get; set; } = 180;
    public double PhaseTime { get; set; }
    public double EncounterCooldown { get; set; }
    public bool HelpRequested { get; set; }
    public bool PlayerResponded { get; set; }
    public int Sequence { get; set; }
}
public sealed class ScenarioProgress
{
    public HashSet<string> Completed { get; set; } = [];
    public int CareActions { get; set; }
    public int TrainingSessions { get; set; }
    public int Rescued { get; set; }
    public int Defenses { get; set; }
    public int ResearchObservations { get; set; }
    public int CareContracts { get; set; }
    public double NextContractAt { get; set; }
    public bool IntroDismissed { get; set; }
}
public sealed class PlaySettings
{
    public bool Invasions { get; set; } = true;
    public bool Accidents { get; set; } = true;
    public bool LethalInjuries { get; set; } = true;
    public bool ProtectCareTools { get; set; } = true;
    public bool Pause { get; set; }
    public double Speed { get; set; } = 1;
    public double MusicVolume { get; set; } = .55;
    public double EffectsVolume { get; set; } = .7;
}
public sealed partial class Creature
{
    public double Fear { get; set; }
    public double Attachment { get; set; } = 10;
    public double Resentment { get; set; }
    public double Obedience { get; set; } = 20;
    public double Belonging { get; set; }
    public double Courage { get; set; } = .45;
    public double Aggression { get; set; } = .15;
    public double Patience { get; set; } = .5;
    public double GuardSkill { get; set; } = 10;
    public double SharingSkill { get; set; } = 10;
    public double Pain { get; set; }
    public double Strain { get; set; }
    public double FillingIntegrity { get; set; } = 100;
    public PhysicalCondition Condition { get; set; }
    public double CriticalRemaining { get; set; }
    public bool Stabilized { get; set; }
    public double RecoveryRemaining { get; set; }
    public string InjuryCause { get; set; } = "";
    public Expression Expression { get; set; }
    public string Speech { get; set; } = "";
    public string LastReaction { get; set; } = "";
    public int ReactionSerial { get; set; }
    public double ReactionRemaining { get; set; }
    public string? ParentId { get; set; }
    public bool IsYoung { get; set; }
    public bool IsElder { get; set; }
    public bool GuardDuty { get; set; }
    public bool IsIntruder { get; set; }
    public string? CurrentOffenseId { get; set; }
    public double OffenseRemaining { get; set; }
    public double BehaviorCooldown { get; set; }
    public double SocialCooldown { get; set; }
    public double TrainingFatigue { get; set; }
    public double RetreatRemaining { get; set; }
    public double LastPlayerTouchAt { get; set; } = -100;
    public int CareSessionCount { get; set; }
    public int Familiarity { get; set; }
    public int TrainingCount { get; set; }
    public List<KnownIncident> Knowledge { get; set; } = [];
    public List<PersonalMemory> PersonalMemories { get; set; } = [];
    public Dictionary<string,int> ActionCounts { get; set; } = [];
}
public sealed partial class WorldState
{
    public bool MilestoneScenario { get; set; }
    public int ScenarioVersion { get; set; } = 1;
    public PlaySettings Settings { get; set; } = new();
    public ScenarioProgress Progress { get; set; } = new();
    public ThreatState Threat { get; set; } = new();
    public int Medicine { get; set; } = 6;
    public long IncidentSequence { get; set; }
    public List<Incident> Incidents { get; set; } = [];
    public HashSet<string> PlayerKnownIncidents { get; set; } = [];
    public string? HelpTargetId { get; set; }
    public string LastActionMessage { get; set; } = "";
    public double EnvironmentRating { get; set; } = 80;
    public double SocialTimer { get; set; }
    public bool ScenarioComplete { get; set; }
}

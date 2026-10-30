namespace Yukkuri.Simulation;

public sealed class LocalInjury
{
    public BodyPart Part { get; set; }
    public SurfaceSide Side { get; set; }
    public double Damage { get; set; }
    public double PermanentLoss { get; set; }
    public double CareRemaining { get; set; }
    public bool SelfCareAttempted { get; set; }
    public string Cause { get; set; } = "";
}

public sealed class BodyInjuryState
{
    public List<LocalInjury> Sites { get; set; } = [];
    public WildSelfCareState SelfCare { get; set; } = new();
    public double CareCooldown { get; set; }
    public bool CriticalEpisode { get; set; }
    public double CriticalCareBudget { get; set; } = 90;
    public double HealthyRecoverySeconds { get; set; }
    public string DeathCause { get; set; } = "";
    public double DeathAt { get; set; }

    public LocalInjury? At(BodyPart part, SurfaceSide side = SurfaceSide.Whole) =>
        Sites.Find(s => s.Part == part && s.Side == InteractionLife.DressingSiteSide(part, side));
    public double PermanentFootLoss => At(BodyPart.Bottom)?.PermanentLoss ?? 0;
    public bool HasActiveWounds => Sites.Any(s => s.Damage > .01);
    public bool NeedsCare(BodyPart part, SurfaceSide side) => At(part, side) is { Damage: > .01, CareRemaining: <= 0 };
    public static void Validate(BodyInjuryState? state, double worldSeconds)
    {
        static bool Range(double value, double max) => double.IsFinite(value) && value >= 0 && value <= max;
        if (state == null || state.Sites == null || state.Sites.Count > 7 ||
            !Range(state.CareCooldown, 30) || !Range(state.CriticalCareBudget, 90) ||
            !Range(state.HealthyRecoverySeconds, 300) || !Range(state.DeathAt, worldSeconds + .001) ||
            state.DeathCause == null || state.DeathCause.Length > 256)
            throw new InvalidDataException("局部伤势或救治记录无效。");
        foreach (var site in state.Sites)
            if (site == null || !Enum.IsDefined(site.Part) || site.Part == BodyPart.Accessory || !Enum.IsDefined(site.Side) ||
                site.Side != InteractionLife.DressingSiteSide(site.Part, site.Side) ||
                !Range(site.Damage, 100) || !Range(site.PermanentLoss, 100) || !Range(site.CareRemaining, 180) ||
                site.Cause == null || site.Cause.Length > 256)
                throw new InvalidDataException("局部伤势的部位、程度或护理时间无效。");
        if (state.Sites.Select(s => (s.Part, s.Side)).Distinct().Count() != state.Sites.Count)
            throw new InvalidDataException("同一部位的伤势重复。");
    }
}

public sealed partial class InteractionLife
{
    public BodyInjuryState Injuries { get; set; } = new();
}

public sealed partial class WorldSimulation
{
    // Local harm is distinct from global health. Food, affection and wiping do not repair it.
    private void ApplyLocalInjury(Creature c, BodyPart part, SurfaceSide side, double amount, string cause)
    {
        if (amount <= 0 || part == BodyPart.Accessory || c.Condition == PhysicalCondition.Dead) return;
        CancelWildSelfCare(c,"新的受伤打断了自行护伤；这次未完成的处置没有产生护理效果。");
        var i = c.Interaction; var state = i.Injuries;
        side = InteractionLife.DressingSiteSide(part, side);
        var wound = state.At(part, side);
        if (wound == null) { wound = new() { Part = part, Side = side }; state.Sites.Add(wound); }
        // Another blow to an unhealed wound cannot buy another random attempt.
        // Only a completely healed site starts a genuinely new injury episode.
        if(wound.Damage==0)wound.SelfCareAttempted=false;
        wound.Damage = Clamp100(wound.Damage + amount);
        wound.Cause = cause; wound.CareRemaining = 0;
        // Only substantial accumulated local damage leaves lasting disability.
        wound.PermanentLoss = Math.Max(wound.PermanentLoss, Math.Max(0, wound.Damage - 65) * 2.2);
        wound.PermanentLoss = Clamp100(wound.PermanentLoss);
        i.RemoveDressing(part, side);
        state.HealthyRecoverySeconds = 0;
        if (part == BodyPart.Bottom)
        {
            i.FootIntegrity = Math.Max(0, Math.Min(i.FootIntegrity - amount, 100 - wound.PermanentLoss));
            i.FootTreated = false; i.FootRecovery = 0;
        }
        else if (part == BodyPart.Head) i.HeadSensitivity = Clamp100(i.HeadSensitivity + amount * .5);
        else if (part == BodyPart.Cheek) i.ChangeCheekSensitivity(side, amount * .5);
    }

    private void AdvanceBodyInjuries(Creature c, double dt)
    {
        if (dt <= 0 || c.Condition == PhysicalCondition.Dead) return;
        var i = c.Interaction; var state = i.Injuries;
        state.CareCooldown = Math.Max(0, state.CareCooldown - dt);
        if (c.Condition == PhysicalCondition.Critical)
        {
            state.CriticalEpisode = true;
            state.CriticalCareBudget = Math.Min(state.CriticalCareBudget, Math.Max(0, c.CriticalRemaining - dt));
            state.HealthyRecoverySeconds = 0;
            return;
        }
        bool nourished = c.Fullness > 25 && c.Energy > 20 && (!i.Enabled || i.Hydration > 20);
        foreach (var wound in state.Sites)
        {
            bool cared = wound.CareRemaining > 0;
            wound.CareRemaining = Math.Max(0, wound.CareRemaining - dt);
            if (nourished && cared) wound.Damage = Math.Max(0, wound.Damage - dt * (i.Resting ? .12 : .04));
            // An untreated severe injury is a continuing health burden, not a second instantaneous blow.
            else if (!cared && wound.Damage > 55)
                Hurt(c, dt * (wound.Damage - 55) * .0008, "未稳定的伤势", null);
            if (c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical) return;
        }
        if (i.FootTreated && nourished)
        {
            i.FootRecovery = Math.Max(0, i.FootRecovery - dt);
            double ceiling = 100 - state.PermanentFootLoss;
            if (i.FootRecovery <= 0)
            {
                bool unable = i.FootIntegrity <= 25;
                i.FootIntegrity = Math.Min(ceiling, i.FootIntegrity + dt * (i.Resting && HasCushionedRestSupport(c) ? .55 : .25));
                if (unable && i.FootIntegrity > 25)
                    Feedback(c, "recovery", Expression.Shy, "底部开始恢复支撑能力，可以试着慢慢移动。", "careful-step");
                if (i.FootIntegrity >= ceiling) i.FootTreated = false;
            }
        }
        i.FootIntegrity = Math.Min(i.FootIntegrity, 100 - state.PermanentFootLoss);
        if (state.CriticalEpisode && c.Condition == PhysicalCondition.Active && c.Health >= 70 && c.Pain <= 10 && nourished && !state.HasActiveWounds)
        {
            state.HealthyRecoverySeconds = Math.Min(300, state.HealthyRecoverySeconds + dt);
            if (state.HealthyRecoverySeconds >= 300)
            { state.CriticalEpisode = false; state.CriticalCareBudget = 90; }
        }
        else state.HealthyRecoverySeconds = 0;
    }

    private void StabilizeBody(Creature c, double recoverySeconds)
    {
        var state = c.Interaction.Injuries;
        if (c.Condition == PhysicalCondition.Critical)
        {
            state.CriticalEpisode = true;
            state.CriticalCareBudget = Math.Min(state.CriticalCareBudget, c.CriticalRemaining);
            c.Stabilized = true; c.Condition = PhysicalCondition.Recovering;
            c.RecoveryRemaining = recoverySeconds; c.Health = 8; c.CriticalRemaining = 0;
            c.Activity = "休养恢复"; RefreshHelpAfterResolution(c);
        }
    }

    private bool ApplyBodyCare(Creature c, BodyPart part, SurfaceSide side)
    {
        CancelWildSelfCare(c,"外部护理已经接手，自行护伤停止；此前付出的精力不返还。");
        var i = c.Interaction; var state = i.Injuries;
        bool localCare = NeedsLocalBodyCare(c, part, side);
        if (c.Condition != PhysicalCondition.Critical && state.CareCooldown > 0 && !localCare) return false;
        bool critical = c.Condition == PhysicalCondition.Critical;
        bool systemicCare = state.CareCooldown <= 0;
        StabilizeBody(c, 55);
        if (!critical && systemicCare) c.Health = Math.Min(100, c.Health + 12);
        if (systemicCare || critical) state.CareCooldown = 30;
        if (state.At(part, side) is { } wound) wound.CareRemaining = 180;
        if (part == BodyPart.Bottom && i.FootIntegrity < 100 - state.PermanentFootLoss && !i.FootTreated)
        { i.FootTreated = true; i.FootRecovery = Math.Max(i.FootRecovery, 60); }
        c.Pain = Math.Max(0, c.Pain - (systemicCare || critical ? 18 : 4));
        if (part == BodyPart.Head) i.HeadSensitivity = Math.Max(0, i.HeadSensitivity - 10);
        if (part == BodyPart.Cheek) i.ChangeCheekSensitivity(side, -10);
        if (part == BodyPart.Side && i.Digestion.Symptomatic)
            i.Digestion.CareRemaining = Math.Max(i.Digestion.CareRemaining, 60);
        i.Dress(part, side); return true;
    }

    private static bool NeedsLocalBodyCare(Creature c, BodyPart part, SurfaceSide side)
    {
        var i = c.Interaction;
        return i.Injuries.NeedsCare(part, side) ||
            part == BodyPart.Bottom && i.FootIntegrity < 100 - i.Injuries.PermanentFootLoss && !i.FootTreated ||
            !i.HasDressing(part, side) && (part == BodyPart.Head && i.HeadSensitivity > 15 ||
                part == BodyPart.Cheek && i.CheekSensitivityAt(side) > 15) ||
            part == BodyPart.Side && i.Digestion.Symptomatic && i.Digestion.CareRemaining <= 0;
    }

    // The milestone UI has no bottom/left/right care selector. A general care
    // action still needs to treat a real wound, rather than bandaging a healthy forehead.
    private static (BodyPart Part, SurfaceSide Side) ChooseGeneralCareSite(Creature c, BodyPart preferred)
    {
        foreach (var side in InteractionLife.HasPairedSides(preferred)
            ? new[] { SurfaceSide.Left, SurfaceSide.Right } : new[] { SurfaceSide.Whole })
            if (NeedsLocalBodyCare(c, preferred, side)) return (preferred, side);
        var wound = c.Interaction.Injuries.Sites.Where(s => s.Damage > .01 && s.CareRemaining <= 0)
            .OrderByDescending(s => s.Damage).FirstOrDefault();
        if (wound != null) return (wound.Part, wound.Side);
        foreach (var part in Enum.GetValues<BodyPart>())
            foreach (var side in new[] { SurfaceSide.Left, SurfaceSide.Right })
                if (NeedsLocalBodyCare(c, part, side)) return (part, side);
        return (preferred, SurfaceSide.Whole);
    }

    private void StopIncapacitatedActions(Creature c)
    {
        CancelWildSelfCare(c,"身体已经无法继续自行护伤，未完成的动作已停止。");
        var i = c.Interaction;
        // End an unfinished intervention before rescue actions or a close-up save.
        // A fatal Hurt settles Die immediately after this cleanup; let Die keep
        // the original pregnancy phase long enough to record its death outcome.
        bool deathPending=State.Settings.LethalInjuries&&
            (c.FillingIntegrity<=0||c.CriticalRemaining<=0);
        if(c.Condition==PhysicalCondition.Critical&&FamilyRules.UnderIntervention(c)&&!deathPending)
            CancelIntervention(c,"母体进入濒危，强制干预立即中止；没有开始新的孕育，已用材料、关系后果与原有恢复负担保留。");
        EndHumanCompany(c);
        CancelFamilyCare(c,reason:"身体已无法继续照护，递送已中断；已取份额留在身旁。");
        // Loading an already-dead body must not refresh social deadlines.
        if(c.Family.CompanionTargetId!=null)ClearCompanionship(c);
        else c.Family.CompanionUntil=0;
        DetachNaturalIntake(c);
        i.EatingRemaining = 0; i.GroundIntake = false; i.GroundIntakeKind = null;
        c.Personality.HungerTargetId=null;c.Personality.HungerFeeding=new();
        i.GroundIntakeSourceId = null; i.NaturalIntakePending = false; i.NaturalBitePause = 0;
        i.Physiology.Excreting = ExcretionKind.None; i.Physiology.Remaining = 0;
        i.ActiveCue = null; i.FollowPlayer = false; i.WaitRemaining = 0;
        i.PendingConversation = null; i.ConversationPraise = null; i.ConversationDelay = 0;
        i.PendingPraise = null; i.PraiseRemaining = 0; i.Resting = false;
        c.TargetX = c.X; c.TargetZ = c.Z;
    }
}

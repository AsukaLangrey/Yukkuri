namespace Yukkuri.Simulation;

public sealed class WildSelfCareState
{
    public bool Active { get; set; }
    public BodyPart? Part { get; set; }
    public SurfaceSide Side { get; set; }
    public double Remaining { get; set; }
    // Rolled and paid at the start, never rerolled when continuing a saved action.
    public bool Success { get; set; }
    public string? RegionId { get; set; }
    public string LastEvent { get; set; } = "";
}

public static class WildSelfCareRules
{
    // Prototype tuning for limited surface-wound care, not surgery or crisis rescue.
    public const double Duration = 6, EnergyCost = 3, CareSeconds = 180;
    public static bool Supports(BodyPart part) => part is BodyPart.Head or BodyPart.Cheek or BodyPart.Side or BodyPart.Bottom;
    public static double SuccessChance(Creature c, LocalInjury wound) => Math.Clamp(
        .65 - wound.Damage * .004 + (c.Energy - 40) * .002 +
        (c.Fullness - 70) * .001 + (c.Interaction.Enabled ? (c.Interaction.Hydration - 70) * .001 : 0), .2, .85);

    internal static bool BodyAvailable(Creature c)
    {
        var i=c.Interaction;
        return c.Condition==PhysicalCondition.Active&&!c.Family.ColdStorage.Stored&&c.Family.ColdStorage.RecoveryRemaining<=0&&
            !FamilyRules.UnderIntervention(c)&&c.Family.Pregnancy.Phase!=PregnancyPhase.Preparing&&
            i.Restraint==RestraintKind.None&&!i.Carried&&!i.Falling&&!i.Lowering&&!i.Supine&&i.Height<=.05&&
            Math.Abs(i.VelocityX)<.05&&Math.Abs(i.VelocityZ)<.05&&Math.Abs(i.VerticalVelocity)<.05&&!i.Rolling&&
            i.EatingRemaining<=0&&!i.NaturalIntakePending&&!i.Physiology.Active&&
            i.ActiveCue==null&&!i.FollowPlayer&&i.WaitRemaining<=0&&!i.Resting&&i.RestRemaining<=0&&
            i.HumanCompany.Until<=0&&c.Family.Conflict.TargetId==null&&c.Family.ChildThreat.TargetId==null&&
            c.Family.FlightRemaining<=0&&c.RetreatRemaining<=0;
    }

    internal static bool InDanger(Creature c,WorldState world) => c.IsIntruder||
        (world.Threat.Phase is ThreatPhase.Approaching or ThreatPhase.Intruding or ThreatPhase.Retreating)&&
        (world.Threat.IntruderId==c.Id||world.Threat.DefenderId==c.Id);

    public static void Validate(Creature c,WorldState world)
    {
        if(c.Interaction==null||c.Interaction.Injuries==null||c.Interaction.HumanCompany==null||
            c.Interaction.Physiology==null||c.Interaction.Digestion==null||c.Family==null||
            c.Family.ColdStorage==null||c.Family.Pregnancy==null||c.Family.Conflict==null||c.Family.ChildThreat==null||
            world.Regions==null||world.Threat==null||c.Interaction.Injuries.Sites==null||c.Interaction.Injuries.Sites.Any(s=>s==null))
            throw new InvalidDataException("自行护伤所需的身体、家庭或世界结构缺失。");
        var injury=c.Interaction.Injuries;var care=injury.SelfCare;
        if(care==null||!double.IsFinite(care.Remaining)||care.Remaining<0||care.Remaining>Duration||
            !Enum.IsDefined(care.Side)||care.LastEvent==null||care.LastEvent.Length>256||
            care.Part.HasValue&&!Supports(care.Part.Value)||care.RegionId is { Length:>128 })
            throw new InvalidDataException("野外自行护伤记录无效。");
        if(!care.Active)
        {
            if(care.Part!=null||care.Side!=SurfaceSide.Whole||care.Remaining!=0||care.Success||care.RegionId!=null)
                throw new InvalidDataException("已结束的自行护伤仍保留进行状态。");
            return;
        }
        if(!world.MilestoneScenario||care.Part==null||care.Remaining<=0||care.RegionId!=c.RegionId||
            !world.Regions.Any(r=>r!=null&&r.Id==care.RegionId&&r.Kind==RegionKind.Wilderness)||
            care.Side!=InteractionLife.DressingSiteSide(care.Part.Value,care.Side)||
            injury.At(care.Part.Value,care.Side) is not { Damage:>.01, CareRemaining:0, SelfCareAttempted:true }||
            !BodyAvailable(c)||InDanger(c,world)||c.Family.CareTargetId!=null||c.Family.CareParcelId!=null||
            c.Family.CompanionTargetId!=null)
            throw new InvalidDataException("自行护伤与当前伤口、地区或身体动作不相容。");
        // Food/energy may cross a threshold during the current world tick.
        // Runtime synchronization cancels that action; loading must not mistake
        // an otherwise valid finite action for corrupt data solely for that drift.
    }
}

public sealed partial class WorldSimulation
{
    private bool WildSelfCareReady(Creature c,bool starting) => State.MilestoneScenario&&WildSelfCareRules.BodyAvailable(c)&&
        !WildSelfCareRules.InDanger(c,State)&&!(InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy)&&
        !NaturalResources.WantsFood(c)&&(!c.Interaction.Enabled||!NaturalResources.WantsWater(c))&&
        c.Energy>=(starting?40:20)&&c.Pain<70&&c.Fear<55&&!c.Interaction.Digestion.Symptomatic;

    // Only validates an existing action: safe for entry-point cleanup, including
    // paused/close-up commands. It never starts, advances or draws random values.
    private void SynchronizeWildSelfCare(Creature c)
    {
        var care=c.Interaction.Injuries.SelfCare;
        if(!care.Active)return;
        if(care.RegionId!=c.RegionId||!State.Regions.Any(r=>r.Id==c.RegionId&&r.Kind==RegionKind.Wilderness))
        {CancelWildSelfCare(c,"离开了原来的野外位置，自行护伤已中断；这次机会已经用过。");return;}
        if(!WildSelfCareReady(c,starting:false)||c.Family.CareTargetId!=null||c.Family.CareParcelId!=null||c.Family.CompanionTargetId!=null)
        {CancelWildSelfCare(c,"身体需要、危险或其他动作打断了自行护伤；尚未获得护理效果。");return;}
        if(care.Part==null||c.Interaction.Injuries.At(care.Part.Value,care.Side) is not { Damage:>.01, CareRemaining:0, SelfCareAttempted:true })
            CancelWildSelfCare(c,"伤口状态已有变化，这次自行护伤已停止。");
    }

    private void CancelWildSelfCare(Creature c,string reason)
    {
        var care=c.Interaction.Injuries.SelfCare;
        if(!care.Active)return;
        care.Active=false;care.Part=null;care.Side=SurfaceSide.Whole;care.Remaining=0;care.Success=false;care.RegionId=null;
        care.LastEvent=reason.Length<=256?reason:reason[..256];
        if(c.Condition==PhysicalCondition.Active&&c.Activity=="停在原地护住伤处")c.Activity="自行护伤已中断";
    }

    private bool TickWildSelfCare(Creature c,RegionState region,double dt)
    {
        var injuries=c.Interaction.Injuries;var care=injuries.SelfCare;
        SynchronizeWildSelfCare(c);
        if(State.Settings.Pause||State.Mode==PlayMode.CloseUp||!double.IsFinite(dt)||dt<=0)return care.Active;
        if(!care.Active)
        {
            if(region.Id!=c.RegionId||region.Kind!=RegionKind.Wilderness||!WildSelfCareReady(c,starting:true))return false;
            var wound=injuries.Sites.Where(s=>WildSelfCareRules.Supports(s.Part)&&s.Damage>.01&&s.CareRemaining<=0&&!s.SelfCareAttempted)
                .OrderByDescending(s=>s.Damage).ThenBy(s=>s.Part).ThenBy(s=>s.Side).FirstOrDefault();
            if(wound==null)return false;
            double probability=WildSelfCareRules.SuccessChance(c,wound);
            CancelFamilyCare(c,reason:"开始照顾自己的伤势，这次递送已中断；如有已取份额，已经留在身旁。");
            if(c.Family.CompanionTargetId!=null)ClearCompanionship(c);
            wound.SelfCareAttempted=true;
            care.Active=true;care.Part=wound.Part;care.Side=wound.Side;care.Remaining=WildSelfCareRules.Duration;
            care.Success=Next()<probability;care.RegionId=c.RegionId;
            c.Energy=Math.Max(0,c.Energy-WildSelfCareRules.EnergyCost);
            care.LastEvent="停下来尝试照顾局部伤口，需要时间和精力，也可能失败。";
            c.TargetX=c.X;c.TargetZ=c.Z;c.Activity="停在原地护住伤处";
            return true;
        }
        c.TargetX=c.X;c.TargetZ=c.Z;c.Activity="停在原地护住伤处";
        care.Remaining=Math.Max(0,care.Remaining-dt);
        if(care.Remaining>0)return true;
        var treated=injuries.At(care.Part!.Value,care.Side)!;
        bool success=care.Success;
        // Do not call ApplyBodyCare: that professional action also heals health,
        // starts foot rehabilitation, and adds a dressing. This action does none.
        if(success)treated.CareRemaining=WildSelfCareRules.CareSeconds;
        CancelWildSelfCare(c,success?"这次自行护伤起了作用，局部伤口暂时受到照顾，仍需食水和时间恢复。":
            "尝试自行护伤没有成功；精力和这次机会已经付出，伤口仍需要外部护理。");
        c.Activity=success?"自行护伤完成，继续留意伤势":"自行护伤失败，仍需要帮助";
        return true;
    }
}

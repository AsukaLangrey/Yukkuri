namespace Yukkuri.Simulation;

// These are memories of contact, separate from current pain and willingness.
public sealed class HumanBond
{
    public double Familiarity { get; set; }
    public double Safety { get; set; }
    public double CareBudget { get; set; } = 3;
    public double LastHarmAt { get; set; } = -100;
    public Dictionary<BodyPart, double> Caution { get; set; } = new();
}

public sealed partial class InteractionLife
{
    public HumanBond HumanBond { get; set; } = new();
}

public static class InteractionReadiness
{
    public static bool WillAcceptAffection(Creature c, BodyPart part, SurfaceSide side) =>
        c.Condition == PhysicalCondition.Active && c.Pain < 40 && c.Fear < 68 && c.Trust >= 12 &&
        !c.Interaction.Digestion.Symptomatic &&
        (part != BodyPart.Head || c.Interaction.HeadSensitivity <= 45) &&
        (part != BodyPart.Cheek || c.Interaction.CheekSensitivityAt(side) <= 45) &&
        (part != BodyPart.Bottom || c.Interaction.FootIntegrity > 50) &&
        c.Interaction.HumanBond.Caution.GetValueOrDefault(part) < 55;

    public static string AffectionRefusal(Creature c, BodyPart part) =>
        c.Pain >= 40 || c.Condition != PhysicalCondition.Active ||
        part == BodyPart.Bottom && c.Interaction.FootIntegrity <= 50 ?
        "它还疼着，先别碰这里；信任你也不代表身体受得住。" :
        c.Interaction.HumanBond.Caution.GetValueOrDefault(part) >= 55 ?
        "它记得这里被弄疼过，护着这个部位。先收手，慢慢重新建立安全感。" :
        "它现在不愿被碰，先留一点距离。";
}

public sealed partial class WorldSimulation
{
    private void AdvanceHumanBond(Creature c, double dt)
    {
        var b = c.Interaction.HumanBond;
        if (c.Condition != PhysicalCondition.Active || c.Pain >= 25 || c.Fear >= 60 ||
            State.WorldSeconds - b.LastHarmAt < 30) return;
        b.CareBudget = Math.Min(3, b.CareBudget + dt * .025);
        // Safety must be experienced with a visible, quiet hand. Waiting offscreen
        // does not erase contact memories, and close-up does not refill the budget.
        if (!SeesHelpingHand(c) || InteractionGestureTarget == c.Id ||
            Distance(c.X, c.Z, State.PlayerX, State.PlayerZ) > 2.5) return;
        b.Safety = Clamp100(b.Safety + dt * .015);
        foreach (var part in b.Caution.Keys.ToArray())
            b.Caution[part] = Math.Max(0, b.Caution[part] - dt * .012);
        c.Resentment = Math.Max(0, c.Resentment - dt * .004);
    }

    private void RememberPlayerHarm(Creature c, double amount)
    {
        var b = c.Interaction.HumanBond;
        b.LastHarmAt = State.WorldSeconds;
        b.Safety = Math.Max(0, b.Safety - amount);
        b.CareBudget = 0;
        var part = _interactionGesture?.TargetId == c.Id ? _interactionGesture.Input.Part : BodyPart.Side;
        b.Caution[part] = Clamp100(b.Caution.GetValueOrDefault(part) + amount * 3);
    }
}

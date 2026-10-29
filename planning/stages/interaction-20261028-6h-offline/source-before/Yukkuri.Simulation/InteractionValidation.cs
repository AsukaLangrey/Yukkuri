namespace Yukkuri.Simulation;

public static class InteractionValidation
{
    public static void Validate(WorldState s)
    {
        if (!s.InteractionPlaytest) return;
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); }
        static bool Finite(params double[] values) => values.All(double.IsFinite);
        static bool Range(params double[] values) => Finite(values) && values.All(v => v >= 0 && v <= 100);
        Require(s.MilestoneScenario && s.RoomObjects != null && s.InteractionSequence >= 0, "互动世界状态无效");
        var ids = s.Creatures.Select(c => c.Id).ToHashSet();
        var accessoryIds = new HashSet<string>();
        foreach (var c in s.Creatures)
        {
            var i = c.Interaction;
            Require(i != null && i.Accessory != null && i.Learning != null && i.Memories != null, "互动个体数据缺失");
            Require(Range(i!.FootIntegrity, i.Dirt, i.Wetness, i.HeadSensitivity, i.CheekSensitivity,
                i.AffectionSaturation, i.FoodSuspicion, i.FireFear, i.ContainerFear, i.NuzzleFamiliarity,
                i.WrapFamiliarity, i.ToySkill, i.CareContinuity), "互动状态数值越界");
            Require(Finite(i.FootRecovery, i.EatingRemaining, i.SocialResponseRemaining, i.WetRestRemaining, i.RestRemaining,
                i.SupineRemaining, i.RestraintSeconds, i.Height, i.Facing, i.VelocityX, i.VelocityZ,
                i.VerticalVelocity, i.WaitRemaining, i.NextAutonomyAt, i.LastHelpAt, i.LastRequestAt, i.LastWalkAt,
                i.CueElapsed, i.CueOriginX, i.CueOriginZ, i.CueTravel, i.PraiseRemaining), "互动状态含无效数值");
            Require(i.Height is >= 0 and <= 5 && i.FootRecovery >= 0 && i.EatingRemaining >= 0 &&
                i.RestRemaining >= 0 && i.SocialResponseRemaining>=0 && i.WaitRemaining >= 0 && Enum.IsDefined(i.Restraint) &&
                (i.ActiveCue == null || Enum.IsDefined(i.ActiveCue.Value)), "互动动作状态无效");
            Require(Finite(i.TouchPreference, i.ToyInterest) && i.TouchPreference is >= 0 and <= 1 && i.ToyInterest is >= 0 and <= 1, "互动偏好无效");
            var a = i.Accessory!;
            Require(!string.IsNullOrWhiteSpace(a.Id) && accessoryIds.Add(a.Id) && Enum.IsDefined(a.Place) &&
                Range(a.Integrity, a.Dirt) && Finite(a.X, a.Z, a.Height, a.RepairProgress) &&
                Math.Abs(a.X) <= 9 && Math.Abs(a.Z) <= 9 && a.Height is >= 0 and <= 5 &&
                a.Losses >= 0 && a.Replacements >= 0, "饰品归属或状态无效");
            foreach (var (cue, learning) in i.Learning!)
                Require(Enum.IsDefined(cue) && learning != null && Range(learning.Familiarity) &&
                    learning.Attempts >= 0 && learning.Successes >= 0 && learning.Applications >= 0 &&
                    learning.Successes <= learning.Attempts && learning.Applications <= learning.Successes &&
                    Finite(learning.LastPracticeAt, learning.DemonstrationX, learning.DemonstrationZ), "学习记录无效");
            Require(i.Memories!.All(p => !string.IsNullOrWhiteSpace(p.Key) && double.IsFinite(p.Value) && p.Value >= 0 && p.Value <= 100), "互动记忆无效");
            Require(i.SurfaceDirt!=null&&i.SurfaceWetness!=null&&i.SurfaceDirt.Concat(i.SurfaceWetness).All(p=>Enum.IsDefined(p.Key)&&p.Key!=BodyPart.Accessory&&Range(p.Value)),"局部清洁状态无效");
        }
        var objectIds = new HashSet<string>();
        foreach (var o in s.RoomObjects!)
            Require(o != null && !string.IsNullOrWhiteSpace(o.Id) && objectIds.Add(o.Id) && Enum.IsDefined(o.Kind) &&
                s.Regions.Any(r => r.Id == o.RegionId) && (o.OwnerId == null || ids.Contains(o.OwnerId)) &&
                Finite(o.X, o.Z, o.Height, o.VelocityX, o.VelocityZ) && Math.Abs(o.X) <= 9 && Math.Abs(o.Z) <= 9 &&
                o.Height is >= 0 and <= 5 && o.Portions is >= 0 and <= 1000, "互动场景物品无效");
    }
}

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
        // Validate object structure before pending-intake references inspect its entries.
        var objectIds = new HashSet<string>();
        foreach (var o in s.RoomObjects!)
            Require(o != null && !string.IsNullOrWhiteSpace(o.Id) && objectIds.Add(o.Id) && Enum.IsDefined(o.Kind) &&
                s.Regions.Any(r => r.Id == o.RegionId) && (o.OwnerId == null || ids.Contains(o.OwnerId)) &&
                Finite(o.X, o.Z, o.Height, o.VelocityX, o.VelocityZ,o.Contamination) && o.Contamination is >=0 and <=1 && Math.Abs(o.X) <= 9 && Math.Abs(o.Z) <= 9 &&
                o.Height is >= 0 and <= 5 && o.Portions is >= 0 and <= 1000, "互动场景物品无效");
        var accessoryIds = new HashSet<string>();
        foreach (var c in s.Creatures)
        {
            var i = c.Interaction;
            Require(i != null && i.Accessory != null && i.Learning != null && i.Memories != null, "互动个体数据缺失");
            Require(Range(i!.FootIntegrity, i.Dirt, i.Wetness, i.HeadSensitivity, i.CheekSensitivity,
                i.AffectionSaturation, i.FoodSuspicion, i.FireFear, i.ContainerFear, i.NuzzleFamiliarity,
                i.WrapFamiliarity, i.ToySkill, i.CareContinuity,i.Hydration), "互动状态数值越界");
            Require(Finite(i.FootRecovery, i.EatingRemaining, i.SocialResponseRemaining, i.WetRestRemaining, i.RestRemaining,
                i.SupineRemaining, i.RestraintSeconds, i.Height, i.Facing, i.VelocityX, i.VelocityZ,
                i.VerticalVelocity, i.WaitRemaining, i.NextAutonomyAt, i.LastHelpAt, i.LastRequestAt, i.LastWalkAt,
                i.CueElapsed, i.CueOriginX, i.CueOriginZ, i.CueTravel, i.PraiseRemaining,i.LastThirstRequestAt,i.IntakeFacing), "互动状态含无效数值");
            Require(i.Height is >= 0 and <= 5 && i.FootRecovery >= 0 && i.EatingRemaining >= 0 &&
                i.RestRemaining >= 0 && i.SocialResponseRemaining>=0 && i.WaitRemaining >= 0 && Enum.IsDefined(i.Restraint) &&
                Enum.IsDefined(i.EatingKind)&&(i.ActiveCue == null || Enum.IsDefined(i.ActiveCue.Value)), "互动动作状态无效");
            Require(double.IsFinite(i.GroundIntakeDistance)&&i.GroundIntakeDistance is >=0 and <=2&&
                double.IsFinite(i.GroundIntakeSourceHeight)&&i.GroundIntakeSourceHeight is >=0 and <=5&&
                (i.GroundIntakeKind==null||NaturalResources.IsResource(i.GroundIntakeKind.Value)),"地面取食位置无效");
            Require(i.GroundIntakeSourceId==null||!string.IsNullOrWhiteSpace(i.GroundIntakeSourceId)&&i.GroundIntakeSourceId.Length<=128,"地面取食来源无效");
            Require(double.IsFinite(i.NaturalBitePause)&&i.NaturalBitePause is >=0 and <=1,"自主取食间歇无效");
            Require(!i.NaturalIntakePending||i.GroundIntake&&i.EatingRemaining>0&&i.GroundIntakeSourceId!=null&&
                s.RoomObjects!.Any(o=>o.Id==i.GroundIntakeSourceId&&o.Kind==i.GroundIntakeKind),"尚未入口的食物来源缺失");
            Require(double.IsFinite(i.DressingRemaining)&&i.DressingRemaining>=0&&Enum.IsDefined(i.DressingSide)&&
                (i.DressingPart==null||Enum.IsDefined(i.DressingPart.Value)&&i.DressingPart!=BodyPart.Accessory),"护理敷料状态无效");
            Require(i.Dressings!=null&&i.Dressings.Count<=7&&i.Dressings.All(d=>d!=null&&Enum.IsDefined(d.Part)&&d.Part!=BodyPart.Accessory&&Enum.IsDefined(d.Side)&&
                d.Side==InteractionLife.DressingSiteSide(d.Part,d.Side)&&double.IsFinite(d.Remaining)&&d.Remaining>=0),"分部位护理敷料无效");
            Require(i.Dressings!.Select(d=>(d.Part,d.Side)).Distinct().Count()==i.Dressings!.Count,"同一部位的护理敷料重复");
            Require(Finite(i.TouchPreference, i.ToyInterest) && i.TouchPreference is >= 0 and <= 1 && i.ToyInterest is >= 0 and <= 1, "互动偏好无效");
            var a = i.Accessory!;
            Require(!string.IsNullOrWhiteSpace(a.Id) && accessoryIds.Add(a.Id) && Enum.IsDefined(a.Place) &&
                Range(a.Integrity, a.Dirt) && Finite(a.X, a.Z, a.Height, a.RepairProgress) &&
                Math.Abs(a.X) <= 9 && Math.Abs(a.Z) <= 9 && a.Height is >= 0 and <= 5 &&
                a.Losses >= 0 && a.Replacements >= 0&&a.RepairProgress>=0&&a.CompletedRepairs>=0, "饰品归属或状态无效");
            foreach (var (cue, learning) in i.Learning!)
                Require(Enum.IsDefined(cue) && learning != null && Range(learning.Familiarity) &&
                    learning.Attempts >= 0 && learning.Successes >= 0 && learning.Applications >= 0 &&
                    learning.Successes <= learning.Attempts && learning.Applications <= learning.Successes &&
                    Finite(learning.LastPracticeAt, learning.DemonstrationX, learning.DemonstrationZ), "学习记录无效");
            Require(i.ActiveCue==null||i.Learning!.ContainsKey(i.ActiveCue.Value),"当前练习缺少学习记录");
            Require(i.Memories!.All(p => !string.IsNullOrWhiteSpace(p.Key) && double.IsFinite(p.Value) && p.Value >= 0 && p.Value <= 100), "互动记忆无效");
            Require(i.SurfaceDirt!=null&&i.SurfaceWetness!=null&&i.SurfaceDirt.Concat(i.SurfaceWetness).All(p=>Enum.IsDefined(p.Key)&&p.Key!=BodyPart.Accessory&&Range(p.Value)),"局部清洁状态无效");
            Require(i.SideDirt!=null&&i.SideWetness!=null&&i.SideDirt.Concat(i.SideWetness).All(p=>InteractionLife.HasPairedSides(p.Key)&&p.Value!=null&&Range(p.Value.Left,p.Value.Right)),"左右清洁状态无效");
            Require(i.CheekSensitivitySides==null||Range(i.CheekSensitivitySides.Left,i.CheekSensitivitySides.Right)&&
                Math.Abs(i.CheekSensitivity-Math.Max(i.CheekSensitivitySides.Left,i.CheekSensitivitySides.Right))<.00001,"左右脸侧敏感状态无效");
            Require(i.Resources!=null&&i.Resources.Count<=128&&i.Resources.All(p=>!string.IsNullOrWhiteSpace(p.Key)&&p.Value!=null&&
                s.Regions.Any(r=>r.Id==p.Value.RegionId)&&NaturalResources.IsResource(p.Value.Kind)&&
                Finite(p.Value.X,p.Value.Z,p.Value.SeenAt,p.Value.AvoidUntil,p.Value.Suspicion)&&
                Math.Abs(p.Value.X)<=9&&Math.Abs(p.Value.Z)<=9&&Range(p.Value.Suspicion)),"资源记忆无效");
            Require(Finite(i.ResourceBestDistance,i.ResourceStuckSeconds,i.NextResourceScanAt,i.ExploreUntil,i.LastHelpDecisionAt,i.HelpWaitingUntil)&&
                i.ResourceBestDistance>=0&&i.ResourceStuckSeconds>=0,"自主寻找进度无效");
            Require(Finite(i.RestBestDistance,i.RestStuckSeconds,i.RestOnGroundUntil)&&
                i.RestBestDistance>=0&&i.RestStuckSeconds>=0&&i.RestOnGroundUntil>=0,"自主休息进度无效");
            var digestion=i.Digestion;
            Require(digestion!=null&&digestion.Exposures>=0&&digestion.Episodes>=0&&digestion.Episodes<=digestion.Exposures&&
                Finite(digestion.IncubationRemaining,digestion.SymptomsRemaining,digestion.CareRemaining,digestion.Severity,digestion.NextFoodAt)&&
                digestion.IncubationRemaining>=0&&digestion.SymptomsRemaining>=0&&digestion.CareRemaining>=0&&digestion.NextFoodAt>=0&&Range(digestion.Severity)&&
                !(digestion.IncubationRemaining>0&&digestion.SymptomsRemaining>0)&&
                (digestion.Severity>0||digestion.IncubationRemaining==0&&digestion.SymptomsRemaining==0&&digestion.CareRemaining==0),"消化健康状态无效");
            Require(digestion!.RecentIntakes!=null&&digestion.RecentIntakes.Count<=12&&digestion.RecentIntakes.All(intake=>
                intake!=null&&double.IsFinite(intake.At)&&intake.At>=0&&intake.At<=s.WorldSeconds+.001&&
                (intake.SourceId==null||!string.IsNullOrWhiteSpace(intake.SourceId))),"最近吃喝记忆无效");
        }
    }
}

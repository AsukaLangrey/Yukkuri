namespace Yukkuri.Simulation;

public static class ShelterValidation
{
    public static void Validate(WorldState state)
    {
        static void Require(bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
        Require(state.Shelters!=null&&state.Shelters.Count<=128&&state.ShelterStock is >=0 and <=99&&state.ShelterSequence>=0,"居所库存或序号无效");
        var ids=state.Creatures.Select(c=>c.Id).Concat(state.RoomObjects.Select(o=>o.Id))
            .Concat(state.SupplyStations.Select(s=>s.Id)).Concat(state.DetachedStemBroods.Select(s=>s.EventId)).ToHashSet(StringComparer.Ordinal);
        var guests=new HashSet<string>(StringComparer.Ordinal);
        foreach(var s in state.Shelters!)
        {
            double scale=s==null?1:ShelterGeometry.Scale(s),approach=ShelterGeometry.ApproachDepth(scale);
            Require(s!=null&&!string.IsNullOrWhiteSpace(s.Id)&&ids.Add(s.Id)&&state.Regions.Any(r=>r.Id==s.RegionId)&&
                s.Capacity is 1 or 5&&s.Residents!=null&&s.Residents.Count<=s.Capacity&&
                Enum.IsDefined(s.Phase)&&s.LastEvent!=null&&new[]{s.X,s.Z,s.BlockedSeconds,s.LastHelpAt,s.DoorProgress,s.DoorIdleSeconds,s.DoorHoldRemaining}.All(double.IsFinite)&&
                Math.Abs(s.X)+ShelterGeometry.OuterWidth*.5*scale<=CourtyardGround.PlacementEdge&&s.Z-ShelterGeometry.OuterDepth*.5*scale>=-CourtyardGround.PlacementEdge&&s.Z+approach<=CourtyardGround.PlacementEdge&&
                s.DoorProgress is >=0 and <=1&&s.DoorIdleSeconds is >=0 and <=2&&s.DoorHoldRemaining is >=0 and <=2&&s.BlockedSeconds is >=0 and <=600&&s.LastHelpAt>=-100&&s.LastHelpAt<=state.WorldSeconds,"居所身份、位置或进度无效");
            Require(s!.Id.StartsWith("shelter-",StringComparison.Ordinal)&&long.TryParse(s.Id.AsSpan(8),
                System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out long sequence)&&
                sequence>0&&sequence<=state.ShelterSequence,"居所身份与序号不一致");
            foreach(double dx in new[]{-ShelterGeometry.OuterWidth*.5*scale,0,ShelterGeometry.OuterWidth*.5*scale})
                foreach(double dz in new[]{-ShelterGeometry.OuterDepth*.5*scale,0,ShelterGeometry.OuterDepth*.5*scale,approach})
                    Require(WoodlandLayout.GroundFits(state,s.RegionId,s.X+dx,s.Z+dz,.08),"居所地基不在干燥空地");
            Require(!WoodlandLayout.HasRiver(state,s.RegionId)||!WoodlandLayout.Trees.Any(t=>
                Math.Abs(t.X-s.X)<ShelterGeometry.OuterWidth*.5*scale+t.TrunkRadius&&
                t.Z-s.Z> -ShelterGeometry.OuterDepth*.5*scale-t.TrunkRadius&&t.Z-s.Z<approach+t.TrunkRadius),"居所地基覆盖树干");
            Require(s.GuestId==null?s.Phase==ShelterVisitPhase.Empty&&!s.ExitRequested&&s.BlockedSeconds==0:
                s.Phase!=ShelterVisitPhase.Empty&&guests.Add(s.GuestId)&&state.Creatures.Any(c=>c.Id==s.GuestId),"居所预约或住户身份无效");
            Require(s.Capacity==1?s.Residents!.Count==0:s.GuestId==null,"旧居所与合住预约混用");
            var slots=new HashSet<int>();
            foreach(var v in s.Residents!)
                Require(v!=null&&v.Slot is >=0 and <5&&slots.Add(v.Slot)&&guests.Add(v.CreatureId)&&
                    state.Creatures.Any(c=>c.Id==v.CreatureId&&c.RegionId==s.RegionId)&&
                    Enum.IsDefined(v.Phase)&&v.Phase!=ShelterVisitPhase.Empty&&
                    double.IsFinite(v.BlockedSeconds)&&v.BlockedSeconds is >=0 and <=600,"合住住户、床位或进度无效");
            Require(!state.Shelters.Any(other=>other!=s&&other!=null&&other.RegionId==s.RegionId&&
                WorldSimulation.ShelterSitesOverlap(s.X,s.Z,scale,other)),"居所屋体与门前通道重叠");
        }
        foreach(var c in state.Creatures)
            Require(double.IsFinite(c.Interaction.StemFold)&&c.Interaction.StemFold is >=0 and <=1&&double.IsFinite(c.Interaction.ShelterSearchAfter)&&c.Interaction.ShelterSearchAfter>=0&&
                double.IsFinite(c.Interaction.NightExposureSeconds)&&c.Interaction.NightExposureSeconds is >=0 and <=600&&
                double.IsFinite(c.Interaction.NightHousingDiscomfort)&&c.Interaction.NightHousingDiscomfort is >=0 and <=30,"寻找居所或夜间住宿状态无效");
    }
}

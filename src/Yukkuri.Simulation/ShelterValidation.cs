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
            Require(s!=null&&!string.IsNullOrWhiteSpace(s.Id)&&ids.Add(s.Id)&&state.Regions.Any(r=>r.Id==s.RegionId)&&
                Enum.IsDefined(s.Phase)&&s.LastEvent!=null&&new[]{s.X,s.Z,s.BlockedSeconds,s.LastHelpAt}.All(double.IsFinite)&&
                Math.Abs(s.X)+ShelterGeometry.OuterWidth*.5<=7.8&&s.Z-ShelterGeometry.OuterDepth*.5>=-7.8&&s.Z+2.55<=7.8&&
                s.BlockedSeconds is >=0 and <=600&&s.LastHelpAt>=-100&&s.LastHelpAt<=state.WorldSeconds,"居所身份、位置或进度无效");
            Require(s!.Id.StartsWith("shelter-",StringComparison.Ordinal)&&long.TryParse(s.Id.AsSpan(8),
                System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out long sequence)&&
                sequence>0&&sequence<=state.ShelterSequence,"居所身份与序号不一致");
            foreach(double dx in new[]{-ShelterGeometry.OuterWidth*.5,0,ShelterGeometry.OuterWidth*.5})
                foreach(double dz in new[]{-ShelterGeometry.OuterDepth*.5,0,ShelterGeometry.OuterDepth*.5,2.55})
                    Require(WoodlandLayout.GroundFits(state,s.RegionId,s.X+dx,s.Z+dz,.08),"居所地基不在干燥空地");
            Require(!WoodlandLayout.HasRiver(state,s.RegionId)||!WoodlandLayout.Trees.Any(t=>
                Math.Abs(t.X-s.X)<ShelterGeometry.OuterWidth*.5+t.TrunkRadius&&
                t.Z-s.Z> -ShelterGeometry.OuterDepth*.5-t.TrunkRadius&&t.Z-s.Z<2.55+t.TrunkRadius),"居所地基覆盖树干");
            Require(s.GuestId==null?s.Phase==ShelterVisitPhase.Empty&&!s.ExitRequested&&s.BlockedSeconds==0:
                s.Phase!=ShelterVisitPhase.Empty&&guests.Add(s.GuestId)&&state.Creatures.Any(c=>c.Id==s.GuestId),"居所预约或住户身份无效");
            Require(!state.Shelters.Any(other=>other!=s&&other!=null&&other.RegionId==s.RegionId&&
                Math.Abs(other.X-s.X)<ShelterGeometry.OuterWidth+.1&&Math.Abs(other.Z-s.Z)<3.9),"居所屋体与门前通道重叠");
        }
        foreach(var c in state.Creatures)
            Require(double.IsFinite(c.Interaction.ShelterSearchAfter)&&c.Interaction.ShelterSearchAfter>=0,"寻找居所的时间无效");
    }
}

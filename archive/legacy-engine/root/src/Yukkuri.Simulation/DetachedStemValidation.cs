using System.Diagnostics.CodeAnalysis;

namespace Yukkuri.Simulation;

public static partial class FamilyRules
{
    private static void ValidateDetachedStems(WorldState state)
    {
        static void Require([DoesNotReturnIf(false)] bool valid,string message)
        {if(!valid)throw new InvalidDataException(message);}
        static bool Number(double n,double max=double.MaxValue)=>double.IsFinite(n)&&n>=0&&n<=max;
        static bool Text(string? s,int max=512)=>s!=null&&s.Length<=max;
        static bool Identity(string? s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=128;
        var creatures=state.Creatures.ToDictionary(c=>c.Id,StringComparer.Ordinal);
        Require(state.DetachedStemBroods!=null&&state.DetachedStemBroods.Count<=10000&&state.StemVessels is >=0 and <=99&&
            state.StemVesselPurchases>=0,"离体茎或培养瓶库存无效");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        bool Source(StemParentSource? p)=>p!=null&&Identity(p.Id)&&creatures.ContainsKey(p.Id)&&Identity(p.BodyId)&&
            Text(p.Name,128)&&Identity(p.SpeciesId)&&SupportedSpecies(p.SpeciesId)&&Text(p.Disposition,128)&&
            Number(p.Courage,1)&&Number(p.Patience,1)&&Number(p.Aggression,1);
        foreach(var s in state.DetachedStemBroods!)
        {
            Require(s!=null&&Identity(s.EventId)&&ids.Add(s.EventId)&&Enum.IsDefined(s.Phase)&&
                Source(s.Mother)&&Source(s.Father)&&s.Mother.Id!=s.Father.Id,"离体茎身份或遗传来源无效");
            Require(state.Regions.Any(r=>r.Id==s.RegionId)&&double.IsFinite(s.X)&&double.IsFinite(s.Z)&&
                Math.Abs(s.X)<=8-WorldSimulation.StemVesselRadius&&Math.Abs(s.Z)<=8-WorldSimulation.StemVesselRadius&&
                s.PlannedChildren is 2 or 3&&Number(s.Elapsed,GestationSeconds(BirthMethod.Stem))&&Number(s.Burden,100)&&
                Number(s.Integrity,100)&&Number(s.Hydration,100)&&Number(s.Nutrition,100)&&Number(s.VesselIntegrity,100)&&
                Number(s.DetachedAt,state.WorldSeconds+.001)&&Text(s.LastDamageCause)&&Text(s.LastEvent)&&
                double.IsFinite(s.LastDamageAt)&&s.LastDamageAt>=-100&&s.LastDamageAt<=state.WorldSeconds+.001&&
                double.IsFinite(s.ThreatIncidentAt)&&s.ThreatIncidentAt>=-100&&s.ThreatIncidentAt<=state.WorldSeconds+.001&&
                (s.LastDamageActorId==null||s.LastDamageActorId=="player"||creatures.ContainsKey(s.LastDamageActorId))&&
                (s.ThreatIncidentId==null||Identity(s.ThreatIncidentId)),"离体茎状态数值无效");
            Require(s.Phase==DetachedStemPhase.Failed?s.Integrity==0:s.Integrity>0,"离体茎活性与阶段不一致");
            Require(s.Phase!=DetachedStemPhase.Loose||s.VesselIntegrity==0,"未入瓶的茎出现瓶体");
            Require(s.Phase==DetachedStemPhase.Born?state.CompletedBirths.Contains(s.EventId)&&
                state.Creatures.Count(c=>c.Family.BirthEventId==s.EventId)==s.PlannedChildren:
                !state.CompletedBirths.Contains(s.EventId),"离体茎与出生结算不一致");
            Require(!state.Creatures.Any(c=>c.Family.Pregnancy.Phase==PregnancyPhase.Gestating&&c.Family.Pregnancy.EventId==s.EventId),
                "同一窝同时依附母体并独立存活");
            Require(!s.HeldByHand||s.Phase==DetachedStemPhase.Bottled,"离体茎手持状态无效");
            Require(!s.Cleared||s.Phase is DetachedStemPhase.Failed or DetachedStemPhase.Born,"活窝被误标为清理残留");
        }
        Require(state.DetachedStemBroods.Count(s=>s.HeldByHand)<=1&&
            (!state.DetachedStemBroods.Any(s=>s.HeldByHand)||
                !state.Creatures.Any(c=>c.Interaction.Carried)&&!state.RoomObjects.Any(o=>o.HeldByHand)),"同一只手持有多个实际对象");
    }
}

using System.Diagnostics.CodeAnalysis;

namespace Yukkuri.Simulation;

public static partial class FamilyRules
{
    public static void ValidateFamily(WorldState s)
    {
        static void Require([DoesNotReturnIf(false)] bool valid,string message)
        {if(!valid)throw new InvalidDataException(message);}
        static bool Number(double n,double max=double.MaxValue)=>double.IsFinite(n)&&n>=0&&n<=max;
        static bool Text(string? value,int max=512)=>value!=null&&value.Length<=max;
        var creatures=s.Creatures.ToDictionary(c=>c.Id,StringComparer.Ordinal);
        Require(s.FamilySequence>=0&&s.CompletedBirths!=null&&s.CompletedBirths.Count<=10000&&
            s.CompletedBirths.All(id=>!string.IsNullOrWhiteSpace(id)&&id.Length<=128),"出生结算记录无效");
        Require(s.CourtshipTools is >=0 and <=99&&s.CourtshipToolPurchases>=0,"繁育诱导用品库存无效");
        // Check every structure before following any links to another individual.
        foreach(var c in s.Creatures)
        {
            var f=c.Family;
            Require(f!=null&&f.Peers!=null&&f.PartnerIds!=null&&f.CaregiverIds!=null&&f.Pregnancy!=null&&
                f.ColdStorage!=null&&f.LifeKnowledge!=null&&f.Conflict!=null&&f.ChildThreat!=null&&f.KnownLosses!=null,"家庭数据缺失");
            Require(Number(f.BreedingInterest,100)&&Number(f.InducedInterestRemaining,90)&&Number(f.NextInductionAt)&&
                double.IsFinite(f.LastInterestEventAt)&&f.LastInterestEventAt>=-100&&f.LastInterestEventAt<=s.WorldSeconds+.001,"繁育兴趣或诱导状态无效");
            Require(c.Interaction!=null,"身体数据缺失");
            BodyInjuryState.Validate(c.Interaction.Injuries,s.WorldSeconds);
            WildSelfCareRules.Validate(c,s);
            Require(Number(c.Interaction.FootIntegrity,100)&&c.Interaction.FootIntegrity<=100-c.Interaction.Injuries.PermanentFootLoss+.00001,
                "底部支撑超过永久损伤上限");
            Require(f.Peers.Count<=creatures.Count&&f.Peers.All(pair=>pair.Key!=c.Id&&creatures.ContainsKey(pair.Key)&&pair.Value!=null&&
                Number(pair.Value.Familiarity,100)&&Number(pair.Value.Affection,100)&&Number(pair.Value.Fear,100)&&
                Number(pair.Value.Resentment,100)&&Number(pair.Value.CareSeconds,1000000)&&Text(pair.Value.LastEvent)),"同伴关系数值或引用无效");
            Require(f.CaregiverIds.Count<=creatures.Count&&f.CaregiverIds.Distinct().Count()==f.CaregiverIds.Count&&
                f.CaregiverIds.All(id=>id!=c.Id&&creatures.ContainsKey(id)),"照护者引用无效");
            Require(Number(f.Warmth,100)&&Number(f.NextCareAt)&&Number(f.NextSocialAt)&&Number(f.NextBreedingDecisionAt)&&
                Number(f.CareApproachSeconds)&&Number(f.CareBestDistance)&&Text(f.LastEvent)&&Text(f.CareLastEvent)&&Enum.IsDefined(f.BirthMethod),"育幼状态无效");
            Require(f.LifeKnowledge.Count<=32&&f.LifeKnowledge.All(k=>!string.IsNullOrWhiteSpace(k.Key)&&k.Key.Length<=128),"生活常识记录无效");
            bool Reference(string? id)=>id==null||id!=c.Id&&creatures.ContainsKey(id);
            Require(Reference(f.MotherId)&&Reference(f.FatherId)&&Reference(f.CareTargetId)&&
                (f.MotherId==null||f.FatherId==null||f.MotherId!=f.FatherId),"亲缘或照护目标无效");
            // Historical parenthood belongs to the continuous identity. A later
            // body transfer may change today's role without rewriting that fact (v033).
            Require(f.BirthEventId==null||s.CompletedBirths.Contains(f.BirthEventId)&&f.MotherId!=null&&f.FatherId!=null,
                "幼体出生缺少结算或亲缘记录");
            var p=f.Pregnancy;
            Require(Enum.IsDefined(p.Phase)&&Enum.IsDefined(p.Method)&&Number(p.Elapsed,GestationSeconds(p.Method))&&
                Number(p.WaitingSeconds,90)&&Number(p.Burden,100)&&Number(p.RecoveryRemaining,900)&&
                Number(p.RetryAt)&&p.PlannedChildren is >=0 and <=3&&Text(p.Outcome)&&
                Reference(p.FatherId)&&(p.EventId==null||!string.IsNullOrWhiteSpace(p.EventId)&&p.EventId.Length<=128),"孕育数据无效");
            if(p.Phase is PregnancyPhase.Preparing or PregnancyPhase.Gestating)
                Require(!c.IsYoung&&c.Body.ReproductionRole==ReproductionRole.Maternal&&p.FatherId!=null&&
                    !creatures[p.FatherId].IsYoung&&creatures[p.FatherId].Body.ReproductionRole==ReproductionRole.Paternal,"孕育缺少成熟角色来源");
            if(p.Phase==PregnancyPhase.Preparing)
                Require(p.EventId==null&&p.PlannedChildren==0&&p.Elapsed<=6.001,"准备期提前生成了出生数据");
            if(p.Phase==PregnancyPhase.Gestating)
                Require(p.EventId!=null&&!s.CompletedBirths.Contains(p.EventId)&&
                    (p.Method==BirthMethod.Live?p.PlannedChildren==1:p.PlannedChildren is 2 or 3),"孕育数量或结算状态无效");
            var cold=f.ColdStorage;
            // Null is the legacy representation. World construction initializes
            // only missing active broods; querying status never repairs a save.
            if(p.Stem is { } stem)
                Require(p.Method==BirthMethod.Stem&&p.EventId!=null&&stem.EventId==p.EventId&&
                    p.Phase!=PregnancyPhase.Preparing&&Number(stem.Integrity,100)&&
                    (p.Phase!=PregnancyPhase.Gestating||stem.Integrity>0)&&Number(stem.Hydration,100)&&
                    Number(stem.SupportRemaining,60)&&double.IsFinite(stem.LastCareAt)&&stem.LastCareAt>=-100&&
                    stem.LastCareAt<=s.WorldSeconds+.001&&double.IsFinite(stem.LastDamageAt)&&stem.LastDamageAt>=-100&&
                    stem.LastDamageAt<=s.WorldSeconds+.001&&Text(stem.LastDamageCause)&&
                    (stem.LastDamageActorId==null||stem.LastDamageActorId=="player"||creatures.ContainsKey(stem.LastDamageActorId)),"外茎照护状态无效");
            Require(Number(cold.Elapsed,1000000)&&Number(cold.RecoveryRemaining,30)&&cold.Sessions>=0,"冷藏记录无效");
            if(cold.Stored)
                Require(s.InteractionPlaytest&&cold.RecoveryRemaining==0&&!c.Interaction.Carried&&!c.Interaction.Falling&&
                    c.Interaction.Height<=.05&&c.Interaction.Restraint==RestraintKind.None&&!c.Interaction.Physiology.Active&&
                    c.Interaction.EatingRemaining==0&&f.CareParcelId==null,"冷藏与正在进行的身体动作冲突");
        }
        Require(s.Creatures.Count(c=>c.Family.ColdStorage.Stored)<=4,"冷藏容量无效");
        var pregnancies=s.Creatures.Where(c=>c.Family.Pregnancy.EventId!=null).Select(c=>c.Family.Pregnancy.EventId).ToArray();
        Require(pregnancies.Distinct().Count()==pregnancies.Length,"孕育事件重复归属");
        // Iterative ancestry validation avoids recursion over corrupted deep saves.
        var children=creatures.Keys.ToDictionary(id=>id,_=>new List<string>(),StringComparer.Ordinal);
        var incoming=creatures.Keys.ToDictionary(id=>id,_=>0,StringComparer.Ordinal);
        foreach(var c in s.Creatures)
            foreach(var id in new[]{c.Family.MotherId,c.Family.FatherId}.Where(id=>id!=null).Distinct())
            {children[id!].Add(c.Id);incoming[c.Id]++;}
        var queue=new Queue<string>(incoming.Where(p=>p.Value==0).Select(p=>p.Key));int visited=0;
        while(queue.TryDequeue(out var id))
        {visited++;foreach(var child in children[id])if(--incoming[child]==0)queue.Enqueue(child);}
        Require(visited==creatures.Count,"亲缘记录形成循环");
        if(s.InteractionPlaytest)
        {
            var objects=s.RoomObjects.ToDictionary(o=>o.Id,StringComparer.Ordinal);
            foreach(var c in s.Creatures)
                Require(c.Family.CareParcelId==null||objects.TryGetValue(c.Family.CareParcelId,out var parcel)&&
                    parcel.CarrierId==c.Id&&c.Family.CareTargetId!=null,"搬运任务缺少对应实物");
            foreach(var o in s.RoomObjects)
                Require(o.CarrierId==null||creatures.TryGetValue(o.CarrierId,out var carrier)&&
                    carrier.Family.CareParcelId==o.Id&&carrier.RegionId==o.RegionId&&!o.Stored&&!o.HeldByHand&&o.Portions==1&&
                    o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water,"食水搬运归属无效");
        }
        ValidateSocial(s);
        ValidateAftermath(s);
        ValidateRisk(s);
        ValidateLearning(s);
    }
}

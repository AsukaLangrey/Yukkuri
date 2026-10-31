namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public const int StemVesselPrice=20;
    public const double StemVesselRadius=.38;
    public bool BuyStemVessel()
    {
        if(State.InteractionPlaytest)return BuyShopProduct("vessel");
        if(!State.InteractionPlaytest||State.StemVessels>=99||!State.UnlimitedResources&&State.Money<StemVesselPrice)return false;
        if(!State.UnlimitedResources)State.Money-=StemVesselPrice;
        State.StemVessels++;State.StemVesselPurchases++;
        State.LastActionMessage="购入一个培养瓶；装瓶后仍需实际补水和补充营养。";return true;
    }
    private DetachedStemBrood? DetachedStem(string eventId)=>State.DetachedStemBroods.Find(s=>s.EventId==eventId);
    private static bool LivingDetachedStem(DetachedStemBrood s)=>(s.Phase is DetachedStemPhase.Loose or DetachedStemPhase.Bottled)&&s.Integrity>0;
    private string? DetachedStemError(DetachedStemBrood? s,bool allowTerminal=false)
    {
        if(!State.InteractionPlaytest||s==null||s.RegionId!=State.ActiveRegionId)return "先对准当前小院里真实存在的离体茎。";
        if(State.Settings.Pause)return "先恢复时间，再照护离体茎。";
        if(State.Mode==PlayMode.CloseUp)return "先回到小院，再操作独立的培养瓶。";
        if(s.Cleared)return "实体已经收起，原来的生命记录仍然保留。";
        if(!allowTerminal&&!LivingDetachedStem(s))return s.Phase==DetachedStemPhase.Born?"这窝已经出生，不再重复结算。":"这窝已经失去活性，不能靠补水重新复活。";
        return null;
    }
    public DetachedStemView? DetachedStemStatus(string eventId)
    {
        var s=DetachedStem(eventId);return s==null?null:DescribeDetachedStem(s);
    }
    private DetachedStemView DescribeDetachedStem(DetachedStemBrood s)
    {
        var error=DetachedStemError(s);bool free=error==null&&!s.HeldByHand&&
            !State.Creatures.Any(c=>c.Interaction.Carried)&&!State.DetachedStemBroods.Any(b=>b.HeldByHand)&&!State.SupplyStations.Any(station=>station.HeldByHand);
        bool vessel=s.Phase==DetachedStemPhase.Bottled&&s.VesselIntegrity>25;
        return new(s.EventId,s.Mother.Id,s.Father.Id,s.RegionId,s.X,s.Z,s.Phase,s.PlannedChildren,
            s.Elapsed/FamilyRules.GestationSeconds(BirthMethod.Stem),s.Integrity,s.Hydration,s.Nutrition,s.VesselIntegrity,s.HeldByHand,
            free&&!vessel&&State.StemVessels>0&&!State.RoomObjects.Any(o=>o.HeldByHand)&&
            CanPlaceDetachedStem(s.RegionId,s.X,s.Z,s.EventId),free&&vessel&&s.Hydration<95,
            free&&vessel&&s.Hydration>=10&&s.Nutrition<95,error,s.LastDamageCause,s.Cleared,
            DetachedStemError(s,allowTerminal:true)==null&&(s.Phase is DetachedStemPhase.Failed or DetachedStemPhase.Born)&&
            !State.Creatures.Any(c=>c.Interaction.Carried)&&!State.RoomObjects.Any(o=>o.HeldByHand)&&!State.DetachedStemBroods.Any(b=>b.HeldByHand)&&!State.SupplyStations.Any(station=>station.HeldByHand),
            error==null&&DetachedStemActionError(s,new(InteractionAction.UnbottleStem))==null);
    }
    public IReadOnlyList<DetachedStemView> DetachedStems(string regionId)=>State.DetachedStemBroods
        .Where(s=>s.RegionId==regionId).Select(DescribeDetachedStem).ToArray();

    public bool CanPlaceDetachedStemAt(string eventId,double x,double z)=>DetachedStem(eventId) is { } s&&
        DetachedStemError(s)==null&&CanPlaceDetachedStem(s.RegionId,x,z,eventId);
    private bool CanPlaceDetachedStem(string regionId,double x,double z,string? ignore=null)
    {
        const double radius=StemVesselRadius;
        if(!ClearOfStations(regionId,x,z,radius))return false;
        if(!double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)>8-radius||Math.Abs(z)>8-radius||
            !WoodlandLayout.GroundFits(State,regionId,x,z,radius))return false;
        if(State.Creatures.Any(c=>c.RegionId==regionId&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&
            !(c.Interaction.Height>1&&c.Interaction.Restraint==RestraintKind.None)&&Distance(c.X,c.Z,x,z)<InteractionFootprint(c)+radius))return false;
        if(State.DetachedStemBroods.Any(s=>s.EventId!=ignore&&s.RegionId==regionId&&!s.Cleared&&s.Phase!=DetachedStemPhase.Born&&
            Distance(s.X,s.Z,x,z)<radius*2))return false;
        return !State.RoomObjects.Any(o=>!o.Stored&&!o.HeldByHand&&o.RegionId==regionId&&
            (o.Kind==RoomObjectKind.Cushion?Math.Abs(o.X-x)<InteractionFixtureGeometry.CushionWidth*.5+radius&&
                Math.Abs(o.Z-z)<InteractionFixtureGeometry.CushionDepth*.5+radius:
                o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water&&Distance(o.X,o.Z,x,z)<radius+.2));
    }
    private bool FindDetachedStemLanding(Creature mother,out double x,out double z)
    {
        for(double radius=1.4;radius<=3;radius+=.35)
            for(int angle=0;angle<24;angle++)
            {
                x=mother.X+Math.Cos(angle*Math.Tau/24)*radius;z=mother.Z+Math.Sin(angle*Math.Tau/24)*radius;
                if(CanPlaceDetachedStem(mother.RegionId,x,z)&&HasGroundSightLine(mother,x,z))return true;
            }
        x=mother.X;z=mother.Z;return false;
    }
    private InteractionFeedback CutAttachedStem(Creature mother)
    {
        var p=mother.Family.Pregnancy;var stem=AttachedStem(mother);
        var father=State.Creatures.Find(c=>c.Id==p.FatherId);
        if(stem==null||father==null||State.DetachedStemBroods.Any(s=>s.EventId==p.EventId))return Unavailable("这窝的来源或归属已经改变，不能重复剪离。");
        if(!FindDetachedStemLanding(mother,out double x,out double z))return Unavailable("母体旁没有可以接住这一窝的干燥空位，先腾出空间。");
        DamageAttachedStem(mother,3,"人类剪断外茎","player",direct:true);
        if(AttachedStem(mother)==null)return Feedback(mother,"stem-loss",Expression.Frightened,p.Outcome,"stem-loss");
        var detached=new DetachedStemBrood{EventId=p.EventId!,Mother=StemParentSource.Capture(mother),Father=StemParentSource.Capture(father),
            RegionId=mother.RegionId,X=x,Z=z,PlannedChildren=p.PlannedChildren,Elapsed=p.Elapsed,Burden=p.Burden,
            Integrity=stem.Integrity,Hydration=stem.Hydration,Nutrition=mother.Fullness*.5,DetachedAt=State.WorldSeconds,
            LastDamageAt=stem.LastDamageAt,LastDamageCause=stem.LastDamageCause,LastDamageActorId=stem.LastDamageActorId,
            LastEvent="整窝外茎已剪离，暂放在母体旁的干燥空地，需要及时移入培养瓶。"};
        State.DetachedStemBroods.Add(detached);p.Stem=null;
        EndPregnancy(mother,"外茎被剪离，整窝仍在独立存活；母体进入恢复期，需要安慰与照护。");
        // Separation has a systemic recovery cost, not an invented Head wound.
        Hurt(mother,2,"外茎剪离后的身体负担",null);
        return Feedback(mother,"stem-cut",Expression.Frightened,detached.LastEvent,"stem-cut");
    }
    private void TickDetachedStems(double dt)
    {
        foreach(var s in State.DetachedStemBroods)
        {
            if(s.Phase is DetachedStemPhase.Failed or DetachedStemPhase.Born)continue;
            if(s.Integrity<=0){FailDetachedStem(s,s.LastDamageCause.Length>0?s.LastDamageCause:"离体茎已经耗尽活性");continue;}
            bool vessel=s.Phase==DetachedStemPhase.Bottled&&s.VesselIntegrity>25;
            bool wet=WoodlandLayout.HasRiver(State,s.RegionId)&&s.X>WoodlandLayout.RiverEdge(s.Z)&&!s.HeldByHand;
            s.Hydration=Math.Max(0,s.Hydration-dt*(wet?.8:vessel?.22:.65));
            s.Nutrition=Math.Max(0,s.Nutrition-dt*(vessel?.16:.25));
            if(wet)DamageDetachedStem(s,dt*1.2,"离体茎浸入河水",null,false);
            else if(!vessel)DamageDetachedStem(s,dt*.1,s.Phase==DetachedStemPhase.Bottled?"培养瓶破损后漏水失养":"离体茎尚未入瓶，持续失水",null,false);
            if(!LivingDetachedStem(s))continue;
            if(s.Hydration<20||s.Nutrition<20)DamageDetachedStem(s,dt*.5,s.Hydration<20?"离体茎持续缺水":"离体茎持续缺乏营养",null,false);
            if(!LivingDetachedStem(s))continue;
            bool nourished=vessel&&!wet&&s.Hydration>=20&&s.Nutrition>=20;
            s.Elapsed=Math.Min(FamilyRules.GestationSeconds(BirthMethod.Stem),s.Elapsed+dt*(nourished?1:.25));
            s.Burden=Math.Clamp(s.Burden+dt*(nourished?-.045:.12),0,100);
            if(s.Elapsed<FamilyRules.GestationSeconds(BirthMethod.Stem)||!nourished||s.HeldByHand)continue;
            var mother=State.Creatures.Find(c=>c.Id==s.Mother.Id);var father=State.Creatures.Find(c=>c.Id==s.Father.Id);
            if(mother==null||father==null){FailDetachedStem(s,"父母身份记录缺失，无法建立真实家谱");continue;}
            var birth=new PregnancyState{EventId=s.EventId,Method=BirthMethod.Stem,PlannedChildren=s.PlannedChildren,Elapsed=s.Elapsed,Burden=s.Burden};
            if(!TryCreateFamilyChildren(mother,father,birth,s.RegionId,s.X,s.Z,s.Mother,s.Father))
            {s.LastEvent="这一窝已经成熟，需要培养瓶旁边有足够的干燥空位置。";continue;}
            s.Phase=DetachedStemPhase.Born;s.LastEvent=$"这窝{ s.PlannedChildren }只宝宝已经在培养瓶旁出生，需要实际吃喝和照护。";
            // A dead or newly pregnant mother keeps her own present state. Birth
            // does not impose a second separation recovery or body injury.
            NotifyDetachedStemWitnesses(s,s.LastEvent,false,2);
        }
    }
    private void DamageDetachedStem(DetachedStemBrood s,double amount,string cause,string? actor,bool damageVessel)
    {
        if(!LivingDetachedStem(s)||amount<=0)return;
        amount=Math.Min(amount,s.Integrity);s.Integrity=Math.Max(0,s.Integrity-amount);
        if(damageVessel&&s.Phase==DetachedStemPhase.Bottled)s.VesselIntegrity=Math.Max(0,s.VesselIntegrity-amount*2);
        s.LastDamageAt=State.WorldSeconds;s.LastDamageCause=cause;s.LastDamageActorId=actor;
        if(actor=="player")NotifyDetachedStemWitnesses(s,cause,true,amount);
        if(s.Integrity<=0)FailDetachedStem(s,cause);
    }
    private void FailDetachedStem(DetachedStemBrood s,string cause)
    {
        s.Integrity=0;s.Phase=DetachedStemPhase.Failed;s.HeldByHand=false;
        s.LastDamageCause=cause;s.LastDamageAt=State.WorldSeconds;s.LastEvent="这窝离体茎未能存活。原因："+cause+"。";
        // Seeing a failed brood is not knowledge of a past unseen cause.
        NotifyDetachedStemWitnesses(s,"眼前的这一窝离体茎已经失去活性，没能继续发育。",false,4);
    }
}

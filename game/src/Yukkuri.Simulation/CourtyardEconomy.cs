using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public enum CreatureDepartureKind { Sale, Release, Destroy }
public sealed record CreatureSale(Creature Creature, int Amount, double At)
{
    public CreatureDepartureKind Kind { get; init; } = CreatureDepartureKind.Sale;
}
public sealed partial class WorldState
{
    public long CreatureSalesRevenue { get; set; }
    // Legacy yards keep their already open catalogue; new economy starts use progression.
    public bool EconomyProgression { get; set; }
    public List<CreatureSale> CreatureSales { get; set; } = new();
    [JsonIgnore] public IEnumerable<Creature> KnownCreatures => Creatures.Concat(CreatureSales.Select(s=>s.Creature));
}
public sealed record CreatureQuote(double Quality,double Ability,double AgeValue,double CoreValue,
    double Clean,double Spirit,double Health,double Food,double Water,int Price);

public static class CourtyardEconomy
{
    // Temperament is inherited; learned ability and registration are not genetic.
    public static double Quality(Creature c)=>Math.Clamp(100*(c.Personality.Kindness*.4+
        c.Patience*.25+(1-c.Personality.Carelessness)*.2+(1-c.Aggression)*.15),0,100);
    public static double Ability(Creature c)
    {
        double completed=c.Interaction.Development.Skills.Values.Sum(s=>Math.Min(10,s.Completed));
        return Math.Clamp(completed*2,0,100);
    }
    public static CreatureQuote Quote(Creature c)
    {
        double quality=Quality(c),ability=Ability(c);
        double age=100-40*Math.Clamp(c.Body.AgeSeconds/GrowthRules.AdultAge,0,1);
        double rank=WorldSimulation.CleaningLicenseLevel(c) switch {1=>1.5,2=>2.3,>=3=>3.5,_=>1};
        double core=age*(.5+quality/100*1.5)*(1+ability/100)*rank;
        double clean=.95+.1*(1-c.Interaction.Dirt/100);
        double spirit=.95+.1*Math.Clamp((c.Interaction.Emotion.Manmanlai-(c.Fear+c.Resentment+c.Interaction.Emotion.Alarm)/3)/100,0,1);
        double health=.5+.5*c.Health/100;
        double food=c.Fullness>=25?1:.5+.5*c.Fullness/25;
        double water=c.Interaction.Hydration>=25?1:.5+.5*c.Interaction.Hydration/25;
        int price=(int)Math.Floor(core*clean*spirit*health*food*water);
        return new(quality,ability,age,core,clean,spirit,health,food,water,Math.Max(1,price));
    }
    public static long UnlockRevenue(string id)=>id switch
    {
        "food-station" or "water-station"=>200,
        "shelter" or "sewing-kit"=>500,
        "inducer" or "freezer" or "frame" or "hanger"=>1000,
        _=>0
    };
    public static void Validate(WorldState s)
    {
        if(s.CreatureSales==null||s.CreatureSales.Count>10000||s.CreatureSalesRevenue<0)
            throw new InvalidDataException("售油账目无效");
        var ids=s.Creatures.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        var bodies=s.Creatures.Select(c=>c.Body.Id).ToHashSet(StringComparer.Ordinal);
        var identities=s.Creatures.Select(c=>c.Registration.IdentityId).ToHashSet(StringComparer.Ordinal);
        long total=0;
        foreach(var sale in s.CreatureSales)
        {
            var c=sale?.Creature;
            if(c==null||string.IsNullOrWhiteSpace(c.Id)||!ids.Add(c.Id)||string.IsNullOrWhiteSpace(c.Name)||
                c.Body==null||c.Family==null||c.Personality==null||c.Registration==null||
                string.IsNullOrWhiteSpace(c.Body.Id)||!bodies.Add(c.Body.Id)||
                string.IsNullOrWhiteSpace(c.Registration.IdentityId)||!identities.Add(c.Registration.IdentityId)||!Enum.IsDefined(sale!.Kind)||
                (sale.Kind==CreatureDepartureKind.Sale?sale.Amount<=0:sale.Amount!=0)||
                !double.IsFinite(sale.At)||sale.At<0||sale.At>s.WorldSeconds)
                throw new InvalidDataException("售油离场档案无效");
            total+=sale.Amount;
        }
        if(total!=s.CreatureSalesRevenue)throw new InvalidDataException("售油收入与账目不符");
        foreach(var sale in s.CreatureSales)
        {
            var c=sale.Creature;
            foreach(var parent in new[]{c.Family.MotherId,c.Family.FatherId})
                if(parent!=null&&(parent==c.Id||!ids.Contains(parent)))throw new InvalidDataException("离场亲缘引用无效");
        }
    }
}

public sealed partial class WorldSimulation
{
    public static WorldSimulation CreateCourtyardEconomy(ulong seed=20261028)
    {
        var sim=CreateInteractionPlaytest(seed);var s=sim.State;s.EconomyProgression=true;
        // A documented starting household, not a retroactive change to existing saves.
        var mother=s.Creatures[0];var father=s.Creatures[1];
        foreach(var c in new[]{mother,father})
        {
            c.Body.AgeSeconds=FamilyRules.YoungSeconds;c.Fullness=90;c.Interaction.Hydration=90;
            c.Family.BreedingInterest=65;c.Family.NextBreedingDecisionAt=20;
        }
        mother.Family.Peers[father.Id]=new(){Familiarity=80,Affection=80};
        father.Family.Peers[mother.Id]=new(){Familiarity=80,Affection=80};
        mother.Family.PartnerIds.Add(father.Id);father.Family.PartnerIds.Add(mother.Id);
        s.FamilyRelationshipEvents.Add(new(){Id="economy-starting-pair",FirstId=mother.Id,SecondId=father.Id});
        father.Z=father.TargetZ=.5;
        foreach(var cushion in s.RoomObjects.Where(o=>o.Kind==RoomObjectKind.Cushion))
        {cushion.X=cushion.X<0?-4.3:4.3;cushion.Z=-2;}
        if(s.RoomObjects.Find(o=>o.Id=="home-leaves-north") is { } leaves){leaves.X=3;leaves.Z=-4.8;}
        s.ShelterStock=1;
        if(!sim.PlaceShelter(-.3,-5).Success)throw new InvalidOperationException("默认小屋的位置不可用。");
        s.LastActionMessage="开局已备一间最多住5只的小屋。小铃和松果是伴侣；照顾吃喝、夜间住宿和繁育，商店可出售各成长阶段的油。";
        return sim;
    }
    private double InheritTrait(double mother,double father)=>Math.Clamp((mother+father)*.5+(Next()+Next()-1)*.2,0,1);
    public string? ShopUnlockReason(string id)=>State.EconomyProgression&&State.CreatureSalesRevenue<CourtyardEconomy.UnlockRevenue(id)
        ?$"累计售油收入达到 {CourtyardEconomy.UnlockRevenue(id)} 元解锁（当前 {State.CreatureSalesRevenue} 元）。":null;

    public string? CreatureSaleReason(string id,bool fromColdStorage=false)
    {
        var c=State.Creatures.Find(c=>c.Id==id);
        if(!State.InteractionPlaytest||c==null||c.RegionId!=State.ActiveRegionId&&!(fromColdStorage&&c.Family.ColdStorage.Stored))return "请选择当前小院的油。";
        if(c.Condition==PhysicalCondition.Dead||c.Family.Memorialized)return "商店只收购存活的油。";
        if(HasInteractionGesture||c.Interaction.Carried||c.Interaction.Falling||c.Interaction.Restraint!=RestraintKind.None||
            c.Family.ColdStorage.Stored&&!fromColdStorage||c.Interaction.EatingRemaining>0||c.Interaction.Physiology.Active||
            State.PortableMotions.Any(m=>m.Id==id))return "请先结束接触、进食或装置操作，将它安稳放下。";
        if(c.Family.Pregnancy.Phase!=PregnancyPhase.None||State.Creatures.Any(m=>m.Family.Pregnancy.FatherId==id&&
            m.Family.Pregnancy.Phase is PregnancyPhase.Preparing or PregnancyPhase.Gestating)||
            State.DetachedStemBroods.Any(s=>(s.Mother.Id==id||s.Father.Id==id)&&s.Phase is DetachedStemPhase.Loose or DetachedStemPhase.Bottled))
            return "请等待这次孕育与恢复结束，再出售相关亲油。";
        if(State.CreatureSales.Count>=10000)return "售油档案已满。";
        if((long)State.Money+CourtyardEconomy.Quote(c).Price>int.MaxValue)return "余额已达上限。";
        return null;
    }

    public string SellCreatureToShop(string id,int expectedPrice,bool fromColdStorage=false)
    {
        if(State.Settings.Pause)return "先恢复时间，再出售。";
        if(fromColdStorage&&!State.Creatures.Any(c=>c.Id==id&&c.Family.ColdStorage.Stored))return "对象已不在回收箱。";
        if(CreatureSaleReason(id,fromColdStorage) is { } reason)return reason;
        var c=State.Creatures.Single(c=>c.Id==id);var quote=CourtyardEconomy.Quote(c);
        if(quote.Price!=expectedPrice)return "状态变化使报价更新，请查看新报价后再次确认。";
        CompleteCreatureDeparture(c,quote.Price,CreatureDepartureKind.Sale);
        State.Money+=quote.Price;State.CreatureSalesRevenue+=quote.Price;
        if(State.SelectedId==id){State.SelectedId=State.Creatures.FirstOrDefault(o=>o.RegionId==State.ActiveRegionId)?.Id;State.Mode=PlayMode.Management;}
        State.LastActionMessage=$"{c.Name}已出售给商店，到账 {quote.Price} 元。";
        Log(State.LastActionMessage);return State.LastActionMessage;
    }
    private void CompleteCreatureDeparture(Creature c,int amount,CreatureDepartureKind kind)
    {
        string id=c.Id;
        ClearCompanionship(c);CancelFamilyCare(c);DetachNaturalIntake(c);
        foreach(var other in State.Creatures)
        {
            var f=other.Family;var d=other.Interaction.Development;
            if(f.CareTargetId==id)CancelFamilyCare(other,reason:"照护对象已出售，已取食水留在实际位置。");
            if(f.Lesson?.LearnerId==id)f.Lesson=null;
            if(f.Conflict.TargetId==id)StopFamilyConflict(other,"对方已离场，冲突结束。");
            if(f.ChildThreat.TargetId==id)f.ChildThreat=new();
            f.CaregiverIds.Remove(id);f.PartnerIds.Remove(id);
            if(d.RecipientId==id){d.Duty=LifeDuty.None;d.RecipientId=null;}
            if(d.CleaningOrderedById==id){d.CleaningOrderedById=null;d.OrderedWasteId=null;d.CleaningOrderUntil=0;}
            if(other.Personality.HungerTargetId==id)other.Personality.HungerTargetId=null;
            if(other.Personality.HungerFeeding.TargetId==id)other.Personality.HungerFeeding=new();
            if(other.Personality.WaterAftercare.TargetId==id)ClearWaterAftercare(other);
            if(other.Personality.WaterConcernId==id){other.Personality.WaterConcernId=null;other.Personality.WaterConcernEventId=null;}
        }
        foreach(var e in State.FamilyRelationshipEvents.Where(e=>e.EndedAt==null&&(e.FirstId==id||e.SecondId==id)))e.EndedAt=State.WorldSeconds;
        foreach(var shelter in State.Shelters.Where(s=>s.GuestId==id))
        {shelter.GuestId=null;shelter.Phase=ShelterVisitPhase.Empty;shelter.ExitRequested=false;shelter.BlockedSeconds=0;}
        foreach(var shelter in State.Shelters)shelter.Residents.RemoveAll(r=>r.CreatureId==id);
        foreach(var o in State.RoomObjects.Where(o=>o.OwnerId==id))o.OwnerId=null;
        if(State.HelpTargetId==id)State.HelpTargetId=null;
        if(State.Threat.IntruderId==id)State.Threat.IntruderId=null;
        if(State.Threat.DefenderId==id)State.Threat.DefenderId=null;
        c.Family.Lesson=null;c.Family.PartnerIds.Clear();
        c.Family.ColdStorage.Stored=false;
        State.Creatures.Remove(c);State.CreatureSales.Add(new(c,amount,State.WorldSeconds){Kind=kind});
    }

}

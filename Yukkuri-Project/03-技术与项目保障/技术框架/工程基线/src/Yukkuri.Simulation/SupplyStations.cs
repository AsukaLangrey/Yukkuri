namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public const int FoodStationPrice=35,WaterStationPrice=30,FoodStationCapacity=8,WaterStationCapacity=12;
    public const int StationWaterBundlePrice=6,StationWaterBundleCount=12;
    public const double SupplyStationRadius=.40,SupplyStationOutletOffset=.85;
    private static int StationCapacity(SupplyStationState s)=>s.Kind==SupplyStationKind.Food?FoodStationCapacity:WaterStationCapacity;
    private static RoomObjectKind StationFoodKind(SupplyStationState s)=>s.Kind==SupplyStationKind.Food?RoomObjectKind.Meal:RoomObjectKind.Water;
    private SupplyStationState? Station(string id)=>State.SupplyStations.Find(s=>s.Id==id);
    private RoomObject? StationOutlet(SupplyStationState s)=>State.RoomObjects.Find(o=>o.Id==s.OutletObjectId&&
        !o.Stored&&!o.HeldByHand&&o.CarrierId==null&&o.RegionId==s.RegionId&&o.Kind==StationFoodKind(s)&&
        Distance(o.X,o.Z,s.X,s.Z+SupplyStationOutletOffset)<.001);
    private int StationOutletPortions(SupplyStationState s)=>StationOutlet(s) is { } o?
        o.Portions+State.Creatures.Count(c=>c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeSourceId==o.Id):0;
    private bool StationOutletBusy(SupplyStationState s)=>StationOutlet(s) is { } o&&
        (NaturalResources.Consumer(State,o)!=null||State.Creatures.Any(c=>c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeSourceId==o.Id));
    private string? StationWorldError()=>!State.InteractionPlaytest||string.IsNullOrWhiteSpace(State.ActiveRegionId)||
        !State.Regions.Any(r=>r.Id==State.ActiveRegionId)?"先回到有效的小院。":
        State.Settings.Pause?"先恢复时间，再操作供给器。":State.Mode==PlayMode.CloseUp?"先退出特写，再操作供给器。":null;
    private bool StationHandOccupied()=>State.Creatures.Any(c=>c.Interaction.Carried)||State.RoomObjects.Any(o=>o.HeldByHand)||
        State.DetachedStemBroods.Any(s=>s.HeldByHand)||State.SupplyStations.Any(s=>s.HeldByHand);
    private string? StationError(SupplyStationState? s)=>StationWorldError()??(s==null||s.RegionId!=State.ActiveRegionId?"这台供给器不在当前小院。":null);
    private string? StationActionError(SupplyStationState s,InteractionAction action)
    {
        if(StationError(s) is { } error)return error;
        if(StationHandOccupied())return "先把手里的实际对象放稳。";
        if(action==InteractionAction.Lift)return StationOutletBusy(s)?"有油正在接取或吞咽出口这一份，等它吃完再搬。":null;
        if(action!=InteractionAction.RefillSupplyStation)return "这个工具不能用于供给器。";
        if(s.StoredPortions+StationOutletPortions(s)>=StationCapacity(s))return "供给器已经装满，出口尚未入口的一份也计入容量。";
        if((s.Kind==SupplyStationKind.Food?State.Food:State.StationWaterPortions)<=0)
            return s.Kind==SupplyStationKind.Food?"食物库存不足，先购买食物。":"批量清水不足，先购买补充清水。";
        return null;
    }
    public SupplyStationView? SupplyStationStatus(string id)=>Station(id) is { } s?DescribeStation(s):null;
    public IReadOnlyList<SupplyStationView> SupplyStations(string? regionId=null)=>State.SupplyStations
        .Where(s=>regionId==null||s.RegionId==regionId).Select(DescribeStation).ToArray();
    private SupplyStationView DescribeStation(SupplyStationState s)
    {
        string? refill=StationActionError(s,InteractionAction.RefillSupplyStation);
        return new(s.Id,s.Kind,s.RegionId,s.X,s.Z,StationCapacity(s),s.StoredPortions,StationOutletPortions(s),
            StationOutlet(s)?.Id,s.X,s.Z+SupplyStationOutletOffset,s.HeldByHand,refill==null,
            StationActionError(s,InteractionAction.Lift)==null,refill,s.LastEvent);
    }
    public bool BuySupplyStation(SupplyStationKind kind)
    {
        if(State.InteractionPlaytest)return Enum.IsDefined(kind)&&BuyShopProduct(kind==SupplyStationKind.Food?"food-station":"water-station");
        if(State.InteractionPlaytest)return Enum.IsDefined(kind)&&BuyShopProduct(kind==SupplyStationKind.Food?"food-station":"water-station");
        if(!State.InteractionPlaytest||!Enum.IsDefined(kind))return false;
        int stock=kind==SupplyStationKind.Food?State.FoodStationStock:State.WaterStationStock;
        int price=kind==SupplyStationKind.Food?FoodStationPrice:WaterStationPrice;
        if(stock>=99||!State.UnlimitedResources&&State.Money<price)return false;
        if(!State.UnlimitedResources)State.Money-=price;
        if(kind==SupplyStationKind.Food)State.FoodStationStock++;else State.WaterStationStock++;
        Log("购入一台空的"+(kind==SupplyStationKind.Food?"食物供给器。":"饮水器。"));return true;
    }
    public bool BuyStationWater()
    {
        if(State.InteractionPlaytest)return BuyShopProduct("water");
        if(State.InteractionPlaytest)return BuyShopProduct("water");
        if(!State.InteractionPlaytest||State.StationWaterPortions>int.MaxValue-StationWaterBundleCount||
            !State.UnlimitedResources&&State.Money<StationWaterBundlePrice)return false;
        if(!State.UnlimitedResources)State.Money-=StationWaterBundlePrice;
        State.StationWaterPortions+=StationWaterBundleCount;Log($"购入 {StationWaterBundleCount} 份供给器清水。");return true;
    }
    private bool StationIdentityUsed(string id)=>State.SupplyStations.Any(s=>s.Id==id)||State.RoomObjects.Any(o=>o.Id==id)||
        State.Creatures.Any(c=>c.Id==id)||State.DetachedStemBroods.Any(s=>s.EventId==id)||State.Shelters.Any(s=>s.Id==id);
    public InteractionFeedback PlaceSupplyStation(SupplyStationKind kind,double x,double z)
    {
        if(StationWorldError() is { } error)return Unavailable(error);
        if(!Enum.IsDefined(kind)||StationHandOccupied())return Unavailable("器具种类无效，或手里还有其他东西。");
        if((kind==SupplyStationKind.Food?State.FoodStationStock:State.WaterStationStock)<=0)return Unavailable("先购买一台空器具。");
        if(!CanPlaceStation(State.ActiveRegionId,x,z))return Unavailable("仓体和前方出料处都需要干燥空地，请换个位置。");
        long sequence=State.SupplyStationSequence;string id;
        do {if(sequence==long.MaxValue)return Unavailable("器具身份序号已用尽，无法再放置新器具。");id="station-"+(++sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);}
        while(StationIdentityUsed(id));
        State.SupplyStationSequence=sequence;
        State.SupplyStations.Add(new(){Id=id,Kind=kind,RegionId=State.ActiveRegionId,X=x,Z=z});
        if(kind==SupplyStationKind.Food)State.FoodStationStock--;else State.WaterStationStock--;
        return new(true,"空器具已放稳，使用补充工具装入实际食水。","","station-place",Changed:true);
    }
    private void TickSupplyStations()
    {
        foreach(var s in State.SupplyStations)
        {
            if(!s.HeldByHand&&s.RefillIntervalSeconds>0&&State.WorldSeconds>=s.NextRefillAt)
            {
                s.NextRefillAt=State.WorldSeconds+s.RefillIntervalSeconds;
                int available=s.Kind==SupplyStationKind.Food?State.Food:State.StationWaterPortions;
                int amount=Math.Min(available,Math.Max(0,StationCapacity(s)-s.StoredPortions-StationOutletPortions(s)));
                if(amount>0)
                {
                    if(s.Kind==SupplyStationKind.Food)State.Food-=amount;else State.StationWaterPortions-=amount;
                    s.StoredPortions+=amount;s.LastEvent=$"按频率从仓库补充{amount}份。";
                }
                else if(available==0)s.LastEvent="仓库食水已用完，等待补充；没有自动购买。";
            }
            var outlet=StationOutlet(s);
            if(s.OutletObjectId!=null&&outlet==null)s.OutletObjectId=null;
            if(s.HeldByHand||StationOutletBusy(s)||outlet is { Portions:>0 }||s.StoredPortions<=0)continue;
            if(!CanStationOutletFit(s.RegionId,s.X,s.Z+SupplyStationOutletOffset,s.Id,outlet?.Id,existingPlate:outlet!=null))
            {s.LastEvent="出料处被占用，仓内份额保留。";continue;}
            if(State.RoomObjects.Count(o=>!o.Stored&&o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water)>=24&&outlet==null)
            {s.LastEvent="小院食水物件已满，先整理出料处。";continue;}
            if(outlet==null)
            {
                long sequence=s.OutputSequence;string? id=null;
                while(sequence<long.MaxValue)
                {
                    string candidate=s.Id+"-out-"+(++sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if(!StationIdentityUsed(candidate)){id=candidate;break;}
                }
                if(id==null){s.LastEvent="出料身份序号已用尽，仓内份额保留。";continue;}
                s.OutputSequence=sequence;
                outlet=new(){Id=id,Kind=StationFoodKind(s),RegionId=s.RegionId,X=s.X,Z=s.Z+SupplyStationOutletOffset,Portions=0};
                State.RoomObjects.Add(outlet);s.OutletObjectId=id;
            }
            outlet.Height=RoomSupplySupport.Height(State,s.RegionId,outlet.X,outlet.Z,outlet.Kind);
            outlet.Portions=1;s.StoredPortions--;s.LastEvent=s.StoredPortions==0?"最后一份已送到出口，仓内已空。":"一份实际食水已送到出口。";
        }
    }

    public bool SetStationRefillInterval(string id,int seconds)
    {
        var s=Station(id);
        if(s==null||s.RegionId!=State.ActiveRegionId||seconds is not (0 or 10 or 30 or 60 or 120))return false;
        s.RefillIntervalSeconds=seconds;s.NextRefillAt=State.WorldSeconds+seconds;return true;
    }
}

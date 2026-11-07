namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public static readonly int[] CourtyardSpeeds=[1,2,3,5,10];
    public double CourtyardSpeed=>CourtyardSpeeds.MinBy(value=>Math.Abs(value-State.Settings.Speed));
    public void SetCourtyardSpeed(int speed)
    {
        if(!CourtyardSpeeds.Contains(speed))throw new ArgumentOutOfRangeException(nameof(speed));
        State.Settings.Speed=speed;
    }
    public string? CourtyardRecycleReason(string id)
    {
        if(State.Settings.Pause||State.Mode==PlayMode.CloseUp)return "先回到运行中的庭院。";
        var c=State.Creatures.Find(c=>c.Id==id);
        if(c!=null)
        {
            if(c.Family.Memorialized||c.Family.ColdStorage.Stored||c.RegionId!=State.ActiveRegionId)return "对象已不在庭院。";
            if(State.Creatures.Count(x=>x.Family.ColdStorage.Stored)>=ColdStorageCapacity)return "回收箱12个位置已满。";
            if(c.Interaction.Restraint!=RestraintKind.None)return "先解除装置。";
            if(WaterEnvironment.IsInWater(State,c))return "先救援到岸上。";
            if(c.Interaction.EatingRemaining>0)return "先等它咽下这一口。";
            return null;
        }
        if(Station(id) is { } station)
        {
            if(station.RegionId!=State.ActiveRegionId)return "器具已不在当前庭院。";
            if(StationOutletBusy(station))return "先等这一口完成。";
            string contents=station.Kind==SupplyStationKind.Food?"food":"water",item=station.Kind==SupplyStationKind.Food?"food-station":"water-station";
            if(ShopQuantity(contents)>int.MaxValue-station.StoredPortions-StationOutletPortions(station)||ShopQuantity(item)>=99)return "仓库已满。";
            return null;
        }
        if(DetachedStem(id) is {Cleared:false} brood)return brood.RegionId==State.ActiveRegionId?null:"培养瓶已不在当前庭院。";
        var obj=State.RoomObjects.Find(o=>o.Id==id&&!o.Stored&&o.RegionId==State.ActiveRegionId);
        if(obj==null)return "对象已不在庭院。";
        if(NaturalResources.Consumer(State,obj)!=null)return "先等它吃完这一口。";
        string? stock=obj.Kind switch{RoomObjectKind.Meal or RoomObjectKind.Sweet=>"food",RoomObjectKind.Water=>"water",RoomObjectKind.Ball=>"ball",RoomObjectKind.Treasure=>"treasure",_=>null};
        int count=obj.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water?obj.Portions:1;
        return stock!=null&&ShopQuantity(stock)>int.MaxValue-count?"仓库已满。":null;
    }
    public const int ColdStorageFoodRecovery=1;
    public string? ColdDepartureReason(string id,bool destroy)
    {
        var c=State.Creatures.Find(c=>c.Id==id&&c.Family.ColdStorage.Stored);
        if(State.Settings.Pause)return "先恢复时间。";
        if(c==null)return "对象已不在回收箱。";
        if(HasInteractionGesture)return "先结束当前接触。";
        if(!destroy&&c.Condition==PhysicalCondition.Dead)return "遗体不能放生，可取回或销毁。";
        if(c.Family.Pregnancy.Phase!=PregnancyPhase.None||State.Creatures.Any(m=>m.Family.Pregnancy.FatherId==id&&
            m.Family.Pregnancy.Phase is PregnancyPhase.Preparing or PregnancyPhase.Gestating)||
            State.DetachedStemBroods.Any(s=>(s.Mother.Id==id||s.Father.Id==id)&&s.Phase is DetachedStemPhase.Loose or DetachedStemPhase.Bottled))
            return "先完成相关孕育与恢复，再安排离场。";
        if(State.CreatureSales.Count>=10000)return "离场档案已满。";
        if(destroy&&ShopQuantity("food")>int.MaxValue-ColdStorageFoodRecovery)return "食物仓库已满，未销毁。";
        return null;
    }
    public string DepartColdCreature(string id,bool destroy)
    {
        if(ColdDepartureReason(id,destroy) is { } reason)return reason;
        var c=State.Creatures.Single(c=>c.Id==id);
        CompleteCreatureDeparture(c,0,destroy?CreatureDepartureKind.Destroy:CreatureDepartureKind.Release);
        if(destroy)ChangeShopQuantity("food",ColdStorageFoodRecovery);
        if(State.SelectedId==id){State.SelectedId=State.Creatures.FirstOrDefault(o=>o.RegionId==State.ActiveRegionId)?.Id;State.Mode=PlayMode.Management;}
        State.LastActionMessage=destroy?$"{c.Name}已销毁，回收{ColdStorageFoodRecovery}份食物。":$"{c.Name}已放生，保留亲缘档案。";
        Log(State.LastActionMessage);return State.LastActionMessage;
    }
}

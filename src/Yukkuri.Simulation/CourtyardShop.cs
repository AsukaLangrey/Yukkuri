namespace Yukkuri.Simulation;

public sealed record ShopProduct(string Id,string Category,string Name,int Price,int Pack=1,bool Reusable=false);
public static class CourtyardShop
{
    public static readonly ShopProduct[] Products =
    [
        new("food","食物与饮水","饭饭 / 零食",WorldSimulation.FoodBundlePrice,WorldSimulation.FoodBundleCount), new("water","食物与饮水","清水",WorldSimulation.StationWaterBundlePrice,WorldSimulation.StationWaterBundleCount),
        new("ball","玩具","软球",12,Reusable:true),new("treasure","玩具","小宝物",3),new("wrap","玩具","柔软包裹",16,Reusable:true),
        new("towel","养护用品","毛巾",8,Reusable:true),new("medicine","养护用品","护理面粉",WorldSimulation.MedicineBundlePrice,WorldSimulation.MedicineBundleCount),new("juice","养护用品","救护橙汁",20),
        new("rescue","养护用品","水域救援用品",WaterEnvironment.RescueToolPrice),new("freezer","养护用品","冷藏箱",60,Reusable:true),
        new("accessory","饰品","替换饰品",12),new("sewing-kit","饰品","缝补工具",8,Reusable:true),new("inducer","繁育与油茎用品","诱导繁育喷剂",24),
        new("suppressant","繁育与油茎用品","抑制发情喷剂",18),new("vessel","繁育与油茎用品","空瓶",WorldSimulation.StemVesselPrice),
        new("scissors","繁育与油茎用品","油茎剪具",12,Reusable:true),
        new("frame","工具与装置","固定具",25,Reusable:true),new("lighter","工具与装置","火机",10,Reusable:true),
        new("box","工具与装置","箱子",18,Reusable:true),new("hanger","工具与装置","悬挂装置",30,Reusable:true),
        new("food-station","食水供给设施","食物供给器",WorldSimulation.FoodStationPrice,Reusable:true),
        new("water-station","食水供给设施","饮水器",WorldSimulation.WaterStationPrice,Reusable:true)
    ];
}
public sealed partial class WorldState
{
    // New fields have neutral defaults. Old money, food, medicine and flags remain untouched.
    public Dictionary<string,int> ShopStock { get; set; } = new();
}
public sealed partial class WorldSimulation
{
    public int ShopQuantity(string id)=>id switch
    {
        "food"=>State.Food,"water"=>State.StationWaterPortions,"medicine"=>State.Medicine,
        "rescue"=>State.WaterRescueTools,"vessel"=>State.StemVessels,"inducer"=>State.CourtshipTools,
        "food-station"=>State.FoodStationStock,"water-station"=>State.WaterStationStock,
        _=>State.ShopStock.GetValueOrDefault(id)
    };
    public long ShopOwnedQuantity(string id)
    {
        long stock=ShopQuantity(id);
        return stock+(id switch
        {
            "food"=>State.RoomObjects.Where(o=>!o.Stored&&o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet).Sum(o=>(long)o.Portions)+
                State.SupplyStations.Where(s=>s.Kind==SupplyStationKind.Food).Sum(s=>(long)s.StoredPortions),
            "water"=>State.RoomObjects.Where(o=>!o.Stored&&o.Kind==RoomObjectKind.Water).Sum(o=>(long)o.Portions)+
                State.SupplyStations.Where(s=>s.Kind==SupplyStationKind.Water).Sum(s=>(long)s.StoredPortions),
            "frame"=>State.Creatures.Count(c=>c.Interaction.Restraint==RestraintKind.Frame),
            "box"=>State.Creatures.Count(c=>c.Interaction.Restraint==RestraintKind.Box),
            "hanger"=>State.Creatures.Count(c=>c.Interaction.Restraint==RestraintKind.Hanging),
            "wrap"=>State.Creatures.Count(c=>c.Interaction.Restraint==RestraintKind.Wrap),
            "ball"=>State.RoomObjects.Count(o=>o.Kind==RoomObjectKind.Ball&&!o.Stored),
            "treasure"=>State.RoomObjects.Count(o=>o.Kind==RoomObjectKind.Treasure&&!o.Stored),
            "food-station"=>State.SupplyStations.Count(s=>s.Kind==SupplyStationKind.Food),
            "water-station"=>State.SupplyStations.Count(s=>s.Kind==SupplyStationKind.Water),
            "vessel"=>State.DetachedStemBroods.Count(s=>s.Phase==DetachedStemPhase.Bottled&&!s.Cleared),
            _=>0
        });
    }
    private void ChangeShopQuantity(string id,int delta)
    {
        switch(id)
        {
            case "food":State.Food+=delta;break;case "water":State.StationWaterPortions+=delta;break;
            case "medicine":State.Medicine+=delta;break;case "rescue":State.WaterRescueTools+=delta;break;
            case "vessel":State.StemVessels+=delta;break;case "inducer":State.CourtshipTools+=delta;break;
            case "food-station":State.FoodStationStock+=delta;break;case "water-station":State.WaterStationStock+=delta;break;
            default:State.ShopStock[id]=ShopQuantity(id)+delta;break;
        }
    }
    public bool BuyShopProduct(string id,int packs=1)
    {
        var p=CourtyardShop.Products.FirstOrDefault(p=>p.Id==id);
        if(!State.InteractionPlaytest||p==null||packs is <1 or >99)return false;
        long cost=(long)p.Price*packs, count=(long)p.Pack*packs;
        int limit=id is "rescue" or "vessel" or "inducer" or "food-station" or "water-station"?99:int.MaxValue;
        if(State.Money<cost||ShopQuantity(id)>limit-count)return false;
        State.Money-=(int)cost;ChangeShopQuantity(id,(int)count);
        if(id=="inducer")State.CourtshipToolPurchases=(int)Math.Min(int.MaxValue-1,(long)State.CourtshipToolPurchases+packs);
        if(id=="rescue")State.WaterRescuePurchases=(int)Math.Min(int.MaxValue-1,(long)State.WaterRescuePurchases+packs);
        if(id=="vessel")State.StemVesselPurchases=(int)Math.Min(int.MaxValue-1,(long)State.StemVesselPurchases+packs);
        Log($"购买{p.Name} {count}份，支付{cost}元。");return true;
    }
    private static string? InstalledItem(RestraintKind kind)=>kind switch
    {RestraintKind.Frame=>"frame",RestraintKind.Box=>"box",RestraintKind.Hanging=>"hanger",RestraintKind.Wrap=>"wrap",_=>null};
    private static string? ActionShopItem(InteractionAction action)=>action switch
    {
        InteractionAction.GiveBall=>"ball",InteractionAction.GiveTreasure=>"treasure",InteractionAction.Wrap=>"wrap",
        InteractionAction.Pin=>"frame",InteractionAction.Box=>"box",InteractionAction.Hang=>"hanger",
        InteractionAction.ReplaceAccessory=>"accessory",InteractionAction.Clean or InteractionAction.Dry or InteractionAction.CleanAccessory=>"towel",
        InteractionAction.RepairAccessory=>"sewing-kit",
        InteractionAction.Burn or InteractionAction.BurnFeet=>"lighter",InteractionAction.CutStem=>"scissors",_=>null
    };
    private string? ShopActionError(InteractionAction action)=>State.InteractionPlaytest&&ActionShopItem(action) is { } item&&ShopQuantity(item)<=0
        ?"仓库没有"+CourtyardShop.Products.First(p=>p.Id==item).Name+"，请先到商店购买。":null;
    private void SettleShopAction(InteractionAction action,RestraintKind before,InteractionFeedback feedback)
    {
        if(!State.InteractionPlaytest||!feedback.Success||!feedback.Changed||feedback.Refused)return;
        if(action is InteractionAction.GiveBall or InteractionAction.GiveTreasure or InteractionAction.Wrap or InteractionAction.Pin or InteractionAction.Box or InteractionAction.Hang or InteractionAction.ReplaceAccessory)
            ChangeShopQuantity(ActionShopItem(action)!,-1);
        if(action is InteractionAction.Release or InteractionAction.OpenBox or InteractionAction.Lower or InteractionAction.Unwrap&&InstalledItem(before) is { } item)
            ChangeShopQuantity(item,1);
    }
    public static void ValidateShop(WorldState state)
    {
        if(state.ShopStock==null||state.ShopStock.Any(p=>p.Value<0||!CourtyardShop.Products.Any(item=>item.Id==p.Key)))
            throw new InvalidDataException("商店库存无效");
        if(state.RoomObjects==null||state.SupplyStations==null||state.DetachedStemBroods==null||state.PortableMotions==null||state.PortableMotions.Count>1||state.PortableMotions.Any(m=>m==null||string.IsNullOrWhiteSpace(m.Id)||
            !new[]{m.Height,m.VelocityX,m.VelocityZ,m.VerticalVelocity}.All(double.IsFinite)||m.Height is <0 or >3||Math.Abs(m.VelocityX)>10||Math.Abs(m.VelocityZ)>10||Math.Abs(m.VerticalVelocity)>15||
            !state.RoomObjects.Any(o=>o.Id==m.Id)&&!state.Creatures.Any(c=>c.Id==m.Id)&&!state.SupplyStations.Any(s=>s.Id==m.Id)&&!state.DetachedStemBroods.Any(s=>s.EventId==m.Id)))
            throw new InvalidDataException("搬运或抛出状态无效");
    }
}

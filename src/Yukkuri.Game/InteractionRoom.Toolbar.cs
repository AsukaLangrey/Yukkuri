using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private enum RoomMode { Observe,Grab,Pet,Tidy,Poke,Pressure,Meal,Sweet,Water,FoodStation,WaterStation,
        Ball,Treasure,Wrap,Stretch,Greeting,Scold,Sing,Teaching,Commands,Company,Towel,Inspect,Accessory,Care,Juice,Cold,Bury,
        Breeding,Induce,Suppress,Stem,Frame,Lighter,Box,Hanging,Shelter }
    private sealed record ToolbarEntry(RoomMode Mode,string Name,InteractionAction Action);
    private static ToolbarEntry E(RoomMode m,string n,InteractionAction a=InteractionAction.Inspect)=>new(m,n,a);
    private static readonly (string Name,ToolbarEntry[] Entries)[] Toolbar =
    [
        ("观察",[E(RoomMode.Observe,"观察")]),
        ("空手",[E(RoomMode.Grab,"抓取",InteractionAction.Lift),E(RoomMode.Pet,"抚摸",InteractionAction.Stroke),E(RoomMode.Tidy,"收拾"),E(RoomMode.Poke,"戳碰",InteractionAction.Poke),E(RoomMode.Pressure,"按压",InteractionAction.Press)]),
        ("食物",[E(RoomMode.Meal,"饭饭",InteractionAction.FeedMeal),E(RoomMode.Sweet,"零食",InteractionAction.FeedSweet),E(RoomMode.Water,"清水",InteractionAction.Drink),E(RoomMode.FoodStation,"食物供给器",InteractionAction.PlaceFoodStation),E(RoomMode.WaterStation,"饮水器",InteractionAction.PlaceWaterStation)]),
        ("玩具",[E(RoomMode.Ball,"软球",InteractionAction.GiveBall),E(RoomMode.Treasure,"小宝物",InteractionAction.GiveTreasure),E(RoomMode.Wrap,"包裹",InteractionAction.Wrap),E(RoomMode.Stretch,"一起伸展",InteractionAction.Stretch)]),
        ("交流",[E(RoomMode.Greeting,"打招呼",InteractionAction.Greet),E(RoomMode.Scold,"训诫",InteractionAction.Scold),E(RoomMode.Sing,"唱歌",InteractionAction.Sing),E(RoomMode.Teaching,"教学"),E(RoomMode.Commands,"指令",InteractionAction.Follow),E(RoomMode.Company,"相处")]),
        ("养护",[E(RoomMode.Towel,"毛巾",InteractionAction.Clean),E(RoomMode.Inspect,"检查身体",InteractionAction.Inspect),E(RoomMode.Accessory,"饰品",InteractionAction.TakeAccessoryCare),E(RoomMode.Care,"护理面粉",InteractionAction.Care),E(RoomMode.Juice,"救护橙汁"),E(RoomMode.Cold,"冷藏与复苏"),E(RoomMode.Bury,"埋葬")]),
        ("繁育",[E(RoomMode.Breeding,"繁殖状态"),E(RoomMode.Induce,"诱导繁育喷剂"),E(RoomMode.Suppress,"抑制发情喷剂"),E(RoomMode.Stem,"油茎养护",InteractionAction.SupportStem)]),
        ("工具",[E(RoomMode.Shelter,"小家与藏身处",InteractionAction.PlaceShelter),E(RoomMode.Frame,"固定具",InteractionAction.Pin),E(RoomMode.Lighter,"火机",InteractionAction.Burn),E(RoomMode.Box,"箱子",InteractionAction.Box),E(RoomMode.Hanging,"悬挂装置",InteractionAction.Hang)])
    ];
    private RoomMode _roomMode=RoomMode.Observe;
    private void SelectActionOwner(InteractionAction action)
    {
        RoomMode mode=action switch
        {
            InteractionAction.Inspect=>RoomMode.Inspect,
            InteractionAction.OpenShelterDoor or InteractionAction.CloseShelterDoor or InteractionAction.CallFromShelter=>RoomMode.Shelter,
            InteractionAction.Clean or InteractionAction.Dry=>RoomMode.Towel,
            InteractionAction.CleanAccessory or InteractionAction.RepairAccessory or InteractionAction.ReplaceAccessory or InteractionAction.TakeAccessory or InteractionAction.ReturnAccessory or InteractionAction.DamageAccessory or InteractionAction.HangAccessory or InteractionAction.StoreAccessory=>RoomMode.Accessory,
            InteractionAction.SupportStem or InteractionAction.CutStem or InteractionAction.BottleStem or InteractionAction.UnbottleStem or InteractionAction.ClearStem=>RoomMode.Stem,
            InteractionAction.Release=>RoomMode.Frame,InteractionAction.OpenBox=>RoomMode.Box,
            InteractionAction.Lower or InteractionAction.AdjustHanging=>RoomMode.Hanging,
            InteractionAction.Rock or InteractionAction.Unwrap=>RoomMode.Wrap,
            InteractionAction.Follow or InteractionAction.Stop or InteractionAction.Wait=>RoomMode.Commands,
            _=>Toolbar.SelectMany(t=>t.Entries).FirstOrDefault(e=>e.Action==action)?.Mode??_roomMode
        };
        _roomMode=mode;for(int i=0;i<Toolbar.Length;i++)if(Toolbar[i].Entries.Any(e=>e.Mode==mode)){_toolbarCategory=i;break;}
    }
    private int _toolbarCategory;
    private VBoxContainer _objectActions=null!;
    private Label _objectDetails=null!;
    private string? _selectedRoomObjectId;
    private string _objectActionSignature="";
    private void BuildToolbar(Control parent)
    {
        var scroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};parent.AddChild(scroll);
        _groupList=Column(scroll,5);_groupList.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        for(int i=0;i<Toolbar.Length;i++){int index=i;_groupList.AddChild(Button(Toolbar[i].Name,()=>SelectToolbarCategory(index)));}
        parent.AddChild(Button("投放新油",ShowCreatureArrival));
        parent.AddChild(Button("商店",()=>ShowShop()));
        _supplyStockLabel=Text("",12);parent.AddChild(_supplyStockLabel);
        parent.AddChild(Text("右键：停止使用工具\n中键拖动画面，滚轮缩放",12,"78816e"));
    }
    private void SelectToolbarCategory(int index)
    {
        _toolbarCategory=index;SelectRoomMode(Toolbar[index].Entries[0]);
    }
    private void SelectRoomMode(ToolbarEntry entry)
    {
        CancelGesture();_roomMode=entry.Mode;_directToolMode=false;
        _group=InteractionCatalog.Group(entry.Action);_action=entry.Action;_toolArmed=entry.Mode!=RoomMode.Observe;
        RebuildToolbarChoices();_force=.25f;_forceSlider.SetValueNoSignal(_force);
        _message=entry.Mode==RoomMode.Observe?"只观察，不接触。点击油或物件查看右侧属性。":"已选择"+entry.Name+"。";
        if(entry.Mode==RoomMode.Commands)ShowCommandChoices();
        if(entry.Mode==RoomMode.Teaching)ShowTeachingChoices();
        if(entry.Mode==RoomMode.Stem)ShowStemChoices();
        if(entry.Mode==RoomMode.Breeding)ShowBreedingStatus();
        if(entry.Mode==RoomMode.Shelter)ArmShelterPlacement();
        if(entry.Mode is RoomMode.FoodStation or RoomMode.WaterStation)ArmSupplyStationPlacement(entry.Mode==RoomMode.FoodStation?SupplyStationKind.Food:SupplyStationKind.Water);
        RefreshRoomUi();
        if(!_modalOpen&&SupplyStockMissing(entry.Action))ShowMissingToolSupplies(entry.Action);
    }
    private void RebuildToolbarChoices()
    {
        ClearChildren(_choiceList);
        if(_toolbarCategory!=0)foreach(var e in Toolbar[_toolbarCategory].Entries)
        {_choiceList.AddChild(Button(e.Name,()=>SelectRoomMode(e),_roomMode==e.Mode));}
        int i=0;foreach(var child in _groupList.GetChildren())if(child is Button b)SetQuickToolHighlight(b,i++==_toolbarCategory);
    }
    private void ShowCommandChoices()
    {
        OpenModal("指令");foreach(var a in new[]{InteractionAction.Follow,InteractionAction.Stop,InteractionAction.Wait})
        {var action=a;_modalContents.AddChild(Button(InteractionCatalog.Choice(a).Label,()=>{CloseModal();SelectAction(action);}));}
    }
    private void ShowTeachingChoices()
    {
        OpenModal("教学");_modalContents.AddChild(Text("点击油示范自己打扫卫生。多次实际练习后，它会主动走到垃圾旁清理。"));
        _modalContents.AddChild(Button("教自己打扫卫生",CloseModal));
    }
    private void ShowStemChoices()
    {
        OpenModal("油茎养护");
        foreach(var a in new[]{InteractionAction.SupportStem,InteractionAction.CutStem,InteractionAction.BottleStem,InteractionAction.UnbottleStem,InteractionAction.ClearStem})
        {var action=a;_modalContents.AddChild(Button(InteractionCatalog.Choice(a).Label,()=>{CloseModal();SelectAction(action);}));}
        _modalContents.AddChild(Text("搬瓶用空手抓取。瓶口食水使用食物入口拿取的同一份物品。"));
    }
    private void ShowShop(string? result=null,string? category=null)
    {
        OpenModal("商店");_modalContents.AddChild(Text($"余额 {Sim.State.Money} 元",20));
        _modalContents.AddChild(Button("提交实际养护记录 · 领取委托收入",()=>ShowShop(Sim.ClaimCareContract().Message,category)));
        if(result!=null)_modalContents.AddChild(Text(result,14));
        var categories=Row(_modalContents,4);
        var selector=new OptionButton();foreach(var name in CourtyardShop.Products.Select(p=>p.Category).Distinct())selector.AddItem(name);
        var names=CourtyardShop.Products.Select(p=>p.Category).Distinct().ToArray();
        int selected=Array.IndexOf(names,category??names[0]);selector.Select(Math.Max(0,selected));
        selector.ItemSelected+=i=>ShowShop(category:names[(int)i]);categories.AddChild(selector);
        foreach(var p in CourtyardShop.Products.Where(p=>p.Category==(category??names[0])))
        {
            _modalContents.AddChild(Text($"{p.Name} · 每包{p.Pack}份 · {p.Price}元 · 持有{Sim.ShopOwnedQuantity(p.Id)} / 仓库{Sim.ShopQuantity(p.Id)}",14));
            var row=Row(_modalContents,6);var quantity=new SpinBox{MinValue=1,MaxValue=99,Step=1,Value=1,CustomMinimumSize=new(100,36)};row.AddChild(quantity);
            var buy=Button($"购买 · 总价 {p.Price} 元",()=>{bool ok=Sim.BuyShopProduct(p.Id,(int)quantity.Value);ShowShop(ok?"购买成功，已入库。":"余额不足或库存已满，未扣款。",p.Category);RefreshSupplyStock();});
            void RefreshPrice(){buy.Text=$"购买 · 总价 {p.Price*(int)quantity.Value} 元";buy.Disabled=Sim.State.Money<(long)p.Price*(int)quantity.Value;}
            quantity.ValueChanged+=_=>RefreshPrice();RefreshPrice();row.AddChild(buy);
        }
        _modalContents.AddChild(Button("购入新油 · 选择场景落点",ShowCreatureArrival));
        _modalContents.AddChild(Button("查看收容箱",ShowContainer));
        _modalContents.AddChild(Button("返回小院",CloseModal));
    }
    private void ShowFamilyDetails()
    {
        OpenModal("对象详细记录");if(Sim.Selected is { } c){_modalContents.AddChild(Text(FamilySummary(c)));_modalContents.AddChild(Text(FamilyRules.DescribeLearning(c)));}
        _modalContents.AddChild(Button("返回",CloseModal));
    }
}

using Godot;
using System;
using System.Collections.Generic;
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
            InteractionAction.TeachFollow or InteractionAction.TeachStop or InteractionAction.TeachWait=>RoomMode.Commands,
            _=>Toolbar.SelectMany(t=>t.Entries).FirstOrDefault(e=>e.Action==action)?.Mode??_roomMode
        };
        _roomMode=mode;for(int i=0;i<Toolbar.Length;i++)if(Toolbar[i].Entries.Any(e=>e.Mode==mode)){_toolbarCategory=i;break;}
    }
    private int _toolbarCategory;
    private readonly Button[] _categoryButtons=new Button[Toolbar.Length+2];
    private readonly Button[] _categoryIconButtons=new Button[Toolbar.Length+2];
    private readonly MarginContainer[] _categoryChoices=new MarginContainer[Toolbar.Length+2];
    private readonly System.Collections.Generic.List<(Button Button,RoomMode Mode)> _navigationActions=new();
    private Control _navigationDivider=null!,_navigationOperations=null!,_navigationCategories=null!;
    private VBoxContainer _objectActions=null!;
    private Label _objectDetails=null!;
    private string? _selectedRoomObjectId;
    private string _objectActionSignature="";
    private GridContainer ToolGrid(Control parent)
    {
        var grid=new GridContainer{Columns=2,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        grid.AddThemeConstantOverride("h_separation",6);grid.AddThemeConstantOverride("v_separation",4);
        parent.AddChild(grid);return grid;
    }
    private Button ToolButton(string label,string icon,Action action,bool active=false)
    {
        var button=Button(label,action,active);
        button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        button.Alignment=HorizontalAlignment.Left;
        button.AddThemeFontSizeOverride("font_size",13);
        UiActionIcons.Apply(button,icon);SetQuickToolHighlight(button,active);
        return button;
    }
    private static string ToolIcon(RoomMode mode)=>mode switch
    {
        RoomMode.Observe or RoomMode.Inspect=>"eye",
        RoomMode.Grab or RoomMode.Pet or RoomMode.Poke or RoomMode.Pressure=>"hand",
        RoomMode.Tidy or RoomMode.Towel=>"clean",
        RoomMode.Meal or RoomMode.Sweet or RoomMode.FoodStation=>"food",
        RoomMode.Water or RoomMode.WaterStation=>"water",
        RoomMode.Ball or RoomMode.Treasure or RoomMode.Stretch=>"ball",
        RoomMode.Wrap or RoomMode.Box=>"box",
        RoomMode.Sing=>"music",RoomMode.Teaching=>"book",
        RoomMode.Greeting or RoomMode.Scold or RoomMode.Commands or RoomMode.Company=>"chat",
        RoomMode.Accessory=>"bow",RoomMode.Care or RoomMode.Juice=>"care",
        RoomMode.Cold=>"cold",RoomMode.Bury=>"leaf",
        RoomMode.Breeding or RoomMode.Stem=>"sprout",
        RoomMode.Induce or RoomMode.Suppress=>"spray",
        RoomMode.Shelter=>"home",RoomMode.Lighter=>"flame",_=>"tool"
    };
    private void BuildToolbar(Control parent)
    {
        string[] categoryIcons=["eye","hand","food","ball","chat","care","sprout","tool"];
        var navigation=Row(parent,6);
        var categories=Column(navigation,4);categories.CustomMinimumSize=new(76,0);
        _navigationCategories=categories;
        var divider=new VSeparator();
        divider.AddThemeStyleboxOverride("separator",new StyleBoxLine{Color=new Color("40524720"),Thickness=1,Vertical=true});
        navigation.AddChild(divider);
        _navigationDivider=divider;
        var operations=Column(navigation,4);operations.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _navigationOperations=operations;
        for(int i=0;i<Toolbar.Length;i++)
        {
            int index=i;
            _categoryButtons[i]=ToolButton(Toolbar[i].Name,categoryIcons[i],()=>ToggleToolbarCategory(index));
            categories.AddChild(_categoryButtons[i]);
            var iconButton=ToolButton("",categoryIcons[i],()=>OpenToolbarCategory(index));
            iconButton.Alignment=HorizontalAlignment.Center;
            _collapsedNavigation.AddChild(iconButton);_categoryIconButtons[i]=iconButton;
            var inset=new MarginContainer{Visible=false,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
            operations.AddChild(inset);_categoryChoices[i]=inset;
            var choices=Column(inset,3);
            foreach(var entry in Toolbar[i].Entries)
            {
                var button=ToolButton(entry.Name,ToolIcon(entry.Mode),()=>SelectRoomMode(entry));
                choices.AddChild(button);_navigationActions.Add((button,entry.Mode));
            }
        }
        VBoxContainer ServiceCategory(int index,string name,string icon)
        {
            _categoryButtons[index]=ToolButton(name,icon,()=>ToggleToolbarCategory(index));
            categories.AddChild(_categoryButtons[index]);
            var iconButton=ToolButton("",icon,()=>OpenToolbarCategory(index));
            iconButton.Alignment=HorizontalAlignment.Center;
            _collapsedNavigation.AddChild(iconButton);_categoryIconButtons[index]=iconButton;
            var inset=new MarginContainer{Visible=false,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
            operations.AddChild(inset);_categoryChoices[index]=inset;
            return Column(inset,3);
        }
        var arrivals=ServiceCategory(Toolbar.Length,"新油","add");
        arrivals.AddChild(ToolButton("投放新油","add",ShowCreatureArrival));
        arrivals.AddChild(ToolButton("回收箱 · 12格","cold",ShowContainer));
        var shop=ServiceCategory(Toolbar.Length+1,"商店","shop");
        foreach(string category in CourtyardShop.Products.Select(p=>p.Category).Distinct())
        {
            string selected=category;
            shop.AddChild(ToolButton(category,"shop",()=>ShowShop(category:selected)));
        }
    }
    private void ToggleToolbarCategory(int index)
    {
        CancelGesture();
        _toolbarCategory=index;
        if(index<Toolbar.Length&&Toolbar[index].Entries.Length==1&&Toolbar[index].Entries[0].Name==Toolbar[index].Name)
        {SelectRoomMode(Toolbar[index].Entries[0]);return;}
        RebuildToolbarChoices();
    }
    private void OpenToolbarCategory(int index)
    {
        ToggleToolbarCategory(index);
        if(_leftCollapsed)ToggleLeftSidebar();
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
        _message="";
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
        for(int i=0;i<_categoryButtons.Length;i++)
        {
            bool expanded=i==_toolbarCategory;
            bool redundant=i<Toolbar.Length&&Toolbar[i].Entries.Length==1&&Toolbar[i].Entries[0].Name==Toolbar[i].Name;
            _categoryChoices[i].Visible=expanded&&!redundant;
            SetQuickToolHighlight(_categoryButtons[i],expanded);
            SetQuickToolHighlight(_categoryIconButtons[i],expanded);
        }
        foreach(var (button,mode) in _navigationActions)SetQuickToolHighlight(button,mode==_roomMode);
        bool hasChoices=_categoryChoices.Any(c=>c.Visible);
        _navigationDivider.Visible=_navigationOperations.Visible=hasChoices;
        _navigationCategories.SizeFlagsHorizontal=hasChoices?Control.SizeFlags.Fill:Control.SizeFlags.ExpandFill;
        _ui.SetMeta("left_sidebar_single",!hasChoices);
        GameUiLayout.Left(_leftPanel,_leftCollapsed);
        _layoutCanvasWidgets?.Invoke();
    }
    private void ShowCommandChoices()
    {
        OpenActivityPanel("指令",ShowCommandChoices);foreach(var a in new[]{InteractionAction.Follow,InteractionAction.Stop,InteractionAction.Wait})
        {var action=a;_panelContents.AddChild(PanelButton(InteractionCatalog.Choice(a).Label,()=>{CloseActivityPanel();SelectAction(action);}));}
    }
    private void ShowTeachingChoices()
    {
        OpenActivityPanel("教学",ShowTeachingChoices);_panelContents.AddChild(Text("清洁示范必须有身旁的真实脏处。愿不愿主动做取决于性格、规则教育与牌照；独立完成后才记入实际能力。"));
        _panelContents.AddChild(PanelButton("教自己打扫卫生",CloseActivityPanel));
        foreach(var action in new[]{InteractionAction.TeachFollow,InteractionAction.TeachStop,InteractionAction.TeachWait})
        {var selected=action;_panelContents.AddChild(PanelButton(InteractionCatalog.Choice(selected).Label,()=>{CloseActivityPanel();SelectAction(selected);}));}
    }
    private void ShowStemChoices()
    {
        OpenActivityPanel("油茎养护",ShowStemChoices);
        foreach(var a in new[]{InteractionAction.SupportStem,InteractionAction.CutStem,InteractionAction.BottleStem,InteractionAction.UnbottleStem,InteractionAction.ClearStem})
        {var action=a;_panelContents.AddChild(PanelButton(InteractionCatalog.Choice(a).Label,()=>{CloseActivityPanel();SelectAction(action);}));}
        _panelContents.AddChild(Text("搬瓶用空手抓取。瓶口食水使用食物入口拿取的同一份物品。"));
    }
    private string? _shopCategory;
    private readonly System.Collections.Generic.Dictionary<string,double> _shopQuantities=new();
    private void ShowShop(string? result=null,string? category=null)
    {
        category??=_shopCategory;_shopCategory=category;
        OpenActivityPanel("商店",() => ShowShop(category:category));_panelContents.AddChild(Text($"余额 {Sim.State.Money} 元",20));
        _panelContents.AddChild(PanelButton("提交实际养护记录 · 领取委托收入",()=>ShowShop(Sim.ClaimCareContract().Message,category)));
        _panelContents.AddChild(PanelButton("商店收购 · 出售当前油",()=>ShowCreatureMarket()));
        _panelContents.AddChild(Text($"累计售油收入 {Sim.State.CreatureSalesRevenue} 元 · 道具解锁进度",14));
        if(category=="养护用品")_panelContents.AddChild(Text("庭院已自带12格回收箱，无需另购冷藏箱。旧库存保留。",13));
        if(result!=null)_panelContents.AddChild(Text(result,14));
        var categories=Row(_panelContents,4);
        var selector=new InlineChoice();foreach(var name in CourtyardShop.Products.Select(p=>p.Category).Distinct())selector.AddItem(name);
        var names=CourtyardShop.Products.Select(p=>p.Category).Distinct().ToArray();
        int selected=Array.IndexOf(names,category??names[0]);selector.Select(Math.Max(0,selected));
        selector.ItemSelected+=i=>ShowShop(category:names[(int)i]);categories.AddChild(selector);
        foreach(var p in CourtyardShop.Products.Where(p=>p.Id!="freezer"&&p.Category==(category??names[0])))
        {
            _panelContents.AddChild(Text($"{p.Name} · 每包{p.Pack}份 · {p.Price}元 · 持有{Sim.ShopOwnedQuantity(p.Id)} / 仓库{Sim.ShopQuantity(p.Id)}",14));
            if(Sim.ShopUnlockReason(p.Id) is { } locked)_panelContents.AddChild(Text(locked,13));
            var row=Row(_panelContents,6);var quantity=new SpinBox{MinValue=1,MaxValue=99,Step=1,Value=_shopQuantities.GetValueOrDefault(p.Id,1),CustomMinimumSize=new(100,36)};row.AddChild(quantity);
            quantity.GetLineEdit().ContextMenuEnabled=false;
            var buy=PanelButton($"购买 · 总价 {p.Price} 元",()=>{bool ok=Sim.BuyShopProduct(p.Id,(int)quantity.Value);ShowShop(ok?"购买成功，已入库。":"余额不足或库存已满，未扣款。",p.Category);RefreshSupplyStock();});
            void RefreshPrice(){buy.Text=$"购买 · 总价 {p.Price*(int)quantity.Value} 元";buy.Disabled=Sim.ShopUnlockReason(p.Id)!=null||Sim.State.Money<(long)p.Price*(int)quantity.Value;}
            quantity.ValueChanged+=value=>{_shopQuantities[p.Id]=value;RefreshPrice();};RefreshPrice();row.AddChild(buy);
        }
        _panelContents.AddChild(PanelButton("购入新油 · 选择场景落点",ShowCreatureArrival));
        _panelContents.AddChild(PanelButton("查看收容箱",ShowContainer));
        _panelContents.AddChild(PanelButton("返回小院",CloseActivityPanel));
    }
    private void ShowFamilyDetails()
    {
        OpenActivityPanel("家庭记录",ShowFamilyDetails);
        _recordTabText=Text(FamilyRecordsText(),14);_panelContents.AddChild(_recordTabText);
        _activityActions.AddChild(PanelButton("家庭照护",ShowFamily));
    }
}

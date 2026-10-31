using Godot;
using System;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private string? _selectedSupplyStationId;
    private WorldState? _supplyStationSelectionWorld;
    private VBoxContainer _supplyStationPanel=null!;
    private Label _supplyStationDescription=null!,_supplyStationInstructions=null!;
    private Button _supplyStationRefillButton=null!,_supplyStationMoveButton=null!;

    private static string SupplyStationName(SupplyStationKind kind)=>kind==SupplyStationKind.Food?"饭饭供给器":"饮水器";
    private int SupplyStationRefillStock(SupplyStationKind kind)=>kind==SupplyStationKind.Food?Sim.State.Food:Sim.State.StationWaterPortions;

    private void BuildSupplyStationPanel(Control parent)
    {
        _supplyStationPanel=Column(parent,7);_supplyStationPanel.Visible=false;
        _supplyStationDescription=Text("",14);_supplyStationPanel.AddChild(_supplyStationDescription);
        _supplyStationInstructions=Text("",12,"78816e");_supplyStationPanel.AddChild(_supplyStationInstructions);
        _supplyStationRefillButton=Button("拿补给用品 · 按住器具补充",()=>SelectQuickTool(InteractionAction.RefillSupplyStation));
        _supplyStationPanel.AddChild(_supplyStationRefillButton);
        _supplyStationMoveButton=Button("手掌 · 拖动器具",()=>SelectQuickTool(InteractionAction.Lift));
        _supplyStationPanel.AddChild(_supplyStationMoveButton);
        _supplyStationPanel.AddChild(Button("购买器具与补给",()=>ShowSupplyStationSupplies()));
    }

    private void ClearSupplyStationSelection()=>_selectedSupplyStationId=null;

    private void NormalizeSupplyStationSelection()
    {
        if(!ReferenceEquals(_supplyStationSelectionWorld,Sim.State))
        {_selectedSupplyStationId=null;_supplyStationSelectionWorld=Sim.State;}
        if(_selectedSupplyStationId is { } id&&
            (Sim.SupplyStationStatus(id) is not { } station||station.RegionId!=Sim.State.ActiveRegionId))
            _selectedSupplyStationId=null;
    }

    private void SelectSupplyStation(string id,bool cancelGesture=true)
    {
        var station=Sim.SupplyStationStatus(id);
        if(station==null||station.RegionId!=Sim.State.ActiveRegionId)return;
        if(cancelGesture)CancelGesture();
        _selectedDetachedStemEventId=null;
        _supplyStationSelectionWorld=Sim.State;_selectedSupplyStationId=id;
        Sim.State.Mode=PlayMode.Management;
        _message="正在观察"+SupplyStationName(station.Kind)+"。补充和搬动需要实际接触。";
        RefreshRoomUi();
    }

    private bool SupplyStationRefillStockMissing()
    {
        var station=_selectedSupplyStationId is { } id?Sim.SupplyStationStatus(id):null;
        return station!=null?SupplyStationRefillStock(station.Kind)<=0:
            Sim.State.Food<=0&&Sim.State.StationWaterPortions<=0;
    }

    private void RefreshSupplyStationUi()
    {
        NormalizeSupplyStationSelection();
        var station=_selectedSupplyStationId is { } id?Sim.SupplyStationStatus(id):null;
        _supplyStationPanel.Visible=station!=null;
        if(station==null)return;
        _detachedStemPanel.Visible=false;
        _creatureVitalMeters.Visible=_condition.Visible=_memory.Visible=_memoryHeading.Visible=false;
        _familyPanelButton.Disabled=_closeButton.Disabled=true;
        _who.Text=SupplyStationName(station.Kind);
        _speechLabel.Text=SupplyStationName(station.Kind)+" · "+station.LastEvent;
        if(!_toolArmed)
        {
            _title.Text="空手 · "+SupplyStationName(station.Kind);
            _hint.Text="按住仓体拖动，松开放稳。补充食水请拿补给用品，再对准仓体持续接触。";
        }
        int total=station.StoredPortions+station.OutletPortions;
        int stock=SupplyStationRefillStock(station.Kind);
        _supplyStationDescription.Text=$"仓内 {station.StoredPortions} 份 · 出料口 {station.OutletPortions} 份\n"+
            $"总量 {total}/{station.Capacity} · 可补充库存 {stock} 份\n"+
            (station.HeldByHand?"正在手中搬动":total==0?"已经空仓，需要补充":"食水从前方出口逐份取用")+
            (string.IsNullOrEmpty(station.LastEvent)?"":"\n"+station.LastEvent);
        _supplyStationInstructions.Text="放下后，油会按自己的需要取用前方的饭饭或清水。\n"+
            "拿补给用品对准仓体按住1.6秒；移开重新计时，成功才转入库存。"+
            (string.IsNullOrEmpty(station.RefusalReason)?"":"\n"+station.RefusalReason);
        _supplyStationRefillButton.Disabled=total>=station.Capacity||!station.CanRefill&&stock>0;
        _supplyStationRefillButton.TooltipText=stock<=0?"缺少对应补给，点击先购买；买到后仍需实际补充器具。":
            "拿起补给工具，对准器具外表按住。仅空余容量实际接收的份额会从库存转入。";
        _supplyStationMoveButton.Disabled=!station.CanMove;
    }

    private void ShowSupplyStationSupplies(string? result=null)
    {
        OpenModal("供给器与补给");
        _modalContents.AddChild(Text("先放器具，再装入有限食水",20));
        _modalContents.AddChild(Text(Sim.State.UnlimitedResources?
            "测试购买不扣钱；空器具、仓量和补给仍有实际数量。":$"现有金钱：{Sim.State.Money} 元"));
        if(!string.IsNullOrEmpty(result))_modalContents.AddChild(Text(result,14));
        void Purchase(string label,int stock,int count,int price,Func<bool> buy,bool hasSpace)
        {
            var row=Row(_modalContents,8);
            var text=Text($"{label} · 库存 {stock}",14);text.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;row.AddChild(text);
            string cost=Sim.State.UnlimitedResources?"测试免费":$"{price} 元";
            var button=Button($"购买 {count} · {cost}",()=>
            {
                bool bought=buy();
                ShowSupplyStationSupplies(bought?$"{label}已入库，仍需实际放置或补充。":"未能购买，请检查金钱和库存。原有用品没有减少。");
                RefreshSupplyStock();
            });
            button.Disabled=!hasSpace||!Sim.State.UnlimitedResources&&Sim.State.Money<price;row.AddChild(button);
        }
        Purchase("空饭饭供给器",Sim.State.FoodStationStock,1,WorldSimulation.FoodStationPrice,
            ()=>Sim.BuySupplyStation(SupplyStationKind.Food),Sim.State.FoodStationStock<99);
        Purchase("空饮水器",Sim.State.WaterStationStock,1,WorldSimulation.WaterStationPrice,
            ()=>Sim.BuySupplyStation(SupplyStationKind.Water),Sim.State.WaterStationStock<99);
        Purchase("饭饭",Sim.State.Food,WorldSimulation.FoodBundleCount,WorldSimulation.FoodBundlePrice,
            Sim.BuyFood,Sim.State.Food<=int.MaxValue-WorldSimulation.FoodBundleCount);
        Purchase("储备清水",Sim.State.StationWaterPortions,WorldSimulation.StationWaterBundleCount,WorldSimulation.StationWaterBundlePrice,
            Sim.BuyStationWater,Sim.State.StationWaterPortions<=int.MaxValue-WorldSimulation.StationWaterBundleCount);
        _modalContents.AddChild(Text($"饭饭供给器容量{WorldSimulation.FoodStationCapacity}份，饮水器容量{WorldSimulation.WaterStationCapacity}份。买到的是空器具；放在干燥空地，前方留出取用空间。",13,"78816e"));
        void Place(string label,SupplyStationKind kind,int stock)
        {
            var button=Button(label,()=>{CloseModal();ArmSupplyStationPlacement(kind);});
            button.Disabled=stock<=0;_modalContents.AddChild(button);
        }
        Place("拿饭饭供给器 · 点击地面放置",SupplyStationKind.Food,Sim.State.FoodStationStock);
        Place("拿饮水器 · 点击地面放置",SupplyStationKind.Water,Sim.State.WaterStationStock);
        var refill=Button("拿补给用品 · 对准器具按住补充",()=>{CloseModal();SelectQuickTool(InteractionAction.RefillSupplyStation);});
        refill.Disabled=SupplyStationRefillStockMissing();_modalContents.AddChild(refill);
        _modalContents.AddChild(Button("返回小院",CloseModal));
    }
}

using Godot;
using System;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private string? _selectedSupplyStationId;
    private WorldState? _supplyStationSelectionWorld;
    private VBoxContainer _supplyStationPanel=null!;
    private Label _supplyStationDescription=null!,_supplyStationInstructions=null!;

    private static string SupplyStationName(SupplyStationKind kind)=>kind==SupplyStationKind.Food?"饭饭供给器":"饮水器";
    private int SupplyStationRefillStock(SupplyStationKind kind)=>kind==SupplyStationKind.Food?Sim.State.Food:Sim.State.StationWaterPortions;

    private void BuildSupplyStationPanel(Control parent)
    {
        _supplyStationPanel=Column(parent,7);_supplyStationPanel.Visible=false;
        _supplyStationDescription=Text("",14);_supplyStationPanel.AddChild(_supplyStationDescription);
        _supplyStationInstructions=Text("",12,"78816e");_supplyStationPanel.AddChild(_supplyStationInstructions);
        var frequency=new OptionButton();foreach(var label in new[]{"停止补充","每10秒","每30秒","每60秒","每120秒"})frequency.AddItem(label);
        frequency.ItemSelected+=index=>{if(_selectedSupplyStationId is { } id)Sim.SetStationRefillInterval(id,new[]{0,10,30,60,120}[(int)index]);};
        _stationFrequency=frequency;_supplyStationPanel.AddChild(frequency);
        _supplyStationPanel.AddChild(Button("用空手搬动",()=>SelectQuickTool(InteractionAction.Lift)));
    }
    private OptionButton _stationFrequency=null!;
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
        ClearShelterSelection();
        if(cancelGesture)CancelGesture();
        _selectedDetachedStemEventId=null;
        _supplyStationSelectionWorld=Sim.State;_selectedSupplyStationId=id;
        Sim.State.Mode=PlayMode.Management;
        _message="正在观察"+SupplyStationName(station.Kind)+"。可设置仓库补充频率。";
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
            _hint.Text="观察不移动器具；使用空手抓取搬动。";
        }
        int total=station.StoredPortions+station.OutletPortions;
        int stock=SupplyStationRefillStock(station.Kind);
        _supplyStationDescription.Text=$"仓内 {station.StoredPortions} 份 · 出料口 {station.OutletPortions} 份\n"+
            $"总量 {total}/{station.Capacity} · 可补充库存 {stock} 份\n"+
            (station.HeldByHand?"正在手中搬动":total==0?"已经空仓，需要补充":"食水从前方出口逐份取用")+
            (string.IsNullOrEmpty(station.LastEvent)?"":"\n"+station.LastEvent);
        var source=Sim.State.SupplyStations.Find(s=>s.Id==station.Id)!;
        _stationFrequency.Select(Array.IndexOf(new[]{0,10,30,60,120},source.RefillIntervalSeconds));
        _supplyStationInstructions.Text="按频率从仓库转入食水。没有库存时停止补充，不自动花钱。";
    }
    private void ShowSupplyStationSupplies(string? result=null)=>ShowShop(result,"食水供给设施");
}

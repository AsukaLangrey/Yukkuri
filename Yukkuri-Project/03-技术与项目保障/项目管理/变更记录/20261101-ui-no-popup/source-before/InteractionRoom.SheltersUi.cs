using Godot;
using System;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private string? _selectedShelterId;
    private WorldState? _shelterSelectionWorld;
    private VBoxContainer _shelterPanel=null!;
    private Label _shelterDescription=null!,_shelterInstructions=null!;
    private Button _shelterOpenButton=null!,_shelterCloseButton=null!,_shelterCallButton=null!;

    private static string ShelterPhaseName(ShelterVisitPhase phase)=>phase switch
    {
        ShelterVisitPhase.Approaching=>"正在走向门口",ShelterVisitPhase.Entering=>"正在进门",
        ShelterVisitPhase.Inside=>"正在屋里",ShelterVisitPhase.Exiting=>"正在出门",_=>"空着"
    };

    private void ClearShelterSelection()=>_selectedShelterId=null;
    private void NormalizeShelterSelection()
    {
        if(!ReferenceEquals(_shelterSelectionWorld,Sim.State))
        {_selectedShelterId=null;_shelterSelectionWorld=Sim.State;}
        if(_selectedShelterId is { } id&&
            (Sim.ShelterStatus(id) is not { } shelter||shelter.RegionId!=Sim.State.ActiveRegionId))
            _selectedShelterId=null;
    }
    private void SelectShelter(string id,bool cancelGesture=true)
    {
        var shelter=Sim.ShelterStatus(id);
        if(shelter==null||shelter.RegionId!=Sim.State.ActiveRegionId)return;
        if(cancelGesture)CancelGesture();
        ClearSupplyStationSelection();_selectedDetachedStemEventId=null;
        _selectedRoomObjectId=null;_observedStemMotherId=null;
        _shelterSelectionWorld=Sim.State;_selectedShelterId=id;
        Sim.State.Mode=PlayMode.Management;
        _message="正在观察这间小家。开闭门帘或呼唤需要实际点击场景中的小家。";
        RefreshRoomUi();
    }
    private void BuildShelterPanel(Control parent)
    {
        _shelterPanel=Column(parent,7);_shelterPanel.Visible=false;
        _shelterDescription=Text("",14);_shelterPanel.AddChild(_shelterDescription);
        _shelterInstructions=Text("",12,"78816e");_shelterPanel.AddChild(_shelterInstructions);
        _shelterOpenButton=Button("开门帘 · 再点击门边",()=>SelectQuickTool(InteractionAction.OpenShelterDoor));
        _shelterCloseButton=Button("关门帘 · 再点击门边",()=>SelectQuickTool(InteractionAction.CloseShelterDoor));
        _shelterCallButton=Button("轻声呼唤 · 再点击小家",()=>SelectQuickTool(InteractionAction.CallFromShelter));
        _shelterPanel.AddChild(_shelterOpenButton);_shelterPanel.AddChild(_shelterCloseButton);_shelterPanel.AddChild(_shelterCallButton);
        _shelterPanel.AddChild(Button("购买与摆放小家",()=>ShowShelterSupplies()));
    }
    private void RefreshShelterUi()
    {
        NormalizeShelterSelection();
        var shelter=_selectedShelterId is { } id?Sim.ShelterStatus(id):null;
        _shelterPanel.Visible=shelter!=null;
        if(shelter==null)return;
        _detachedStemPanel.Visible=_supplyStationPanel.Visible=false;
        _creatureVitalMeters.Visible=_condition.Visible=_memory.Visible=_memoryHeading.Visible=false;
        _familyPanelButton.Disabled=_closeButton.Disabled=true;_who.Text="小家与藏身处";
        _immediateShelter.Visible=false;
        string? guestId=shelter.OccupantId??shelter.ReservedById;
        string guest=guestId==null?"还没有住户":Sim.State.Creatures.Find(c=>c.Id==guestId)?.Name??"住户";
        string phase=shelter.Phase==ShelterVisitPhase.Empty&&shelter.OccupantId!=null?"身体仍占用屋内":ShelterPhaseName(shelter.Phase);
        _shelterDescription.Text=$"门帘{(shelter.DoorOpen?"开着":"关着")} · {phase}\n{guest}\n"+
            (shelter.ExitRequested?"正在等待走出来。\n":"")+
            (shelter.BlockedSeconds>0?$"出入受阻 {shelter.BlockedSeconds:0.0} 秒。\n":"")+shelter.LastEvent;
        _shelterInstructions.Text="开闭门帘：拿工具，再点门帘或门边；屋顶与侧墙只用于观察。\n"+
            "呼唤：拿说话工具，再点小家，等它决定并实际走出。门口堵住时先移开障碍。"+
            (string.IsNullOrEmpty(shelter.RefusalReason)?"":"\n"+shelter.RefusalReason)+
            (shelter.CanCall||shelter.CallRefusalReason==shelter.RefusalReason?"":"\n呼唤："+shelter.CallRefusalReason);
        _shelterOpenButton.Disabled=!shelter.CanOpen;
        _shelterCloseButton.Disabled=!shelter.CanClose;
        _shelterCallButton.Disabled=!shelter.CanCall;
        _speechLabel.Text="小家 · "+shelter.LastEvent;
        if(!_toolArmed)
        {_title.Text="空手 · 观察小家";_hint.Text="点击小家查看住户与门口情况。开门或呼唤请先拿对应工具，再点击实际位置。";}
    }
    private void ShowShelterSupplies(string? result=null)
    {
        OpenModal("小家与藏身处");
        _modalContents.AddChild(Text("留出门口，等它自己走进去",20));
        _modalContents.AddChild(Text($"待摆放小家：{Sim.State.ShelterStock} 间"));
        _modalContents.AddChild(Text($"现有金钱：{Sim.State.Money} 元；摆放消耗已购入的小家。"));
        if(!string.IsNullOrEmpty(result))_modalContents.AddChild(Text(result,14));
        string cost=$"{WorldSimulation.ShelterPrice} 元";
        var buy=Button("购买一间 · "+cost,()=>
        {
            bool success=Sim.BuyShelter();
            ShowShelterSupplies(success?"小家已入库，拿起来后点击空地摆放。":"未能购买，请检查金钱和库存。原有用品没有减少。");
            RefreshSupplyStock();
        });
        buy.Disabled=Sim.State.ShelterStock>=99||Sim.State.Money<WorldSimulation.ShelterPrice;
        _modalContents.AddChild(buy);
        var place=Button("拿小家 · 点击地面摆放",()=>{CloseModal();ArmShelterPlacement();});
        place.Disabled=Sim.State.ShelterStock<=0;_modalContents.AddChild(place);
        _modalContents.AddChild(Text("一间小家同时留给一只油。需要干燥空地和畅通门口，摆好后油会按自己的需要寻找藏身处。门口受阻时先移开障碍；开门与呼唤后仍需等它实际走出来。",13,"78816e"));
        _modalContents.AddChild(Button("返回小院",CloseModal));
    }
}

using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private string? _selectedShelterId;
    private WorldState? _shelterSelectionWorld;
    private VBoxContainer _shelterPanel=null!;
    private Label _shelterDescription=null!,_shelterInstructions=null!;
    private VBoxContainer _shelterResidentChoices=null!;
    private string _residentChoiceSignature="";
    private Button _shelterOpenButton=null!,_shelterCloseButton=null!,_shelterCallButton=null!,_shelterShopButton=null!;

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
        if(InspectingRecycleBox)CloseActivityPanel();
        var shelter=Sim.ShelterStatus(id);
        if(shelter==null||shelter.RegionId!=Sim.State.ActiveRegionId)return;
        if(cancelGesture)CancelGesture();
        ClearSupplyStationSelection();_selectedDetachedStemEventId=null;
        _selectedRoomObjectId=null;_observedStemMotherId=null;
        _shelterSelectionWorld=Sim.State;_selectedShelterId=id;
        Sim.State.SelectedId=null;_selectedRoomObjectId=_observedStemMotherId=null;
        Sim.State.Mode=PlayMode.Management;
        _message="正在观察这间小家。开闭门帘或呼唤需要实际点击场景中的小家。";
        RefreshRoomUi();
    }
    private void BuildShelterPanel(Control parent)
    {
        _shelterPanel=Column(parent,7);_shelterPanel.Visible=false;
        _shelterDescription=Text("",14);_shelterPanel.AddChild(_shelterDescription);
        _shelterResidentChoices=Column(_shelterPanel,4);
        _shelterInstructions=Text("",12,"78816e");_shelterPanel.AddChild(_shelterInstructions);
        _shelterOpenButton=PanelButton("开门帘 · 再点击门边",()=>SelectQuickTool(InteractionAction.OpenShelterDoor));
        _shelterCloseButton=PanelButton("关门帘 · 再点击门边",()=>SelectQuickTool(InteractionAction.CloseShelterDoor));
        _shelterCallButton=PanelButton("轻声呼唤 · 再点击小家",()=>SelectQuickTool(InteractionAction.CallFromShelter));
        _shelterPanel.AddChild(_shelterOpenButton);_shelterPanel.AddChild(_shelterCloseButton);_shelterPanel.AddChild(_shelterCallButton);
        _shelterShopButton=PanelButton("购买与摆放小家",()=>ShowShelterSupplies());_shelterPanel.AddChild(_shelterShopButton);
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
        string door=shelter.DoorProgress>=.999?"开着":shelter.DoorProgress<=.001?"关着":shelter.DoorOpen?"正在打开":"正在关闭";
        string guest=guestId==null?"还没有住户":Sim.State.Creatures.Find(c=>c.Id==guestId)?.Name??"住户";
        string phase=shelter.Phase==ShelterVisitPhase.Empty&&shelter.OccupantId!=null?"身体仍占用屋内":ShelterPhaseName(shelter.Phase);
        _shelterDescription.Text=$"门帘{door} · {phase}\n{guest}\n"+
            (shelter.ExitRequested?"正在等待走出来。\n":"")+
            (shelter.BlockedSeconds>0?$"出入受阻 {shelter.BlockedSeconds:0.0} 秒。\n":"")+shelter.LastEvent;
        if(shelter.Capacity==5)
        {
            string Names(string[] ids)=>string.Join("、",ids.Select(id=>Sim.State.Creatures.Find(c=>c.Id==id)?.Name??id));
            _who.Text="油库里小屋";
            _shelterDescription.Text=$"已入住 {shelter.OccupantIds.Length}/5 · {shelter.Comfort}\n"+
                $"门帘{door}\n"+
                (shelter.OccupantIds.Length>0?"住户："+Names(shelter.OccupantIds)+"\n":"")+
                (shelter.IncomingIds.Length>0?"进出途中："+Names(shelter.IncomingIds)+"\n":"")+
                "1–2只最舒适 · 3只适用\n4只拥挤 · 5只极度拥挤\n"+shelter.LastEvent;
        }
        _shelterInstructions.Text="开闭门帘：拿工具，再点门帘或门边；屋顶与侧墙只用于观察。\n"+
            "呼唤：拿说话工具，再点小家，等它决定并实际走出。门口堵住时先移开障碍。"+
            (string.IsNullOrEmpty(shelter.RefusalReason)?"":"\n"+shelter.RefusalReason)+
            (shelter.CanCall||shelter.CallRefusalReason==shelter.RefusalReason?"":"\n呼唤："+shelter.CallRefusalReason);
        foreach(var button in new[]{_shelterOpenButton,_shelterCloseButton,_shelterCallButton,_shelterShopButton})button.Visible=shelter.Capacity!=5;
        if(shelter.Capacity==5)_shelterInstructions.Text="门帘随进出自动开合。点击住户名字查看状态。";
        _shelterOpenButton.Disabled=!shelter.CanOpen;
        _shelterCloseButton.Disabled=!shelter.CanClose;
        _shelterCallButton.Disabled=!shelter.CanCall;
        string signature=shelter.Id+":"+string.Join(",",shelter.OccupantIds);
        if(signature!=_residentChoiceSignature)
        {
            _residentChoiceSignature=signature;ClearChildren(_shelterResidentChoices);
            foreach(var residentId in shelter.OccupantIds)
            {
                var resident=Sim.State.Creatures.Find(c=>c.Id==residentId);if(resident==null)continue;
                string selectedId=resident.Id;_shelterResidentChoices.AddChild(PanelButton("查看住户 · "+resident.Name,()=>SelectOil(selectedId)));
            }
        }
        _speechLabel.Text="小家 · "+shelter.LastEvent;
        if(!_toolArmed)
        {_title.Text="空手 · 观察小家";_hint.Text="点击小家查看住户与门口情况。开门或呼唤请先拿对应工具，再点击实际位置。";}
    }
    private void ShowShelterSupplies(string? result=null)
    {
        OpenActivityPanel("小家与藏身处",() => ShowShelterSupplies());
        _panelContents.AddChild(Text("留出门口，等它自己走进去",20));
        _panelContents.AddChild(Text($"待摆放小家：{Sim.State.ShelterStock} 间"));
        _panelContents.AddChild(Text($"现有金钱：{Sim.State.Money} 元；摆放消耗已购入的小家。"));
        string? unlockReason=Sim.ShopUnlockReason("shelter");
        if(unlockReason!=null)_panelContents.AddChild(Text(unlockReason,14));
        if(!string.IsNullOrEmpty(result))_panelContents.AddChild(Text(result,14));
        string cost=$"{WorldSimulation.ShelterPrice} 元";
        var buy=PanelButton("购买一间 · "+cost,()=>
        {
            bool success=Sim.BuyShelter();
            ShowShelterSupplies(success?"小家已入库，拿起来后点击空地摆放。":
                Sim.ShopUnlockReason("shelter")??(Sim.State.ShelterStock>=99?"待摆放小家库存已满（99间），未扣款。":
                Sim.State.Money<WorldSimulation.ShelterPrice?$"余额不足，购买一间需要 {WorldSimulation.ShelterPrice} 元，未扣款。":"当前无法购买，未扣款。"));
            RefreshSupplyStock();
        });
        buy.Disabled=unlockReason!=null||Sim.State.ShelterStock>=99||Sim.State.Money<WorldSimulation.ShelterPrice;
        _panelContents.AddChild(buy);
        var place=PanelButton("拿小家 · 点击地面摆放",()=>{CloseActivityPanel();ArmShelterPlacement();});
        place.Disabled=Sim.State.ShelterStock<=0;_panelContents.AddChild(place);
        _panelContents.AddChild(Text(Sim.State.EconomyProgression?
            "小屋最多住5只：1–2只最舒适，3只适用，4只拥挤，5只极度拥挤。夜晚油会找屋睡觉，露宿会逐渐降低幸福感。需要干燥空地与畅通门口。":
            "旧式小家同时留给一只油。需要干燥空地和畅通门口，油会按需要寻找藏身处。",13,"78816e"));
        _panelContents.AddChild(PanelButton("返回小院",CloseActivityPanel));
    }
}

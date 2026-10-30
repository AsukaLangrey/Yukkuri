using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private string? _selectedDetachedStemEventId;
    private WorldState? _detachedSelectionWorld;
    private VBoxContainer _detachedStemPanel=null!;
    private Label _detachedStemDescription=null!,_detachedStemInstructions=null!;
    private Button _detachedBottleButton=null!,_detachedMoveButton=null!,_detachedClearButton=null!,_detachedParentButton=null!;
    private Label _memoryHeading=null!;
    private Button _familyPanelButton=null!;

    private void BuildDetachedStemPanel(Control parent)
    {
        _detachedStemPanel=Column(parent,7);_detachedStemPanel.Visible=false;
        _detachedStemDescription=Text("",14);_detachedStemPanel.AddChild(_detachedStemDescription);
        _detachedStemInstructions=Text("",12,"78816e");_detachedStemPanel.AddChild(_detachedStemInstructions);
        _detachedBottleButton=Button("拿空瓶 · 按住茎装瓶",()=>
        {
            if(Sim.State.StemVessels>0)SelectQuickTool(InteractionAction.BottleStem);
            else ShowStemVessels();
        });_detachedStemPanel.AddChild(_detachedBottleButton);
        _detachedMoveButton=Button("手掌 · 拖动整瓶",()=>SelectQuickTool(InteractionAction.Lift));
        _detachedStemPanel.AddChild(_detachedMoveButton);
        _detachedClearButton=Button("收拾残留 · 对准后按住",()=>SelectQuickTool(InteractionAction.ClearStem));
        _detachedStemPanel.AddChild(_detachedClearButton);
        _detachedParentButton=Button("查看母体",()=>
        {
            if(_selectedDetachedStemEventId is { } id&&Sim.DetachedStemStatus(id) is { } stem)
                SelectOil(stem.MotherId);
        });_detachedStemPanel.AddChild(_detachedParentButton);
    }

    private void NormalizeDetachedStemSelection()
    {
        if(!ReferenceEquals(_detachedSelectionWorld,Sim.State))
        {_selectedDetachedStemEventId=null;_detachedSelectionWorld=Sim.State;}
        if(_selectedDetachedStemEventId is { } id&&
            (Sim.DetachedStemStatus(id) is not { } stem||stem.RegionId!=Sim.State.ActiveRegionId))
            _selectedDetachedStemEventId=null;
    }

    // The scene selects the independent object. The mother's existing creature
    // selection is retained only as a place to return to, never as its target.
    private void SelectDetachedStem(string eventId,bool cancelGesture=true)
    {
        var stem=Sim.DetachedStemStatus(eventId);
        if(stem==null||stem.RegionId!=Sim.State.ActiveRegionId)return;
        if(cancelGesture)CancelGesture();
        _detachedSelectionWorld=Sim.State;_selectedDetachedStemEventId=eventId;
        Sim.State.Mode=PlayMode.Management;
        _message=stem.Cleared?"正在查看已收拾的离体茎记录。":"正在观察这一窝离体茎。操作需要实际接触。";
        RefreshRoomUi();
    }

    private void RefreshDetachedStemUi()
    {
        NormalizeDetachedStemSelection();
        var stem=_selectedDetachedStemEventId is { } id?Sim.DetachedStemStatus(id):null;
        bool selected=stem!=null;
        bool hasCreature=Sim.Selected!=null;
        _detachedStemPanel.Visible=selected;_creatureVitalMeters.Visible=!selected&&hasCreature;
        _condition.Visible=_memory.Visible=_memoryHeading.Visible=!selected&&hasCreature;
        _familyPanelButton.Disabled=selected;_closeButton.Disabled=selected||!hasCreature;
        if(stem==null)return;
        string mother=Sim.State.Creatures.Find(c=>c.Id==stem.MotherId)?.Name??"母体";
        string phase=stem.Phase switch
        {
            DetachedStemPhase.Loose=>"离体 · 等待装瓶",
            DetachedStemPhase.Bottled=>"瓶中养护",
            DetachedStemPhase.Failed=>"孕育已停止",
            _=>"幼体已出生"
        };
        if(stem.Cleared)phase+=" · 已收拾";
        _who.Text=$"{mother}的离体茎";
        _detachedStemDescription.Text=$"{phase}\n幼芽 {stem.PlannedChildren} 个 · 发育 {stem.Progress*100:0}%\n"+
            $"完整度 {stem.Integrity:0}/100\n含水 {stem.Hydration:0}/100 · 营养 {stem.Nutrition:0}/100"+
            (stem.Phase==DetachedStemPhase.Bottled?$"\n瓶体完整 {stem.VesselIntegrity:0}/100":"")+
            (string.IsNullOrEmpty(stem.LastDamageCause)?"":"\n最近受损："+stem.LastDamageCause);
        bool alive=!stem.Cleared&&(stem.Phase is DetachedStemPhase.Loose or DetachedStemPhase.Bottled);
        _detachedStemInstructions.Text=!alive?(stem.Cleared?"残留已收拾。亲缘、出生或损失记录仍保留。":"这窝的结果已经确定。可拿收拾工具，对准残留按住清理；亲缘与经历仍保留。"):
            stem.Phase==DetachedStemPhase.Loose?"先拿空瓶，对准离体茎按住装瓶。离体后仍在消耗水分和营养。":
            "空手拿起地面水杯或饭饭，再把实际食水按住递到瓶口。搬动时拖整瓶，放到干燥空处。";
        if(alive&&!string.IsNullOrEmpty(stem.RefusalReason))
            _detachedStemInstructions.Text+="\n"+stem.RefusalReason;
        _detachedBottleButton.Visible=alive&&(stem.Phase==DetachedStemPhase.Loose||stem.VesselIntegrity<=25);
        _detachedBottleButton.Text=Sim.State.StemVessels>0?$"拿空瓶 · 库存 {Sim.State.StemVessels}":"购买空瓶";
        _detachedBottleButton.TooltipText="拿工具后仍需对准这一窝连续接触；成功装瓶才消耗一个空瓶。";
        _detachedMoveButton.Visible=alive&&stem.Phase==DetachedStemPhase.Bottled;
        _detachedClearButton.Visible=stem.CanClear;
        _detachedParentButton.Text="查看母体 · "+mother;
        if(!_toolArmed&&!_waterRescueToolArmed&&_heldSupplyId==null)
        {
            _title.Text=stem.HeldByHand?"托着整瓶":"空手 · 观察离体茎";
            _hint.Text=alive?"点击茎或瓶观察；整瓶可按住拖动，食水需拿起实物后递到瓶口。":
                stem.Cleared?"已收拾的记录只供查看。":"拿收拾工具，对准残留按住清理。";
        }
        _speechLabel.Text="离体茎 · "+phase;
    }

    private void ShowStemVessels(string? result=null)
    {
        OpenModal("瓶养用品");
        _modalContents.AddChild(Text($"空瓶 · 每个 {WorldSimulation.StemVesselPrice} 元",20));
        _modalContents.AddChild(Text("拿空瓶对准离体茎，按住完成装瓶。成功后消耗一个真实空瓶；水和营养需要继续补给。"));
        _modalContents.AddChild(Text(Sim.State.UnlimitedResources?"测试阶段购买不扣钱，装瓶仍消耗库存。":$"现有金钱：{Sim.State.Money} 元"));
        _modalContents.AddChild(Text($"空瓶库存：{Sim.State.StemVessels}"));
        if(!string.IsNullOrEmpty(result))_modalContents.AddChild(Text(result,14));
        var buy=Button("购买一个空瓶",()=>
        {
            bool purchased=Sim.BuyStemVessel();
            ShowStemVessels(purchased?"空瓶已入库。":"未能购买，请检查金钱和库存。已持有的用品不会减少。");
        });
        buy.Disabled=Sim.State.StemVessels>=99||!Sim.State.UnlimitedResources&&Sim.State.Money<WorldSimulation.StemVesselPrice;
        _modalContents.AddChild(buy);
        var equip=Button("拿起空瓶",()=>{CloseModal();SelectQuickTool(InteractionAction.BottleStem);});
        equip.Disabled=Sim.State.StemVessels<=0;_modalContents.AddChild(equip);
        _modalContents.AddChild(Button("返回小院",CloseModal));
    }
}

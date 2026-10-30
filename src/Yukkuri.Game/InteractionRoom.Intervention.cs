using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private Button _immediateRescue=null!,_immediateStopThreat=null!,_immediateCare=null!,_immediateRelease=null!,_immediateDry=null!;
    private Label _immediateThreatDescription=null!;
    private Button _yardHelpButton=null!;
    private Label _stemCareDescription=null!;
    private Button _stemCareButton=null!;
    private string? _yardHelpTargetId;

    private void BuildYardHelpButton(Control parent)
    {
        _yardHelpButton=Button("查看求助",FocusYardHelp);
        _yardHelpButton.AddThemeFontSizeOverride("font_size",13);
        _yardHelpButton.Visible=false;parent.AddChild(_yardHelpButton);
    }

    private void RefreshYardHelpButton()
    {
        if(_yardHelpButton==null)return;
        var requests=Sim.InteractionHelpRequests();
        var request=requests.Count>0?requests[0]:null;
        var target=request?.Target;
        _yardHelpTargetId=target?.Id;
        _yardHelpButton.Visible=target!=null;
        if(target==null)return;
        string reason=request?.Kind==InteractionHelpKind.Critical?"急需照护":
            request?.Kind==InteractionHelpKind.Water?"落水求助":"受到威胁";
        // Keep a long custom name from stretching the entire header. The full
        // name and meaning remain available in the tooltip and selected panel.
        var displayName=new System.Globalization.StringInfo(target.Name);
        string name=displayName.LengthInTextElements>8?displayName.SubstringByTextElements(0,8)+"…":target.Name;
        _yardHelpButton.Text=$"{reason} · {name}";
        _yardHelpButton.TooltipText=$"{target.Name}：{request?.Description}。只切换观察对象，不自动使用道具或解除暂停。";
    }

    private void FocusYardHelp()
    {
        if(_modalOpen)return;
        var target=Sim.CurrentInteractionHelpTarget();
        if(target==null||target.Id!=_yardHelpTargetId)
        {_message="求助情况有变化，入口已经更新。";RefreshRoomUi();return;}
        CancelGesture();
        if(DebugPanelVisible)ToggleDebugPanel();
        Sim.State.Mode=PlayMode.Management;
        _yardFocus=new Vector3((float)target.X,0,(float)target.Z);
        SelectOil(target.Id);
        _message=$"正在查看{target.Name}的求助，请先判断身体与周围情况，再选择救助。";
        RefreshRoomUi();
    }

    private void BuildImmediateInterventions(Control parent)
    {
        _immediateThreatDescription=Text("",12,"a16a51");parent.AddChild(_immediateThreatDescription);
        _immediateStopThreat=Button("制止袭击",StopCurrentThreat);parent.AddChild(_immediateStopThreat);
        _immediateRelease=Button("拿解除工具",ArmImmediateRelease);parent.AddChild(_immediateRelease);
        _immediateRescue=Button("拿救援用品",()=>
        {
            if(Sim.State.WaterRescueTools>0)ArmWaterRescueTool();
            else ShowRescueSupplies();
        });parent.AddChild(_immediateRescue);
        _immediateCare=Button("拿护理用品",()=>
        {
            if(Sim.State.UnlimitedResources||Sim.State.Medicine>0)SelectQuickTool(InteractionAction.Care);
            else ShowMedicineSupplies();
        });parent.AddChild(_immediateCare);
        _immediateDry=Button("拿毛巾 · 擦干身体",()=>SelectQuickTool(InteractionAction.Dry));parent.AddChild(_immediateDry);
        _stemCareDescription=Text("",12,"78816e");parent.AddChild(_stemCareDescription);
        _stemCareButton=Button("拿茎部支护用品",()=>
        {
            if(Sim.State.Medicine>0)SelectQuickTool(InteractionAction.SupportStem);
            else ShowMedicineSupplies(equipAction:InteractionAction.SupportStem);
        });parent.AddChild(_stemCareButton);
        _stemCareDescription.Visible=_stemCareButton.Visible=false;
        _immediateThreatDescription.Visible=_immediateStopThreat.Visible=_immediateRescue.Visible=
            _immediateCare.Visible=_immediateRelease.Visible=_immediateDry.Visible=false;
    }

    private void ArmImmediateRelease()
    {
        var c=Sim.Selected;
        if(c==null||c.RegionId!=Sim.State.ActiveRegionId||!HasVisibleFamilyBody(c)||c.Condition==PhysicalCondition.Dead)
        {RefreshRoomUi();return;}
        InteractionAction? action=c.Interaction.Restraint switch
        {
            RestraintKind.Box=>InteractionAction.OpenBox,
            RestraintKind.Wrap=>InteractionAction.Unwrap,
            RestraintKind.Hanging=>InteractionAction.Lower,
            RestraintKind.Frame=>InteractionAction.Release,
            _=>null
        };
        if(action is { } release)SelectQuickTool(release);
        else RefreshRoomUi();
    }

    private Creature? ThreatForSelected(Creature selected)=>
        Sim.CurrentInteractionThreatTarget(selected)!=null?selected:
            Sim.InteractionHelpFor(selected)?.ThreateningCreature;

    private void RefreshImmediateInterventions(Creature selected)
    {
        bool local=selected.RegionId==Sim.State.ActiveRegionId&&HasVisibleFamilyBody(selected)&&
            selected.Condition!=PhysicalCondition.Dead;
        bool inWater=local&&WaterEnvironment.IsInWater(Sim.State,selected);
        bool critical=local&&selected.Condition==PhysicalCondition.Critical;
        bool recovery=local&&selected.Interaction.Water.RecoverySeconds>0;
        _immediateRelease.Visible=(inWater||critical||recovery)&&selected.Interaction.Restraint is
            RestraintKind.Box or RestraintKind.Wrap or RestraintKind.Frame or RestraintKind.Hanging;
        _immediateRelease.Text=selected.Interaction.Restraint switch
        {
            RestraintKind.Box=>"开箱工具 · 点击箱子",
            RestraintKind.Wrap=>"解包工具 · 点击包裹",
            RestraintKind.Hanging=>"托住解下 · 点击支架",
            _=>"解除工具 · 点击固定具"
        };
        _immediateRelease.TooltipText="只拿出对应工具；仍需在场景中接触装置，解除后再救援或护理。";
        _immediateRescue.Visible=inWater;
        _immediateRescue.Text=Sim.State.WaterRescueTools>0?$"拿救援用品 · 余 {Sim.State.WaterRescueTools}":"购买救援用品";
        // Equipping is harmless while paused. Actual rescue retains its simulation pause gate.
        _immediateRescue.Disabled=false;
        _immediateCare.Visible=critical&&!inWater;
        _immediateCare.Text=Sim.State.UnlimitedResources?"拿护理用品 · 按住伤处":
            Sim.State.Medicine>0?$"拿护理用品 · 余 {Sim.State.Medicine} 包":"购买护理用品";
        _immediateCare.TooltipText="拿出后，按住适用伤处完成护理。不会自动治疗或省略接触；稳定后仍要照护。";
        _immediateDry.Visible=recovery&&!critical&&!inWater;
        _immediateDry.TooltipText="拿毛巾后按住湿处擦拭；擦干不会消除已有伤势或惊惧。";
        var stem=local?Sim.StemCareStatus(selected.Id):null;
        _stemCareDescription.Visible=_stemCareButton.Visible=stem!=null;
        if(stem!=null)
        {
            _stemCareDescription.Text=$"外茎幼芽 {stem.PlannedChildren} 个 · 发育 {stem.Progress*100:0}%\n茎部完整 {stem.Integrity:0}/100 · 含水 {stem.Hydration:0}/100"+
                (stem.SupportRemaining>0?$"\n支护余下 {System.Math.Ceiling(stem.SupportRemaining):0} 秒":"\n需要时可按住外茎完成支护。")+
                (string.IsNullOrEmpty(stem.LastDamageCause)?"":"\n最近受损："+stem.LastDamageCause);
            _stemCareButton.Text=Sim.State.Medicine>0?"拿茎部支护用品 · 按住外茎":"购买茎部支护用品";
            _stemCareButton.Disabled=false;
            _stemCareButton.TooltipText=stem.CanSupport?"按住外茎1.6秒；成功支护消耗1包真实库存。":stem.RefusalReason??"当前不能支护。";
        }
        var aggressor=local?ThreatForSelected(selected):null;
        _immediateStopThreat.Visible=_immediateThreatDescription.Visible=aggressor!=null;
        if(aggressor==null)return;
        var target=Sim.CurrentInteractionThreatTarget(aggressor);
        bool consumingRemains=target?.Condition==PhysicalCondition.Dead;
        _immediateThreatDescription.Text=consumingRemains?
            $"它正在啃食{target!.Name}的残躯，可以制止后隔开并补充食物。":aggressor==selected?
            $"它正威胁{target?.Name??"同伴"}，可以及时制止并分开。":
            $"{aggressor.Name}正威胁它，可以先制止，再检查伤势。";
        _immediateStopThreat.Text=consumingRemains?"制止啃食":aggressor==selected?"制止它的袭击":"制止对它的袭击";
        _immediateStopThreat.Disabled=Sim.State.Settings.Pause;
        _immediateStopThreat.TooltipText=Sim.State.Settings.Pause?"先继续小院时间，再出面制止。":"停止当前危险行为；仍需解决饥饿、伤势和关系。";
    }

    private void StopCurrentThreat()
    {
        var selected=Sim.Selected;
        var aggressor=selected==null?null:ThreatForSelected(selected);
        if(aggressor==null){_message="当前没有正在进行的袭击。";RefreshRoomUi();return;}
        CancelGesture();
        _message=Sim.InterruptInteractionThreat(aggressor.Id);
        RefreshRoomUi();
    }
}

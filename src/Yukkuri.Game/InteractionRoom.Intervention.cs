using Godot;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private Button _immediateRescue=null!,_immediateStopThreat=null!;
    private Label _immediateThreatDescription=null!;

    private void BuildImmediateInterventions(Control parent)
    {
        _immediateThreatDescription=Text("",12,"a16a51");parent.AddChild(_immediateThreatDescription);
        _immediateStopThreat=Button("制止袭击",StopCurrentThreat);parent.AddChild(_immediateStopThreat);
        _immediateRescue=Button("拿救援用品",()=>
        {
            if(Sim.State.WaterRescueTools>0)ArmWaterRescueTool();
            else ShowRescueSupplies();
        });parent.AddChild(_immediateRescue);
        _immediateThreatDescription.Visible=_immediateStopThreat.Visible=_immediateRescue.Visible=false;
    }

    private static string? CurrentThreatTarget(Creature actor)=>actor.Family.ChildThreat.TargetId??
        actor.Personality.HungerTargetId??actor.Family.Conflict.TargetId;

    private bool IsAvailableThreatActor(Creature actor)=>actor.Condition==PhysicalCondition.Active&&
        actor.RegionId==Sim.State.ActiveRegionId&&HasVisibleFamilyBody(actor)&&
        !actor.Interaction.Carried&&actor.Interaction.Restraint==RestraintKind.None&&
        _actors.TryGetValue(actor.Id,out var view)&&view.Visible;

    private Creature? ThreatForSelected(Creature selected)
    {
        if(IsAvailableThreatActor(selected)&&CurrentThreatTarget(selected)!=null)return selected;
        return Sim.State.Creatures.FirstOrDefault(actor=>actor!=selected&&IsAvailableThreatActor(actor)&&
            CurrentThreatTarget(actor)==selected.Id);
    }

    private void RefreshImmediateInterventions(Creature selected)
    {
        bool local=selected.RegionId==Sim.State.ActiveRegionId&&HasVisibleFamilyBody(selected)&&
            selected.Condition!=PhysicalCondition.Dead;
        bool inWater=local&&WaterEnvironment.IsInWater(Sim.State,selected);
        _immediateRescue.Visible=inWater;
        _immediateRescue.Text=Sim.State.WaterRescueTools>0?$"拿救援用品 · 余 {Sim.State.WaterRescueTools}":"购买救援用品";
        // Equipping is harmless while paused. Actual rescue retains its simulation pause gate.
        _immediateRescue.Disabled=false;
        var aggressor=local?ThreatForSelected(selected):null;
        _immediateStopThreat.Visible=_immediateThreatDescription.Visible=aggressor!=null;
        if(aggressor==null)return;
        string? targetId=CurrentThreatTarget(aggressor);
        var target=Sim.State.Creatures.Find(c=>c.Id==targetId);
        _immediateThreatDescription.Text=aggressor==selected?
            $"它正威胁{target?.Name??"同伴"}，可以及时制止并分开。":
            $"{aggressor.Name}正威胁它，可以先制止，再检查伤势。";
        _immediateStopThreat.Text=aggressor==selected?"制止它的袭击":"制止对它的袭击";
        _immediateStopThreat.Disabled=Sim.State.Settings.Pause;
        _immediateStopThreat.TooltipText=Sim.State.Settings.Pause?"先继续小院时间，再出面制止。":"停止当前危险行为；仍需解决饥饿、伤势和关系。";
    }

    private void StopCurrentThreat()
    {
        var selected=Sim.Selected;
        var aggressor=selected==null?null:ThreatForSelected(selected);
        if(aggressor==null){_message="当前没有正在进行的袭击。";RefreshRoomUi();return;}
        CancelGesture();
        _message=aggressor.Family.ChildThreat.TargetId!=null?
            Sim.ProtectFamilyChild(aggressor.Id):Sim.InterruptFamilyConflict(aggressor.Id);
        RefreshRoomUi();
    }
}

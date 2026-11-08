using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class Main
{
    // Commands use the logical key so virtual/remote keyboards work as well.
    // Physical-only events remain supported by the in-engine verification harness.
    private static Key ShortcutKey(InputEventKey key)=>key.Keycode!=Key.None?key.Keycode:key.PhysicalKeycode;
    private static bool MoveKeyPressed(Key key)=>Input.IsPhysicalKeyPressed(key)||Input.IsKeyPressed(key);
    private Vector3 PointerOnPlane(Vector2 point,float height)
    {
        var origin=Camera.ProjectRayOrigin(point);var direction=Camera.ProjectRayNormal(point);
        if(Math.Abs(direction.Y)<.001)return new(0,height,0);
        return origin+direction*((height-origin.Y)/direction.Y);
    }
    private Vector3 HoldPoint(Vector2 point)
    {
        var position=PointerOnPlane(point,_holdHeight);
        return new(Math.Clamp(position.X,-8,8),_holdHeight,Math.Clamp(position.Z,-8,8));
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(!_ready)return;
        if(Sim.State.MilestoneScenario&&HandleMilestoneInput(input))return;
        if(input is InputEventKey key&&key.Pressed&&!key.Echo)
        {
            switch(ShortcutKey(key))
            {
                case Key.Tab: HumanMode();break;
                case Key.E: CloseUp();break;
                case Key.Escape: if(Sim.State.Mode==PlayMode.CloseUp)ExitCloseUp();else {_buildKind=null;_foodInHand=false;_welcome.Visible=false;}break;
                case Key.F5: Save(_slot);break;
                case Key.F9: Load(_slot);break;
                case Key.O: Sim.Observe();break;
                case Key.F:
                    if(Sim.State.Settings.Pause)Toast("先继续时间，再喂食。");
                    else if(Sim.State.Mode==PlayMode.CloseUp)_foodInHand=!_foodInHand;
                    else if(Sim.Selected!=null)Sim.Feed(Sim.Selected.Id);
                    break;
                case Key.Key1: _buildKind=FacilityKind.Feeder;break;
                case Key.Key2: _buildKind=FacilityKind.Bed;break;
            }
        }
        if(CloseInteractionSuspended&&Sim.State.Mode==PlayMode.CloseUp&&input is InputEventMouseButton or InputEventMouseMotion)
        {SuspendCloseInteraction();return;}
        if(input is InputEventMouseMotion motion&&_held!=null)
            _holdTarget=HoldPoint(motion.Position);
        if(input is not InputEventMouseButton mouse)return;
        if(!mouse.Pressed&&mouse.ButtonIndex==MouseButton.Left){_held=null;return;}
        if(!mouse.Pressed)return;
        if(mouse.ButtonIndex==MouseButton.Right){_buildKind=null;_foodInHand=false;_held=null;return;}
        if(mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            _holdHeight=Math.Clamp(_holdHeight+(mouse.ButtonIndex==MouseButton.WheelUp?.15f:-.15f),.5f,2.5f);
            if(_held!=null)_holdTarget=HoldPoint(mouse.Position);
            return;
        }
        if(mouse.ButtonIndex!=MouseButton.Left)return;
        if(Sim.State.Mode==PlayMode.CloseUp)
        {
            if(Sim.Selected==null||!Actors.TryGetValue(Sim.Selected.Id,out var actor))return;
            var point=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(0,0,.45f));
            if(_foodInHand)
            {
                if(point.DistanceTo(mouse.Position)<90&&Sim.Feed(Sim.Selected.Id)){_foodInHand=false;Sound("feed");Toast("食物已接触嘴部，消耗一份。");}
                else Toast(point.DistanceTo(mouse.Position)<90?LegacyFeedFailure():"将食物移到嘴部附近再点击。");
                return;
            }
            if(Camera.UnprojectPosition(actor.GlobalPosition).DistanceTo(mouse.Position)<110)
            { _held=actor;_holdHeight=Math.Clamp(actor.Position.Y+.6f,.7f,2.5f);_holdTarget=HoldPoint(mouse.Position);actor.Sleeping=false; }
            return;
        }
        var ground=PointerOnPlane(mouse.Position,0);
        if(_buildKind!=null)
        {
            if(Sim.Place(_buildKind.Value,ground.X,ground.Z)){RebuildRegion();Sound("build");Toast("设施已放置。食盆需从库存补充。");}
            else Toast("无法建造：仅限乐园，避开已有设施，并确认资金。");
            _buildKind=null;return;
        }
        var picked=Actors.Values.MinBy(a=>Camera.UnprojectPosition(a.GlobalPosition).DistanceSquaredTo(mouse.Position));
        if(picked!=null&&Camera.UnprojectPosition(picked.GlobalPosition).DistanceTo(mouse.Position)<45)
        {Sim.Select(picked.CreatureId);Sound("select");UpdateUi();}
    }
    public override void _Input(InputEvent input)
    {
        if(_ready&&CloseInteractionSuspended)SuspendCloseInteraction();
        if(input is InputEventKey traceKey&&System.Array.Exists(OS.GetCmdlineUserArgs(),arg=>arg=="--trace-keys"))
            GD.Print($"INPUT_KEY physical={traceKey.PhysicalKeycode} logical={traceKey.Keycode} unicode={traceKey.Unicode} pressed={traceKey.Pressed} echo={traceKey.Echo}");
        if(_ready&&Sim.State.MilestoneScenario&&input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:false}&&_gestureActive)FinishGesture();
        // Releasing over a UI panel must still release the physics grab.
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:false})_held=null;
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Middle,Pressed:false})_mapDragging=false;
    }
}

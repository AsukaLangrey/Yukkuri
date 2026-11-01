using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class Main
{
    private string? _audibleHelpId;
    private float _mapZoom=25;
    private Vector3 _mapPan;
    private bool _mapDragging;
    private bool HandleMilestoneInput(InputEvent input)
    {
        if(_helpPanel.Visible){if(input is InputEventKey{Pressed:true} helpKey&&ShortcutKey(helpKey)==Key.Escape)CloseHelp();return true;}
        if(_diaryPanel.Visible){if(input is InputEventKey{Pressed:true} diaryKey&&ShortcutKey(diaryKey)==Key.Escape)CloseDiary();return true;}
        if(_welcome.Visible||_settingsPanel.Visible)
        {
            if(input is InputEventKey{Pressed:true} modalKey&&ShortcutKey(modalKey)==Key.Escape)
            {if(_settingsPanel.Visible)CloseSettings();else if(_milestoneStarted)CloseMilestoneMenu();}
            return true;
        }
        if(Sim.State.Mode!=PlayMode.CloseUp)
        {
            if(input is InputEventMouseButton mapMouse)
            {
                if(mapMouse.ButtonIndex==MouseButton.Middle){_mapDragging=mapMouse.Pressed;return true;}
                if(mapMouse.Pressed&&mapMouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
                {_mapZoom=Math.Clamp(_mapZoom+(mapMouse.ButtonIndex==MouseButton.WheelUp?-1.5f:1.5f),14,34);return true;}
            }
            if(input is InputEventMouseMotion mapMotion&&_mapDragging)
            {
                _mapPan-=(new Vector3(.8f,0,-.6f)*mapMotion.Relative.X+new Vector3(.6f,0,.8f)*mapMotion.Relative.Y)*(.025f*_mapZoom/25);
                _mapPan=new(Math.Clamp(_mapPan.X,-8,8),0,Math.Clamp(_mapPan.Z,-8,8));return true;
            }
        }
        if(input is InputEventKey {Pressed:true,Echo:false} key)
        {
            if(ShortcutKey(key)==Key.Space){Sim.State.Settings.Pause=!Sim.State.Settings.Pause;UpdateUi();return true;}
            if(ShortcutKey(key)==Key.F){MilestoneFeed();return true;}
            if(ShortcutKey(key)==Key.O){Sim.Observe();Sim.State.Progress.ResearchObservations++;UpdateUi();return true;}
            if(ShortcutKey(key)==Key.Escape)
            {
                if(Sim.State.Mode==PlayMode.CloseUp)ExitCloseUp();else if(_buildKind!=null)_buildKind=null;else OpenMilestoneMenu();
                return true;
            }
        }
        if(Sim.State.Mode!=PlayMode.CloseUp||_grabTool||_foodInHand)return false;
        if(input is InputEventMouseMotion motion)
        {
            _gesturePoint=motion.Position;
            if(_gestureActive&&TryContactPoint(motion.Position,out var area))_contactArea=area;
            return true;
        }
        if(input is InputEventMouseButton mouse)
        {
            if(mouse.ButtonIndex==MouseButton.Right){CancelGesture();return true;}
            if(mouse.ButtonIndex!=MouseButton.Left)return true;
            if(mouse.Pressed&&Sim.Selected!=null&&TryContactPoint(mouse.Position,out var area))
            {
                _gestureActive=true;_gestureDuration=0;_gestureTarget=Sim.Selected.Id;_contactArea=area;
                _gestureOrigin=mouse.Position;_gesturePoint=mouse.Position;_gestureForce=(float)_pressure.Value;
            }
            else if(!mouse.Pressed&&_gestureActive)FinishGesture();
            return true;
        }
        return false;
    }
    private bool TryContactPoint(Vector2 screen,out ContactArea area)
    {
        area=ContactArea.Body;
        if(Sim.Selected==null||!Actors.TryGetValue(Sim.Selected.Id,out var actor))return false;
        var origin=Camera.ProjectRayOrigin(screen);var direction=Camera.ProjectRayNormal(screen);
        var query=PhysicsRayQueryParameters3D.Create(origin,origin+direction*60,2);
        var hit=GetWorld3D().DirectSpaceState.IntersectRay(query);
        if(hit.Count==0||hit["collider"].AsGodotObject()!=actor)return false;
        Vector3 point=(Vector3)hit["position"]-actor.GlobalPosition;
        area=point.Y>.15f*actor.BodyScale?ContactArea.Forehead:Math.Abs(point.X)>.16f*actor.BodyScale?ContactArea.Cheek:ContactArea.Body;
        return true;
    }
    private void FinishGesture()
    {
        if(!_gestureActive)return;
        string? id=_gestureTarget;double duration=Math.Clamp(_gestureDuration,.05,6);
        _gestureActive=false;_gestureTarget=null;
        if(id==null)return;
        var result=Sim.Contact(id,new(_contactAction,_contactArea,_contactTool,_gestureForce,duration));
        if(Actors.TryGetValue(id,out var actor))
        {
            actor.SetContactPose(_contactAction,false);
            if(result.Success&&_contactAction==ContactAction.Discipline)actor.ApplyCentralImpulse(new Vector3(0,.25f,-.3f)*_gestureForce*2);
        }
        ShowResult(result);
    }
    private void CancelGesture()
    {
        _gestureActive=false;_gestureTarget=null;_gestureDuration=0;
        foreach(var actor in Actors.Values)actor.SetContactPose(_contactAction,false);
        if(_handVisual!=null)_handVisual.Visible=false;
    }
    private void ProcessMilestone(float dt)
    {
        if(Sim.State.HelpTargetId!=_audibleHelpId)
        {
            _audibleHelpId=Sim.State.HelpTargetId;
            if(_audibleHelpId!=null)Sound("coo-help");
        }
        _actorSyncTimer+=dt;
        if(_actorSyncTimer>.2)
        {
            _actorSyncTimer=0;
            string key=string.Join(",",Sim.State.Creatures.Where(c=>c.RegionId==Sim.State.ActiveRegionId).Select(c=>c.Id));
            if(key!=_visibleActorKey){_visibleActorKey=key;RebuildRegion(false);}
        }
        bool close=Sim.State.Mode==PlayMode.CloseUp;
        if(_handVisual!=null)
        {
            _handVisual.Visible=close&&_gestureActive;
            _handVisual.GetNode<Node3D>("Paddle").Visible=_contactTool==ContactTool.TrainingPaddle;
            _handVisual.GetNode<Node3D>("Glove").Visible=_contactTool==ContactTool.SoftGlove;
        }
        if(_gestureActive&&Sim.Selected!=null&&Actors.TryGetValue(Sim.Selected.Id,out var target))
        {
            bool touching=TryContactPoint(_gesturePoint,out var area);
            if(_contactAction==ContactAction.Hug)touching=true; // Captured embrace follows the lifted body.
            if(touching)
            {
                _gestureDuration=Math.Min(6,_gestureDuration+dt);_contactArea=area;
            }
            float effectiveForce=_gestureForce;
            if(Sim.State.Settings.ProtectCareTools&&_contactAction is ContactAction.Stroke or ContactAction.Hug or ContactAction.Nuzzle)effectiveForce=Math.Min(effectiveForce,.45f);
            target.SetContactPose(_contactAction,touching,effectiveForce);
            if(_handVisual!=null)
            {
                _handVisual.Visible=touching;
                Vector3 offset=_contactAction switch
                {
                    ContactAction.Hug=>new(.55f,.02f,.12f),
                    ContactAction.Nuzzle=>new(.45f,-.02f,.45f),
                    _=>_contactArea switch{ContactArea.Forehead=>new(0,.42f,.25f),ContactArea.Cheek=>new(.33f,.03f,.5f),_=>new(0,-.12f,.52f)}
                };
                _handVisual.Position=target.GlobalPosition+offset*target.BodyScale;
                _handVisual.RotationDegrees=_contactAction==ContactAction.Hug?new(0,0,75):new(-25,0,Mathf.Sin((float)_gestureDuration*6)*5);
            }
        }
        if(_employee!=null)
        {
            _employee.Visible=Sim.State.ActiveRegionId=="home"&&!close;
            if(_employee.Visible&&!Sim.State.Settings.Pause)
            {
                var residents=Sim.State.Creatures.Where(c=>c.RegionId=="home"&&!c.IsIntruder);
                var careTarget=Sim.State.Food>0?residents.Where(c=>c.Fullness<35&&c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering).MinBy(c=>c.Fullness):null;
                careTarget??=Sim.State.Medicine>0?residents.Where(c=>c.Health<80&&c.Condition!=PhysicalCondition.Dead).MinBy(c=>c.Health):null;
                bool working=Sim.ActiveRegion.StaffEnabled&&(Sim.State.Money>0||Sim.State.UnlimitedResources);
                var destination=careTarget!=null&&working?new Vector3((float)careTarget.X,0,(float)careTarget.Z+1):new Vector3(4,0,-4);
                _employee.Position=_employee.Position.MoveToward(destination,dt*.65f);
            }
        }
    }
}

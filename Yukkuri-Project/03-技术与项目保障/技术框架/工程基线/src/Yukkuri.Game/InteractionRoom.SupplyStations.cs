using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private readonly Dictionary<string,RoomSupplyStationActor> _supplyStationActors=new();
    private SupplyStationKind? _placingSupplyStationKind;
    private string? _stationContactId;
    private MeshInstance3D? _stationPlacementBody,_stationPlacementOutlet;

    private void CancelSupplyStationPlacement()
    {
        _placingSupplyStationKind=null;
        if(_stationPlacementBody!=null)_stationPlacementBody.Visible=false;
        if(_stationPlacementOutlet!=null)_stationPlacementOutlet.Visible=false;
    }
    private void ArmSupplyStationPlacement(SupplyStationKind kind)
    {
        CancelGesture();ClearSupplyStationSelection();ClearShelterSelection();_selectedDetachedStemEventId=null;
        Sim.State.Mode=PlayMode.Management;
        _action=kind==SupplyStationKind.Water?InteractionAction.PlaceWaterStation:InteractionAction.PlaceFoodStation;
        _group=InteractionCatalog.Group(_action);_directToolMode=true;
        _title.Text=_group.Name+" · "+InteractionCatalog.Choice(_action).Label;
        _hint.Text=ToolHint(_action);
        if((kind==SupplyStationKind.Water?Sim.State.WaterStationStock:Sim.State.FoodStationStock)<=0)
        {
            _toolArmed=false;
            ShowSupplyStationSupplies("还没有这种空器具，先购买，再点击摆放。");
            return;
        }
        _placingSupplyStationKind=kind;_toolArmed=true;
        _message="点击干燥空地放置器具，前方留出餐盘和取食空间；右键取消。";
        RefreshRoomUi();
    }
    private void UpdateSupplyStationPlacement()
    {
        Vector2 pointer=GetViewport().GetMousePosition();
        bool visible=_placingSupplyStationKind.HasValue&&_windowFocused&&!_modalOpen&&!_panning&&!_orbiting&&
            !Sim.State.Settings.Pause&&Sim.State.Mode!=PlayMode.CloseUp&&IsScenePoint(pointer);
        if(!visible)
        {
            if(_stationPlacementBody!=null)_stationPlacementBody.Visible=false;
            if(_stationPlacementOutlet!=null)_stationPlacementOutlet.Visible=false;
            return;
        }
        if(!TryGroundPoint(pointer,0,out var point))
        {
            if(_stationPlacementBody!=null)_stationPlacementBody.Visible=false;
            if(_stationPlacementOutlet!=null)_stationPlacementOutlet.Visible=false;
            return;
        }
        if(_stationPlacementBody==null)
        {
            float radius=(float)WorldSimulation.SupplyStationRadius;
            _stationPlacementBody=SceneArt.Mesh(this,new TorusMesh{InnerRadius=radius-.012f,OuterRadius=radius,Rings=40,RingSegments=6},Vector3.Zero,"899e90");
            _stationPlacementOutlet=SceneArt.Mesh(this,new TorusMesh{InnerRadius=.308f,OuterRadius=.32f,Rings=40,RingSegments=6},Vector3.Zero,"899e90");
            _stationPlacementBody.CastShadow=_stationPlacementOutlet.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        }
        bool clear=ContactAt(pointer)==null&&ObjectAt(pointer)==null&&
            Sim.CanPlaceSupplyStationAt(_placingSupplyStationKind!.Value,point.X,point.Z);
        _stationPlacementBody.Visible=_stationPlacementOutlet!.Visible=true;
        _stationPlacementBody.Position=point+Vector3.Up*.018f;
        _stationPlacementOutlet.Position=point+new Vector3(0,.018f,(float)WorldSimulation.SupplyStationOutletOffset);
        _stationPlacementBody.MaterialOverride=_stationPlacementOutlet.MaterialOverride=SceneArt.Mat(clear?"899e90":"b7806c");
    }
    private bool PlaceSupplyStationAt(Vector2 pointer)
    {
        if(_placingSupplyStationKind is not { } kind)return false;
        if(Sim.State.Settings.Pause||Sim.State.Mode==PlayMode.CloseUp)
        {_message="先恢复小院时间，再放置器具。";return true;}
        if(ContactAt(pointer)!=null||ObjectAt(pointer)!=null)
        {_message="请点击空地，避开身体、物品和器具。";return true;}
        if(!TryGroundPoint(pointer,0,out var ground))
        {_message="这个视角看不到地面落点。";return true;}
        var oldIds=Sim.SupplyStations(Sim.State.ActiveRegionId).Select(s=>s.Id).ToHashSet();
        var reply=Sim.PlaceSupplyStation(kind,ground.X,ground.Z);AcceptFeedback(reply);
        if(!reply.Success&&(kind==SupplyStationKind.Water?Sim.State.WaterStationStock:Sim.State.FoodStationStock)<=0)
        {ShowSupplyStationSupplies(reply.Message);return true;}
        if(reply.Success&&reply.Changed)
        {
            CancelSupplyStationPlacement();_toolArmed=false;
            SynchronizeSupplyStationActors();
            var created=Sim.SupplyStations(Sim.State.ActiveRegionId).FirstOrDefault(s=>!oldIds.Contains(s.Id));
            if(created!=null)SelectSupplyStation(created.Id);
            AcceptFeedback(reply);
        }
        RefreshRoomUi();return true;
    }
    private void ResetSupplyStationActors()
    {
        foreach(var actor in _supplyStationActors.Values)actor.QueueFree();
        _supplyStationActors.Clear();
    }
    private void SynchronizeSupplyStationActors()
    {
        var states=Sim.SupplyStations(Sim.State.ActiveRegionId);
        foreach(var id in _supplyStationActors.Keys.Where(id=>!states.Any(s=>s.Id==id)).ToArray())
        {_supplyStationActors[id].QueueFree();_supplyStationActors.Remove(id);}
        foreach(var state in states)
        {
            if(!_supplyStationActors.TryGetValue(state.Id,out var actor))
            {actor=new RoomSupplyStationActor();AddChild(actor);actor.Build(state.Kind==SupplyStationKind.Water);_supplyStationActors[state.Id]=actor;}
            actor.Visible=Sim.State.Mode!=PlayMode.CloseUp;
            actor.Present(state.X,state.Z,state.StoredPortions,state.Capacity,_selectedSupplyStationId==state.Id,state.HeldByHand);
        }
    }
    private float SupplyStationOcclusionDistance(Vector3 origin,Vector3 direction)
    {
        float nearest=float.PositiveInfinity;
        foreach(var actor in _supplyStationActors.Values)
            if(actor.RayContact(origin,direction) is { } hit)nearest=Mathf.Min(nearest,hit.Distance);
        return nearest;
    }
    private Contact? NearestSupplyStationContact(Vector3 origin,Vector3 direction,ref float maximum,out bool blocks)
    {
        Contact? best=null;blocks=false;
        foreach(var pair in _supplyStationActors)
        {
            if(pair.Value.RayContact(origin,direction) is not { } hit||hit.Distance>=maximum)continue;
            // Actual output cups/plates keep their own closer pickup target.
            bool obscured=false;
            foreach(var obj in Sim.State.RoomObjects)
            {
                if(obj.Stored||obj.HeldByHand||obj.RegionId!=Sim.State.ActiveRegionId||!ObjectHasVisibleGeometry(obj)||
                    !_objects.TryGetValue(obj.Id,out var visual)||!visual.Visible)continue;
                if(InteractionPropContact.RayHit(obj.Kind,new((float)obj.X,(float)obj.Height,(float)obj.Z),
                    new(origin.X,origin.Y,origin.Z),new(direction.X,direction.Y,direction.Z),out float distance,
                    obj.Kind==RoomObjectKind.Ball?RoomBallEnvironment.CenterHeight(Sim.State,obj):null)&&distance<hit.Distance)
                {obscured=true;break;}
            }
            maximum=hit.Distance;blocks=true;
            best=obscured?null:new(pair.Key,BodyPart.Side,hit.Point,hit.Normal,hit.Distance,false,RestraintKind.None,
                SurfaceSide.Whole,InteractionTargetKind.SupplyStation,pair.Key);
        }
        return best;
    }
    private string SupplyStationHint(Contact contact)
    {
        var state=Sim.SupplyStationStatus(contact.Id);
        if(state==null)return "这件器具已不在这里";
        string name=state.Kind==SupplyStationKind.Water?"饮水器":"食物供给器";
        string stock=$"仓内 {state.StoredPortions}/{state.Capacity} · 出口 {state.OutletPortions}";
        if(_heldSupplyId!=null)return name+" · 手中食水请递给油或培养瓶；器具用补充工具";
        if(_waterRescueToolArmed)return name+" · 救援用品请对准落水油";
        if(_toolArmed&&_action==InteractionAction.RefillSupplyStation)
            return name+" · "+stock+(state.CanRefill?" · 长按外表补充，移开重新计时":" · "+(state.RefusalReason??"当前不能补充"));
        return name+" · "+stock+" · 点击查看 / 手掌按住拖动，松开放稳";
    }
    private void BeginSupplyStationPointer(Contact contact,Vector2 pointer)
    {
        bool armed=_toolArmed,rescue=_waterRescueToolArmed;var action=armed?_action:InteractionAction.Lift;
        SelectSupplyStation(contact.Id);
        if(Sim.State.Settings.Pause||Sim.State.Mode==PlayMode.CloseUp)
        {_message="先恢复小院时间，再操作器具。";return;}
        if(rescue){_message="救援用品用于落水油，器具请用手掌或补充工具。";return;}
        if(action is not (InteractionAction.Lift or InteractionAction.RefillSupplyStation))
        {_message="这件器具只支持手掌搬动或补充食水，不作用于身后的油。";return;}
        _action=action;_toolArmed=armed;
        _gestureTargetKind=InteractionTargetKind.SupplyStation;_gestureTargetObject=contact.Id;
        _dragStart=_lastPointer=pointer;_lastGestureSampleUsec=Time.GetTicksUsec();
        _directGesture=false;_stationContactId=contact.Id;
        var reply=Sim.BeginSupplyStationGesture(contact.Id,new(action,BodyPart.Side,.2,ObjectId:contact.Id,TargetKind:InteractionTargetKind.SupplyStation));
        AcceptFeedback(reply);_gesturePose=reply.Pose;_mouseHeld=Sim.HasInteractionGesture;
        if(_mouseHeld){ShowInteractionTool();PlaceContactTool(contact);_hand.Visible=action==InteractionAction.RefillSupplyStation;}
        else {_stationContactId=null;ResetContactMotion();}
    }
    private void ProcessSupplyStationGesture(float dt,double sampleSeconds,bool releasing)
    {
        if(!_mouseHeld||!Sim.HasInteractionGesture||Sim.State.Settings.Pause||Sim.State.Mode==PlayMode.CloseUp||
            !_windowFocused||_modalOpen||(!releasing&&!Input.IsMouseButtonPressed(MouseButton.Left)))
        {CancelGesture();return;}
        ulong now=Time.GetTicksUsec();double elapsed=(now-_lastGestureSampleUsec)/1_000_000.0;_lastGestureSampleUsec=now;
        var state=_gestureTargetObject==null?null:Sim.SupplyStationStatus(_gestureTargetObject);
        if(state==null||state.RegionId!=Sim.State.ActiveRegionId){CancelGesture();return;}
        Vector2 point=GetViewport().GetMousePosition();
        var contact=IsScenePoint(point)?ContactAt(point):null;
        bool touching=contact is {TargetKind:InteractionTargetKind.SupplyStation}&&contact.Id==state.Id&&contact.ObjectId==state.Id;
        bool timely=double.IsFinite(sampleSeconds)&&sampleSeconds>0&&sampleSeconds<=ContactMotion.MaximumSampleSeconds&&
            elapsed>0&&elapsed<=ContactMotion.MaximumSampleSeconds&&float.IsFinite(dt)&&dt>0;
        bool settled=timely&&touching&&_stationContactId==state.Id;
        _stationContactId=touching?state.Id:null;
        Vector3 ground=new((float)state.X,0,(float)state.Z);
        if(_action==InteractionAction.Lift&&timely&&IsScenePoint(point)&&IsScenePoint(_lastPointer))
            ground+=SupportDrag(point-_lastPointer);
        // The first rendered frame may have started before the press. Spend at
        // most the real interval since that press/sample, never its whole dt.
        float duration=timely?(float)Math.Min(Math.Min(.1,dt),elapsed):.01f;
        var reply=Sim.ContinueInteractionGesture(new(_action,BodyPart.Side,.2,duration,X:ground.X,Z:ground.Z,
            ObjectId:state.Id,TargetKind:InteractionTargetKind.SupplyStation),settled);
        AcceptFeedback(reply);_gesturePose=reply.Pose;_lastPointer=point;
        bool completed=_action==InteractionAction.RefillSupplyStation&&reply.Success&&reply.Changed;
        ShowInteractionTool();_hand.Visible=touching&&reply.Success&&!completed&&_action==InteractionAction.RefillSupplyStation;
        if(touching&&contact!=null)PlaceContactTool(contact);
        if(completed)
        {AcceptFeedback(Sim.EndInteractionGesture());_mouseHeld=false;_hand.Visible=false;ResetContactMotion();}
        else if(!Sim.HasInteractionGesture){_mouseHeld=false;_hand.Visible=false;ResetContactMotion();}
    }
}

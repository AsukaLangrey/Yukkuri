using Godot;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private readonly Dictionary<string,ShelterActor> _shelterActors=new();
    private bool _placingShelter;
    private Node3D? _shelterPlacement;
    private readonly List<MeshInstance3D> _shelterPlacementLines=new();

    private void CancelShelterPlacement()
    {_placingShelter=false;if(_shelterPlacement!=null)_shelterPlacement.Visible=false;}
    private void ArmShelterPlacement()
    {
        CancelGesture();ClearShelterSelection();ClearSupplyStationSelection();_selectedDetachedStemEventId=null;
        Sim.State.Mode=PlayMode.Management;_action=InteractionAction.PlaceShelter;
        _group=InteractionCatalog.Group(_action);_directToolMode=true;
        _title.Text=_group.Name+" · "+InteractionCatalog.Choice(_action).Label;_hint.Text=ToolHint(_action);
        if(Sim.State.ShelterStock<=0)
        {_toolArmed=false;ShowShelterSupplies("还没有空居所，先购买，再点击地面放置。");return;}
        _placingShelter=true;_toolArmed=true;
        _message="点击干燥空地安放居所，前门朝向小院前方；留出门口通道。右键取消。";
        RefreshRoomUi();
    }
    private void UpdateShelterPlacement()
    {
        Vector2 pointer=GetViewport().GetMousePosition();
        bool visible=_placingShelter&&_windowFocused&&!_modalOpen&&!_panning&&!_orbiting&&
            !Sim.State.Settings.Pause&&Sim.State.Mode!=PlayMode.CloseUp&&IsScenePoint(pointer);
        if(!visible||!TryGroundPoint(pointer,0,out var point))
        {if(_shelterPlacement!=null)_shelterPlacement.Visible=false;return;}
        if(_shelterPlacement==null)
        {
            _shelterPlacement=new Node3D();AddChild(_shelterPlacement);
            float width=(float)ShelterGeometry.OuterWidth,depth=(float)ShelterGeometry.OuterDepth;
            foreach(float sign in new[]{-1f,1f})
            {
                _shelterPlacementLines.Add(SceneArt.Box(_shelterPlacement,new(sign*width*.5f,0,0),new(.018f,.008f,depth),"899e90"));
                _shelterPlacementLines.Add(SceneArt.Box(_shelterPlacement,new(0,0,sign*depth*.5f),new(width,.008f,.018f),"899e90"));
            }
            _shelterPlacementLines.Add(SceneArt.Box(_shelterPlacement,new(0,0,depth*.5f+.10f),new((float)ShelterGeometry.DoorWidth,.008f,.025f),"899e90"));
            foreach(var line in _shelterPlacementLines)line.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        }
        bool clear=ContactAt(pointer)==null&&ObjectAt(pointer)==null&&Sim.CanPlaceShelterAt(point.X,point.Z);
        _shelterPlacement.Visible=true;_shelterPlacement.Position=point+Vector3.Up*.018f;
        foreach(var line in _shelterPlacementLines)line.MaterialOverride=SceneArt.Mat(clear?"899e90":"b7806c");
    }
    private bool PlaceShelterAt(Vector2 pointer)
    {
        if(!_placingShelter)return false;
        if(Sim.State.Settings.Pause||Sim.State.Mode==PlayMode.CloseUp)
        {_message="先恢复小院时间，再安放居所。";return true;}
        if(ContactAt(pointer)!=null||ObjectAt(pointer)!=null)
        {_message="请点击干燥空地，避开身体、物品与建筑。";return true;}
        if(!TryGroundPoint(pointer,0,out var point)){_message="这个视角看不到地面落点。";return true;}
        var oldIds=Sim.Shelters(Sim.State.ActiveRegionId).Select(s=>s.Id).ToHashSet();
        var reply=Sim.PlaceShelter(point.X,point.Z);AcceptFeedback(reply);
        if(!reply.Success&&Sim.State.ShelterStock<=0){ShowShelterSupplies(reply.Message);return true;}
        if(reply.Success&&reply.Changed)
        {
            CancelShelterPlacement();_toolArmed=false;SynchronizeShelterActors();
            var created=Sim.Shelters(Sim.State.ActiveRegionId).FirstOrDefault(s=>!oldIds.Contains(s.Id));
            if(created!=null)SelectShelter(created.Id);
            AcceptFeedback(reply);
        }
        RefreshRoomUi();return true;
    }
    private void ResetShelterActors()
    {
        foreach(var actor in _shelterActors.Values)actor.QueueFree();
        _shelterActors.Clear();
    }
    private void SynchronizeShelterActors()
    {
        var states=Sim.Shelters(Sim.State.ActiveRegionId);
        foreach(var id in _shelterActors.Keys.Where(id=>!states.Any(s=>s.Id==id)).ToArray())
        {_shelterActors[id].QueueFree();_shelterActors.Remove(id);}
        foreach(var state in states)
        {
            if(!_shelterActors.TryGetValue(state.Id,out var actor))
            {actor=new ShelterActor();AddChild(actor);actor.Build();_shelterActors[state.Id]=actor;}
            // Unlike distant scenery, walls keep their visible and clickable
            // occlusion while looking closely at an occupant or its neighbours.
            actor.Visible=true;actor.Present(state,_selectedShelterId==state.Id);
        }
    }
    private float ShelterOcclusionDistance(Vector3 origin,Vector3 direction)
    {
        float nearest=float.PositiveInfinity;
        foreach(var actor in _shelterActors.Values)
            if(actor.RayContact(origin,direction) is { } hit)nearest=Mathf.Min(nearest,hit.Distance);
        return nearest;
    }
    private Contact? NearestShelterContact(Vector3 origin,Vector3 direction,ref float maximum,out bool blocks)
    {
        Contact? best=null;blocks=false;
        foreach(var pair in _shelterActors)
        {
            if(pair.Value.RayContact(origin,direction) is not { } hit||hit.Distance>=maximum)continue;
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
                SurfaceSide.Whole,InteractionTargetKind.Shelter,pair.Key);
        }
        return best;
    }
    private bool IsShelterDoorContact(Contact contact,ShelterView state)=>
        ShelterGeometry.DoorControlHit(contact.World.X-state.X,contact.World.Y,contact.World.Z-state.Z);
    private string ShelterHint(Contact contact)
    {
        var state=Sim.ShelterStatus(contact.Id);
        if(state==null)return "这个居所已不在这里";
        string title="居所 · "+(state.DoorOpen?"门已打开":"门帘关闭");
        if(_waterRescueToolArmed)return title+" · 救援用品请对准落水油";
        if(_heldSupplyId!=null)return title+" · 门墙挡住了食水，请从实际门洞递送";
        if(_toolArmed&&_action is InteractionAction.OpenShelterDoor or InteractionAction.CloseShelterDoor)
            return title+(IsShelterDoorContact(contact,state)?" · 点击门帘或门框拉环操作":" · 请对准前方门帘或门框拉环，侧墙和屋顶只能观察");
        if(_toolArmed&&_action==InteractionAction.CallFromShelter)
            return title+" · 点击呼唤；是否出来、通道是否畅通仍由实际状态决定";
        return title+" · 点击查看住户和入口状态";
    }
    private void BeginShelterPointer(Contact contact)
    {
        bool armed=_toolArmed,rescue=_waterRescueToolArmed;var action=_action;
        SelectShelter(contact.Id);
        if(rescue){_message="救援用品用于落水油，居所请使用开门或呼唤。";return;}
        if(!armed||action is not (InteractionAction.OpenShelterDoor or InteractionAction.CloseShelterDoor or InteractionAction.CallFromShelter))return;
        if(Sim.State.Settings.Pause){_message="先恢复小院时间，再操作门帘或呼唤。";return;}
        var state=Sim.ShelterStatus(contact.Id);
        if(state==null)return;
        if(action is InteractionAction.OpenShelterDoor or InteractionAction.CloseShelterDoor&&!IsShelterDoorContact(contact,state))
        {_message="需要实际点击前方门帘或门框拉环，不能隔着侧墙开关门。";return;}
        var reply=Sim.ExecuteShelterInteraction(contact.Id,new(action,BodyPart.Side,.2,
            X:contact.World.X,Z:contact.World.Z,Height:contact.World.Y,ObjectId:contact.Id,TargetKind:InteractionTargetKind.Shelter));
        AcceptFeedback(reply);SynchronizeShelterActors();RefreshRoomUi();
    }
}

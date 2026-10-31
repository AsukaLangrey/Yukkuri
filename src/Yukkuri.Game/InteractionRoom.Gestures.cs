using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private string? _sceneDragId,_sceneDragKind,_hoverGestureId,_companyFirst;
    private Contact? _pokeContact;
    private Vector2 _redesignStart,_redesignPrevious;
    private double _pressSeconds,_swingCooldown;
    private bool _swingArmed,_pulled;
    private string? _observedStemMotherId;
    private PanelContainer _recycleBox=null!;
    private readonly CarryReleaseMotion _portableRelease=new();
    private Vector3 _sceneDragOffset;
    private void BuildRecycleBox()
    {
        _recycleBox=Panel(_ui,"d9dfcbea");_recycleBox.AnchorLeft=_recycleBox.AnchorRight=_recycleBox.AnchorTop=_recycleBox.AnchorBottom=1;
        _recycleBox.OffsetLeft=-248;_recycleBox.OffsetRight=-24;_recycleBox.OffsetTop=-130;_recycleBox.OffsetBottom=-28;
        _recycleBox.AddChild(Text("收容箱\n拖入回收 · 不退钱",18));_recycleBox.Visible=false;
    }
    private bool HandleRedesignRelease(InputEvent input)
    {
        if(input is not InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:false} mouse)return false;
        string? recycleId=_sceneDragId??_heldSupplyId??Sim.InteractionGestureObject??Sim.InteractionGestureTarget;
        if(_recycleBox.Visible&&_recycleBox.GetGlobalRect().HasPoint(mouse.Position)&&recycleId!=null)
        {CancelGesture();_message=Sim.RecycleCourtyardObject(recycleId);RefreshRoomUi();return true;}
        if(_sceneDragId!=null)
        {
            if(_sceneDragKind=="accessory"&&IsScenePoint(mouse.Position)&&ContactAt(mouse.Position)?.Id==_sceneDragId&&ObjectAt(mouse.Position)==null&&_actors.TryGetValue(_sceneDragId,out var ownerActor)&&
                ownerActor.RayContact(_camera.ProjectRayOrigin(mouse.Position),_camera.ProjectRayNormal(mouse.Position),true) is {TargetKind:InteractionTargetKind.Body,Blocked:false})
                AcceptFeedback(Sim.ExecuteInteraction(_sceneDragId,new(InteractionAction.ReturnAccessory,BodyPart.Accessory)),_sceneDragId);
            if(_sceneDragKind=="portable")Sim.ReleasePortableCarry(_sceneDragId,_portableRelease.Velocity,cancel:!IsScenePoint(mouse.Position));
            Sim.SetGroundHandTarget(null);
            _sceneDragId=_sceneDragKind=null;_recycleBox.Visible=false;RefreshRoomUi();return true;
        }
        if(_pokeContact is { } poke)
        {
            if(!_pulled&&IsScenePoint(mouse.Position))ApplyPoke(poke,false,0);
            _pokeContact=null;_swingArmed=false;return true;
        }
        if(_swingArmed){_swingArmed=false;return true;}
        return false;
    }
    private void ResetRedesignGesture()
    {
        Sim?.SetGroundHandTarget(null);
        if(_sceneDragId!=null&&_sceneDragKind=="portable")Sim?.ReleasePortableCarry(_sceneDragId,System.Numerics.Vector2.Zero,true);
        _sceneDragId=_sceneDragKind=_hoverGestureId=null;_pokeContact=null;_swingArmed=false;
        if(_recycleBox!=null)_recycleBox.Visible=false;
    }
    private void SelectObserved(Contact? contact,RoomObject? obj)
    {
        _observedStemMotherId=null;
        if(contact?.TargetKind==InteractionTargetKind.SupplyStation){_selectedRoomObjectId=null;SelectSupplyStation(contact.Id);}
        else if(contact?.TargetKind==InteractionTargetKind.DetachedStem){_selectedRoomObjectId=null;SelectDetachedStem(contact.Id);}
        else if(obj!=null){CancelGesture();ClearSupplyStationSelection();_selectedDetachedStemEventId=null;_selectedRoomObjectId=obj.Id;_message=RoomResourceText.Describe(obj,Sim.State);}
        else if(contact!=null){SelectOil(contact.Id);if(contact.TargetKind==InteractionTargetKind.Stem)_observedStemMotherId=contact.Id;}
        RefreshRoomUi();
    }
    private bool HandleRedesignPress(InputEventMouseButton mouse,Contact? contact)
    {
        var obj=ObjectAt(mouse.Position);
        if(_waterRescueToolArmed)return false;
        if(_roomMode==RoomMode.Observe){SelectObserved(contact,obj);if(mouse.DoubleClick&&contact?.TargetKind==InteractionTargetKind.Body)ToggleClose();return true;}
        if(!_toolArmed){SelectObserved(contact,obj);return true;}
        if(Sim.State.Settings.Pause)return false;
        if(_hoverGestureId!=null){Sim.EndInteractionGesture(true);_hoverGestureId=null;}
        if(_roomMode==RoomMode.Tidy){_message=obj==null?"点击屎、尿或空容器收拾。":Sim.CleanSceneObject(obj.Id);RefreshRoomUi();return true;}
        if(_action is InteractionAction.CleanAccessory or InteractionAction.RepairAccessory or InteractionAction.AdjustHanging or InteractionAction.Release or InteractionAction.OpenBox or InteractionAction.Lower or InteractionAction.Unwrap)return false;
        if(contact?.Part==BodyPart.Accessory&&contact.TargetKind==InteractionTargetKind.Body&&
            Sim.State.Creatures.Find(c=>c.Id==contact.Id)?.Interaction.Accessory.Place!=AccessoryPlace.Worn&&_roomMode is RoomMode.Grab or RoomMode.Accessory or RoomMode.Poke)
        {SelectOil(contact.Id);StartSceneDrag(contact.Id,"accessory",mouse.Position);return true;}
        if(_roomMode is RoomMode.Grab or RoomMode.Pet&&obj!=null)
        {
            if(WorldSimulation.IsWaste(obj.Kind)||NaturalResources.IsWild(obj.Kind)){SelectObserved(null,obj);return true;}
            if(_roomMode==RoomMode.Grab&&obj.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water)
            {
                if(Sim.HoldSupply(obj.Id))
                {_portableRelease.Reset();_heldSupplyId=obj.Id;_supplyBiteTaken=false;_directGesture=true;_mouseHeld=true;_lastPointer=mouse.Position;_action=obj.Kind==RoomObjectKind.Water?InteractionAction.Drink:obj.Kind==RoomObjectKind.Sweet?InteractionAction.FeedSweet:InteractionAction.FeedMeal;ShowInteractionTool();}
                return true;
            }
            if(_roomMode==RoomMode.Grab){if(Sim.BeginPortableCarry(obj.Id))StartSceneDrag(obj.Id,"portable",mouse.Position);}
            else StartSceneDrag(obj.Id,"prop",mouse.Position);return true;
        }
        if(_roomMode==RoomMode.Poke)
        {
            if(contact!=null&&contact.TargetKind!=InteractionTargetKind.Body||obj!=null){SelectObserved(contact,obj);return true;}
            if(contact?.Blocked==true){SelectObserved(contact,null);return true;}
            if(contact!=null)SelectOil(contact.Id);
            _pokeContact=contact;_swingArmed=contact==null;_pulled=false;_pressSeconds=0;
            _redesignStart=_redesignPrevious=mouse.Position;return true;
        }
        if(contact?.TargetKind==InteractionTargetKind.SupplyStation)
        {
            if(_roomMode==RoomMode.Grab){SelectSupplyStation(contact.Id);if(Sim.BeginPortableCarry(contact.Id))StartSceneDrag(contact.Id,"portable",mouse.Position);return true;}
            if(_roomMode==RoomMode.Pet){_action=InteractionAction.Lift;return false;}
            SelectObserved(contact,null);return true;
        }
        if(contact?.TargetKind==InteractionTargetKind.DetachedStem&&_roomMode==RoomMode.Grab)
        {SelectDetachedStem(contact.Id);if(Sim.BeginPortableCarry(contact.Id))StartSceneDrag(contact.Id,"portable",mouse.Position);return true;}
        if(contact?.TargetKind==InteractionTargetKind.Body)
        {
            var c=Sim.State.Creatures.Find(c=>c.Id==contact.Id)!;
            if(contact.Id!=Sim.State.SelectedId)SelectOil(contact.Id);
            if(contact.Blocked&&_roomMode is RoomMode.Induce or RoomMode.Suppress or RoomMode.Juice or RoomMode.Accessory)
            {_message="装置挡住了接触位置，先打开或解除装置。";return true;}
            switch(_roomMode)
            {
                case RoomMode.Grab:
                    if(c.Condition==PhysicalCondition.Dead||c.Interaction.Restraint!=RestraintKind.None){if(Sim.BeginPortableCarry(c.Id))StartSceneDrag(c.Id,"portable",mouse.Position);return true;}
                    _action=InteractionAction.Lift;break;
                case RoomMode.Pet:StartSceneDrag(c.Id,"body",mouse.Position);return true;
                case RoomMode.Greeting:_action=c.Fear>30||c.Pain>15?InteractionAction.Comfort:c.Interaction.PendingPraise!=null&&c.Interaction.PraiseRemaining>0?InteractionAction.Praise:InteractionAction.Greet;break;
                case RoomMode.Teaching:_message=Sim.TeachCleaning(c.Id);RefreshRoomUi();return true;
                case RoomMode.Company:
                    if(_companyFirst==null||_companyFirst==c.Id){_companyFirst=c.Id;_message="已指定"+c.Name+"，再点击另一只油邀请相处。";}
                    else{_message=Sim.InviteCompanion(_companyFirst,c.Id);_companyFirst=null;}return true;
                case RoomMode.Inspect:_message=Sim.ExamineBody(c.Id);RefreshRoomUi();return true;
                case RoomMode.Juice:_message=Sim.UseRescueJuice(c.Id);RefreshRoomUi();return true;
                case RoomMode.Cold:_message=Sim.SetColdStorage(c.Id,!c.Family.ColdStorage.Stored);RefreshRoomUi();return true;
                case RoomMode.Bury:_message=Sim.BuryCreature(c.Id);RefreshRoomUi();return true;
                case RoomMode.Breeding:ShowBreedingStatus();return true;
                case RoomMode.Induce:AcceptFeedback(Sim.InduceBreedingInterest(c.Id),c.Id);return true;
                case RoomMode.Suppress:_message=Sim.SuppressBreedingInterest(c.Id);RefreshRoomUi();return true;
                case RoomMode.Towel:_action=c.Interaction.DirtAt(contact.Part,contact.Side)>1?InteractionAction.Clean:InteractionAction.Dry;break;
                case RoomMode.Accessory:
                    AcceptFeedback(Sim.ExecuteInteraction(c.Id,new(InteractionAction.TakeAccessoryCare,BodyPart.Accessory)),c.Id);RefreshRoomUi();return true;
                case RoomMode.Wrap:_action=c.Interaction.Restraint==RestraintKind.Wrap?InteractionAction.Rock:InteractionAction.Wrap;break;
                case RoomMode.Pressure:
                    _action=contact.Part==BodyPart.Side?InteractionAction.Squeeze:contact.Part==BodyPart.Bottom?InteractionAction.Roll:InteractionAction.Press;
                    _redesignStart=mouse.Position;
                    _pressureOrigin=contact.Part==BodyPart.Bottom||_actors.TryGetValue(c.Id,out var pressureActor)&&pressureActor.ContactCoordinate(contact.World,contact.Part).Y<=ActorFaceSurface.CenterY?contact:null;break;
                case RoomMode.Frame:_action=c.Interaction.Restraint==RestraintKind.Frame?InteractionAction.Release:InteractionAction.Pin;break;
                case RoomMode.Box:_action=c.Interaction.Restraint==RestraintKind.Box?InteractionAction.OpenBox:InteractionAction.Box;break;
                case RoomMode.Hanging:_action=c.Interaction.Restraint==RestraintKind.Hanging?InteractionAction.AdjustHanging:InteractionAction.Hang;break;
            }
        }
        if(_roomMode==RoomMode.Ball)_action=obj?.Kind==RoomObjectKind.Ball?InteractionAction.PlayBall:InteractionAction.GiveBall;
        return false;
    }
    private Contact? _pressureOrigin;
    private void StartSceneDrag(string id,string kind,Vector2 position)
    {
        _sceneDragId=id;_sceneDragKind=kind;_redesignStart=_redesignPrevious=position;_pressSeconds=0;_portableRelease.Reset();_height=.65f;
        if(kind=="body")Sim.SetGroundHandTarget(id);
        _sceneDragOffset=Vector3.Zero;
        if(Sim.PortablePosition(id) is { } p&&TryGroundPoint(position,0,out var ground))_sceneDragOffset=new Vector3(p.X,0,p.Y)-ground;
        if(kind=="accessory"&&Sim.State.Creatures.Find(c=>c.Id==id) is { } owner&&TryGroundPoint(position,0,out var accessoryGround))
            _sceneDragOffset=new Vector3((float)owner.Interaction.Accessory.X,0,(float)owner.Interaction.Accessory.Z)-accessoryGround;
    }
    private void ApplyPoke(Contact contact,bool swing,double speed)
    {
        var c=Sim.State.Creatures.Find(c=>c.Id==contact.Id);if(c==null)return;
        bool combo=c.Interaction.HitRemaining>0;
        var action=swing||combo?(speed>3?InteractionAction.Punch:InteractionAction.Slap):contact.Part==BodyPart.Head?InteractionAction.Flick:InteractionAction.Poke;
        _action=action;
        var normal=contact.Normal;var forward=-_camera.GlobalBasis.Z;
        var direction=InteractionCameraOrbit.ContactImpulse(new(normal.X,normal.Y,normal.Z),new(forward.X,forward.Y,forward.Z));
        var reply=Sim.ExecuteInteraction(c.Id,new(action,contact.Part,Force:swing?Math.Clamp(speed/6,.25,.95):.12,DirectionX:direction.X,DirectionZ:direction.Y,Side:contact.Side));
        AcceptFeedback(reply,c.Id);
        if(reply.Success&&reply.Changed){_tapContact=contact;_tapAge=0;if(swing||combo)RegisterHit(c.Id);}
    }
    private void ProcessRedesignGestures(float dt)
    {
        Vector2 point=GetViewport().GetMousePosition();
        bool dragging=_sceneDragId!=null||_heldSupplyId!=null||_mouseHeld&&(_action==InteractionAction.Lift||_gestureTargetKind is InteractionTargetKind.SupplyStation or InteractionTargetKind.DetachedStem);
        _recycleBox.Visible=dragging&&_roomMode==RoomMode.Grab;
        if(!_windowFocused||_modalOpen||Sim.State.Settings.Pause){if(_hoverGestureId!=null){Sim.EndInteractionGesture(true);_hoverGestureId=null;}return;}
        if(_roomMode is RoomMode.Meal or RoomMode.Sweet or RoomMode.Water&&_toolArmed&&!_mouseHeld&&_clickOfferId==null&&_offerReturnId==null)
        {
            bool available=_roomMode==RoomMode.Water?Sim.State.StationWaterPortions>0:Sim.State.Food>0;
            _hand.Visible=available&&IsScenePoint(point);
            if(_hand.Visible&&TryGroundPoint(point,.8f,out var offered)){ShowInteractionTool();_hand.Position=offered;_hand.GlobalBasis=Basis.Identity;}
        }
        _pressSeconds+=dt;_swingCooldown=Math.Max(0,_swingCooldown-dt);
        if(_sceneDragId is { } id)
        {
            if(!Input.IsMouseButtonPressed(MouseButton.Left)){ResetRedesignGesture();return;}
            if(IsScenePoint(point)&&TryGroundPoint(point,0,out var ground))
            {
                ground+=_sceneDragOffset;
                if(_sceneDragKind=="body")Sim.MoveSupportedBodyOrFixture(id,ground.X,ground.Z);
                else if(_sceneDragKind=="prop")Sim.MoveLooseObject(id,ground.X,ground.Z);
                else if(_sceneDragKind=="portable")
                {
                    var before=Sim.PortablePosition(id);Sim.MovePortableCarry(id,ground.X,ground.Z,_height);var after=Sim.PortablePosition(id);
                    if(before.HasValue&&after.HasValue)_portableRelease.Sample(after.Value-before.Value,dt);
                }
                else if(_sceneDragKind=="accessory"&&Sim.State.Creatures.Find(c=>c.Id==id) is { } owner)
                {owner.Interaction.Accessory.Place=AccessoryPlace.Hand;owner.Interaction.Accessory.X=Math.Clamp(ground.X,-7.7,7.7);owner.Interaction.Accessory.Z=Math.Clamp(ground.Z,-7.7,7.7);owner.Interaction.Accessory.Height=.7;}
            }
            else _portableRelease.Reset();
            _redesignPrevious=point;return;
        }
        if(_pokeContact is { } poke&&Input.IsMouseButtonPressed(MouseButton.Left))
        {
            if(_pressSeconds>.35&&point.DistanceTo(_redesignStart)>12&&IsScenePoint(point))
            {
                if(poke.Part==BodyPart.Accessory)
                {
                    var reply=Sim.ExecuteInteraction(poke.Id,new(InteractionAction.TakeAccessory,BodyPart.Accessory));AcceptFeedback(reply,poke.Id);
                    if(reply.Success&&reply.Changed){_pokeContact=null;StartSceneDrag(poke.Id,"accessory",point);}
                }
                else AcceptFeedback(Sim.PullContactPart(poke.Id,poke.Part,poke.Side,Math.Min(1,point.DistanceTo(_redesignStart)/120),dt),poke.Id);
                _pulled=true;
            }
            return;
        }
        if(_swingArmed)
        {
            if(!Input.IsMouseButtonPressed(MouseButton.Left)){_swingArmed=false;return;}
            Vector2 previous=_redesignPrevious;double speed=ViewMotion(point-previous).Length()/Math.Max(.01,dt);_redesignPrevious=point;
            if(_pressSeconds>.1&&speed>.4&&_swingCooldown<=0&&IsScenePoint(point))
            {
                int steps=Math.Clamp((int)Math.Ceiling(point.DistanceTo(previous)/8),1,48);
                for(int n=1;n<=steps;n++)if(ContactAt(previous.Lerp(point,(float)n/steps)) is {Blocked:false,TargetKind:InteractionTargetKind.Body} hit)
                {ApplyPoke(hit,true,speed);_swingCooldown=.2;break;}
            }
            return;
        }
        if(_roomMode==RoomMode.Pressure&&_mouseHeld&&_pressureOrigin is { } origin&&point.Y<_redesignStart.Y-35&&origin.Part is BodyPart.Bottom or BodyPart.Side)
        {
            string target=origin.Id;bool flipped=Sim.State.Creatures.Find(c=>c.Id==target)?.Interaction.Supine==true;
            CancelGesture();_pressureOrigin=null;if(!flipped)AcceptFeedback(Sim.ExecuteInteraction(target,new(InteractionAction.Flip)),target);RefreshRoomUi();return;
        }
        if(_mouseHeld||Input.IsMouseButtonPressed(MouseButton.Left))return;
        if(!IsScenePoint(point)){if(_hoverGestureId!=null){Sim.EndInteractionGesture(true);_hoverGestureId=null;}return;}
        var contact=ContactAt(point);
        InteractionAction? hoverAction=_roomMode==RoomMode.Pet&&contact?.Part is BodyPart.Head or BodyPart.Cheek?contact.Part==BodyPart.Cheek?InteractionAction.Nuzzle:InteractionAction.Stroke:
            _roomMode==RoomMode.Sweet&&Sim.State.Food>0?InteractionAction.Tease:_roomMode==RoomMode.Pressure?InteractionAction.Block:null;
        if(contact==null&&hoverAction is InteractionAction.Tease or InteractionAction.Block&&TryGroundPoint(point,0,out var near))
        {
            var nearest=Sim.State.Creatures.Where(c=>c.RegionId==Sim.State.ActiveRegionId&&c.Condition==PhysicalCondition.Active&&Math.Pow(c.X-near.X,2)+Math.Pow(c.Z-near.Z,2)<2.25).OrderBy(c=>Math.Pow(c.X-near.X,2)+Math.Pow(c.Z-near.Z,2)).FirstOrDefault();
            if(nearest!=null)contact=new(nearest.Id,BodyPart.Side,near,Vector3.Up,0,false,RestraintKind.None,SurfaceSide.Whole);
        }
        if(hoverAction==null||contact==null||contact.Blocked||contact.TargetKind!=InteractionTargetKind.Body)
        {if(_hoverGestureId!=null){Sim.EndInteractionGesture(true);_hoverGestureId=null;}return;}
        var request=new InteractionInput(hoverAction.Value,contact.Part,Force:.12,Duration:dt,Motion:dt*.08,X:contact.World.X,Z:contact.World.Z,Side:contact.Side);
        if(_hoverGestureId!=contact.Id||Sim.CurrentInteractionAction!=hoverAction)
        {Sim.EndInteractionGesture(true);_hoverGestureId=null;var reply=Sim.BeginInteractionGesture(contact.Id,request);if(!reply.Success)return;_hoverGestureId=contact.Id;}
        var hoverReply=Sim.ContinueInteractionGesture(request,true);_gesturePose=hoverReply.Pose;AcceptFeedback(hoverReply,contact.Id);
    }
}

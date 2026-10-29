using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private RoomPointer _pointer=null!;
    private Vector2 _dragStart,_lastPointer,_gestureDirection;
    private Vector3 _dragWorldStart;
    private string _gesturePose="idle";
    private string? _hoverId;
    private BodyPart _hoverPart;
    private bool _mouseHeld;
    private bool _orbiting;
    private Contact? _tapContact;
    private float _tapAge;
    private readonly ContactStrokeTracker _strokeTracker=new();
    private readonly ContactMotion _contactMotion=new();
    private bool _directGesture;
    private float _appliedForce=.12f;
    private string? _heldSupplyId;
    private bool _supplyBiteTaken;
    private float _closeYaw,_closePitch=InteractionCameraOrbit.DefaultPitch,_closeZoom=1;
    private string? _ballDragId;
    private record Contact(string Id,BodyPart Part,Vector3 World,Vector3 Normal,float Distance,bool Blocked,RestraintKind Fixture,SurfaceSide Side);
    private bool CanUseContact(Contact contact)=>!contact.Blocked||contact.Fixture switch
    {
        RestraintKind.Box=>_action==InteractionAction.OpenBox,
        RestraintKind.Wrap=>_action is InteractionAction.Unwrap or InteractionAction.Rock,
        RestraintKind.Frame=>_action==InteractionAction.Release,
        RestraintKind.Hanging=>_action is InteractionAction.Lower or InteractionAction.AdjustHanging,
        _=>false
    };
    private static string ContactName(Contact c)=>!c.Blocked?PartName(c.Part,c.Side):c.Fixture switch
    {RestraintKind.Box=>"箱子",RestraintKind.Wrap=>"包裹",RestraintKind.Frame=>"固定具",RestraintKind.Hanging=>"悬挂支架",_=>"托举支撑"};

    private Vector3 GroundPoint(Vector2 point,float height=0)
    {
        if(TryGroundPoint(point,height,out var hit))return hit;
        return new((float)(Sim.Selected?.X??0),height,(float)(Sim.Selected?.Z??0));
    }
    private bool TryGroundPoint(Vector2 point,float height,out Vector3 hit)
    {
        var start=_camera.ProjectRayOrigin(point);var dir=_camera.ProjectRayNormal(point);
        bool valid=InteractionCameraOrbit.TryPlaneHit(new(start.X,start.Y,start.Z),new(dir.X,dir.Y,dir.Z),height,out var world);
        hit=new(Mathf.Clamp(world.X,-7.7f,7.7f),height,Mathf.Clamp(world.Z,-7.7f,7.7f));return valid;
    }
    private Vector3 ViewMotion(Vector2 pixels)
    {
        var right=_camera.GlobalBasis.X;var up=_camera.GlobalBasis.Y;
        var motion=InteractionCameraOrbit.ViewMotion(new(right.X,right.Y,right.Z),new(up.X,up.Y,up.Z),new(pixels.X,pixels.Y),_camera.Size,GetViewport().GetVisibleRect().Size.Y);
        return new(motion.X,motion.Y,motion.Z);
    }
    private Vector3 SupportDrag(Vector2 pixels)
    {
        var right=_camera.GlobalBasis.X;var forward=-_camera.GlobalBasis.Z;
        var motion=InteractionCameraOrbit.GroundDrag(new(right.X,right.Y,right.Z),new(forward.X,forward.Y,forward.Z),new(pixels.X,pixels.Y),_camera.Size,GetViewport().GetVisibleRect().Size.Y);
        return new(motion.X,0,motion.Z);
    }
    private bool IsScenePoint(Vector2 point)
    {
        if(_modalOpen)return false;
        return point.Y>82&&point.Y<_ui.Size.Y-216&&point.X>226&&point.X<_ui.Size.X-282;
    }
    private Contact? ContactAt(Vector2 point)
    {
        Contact? best=null;
        Vector3 origin=_camera.ProjectRayOrigin(point),direction=_camera.ProjectRayNormal(point);
        float nearest=SceneOcclusionDistance(origin,direction);
        foreach(var c in Sim.State.Creatures)
        {
            if(!_actors.TryGetValue(c.Id,out var actor)||!actor.Visible)continue;
            var hit=actor.RayContact(origin,direction);
            if(hit==null||hit.Distance>=nearest)continue;
            nearest=hit.Distance;
            var local=actor.ContactCoordinate(hit.Point,hit.Part);
            best=new(c.Id,hit.Part,hit.Point,hit.Normal,hit.Distance,hit.Blocked,hit.Fixture,ActorSurfaceMarks.ContactSide(hit.Part,local.X));
        }
        return best;
    }
    private RoomObject? ObjectAt(Vector2 pointer)
    {
        var origin=_camera.ProjectRayOrigin(pointer);var direction=_camera.ProjectRayNormal(pointer);
        float nearest=ContactAt(pointer)?.Distance??SceneOcclusionDistance(origin,direction);RoomObject? result=null;
        foreach(var obj in Sim.State.RoomObjects)
        {
            if(obj.Stored||obj.RegionId!=Sim.State.ActiveRegionId||!_objects.TryGetValue(obj.Id,out var visual)||!visual.Visible)continue;
            bool floating=obj.Kind==RoomObjectKind.Ball&&RoomBallEnvironment.InWater(Sim.State,obj);
            if(!InteractionPropContact.RayHit(obj.Kind,new((float)obj.X,(float)obj.Height,(float)obj.Z),new(origin.X,origin.Y,origin.Z),new(direction.X,direction.Y,direction.Z),out float distance,
                obj.Kind==RoomObjectKind.Ball?RoomBallEnvironment.CenterHeight(Sim.State,obj):null)||distance>=nearest)continue;
            if(floating&&(origin+direction*distance).Y<NaturalIntakeGeometry.WaterHeight)continue;
            nearest=distance;result=obj;
        }
        return result;
    }
    private float SceneOcclusionDistance(Vector3 origin,Vector3 direction)=>WoodlandLayout.HasRiver(Sim.State,Sim.State.ActiveRegionId)?
        WoodlandOcclusion.RayDistance(new(origin.X,origin.Y,origin.Z),new(direction.X,direction.Y,direction.Z)):float.PositiveInfinity;
    private static string PartName(BodyPart part,SurfaceSide side=SurfaceSide.Whole)=>part switch{BodyPart.Head=>"头顶",BodyPart.Cheek=>side==SurfaceSide.Left?"左脸":side==SurfaceSide.Right?"右脸":"脸侧",BodyPart.Side=>side==SurfaceSide.Left?"身体左侧":side==SurfaceSide.Right?"身体右侧":"身体侧面",BodyPart.Bottom=>"底部",BodyPart.Mouth=>"嘴边",_=>"饰品"};

    public override void _Input(InputEvent input)
    {
        if(!_ready||!_windowFocused)return;
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Middle,Pressed:false})_orbiting=false;
        if(input is InputEventMouseMotion orbit&&_orbiting)
        {
            if(!Input.IsMouseButtonPressed(MouseButton.Middle)||Sim.State.Mode!=PlayMode.CloseUp||_modalOpen){_orbiting=false;return;}
            _closeYaw=Mathf.Wrap(_closeYaw-orbit.Relative.X*.35f,-180,180);
            _closePitch=Mathf.Clamp(_closePitch+orbit.Relative.Y*.25f,-25,65);
            GetViewport().SetInputAsHandled();return;
        }
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:false}&&_mouseHeld)
        {
            EndPointerGesture();
            GetViewport().SetInputAsHandled();
        }
        if(input is InputEventMouseButton{ButtonIndex:MouseButton.Right,Pressed:true})
        {if(!_modalOpen)DisarmTool();GetViewport().SetInputAsHandled();}
        if(input is InputEventKey{Pressed:true,Echo:false} key)
        {
            if(key.Keycode==Key.Escape)
            {if(_modalOpen)CloseModal();else if(_toolArmed||Sim.HasInteractionGesture)DisarmTool();else if(Sim.State.Mode==PlayMode.CloseUp)ToggleClose();else ShowSettings();GetViewport().SetInputAsHandled();}
            else if(!_modalOpen&&key.Keycode==Key.Space){TogglePause();GetViewport().SetInputAsHandled();}
            else if(!_modalOpen&&key.Keycode==Key.Tab){ToggleClose();GetViewport().SetInputAsHandled();}
            else if(!_modalOpen&&key.Keycode==Key.Delete&&IsScenePoint(GetViewport().GetMousePosition()))
            {
                var obj=ObjectAt(GetViewport().GetMousePosition());
                if(obj!=null)_message=Sim.PutAwayRoomObject(obj.Id)?"已经收起地上的物品。":
                    NaturalResources.IsWild(obj.Kind)?"这是野外的资源，不能直接收进物品栏。":"这件物品现在不能收起。";
            }
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if(!_ready||!_windowFocused||_modalOpen)return;
        if(input is not InputEventMouseButton mouse||!mouse.Pressed)return;
        if(!IsScenePoint(mouse.Position))return;
        if(mouse.ButtonIndex==MouseButton.Middle&&Sim.State.Mode==PlayMode.CloseUp)
        {
            CancelGesture();_orbiting=true;
            _message="正在转动观察视角。松开中键后，可以对准另一处继续接触。";
            GetViewport().SetInputAsHandled();return;
        }
        if(mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            float step=mouse.ButtonIndex==MouseButton.WheelUp?1:-1;
            if(mouse.ShiftPressed)
            {
                float scale=Sim.Selected is { } oil&&_actors.TryGetValue(oil.Id,out var actor)?actor.BodyScale:1;
                _height=(float)InteractionFixtureGeometry.ClampSupportHeight(_height+step*.15f,scale,_action==InteractionAction.AdjustHanging);
            }
            else
            {
                CancelGesture();
                if(Sim.State.Mode==PlayMode.CloseUp)_closeZoom=Mathf.Clamp(_closeZoom-step*.08f,.65f,1.6f);
                else _zoom=Mathf.Clamp(_zoom-step,9,23);
            }
            GetViewport().SetInputAsHandled();return;
        }
        if(mouse.ButtonIndex!=MouseButton.Left)return;
        if(Sim.State.Settings.Pause){_message="小院暂停中，先点“继续”或按空格。";return;}
        var contact=ContactAt(mouse.Position);
        if(!_toolArmed&&mouse.DoubleClick&&contact!=null){SelectOil(contact.Id);ToggleClose();return;}
        if(!_toolArmed)
        {
            var obj=ObjectAt(mouse.Position);
            if(obj?.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water)
            {
                if(Sim.HoldSupply(obj.Id))
                {
                    _heldSupplyId=obj.Id;_supplyBiteTaken=false;_directGesture=true;_mouseHeld=true;_lastPointer=mouse.Position;
                    _action=obj.Kind==RoomObjectKind.Water?InteractionAction.Drink:obj.Kind==RoomObjectKind.Sweet?InteractionAction.FeedSweet:InteractionAction.FeedMeal;
                    _message="拿起来了。按住递到嘴边，或移到空地松开放下。";ShowInteractionTool();
                }
                GetViewport().SetInputAsHandled();return;
            }
            if(Sim.Selected?.Interaction.Carried==true&&contact==null)
            {
                if(TryGroundPoint(mouse.Position,0,out var landing))
                    AcceptFeedback(Sim.ExecuteInteraction(Sim.Selected.Id,new(InteractionAction.PutDown,X:landing.X,Z:landing.Z)));
                return;
            }
            if(obj!=null&&NaturalResources.IsWild(obj.Kind))
            {_message=RoomResourceText.Describe(obj,Sim.State);GetViewport().SetInputAsHandled();return;}
            if(obj?.Kind==RoomObjectKind.Ball)_action=InteractionAction.PlayBall;
            else if(contact!=null)
            {
                if(contact.Id!=Sim.State.SelectedId)SelectOil(contact.Id);
                _action=mouse.ShiftPressed?InteractionAction.Lift:InteractionAction.Touch;
            }
            else return;
            _directGesture=true;
        }
        var ground=GroundPoint(mouse.Position);
        bool placement=_action is InteractionAction.PlaceMeal or InteractionAction.PlaceSweet or InteractionAction.PlaceWater or InteractionAction.PutDown or InteractionAction.GiveBall;
        if(placement)
        {
            if(!TryGroundPoint(mouse.Position,0,out ground)){_message="这个视角看不到落点。先把镜头转向地面，再放置；托举仍保持支撑。";return;}
            if(Sim.Selected!=null)AcceptFeedback(Sim.ExecuteInteraction(Sim.Selected.Id,new(_action,Force:_force,X:ground.X,Z:ground.Z)));
            return;
        }
        bool ball=_action==InteractionAction.PlayBall;
        if(ball)
        {
            var obj=ObjectAt(mouse.Position);if(obj?.Kind!=RoomObjectKind.Ball){_message="按住软球本身，再拖动推球。";return;}
            _ballDragId=obj.Id;
        }
        else if(contact==null){_message="还没有接触到小油。双击它靠近看，会更容易对准。";return;}
        if(!ball&&contact!=null&&contact.Id!=Sim.State.SelectedId){SelectOil(contact.Id);return;}
        if(!ball&&contact!=null&&!CanUseContact(contact))
        {_message="这里是"+ContactName(contact)+"，挡住了身体。先用相应的打开、解开或解除操作。";return;}
        var selected=Sim.Selected;if(selected==null)return;
        if(_action==InteractionAction.Lift)_height=selected.Interaction.Carried?(float)selected.Interaction.Height:.8f;
        else if(_action==InteractionAction.AdjustHanging)
            _height=(float)selected.Interaction.Height;
        _dragStart=mouse.Position;_lastPointer=mouse.Position;_dragWorldStart=ground;_gestureDirection=Vector2.Zero;
        _strokeTracker.Reset();
        _contactMotion.Reset();_appliedForce=_directGesture?.12f:_force;
        _contactMotion.Sample(contact?.Id,System.Numerics.Vector3.Zero,System.Numerics.Vector3.UnitY,.01);
        if(contact!=null)SampleStroke(contact,mouse.Position,true);
        var part=contact?.Part??BodyPart.Side;
        var request=new InteractionInput(_action,part,_appliedForce,X:ground.X,Z:ground.Z,Height:_height,Side:contact?.Side??SurfaceSide.Whole,ObjectId:ball?_ballDragId:null);
        if(contact!=null&&_action is InteractionAction.Poke or InteractionAction.Flick or InteractionAction.Slap)
        {
            var normal=contact.Normal;var forward=-_camera.GlobalBasis.Z;
            var impulse=InteractionCameraOrbit.ContactImpulse(new(normal.X,normal.Y,normal.Z),new(forward.X,forward.Y,forward.Z));
            request=request with {DirectionX=impulse.X,DirectionZ=impulse.Y};
        }
        if(InteractionCatalog.Choice(_action).Continuous)
        {
            var feedback=Sim.BeginInteractionGesture(selected.Id,request);AcceptFeedback(feedback);
            _mouseHeld=Sim.HasInteractionGesture;_gesturePose=feedback.Pose;
            if(!_mouseHeld)CancelGesture();
        }
        else
        {
            var feedback=Sim.ExecuteInteraction(selected.Id,request);AcceptFeedback(feedback);
            if(contact!=null&&feedback.Success&&feedback.Changed&&!feedback.Refused&&_action is InteractionAction.Poke or InteractionAction.Flick or InteractionAction.Slap)
            {
                _tapContact=contact;_tapAge=0;ShowInteractionTool();PlaceContactTool(contact);_hand.Visible=true;
            }
        }
        GetViewport().SetInputAsHandled();
    }

    private void ResetContactMotion()
    {
        _strokeTracker.Reset();_contactMotion.Reset();
        _appliedForce=.12f;_gestureDirection=Vector2.Zero;_gesturePose="idle";
    }
    private void CancelGesture()
    {
        _tapContact=null;
        _orbiting=false;
        ResetContactMotion();_directGesture=false;
        if(Sim?.HasInteractionGesture==true)AcceptFeedback(Sim.EndInteractionGesture(true));
        if(_heldSupplyId is { } supply){Sim?.ReleaseSupply(supply);_heldSupplyId=null;}
        _mouseHeld=false;_ballDragId=null;if(_hand!=null)_hand.Visible=false;
    }
    private void EndPointerGesture()
    {
        string? target=Sim.InteractionGestureTarget;
        bool place=_directGesture&&_action==InteractionAction.Lift;
        _mouseHeld=false;_ballDragId=null;AcceptFeedback(Sim.EndInteractionGesture());_hand.Visible=false;
        if(_heldSupplyId is { } supply)
        {
            Vector2 point=GetViewport().GetMousePosition();
            bool placed=IsScenePoint(point)&&TryGroundPoint(point,0,out var drop)&&Sim.ReleaseSupply(supply,drop.X,drop.Z);
            if(!placed)Sim.ReleaseSupply(supply);
            _heldSupplyId=null;_message=placed?"已经放到地面，可以让它自己过来吃喝。":"没有安全落点，剩余食水放回原处。";
        }
        if(place&&target!=null)
        {
            Vector2 point=GetViewport().GetMousePosition();
            if(IsScenePoint(point)&&TryGroundPoint(point,0,out var ground))
                AcceptFeedback(Sim.ExecuteInteraction(target,new(InteractionAction.PutDown,X:ground.X,Z:ground.Z)));
            else _message="这里看不到安全落点，仍然托着；在空地上点一下放稳。";
        }
        _directGesture=false;ResetContactMotion();
    }

    private void ProcessRoomGesture(float dt,double sampleSeconds)
    {
        if(_heldSupplyId!=null){ProcessHeldSupply(dt);return;}
        if(!_mouseHeld)return;
        if(!Sim.HasInteractionGesture){CancelGesture();return;}
        if(!Input.IsMouseButtonPressed(MouseButton.Left)){CancelGesture();return;}
        Vector2 current=GetViewport().GetMousePosition();var contact=IsScenePoint(current)?ContactAt(current):null;
        bool touching=contact!=null&&contact.Id==Sim.InteractionGestureTarget&&CanUseContact(contact);
        var ground=GroundPoint(current);var worldMotion=ground-GroundPoint(_lastPointer);
        bool surfaceMotion=_action is InteractionAction.Stroke or InteractionAction.Nuzzle or InteractionAction.Clean or InteractionAction.Dry or InteractionAction.CleanAccessory or InteractionAction.Rock;
        bool timelySample=double.IsFinite(sampleSeconds)&&sampleSeconds>0&&sampleSeconds<=ContactMotion.MaximumSampleSeconds;
        if(!timelySample)_strokeTracker.Reset();
        ContactStroke stroke=surfaceMotion&&timelySample?SampleStroke(contact,current,touching):default;
        var pointerTravel=ViewMotion(current-_lastPointer);
        var normal=contact?.Normal??Vector3.Up;
        bool bodyMotionContact=touching&&(_action!=InteractionAction.Touch||contact!.Part!=BodyPart.Accessory);
        var motionSample=_contactMotion.Sample(bodyMotionContact?contact!.Id:null,
            new(pointerTravel.X,pointerTravel.Y,pointerTravel.Z),new(normal.X,normal.Y,normal.Z),sampleSeconds,
            contact!=null?_actors[contact.Id].BodyScale:1);
        bool speedForce=_action is InteractionAction.Touch or InteractionAction.Stroke or InteractionAction.Nuzzle or InteractionAction.Rock or
            InteractionAction.Clean or InteractionAction.Dry or InteractionAction.Press or InteractionAction.Squeeze;
        _appliedForce=_action==InteractionAction.Touch?(float)motionSample.Force:
            speedForce?Math.Max(_force,(float)motionSample.Force):_force;
        Vector2 poseDirection=surfaceMotion?new(stroke.Direction.X,stroke.Direction.Y):(current-_lastPointer).LimitLength(6)/6;
        _gestureDirection=_gestureDirection.Lerp(poseDirection,Mathf.Min(1,dt*16));
        bool aiming=_action is InteractionAction.Push or InteractionAction.Throw or InteractionAction.PlayBall;
        if(aiming)worldMotion=SupportDrag(current-_lastPointer);
        Vector3 direction=aiming?SupportDrag(current-_dragStart):ground-_dragWorldStart;
        if(_action==InteractionAction.Touch)direction=new(motionSample.Direction.X,motionSample.Direction.Y,motionSample.Direction.Z);
        var carriedTarget=ground;
        if(_action==InteractionAction.Tease)
        {
            var oil=Sim.State.Creatures.Find(o=>o.Id==Sim.InteractionGestureTarget)!;
            float foodHeight=_actors[oil.Id].ContactPoint(BodyPart.Mouth).Y;
            carriedTarget=GroundPoint(current,foodHeight);
            worldMotion=ViewMotion(current-_lastPointer);
        }
        if(_action is InteractionAction.Lift or InteractionAction.AdjustHanging or InteractionAction.Roll)
        {
            var c=Sim.State.Creatures.Find(o=>o.Id==Sim.InteractionGestureTarget)!;
            var relative=SupportDrag(current-_lastPointer);
            carriedTarget=new Vector3((float)c.X,_height,(float)c.Z)+relative;
            if(_action==InteractionAction.Roll)worldMotion=relative;
        }
        double appliedMotion=_action==InteractionAction.Touch?motionSample.Distance:surfaceMotion?stroke.Distance:worldMotion.Length();
        var input=new InteractionInput(_action,contact?.Part??BodyPart.Side,_appliedForce,dt,appliedMotion,Math.Clamp(carriedTarget.X,-7.7,7.7),Math.Clamp(carriedTarget.Z,-7.7,7.7),_height,direction.X,direction.Z,contact?.Side??SurfaceSide.Whole,_action==InteractionAction.PlayBall?_ballDragId:null);
        if(_action==InteractionAction.Touch)input=input with {StopImpact=motionSample.StopImpact,TouchTangentFraction=motionSample.TangentFraction,TouchNormalSpeed=motionSample.NormalSpeed};
        // Dragging off the scene never creates force, consumes a dose, or drops a held oil.
        if(!IsScenePoint(current))
        {
            if(_action==InteractionAction.Tease){CancelGesture();_lastPointer=current;return;}
            // Tell the simulation that contact really broke, including mouth/care dwell.
            if(_action is not (InteractionAction.Lift or InteractionAction.Throw or InteractionAction.PlayBall or InteractionAction.AdjustHanging))
                Sim.ContinueInteractionGesture(input,false);
            _gesturePose="idle";_hand.Visible=false;_lastPointer=current;return;
        }
        var feedback=Sim.ContinueInteractionGesture(input,touching);
        _gesturePose=feedback.Pose;AcceptFeedback(feedback,Sim.InteractionGestureTarget);
        if(!Sim.HasInteractionGesture){CancelGesture();_lastPointer=current;return;}
        ShowInteractionTool();
        _hand.Visible=(_action is InteractionAction.Tease or InteractionAction.PlayBall||touching)&&(!Sim.InteractionIntakeAccepted||_action is InteractionAction.Drink or InteractionAction.FeedMeal)&&_action is not (InteractionAction.Lift or InteractionAction.Throw);
        if(_toolFood!=null)_toolFood.Visible=!Sim.InteractionIntakeAccepted;
        if(_action==InteractionAction.Drink&&_cupWater!=null)
        {
            var oil=Sim.State.Creatures.Find(o=>o.Id==Sim.InteractionGestureTarget)!;
            float progress=Sim.InteractionIntakeAccepted?1-(float)(oil.Interaction.EatingRemaining/InteractionLife.IntakeDuration(IntakeKind.Water)):0;
            float height=InteractionToolSurface.SipWaterHeight(progress),radius=InteractionToolSurface.SipWaterRadius(height);
            _cupWater.Position=new(0,height,0);_cupWater.Scale=new(radius/InteractionToolSurface.WaterRadius,1,radius/InteractionToolSurface.WaterRadius);
        }
        if(_action==InteractionAction.PlayBall&&Sim.State.RoomObjects.Find(o=>o.Id==Sim.InteractionGestureObject) is { } ballObject)
            PlaceContactTool(new(ballObject.Id,BodyPart.Side,new((float)ballObject.X,(float)ballObject.Height+RoomBallEnvironment.CenterHeight(Sim.State,ballObject)+InteractionBallSurface.Radius,(float)ballObject.Z),Vector3.Up,0,false,RestraintKind.None,SurfaceSide.Whole));
        else if(_action==InteractionAction.Tease)
        {
            _hand.GlobalBasis=Basis.Identity;
            _hand.GlobalPosition=carriedTarget+Vector3.Up*.015f;
        }
        else if(contact!=null)PlaceContactTool(contact);
        _lastPointer=current;
    }

    private void ProcessHeldSupply(float dt)
    {
        if(!_mouseHeld||!Input.IsMouseButtonPressed(MouseButton.Left)){CancelGesture();return;}
        Vector2 point=GetViewport().GetMousePosition();
        var obj=Sim.State.RoomObjects.Find(o=>o.Id==_heldSupplyId&&o.HeldByHand);
        if(obj==null){CancelGesture();return;}
        var contact=IsScenePoint(point)?ContactAt(point):null;
        bool mouth=contact is {Part:BodyPart.Mouth,Blocked:false};
        if(Sim.HasInteractionGesture&&contact?.Id!=Sim.InteractionGestureTarget)AcceptFeedback(Sim.EndInteractionGesture(true));
        if(mouth&&contact!=null&&obj.Portions>0&&!_supplyBiteTaken&&!Sim.HasInteractionGesture)
        {
            Sim.State.SelectedId=contact.Id;
            AcceptFeedback(Sim.BeginInteractionGesture(contact.Id,new(_action,BodyPart.Mouth,.2,dt,ObjectId:obj.Id)));
        }
        if(Sim.HasInteractionGesture)
        {
            var reply=Sim.ContinueInteractionGesture(new(_action,contact?.Part??BodyPart.Side,.2,dt,ObjectId:obj.Id),mouth);
            _supplyBiteTaken|=Sim.InteractionIntakeAccepted;
            _gesturePose=reply.Pose;AcceptFeedback(reply);
        }
        ShowInteractionTool();_hand.Visible=IsScenePoint(point);
        if(_toolFood!=null)_toolFood.Visible=obj.Portions>0;
        if(_cupWater!=null)_cupWater.Visible=obj.Portions>0;
        if(mouth&&contact!=null)PlaceContactTool(contact);
        else if(TryGroundPoint(point,.6f,out var position)){_hand.GlobalBasis=Basis.Identity;_hand.GlobalPosition=position;}
        else _hand.Visible=false;
        _lastPointer=point;
    }

    private void PlaceContactTool(Contact contact)
    {
            Vector3 normal=contact.Normal;
            if(_action is InteractionAction.Drink or InteractionAction.FeedMeal or InteractionAction.FeedSweet)
            {
                var forward=new Vector3(normal.X,0,normal.Z).Normalized();if(forward.LengthSquared()<.01f)forward=Vector3.Back;
                var right=Vector3.Up.Cross(forward);var basis=new Basis(right,Vector3.Up,forward);
                Vector3 offerPoint;
                if(_action==InteractionAction.Drink)
                {
                    basis=basis.Rotated(right,Mathf.DegToRad(-5));
                    var lip=InteractionToolSurface.CupLip;offerPoint=new(lip.X,lip.Y,lip.Z);
                }
                else
                {var foodPoint=_action==InteractionAction.FeedSweet?InteractionFoodSurface.SweetOffer:InteractionFoodSurface.MealOffer;offerPoint=new(foodPoint.X,foodPoint.Y,foodPoint.Z);}
                _hand.GlobalBasis=basis;_hand.GlobalPosition=contact.World-basis*offerPoint+normal*(_action==InteractionAction.FeedMeal?.005f:.003f);
            }
            else
            {
                _hand.GlobalBasis=new Basis(new Quaternion(Vector3.Up,normal));
                if(_action==InteractionAction.BurnFeet)
                {
                    _hand.GlobalBasis=new Basis(new Quaternion(Vector3.Up,-normal));
                    _hand.GlobalPosition=contact.World+normal*.016f;
                }
                else if(_action==InteractionAction.RepairAccessory)
                {
                    _hand.GlobalBasis*=Basis.FromEuler(new(Mathf.DegToRad(InteractionCareSurface.NeedleTiltDegrees),0,0));
                    var tip=InteractionCareSurface.NeedleTip;
                    _hand.GlobalPosition=contact.World-_hand.GlobalBasis*new Vector3(tip.X,tip.Y,tip.Z)+normal*.004f;
                }
                else if(_action is InteractionAction.Clean or InteractionAction.Dry or InteractionAction.CleanAccessory)
                    _hand.GlobalPosition=contact.World+normal*.034f;
                else if(_action is InteractionAction.Press or InteractionAction.Squeeze or InteractionAction.Hold or InteractionAction.Slap)
                    _hand.GlobalPosition=contact.World+normal*.052f;
                else if(_action==InteractionAction.Care)_hand.GlobalPosition=contact.World+normal*(-InteractionCareSurface.PadBottom+.003f);
                else
                {
                    _hand.GlobalBasis*=Basis.FromEuler(new(Mathf.DegToRad(InteractionToolSurface.FingerTiltDegrees),0,0));
                    var pad=InteractionToolSurface.FingerPad;
                    _hand.GlobalPosition=contact.World-_hand.GlobalBasis*new Vector3(pad.X,pad.Y,pad.Z)+normal*.003f;
                }
            }
    }

    private void UpdateTapTool(float dt)
    {
        if(_tapContact is not { } contact)return;
        _tapAge+=dt;
        if(_tapAge>=.22f){_tapContact=null;_hand.Visible=false;return;}
        PlaceContactTool(contact);
        float release=Mathf.Clamp((_tapAge-.045f)/.175f,0,1);
        _hand.GlobalPosition+=contact.Normal*(.14f*release*release);
    }

    private ContactStroke SampleStroke(Contact? contact,Vector2 pointer,bool touching)
    {
        if(contact==null||!touching){_strokeTracker.Reset();return new(0,System.Numerics.Vector2.Zero);}
        var actor=_actors[contact.Id];var local=actor.ContactCoordinate(contact.World,contact.Part);
        System.Numerics.Vector3? previousLocal=null;
        if(pointer!=_lastPointer)
        {
            var previous=IsScenePoint(_lastPointer)?ContactAt(_lastPointer):null;
            if(previous==null||previous.Id!=contact.Id||previous.Part!=contact.Part||previous.Side!=contact.Side||!CanUseContact(previous))
                _strokeTracker.Reset();
            else
            {
                var point=actor.ContactCoordinate(previous.World,previous.Part);
                previousLocal=new(point.X,point.Y,point.Z);
            }
        }
        return _strokeTracker.Sample(contact.Id,contact.Part,new(pointer.X,pointer.Y),new(local.X,local.Y,local.Z),actor.BodyScale,contact.Side,previousLocal);
    }

    private void UpdatePointer()
    {
        if(_pointer==null)return;
        Vector2 point=GetViewport().GetMousePosition();bool scene=_windowFocused&&IsScenePoint(point);
        var contact=scene?ContactAt(point):null;
        var roomObject=scene&&(!_toolArmed||_action==InteractionAction.PlayBall)?ObjectAt(point):null;
        _hoverId=contact?.Id;_hoverPart=contact?.Part??BodyPart.Side;
        _hoverLabel.Visible=(contact!=null||roomObject!=null)&&!_modalOpen;
        if(contact!=null)
        {
            _hoverLabel.Text=ContactName(contact)+(Sim.InteractionGestureTarget==contact.Id&&CanUseContact(contact)?
                " · "+(_appliedForce<.35?"轻触":_appliedForce<.62?"稍重":"用力"):
                !_toolArmed?" · 按住轻摸 / Shift 托起":"");
            _hoverLabel.Position=point+new Vector2(18,18);
        }
        if(roomObject!=null){_hoverLabel.Text=RoomResourceText.Label(roomObject,Sim.State);_hoverLabel.Position=point+new Vector2(18,18);}
        _pointer.Point=point;_pointer.Active=scene&&(contact!=null||roomObject!=null);
        _pointer.Rough=_mouseHeld?_appliedForce>.62f:_toolArmed&&_group.Rough;_pointer.Pressed=_mouseHeld;
        _pointer.DragOrigin=_dragStart;_pointer.Aim=_mouseHeld&&_action is InteractionAction.Push or InteractionAction.Throw or InteractionAction.PlayBall;
        _pointer.QueueRedraw();
    }
}

public partial class RoomPointer : Control
{
    public Vector2 Point,DragOrigin;
    public bool Active,Rough,Pressed,Aim;
    public override void _Draw()
    {
        var color=new Color(Rough?"ae6e51":"62846d");
        if(Active){DrawArc(Point,Pressed?12:9,0,Mathf.Tau,32,color,2,true);DrawCircle(Point,2,color);}
        if(Aim){DrawLine(DragOrigin,Point,color,2,true);DrawCircle(DragOrigin,4,color);}
    }
}

using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private RoomPointer _pointer=null!;
    private Vector2 _dragStart,_lastPointer,_gestureDirection;
    private Vector3 _dragWorldStart;
    private Vector3 _carryPointerOffset;
    private string _gesturePose="idle";
    private string? _hoverId;
    private BodyPart _hoverPart;
    private bool _mouseHeld;
    private string? _ballDragId;
    private record Contact(string Id,BodyPart Part,Vector3 World,Vector3 Normal,float Distance,bool Blocked,RestraintKind Fixture);
    private bool CanUseContact(Contact contact)=>!contact.Blocked||contact.Fixture switch
    {
        RestraintKind.Box=>_action==InteractionAction.OpenBox,
        RestraintKind.Wrap=>_action is InteractionAction.Unwrap or InteractionAction.Rock,
        RestraintKind.Frame=>_action==InteractionAction.Release,
        RestraintKind.Hanging=>_action is InteractionAction.Lower or InteractionAction.AdjustHanging,
        _=>false
    };
    private static string ContactName(Contact c)=>!c.Blocked?PartName(c.Part):c.Fixture switch
    {RestraintKind.Box=>"箱子",RestraintKind.Wrap=>"包裹",RestraintKind.Frame=>"固定具",RestraintKind.Hanging=>"悬挂支架",_=>"托举支撑"};

    private Vector3 GroundPoint(Vector2 point,float height=0)
    {
        var start=_camera.ProjectRayOrigin(point);var dir=_camera.ProjectRayNormal(point);
        if(Mathf.Abs(dir.Y)<.0001f)return Vector3.Zero;
        var hit=start+dir*((height-start.Y)/dir.Y);
        return new(Mathf.Clamp(hit.X,-7.7f,7.7f),height,Mathf.Clamp(hit.Z,-7.7f,7.7f));
    }
    private bool IsScenePoint(Vector2 point)
    {
        if(_modalOpen)return false;
        return point.Y>82&&point.Y<_ui.Size.Y-216&&point.X>226&&point.X<_ui.Size.X-282;
    }
    private Contact? ContactAt(Vector2 point)
    {
        Contact? best=null;float nearest=float.PositiveInfinity;
        Vector3 origin=_camera.ProjectRayOrigin(point),direction=_camera.ProjectRayNormal(point);
        foreach(var c in Sim.State.Creatures)
        {
            if(!_actors.TryGetValue(c.Id,out var actor)||!actor.Visible)continue;
            var hit=actor.RayContact(origin,direction);
            if(hit==null||hit.Distance>=nearest)continue;
            nearest=hit.Distance;
            best=new(c.Id,hit.Part,hit.Point,hit.Normal,hit.Distance,hit.Blocked,hit.Fixture);
        }
        return best;
    }
    private RoomObject? ObjectAt(Vector2 pointer)
    {
        return Sim.State.RoomObjects.Where(o=>!o.Stored&&o.Kind!=RoomObjectKind.Cushion)
            .Select(o=>(obj:o,distance:pointer.DistanceTo(_camera.UnprojectPosition(new Vector3((float)o.X,.15f+(float)o.Height,(float)o.Z)))))
            .Where(p=>p.distance<Mathf.Max(13,180/_camera.Size)).OrderBy(p=>p.distance).Select(p=>p.obj).FirstOrDefault();
    }
    private static string PartName(BodyPart part)=>part switch{BodyPart.Head=>"头顶",BodyPart.Cheek=>"脸侧",BodyPart.Side=>"身体侧面",BodyPart.Bottom=>"底部",BodyPart.Mouth=>"嘴边",_=>"饰品"};

    public override void _Input(InputEvent input)
    {
        if(!_ready)return;
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:false}&&_mouseHeld)
        {
            _mouseHeld=false;_ballDragId=null;AcceptFeedback(Sim.EndInteractionGesture());_hand.Visible=false;
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
            {var obj=ObjectAt(GetViewport().GetMousePosition());if(obj!=null){Sim.PutAwayRoomObject(obj.Id);_message="已经收起地上的物品。";}}
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if(!_ready||_modalOpen)return;
        if(input is not InputEventMouseButton mouse||!mouse.Pressed)return;
        if(!IsScenePoint(mouse.Position))return;
        if(mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            float step=mouse.ButtonIndex==MouseButton.WheelUp?1:-1;
            if(mouse.ShiftPressed)_height=Mathf.Clamp(_height+step*.15f,.4f,3);
            else _zoom=Mathf.Clamp(_zoom-step,9,23);
            GetViewport().SetInputAsHandled();return;
        }
        if(mouse.ButtonIndex!=MouseButton.Left)return;
        if(Sim.State.Settings.Pause){_message="小院暂停中，先点“继续”或按空格。";return;}
        var contact=ContactAt(mouse.Position);
        if(mouse.DoubleClick&&contact!=null){SelectOil(contact.Id);ToggleClose();return;}
        if(!_toolArmed){if(contact!=null)SelectOil(contact.Id);return;}
        var ground=GroundPoint(mouse.Position);
        bool placement=_action is InteractionAction.PlaceMeal or InteractionAction.PlaceSweet or InteractionAction.PutDown or InteractionAction.GiveBall;
        if(placement)
        {
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
        if(_action==InteractionAction.AdjustHanging||_action==InteractionAction.Lift&&selected.Interaction.Carried)
            _height=(float)selected.Interaction.Height;
        _dragStart=mouse.Position;_lastPointer=mouse.Position;_dragWorldStart=ground;_gestureDirection=Vector2.Zero;
        _carryPointerOffset=new Vector3((float)selected.X,0,(float)selected.Z)-GroundPoint(mouse.Position,_height+.46f*_actors[selected.Id].BodyScale);
        _carryPointerOffset.Y=0;
        var part=contact?.Part??BodyPart.Side;
        var request=new InteractionInput(_action,part,_force,X:ground.X,Z:ground.Z,Height:_height);
        if(InteractionCatalog.Choice(_action).Continuous)
        {
            var feedback=Sim.BeginInteractionGesture(selected.Id,request);AcceptFeedback(feedback);
            _mouseHeld=Sim.HasInteractionGesture;_gesturePose=feedback.Pose;
        }
        else AcceptFeedback(Sim.ExecuteInteraction(selected.Id,request));
        GetViewport().SetInputAsHandled();
    }

    private void CancelGesture()
    {
        if(Sim?.HasInteractionGesture==true)AcceptFeedback(Sim.EndInteractionGesture(true));
        _mouseHeld=false;_ballDragId=null;if(_hand!=null)_hand.Visible=false;
    }
    private void ProcessRoomGesture(float dt)
    {
        if(!_mouseHeld||!Sim.HasInteractionGesture)return;
        if(!Input.IsMouseButtonPressed(MouseButton.Left)){CancelGesture();return;}
        Vector2 current=GetViewport().GetMousePosition();var contact=IsScenePoint(current)?ContactAt(current):null;
        bool touching=contact!=null&&contact.Id==Sim.InteractionGestureTarget&&CanUseContact(contact);
        var ground=GroundPoint(current);var worldMotion=ground-GroundPoint(_lastPointer);
        _gestureDirection=_gestureDirection.Lerp((current-_lastPointer).LimitLength(6)/6,Mathf.Min(1,dt*16));
        Vector3 direction=ground-_dragWorldStart;
        var carriedTarget=_action is InteractionAction.Lift or InteractionAction.AdjustHanging?GroundPoint(current,_height+.46f*_actors[Sim.InteractionGestureTarget!].BodyScale)+_carryPointerOffset:ground;
        var input=new InteractionInput(_action,contact?.Part??BodyPart.Side,_force,dt,worldMotion.Length(),Math.Clamp(carriedTarget.X,-7.7,7.7),Math.Clamp(carriedTarget.Z,-7.7,7.7),_height,direction.X,direction.Z);
        // Dragging off the scene never creates force, consumes a dose, or drops a held oil.
        if(!IsScenePoint(current))
        {
            // Tell the simulation that contact really broke, including mouth/care dwell.
            if(_action is not (InteractionAction.Lift or InteractionAction.Throw or InteractionAction.PlayBall or InteractionAction.AdjustHanging))
                Sim.ContinueInteractionGesture(input,false);
            _gesturePose="idle";_hand.Visible=false;_lastPointer=current;return;
        }
        var feedback=Sim.ContinueInteractionGesture(input,touching);
        _gesturePose=feedback.Pose;AcceptFeedback(feedback,Sim.InteractionGestureTarget);
        ShowInteractionTool();
        _hand.Visible=touching&&_action is not (InteractionAction.Lift or InteractionAction.Throw);
        if(contact!=null)
        {
            Vector3 normal=contact.Normal;
            _hand.GlobalBasis=new Basis(new Quaternion(Vector3.Up,normal));
            float clearance=_action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink or InteractionAction.Tease?.12f:.065f;
            _hand.GlobalPosition=contact.World+normal*clearance;
        }
        _lastPointer=current;
    }

    private void UpdatePointer()
    {
        if(_pointer==null)return;
        Vector2 point=GetViewport().GetMousePosition();bool scene=IsScenePoint(point);
        var contact=scene?ContactAt(point):null;
        _hoverId=contact?.Id;_hoverPart=contact?.Part??BodyPart.Side;
        _hoverLabel.Visible=contact!=null&&!_modalOpen;
        if(contact!=null)
        {
            _hoverLabel.Text=ContactName(contact)+(Sim.HasInteractionGesture&&CanUseContact(contact)?" · 接触中":"");
            _hoverLabel.Position=point+new Vector2(18,18);
        }
        _pointer.Point=point;_pointer.Active=scene&&contact!=null;
        _pointer.Rough=_group.Rough;_pointer.Pressed=_mouseHeld;
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

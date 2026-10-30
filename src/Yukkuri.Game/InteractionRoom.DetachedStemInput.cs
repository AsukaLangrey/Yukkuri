using Godot;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private readonly Dictionary<string,DetachedStemActor> _detachedStemActors=new();
    private bool _detachedLiveContact,_detachedActionComplete;
    private string? _detachedSupplyContactId;
    private Vector3 _detachedSupplyContactPoint;

    private void ResetDetachedStemActors()
    {
        foreach(var actor in _detachedStemActors.Values)actor.QueueFree();
        _detachedStemActors.Clear();
    }
    private void SynchronizeDetachedStemActors()
    {
        var visible=Sim.DetachedStems(Sim.State.ActiveRegionId).Where(s=>!s.Cleared).ToArray();
        foreach(var id in _detachedStemActors.Keys.Where(id=>!visible.Any(s=>s.EventId==id)).ToArray())
        {_detachedStemActors[id].QueueFree();_detachedStemActors.Remove(id);}
        foreach(var state in visible)
        {
            if(!_detachedStemActors.TryGetValue(state.EventId,out var actor))
            {actor=new DetachedStemActor();AddChild(actor);actor.Build();_detachedStemActors[state.EventId]=actor;}
            actor.Visible=Sim.State.Mode!=PlayMode.CloseUp;
            actor.Present(state,_selectedDetachedStemEventId==state.EventId);
        }
    }
    private string DetachedStemHint(Contact contact)
    {
        var state=Sim.DetachedStemStatus(contact.Id);
        if(state==null)return "这一窝已不在这里";
        if(state.Phase is DetachedStemPhase.Failed or DetachedStemPhase.Born)
            return (state.Phase==DetachedStemPhase.Born?"出生后的枯梗残留":"枯损离体茎 · "+state.LastDamageCause)+
                (_toolArmed&&_action==InteractionAction.ClearStem?" · 按住收起残留，记录保留":" · 点击查看 / 选择收起残留工具");
        if(_heldSupplyId!=null)return contact.Part==BodyPart.Mouth?"瓶口 · 持续递入实际食水，移开重新计时":"培养瓶 · 请对准上方开口";
        if(_detachedActionComplete&&MatchesGestureContact(contact))return "已完成 · 松开后再操作";
        if(_waterRescueToolArmed)return "离体茎 · 救援用品只用于落水油";
        if(!_toolArmed||_action==InteractionAction.Lift)return state.Phase==DetachedStemPhase.Bottled?
            "培养瓶 · 按住低托拖动 / 松开放稳":"离体茎 · 点击查看，选择装瓶工具";
        return _action switch
        {
            InteractionAction.BottleStem=>"离体茎 · 持续接触装瓶，完成才消耗培养瓶",
            InteractionAction.Burn=>"离体茎 · 长按灼烧 / 松开停止",
            InteractionAction.Punch=>"离体茎 · 点击击打，损伤只作用这一窝",
            InteractionAction.Drink or InteractionAction.FeedMeal=>"培养瓶 · 先拿起地面实际食水，再递到瓶口",
            _=>"离体茎 · 点击查看；当前工具按独立规则判断"
        };
    }
    private void BeginDetachedPointer(Contact contact,Vector2 pointer)
    {
        // Observation keeps its own EventId; never select the mother as a stand-in.
        bool armed=_toolArmed,rescue=_waterRescueToolArmed;
        if(_selectedDetachedStemEventId!=contact.Id)SelectDetachedStem(contact.Id);
        else CancelGesture();
        if(Sim.State.Settings.Pause){_message="小院暂停中，先继续时间。";return;}
        if(rescue){_message="救援用品用于落水油；这一窝请使用装瓶和实际食水。";return;}
        if(!armed)_action=InteractionAction.Lift;
        var state=Sim.DetachedStemStatus(contact.Id);
        if(state==null||state.Cleared||_action!=InteractionAction.ClearStem&&(state.Phase is DetachedStemPhase.Failed or DetachedStemPhase.Born))
        {_message=state?.RefusalReason??"这一窝已结束，记录保留在观察面板。";return;}
        if(IsFoodTool(_action))
        {_message="先拿起地面的实际水杯或正餐，再按住递到培养瓶上方开口。";return;}
        _gestureTargetKind=InteractionTargetKind.DetachedStem;_gestureTargetObject=contact.Id;
        _dragStart=_lastPointer=pointer;_lastGestureSampleUsec=Time.GetTicksUsec();
        _directGesture=false;_appliedForce=_force;
        var input=new InteractionInput(_action,contact.Part,_force,ObjectId:contact.Id,TargetKind:InteractionTargetKind.DetachedStem);
        var reply=_action==InteractionAction.Punch?Sim.ExecuteInteraction(contact.Id,input):Sim.BeginDetachedStemGesture(contact.Id,input);
        AcceptFeedback(reply);_gesturePose=reply.Pose;_mouseHeld=Sim.HasInteractionGesture;
        if(reply.Success&&reply.Changed&&_action==InteractionAction.Punch)
        {_tapContact=contact;_tapAge=0;_hitTarget=null;ShowInteractionTool();PlaceContactTool(contact);_hand.Visible=true;}
        if(_mouseHeld){ShowInteractionTool();PlaceContactTool(contact);_hand.Visible=_action!=InteractionAction.Lift;}
    }
    private void ProcessDetachedGesture(float dt,double sampleSeconds,bool releasing)
    {
        _detachedLiveContact=false;
        if(!_mouseHeld||!Sim.HasInteractionGesture||Sim.State.Settings.Pause||Sim.State.Mode==PlayMode.CloseUp||
            (!releasing&&!Input.IsMouseButtonPressed(MouseButton.Left)))
        {CancelGesture();return;}
        ulong now=Time.GetTicksUsec();
        double sinceSample=(now-_lastGestureSampleUsec)/1_000_000.0;_lastGestureSampleUsec=now;
        Vector2 point=GetViewport().GetMousePosition();
        var contact=IsScenePoint(point)?ContactAt(point):null;
        bool touching=contact!=null&&MatchesGestureContact(contact);
        var state=Sim.DetachedStemStatus(_gestureTargetObject!);
        if(state==null||state.RegionId!=Sim.State.ActiveRegionId){CancelGesture();return;}
        Vector3 ground=new((float)state.X,0,(float)state.Z);
        if(_action==InteractionAction.Lift&&IsScenePoint(point)&&IsScenePoint(_lastPointer)&&
            sampleSeconds>0&&sampleSeconds<=ContactMotion.MaximumSampleSeconds&&
            sinceSample>0&&sinceSample<=ContactMotion.MaximumSampleSeconds)
            ground+=SupportDrag(point-_lastPointer);
        var reply=Sim.ContinueInteractionGesture(new(_action,contact?.Part??BodyPart.Side,_force,dt,
            X:ground.X,Z:ground.Z,ObjectId:_gestureTargetObject,TargetKind:InteractionTargetKind.DetachedStem),touching);
        AcceptFeedback(reply);_gesturePose=reply.Pose;
        _detachedActionComplete|=(_action is InteractionAction.BottleStem or InteractionAction.ClearStem)&&reply.Success&&reply.Changed;
        _detachedLiveContact=touching&&reply.Success&&Sim.HasInteractionGesture&&!_detachedActionComplete;
        ShowInteractionTool();_hand.Visible=_detachedLiveContact&&_action!=InteractionAction.Lift;
        if(touching&&contact!=null)PlaceContactTool(contact);
        _lastPointer=point;
        if(_action==InteractionAction.ClearStem&&_detachedActionComplete)
        {AcceptFeedback(Sim.EndInteractionGesture());_mouseHeld=false;_hand.Visible=false;ResetContactMotion();return;}
        if(!Sim.HasInteractionGesture){_mouseHeld=false;_hand.Visible=false;ResetContactMotion();}
    }

    // Returns true while the held source is aimed at, or still bound to, a bottle.
    // Its original source remains held until the ordinary pointer release path.
    private bool ProcessDetachedSupply(RoomObject source,Contact? contact,Vector2 point,float dt)
    {
        bool detached=contact?.TargetKind==InteractionTargetKind.DetachedStem;
        bool bound=_gestureTargetKind==InteractionTargetKind.DetachedStem&&Sim.CurrentInteractionTargetKind==InteractionTargetKind.DetachedStem;
        if(!detached&&!bound)
        {if(_gestureTargetKind==InteractionTargetKind.DetachedStem)ResetContactMotion();return false;}
        if(Sim.State.Settings.Pause||!_windowFocused||_modalOpen||Sim.State.Mode==PlayMode.CloseUp)
        {CancelGesture();return true;}
        if(bound&&contact!=null&&(!detached||contact.Id!=_gestureTargetObject))
        {AcceptFeedback(Sim.EndInteractionGesture(true));ResetContactMotion();bound=false;}
        if(!detached&&!bound)return false;
        if(detached&&contact!=null&&_selectedDetachedStemEventId!=contact.Id)SelectDetachedStem(contact.Id,false);
        bool mouth=detached&&contact is {Part:BodyPart.Mouth,Blocked:false};
        // A new opening or a large tool jump first establishes real contact.
        bool settled=mouth&&contact!=null&&_detachedSupplyContactId==contact.Id&&
            _detachedSupplyContactPoint.DistanceTo(contact.World)<.035f;
        ShowInteractionTool();
        if(mouth&&contact!=null)PlaceContactTool(contact);
        _detachedSupplyContactId=mouth?contact!.Id:null;
        if(mouth)_detachedSupplyContactPoint=contact!.World;
        if(detached&&Sim.HasInteractionGesture&&!bound)AcceptFeedback(Sim.EndInteractionGesture(true));
        if(mouth&&contact!=null&&!Sim.HasInteractionGesture&&!_supplyBiteTaken&&source.Portions>0)
        {
            _gestureTargetKind=InteractionTargetKind.DetachedStem;_gestureTargetObject=contact.Id;
            _detachedActionComplete=false;
            var start=Sim.BeginDetachedStemGesture(contact.Id,new(_action,BodyPart.Mouth,.2,dt,
                ObjectId:contact.Id,TargetKind:InteractionTargetKind.DetachedStem,SupplyObjectId:source.Id));
            AcceptFeedback(start);_gesturePose=start.Pose;
            // A refused offer is one attempt for this pickup, not a per-frame retry.
            if(!start.Success)_supplyBiteTaken=true;
        }
        if(Sim.HasInteractionGesture&&_gestureTargetKind==InteractionTargetKind.DetachedStem)
        {
            var reply=Sim.ContinueInteractionGesture(new(_action,BodyPart.Mouth,.2,dt,
                ObjectId:_gestureTargetObject,TargetKind:InteractionTargetKind.DetachedStem,SupplyObjectId:source.Id),
                settled&&contact?.Id==_gestureTargetObject);
            _supplyBiteTaken|=reply.Success&&reply.Changed;
            _supplyBiteTaken|=!reply.Success||reply.Refused;
            _detachedActionComplete|=reply.Success&&reply.Changed;
            _gesturePose=reply.Pose;AcceptFeedback(reply);
        }
        ShowInteractionTool();_hand.Visible=IsScenePoint(point);
        if(_toolFood!=null)_toolFood.Visible=source.Portions>0;
        if(_cupWater!=null)SetCupFill(_cupWater,(float)NaturalResources.VisibleWaterFill(Sim.State,source));
        if(mouth&&contact!=null)PlaceContactTool(contact);
        else if(TryGroundPoint(point,.6f,out var position)){_hand.GlobalBasis=Basis.Identity;_hand.GlobalPosition=position;}
        else _hand.Visible=false;
        _lastPointer=point;return true;
    }
}

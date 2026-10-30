using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private string? _clickOfferId;
    private Vector2 _clickOfferOrigin;
    private float _clickOfferAge;
    private const float OfferApproachSeconds=.16f,OfferApproachDistance=.14f,OfferReturnSeconds=.22f;
    private string? _offerReturnId;
    private Vector2 _offerReturnTargetOrigin;
    private Vector3 _offerReturnOrigin,_offerReturnDirection;
    private float _offerReturnAge;
    private static bool IsFoodTool(InteractionAction action)=>action is
        InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink;
    private static InteractionAction GroundFoodAction(InteractionAction action)=>action switch
    {
        InteractionAction.FeedSweet=>InteractionAction.PlaceSweet,
        InteractionAction.Drink=>InteractionAction.PlaceWater,
        _=>InteractionAction.PlaceMeal
    };

    private Contact? MouthOfferContact(string id)
    {
        if(!_actors.TryGetValue(id,out var actor)||!actor.Visible)return null;
        var mouth=actor.ContactPoint(BodyPart.Mouth);
        var normal=actor.ContactNormal(BodyPart.Mouth,mouth);
        var origin=mouth+normal*.38f;
        // Approach the actual mouth surface. A fixture or another body cannot be fed through.
        var hit=actor.RayContact(origin,-normal);
        if(hit==null||hit.Blocked||hit.TargetKind!=InteractionTargetKind.Body||hit.Part!=BodyPart.Mouth||hit.Distance>.42f)return null;
        if(SceneOcclusionDistance(origin,-normal)<hit.Distance)return null;
        foreach(var other in _actors)
        {
            if(other.Key==id||!other.Value.Visible)continue;
            var obstruction=other.Value.RayContact(origin,-normal);
            if(obstruction!=null&&obstruction.Distance<hit.Distance)return null;
        }
        return new(id,BodyPart.Mouth,hit.Point,hit.Normal,hit.Distance,false,RestraintKind.None,SurfaceSide.Whole);
    }

    private void BeginClickOffer(Contact selectedContact)
    {
        CancelGesture();
        if(selectedContact.TargetKind!=InteractionTargetKind.Body)return;
        if(!CanUseContact(selectedContact))
        {_message="装置挡住了它。先解除遮挡，再把食水递过去。";return;}
        if(selectedContact.Id!=Sim.State.SelectedId)SelectOil(selectedContact.Id);
        var contact=MouthOfferContact(selectedContact.Id);
        if(contact==null){_message="嘴边被挡住了。先调整位置或解除遮挡，再递食水。";return;}
        var oil=Sim.Selected;if(oil==null)return;
        var feedback=Sim.BeginInteractionGesture(oil.Id,new(_action,BodyPart.Mouth,.12f));
        AcceptFeedback(feedback);
        if(!Sim.HasInteractionGesture)return;
        if(!feedback.Success||feedback.Refused){Sim.EndInteractionGesture(true);return;}
        _clickOfferId=oil.Id;_clickOfferOrigin=new((float)oil.X,(float)oil.Z);_clickOfferAge=0;
        _appliedForce=.12f;_gesturePose=feedback.Pose;
        ShowInteractionTool();
        if(_toolFood!=null)_toolFood.Visible=true;
        if(_cupWater!=null)SetCupFill(_cupWater,1);
        PlaceContactTool(contact);_hand.GlobalPosition+=contact.Normal*OfferApproachDistance;_hand.Visible=true;
        _message="正在递到嘴边，等它接下这一口。右键可收回。";
    }

    private void ProcessClickOffer(float dt)
    {
        if(_clickOfferId is not { } id)return;
        if(!Sim.HasInteractionGesture||Sim.InteractionGestureTarget!=id){CancelGesture();return;}
        if(Sim.State.Settings.Pause||!_windowFocused||_modalOpen){CancelGesture();return;}
        _clickOfferAge+=dt;
        var oil=Sim.State.Creatures.Find(c=>c.Id==id);
        var contact=MouthOfferContact(id);
        if(oil==null||contact==null||oil.Condition==PhysicalCondition.Dead||oil.Family.ColdStorage.Stored||_clickOfferAge>3||
            new Vector2((float)oil.X,(float)oil.Z).DistanceTo(_clickOfferOrigin)>.55f)
        {CancelGesture();_message="它挪开了，或嘴边被挡住了。这一份先收回。";return;}
        PlaceContactTool(contact);_hand.Visible=true;
        float approach=Mathf.Clamp(_clickOfferAge/OfferApproachSeconds,0,1);
        _hand.GlobalPosition+=contact.Normal*(OfferApproachDistance*(1-approach)*(1-approach));
        if(approach<1)return;
        // Only the part of this frame after arriving at the actual clear mouth
        // can count as contact. The approach is anticipation, never free intake.
        float contactSeconds=Mathf.Min(dt,_clickOfferAge-OfferApproachSeconds);
        if(contactSeconds<=0)return;
        var feedback=Sim.ContinueInteractionGesture(new(_action,BodyPart.Mouth,.12f,contactSeconds),true);
        _gesturePose=feedback.Pose;AcceptFeedback(feedback,id);
        if(Sim.InteractionIntakeAccepted||feedback.Refused||!feedback.Success)
        {
            bool accepted=Sim.InteractionIntakeAccepted&&feedback.Success&&!feedback.Refused;
            // Settle the gesture once. The return animation has no simulation command.
            _clickOfferId=null;
            var ended=Sim.EndInteractionGesture(!accepted);
            if(accepted)AcceptFeedback(ended);
            if(accepted&&_action is InteractionAction.FeedMeal or InteractionAction.Drink)
            {
                if(_toolFood!=null)_toolFood.Visible=false;
                if(_cupWater!=null)SetCupFill(_cupWater,0);
                _offerReturnId=id;_offerReturnTargetOrigin=new((float)oil.X,(float)oil.Z);
                _offerReturnOrigin=_hand.GlobalPosition;_offerReturnDirection=contact.Normal;_offerReturnAge=0;
            }
            else _hand.Visible=false;
        }
    }

    private void UpdateOfferReturn(float dt,bool paused)
    {
        if(_offerReturnId is not { } id)return;
        if(paused||!_windowFocused||_modalOpen){CancelGesture();return;}
        var oil=Sim.State.Creatures.Find(c=>c.Id==id);
        if(oil==null||oil.Condition==PhysicalCondition.Dead||MouthOfferContact(id)==null||
            new Vector2((float)oil.X,(float)oil.Z).DistanceTo(_offerReturnTargetOrigin)>.3f)
        {_offerReturnId=null;_hand.Visible=false;return;}
        _offerReturnAge+=dt;
        float progress=Mathf.Clamp(_offerReturnAge/OfferReturnSeconds,0,1);
        _hand.GlobalPosition=_offerReturnOrigin+_offerReturnDirection*(.20f*progress*progress*(3-2*progress));
        if(progress>=1){_offerReturnId=null;_hand.Visible=false;}
    }
}

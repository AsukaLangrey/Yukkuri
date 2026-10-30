using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private string? _clickOfferId;
    private Vector2 _clickOfferOrigin;
    private float _clickOfferAge;
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
        if(hit==null||hit.Blocked||hit.Part!=BodyPart.Mouth||hit.Distance>.42f)return null;
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
        if(!CanUseContact(selectedContact))
        {_message="装置挡住了它。先解除遮挡，再把食水递过去。";return;}
        if(selectedContact.Id!=Sim.State.SelectedId)SelectOil(selectedContact.Id);
        var contact=MouthOfferContact(selectedContact.Id);
        if(contact==null){_message="嘴边被挡住了。先调整位置或解除遮挡，再递食水。";return;}
        var oil=Sim.Selected;if(oil==null)return;
        var feedback=Sim.BeginInteractionGesture(oil.Id,new(_action,BodyPart.Mouth,.12f));
        AcceptFeedback(feedback);
        if(!Sim.HasInteractionGesture)return;
        _clickOfferId=oil.Id;_clickOfferOrigin=new((float)oil.X,(float)oil.Z);_clickOfferAge=0;
        _appliedForce=.12f;_gesturePose=feedback.Pose;
        ShowInteractionTool();PlaceContactTool(contact);_hand.Visible=true;
        _message="正在递到嘴边，等它接下这一口。右键可收回。";
    }

    private void ProcessClickOffer(float dt)
    {
        if(_clickOfferId is not { } id)return;
        if(!Sim.HasInteractionGesture||Sim.InteractionGestureTarget!=id){CancelGesture();return;}
        if(Sim.State.Settings.Pause)return;
        _clickOfferAge+=dt;
        var oil=Sim.State.Creatures.Find(c=>c.Id==id);
        var contact=MouthOfferContact(id);
        if(oil==null||contact==null||_clickOfferAge>3||
            new Vector2((float)oil.X,(float)oil.Z).DistanceTo(_clickOfferOrigin)>.55f)
        {CancelGesture();_message="它挪开了，或嘴边被挡住了。这一份先收回。";return;}
        var feedback=Sim.ContinueInteractionGesture(new(_action,BodyPart.Mouth,.12f,dt),true);
        _gesturePose=feedback.Pose;AcceptFeedback(feedback,id);
        PlaceContactTool(contact);_hand.Visible=true;
        if(Sim.InteractionIntakeAccepted||feedback.Refused||!feedback.Success)
        {
            // One click offers one portion. Intake continues in the simulation after the tool retracts.
            _clickOfferId=null;
            AcceptFeedback(Sim.EndInteractionGesture());_hand.Visible=false;
        }
    }
}

using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Creature? _heatOwner;
    private string? _heatRegion;
    private Vector2 _heatPreviousPosition;
    private bool _heatTravelReady;
    private float? _heatHeading;

    private void ApplyHeatBodyPose(Creature c,string pose,float delta,bool paused,bool close)
    {
        var life=c.Interaction;
        bool eligible=Visible&&c.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)&&
            !HasWaterBodyPresentation(c)&&!life.Carried&&!life.Falling&&!life.Rolling&&!life.Supine&&!life.Resting&&
            !c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&!FamilyRules.UnderIntervention(c);
        bool fleeing=eligible&&pose=="burn-flee"&&WorldSimulation.CanSelfMove(c);
        bool timely=float.IsFinite(delta)&&delta>0&&delta<=ContactMotion.MaximumSampleSeconds;
        bool liveTravel=fleeing&&!paused&&!close&&timely;
        var position=new Vector2((float)c.X,(float)c.Z);
        bool sameOwner=ReferenceEquals(_heatOwner,c)&&_heatRegion==c.RegionId;
        Vector2 travel=sameOwner&&_heatTravelReady&&liveTravel?position-_heatPreviousPosition:Vector2.Zero;
        if(!sameOwner||!fleeing||close)_heatHeading=null;
        // Read every current position, even when unavailable, so loading, changing
        // region/view or resuming cannot borrow travel from a hidden interval.
        _heatOwner=c;_heatRegion=c.RegionId;_heatPreviousPosition=position;_heatTravelReady=liveTravel;
        if(!eligible)return;
        bool fixedBody=life.Restraint!=RestraintKind.None;
        if(ActorHeatPresentation.Sample(life.HeatTension,fixedBody,fleeing,new(travel.X,travel.Y)) is not { } frame)
        {_heatHeading=null;return;}
        // Preserve the existing support orientation. Residual heat is a small
        // contraction, not another burn/escape event or a periodic fake jump.
        _visual.Scale*=AccessoryPoint(frame.Scale);
        _visual.RotationDegrees+=new Vector3(frame.Pitch,0,0);
        if(frame.Heading is { } heading)_heatHeading=heading;
        // A blocked continuation keeps the last heading actually earned by
        // travel; it must not turn back to the default front every still frame.
        if(_heatHeading is { } facing)
        {
            var rotation=_visual.Rotation;rotation.Y=facing;_visual.Rotation=rotation;
        }
    }
}

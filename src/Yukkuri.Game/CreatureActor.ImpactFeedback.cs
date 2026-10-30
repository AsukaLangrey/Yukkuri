using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Creature? _impactOwner;
    private int _impactSerial;
    private bool _guardedImpactActive;
    private float _guardedImpactAge;
    private string _guardedImpactPose="";
    private Vector2 _guardedImpactDirection;

    private void ObserveGuardedImpact(Creature c,string pose,float delta,bool paused)
    {
        // Rebuilt actors observe a baseline, not an invitation to replay a saved
        // reaction. React clears the old pose before incrementing its serial;
        // only Feedback then supplies the new resolved impact pose below.
        if(_impactOwner!=c)
        {_impactOwner=c;_impactSerial=c.ReactionSerial;_guardedImpactActive=false;return;}
        bool fresh=_impactSerial!=c.ReactionSerial;
        _impactSerial=c.ReactionSerial;
        var life=c.Interaction;
        bool valid=Visible&&pose=="guard"&&c.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)&&
            !life.Carried&&!life.Falling&&!life.Rolling&&!life.Supine&&!life.Resting&&!HasWaterBodyPresentation(c)&&
            !c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&!FamilyRules.UnderIntervention(c)&&
            life.ReactionPoseRemaining>0&&(life.ReactionPose is "slap" or "flick" or "poke" or "land");
        if(!valid||fresh&&paused)
        {_guardedImpactActive=false;return;}
        if(fresh)
        {
            _guardedImpactActive=true;_guardedImpactAge=0;_guardedImpactPose=life.ReactionPose;
            _guardedImpactDirection=new((float)life.ReactionDirectionX,(float)life.ReactionDirectionZ);
        }
        else if(_guardedImpactActive)
        {
            if(_guardedImpactPose!=life.ReactionPose){_guardedImpactActive=false;return;}
            if(!paused&&float.IsFinite(delta)&&delta>0)_guardedImpactAge+=Mathf.Min(delta,.1f);
            if(_guardedImpactAge>=ActorReactionMotion.GuardedImpactDuration)_guardedImpactActive=false;
        }
    }

    private void ApplyGuardedImpact(Creature c)
    {
        if(!_guardedImpactActive)return;
        // Direction is stored in world ground coordinates. Convert it after the
        // ordinary heading has settled so recoil still points away from contact.
        float yaw=_visual.Rotation.Y,sine=Mathf.Sin(yaw),cosine=Mathf.Cos(yaw);
        var direction=new System.Numerics.Vector2(_guardedImpactDirection.X*cosine-_guardedImpactDirection.Y*sine,
            _guardedImpactDirection.X*sine+_guardedImpactDirection.Y*cosine);
        if(ActorReactionMotion.SampleGuardedImpact(_guardedImpactPose,_guardedImpactAge,direction,
            c.Interaction.Restraint!=RestraintKind.None) is not { } motion)return;
        _visual.Scale*=AccessoryPoint(motion.Scale);
        _visual.RotationDegrees+=AccessoryPoint(motion.RotationDegrees);
    }
}

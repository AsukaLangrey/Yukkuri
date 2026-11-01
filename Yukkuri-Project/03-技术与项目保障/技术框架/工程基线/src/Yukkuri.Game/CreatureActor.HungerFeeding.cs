using Godot;
using Yukkuri.Simulation;
using Expression=Yukkuri.Simulation.Expression;

public partial class CreatureActor
{
    private Creature? _hungerCueOwner,_hungerCueTarget;
    private string? _hungerCueEventId;
    private HungerFeedingPhase _hungerCuePhase;
    private bool _hungerCueActive,_hungerCueInvalidated;
    private float _hungerCueProgress,_hungerCueUnease;

    // This observes the simulation's current intention. It never starts a bite,
    // moves either body, advances preparation, or creates an accepted mouthful.
    public void PrepareHungerFeeding(Creature c,HungerFeedingView? feeding,Creature? target,
        string pose,bool paused,bool close)
    {
        bool same=_hungerCueOwner==c&&_hungerCueTarget==target&&
            _hungerCueEventId==feeding?.EventId&&_hungerCuePhase==feeding?.Phase;
        bool valid=!close&&Visible&&feeding!=null&&target!=null&&target.Id==feeding.TargetId&&
            target.RegionId==c.RegionId&&(feeding.Phase is HungerFeedingPhase.Pursuing or HungerFeedingPhase.Preparing)&&
            c.Condition==PhysicalCondition.Active&&c.Interaction.EatingRemaining<=0&&
            !_waterConcernActive&&!c.Interaction.Carried&&!c.Interaction.Falling&&!c.Interaction.Rolling&&
            !c.Interaction.Supine&&!c.Interaction.Resting&&!HasWaterBodyPresentation(c)&&
            c.Interaction.Restraint==RestraintKind.None&&!FamilyRules.UnderIntervention(c)&&
            pose is "idle" or "guard" or "careful-step";
        if(!valid||paused&&(!same||!_hungerCueActive))
        {
            _hungerCueInvalidated|=_hungerCueActive;
            _hungerCueActive=false;_hungerCueProgress=0;_hungerCueUnease=0;
            _hungerCueOwner=c;_hungerCueTarget=null;_hungerCueEventId=null;return;
        }
        // Pausing holds an already observed intention, but a changed/invalid
        // identity above clears it even when the ordinary world clock is stopped.
        if(paused)return;
        if(!same&&_hungerCueActive)_hungerCueInvalidated=true;
        _hungerCueOwner=c;_hungerCueTarget=target;_hungerCueEventId=feeding!.EventId;
        _hungerCuePhase=feeding.Phase;_hungerCueActive=true;
        _hungerCueProgress=feeding.Phase==HungerFeedingPhase.Preparing?
            Mathf.Clamp((float)feeding.ContactProgress,0,1):0;
        _hungerCueUnease=Mathf.Clamp(Mathf.Max((float)c.Pain/40,
            Mathf.Max((float)c.Fear/65,(float)c.Interaction.Emotion.Alarm/100)),0,1);
    }

    private Expression HungerFeedingExpression(Expression expression)
    {
        if(!_hungerCueActive||_hungerCueUnease>.3f)return expression;
        // Explicit pain, fear, grief and requests for help retain their own face.
        return expression is Expression.Neutral or Expression.Curious or Expression.Content or Expression.Delighted?
            Expression.Angry:expression;
    }

    private void HungerFeedingEyes(Expression expression,ref float eyes,ref EyeCurve curve,ref float left,ref float right)
    {
        if(!_hungerCueActive||expression is not (Expression.Angry or Expression.Defiant or Expression.Neutral or Expression.Curious))return;
        float pressure=_hungerCueProgress*_hungerCueProgress*(3-2*_hungerCueProgress);
        float focus=1-_hungerCueUnease;
        eyes=Mathf.Lerp(eyes,Mathf.Lerp(.72f,.49f,pressure),focus);
        left=Mathf.Lerp(left,Mathf.Lerp(-11,-23,pressure),focus);right=Mathf.Lerp(right,Mathf.Lerp(11,23,pressure),focus);
        // Alarm remains visible even while hunger keeps the creature hostile.
        eyes=Mathf.Lerp(eyes,.94f,_hungerCueUnease);
        left=Mathf.Lerp(left,17,_hungerCueUnease);right=Mathf.Lerp(right,-17,_hungerCueUnease);
        curve=EyeCurve.Open;
    }
}

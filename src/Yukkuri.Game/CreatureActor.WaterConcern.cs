using Godot;
using Yukkuri.Simulation;
using Expression=Yukkuri.Simulation.Expression;

public partial class CreatureActor
{
    private bool _waterConcernActive,_waterConcernKind,_waterConcernMock,_waterConcernFacing;
    private float _waterConcernWeight;
    private Vector2 _waterConcernPoint,_waterConcernPlayer;

    private static bool AllowsWaterConcern(Creature c,string pose)=>
        c.Condition==PhysicalCondition.Active&&!c.Interaction.Carried&&!c.Interaction.Falling&&
        !c.Interaction.Rolling&&!c.Interaction.Supine&&!c.Interaction.Resting&&
        c.Interaction.Restraint==RestraintKind.None&&c.Interaction.Height<=.001&&
        c.Interaction.EatingRemaining<=0&&!c.Interaction.Physiology.Active&&
        !c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&!FamilyRules.UnderIntervention(c)&&
        !HasWaterBodyPresentation(c)&&c.Pain<40&&c.Interaction.HeatTension<=20&&
        pose is "idle" or "guard" or "careful-step" or "help-look" or "water-help";

    // The room supplies a live simulation-qualified concern, never merely a saved ID.
    public void PrepareWaterConcern(Creature c,Creature? target,Vector2 player,string pose)
    {
        _waterConcernActive=target!=null&&AllowsWaterConcern(c,pose);
        if(!_waterConcernActive)return;
        _waterConcernPoint=new((float)target!.X,(float)target.Z);
        _waterConcernPlayer=player;
        _waterConcernKind=c.Personality.Kindness>=.5;
        // An unkind trait alone does not mean an actual taunt is happening now.
        _waterConcernMock=c.Personality.Kindness<.25&&c.Expression==Expression.Defiant&&c.ReactionRemaining>0;
    }

    private Expression WaterConcernExpression(Expression expression)
    {
        if(!_waterConcernActive)return expression;
        if(_waterConcernKind&&expression is Expression.Neutral or Expression.Curious or Expression.Content)
            return Expression.Worried;
        return expression;
    }

    private void WaterConcernEyes(Expression expression,ref float eyes,ref float left,ref float right)
    {
        if(!_waterConcernActive)return;
        if(_waterConcernKind&&expression is Expression.Calling or Expression.Worried)
        {
            eyes=Mathf.Max(eyes,.9f);
            left=18+Mathf.Sin(_phase*3.1f)*2;right=-left;
        }
        else if(_waterConcernMock&&expression==Expression.Defiant)
        {eyes=.68f;left=-9;right=21;}
    }

    private void ApplyWaterConcernBody(Creature c,string pose,float dt)
    {
        _waterConcernFacing=false;
        if(!AllowsWaterConcern(c,pose)){_waterConcernWeight=0;return;}
        float target=_waterConcernActive?1:0;
        if(dt>0)_waterConcernWeight=Mathf.Lerp(_waterConcernWeight,target,1-Mathf.Exp(-dt*7));
        if(_waterConcernWeight<.005f)return;
        float weight=_waterConcernWeight;
        // While moving, preserve the authored launch/flight heading. Once planted,
        // the whole head turns between the endangered oil and the human for help.
        if(!_hopMotion.Active&&!_hopMotion.Turning)
        {
            float lookToHuman=_waterConcernKind?Mathf.Clamp((Mathf.Sin(_phase*1.8f)-.4f)*2,0,1):0;
            Vector2 attention=_waterConcernPoint.Lerp(_waterConcernPlayer,lookToHuman);
            var direction=attention-new Vector2((float)c.X,(float)c.Z);
            if(direction.LengthSquared()>.001f)
            {
                var rotation=_visual.Rotation;
                rotation.Y=Mathf.LerpAngle(rotation.Y,Mathf.Atan2(direction.X,direction.Y),weight);
                _visual.Rotation=rotation;_waterConcernFacing=true;
            }
        }
        if(_waterConcernKind)
        {
            float reach=Mathf.Max(0,Mathf.Sin(_phase*4.6f));
            _visual.Scale*=new Vector3(1-reach*.012f*weight,1+reach*.03f*weight,1);
            _visual.RotationDegrees+=new Vector3(-reach*3*weight,0,Mathf.Sin(_phase*3.1f)*1.2f*weight);
            PoseTag="water-concern-help";
        }
        else if(_waterConcernMock)
        {
            _visual.RotationDegrees+=new Vector3(-3*weight,0,Mathf.Sin(_phase*2.4f)*2.8f*weight);
            _visual.Scale*=new Vector3(1,1+Mathf.Abs(Mathf.Sin(_phase*4))*.012f*weight,1);
            PoseTag="water-concern-mock";
        }
        else PoseTag="water-concern-watch";
    }
}

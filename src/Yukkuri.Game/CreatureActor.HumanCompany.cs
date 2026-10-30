using Godot;
using Yukkuri.Simulation;
using Expression=Yukkuri.Simulation.Expression;

public partial class CreatureActor
{
    private bool _companyActive,_companyComfort,_companyArrived,_companyFacing;
    private float _companyWeight,_companyCalm,_companyClock;
    private double _companyEpisode=-1;
    private Vector2 _companyPoint;

    private bool AllowsCompanyBody(Creature c,string pose)=>
        c.Condition==PhysicalCondition.Active&&!c.Interaction.Carried&&!c.Interaction.Falling&&
        !c.Interaction.Rolling&&!c.Interaction.Supine&&!c.Interaction.Resting&&
        c.Interaction.Restraint==RestraintKind.None&&c.Interaction.Height<=.001&&
        c.Interaction.EatingRemaining<=0&&!c.Interaction.Physiology.Active&&
        !c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&!FamilyRules.UnderIntervention(c)&&
        !HasWaterBodyPresentation(c)&&c.Pain<25&&c.Fear<60&&c.Interaction.HeatTension<=20&&
        c.RetreatRemaining<=0&&c.Family.FlightRemaining<=0&&
        !_waterConcernActive&&_waterConcernWeight<=.01f&&
        pose is "idle" or "guard" or "careful-step" or "invite";

    public void PrepareHumanCompany(Creature c,bool live,Vector2 player,string pose,bool paused,bool close)
    {
        if(close||!AllowsCompanyBody(c,pose))
        {_companyActive=false;_companyWeight=0;return;}
        // A pause preserves the existing view, but cannot invent a new meeting
        // from a saved Until timestamp after the real hand has gone away.
        if(paused)return;
        _companyActive=live;
        if(!live)return;
        var company=c.Interaction.HumanCompany;
        if(_companyEpisode!=company.StartedAt)
        {_companyEpisode=company.StartedAt;_companyClock=0;}
        _companyPoint=player;
        _companyArrived=company.ContactSeconds>0&&player.DistanceSquaredTo(new((float)c.X,(float)c.Z))<=1.15f*1.15f;
        _companyCalm=Mathf.Clamp((float)company.ContactSeconds/6,0,1);
        _companyComfort=company.SeekingComfort||c.Family.GriefRemaining>0||!EmotionalReadiness.Settled(c);
    }

    private Expression CompanyExpression(Expression expression)
    {
        if(!_companyActive)return expression;
        if(expression is not (Expression.Neutral or Expression.Curious or Expression.Content or Expression.Delighted or Expression.Worried))return expression;
        if(_companyComfort)return Expression.Worried;
        return _companyArrived&&_companyCalm>.15f?Expression.Content:expression;
    }

    private void CompanyEyes(Expression expression,ref float eyes,ref EyeCurve curve,ref float left,ref float right)
    {
        if(!_companyActive)return;
        if(_companyComfort&&expression==Expression.Worried)
        {
            // Seeking familiar company is compatible with grief and caution.
            eyes=Mathf.Lerp(.92f,.72f,_companyCalm);left=15;right=-15;
        }
        else if(_companyArrived&&expression==Expression.Content)
        {
            eyes=Mathf.Lerp(.72f,.38f,_companyCalm);curve=EyeCurve.Relaxed;
            left=Mathf.Lerp(7,0,_companyCalm);right=-left;
        }
        else return;
        float phase=Mathf.PosMod(_companyClock,4.8f);
        float blink=phase>=3.8f&&phase<4.16f?Mathf.Sin((phase-3.8f)/.36f*Mathf.Pi):0;
        eyes=Mathf.Lerp(eyes,.07f,blink*.9f);
    }

    private void ApplyHumanCompanyBody(Creature c,string pose,float dt)
    {
        _companyFacing=false;
        if(!AllowsCompanyBody(c,pose)){_companyWeight=0;return;}
        if(dt>0)
        {
            _companyWeight=Mathf.Lerp(_companyWeight,_companyActive?1:0,1-Mathf.Exp(-dt*5));
            if(_companyWeight>.005f)_companyClock+=dt;
        }
        if(_companyWeight<.005f)return;
        float weight=_companyWeight;
        if(!_hopMotion.Active&&!_hopMotion.Turning)
        {
            var toward=_companyPoint-new Vector2((float)c.X,(float)c.Z);
            if(toward.LengthSquared()>.001f)
            {
                var rotation=_visual.Rotation;
                rotation.Y=Mathf.LerpAngle(rotation.Y,Mathf.Atan2(toward.X,toward.Y),weight);
                _visual.Rotation=rotation;_companyFacing=true;
            }
            if(_companyArrived)
            {
                float breath=Mathf.Sin(_companyClock*(_companyComfort?3.1f:2.3f));
                float soften=weight*_companyCalm;
                _visual.Scale*=new Vector3(1+soften*.012f,1-soften*.015f+breath*.006f*weight,1+soften*.006f);
                _visual.RotationDegrees+=new Vector3((_companyComfort?-2:-.7f)*soften,0,
                    Mathf.Sin(_companyClock*1.7f)*(_companyComfort ? .45f : 1.1f)*soften);
            }
        }
        PoseTag=_companyComfort?"company-comfort":_companyArrived?"company-settled":"company-approach";
    }
}

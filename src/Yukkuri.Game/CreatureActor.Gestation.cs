using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Node3D? _gestationPlant;
    private readonly Node3D[] _gestationBuds=new Node3D[3];
    private bool _gestationReady;
    private float _gestationMass,_stemAmount,_lastStemProgress,_birthCueRemaining,_birthCueWindow,_newbornCueRemaining;
    private int _lastStemChildren=2;
    private BirthMethod _birthCueMethod;

    // Called only when the room discovers a real newly-created child with a birth event.
    public void BeginBirthPresentation(BirthMethod method)
    {_birthCueMethod=method;_birthCueRemaining=1.25f;_birthCueWindow=4;}
    public void BeginNewbornPresentation()=>_newbornCueRemaining=.8f;

    private void ApplyPregnancyBodyPose(Creature c,string pose,float dt)
    {
        var pregnancy=c.Family.Pregnancy;
        float progress=Mathf.Clamp((float)(pregnancy.Elapsed/FamilyRules.GestationSeconds(pregnancy.Method)),0,1);
        float target=pregnancy.Method==BirthMethod.Live&&pregnancy.Phase==PregnancyPhase.Gestating?
            .02f+progress*.09f:pregnancy.Method==BirthMethod.Live&&pregnancy.Phase==PregnancyPhase.Recovering?
            .025f*Mathf.Clamp((float)(pregnancy.RecoveryRemaining/FamilyRules.RecoverySeconds(BirthMethod.Live)),0,1):0;
        if(!_gestationReady){_gestationMass=target;_gestationReady=true;}
        else if(dt>0)_gestationMass=Mathf.Lerp(_gestationMass,target,1-Mathf.Exp(-dt*3));
        // A previous care/reaction pose may still own the body after a real birth.
        // Wait briefly for its safe idle posture, without replaying a stale cue later.
        _birthCueWindow=Mathf.Max(0,_birthCueWindow-dt);
        if(_birthCueWindow<=0)_birthCueRemaining=0;
        _newbornCueRemaining=Mathf.Max(0,_newbornCueRemaining-dt);
        // Birth or pregnancy never replaces the shape of a critical/dead or immersed body.
        if(c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical||HasWaterBodyPresentation(c))return;
        if(_gestationMass>.001f)
            _visual.Scale*=new Vector3(1+_gestationMass,1-_gestationMass*.25f,1+_gestationMass*.6f);
        bool settled=!c.Interaction.Carried&&!c.Interaction.Falling&&!c.Interaction.Rolling&&!c.Interaction.Supine&&
            c.Interaction.Restraint==RestraintKind.None&&pose is "idle" or "rest" or "guard" or "water-recover";
        if(settled&&pregnancy.Phase==PregnancyPhase.Gestating&&pregnancy.Method==BirthMethod.Live)
        {
            float burden=Mathf.Clamp((float)pregnancy.Burden/100,0,1);
            _visual.Scale*=new Vector3(1,1+Mathf.Sin(_phase*2.8f)*(.005f+burden*.01f),1);
            _visual.RotationDegrees+=new Vector3(-burden*3,0,0);
            if(pose=="idle")PoseTag="live-gestation";
        }
        if(settled&&_birthCueRemaining>0)
        {
            _birthCueRemaining=Mathf.Max(0,_birthCueRemaining-dt);
            float settle=_birthCueRemaining/1.25f;
            _visual.Scale*=new Vector3(1+settle*.025f,1-settle*.035f,1);
            PoseTag=_birthCueMethod==BirthMethod.Stem?"stem-delivery":"live-delivery";
        }
        if(settled&&_newbornCueRemaining>0)
        {
            float settle=_newbornCueRemaining/.8f;
            _visual.Scale*=new Vector3(1+settle*.025f,1-settle*.045f,1);
            PoseTag="newborn-settle";
        }
    }

    private void BuildPregnancyPlant()
    {
        // Root on the authored head surface; the stem bends out past headwear.
        _gestationPlant=new Node3D{Name="GestationPlant",Position=new(.32f,.27f,ActorFaceSurface.Height(.32f,.27f,.006f))};
        _visual.AddChild(_gestationPlant);
        SculptTube(_gestationPlant,u=>new(.70f*Mathf.Sin(u*Mathf.Pi*.5f),.53f*u,Mathf.Sin(u*Mathf.Pi)*.035f),.014f,"7e9b68",24);
        for(int n=0;n<_gestationBuds.Length;n++)
        {
            float side=n-1;
            var tip=new Vector3(.70f+side*.16f,.58f+(n==1?.10f:0),.035f+Mathf.Abs(side)*.04f);
            SculptTube(_gestationPlant,u=>new Vector3(.52f,.29f,.025f).Lerp(tip,u)+Vector3.Up*Mathf.Sin(u*Mathf.Pi)*.06f,.010f,"8ba676",18);
            var bud=new Node3D{Position=tip};_gestationPlant.AddChild(bud);_gestationBuds[n]=bud;
            SceneArt.Ball(bud,Vector3.Zero,new(.16f,.14f,.14f),"eddbb7");
            SceneArt.Ball(bud,new(0,.046f,-.012f),new(.15f,.065f,.12f),"a2b382");
            foreach(float eye in new[]{-.028f,.028f})
                SceneArt.Ball(bud,new(eye,.005f,.067f),new(.011f,.017f,.006f),"665244");
            SceneArt.Ball(bud,new(0,-.025f,.069f),new(.018f,.007f,.005f),"9b6953");
            float sign=n%2==0?-1:1;
            var leaf=SculptSurface(_gestationPlant,12,8,(u,v)=>
                new(.52f+sign*u*.15f,.29f+u*.055f+Mathf.Sin(u*Mathf.Pi)*.025f,
                    .025f+(v-.5f)*Mathf.Sin(u*Mathf.Pi)*.075f),"9bb67c",flipNormal:sign>0);
            leaf.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    private void PresentPregnancyPlant(Creature c,float dt)
    {
        var pregnancy=c.Family.Pregnancy;
        bool growing=c.Condition!=PhysicalCondition.Dead&&pregnancy.Phase==PregnancyPhase.Gestating&&pregnancy.Method==BirthMethod.Stem&&
            !c.Family.ColdStorage.Stored&&!c.Family.Memorialized;
        if(_gestationPlant==null)
        {
            if(!growing)return;
            BuildPregnancyPlant();_stemAmount=1;
        }
        if(growing)
        {
            _lastStemProgress=Mathf.Clamp((float)(pregnancy.Elapsed/FamilyRules.GestationSeconds(BirthMethod.Stem)),0,1);
            _lastStemChildren=System.Math.Clamp(pregnancy.PlannedChildren,1,3);
        }
        if(dt>0)_stemAmount=Mathf.Lerp(_stemAmount,growing?1:0,1-Mathf.Exp(-dt*4));
        _gestationPlant!.Visible=_stemAmount>.01f&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized;
        if(!_gestationPlant.Visible)return;
        var deformation=_visual.Scale/BodyScale;
        float growth=(.20f+_lastStemProgress*.80f)*_stemAmount;
        _gestationPlant.Scale=new(1/deformation.X,growth/deformation.Y,1/deformation.Z);
        _gestationPlant.RotationDegrees=new(0,0,c.Condition==PhysicalCondition.Active?Mathf.Sin(_phase*1.8f)*1.5f:0);
        for(int n=0;n<_gestationBuds.Length;n++)
        {
            float size=(.18f+.82f*Mathf.SmoothStep(.10f,1,_lastStemProgress))*_stemAmount;
            _gestationBuds[n].Visible=n<_lastStemChildren;
            _gestationBuds[n].Scale=new(size,size/Mathf.Max(.05f,growth),size);
        }
    }
}

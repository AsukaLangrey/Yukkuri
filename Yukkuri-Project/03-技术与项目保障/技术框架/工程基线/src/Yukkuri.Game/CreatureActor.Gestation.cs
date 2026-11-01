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
    private string? _stemContactEventId,_stemDisplayEventId;
    private float _stemIntegrity=1,_stemMoisture=1,_stemSupport,_stemStress;
    private bool _stemStressReady;
    private Node3D? _stemSupportBand;
    private readonly MeshInstance3D[] _stemBudEyes=new MeshInstance3D[6];
    private readonly MeshInstance3D[] _stemBudMouths=new MeshInstance3D[3];
    private readonly StandardMaterial3D[] _stemBudMaterials=new StandardMaterial3D[3];
    private readonly StandardMaterial3D[] _stemLeafMaterials=new StandardMaterial3D[3];

    // The room supplies the current simulation's whole-clutch status, never a text cue.
    public void PrepareStemPresentation(StemCareView? status)
    {
        string? eventId=status?.EventId;
        _stemContactEventId=eventId;
        if(_stemDisplayEventId!=eventId)
        {
            _stemDisplayEventId=eventId;_stemStressReady=false;
            _stemAmount=eventId==null?0:1;
        }
        _stemIntegrity=Mathf.Clamp((float)(status?.Integrity??0)/100,0,1);
        _stemMoisture=Mathf.Clamp((float)(status?.Hydration??0)/100,0,1);
        _stemSupport=Mathf.Clamp((float)(status?.SupportRemaining??0)/8,0,1);
        _lastStemProgress=Mathf.Clamp((float)(status?.Progress??0),0,1);
        _lastStemChildren=System.Math.Clamp(status?.PlannedChildren??0,0,3);
    }

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
            var skin=SceneArt.Ball(bud,Vector3.Zero,new(.16f,.14f,.14f),"eddbb7");
            _stemBudMaterials[n]=(StandardMaterial3D)skin.MaterialOverride.Duplicate();skin.MaterialOverride=_stemBudMaterials[n];
            SceneArt.Ball(bud,new(0,.046f,-.012f),new(.15f,.065f,.12f),"a2b382");
            for(int eye=0;eye<2;eye++)
                _stemBudEyes[n*2+eye]=SceneArt.Ball(bud,new(eye==0?-.028f:.028f,.005f,.067f),new(.011f,.017f,.006f),"665244");
            _stemBudMouths[n]=SceneArt.Ball(bud,new(0,-.025f,.069f),new(.018f,.007f,.005f),"9b6953");
            float sign=n%2==0?-1:1;
            var leaf=SculptSurface(_gestationPlant,12,8,(u,v)=>
                new(.52f+sign*u*.15f,.29f+u*.055f+Mathf.Sin(u*Mathf.Pi)*.025f,
                    .025f+(v-.5f)*Mathf.Sin(u*Mathf.Pi)*.075f),"9bb67c",flipNormal:sign>0);
            leaf.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
            _stemLeafMaterials[n]=(StandardMaterial3D)leaf.MaterialOverride.Duplicate();leaf.MaterialOverride=_stemLeafMaterials[n];
        }
        // A small visible binding indicates existing finite support; it creates no floor support.
        _stemSupportBand=new Node3D{Position=new(.34f,.17f,.02f),RotationDegrees=new(0,0,-45)};
        _gestationPlant.AddChild(_stemSupportBand);
        for(int n=0;n<3;n++)
            SceneArt.Mesh(_stemSupportBand,new TorusMesh{InnerRadius=.017f,OuterRadius=.023f,Rings=20,RingSegments=6},new(0,(n-1)*.015f,0),"d9d6b9");
    }

    private void PresentPregnancyPlant(Creature c,float dt)
    {
        var pregnancy=c.Family.Pregnancy;
        bool growing=_stemContactEventId!=null&&c.Condition!=PhysicalCondition.Dead&&pregnancy.Phase==PregnancyPhase.Gestating&&pregnancy.Method==BirthMethod.Stem&&
            !c.Family.ColdStorage.Stored&&!c.Family.Memorialized;
        if(!growing)
        {
            _stemContactEventId=null;_stemAmount=0;_stemStressReady=false;
            if(_gestationPlant!=null)_gestationPlant.Visible=false;
            return;
        }
        if(_gestationPlant==null)
        {
            if(!growing)return;
            BuildPregnancyPlant();_stemAmount=1;
        }
        if(dt>0)_stemAmount=Mathf.Lerp(_stemAmount,growing?1:0,1-Mathf.Exp(-dt*4));
        _gestationPlant!.Visible=_stemAmount>.01f&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized;
        if(!_gestationPlant.Visible)return;
        float stress=Mathf.Clamp((1-_stemIntegrity)*.65f+(1-_stemMoisture)*.55f,0,1);
        if(!_stemStressReady){_stemStress=stress;_stemStressReady=true;}
        else if(dt>0)_stemStress=Mathf.Lerp(_stemStress,stress,1-Mathf.Exp(-dt*3));
        var deformation=_visual.Scale/BodyScale;
        float growth=(.20f+_lastStemProgress*.80f)*_stemAmount;
        _gestationPlant.Scale=new(1/deformation.X,growth/deformation.Y,1/deformation.Z);
        float sway=c.Condition==PhysicalCondition.Active?Mathf.Sin(_phase*1.8f)*1.5f*(1-_stemSupport*.7f)*(1-_stemStress*.65f):0;
        _gestationPlant.RotationDegrees=new(0,0,sway-_stemStress*13*(1-_stemSupport*.45f));
        _stemSupportBand!.Visible=_stemSupport>0;
        for(int n=0;n<_gestationBuds.Length;n++)
        {
            float size=(.18f+.82f*Mathf.SmoothStep(.10f,1,_lastStemProgress))*_stemAmount;
            _gestationBuds[n].Visible=n<_lastStemChildren;
            _gestationBuds[n].Scale=new(size,size/Mathf.Max(.05f,growth),size);
            _gestationBuds[n].RotationDegrees=new(_stemStress*6,0,-_stemStress*8);
            _stemBudMaterials[n].AlbedoColor=new Color("eddbb7").Lerp(new Color("bdac89"),_stemStress*.65f);
            _stemLeafMaterials[n].AlbedoColor=new Color("9bb67c").Lerp(new Color("9a8055"),_stemStress);
            for(int eye=0;eye<2;eye++)
            {
                var lid=_stemBudEyes[n*2+eye];
                lid.Scale=new Vector3(.011f,.017f,.006f)*new Vector3(1,1-_stemStress*.6f,1);
                lid.RotationDegrees=new(0,0,(eye==0?-1:1)*_stemStress*12);
            }
            _stemBudMouths[n].Scale=new Vector3(.018f,.007f,.005f)*new Vector3(1-_stemStress*.25f,1+_stemStress*.8f,1);
        }
    }
}

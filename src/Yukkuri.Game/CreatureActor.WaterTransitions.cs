using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private enum WaterTransition { None, Enter, Leave, Rescued }
    private bool _waterObserved,_waterWasVisible,_waterWasInside,_waterActuallyInside;
    private string _waterObservedRegion="";
    private Creature? _waterObservedCreature;
    private Creature? _waterRescueNotifiedFor;
    private string _waterRescueNotifiedRegion="";
    private WaterTransition _waterTransition;
    private float _waterTransitionAge,_wetDripClock;
    private Vector3 _waterTransitionOrigin;
    private Node3D? _waterTransitionRoot,_wetDripRoot;
    private MeshInstance3D? _waterTransitionRing;
    private readonly MeshInstance3D[] _waterTransitionDrops=new MeshInstance3D[8];
    private readonly MeshInstance3D[] _wetDrips=new MeshInstance3D[5];
    private readonly Vector3[] _wetDripOrigins=new Vector3[5];
    private readonly int[] _wetDripCycles={-1,-1,-1,-1,-1};

    // Called only after a current player command actually moved the target to the bank.
    // Historical LastRescueAt is deliberately not an animation trigger.
    public void NotifyActualWaterRescue(Creature c)
    {
        if(!Visible||c.Condition==PhysicalCondition.Dead)return;
        _waterRescueNotifiedFor=c;_waterRescueNotifiedRegion=c.RegionId;
    }

    public void ObserveWaterState(Creature c,bool inside,bool paused)
    {
        _waterActuallyInside=inside;
        bool baseline=!_waterObserved||!ReferenceEquals(_waterObservedCreature,c)||
            _waterObservedRegion!=c.RegionId||!_waterWasVisible;
        bool available=Visible&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized;
        if(!available)
        {
            _waterTransition=WaterTransition.None;
            ResetWetDripCycles();
        }
        else if(!baseline&&!paused&&inside!=_waterWasInside)
        {
            // Death in the river is not an exit, rescue, or renewed active struggle.
            if(c.Condition!=PhysicalCondition.Dead)
                StartWaterTransition(c,inside?WaterTransition.Enter:WaterTransition.Leave);
        }
        if(baseline)
        {
            _waterTransition=WaterTransition.None;
            ResetWetDripCycles();
        }
        // A just-successful local rescue remains valid even before first observation;
        // it cannot survive a replacement model, a hidden target, or a new immersion.
        bool pendingRescue=ReferenceEquals(_waterRescueNotifiedFor,c)&&_waterRescueNotifiedRegion==c.RegionId&&
            available&&!inside&&c.Condition!=PhysicalCondition.Dead;
        if(pendingRescue&&!paused)StartWaterTransition(c,WaterTransition.Rescued);
        if(!pendingRescue||!paused)_waterRescueNotifiedFor=null;
        _waterObserved=true;_waterObservedCreature=c;_waterObservedRegion=c.RegionId;
        _waterWasVisible=available;_waterWasInside=inside;
    }

    private void StartWaterTransition(Creature c,WaterTransition transition)
    {
        _waterTransition=transition;_waterTransitionAge=0;
        _waterTransitionOrigin=new((float)c.X,
            transition==WaterTransition.Enter?NaturalIntakeGeometry.WaterHeight+.012f:
            transition==WaterTransition.Leave?(float)c.Interaction.Height+.08f*BodyScale:.025f,(float)c.Z);
    }

    private void ResetWetDripCycles()
    {
        for(int n=0;n<_wetDripCycles.Length;n++)_wetDripCycles[n]=-1;
    }

    private void BuildWaterTransitionEffects()
    {
        _waterTransitionRoot=new Node3D();AddChild(_waterTransitionRoot);
        _waterTransitionRing=SceneArt.Mesh(_waterTransitionRoot,
            new TorusMesh{InnerRadius=.48f,OuterRadius=.50f,Rings=40,RingSegments=6},Vector3.Zero,"c2dfe0");
        SceneArt.EnableSurfaceFade(_waterTransitionRing);
        for(int n=0;n<_waterTransitionDrops.Length;n++)
        {
            var drop=SceneArt.Ball(_waterTransitionRoot,Vector3.Zero,new(.025f,.045f,.025f),"b8d9dc");
            SceneArt.EnableSurfaceFade(drop);_waterTransitionDrops[n]=drop;
        }
        _wetDripRoot=new Node3D();AddChild(_wetDripRoot);
        using var dripSurface=new SurfaceTool();dripSurface.Begin(Mesh.PrimitiveType.Triangles);
        void AddDripVertex(WaterDropletVertex vertex)
        {
            dripSurface.SetNormal(new(vertex.Normal.X,vertex.Normal.Y,vertex.Normal.Z));
            dripSurface.AddVertex(new(vertex.Position.X,vertex.Position.Y,vertex.Position.Z));
        }
        foreach(var face in ActorWaterDropletSurface.Triangles())
        {AddDripVertex(face.A);AddDripVertex(face.B);AddDripVertex(face.C);}
        dripSurface.Index();var dripMesh=dripSurface.Commit();
        for(int n=0;n<_wetDrips.Length;n++)
        {
            var drop=SceneArt.Mesh(_wetDripRoot,dripMesh,Vector3.Zero,"badde0");
            drop.Scale=new(.018f,.035f,.018f);
            SceneArt.EnableSurfaceFade(drop);_wetDrips[n]=drop;
        }
    }

    private void PresentWaterTransitions(Creature c,float dt)
    {
        bool available=Visible&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized;
        if(_waterTransitionRoot==null)
        {
            bool wetSource=c.Interaction.WetnessAt(BodyPart.Head)>25||
                c.Interaction.WetnessAt(BodyPart.Cheek,SurfaceSide.Left)>25||
                c.Interaction.WetnessAt(BodyPart.Cheek,SurfaceSide.Right)>25||
                c.Interaction.WetnessAt(BodyPart.Side,SurfaceSide.Left)>25||
                c.Interaction.WetnessAt(BodyPart.Side,SurfaceSide.Right)>25;
            if(!available||_waterTransition==WaterTransition.None&&!wetSource)return;
            BuildWaterTransitionEffects();
        }
        _waterTransitionRoot!.Visible=available&&_waterTransition!=WaterTransition.None;
        _wetDripRoot!.Visible=available&&!_waterActuallyInside;
        if(!available)return;
        // This clock animates droplets only. It never advances exposure, recovery, or world time.
        _wetDripClock+=dt;
        if(_waterTransition!=WaterTransition.None)
        {
            _waterTransitionAge+=dt;
            float duration=_waterTransition==WaterTransition.Rescued?1.1f:.65f;
            float t=Mathf.Clamp(_waterTransitionAge/duration,0,1);
            _waterTransitionRoot.GlobalPosition=_waterTransitionOrigin;
            bool entry=_waterTransition==WaterTransition.Enter;
            bool rescued=_waterTransition==WaterTransition.Rescued;
            _waterTransitionRing!.Visible=entry||rescued;
            _waterTransitionRing.Scale=new Vector3(.65f+t*1.25f,1,.65f+t*1.25f)*BodyScale;
            SceneArt.SetSurfaceOpacity(_waterTransitionRing,(1-t)*(rescued?.32f:.55f));
            for(int n=0;n<_waterTransitionDrops.Length;n++)
            {
                float angle=n*Mathf.Tau/_waterTransitionDrops.Length;
                float distance=(.28f+t*(entry?.65f:.34f))*BodyScale;
                float height=entry?Mathf.Sin(t*Mathf.Pi)*(.18f+n%3*.045f):
                    Mathf.Max(0,(.28f+n%3*.06f)*(1-t)-t*t*.15f);
                _waterTransitionDrops[n].Position=new(Mathf.Cos(angle)*distance,height*BodyScale,Mathf.Sin(angle)*distance);
                _waterTransitionDrops[n].Scale=new Vector3(.025f,.045f,.025f)*BodyScale;
                SceneArt.SetSurfaceOpacity(_waterTransitionDrops[n],(1-t)*.7f);
            }
            if(t>=1){_waterTransition=WaterTransition.None;_waterTransitionRoot.Visible=false;}
        }
        if(_waterActuallyInside){ResetWetDripCycles();return;}
        for(int n=0;n<_wetDrips.Length;n++)
        {
            BodyPart part=n==0?BodyPart.Head:n<3?BodyPart.Cheek:BodyPart.Side;
            float side=n==1||n==3?-1:1;
            SurfaceSide surface=n==0?SurfaceSide.Whole:side<0?SurfaceSide.Left:SurfaceSide.Right;
            float wet=Mathf.Clamp(((float)c.Interaction.WetnessAt(part,surface)-25)/75,0,1);
            if(wet<=.02f){_wetDrips[n].Visible=false;_wetDripCycles[n]=-1;continue;}
            float cyclePosition=_wetDripClock*.9f+n*.37f;
            int cycle=(int)Mathf.Floor(cyclePosition);
            float phase=Mathf.PosMod(cyclePosition,1);
            if(_wetDripCycles[n]!=cycle)
            {
                _wetDripCycles[n]=cycle;_wetDripOrigins[n]=ContactPoint(part,side);
            }
            var drip=_wetDrips[n];
            drip.Visible=wet>.02f&&phase<wet*.72f;
            if(!drip.Visible)continue;
            float fall=phase/.72f;
            Vector3 point=_wetDripOrigins[n]+Vector3.Down*(fall*fall*.48f*BodyScale);
            drip.Visible=point.Y>.018f;
            drip.GlobalPosition=point;
            drip.Scale=new Vector3(.018f,.035f,.018f)*BodyScale*(.6f+wet*.4f);
            SceneArt.SetSurfaceOpacity(drip,(1-fall)*wet*.72f);
        }
    }
}

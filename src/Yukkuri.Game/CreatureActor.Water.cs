using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Node3D? _waterSurface;
    private readonly MeshInstance3D[] _waterRings=new MeshInstance3D[3];
    private readonly MeshInstance3D[] _waterDrops=new MeshInstance3D[6];
    private float _waterPhase;
    private static bool HasWaterBodyPresentation(Creature c)=>c.Interaction.Water.InWater&&
        !c.Interaction.Carried&&!c.Interaction.Falling&&c.Interaction.Height<=.1&&
        !c.Family.ColdStorage.Stored&&!c.Family.Memorialized;

    private void ApplyWaterBodyPose(Creature c,string pose,float dt)
    {
        var water=c.Interaction.Water;
        if(!HasWaterBodyPresentation(c))
        {
            if(water.RecoverySeconds>0&&!c.Interaction.Carried&&!c.Interaction.Falling&&
                !c.Family.ColdStorage.Stored&&c.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)&&
                pose is "idle" or "guard" or "water-recover")
            {
                float amount=Mathf.Clamp((float)water.RecoverySeconds/35,0,1);
                _visual.Position+=new Vector3(Mathf.Sin(_phase*23)*.009f*amount,0,0);
                _visual.Scale*=new Vector3(1,1-Mathf.Abs(Mathf.Sin(_phase*4))*.025f*amount,1);
                PoseTag="water-recover";
            }
            return;
        }
        _waterPhase+=dt;
        float effort=c.Condition==PhysicalCondition.Active?Mathf.Clamp((float)water.Struggle/100,0,1):0;
        float struggle=Mathf.Sin(_waterPhase*(5+effort*4));
        float reach=Mathf.Max(0,Mathf.Sin(_waterPhase*2.5f));
        _visual.Position+=new Vector3(struggle*.025f*effort,-.23f+(reach*.09f+Mathf.Abs(struggle)*.025f)*effort,0)*BodyScale;
        _visual.RotationDegrees+=new Vector3(-reach*12*effort,0,struggle*10*effort);
        _visual.Scale*=new Vector3(1-effort*reach*.04f,1+effort*reach*.065f,1);
        PoseTag=c.Condition==PhysicalCondition.Dead?"water-dead":effort>.12f?"water-struggle":"water-exhausted";
    }

    private void BuildWaterSurface()
    {
        _waterSurface=new Node3D();AddChild(_waterSurface);
        for(int n=0;n<_waterRings.Length;n++)
        {
            var ring=SceneArt.Mesh(_waterSurface,new TorusMesh{InnerRadius=.48f,OuterRadius=.50f,Rings=40,RingSegments=6},Vector3.Zero,"c2dfe0");
            SceneArt.EnableSurfaceFade(ring);_waterRings[n]=ring;
        }
        for(int n=0;n<_waterDrops.Length;n++)
        {
            var drop=SceneArt.Ball(_waterSurface,Vector3.Zero,new(.025f,.045f,.025f),"b8d9dc");
            SceneArt.EnableSurfaceFade(drop);_waterDrops[n]=drop;
        }
    }

    private void PresentWaterSurface(Creature c,float dt)
    {
        bool wet=HasWaterBodyPresentation(c);
        if(_waterSurface==null){if(!wet)return;BuildWaterSurface();}
        _waterSurface!.Visible=wet;
        if(!wet)return;
        _waterSurface.GlobalPosition=new((float)c.X,NaturalIntakeGeometry.WaterHeight+.008f,(float)c.Z);
        float effort=c.Condition==PhysicalCondition.Active?Mathf.Clamp((float)c.Interaction.Water.Struggle/100,0,1):0;
        for(int n=0;n<_waterRings.Length;n++)
        {
            float phase=Mathf.PosMod(_waterPhase*(.30f+effort*.24f)+n/3f,1);
            _waterRings[n].Scale=new Vector3(1+phase*.85f,1,1+phase*.75f)*BodyScale;
            SceneArt.SetSurfaceOpacity(_waterRings[n],(1-phase)*(.16f+effort*.42f));
        }
        for(int n=0;n<_waterDrops.Length;n++)
        {
            float phase=Mathf.PosMod(_waterPhase*(1.1f+effort)+n/6f,1);
            float angle=n*Mathf.Tau/6+_waterPhase*.3f;
            float distance=(.42f+phase*.22f)*BodyScale;
            _waterDrops[n].Position=new(Mathf.Cos(angle)*distance,Mathf.Sin(phase*Mathf.Pi)*.16f*effort,Mathf.Sin(angle)*distance);
            _waterDrops[n].Visible=effort>.12f;
            SceneArt.SetSurfaceOpacity(_waterDrops[n],(1-phase)*effort*.72f);
        }
    }
}

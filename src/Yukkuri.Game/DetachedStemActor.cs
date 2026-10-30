using Godot;
using System.Collections.Generic;
using Yukkuri.Simulation;

public partial class DetachedStemActor : Node3D
{
    public const float MouthHeight=.60f,MouthRadius=.14f;
    private Node3D _vessel=null!,_plant=null!,_residue=null!;
    private MeshInstance3D _water=null!,_selection=null!;
    private readonly MeshInstance3D[] _buds=new MeshInstance3D[3],_nutrients=new MeshInstance3D[3],_cracks=new MeshInstance3D[3];
    private readonly MeshInstance3D[] _eyes=new MeshInstance3D[6];
    private readonly MeshInstance3D[] _mouths=new MeshInstance3D[3],_branches=new MeshInstance3D[3];
    private readonly List<MeshInstance3D> _contacts=new();
    private readonly Dictionary<Mesh,TriangleMesh> _trees=new();
    private DetachedStemView? _state;

    public void Build()
    {
        _vessel=new Node3D();AddChild(_vessel);
        var body=SceneArt.Mesh(_vessel,new CylinderMesh{TopRadius=.30f,BottomRadius=.30f,Height=.35f,RadialSegments=32,CapTop=false},new(0,.20f,0),"a9c4c4");
        var shoulder=SceneArt.Mesh(_vessel,new CylinderMesh{TopRadius=.16f,BottomRadius=.30f,Height=.125f,RadialSegments=32,CapTop=false,CapBottom=false},new(0,.4375f,0),"b7d0cc");
        var neck=SceneArt.Mesh(_vessel,new CylinderMesh{TopRadius=.16f,BottomRadius=.16f,Height=.10f,RadialSegments=32,CapTop=false,CapBottom=false},new(0,.55f,0),"b7d0cc");
        foreach(var glass in new[]{body,shoulder,neck}){SceneArt.EnableSurfaceFade(glass);SceneArt.SetSurfaceOpacity(glass,.28f);}
        SceneArt.Mesh(_vessel,new TorusMesh{InnerRadius=MouthRadius,OuterRadius=.18f,Rings=32,RingSegments=8},new(0,MouthHeight,0),"b8d0c8");
        _water=SceneArt.Mesh(_vessel,new CylinderMesh{TopRadius=.275f,BottomRadius=.275f,Height=1,RadialSegments=32},Vector3.Zero,"91bfc5");
        SceneArt.EnableSurfaceFade(_water);SceneArt.SetSurfaceOpacity(_water,.55f);
        for(int n=0;n<3;n++)
        {
            _nutrients[n]=SceneArt.Ball(_vessel,new((n-1)*.10f,.06f,.05f),new(.08f,.035f,.07f),"bba776");
            _cracks[n]=SceneArt.Box(_vessel,new((n-1)*.085f,.22f+n*.045f,.292f),new(.009f,.12f,.006f),"687e79");
            _cracks[n].RotationDegrees=new(0,0,n%2==0?25:-35);
        }
        _plant=new Node3D();AddChild(_plant);
        SceneArt.Mesh(_plant,new CylinderMesh{TopRadius=.012f,BottomRadius=.018f,Height=.48f,RadialSegments=8},new(0,.24f,0),"91a774");
        for(int n=0;n<3;n++)
        {
            // The tapered round section stays inside the former .018-square
            // branch; its length, endpoints and actual contact mesh stay aligned.
            var branch=SceneArt.Mesh(_plant,new CylinderMesh
                {TopRadius=.005f,BottomRadius=.009f,Height=.22f,RadialSegments=10,CapTop=true,CapBottom=true},
                new((n-1)*.085f,.47f,0),"91a774");
            _branches[n]=branch;
            branch.RotationDegrees=new(0,0,(n-1)*-45);
            var bud=SceneArt.Ball(_plant,new((n-1)*.17f,.60f+(n==1?.04f:0),0),new(.15f,.13f,.14f),"ebdab6");
            bud.MaterialOverride=bud.MaterialOverride.Duplicate() as Material;_buds[n]=bud;
            for(int eye=0;eye<2;eye++)
                _eyes[n*2+eye]=SceneArt.Ball(_plant,bud.Position+new Vector3(eye==0?-.027f:.027f,.005f,.066f),new(.012f,.018f,.006f),"655347");
            _mouths[n]=SceneArt.Ball(_plant,bud.Position+new Vector3(0,-.025f,.068f),new(.022f,.008f,.005f),"a87564");
        }
        _residue=new Node3D();AddChild(_residue);
        for(int n=0;n<3;n++)
        {
            var twig=SceneArt.Box(_residue,new((n-1)*.035f,.008f,0),new(.014f,.012f,.18f),"a89a76");
            twig.RotationDegrees=new(0,(n-1)*28,0);
        }
        _selection=SceneArt.Mesh(this,new TorusMesh{InnerRadius=.35f,OuterRadius=(float)WorldSimulation.StemVesselRadius,Rings=40,RingSegments=6},new(0,.008f,0),"95b6a0");
        SceneArt.EnableSurfaceFade(_selection);
        void Collect(Node root)
        {
            foreach(var child in root.GetChildren())
            {if(child is MeshInstance3D mesh&&mesh!=_selection)_contacts.Add(mesh);Collect(child);}
        }
        Collect(this);SceneArt.SetRenderLayer(this,SceneArt.SceneryLayer);
    }

    public void Present(DetachedStemView state,bool selected)
    {
        _state=state;Position=new((float)state.X,0,(float)state.Z);
        bool bottled=state.Phase==DetachedStemPhase.Bottled||state.VesselIntegrity>0;
        bool alive=state.Phase is DetachedStemPhase.Loose or DetachedStemPhase.Bottled;
        _vessel.Visible=bottled&&state.Phase!=DetachedStemPhase.Born;
        _residue.Visible=state.Phase==DetachedStemPhase.Born;
        _plant.Visible=state.Phase!=DetachedStemPhase.Born;
        _plant.Position=new(0,bottled?.08f:.025f,0);
        _plant.RotationDegrees=new(0,0,bottled?0:-28);
        float progress=Mathf.Clamp((float)state.Progress,0,1);
        float stress=Mathf.Clamp((float)((100-state.Integrity)+(100-state.Hydration)*.5)/130,0,1);
        _plant.Scale=bottled?new(1,(.45f+progress*.55f)*(alive?1: .48f),1):new(.75f,.40f,.75f);
        float waterHeight=Mathf.Clamp((float)state.Hydration/100,0,1)*.30f;
        _water.Visible=waterHeight>.003f&&state.Phase!=DetachedStemPhase.Born;
        _water.Scale=new(1,Mathf.Max(.003f,waterHeight),1);_water.Position=new(0,.04f+waterHeight*.5f,0);
        for(int n=0;n<3;n++)
        {
            bool bud=n<state.PlannedChildren;
            _buds[n].Visible=_mouths[n].Visible=_branches[n].Visible=bud;
            if(_buds[n].MaterialOverride is StandardMaterial3D skin)skin.AlbedoColor=new Color("ebdab6").Lerp(new Color("a69b80"),alive?stress:1);
            for(int eye=0;eye<2;eye++)
            {_eyes[n*2+eye].Visible=bud;_eyes[n*2+eye].Scale=new(.012f,.018f*(alive?1-stress*.65f:.15f),.006f);}
            _nutrients[n].Visible=state.Nutrition>(n*30+5)&&state.Phase!=DetachedStemPhase.Born;
            _cracks[n].Visible=state.VesselIntegrity<75-n*20;
        }
        _selection.Visible=selected||state.HeldByHand;
        SceneArt.SetSurfaceOpacity(_selection,state.HeldByHand?.75f:.40f);
    }

    public CreatureActor.SurfaceContact? RayContact(Vector3 origin,Vector3 direction)
    {
        if(_state==null||!Visible)return null;
        CreatureActor.SurfaceContact? nearest=null;
        foreach(var mesh in _contacts)
        {
            if(!mesh.IsVisibleInTree()||mesh.Mesh==null)continue;
            var transform=mesh.GlobalTransform;var inverse=transform.AffineInverse();
            Vector3 localOrigin=inverse*origin,localDirection=(inverse.Basis*direction).Normalized();
            if(!mesh.Mesh.GetAabb().IntersectsSegment(localOrigin,localOrigin+localDirection*1000))continue;
            if(!_trees.TryGetValue(mesh.Mesh,out var tree))
            {tree=new TriangleMesh();if(!tree.CreateFromFaces(mesh.Mesh.GetFaces())){tree.Dispose();continue;}_trees[mesh.Mesh]=tree;}
            using var hit=tree.IntersectRay(localOrigin,localDirection);if(hit.Count==0)continue;
            Vector3 point=transform*hit["position"].AsVector3();float distance=origin.DistanceTo(point);
            if(nearest!=null&&distance>=nearest.Distance)continue;
            Vector3 normal=(inverse.Basis.Transposed()*hit["normal"].AsVector3()).Normalized();
            if(normal.Dot(direction)>0)normal=-normal;
            nearest=new(BodyPart.Side,point,normal,distance,false,RestraintKind.None,InteractionTargetKind.DetachedStem,_state.EventId);
        }
        // The open neck is a real aperture, not a clickable whole-bottle proxy.
        // A nearer rim, stem, other object, or scene obstacle still wins the ray.
        if(_state.Phase==DetachedStemPhase.Bottled&&_state.VesselIntegrity>25&&direction.Y<-.0001f)
        {
            float t=(GlobalPosition.Y+MouthHeight-origin.Y)/direction.Y;
            Vector3 point=origin+direction*t,local=ToLocal(point);
            if(t>0&&new Vector2(local.X,local.Z).Length()<MouthRadius&&
                (nearest==null||origin.DistanceTo(point)<nearest.Distance))
                nearest=new(BodyPart.Mouth,point,Vector3.Up,origin.DistanceTo(point),false,RestraintKind.None,InteractionTargetKind.DetachedStem,_state.EventId);
        }
        return nearest;
    }

    public override void _ExitTree(){foreach(var tree in _trees.Values)tree.Dispose();_trees.Clear();}
}

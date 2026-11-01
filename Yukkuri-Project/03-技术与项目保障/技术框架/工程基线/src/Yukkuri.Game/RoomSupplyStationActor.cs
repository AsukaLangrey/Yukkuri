using Godot;
using System.Collections.Generic;

// Only the fixed rear reservoir is drawn here. Its ordinary plate/cup is a
// separate RoomObject, with the existing intake, portion and pickup geometry.
public partial class RoomSupplyStationActor : Node3D
{
    public readonly record struct SurfaceHit(Vector3 Point,Vector3 Normal,float Distance);
    private readonly List<MeshInstance3D> _contacts=new();
    private readonly Dictionary<Mesh,TriangleMesh> _trees=new();
    private MeshInstance3D _contents=null!,_selection=null!;

    public void Build(bool water)
    {
        SceneArt.Mesh(this,new CylinderMesh{TopRadius=.38f,BottomRadius=.38f,Height=.045f,RadialSegments=24},new(0,.0225f,0),"818b80");
        // The open reservoir stays inside the shared .40 horizontal footprint.
        InteractionToolArt.Surface(this,4,32,(u,v)=>new Vector3(Mathf.Cos(v*Mathf.Tau)*.30f,.045f+u*.52f,Mathf.Sin(v*Mathf.Tau)*.30f),water?"a8bfc0":"c7b794",true);
        InteractionToolArt.Surface(this,4,32,(u,v)=>new Vector3(Mathf.Cos(v*Mathf.Tau)*.276f,.045f+u*.52f,Mathf.Sin(v*Mathf.Tau)*.276f),water?"c7d7d5":"dfd1b2");
        SceneArt.Mesh(this,new TorusMesh{InnerRadius=.276f,OuterRadius=.301f,Rings=32,RingSegments=8},new(0,.565f,0),water?"d1ddda":"e7d9bb");
        SceneArt.Mesh(this,new CylinderMesh{TopRadius=.276f,BottomRadius=.276f,Height=.012f,RadialSegments=32},new(0,.054f,0),water?"8faaa9":"a79575");
        // This short outlet ends inside the machine footprint. The real output
        // is placed farther forward by simulation; no decorative food is emitted.
        SceneArt.Box(this,new(0,.13f,.323f),new(.13f,.09f,.10f),"7c877e");
        SceneArt.Box(this,new(0,.13f,.376f),new(.085f,.045f,.004f),"394c47");
        _contents=SceneArt.Mesh(this,new CylinderMesh{TopRadius=.270f,BottomRadius=.270f,Height=1,RadialSegments=32},Vector3.Zero,water?"7db6c0":"d8be8d");
        if(!water)
            for(int n=0;n<3;n++)
                SceneArt.Ball(this,new((n-1)*.075f,.385f,.296f),new(.045f,.025f,.025f),"eee0b9");
        else
            SceneArt.Ball(this,new(0,.385f,.298f),new(.055f,.085f,.025f),"d8ece9");
        RememberContacts(this);
        _selection=SceneArt.Mesh(this,new TorusMesh{InnerRadius=.395f,OuterRadius=.411f,Rings=40,RingSegments=6},new(0,.014f,0),"ddc893");
        SceneArt.EnableSurfaceFade(_selection);SceneArt.SetSurfaceOpacity(_selection,.6f);
    }

    private void RememberContacts(Node node)
    {
        foreach(var child in node.GetChildren())
        {
            if(child is MeshInstance3D mesh)_contacts.Add(mesh);
            RememberContacts(child);
        }
    }

    public void Present(double x,double z,int stock,int capacity,bool selected,bool held)
    {
        Position=new((float)x,0,(float)z);
        float fraction=capacity>0?Mathf.Clamp((float)stock/capacity,0,1):0;
        float height=.46f*fraction;
        _contents.Visible=stock>0;
        _contents.Scale=new(1,Mathf.Max(.001f,height),1);
        _contents.Position=new(0,.062f+height*.5f,0);
        _selection.Visible=selected||held;
        // No bobbing or lifting offset: movement is the simulation's legal X/Z.
    }

    public SurfaceHit? RayContact(Vector3 origin,Vector3 direction)
    {
        if(!Visible)return null;
        SurfaceHit? nearest=null;
        foreach(var mesh in _contacts)
        {
            if(!mesh.IsVisibleInTree()||mesh.Mesh==null)continue;
            var transform=mesh.GlobalTransform;var inverse=transform.AffineInverse();
            Vector3 localOrigin=inverse*origin,localDirection=(inverse.Basis*direction).Normalized();
            if(!mesh.Mesh.GetAabb().IntersectsSegment(localOrigin,localOrigin+localDirection*1000))continue;
            if(!_trees.TryGetValue(mesh.Mesh,out var tree))
            {
                tree=new TriangleMesh();
                if(!tree.CreateFromFaces(mesh.Mesh.GetFaces())){tree.Dispose();continue;}
                _trees[mesh.Mesh]=tree;
            }
            using var result=tree.IntersectRay(localOrigin,localDirection);
            if(result.Count==0)continue;
            Vector3 point=transform*result["position"].AsVector3();float distance=origin.DistanceTo(point);
            if(nearest is { } prior&&distance>=prior.Distance)continue;
            Vector3 normal=(inverse.Basis.Transposed()*result["normal"].AsVector3()).Normalized();
            if(normal.Dot(direction)>0)normal=-normal;
            nearest=new(point,normal,distance);
        }
        return nearest;
    }

    public override void _ExitTree()
    {
        foreach(var tree in _trees.Values)tree.Dispose();
        _trees.Clear();_contacts.Clear();
    }
}

using Godot;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

// The same solid wall boxes are used by simulation. The roof stays visible and
// ray-selectable in close-up; observing an occupant never removes its house.
public partial class ShelterActor : Node3D
{
    public readonly record struct SurfaceHit(Vector3 Point,Vector3 Normal,float Distance);
    private readonly List<MeshInstance3D> _contacts=new(),_door=new(),_selection=new();
    private readonly Dictionary<Mesh,TriangleMesh> _trees=new();
    private static Vector3 V(System.Numerics.Vector3 value)=>new(value.X,value.Y,value.Z);

    public void Build()
    {
        var open=ShelterGeometry.WallBoxes(true);
        foreach(var box in open)SceneArt.Box(this,V(box.Center),V(box.Size),"b39d79");
        foreach(var box in ShelterGeometry.WallBoxes(false))
            if(!open.Contains(box))_door.Add(SceneArt.Box(this,V(box.Center),V(box.Size),"a8b29a"));
        SceneArt.Box(this,new(0,(float)ShelterGeometry.CeilingY+.04f,0),
            new((float)ShelterGeometry.OuterWidth,.08f,(float)ShelterGeometry.OuterDepth),"8f9e85");
        // A small visible pull remains on each front post when the curtain is
        // open. Both sit outside the clear door width, inside its control band.
        foreach(float side in new[]{-1f,1f})
        {
            var pull=SceneArt.Mesh(this,new TorusMesh{InnerRadius=.027f,OuterRadius=.040f,Rings=16,RingSegments=6},
                new(side*1.10f,1.05f,1.33f),"e1d1a3");
            pull.RotationDegrees=new(90,0,0);
        }
        // Timber edging and a shallow pitched roof stay outside the walking clearance.
        foreach(float side in new[]{-1f,1f})
        {
            SceneArt.Box(this,new(side*1.22f,1.18f,1.28f),new(.10f,2.38f,.14f),"816b50");
            for(int n=0;n<5;n++)SceneArt.Box(this,new(side*1.255f,.28f+n*.43f,0),new(.018f,.035f,2.5f),"9c8766");
            var roof=SceneArt.Box(this,new(side*.67f,2.58f,0),new(1.42f,.12f,2.95f),"7c9273");roof.RotationDegrees=new(0,0,side*-17);
            SceneArt.Box(this,new(side*.72f,.09f,1.41f),new(.40f,.13f,.23f),"a68a62");
            for(int n=0;n<3;n++)SceneArt.Ball(this,new(side*.72f+(n-1)*.1f,.21f,1.43f),new(.12f,.14f,.12f),n==1?"e3c875":"849b6c");
        }
        SceneArt.Box(this,new(0,2.79f,0),new(.14f,.12f,2.98f),"657b60");
        SceneArt.Box(this,new(0,2.22f,1.34f),new(.58f,.20f,.055f),"ddcca6");
        RememberContacts(this);
        float width=(float)ShelterGeometry.OuterWidth,depth=(float)ShelterGeometry.OuterDepth;
        foreach(float side in new[]{-1f,1f})
        {
            _selection.Add(SceneArt.Box(this,new(side*width*.5f,.009f,0),new(.018f,.008f,depth),"d8c38d"));
            _selection.Add(SceneArt.Box(this,new(0,.009f,side*depth*.5f),new(width,.008f,.018f),"d8c38d"));
        }
        foreach(var mesh in _selection)mesh.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
    }
    private void RememberContacts(Node node)
    {
        foreach(var child in node.GetChildren())
        {if(child is MeshInstance3D mesh)_contacts.Add(mesh);RememberContacts(child);}
    }
    public void Present(ShelterView state,bool selected)
    {
        Position=new((float)state.X,0,(float)state.Z);
        float scale=state.Capacity==5?(float)ShelterGeometry.SharedScale:1;Scale=new(scale,1,scale);
        foreach(var mesh in _door)
        {
            float remaining=Mathf.Max(.025f,1-(float)state.DoorProgress);
            mesh.Visible=true;mesh.Scale=new(1,remaining,1);mesh.Position=new(0,2.30f-1.15f*remaining,1.25f);
        }
        foreach(var mesh in _selection)mesh.Visible=selected;
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
        _trees.Clear();_contacts.Clear();_door.Clear();_selection.Clear();
    }
}

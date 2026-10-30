using Godot;
using System.Collections.Generic;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    public sealed record SurfaceContact(BodyPart Part,Vector3 Point,Vector3 Normal,float Distance,bool Blocked,RestraintKind Fixture,
        InteractionTargetKind TargetKind=InteractionTargetKind.Body,string? ObjectId=null);
    private MeshInstance3D? _skinContact;
    private Node3D? _hairContact;
    private readonly Dictionary<Mesh,TriangleMesh> _contactTrees=new();
    private readonly Dictionary<Node,List<MeshInstance3D>> _contactMeshes=new();
    public Vector3 ContactCoordinate(Vector3 worldPoint,BodyPart part)
    {
        if(part==BodyPart.Accessory&&_looseAccessory?.Visible==true)return _looseAccessory.ToLocal(worldPoint);
        return _visual.ToLocal(worldPoint);
    }

    private List<MeshInstance3D> ContactMeshes(Node root)
    {
        if(_contactMeshes.TryGetValue(root,out var known))return known;
        var found=new List<MeshInstance3D>();
        void Visit(Node node)
        {
            // Surface decals don't change anatomy, and the holder's hand is not part of its accessory.
            if(node==_accessoryGrip||node.Name=="DirtMarks"||node.Name=="WearMarks"||node.Name=="RepairStitches")return;
            if(node is MeshInstance3D mesh&&mesh.Mesh!=null)found.Add(mesh);
            foreach(var child in node.GetChildren())Visit(child);
        }
        Visit(root);_contactMeshes[root]=found;return found;
    }
    public SurfaceContact? RayContact(Vector3 origin,Vector3 direction)
    {
        SurfaceContact? nearest=null;
        void Test(MeshInstance3D mesh,BodyPart part,bool blocked=false,bool skin=false,RestraintKind fixture=RestraintKind.None,
            InteractionTargetKind targetKind=InteractionTargetKind.Body,string? objectId=null)
        {
            if(!mesh.IsVisibleInTree()||mesh.Mesh==null)return;
            Transform3D transform=mesh.GlobalTransform,inverse=transform.AffineInverse();
            Vector3 localOrigin=inverse*origin,localDirection=(inverse.Basis*direction).Normalized();
            if(!mesh.Mesh.GetAabb().IntersectsSegment(localOrigin,localOrigin+localDirection*1000))return;
            if(!_contactTrees.TryGetValue(mesh.Mesh,out var tree))
            {
                tree=new TriangleMesh();if(!tree.CreateFromFaces(mesh.Mesh.GetFaces())){tree.Dispose();return;}
                _contactTrees[mesh.Mesh]=tree;
            }
            using var result=tree.IntersectRay(localOrigin,localDirection);if(result.Count==0)return;
            Vector3 localPoint=result["position"].AsVector3();
            Vector3 point=transform*localPoint;float distance=origin.DistanceTo(point);
            if(nearest!=null&&distance>=nearest.Distance)return;
            Vector3 normal=(inverse.Basis.Transposed()*result["normal"].AsVector3()).Normalized();
            if(normal.Dot(direction)>0)normal=-normal;
            if(skin)
            {
                Vector3 p=_visual.ToLocal(point);
                var n=new Vector3(p.X/(ActorFaceSurface.RadiusX*ActorFaceSurface.RadiusX),
                    (p.Y-ActorFaceSurface.CenterY)/(ActorFaceSurface.RadiusY*ActorFaceSurface.RadiusY),
                    (p.Z-ActorFaceSurface.CenterZ)/(ActorFaceSurface.RadiusZ*ActorFaceSurface.RadiusZ)).Normalized();
                part=ActorSurfaceMarks.ClassifySkin(new(p.X,p.Y,p.Z));
                normal=(_visual.GlobalBasis.Inverse().Transposed()*n).Normalized();
            }
            nearest=new(part,point,normal,distance,blocked,fixture,targetKind,objectId);
        }
        void TestRoot(Node3D? root,BodyPart part,bool blocked=false,RestraintKind fixture=RestraintKind.None)
        {if(root?.IsVisibleInTree()!=true)return;foreach(var mesh in ContactMeshes(root))Test(mesh,part,blocked,fixture:fixture);}
        if(_skinContact!=null)Test(_skinContact,BodyPart.Side,skin:true);
        TestRoot(_hairContact,BodyPart.Head);
        TestRoot(_accessoryRoot,BodyPart.Accessory);TestRoot(_looseAccessory,BodyPart.Accessory);
        if(_stemContactEventId!=null&&_gestationPlant?.IsVisibleInTree()==true)
            foreach(var mesh in ContactMeshes(_gestationPlant))
                Test(mesh,BodyPart.Side,targetKind:InteractionTargetKind.Stem,objectId:_stemContactEventId);
        // A box wall, fixing frame, wrapping cloth or support hand in front of the body occludes it.
        TestRoot(_boxEquipment,BodyPart.Side,true,RestraintKind.Box);
        TestRoot(_frameEquipment,BodyPart.Side,true,RestraintKind.Frame);
        TestRoot(_wrapEquipment,BodyPart.Side,true,RestraintKind.Wrap);
        TestRoot(_hangEquipment,BodyPart.Side,true,RestraintKind.Hanging);
        TestRoot(_hangHarness,BodyPart.Side,true,RestraintKind.Hanging);
        TestRoot(_carrySupport,BodyPart.Side,true);
        return nearest;
    }

    public override void _ExitTree()
    {
        // These BVHs are private to this actor, never shared with a scene mesh.
        foreach(var tree in _contactTrees.Values)tree.Dispose();
        _contactTrees.Clear();_contactMeshes.Clear();
        // Drop managed references promptly; shared render meshes remain owned by
        // their nodes and must not be disposed while another instance uses them.
        _eyeSurfaceCache.Clear();_browCache.Clear();_mouthShapes.Clear();_supportMeshes.Clear();_supportVertices.Clear();
        _intakeMeshCache.Clear();
        _mouthWeights.Clear();_mouthFadeMeshes.Clear();_blushFadeMeshes.Clear();
    }
}

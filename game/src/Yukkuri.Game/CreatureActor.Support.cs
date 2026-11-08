using Godot;
using System.Collections.Generic;

public partial class CreatureActor
{
    private readonly List<MeshInstance3D> _supportMeshes=new();
    private readonly Dictionary<MeshInstance3D,(Mesh Mesh,System.Numerics.Vector3[] Vertices)> _supportVertices=new();
    private System.Numerics.Vector3[] SupportVertices(MeshInstance3D owner,Mesh mesh)
    {
        if(_supportVertices.TryGetValue(owner,out var cached)&&ReferenceEquals(cached.Mesh,mesh))return cached.Vertices;
        var unique=new HashSet<Vector3>(mesh.GetFaces());
        var vertices=new System.Numerics.Vector3[unique.Count];int index=0;
        foreach(var p in unique)vertices[index++]=new(p.X,p.Y,p.Z);
        // Animated eyes, brows and mouths replace their meshes. Keep only this
        // node's current surface, not every historical shape from their caches.
        _supportVertices[owner]=(mesh,vertices);return vertices;
    }
    private static void RestAccessoryOnGround(Node3D root)
    {
        float bottom=float.PositiveInfinity;
        void Visit(Node node)
        {
            if(node is MeshInstance3D mesh&&mesh.Mesh!=null&&mesh.IsVisibleInTree())
            {
                Aabb bounds=mesh.Mesh.GetAabb();
                for(int i=0;i<8;i++)bottom=Mathf.Min(bottom,(mesh.GlobalTransform*bounds.GetEndpoint(i)).Y);
            }
            foreach(var child in node.GetChildren())Visit(child);
        }
        Visit(root);
        if(float.IsFinite(bottom))root.GlobalPosition+=Vector3.Up*(.01f-bottom);
    }
    private void RememberSupportMeshes(Node root)
    {
        foreach(var child in root.GetChildren())
        {
            // A flexible reproductive stem must not become a rigid floor support.
            if(child==_gestationPlant)continue;
            if(child is MeshInstance3D m)_supportMeshes.Add(m);RememberSupportMeshes(child);
        }
    }
    private void KeepBodyAboveSupport(float floor)
    {
        if(_supportMeshes.Count==0)RememberSupportMeshes(_visual);
        float lowest=floor-Position.Y;
        Transform3D local=GlobalTransform.AffineInverse();
        for(int index=0;index<_supportMeshes.Count;index++)
        {
            var mesh=_supportMeshes[index];
            if(!GodotObject.IsInstanceValid(mesh))
            {_supportVertices.Remove(mesh);_supportMeshes.RemoveAt(index--);continue;}
            var geometry=mesh.Mesh;
            if(geometry==null||!GodotObject.IsInstanceValid(geometry))
            {_supportVertices.Remove(mesh);continue;}
            // Release an obsolete shape even if the new one is hidden or its
            // bounds need no detailed support sampling on this frame.
            if(_supportVertices.TryGetValue(mesh,out var cached)&&!ReferenceEquals(cached.Mesh,geometry))
                _supportVertices.Remove(mesh);
            if(!mesh.IsVisibleInTree())continue;
            Transform3D t=local*mesh.GlobalTransform;
            float y;
            if(geometry is SphereMesh sphere)
            {
                float rx=sphere.Radius,ry=sphere.Height*.5f;
                float extent=Mathf.Sqrt(Mathf.Pow(t.Basis.X.Y*rx,2)+Mathf.Pow(t.Basis.Y.Y*ry,2)+Mathf.Pow(t.Basis.Z.Y*rx,2));
                y=t.Origin.Y-extent;
            }
            else
            {
                Aabb bounds=geometry.GetAabb();y=float.PositiveInfinity;
                for(int n=0;n<8;n++)y=Mathf.Min(y,(t*bounds.GetEndpoint(n)).Y);
                if(y>=lowest)continue;
                var vertices=SupportVertices(mesh,geometry);
                if(vertices.Length>0)y=ActorMeshSupport.LowestY(vertices,new(t.Basis.X.Y,t.Basis.Y.Y,t.Basis.Z.Y),t.Origin.Y);
            }
            lowest=Mathf.Min(lowest,y);
        }
        if(!float.IsFinite(lowest))return;
        // Preserve an intended hop, but correct any mesh that would dip into the support.
        float gap=Position.Y+lowest-floor;
        if(gap<0)_visual.Position+=Vector3.Up*(-gap+.003f);
    }
    private void BuildSoftCradle(Node3D parent)
    {
        SculptSurface(parent,ActorCradleSurface.Rows,ActorCradleSurface.Sides,(u,v)=>AccessoryPoint(ActorCradleSurface.Point(u,v*Mathf.Tau)),"c3d1c1",flipNormal:true);
        SculptSurface(parent,ActorCradleSurface.Rows,ActorCradleSurface.Sides,(u,v)=>AccessoryPoint(ActorCradleSurface.Point(u,v*Mathf.Tau,true)),"e1e6d6");
        SculptTube(parent,u=>AccessoryPoint(ActorCradleSurface.Hem(u*Mathf.Tau)),ActorCradleSurface.HemRadius,"e8e9d5",ActorCradleSurface.Sides);
        // A narrow sewn line follows the same outer fabric instead of floating above it.
        SculptTube(parent,u=>AccessoryPoint(ActorCradleSurface.Point(.92f,u*Mathf.Tau))+new Vector3(Mathf.Cos(u*Mathf.Tau),0,Mathf.Sin(u*Mathf.Tau))*.003f,.0025f,"9aad98",64);
    }
}

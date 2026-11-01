using Godot;
using System.Collections.Generic;

public partial class CreatureActor
{
    private readonly List<MeshInstance3D> _supportMeshes=new();
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
        {if(child is MeshInstance3D m)_supportMeshes.Add(m);RememberSupportMeshes(child);}
    }
    private void KeepBodyAboveSupport(float floor)
    {
        if(_supportMeshes.Count==0)RememberSupportMeshes(_visual);
        float lowest=float.PositiveInfinity;
        Transform3D local=GlobalTransform.AffineInverse();
        foreach(var mesh in _supportMeshes)
        {
            if(!mesh.IsVisibleInTree()||mesh.Mesh==null)continue;
            Transform3D t=local*mesh.GlobalTransform;
            float y;
            if(mesh.Mesh is SphereMesh sphere)
            {
                float rx=sphere.Radius,ry=sphere.Height*.5f;
                float extent=Mathf.Sqrt(Mathf.Pow(t.Basis.X.Y*rx,2)+Mathf.Pow(t.Basis.Y.Y*ry,2)+Mathf.Pow(t.Basis.Z.Y*rx,2));
                y=t.Origin.Y-extent;
            }
            else
            {
                Aabb bounds=mesh.Mesh.GetAabb();y=float.PositiveInfinity;
                for(int n=0;n<8;n++)y=Mathf.Min(y,(t*bounds.GetEndpoint(n)).Y);
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
        const int sides=64,rows=20;
        Vector3 Point(float row,float angle)
        {
            float front=Mathf.Max(0,Mathf.Sin(angle));
            float top=.015f-.32f*Mathf.Pow(front,2);
            float y=Mathf.Lerp(-.505f,top,row);
            float radius=Mathf.Sqrt(Mathf.Max(.001f,1-Mathf.Pow(y/.515f,2)));
            return new(Mathf.Cos(angle)*.65f*radius,y,Mathf.Sin(angle)*.565f*radius-.025f);
        }
        SculptSurface(parent,rows,sides,(u,v)=>Point(u,v*Mathf.Tau),"d2ddd0",flipNormal:true);
        SculptTube(parent,u=>Point(1,u*Mathf.Tau),.018f,"e8e9d5",sides);
    }
}

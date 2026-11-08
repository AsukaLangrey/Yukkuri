using Godot;
using System;

public static class InteractionToolArt
{
    private static Vector3 V(System.Numerics.Vector3 p)=>new(p.X,p.Y,p.Z);
    internal static MeshInstance3D Surface(Node3D parent,int rows,int columns,Func<float,float,Vector3> point,string color,bool flip=false)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(float u,float v)
        {
            const float step=.0005f;
            float normalU=Mathf.Clamp(u,.001f,.999f);
            var du=point(normalU+step,v)-point(normalU-step,v);
            var dv=point(normalU,Mathf.Min(1,v+step))-point(normalU,Mathf.Max(0,v-step));
            var normal=dv.Cross(du).Normalized();surface.SetNormal(flip?-normal:normal);
            surface.AddVertex(point(u,v));
        }
        for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)
        {
            float a=r/(float)rows,b=(r+1)/(float)rows,x=c/(float)columns,y=(c+1)/(float)columns;
            if(flip){Vertex(a,x);Vertex(b,y);Vertex(b,x);Vertex(a,x);Vertex(a,y);Vertex(b,y);}
            else{Vertex(a,x);Vertex(b,x);Vertex(b,y);Vertex(a,x);Vertex(b,y);Vertex(a,y);}
        }
        surface.Index();return SceneArt.Mesh(parent,surface.Commit(),Vector3.Zero,color);
    }
    internal static void Tube(Node3D parent,Func<float,Vector3> path,float radius,string color,int steps=48)
    {
        Surface(parent,steps,8,(u,v)=>
        {
            var tangent=(path(Mathf.Min(1,u+.001f))-path(Mathf.Max(0,u-.001f))).Normalized();
            var axis=Mathf.Abs(tangent.Y)>.95f?Vector3.Right:Vector3.Up;
            var right=tangent.Cross(axis).Normalized();var up=tangent.Cross(right).Normalized();
            return path(u)+radius*(right*Mathf.Cos(v*Mathf.Tau)+up*Mathf.Sin(v*Mathf.Tau));
        },color);
    }
    public static void Towel(Node3D parent,bool dry)
    {
        Surface(parent,10,64,(u,v)=>V(InteractionToolSurface.Cloth(u,v*Mathf.Tau)),"eae5d4");
        Surface(parent,10,64,(u,v)=>V(InteractionToolSurface.Cloth(u,v*Mathf.Tau,true)),"d9d3c2",true);
        Tube(parent,v=>V(InteractionToolSurface.Cloth(1,v*Mathf.Tau))-Vector3.Up*.011f,.011f,"ded8c7",64);
        Tube(parent,v=>V(InteractionToolSurface.Cloth(.88f,v*Mathf.Tau))+Vector3.Up*.002f,.0025f,dry?"c0ac84":"91a9a2",64);
    }
    public static MeshInstance3D Cup(Node3D parent)
    {
        SceneArt.Mesh(parent,new CylinderMesh{TopRadius=InteractionToolSurface.CupRimRadius,BottomRadius=.08f,Height=InteractionToolSurface.CupTop*2,RadialSegments=48,CapTop=false,CapBottom=false},Vector3.Zero,"d9e1dc");
        SceneArt.Mesh(parent,new CylinderMesh{TopRadius=.104f,BottomRadius=.064f,Height=.132f,RadialSegments=48,CapTop=false,CapBottom=false,FlipFaces=true},new(0,.009f,0),"c5d0ca");
        SceneArt.Mesh(parent,new CylinderMesh{TopRadius=.08f,BottomRadius=.075f,Height=.018f,RadialSegments=48},new(0,-.066f,0),"d9e1dc");
        SceneArt.Mesh(parent,new TorusMesh{InnerRadius=.103f,OuterRadius=InteractionToolSurface.CupRimRadius,Rings=48,RingSegments=10},new(0,InteractionToolSurface.CupTop,0),"e8ece3");
        var water=SceneArt.Mesh(parent,new CylinderMesh{TopRadius=InteractionToolSurface.WaterRadius,BottomRadius=InteractionToolSurface.WaterRadius,Height=.002f,RadialSegments=48},new(0,InteractionToolSurface.WaterHeight,0),"9dbdc4");
        var waterMaterial=(StandardMaterial3D)water.MaterialOverride.Duplicate();waterMaterial.Roughness=.18f;water.MaterialOverride=waterMaterial;
        Tube(parent,u=>V(InteractionToolSurface.CupHandle(u)),.009f,"d9e1dc",40);
        return water;
    }
    public static void Cushion(Node3D parent)
    {
        Surface(parent,ActorCushionSurface.Rows,ActorCushionSurface.Sides,(u,v)=>V(ActorCushionSurface.Point(u,v)),"b8c4a8",true);
        Tube(parent,u=>V(ActorCushionSurface.Seam(u)),.007f,"e1dfc2",ActorCushionSurface.Sides);
    }
    public static void CarePad(Node3D parent)
    {
        Surface(parent,16,64,(u,v)=>V(InteractionCareSurface.Pad(u,v)),"e5dfcd");
        Surface(parent,InteractionCareSurface.GauzeRows,InteractionCareSurface.GauzeSides,(u,v)=>V(InteractionCareSurface.Pad(u,v,true)),"f0ebdb");
        Tube(parent,u=>V(InteractionCareSurface.PadSeam(u)),.0015f,"c9c2ad",64);
    }
    public static void RepairNeedle(Node3D parent)
    {
        var needle=Surface(parent,16,16,(u,v)=>V(InteractionCareSurface.Needle(u,v)),"b8c2bb");
        var metal=(StandardMaterial3D)needle.MaterialOverride.Duplicate();metal.Metallic=.65f;metal.Roughness=.3f;needle.MaterialOverride=metal;
        Tube(parent,u=>V(InteractionCareSurface.NeedleEye(u)),.0017f,"b8c2bb",48);
        Tube(parent,u=>V(InteractionCareSurface.Thread(u)),.0015f,"dfcda3",48);
    }
    public static void Treasure(Node3D parent)
    {
        var body=Surface(parent,24,64,(u,v)=>V(InteractionTreasureSurface.Point(u*Mathf.Pi,v*Mathf.Tau)),"7b9b92");
        var stone=(StandardMaterial3D)body.MaterialOverride.Duplicate();stone.Roughness=.35f;body.MaterialOverride=stone;
        Surface(parent,4,64,(u,v)=>V(InteractionTreasureSurface.Vein(u,v)),"c5c8ac");
    }
}

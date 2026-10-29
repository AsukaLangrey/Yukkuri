using Godot;

public static class InteractionFixtureArt
{
    public static MeshInstance3D RoundedBox(Node3D parent,Vector3 position,Vector3 size,string color,float bevel=.018f)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        int count=InteractionFixtureSurface.Segments;
        void Vertex(int face,float u,float v)
        {
            var p=InteractionFixtureSurface.Sample(face,u,v,new(size.X,size.Y,size.Z),bevel);
            surface.SetNormal(new(p.Normal.X,p.Normal.Y,p.Normal.Z));surface.AddVertex(new(p.Point.X,p.Point.Y,p.Point.Z));
        }
        for(int face=0;face<6;face++)for(int r=0;r<count;r++)for(int c=0;c<count;c++)
        {
            float a=r/(float)count,b=(r+1)/(float)count,x=c/(float)count,y=(c+1)/(float)count;
            Vertex(face,a,x);Vertex(face,b,x);Vertex(face,b,y);Vertex(face,a,x);Vertex(face,b,y);Vertex(face,a,y);
        }
        surface.Index();return SceneArt.Mesh(parent,surface.Commit(),position,color);
    }
    public static void BoxFastenings(Node3D parent,float front,float top,float bottom)
    {
        foreach(float x in new[]{-.85f,.85f})foreach(float y in new[]{top-.12f,bottom+.12f})
            SceneArt.Ball(parent,new(x,y,front+.05f),new(.030f,.030f,.012f),"6e685b");
    }
}

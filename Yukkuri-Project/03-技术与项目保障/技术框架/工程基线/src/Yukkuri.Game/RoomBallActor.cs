using Godot;
using Yukkuri.Simulation;

public partial class RoomBallActor : Node3D
{
    private Node3D _shell=null!;
    private readonly InteractionBallMotion _motion=new();
    public void Build()
    {
        _shell=new Node3D{Position=new(0,InteractionBallSurface.Radius,0)};AddChild(_shell);
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        for(int panel=0;panel<InteractionBallSurface.Panels;panel++)
        {
            var color=new Color(InteractionBallSurface.Colors[panel]);
            void Vertex(float u,float v)
            {
                var p=InteractionBallSurface.Point(u,(panel+v)*Mathf.Tau/InteractionBallSurface.Panels);
                var normal=System.Numerics.Vector3.Normalize(p);float shade=InteractionBallSurface.Shade(u,v);
                surface.SetColor(new(color.R*shade,color.G*shade,color.B*shade));surface.SetNormal(new(normal.X,normal.Y,normal.Z));surface.AddVertex(new(p.X,p.Y,p.Z));
            }
            for(int row=0;row<InteractionBallSurface.Rows;row++)for(int column=0;column<InteractionBallSurface.ColumnsPerPanel;column++)
            {
                float a=row/(float)InteractionBallSurface.Rows,b=(row+1)/(float)InteractionBallSurface.Rows,x=column/(float)InteractionBallSurface.ColumnsPerPanel,y=(column+1)/(float)InteractionBallSurface.ColumnsPerPanel;
                Vertex(a,x);Vertex(b,x);Vertex(b,y);Vertex(a,x);Vertex(b,y);Vertex(a,y);
            }
        }
        surface.Index();var mesh=SceneArt.Mesh(_shell,surface.Commit(),Vector3.Zero,"ffffff");
        var material=(StandardMaterial3D)SceneArt.Mat("ffffff").Duplicate();material.VertexColorUseAsAlbedo=true;material.Roughness=.82f;mesh.MaterialOverride=material;
    }
    public void Present(RoomObject ball,bool paused,float centerHeight)
    {
        _motion.Present(ball.X,ball.Z,paused,centerHeight<InteractionBallSurface.Radius?.2f:1);var q=_motion.Rotation;
        _shell.Position=new(0,centerHeight,0);
        _shell.Quaternion=new(q.X,q.Y,q.Z,q.W);
    }
}

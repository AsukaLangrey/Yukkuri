using Godot;
using System.Collections.Generic;

public partial class CreatureActor
{
    private MeshInstance3D BottomOval(Node3D parent,Vector2 diameter,string color,float offset=.008f)
    {
        return SculptSurface(parent,8,64,(u,v)=>
        {
            float x=Mathf.Cos(v*Mathf.Tau)*diameter.X*.5f*u;
            float z=ActorFaceSurface.CenterZ+Mathf.Sin(v*Mathf.Tau)*diameter.Y*.5f*u;
            return new(x,ActorFaceSurface.Bottom(x,z,offset),z);
        },color,flipNormal:true);
    }
    private readonly Dictionary<float,(ArrayMesh left,ArrayMesh right)> _eyeSurfaceCache=new();
    private float _eyeOpen=-1;
    private readonly Dictionary<(float,float,float),(ArrayMesh left,ArrayMesh right)> _browCache=new();

    private ArrayMesh FaceOvalMesh(Vector2 center,Vector2 diameter,float offset)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        const int segments=48,rings=5;
        void Vertex(float r,float angle)
        {
            var v=ActorFaceSurface.Point(center.X,center.Y,diameter.X,diameter.Y,r,angle,offset);
            var normal=ActorFaceSurface.Normal(v.X,v.Y);
            surface.SetNormal(new Vector3(normal.X,normal.Y,normal.Z));
            surface.AddVertex(new Vector3(v.X,v.Y,v.Z));
        }
        for(int ring=0;ring<rings;ring++)for(int n=0;n<segments;n++)
        {
            float r0=(float)ring/rings,r1=(float)(ring+1)/rings,a0=Mathf.Tau*n/segments,a1=Mathf.Tau*(n+1)/segments;
            Vertex(r0,a0);Vertex(r1,a1);Vertex(r1,a0);
            if(ring>0){Vertex(r0,a0);Vertex(r0,a1);Vertex(r1,a1);}
        }
        return surface.Commit();
    }
    private MeshInstance3D FaceOval(Node3D parent,Vector2 center,Vector2 diameter,string color,float offset=.009f)
    {
        var oval=SceneArt.Mesh(parent,FaceOvalMesh(center,diameter,offset),Vector3.Zero,color);
        oval.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;return oval;
    }
    private ArrayMesh FaceStrokeMesh(Vector2 start,Vector2 end,float width,float offset=.011f)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector2 normal=(end-start).Normalized().Orthogonal()*width*.5f;
        void Vertex(Vector2 p)
        {
            var n=ActorFaceSurface.Normal(p.X,p.Y);surface.SetNormal(new Vector3(n.X,n.Y,n.Z));
            surface.AddVertex(new Vector3(p.X,p.Y,ActorFaceSurface.Height(p.X,p.Y,offset)));
        }
        for(int n=0;n<12;n++)
        {
            Vector2 a=start.Lerp(end,n/12f),b=start.Lerp(end,(n+1)/12f);
            Vertex(a-normal);Vertex(b+normal);Vertex(b-normal);
            Vertex(a-normal);Vertex(a+normal);Vertex(b+normal);
        }
        return surface.Commit();
    }
    private MeshInstance3D FaceStroke(Node3D parent,Vector2 start,Vector2 end,float width,string color,float offset=.011f)
    {
        var stroke=SceneArt.Mesh(parent,FaceStrokeMesh(start,end,width,offset),Vector3.Zero,color);
        stroke.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;return stroke;
    }
    private void SetSurfaceBrows(float left,float right,float height)
    {
        var key=(left,right,height);
        if(!_browCache.TryGetValue(key,out var pair))
        {
            ArrayMesh Shape(float x,float angle)
            {
                Vector2 extent=new Vector2(.09f,0).Rotated(Mathf.DegToRad(angle)),center=new(x,.205f+height);
                return FaceStrokeMesh(center-extent,center+extent,.018f);
            }
            pair=(Shape(-.235f,left),Shape(.235f,right));_browCache[key]=pair;
        }
        _leftBrow.Mesh=pair.left;_rightBrow.Mesh=pair.right;
    }
    private void BuildSurfaceEyes()
    {
        _leftEye=FaceOval(_visual,new(-.23f,.04f),new(.16f,.24f),"352b2a");
        _rightEye=FaceOval(_visual,new(.23f,.04f),new(.16f,.24f),"352b2a");
        _eyeSurfaceCache[1]=((ArrayMesh)_leftEye.Mesh,(ArrayMesh)_rightEye.Mesh);_eyeOpen=1;
        foreach(var item in new[]{(_leftEye,-.23f),(_rightEye,.23f)})
        {
            FaceOval(item.Item1,new(item.Item2-.022f,.09f),new(.049f,.064f),"fff7e7",.016f);
            FaceOval(item.Item1,new(item.Item2+.02f,-.015f),new(.027f,.027f),"c9af91",.015f);
        }
    }
    private void SetSurfaceEyes(float openness)
    {
        if(Mathf.IsEqualApprox(_eyeOpen,openness))return;
        if(!_eyeSurfaceCache.TryGetValue(openness,out var pair))
        {
            pair=(FaceOvalMesh(new(-.23f,.04f),new(.16f,.24f*openness),.009f),FaceOvalMesh(new(.23f,.04f),new(.16f,.24f*openness),.009f));
            _eyeSurfaceCache[openness]=pair;
        }
        _leftEye.Mesh=pair.left;_rightEye.Mesh=pair.right;_eyeOpen=openness;
        foreach(var eye in new[]{_leftEye,_rightEye})foreach(var child in eye.GetChildren())
            if(child is Node3D feature)feature.Visible=openness>.7f;
    }
}

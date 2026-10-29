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
    private readonly Dictionary<(float,EyeCurve),(ArrayMesh left,ArrayMesh right)> _eyeSurfaceCache=new();
    private float _eyeOpen=-1;
    private EyeCurve _eyeCurve;
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
    private void FaceMouthCurve(Node3D parent,MouthCurve curve,string color)
    {
        var shape=SculptSurface(parent,40,4,(u,v)=>AccessoryPoint(ActorMouthSurface.Point(curve,u,v)),color,flipNormal:true);
        shape.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
    }
    private void SetSurfaceBrows(float left,float right,float height)
    {
        var key=(left,right,height);
        if(!_browCache.TryGetValue(key,out var pair))
        {
            pair=(FaceBrowMesh(-1,left,height),FaceBrowMesh(1,right,height));_browCache[key]=pair;
        }
        _leftBrow.Mesh=pair.left;_rightBrow.Mesh=pair.right;
    }
    private ArrayMesh FaceBrowMesh(float side,float angle,float height)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(float u,float v)
        {
            var p=ActorBrowSurface.Point(side,angle,height,u,v);surface.SetNormal(AccessoryPoint(ActorFaceSurface.Normal(p.X,p.Y)));surface.AddVertex(AccessoryPoint(p));
        }
        for(int row=0;row<ActorBrowSurface.Segments;row++)for(int column=0;column<ActorBrowSurface.Across;column++)
        {
            float a=row/(float)ActorBrowSurface.Segments,b=(row+1)/(float)ActorBrowSurface.Segments,x=column/(float)ActorBrowSurface.Across,y=(column+1)/(float)ActorBrowSurface.Across;
            Vertex(a,x);Vertex(b,y);Vertex(b,x);Vertex(a,x);Vertex(a,y);Vertex(b,y);
        }
        surface.Index();return surface.Commit();
    }
    private void BuildSurfaceEyes()
    {
        _leftEye=SceneArt.Mesh(_visual,EyeSurfaceMesh(-1,1,EyeCurve.Open),Vector3.Zero,"ffffff");
        _rightEye=SceneArt.Mesh(_visual,EyeSurfaceMesh(1,1,EyeCurve.Open),Vector3.Zero,"ffffff");
        var material=(StandardMaterial3D)SceneArt.Mat("ffffff").Duplicate();material.VertexColorUseAsAlbedo=true;material.Roughness=.38f;
        _leftEye.MaterialOverride=material;_rightEye.MaterialOverride=material;
        _leftEye.CastShadow=_rightEye.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        _eyeSurfaceCache[(1,EyeCurve.Open)]=((ArrayMesh)_leftEye.Mesh,(ArrayMesh)_rightEye.Mesh);_eyeOpen=1;
    }
    private ArrayMesh EyeSurfaceMesh(float side,float openness,EyeCurve curve)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach(EyeLayer layer in System.Enum.GetValues<EyeLayer>())
        {
            if(!ActorEyeSurface.Visible(layer,openness))continue;
            Color color=new(layer switch {EyeLayer.Iris=>_hatAccessory?"84633d":"70503e",EyeLayer.MainLight=>"fff7e7",EyeLayer.SmallLight=>"d8c4a4",_=>"352b2a"});
            void Vertex(float radius,float angle)
            {
                var p=ActorEyeSurface.Point(side,openness,curve,layer,radius,angle);var normal=ActorFaceSurface.Normal(p.X,p.Y);
                surface.SetNormal(AccessoryPoint(normal));surface.SetColor(color);surface.AddVertex(AccessoryPoint(p));
            }
            const int rings=5,segments=48;
            for(int ring=0;ring<rings;ring++)for(int n=0;n<segments;n++)
            {
                float a=ring/(float)rings,b=(ring+1)/(float)rings,x=n*Mathf.Tau/segments,y=(n+1)*Mathf.Tau/segments;
                Vertex(a,x);Vertex(b,y);Vertex(b,x);
                if(ring>0){Vertex(a,x);Vertex(a,y);Vertex(b,y);}
            }
        }
        surface.Index();return surface.Commit();
    }
    private void SetSurfaceEyes(float openness,EyeCurve curve=EyeCurve.Open)
    {
        if(Mathf.IsEqualApprox(_eyeOpen,openness)&&_eyeCurve==curve)return;
        var key=(openness,curve);
        if(!_eyeSurfaceCache.TryGetValue(key,out var pair))
        {
            pair=(EyeSurfaceMesh(-1,openness,curve),EyeSurfaceMesh(1,openness,curve));_eyeSurfaceCache[key]=pair;
        }
        _leftEye.Mesh=pair.left;_rightEye.Mesh=pair.right;_eyeOpen=openness;_eyeCurve=curve;
    }
}

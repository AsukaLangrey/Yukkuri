using System;
using System.Numerics;
using Yukkuri.Simulation;

public static class ActorCushionSurface
{
    public const int Rows=32,Sides=96;
    public const float RadiusX=InteractionFixtureGeometry.CushionWidth*.5f,RadiusZ=InteractionFixtureGeometry.CushionDepth*.5f,CenterY=.12f,HalfHeight=.11f;
    private const float VerticalPower=.7f,HorizontalPower=.55f;
    private static float SignedPow(float value,float power)=>MathF.CopySign(MathF.Pow(MathF.Abs(value),power),value);
    private static float Tuft(float x,float z)
    {
        float across=MathF.Exp(-(x-.32f)*(x-.32f)/.008f)+MathF.Exp(-(x+.32f)*(x+.32f)/.008f);
        float along=MathF.Exp(-(z-.23f)*(z-.23f)/.008f)+MathF.Exp(-(z+.23f)*(z+.23f)/.008f);
        return .010f*across*along;
    }
    public static float TopHeight(float x,float z)
    {
        float q=MathF.Pow(MathF.Abs(x)/RadiusX,2/HorizontalPower)+MathF.Pow(MathF.Abs(z)/RadiusZ,2/HorizontalPower);
        if(q>1.00001f)return 0;
        return CenterY+HalfHeight*MathF.Pow(MathF.Max(0,1-MathF.Pow(q,HorizontalPower/VerticalPower)),VerticalPower*.5f)-Tuft(x,z);
    }
    public static Vector3 Point(float u,float v)
    {
        if(u<=0)return new(0,CenterY-HalfHeight,0);
        if(u>=1)return new(0,TopHeight(0,0),0);
        float latitude=(u-.5f)*MathF.PI,angle=v*MathF.Tau;
        float radial=MathF.Pow(MathF.Max(0,MathF.Cos(latitude)),VerticalPower);
        float x=RadiusX*radial*SignedPow(MathF.Cos(angle),HorizontalPower),z=RadiusZ*radial*SignedPow(MathF.Sin(angle),HorizontalPower);
        float y=u>=.5f?TopHeight(x,z):CenterY+HalfHeight*SignedPow(MathF.Sin(latitude),VerticalPower);
        return new(x,y,z);
    }
    public static Vector3 Seam(float u)
    {
        var p=Point(.5f,u);return new(p.X*1.003f,CenterY,p.Z*1.003f);
    }
    private static readonly Vector3[] TopPoints=BuildTopPoints();
    private static Vector3[] BuildTopPoints()
    {
        // Uniform X/Z coverage avoids the wide gap beside a superellipsoid's top pole.
        // That gap can miss the contact beneath a tilted body even with many angular slices.
        const int across=48,along=36;
        var points=new Vector3[(across+1)*(along+1)];int n=0;
        for(int x=0;x<=across;x++)for(int z=0;z<=along;z++)
        {
            float px=RadiusX*(x*2f/across-1),pz=RadiusZ*(z*2f/along-1),y=TopHeight(px,pz);
            if(y>0)points[n++]=new(px,y,pz);
        }
        Array.Resize(ref points,n);
        return points;
    }
    public static float SkinLift(Vector3 cushionOrigin,Vector3 bodyOrigin,Vector3 scale,Quaternion rotation)
        =>EllipsoidLift(cushionOrigin,bodyOrigin,scale,rotation,new(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ),new(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ));
    public static float HairLift(Vector3 cushionOrigin,Vector3 bodyOrigin,Vector3 scale,Quaternion rotation)
    {
        float crown=EllipsoidLift(cushionOrigin,bodyOrigin,scale,rotation,ActorSurfaceMarks.HairCenter,ActorSurfaceMarks.HairRadii);
        var transform=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(bodyOrigin);
        return MathF.Max(crown,PointLift(ActorHairSurface.BackSupport,transform,cushionOrigin));
    }
    private static float EllipsoidLift(Vector3 cushionOrigin,Vector3 bodyOrigin,Vector3 scale,Quaternion rotation,Vector3 localCenter,Vector3 radii)
    {
        var inverse=Quaternion.Inverse(rotation);
        var center=bodyOrigin+Vector3.Transform(localCenter*scale,rotation);
        var scaledVertical=Vector3.Transform(Vector3.UnitY,inverse)*scale*radii;
        if(center.Y-scaledVertical.Length()>cushionOrigin.Y+CenterY+HalfHeight+.004f)return 0;
        var vertical=Vector3.Transform(Vector3.UnitY,inverse)/scale/radii;
        float a=vertical.LengthSquared(),lift=0;
        foreach(var sample in TopPoints)
        {
            var p=sample+cushionOrigin;
            var flat=Vector3.Transform(new Vector3(p.X-center.X,0,p.Z-center.Z),inverse)/scale/radii;
            float b=2*Vector3.Dot(vertical,flat),c=flat.LengthSquared()-1,discriminant=b*b-4*a*c;
            if(discriminant<0)continue;
            float bottom=center.Y+(-b-MathF.Sqrt(discriminant))/(2*a);
            lift=MathF.Max(lift,p.Y-bottom+.004f);
        }
        return lift;
    }
    public static float PointLift(ReadOnlySpan<Vector3> points,Matrix4x4 transform,Vector3 cushionOrigin,float margin=.007f)
    {
        float lift=0,ceiling=cushionOrigin.Y+CenterY+HalfHeight+margin;
        foreach(var local in points)
        {
            var p=Vector3.Transform(local,transform);if(p.Y>ceiling)continue;
            float top=TopHeight(p.X-cushionOrigin.X,p.Z-cushionOrigin.Z);
            if(top>0)lift=MathF.Max(lift,cushionOrigin.Y+top-p.Y+margin);
        }
        return lift;
    }
    public static float CradleLift(Vector3 cushionOrigin,Vector3 bodyOrigin,Vector3 scale,Quaternion rotation)
    {
        float lift=0;
        foreach(var local in ActorCradleSurface.Vertices)
        {
            var p=bodyOrigin+Vector3.Transform(local*scale,rotation);
            float top=TopHeight(p.X-cushionOrigin.X,p.Z-cushionOrigin.Z);
            if(top==0)continue;
            lift=MathF.Max(lift,cushionOrigin.Y+top-p.Y+ActorCradleSurface.HemRadius*MathF.Max(scale.X,MathF.Max(scale.Y,scale.Z))+.004f);
        }
        return lift;
    }
}

using System;
using System.Numerics;
using Yukkuri.Simulation;

public static class ActorCushionSurface
{
    public const int Rows=32,Sides=96;
    public const float RadiusX=CushionGeometry.RadiusX,RadiusZ=CushionGeometry.RadiusZ,CenterY=CushionGeometry.CenterY,HalfHeight=CushionGeometry.HalfHeight;
    private const float VerticalPower=CushionGeometry.VerticalPower,HorizontalPower=CushionGeometry.HorizontalPower;
    private static float SignedPow(float value,float power)=>MathF.CopySign(MathF.Pow(MathF.Abs(value),power),value);
    public static float TopHeight(float x,float z)=>CushionGeometry.TopHeight(x,z);
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
    private const int GridAcross=48,GridAlong=36;
    private static Vector3[] BuildTopPoints()
    {
        // Uniform X/Z coverage avoids the wide gap beside a superellipsoid's top pole.
        // That gap can miss the contact beneath a tilted body even with many angular slices.
        var points=new Vector3[(GridAcross+1)*(GridAlong+1)];int n=0;
        for(int x=0;x<=GridAcross;x++)for(int z=0;z<=GridAlong;z++)
        {
            float px=RadiusX*(x*2f/GridAcross-1),pz=RadiusZ*(z*2f/GridAlong-1),y=TopHeight(px,pz);
            points[n++]=new(px,y,pz);
        }
        return points;
    }
    public static float SkinLift(Vector3 cushionOrigin,Vector3 bodyOrigin,Vector3 scale,Quaternion rotation)
        =>EllipsoidLift(cushionOrigin,bodyOrigin,scale,rotation,new(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ),new(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ));
    public static float HairLift(Vector3 cushionOrigin,Vector3 bodyOrigin,Vector3 scale,Quaternion rotation)
    {
        var transform=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(bodyOrigin);
        return PointLift(ActorHairSurface.BackSupport,transform,cushionOrigin,.010f);
    }
    private static float EllipsoidLift(Vector3 cushionOrigin,Vector3 bodyOrigin,Vector3 scale,Quaternion rotation,Vector3 localCenter,Vector3 radii)
    {
        var inverse=Quaternion.Inverse(rotation);
        var center=bodyOrigin+Vector3.Transform(localCenter*scale,rotation);
        var scaledVertical=Vector3.Transform(Vector3.UnitY,inverse)*scale*radii;
        if(center.Y-scaledVertical.Length()>cushionOrigin.Y+CenterY+HalfHeight+.004f)return 0;
        var vertical=Vector3.Transform(Vector3.UnitY,inverse)/scale/radii;
        var across=Vector3.Transform(Vector3.UnitX,inverse)/scale/radii;
        var along=Vector3.Transform(Vector3.UnitZ,inverse)/scale/radii;
        float a=vertical.LengthSquared(),bx=2*Vector3.Dot(vertical,across),bz=2*Vector3.Dot(vertical,along);
        float xx=bx*bx-4*a*across.LengthSquared(),xz=2*bx*bz-8*a*Vector3.Dot(across,along),zz=bz*bz-4*a*along.LengthSquared();
        float extentX=(Vector3.Transform(Vector3.UnitX,inverse)*scale*radii).Length();
        float extentZ=(Vector3.Transform(Vector3.UnitZ,inverse)*scale*radii).Length();
        float localX=center.X-cushionOrigin.X,localZ=center.Z-cushionOrigin.Z;
        if(MathF.Abs(localX)>RadiusX+extentX||MathF.Abs(localZ)>RadiusZ+extentZ)return 0;
        int minX=Math.Clamp((int)MathF.Floor((localX-extentX+RadiusX)/(2*RadiusX)*GridAcross),0,GridAcross);
        int maxX=Math.Clamp((int)MathF.Ceiling((localX+extentX+RadiusX)/(2*RadiusX)*GridAcross),0,GridAcross);
        int minZ=Math.Clamp((int)MathF.Floor((localZ-extentZ+RadiusZ)/(2*RadiusZ)*GridAlong),0,GridAlong);
        int maxZ=Math.Clamp((int)MathF.Ceiling((localZ+extentZ+RadiusZ)/(2*RadiusZ)*GridAlong),0,GridAlong);
        float lift=0;
        for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
        {
            var sample=TopPoints[x*(GridAlong+1)+z];if(sample.Y<=0)continue;
            var p=sample+cushionOrigin;float dx=p.X-center.X,dz=p.Z-center.Z;
            float b=bx*dx+bz*dz,discriminant=4*a+xx*dx*dx+xz*dx*dz+zz*dz*dz;
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
        float lift=0,margin=ActorCradleSurface.HemRadius*MathF.Max(scale.X,MathF.Max(scale.Y,scale.Z))+.004f;
        float ceiling=cushionOrigin.Y+CenterY+HalfHeight+margin;
        foreach(var local in ActorCradleSurface.Vertices)
        {
            var p=bodyOrigin+Vector3.Transform(local*scale,rotation);
            if(p.Y>ceiling)continue;
            float top=TopHeight(p.X-cushionOrigin.X,p.Z-cushionOrigin.Z);
            if(top==0)continue;
            lift=MathF.Max(lift,cushionOrigin.Y+top-p.Y+margin);
        }
        return lift;
    }
}

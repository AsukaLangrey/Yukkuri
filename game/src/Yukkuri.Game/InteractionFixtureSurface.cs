using System;
using System.Numerics;

public static class InteractionFixtureSurface
{
    public const int Segments=7;
    private static float Axis(float t,float half,float radius)
    {
        float s=Math.Clamp(t,0,1)*Segments;
        return s<3?-half+radius*s/3:s>4?half-radius+radius*(s-4)/3:-half+radius+(s-3)*(2*half-2*radius);
    }
    public static (Vector3 Point,Vector3 Normal) Sample(int face,float u,float v,Vector3 size,float bevel)
    {
        var half=size*.5f;float r=Math.Clamp(bevel,.0001f,MathF.Min(half.X,MathF.Min(half.Y,half.Z))*.8f);
        Vector3 p=face switch
        {
            0=>new(half.X,Axis(v,half.Y,r),Axis(u,half.Z,r)),
            1=>new(-half.X,Axis(v,half.Y,r),-Axis(u,half.Z,r)),
            2=>new(Axis(u,half.X,r),half.Y,Axis(v,half.Z,r)),
            3=>new(Axis(u,half.X,r),-half.Y,-Axis(v,half.Z,r)),
            4=>new(Axis(v,half.X,r),Axis(u,half.Y,r),half.Z),
            _=>new(-Axis(v,half.X,r),Axis(u,half.Y,r),-half.Z)
        };
        var inner=half-new Vector3(r);var q=Vector3.Clamp(p,-inner,inner);var normal=Vector3.Normalize(p-q);
        return(q+normal*r,normal);
    }
}

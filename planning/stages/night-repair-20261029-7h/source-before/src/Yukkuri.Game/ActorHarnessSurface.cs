using System;
using System.Numerics;

public static class ActorHarnessSurface
{
    private const float Clearance=.023f;
    public static Vector3 Back(float x,float y)
    {
        float skin=1-x*x/(ActorFaceSurface.RadiusX*ActorFaceSurface.RadiusX)-MathF.Pow((y-ActorFaceSurface.CenterY)/ActorFaceSurface.RadiusY,2);
        float z=ActorFaceSurface.CenterZ-ActorFaceSurface.RadiusZ*MathF.Sqrt(MathF.Max(0,skin));
        var hc=ActorSurfaceMarks.HairCenter;var hr=ActorSurfaceMarks.HairRadii;
        float hair=1-MathF.Pow((x-hc.X)/hr.X,2)-MathF.Pow((y-hc.Y)/hr.Y,2);
        if(hair>=0)z=MathF.Min(z,hc.Z-hr.Z*MathF.Sqrt(hair));
        return ActorHairSurface.OutsideBack(new(x,y,z-Clearance),Clearance);
    }
    public static Vector3 Belt(float angle)
    {
        const float y=-.30f;
        float section=MathF.Sqrt(1-MathF.Pow((y-ActorFaceSurface.CenterY)/ActorFaceSurface.RadiusY,2));
        return ActorHairSurface.OutsideBack(new(MathF.Cos(angle)*(ActorFaceSurface.RadiusX*section+Clearance),y,
            ActorFaceSurface.CenterZ+MathF.Sin(angle)*(ActorFaceSurface.RadiusZ*section+Clearance)),Clearance);
    }
    public static Vector3 Strap(float side,float u)
    {
        float x=side*.28f,section=MathF.Sqrt(1-x*x/(ActorFaceSurface.RadiusX*ActorFaceSurface.RadiusX));
        float start=MathF.PI-MathF.Asin((.18f-ActorFaceSurface.CenterY)/(ActorFaceSurface.RadiusY*section));
        float end=MathF.Tau+MathF.Asin((-.30f-ActorFaceSurface.CenterY)/(ActorFaceSurface.RadiusY*section));
        float angle=start+(end-start)*u;
        var point=new Vector3(x,ActorFaceSurface.CenterY+MathF.Sin(angle)*(ActorFaceSurface.RadiusY*section+Clearance),
            ActorFaceSurface.CenterZ+MathF.Cos(angle)*(ActorFaceSurface.RadiusZ*section+Clearance));
        if(point.Z<ActorFaceSurface.CenterZ)point.Z=MathF.Min(point.Z,Back(x,point.Y).Z);
        return point;
    }
    public static Vector3 Yoke(float u)=>Back((u-.5f)*.56f,.18f);
    public static Vector3 Anchor=>Back(0,.18f);
}

using System;
using System.Numerics;

// Shared by the authored meshes and offline geometry checks. No engine allocation.
public static class ActorAccessorySurface
{
    public const float ClothThickness=.012f,BrimThickness=.016f;
    public static Vector3 Bow(float sign,float u,float v)
    {
        float edge=v*2-1,spread=.045f+.17f*u;
        return new(sign*(.035f+.43f*u),edge*spread,
            .018f+.10f*MathF.Sin(u*MathF.PI)*(1-edge*edge)+.018f*MathF.Sin(edge*MathF.PI)*u);
    }
    public static Vector3 BowTrim(float sign,float u,float v)=>Bow(sign,u,v)+Vector3.UnitZ*.005f;
    public static Vector3 HatCone(float t,float angle)
    {
        float r=.40f*MathF.Pow(MathF.Max(0,1-t),.78f)+.012f*(1-t);
        return new(.025f+.13f*t*t+MathF.Cos(angle)*r,.028f+.68f*t,-.02f-.08f*t*t+MathF.Sin(angle)*r);
    }
    public static Vector3 HatBrim(float u,float angle)
    {
        float r=.035f+.65f*u;
        return new(MathF.Cos(angle)*r,u*u*(.018f+MathF.Sin(angle*2+.4f)*.025f),MathF.Sin(angle)*r);
    }
    public static Vector3 HatBrimUnderside(float u,float angle)=>HatBrim(u,angle)-Vector3.UnitY*BrimThickness;
    public static Vector3 HatBrimRim(float angle)=>HatBrim(1,angle)-Vector3.UnitY*BrimThickness*.5f;
}

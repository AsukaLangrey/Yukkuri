using System;
using System.Numerics;

// Shared by the authored meshes and offline geometry checks. No engine allocation.
public static class ActorAccessorySurface
{
    public const float ClothThickness=.012f,BrimThickness=.016f;
    public const float HatBowScale=.38f;
    public const float HatHeight=.64f;
    // Bend the small bow around the crown; a flat, translated bow cut into it.
    public static Vector3 HatBow(Vector3 cloth)
    {
        float y=.19f+cloth.Y,t=Math.Clamp((y-.028f)/HatHeight,0,1);
        var center=HatCone(t,0);float radius=center.X-(.025f+.13f*t*t);
        center=new(.025f+.13f*t*t,y,-.02f-.08f*t*t);
        var normal=new Vector3(.70710678f,0,.70710678f);
        var tangent=new Vector3(.70710678f,0,-.70710678f);
        return center+tangent*cloth.X+normal*(MathF.Sqrt(MathF.Max(0,radius*radius-cloth.X*cloth.X))+.038f+cloth.Z);
    }
    public static Vector3 Bow(float sign,float u,float v)
    {
        float edge=v*2-1,spread=.045f+.17f*u;
        return new(sign*(.035f+.43f*u),edge*spread,
            .018f+.10f*MathF.Sin(u*MathF.PI)*(1-edge*edge)+.018f*MathF.Sin(edge*MathF.PI)*u);
    }
    public static Vector3 BowTrim(float sign,float u,float v)=>Bow(sign,u,v)+Vector3.UnitZ*.005f;
    public static Vector3 BowTail(float sign,float u,float v)=>new(sign*(.09f+.06f*u+(v-.5f)*.13f),-.03f-.33f*u,
        -.03f+.035f*MathF.Sin(u*MathF.PI)+.02f*MathF.Sin(v*MathF.Tau));
    public static Vector3 HatCone(float t,float angle)
    {
        float r=.40f*MathF.Pow(MathF.Max(0,1-t),.78f)+.012f*(1-t);
        return new(.025f+.13f*t*t+MathF.Cos(angle)*r,.028f+HatHeight*t,-.02f-.08f*t*t+MathF.Sin(angle)*r);
    }
    public static Vector3 HatBrim(float u,float angle)
    {
        float r=.035f+.65f*u;
        return new(MathF.Cos(angle)*r,u*u*(.018f+MathF.Sin(angle*2+.4f)*.025f),MathF.Sin(angle)*r);
    }
    public static Vector3 HatBrimUnderside(float u,float angle)=>HatBrim(u,angle)-Vector3.UnitY*BrimThickness;
    public static Vector3 HatBrimRim(float angle)=>HatBrim(1,angle)-Vector3.UnitY*BrimThickness*.5f;
}

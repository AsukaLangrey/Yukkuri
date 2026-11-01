using System;
using System.Numerics;

public enum MouthCurve { Smile, Worry, Defiant, Flat }
public static class ActorMouthSurface
{
    public static Vector2 Center(MouthCurve curve,float u)
    {
        if(curve==MouthCurve.Flat)return new(-.055f+.11f*u,-.17f);
        if(curve==MouthCurve.Defiant)
        {
            float t=1-u;
            return new Vector2(-.09f,-.15f)*(t*t*t)+new Vector2(-.04f,-.19f)*(3*t*t*u)+
                new Vector2(.025f,-.175f)*(3*t*u*u)+new Vector2(.10f,-.132f)*(u*u*u);
        }
        return new(-.095f+.19f*u,-.16f+(curve==MouthCurve.Worry?1:-1)*.028f*MathF.Sin(u*MathF.PI));
    }
    public static Vector3 Point(MouthCurve curve,float u,float v,float offset=.011f)
    {
        var tangent=Vector2.Normalize(Center(curve,MathF.Min(1,u+.0005f))-Center(curve,MathF.Max(0,u-.0005f)));
        var normal=new Vector2(-tangent.Y,tangent.X);
        float end=Math.Clamp((MathF.Abs(u*2-1)-.88f)/.12f,0,1);
        float halfWidth=(curve==MouthCurve.Flat?.008f:.010f)*MathF.Sqrt(MathF.Max(0,1-end*end));
        var p=Center(curve,u)+normal*((v*2-1)*halfWidth);
        return new(p.X,p.Y,ActorFaceSurface.Height(p.X,p.Y,offset));
    }
}

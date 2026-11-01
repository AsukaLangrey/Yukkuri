using System;
using System.Numerics;

public static class ActorBrowSurface
{
    public const int Segments=24,Across=4;
    public const float CenterY=.184f,FrightHeight=.006f;
    public const float SurfaceOffset=.011f,EdgeRecess=.0015f;
    public static float Recess(float u,float v)
    {
        if(u<=0||u>=1)return 0;
        float along=MathF.Sin(MathF.PI*u),edge=v*2-1;
        return EdgeRecess*along*along*edge*edge;
    }
    public static Vector3 Point(float side,float angleDegrees,float height,float u,float v)
    {
        float angle=angleDegrees*MathF.PI/180,sine=MathF.Sin(MathF.PI*u);
        var along=new Vector2(MathF.Cos(angle),MathF.Sin(angle));var across=new Vector2(-along.Y,along.X);
        float halfWidth=.009f*MathF.Sqrt(MathF.Max(0,sine));
        var p=new Vector2(side*.235f,CenterY+height)+along*((u-.5f)*.18f)+across*((v*2-1)*halfWidth)+Vector2.UnitY*(.004f*sine);
        // Keep the original outline and center ridge. Only thin the sides into
        // the existing face gap, never pushing a new ridge beyond the old front.
        return new(p.X,p.Y,ActorFaceSurface.Height(p.X,p.Y,SurfaceOffset-Recess(u,v)));
    }
    public static Vector3 Normal(float side,float angleDegrees,float height,float u,float v)
    {
        var point=Point(side,angleDegrees,height,u,v);
        // The strip closes to one point at each tip. Its relief and slope vanish
        // there, so the supporting face supplies the stable limiting normal.
        if(u<=0||u>=1)return ActorFaceSurface.Normal(point.X,point.Y);
        const float step=.0005f;
        var du=Point(side,angleDegrees,height,MathF.Min(1,u+step),v)-
            Point(side,angleDegrees,height,MathF.Max(0,u-step),v);
        var dv=Point(side,angleDegrees,height,u,MathF.Min(1,v+step))-
            Point(side,angleDegrees,height,u,MathF.Max(0,v-step));
        var normal=Vector3.Cross(du,dv);
        float lengthSquared=normal.LengthSquared();
        if(!float.IsFinite(lengthSquared)||lengthSquared<1e-20f)
            return ActorFaceSurface.Normal(point.X,point.Y);
        normal/=MathF.Sqrt(lengthSquared);
        return normal.Z<0?-normal:normal;
    }
}

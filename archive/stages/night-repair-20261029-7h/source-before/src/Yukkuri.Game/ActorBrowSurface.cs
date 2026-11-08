using System;
using System.Numerics;

public static class ActorBrowSurface
{
    public const int Segments=24,Across=4;
    public const float CenterY=.184f,FrightHeight=.006f;
    public static Vector3 Point(float side,float angleDegrees,float height,float u,float v)
    {
        float angle=angleDegrees*MathF.PI/180,sine=MathF.Sin(MathF.PI*u);
        var along=new Vector2(MathF.Cos(angle),MathF.Sin(angle));var across=new Vector2(-along.Y,along.X);
        float halfWidth=.009f*MathF.Sqrt(MathF.Max(0,sine));
        var p=new Vector2(side*.235f,CenterY+height)+along*((u-.5f)*.18f)+across*((v*2-1)*halfWidth)+Vector2.UnitY*(.004f*sine);
        return new(p.X,p.Y,ActorFaceSurface.Height(p.X,p.Y,.011f));
    }
}

using System;
using System.Numerics;

// Kept independent of Godot so facial attachment geometry can be checked without a game window.
public static class ActorFaceSurface
{
    // One continuous skin ellipsoid: facial details share the body's surface, without a second face shell.
    public const float RadiusX=.565f,RadiusY=.445f,RadiusZ=.51f,CenterY=-.015f,CenterZ=.04f;
    public static float Height(float x,float y,float offset=0)
    {
        float q=1-x*x/(RadiusX*RadiusX)-(y-CenterY)*(y-CenterY)/(RadiusY*RadiusY);
        return CenterZ+RadiusZ*MathF.Sqrt(MathF.Max(0,q))+offset;
    }
    public static Vector3 Normal(float x,float y)
    {
        return Vector3.Normalize(new Vector3(x/(RadiusX*RadiusX),(y-CenterY)/(RadiusY*RadiusY),
            (Height(x,y)-CenterZ)/(RadiusZ*RadiusZ)));
    }
    public static float Bottom(float x,float z,float offset=0)
    {
        float q=1-x*x/(RadiusX*RadiusX)-(z-CenterZ)*(z-CenterZ)/(RadiusZ*RadiusZ);
        return CenterY-RadiusY*MathF.Sqrt(MathF.Max(0,q))-offset;
    }
    public static Vector3 Point(float centerX,float centerY,float width,float height,float radius,float angle,float offset)
    {
        float x=centerX+MathF.Cos(angle)*width*.5f*radius;
        float y=centerY+MathF.Sin(angle)*height*.5f*radius;
        return new(x,y,Height(x,y,offset));
    }
}

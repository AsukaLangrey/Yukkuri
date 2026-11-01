using System;
using System.Numerics;

public static class InteractionLighterSurface
{
    public const int Rows=16,Sides=16;

    // u travels from the closed lower pole to the contact tip; v wraps once.
    // r <= 2u(1-u) <= sqrt(u(1-u)), so every point stays in the old unit sphere.
    public static Vector3 Point(float u,float v)
    {
        if(u<=0)return new(0,-.5f,0);
        if(u>=1)return new(0,.5f,0);
        float radius=2*u*(1-u)*(1-.45f*u);
        float angle=v==1?0:v*MathF.Tau;
        return new(radius*MathF.Cos(angle),u-.5f,radius*MathF.Sin(angle));
    }

    public static Vector3 Normal(float u,float v)
    {
        if(u<=0)return -Vector3.UnitY;
        if(u>=1)return Vector3.UnitY;
        float slope=2*(1-2*u)*(1-.45f*u)-.9f*u*(1-u);
        float angle=v==1?0:v*MathF.Tau;
        return Vector3.Normalize(new(MathF.Cos(angle),-slope,MathF.Sin(angle)));
    }
}

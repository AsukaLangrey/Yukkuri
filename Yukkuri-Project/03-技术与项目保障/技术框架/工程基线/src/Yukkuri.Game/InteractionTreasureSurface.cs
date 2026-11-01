using System;
using System.Numerics;

public static class InteractionTreasureSurface
{
    public static Vector3 Point(float latitude,float angle,float offset=0)
    {
        float s=MathF.Sin(latitude),c=MathF.Cos(latitude);
        float shape=1+.045f*MathF.Sin(angle*3)*s*s;
        return new(MathF.Cos(angle)*s*(.104f*shape+offset),.08f+c*(.076f+offset),MathF.Sin(angle)*s*(.084f*shape+offset));
    }
    public static Vector3 Vein(float u,float v)
    {
        float angle=v*MathF.Tau,latitude=MathF.PI*.45f+.19f*MathF.Sin(angle)+(u-.5f)*.13f;
        return Point(latitude,angle,.0006f);
    }
}

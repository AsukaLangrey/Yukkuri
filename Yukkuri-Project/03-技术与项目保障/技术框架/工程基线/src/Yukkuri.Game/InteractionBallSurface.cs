using System;
using System.Numerics;
using Yukkuri.Simulation;

public static class InteractionBallSurface
{
    public const float Radius=InteractionFixtureGeometry.BallRadius;
    public const int Rows=24,Panels=6,ColumnsPerPanel=12;
    public static readonly string[] Colors=["c69a79","a9bbb0","e5d8b8","b9877c","b5c2bb","e6d4ad"];
    public static Vector3 Point(float u,float longitude)
    {
        float latitude=u*MathF.PI,band=longitude*Panels/MathF.Tau;
        float edge=MathF.Abs(band-MathF.Round(band));
        float seam=.0018f*MathF.Exp(-edge*edge/.0008f)*MathF.Sin(latitude);
        float radius=Radius-seam;
        return new Vector3(MathF.Sin(latitude)*MathF.Cos(longitude),MathF.Cos(latitude),MathF.Sin(latitude)*MathF.Sin(longitude))*radius;
    }
    public static float Shade(float u,float panelU)
    {
        float edge=MathF.Min(panelU,1-panelU);
        return .86f+.14f*MathF.Min(1,edge*24)*(.92f+.08f*MathF.Sin(u*MathF.PI));
    }
}

public sealed class InteractionBallMotion
{
    private bool _initialized;
    private double _x,_z;
    public Quaternion Rotation {get;private set;}=Quaternion.Identity;
    public void Present(double x,double z,bool paused,float rollFraction=1)
    {
        if(!_initialized){_x=x;_z=z;_initialized=true;return;}
        if(paused)return;
        float dx=(float)(x-_x),dz=(float)(z-_z);_x=x;_z=z;
        float distance=MathF.Sqrt(dx*dx+dz*dz);
        if(distance<.000001f)return;
        var axis=new Vector3(dz/distance,0,-dx/distance);
        Rotation=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(axis,distance/InteractionBallSurface.Radius*rollFraction)*Rotation);
    }
}

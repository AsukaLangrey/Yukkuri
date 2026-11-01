using System;
using System.Numerics;

public static class InteractionToolSurface
{
    public const float FingerTiltDegrees=-20;
    public static readonly Vector3 FingerPad=new(0,-.035f,-.17f);
    public const float CupTop=.075f,CupRimRadius=.12f;
    public const float GroundCupLift=.075f;
    public const float WaterHeight=.056f,WaterRadius=.098f;
    public static Vector3 CupLip=>new(0,CupTop,-CupRimRadius);
    public static float SipWaterHeight(float progress)=>WaterHeight-.041f*Math.Clamp(progress,0,1);
    public static float SipWaterRadius(float height)=>MathF.Min(WaterRadius,.064f+.04f*(height+.057f)/.132f-.002f);
    public static Vector3 Cloth(float radius,float angle,bool underside=false)
    {
        float c=MathF.Cos(angle),s=MathF.Sin(angle);
        float x=MathF.CopySign(MathF.Pow(MathF.Abs(c),.6f),c)*.14f*radius;
        float z=MathF.CopySign(MathF.Pow(MathF.Abs(s),.6f),s)*.11f*radius;
        float y=-.010f*(1-radius*radius)+.004f*MathF.Sin(angle*2)*radius;
        return new(x,y-(underside?.022f:0),z);
    }
    public static Vector3 CupHandle(float u)
    {
        float a=-MathF.PI*.5f+u*MathF.PI,y=MathF.Sin(a)*.05f;
        float wall=.08f+.04f*(y+.075f)/.15f;
        return new(wall+MathF.Cos(a)*.062f,y,.005f);
    }
}

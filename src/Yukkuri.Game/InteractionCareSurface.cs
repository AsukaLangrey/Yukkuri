using System;
using System.Numerics;

public static class InteractionCareSurface
{
    public static readonly Vector3 NeedleTip=new(0,0,-.20f);
    public const float NeedleTiltDegrees=-20,PadBottom=-.022f;
    public const int GauzeRows=12,GauzeSides=48;
    public const float GauzeFoldMaximum=.0009f;
    // One closed, softly rounded cloth volume; the gauze is a smaller folded layer.
    public static Vector3 Pad(float u,float v,bool gauze=false)
    {
        var point=PadShell(u,v,gauze);
        if(gauze)point.Y-=GauzeFoldDepth(u,point);
        return point;
    }
    private static Vector3 PadShell(float u,float v,bool gauze)
    {
        float theta=u*MathF.PI,angle=v*MathF.Tau;
        float r=MathF.Pow(MathF.Max(0,MathF.Sin(theta)),.34f);
        float Round(float value)=>MathF.Abs(value)<.000001f?0:MathF.CopySign(MathF.Pow(MathF.Abs(value),.35f),value);
        float x=Round(MathF.Cos(angle)),z=Round(MathF.Sin(angle));
        return new(x*r*(gauze?.048f:.115f),(gauze?.024f:0)+MathF.Cos(theta)*(gauze?.006f:.022f),z*r*(gauze?.042f:.075f));
    }
    public static float GauzeFoldDepth(float u,float v)=>GauzeFoldDepth(u,PadShell(u,v,true));
    private static float GauzeFoldDepth(float u,Vector3 point)
    {
        if(u<=0||u>=.5f)return 0;
        float x=MathF.Abs(point.X/.048f),z=MathF.Abs(point.Z/.042f);
        float across=Math.Clamp(1-MathF.Abs(x-.55f)/.20f,0,1);
        float along=Math.Clamp((.88f-z)/.20f,0,1);
        across=across*across*(3-2*across);along=along*along*(3-2*along);
        float upper=MathF.Cos(u*MathF.PI);
        // .006*upper - depth >= .0051*upper: folds never cross the
        // upper half's equator. The center, outer edge and lower half stay intact.
        return GauzeFoldMaximum*across*along*upper*upper;
    }
    public static Vector3 PadSeam(float u)
    {
        var p=Pad(.26f,u);p.Y+=.0012f;return p;
    }
    public static Vector3 Needle(float u,float v)
    {
        float radius=.0038f*MathF.Min(1,u*8);
        return new(MathF.Cos(v*MathF.Tau)*radius,MathF.Sin(v*MathF.Tau)*radius,NeedleTip.Z+u*.286f);
    }
    public static Vector3 NeedleEye(float u)
    {
        float angle=u*MathF.Tau;
        return new(MathF.Cos(angle)*.0065f,0,.103f+MathF.Sin(angle)*.018f);
    }
    public static Vector3 Thread(float u)
    {
        float x=.003f+.053f*MathF.Sin(u*MathF.PI*1.6f),y=.002f+.011f*MathF.Sin(u*MathF.PI);
        return new(x,y,.105f+u*.15f);
    }
}

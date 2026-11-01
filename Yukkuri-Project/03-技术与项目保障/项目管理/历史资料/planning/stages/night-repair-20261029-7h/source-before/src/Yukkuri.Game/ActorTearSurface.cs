using System;
using System.Numerics;

public static class ActorTearSurface
{
    public static Vector3 Point(float side,float radius,float angle,bool glint=false)
    {
        float x=MathF.Cos(angle)*radius,y=MathF.Sin(angle)*radius;
        if(glint){x=x*.16f-.28f;y=y*.34f-.30f;}
        float px=side*(.27f+.006f*(1-y))+x*.028f*(.55f-.45f*y);
        float py=-.16f+y*.085f;
        return new(px,py,ActorFaceSurface.Height(px,py,glint?.019f:.015f));
    }
}

public sealed class ActorTearPresentation
{
    public float Amount {get;private set;}
    public void Advance(bool crying,float delta)
    {
        float step=Math.Clamp(delta,0,.1f)*(crying?3:.45f);
        Amount=crying?MathF.Min(1,Amount+step):MathF.Max(0,Amount-step);
    }
}

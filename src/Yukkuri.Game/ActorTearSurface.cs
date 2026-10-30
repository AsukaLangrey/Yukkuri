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
    public static float Target(float pain,float fear,float alarm,bool distressed,bool critical=false,bool dead=false)
    {
        if(dead)return 0;
        float sore=Math.Clamp((pain-15)/65,0,1);
        float frightened=Math.Clamp((fear-30)/65,0,1);
        float lingering=Math.Clamp(alarm/100,0,1)*.55f;
        float target=MathF.Max(distressed?.70f:0,MathF.Max(sore,MathF.Max(frightened,lingering)));
        return critical?MathF.Min(.55f,target):target;
    }
    // Reading a save observes the existing state; it does not replay onset.
    public void Initialize(float amount)=>Amount=Math.Clamp(amount,0,1);
    public void Advance(float target,float delta)=>AdvanceTowards(target,delta,.20f);
    public void Advance(bool crying,float delta)
        =>AdvanceTowards(crying?1:0,delta,.45f);
    private void AdvanceTowards(float target,float delta,float fadeSpeed)
    {
        target=Math.Clamp(target,0,1);
        float step=Math.Clamp(delta,0,.1f)*(target>Amount?3:fadeSpeed);
        Amount=target>Amount?MathF.Min(target,Amount+step):MathF.Max(target,Amount-step);
    }
}

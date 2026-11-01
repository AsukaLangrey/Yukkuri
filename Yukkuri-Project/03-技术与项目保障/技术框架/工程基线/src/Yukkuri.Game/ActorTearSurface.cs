using System;
using System.Numerics;

public static class ActorTearSurface
{
    public static Vector2 SurfaceCoordinate(float radius,float angle,bool glint=false)
    {
        float x=MathF.Cos(angle)*radius,y=MathF.Sin(angle)*radius;
        if(glint){x=x*.16f-.28f;y=y*.34f-.30f;}
        return new(x,y);
    }
    public static float SurfaceOffset(Vector2 coordinate)
    {
        float cap=MathF.Max(0,1-coordinate.LengthSquared());
        return .015f+.002f*cap*cap;
    }
    public static float EdgeOpacity(float radius,bool glint=false)
    {
        float start=glint?.60f:.70f;
        float edge=Math.Clamp((radius-start)/(1-start),0,1);
        return 1-edge*edge*(3-2*edge);
    }
    public static Vector3 Point(float side,float radius,float angle,bool glint=false)
    {
        var coordinate=SurfaceCoordinate(radius,angle,glint);
        float x=coordinate.X,y=coordinate.Y;
        float px=side*(.27f+.006f*(1-y))+x*.028f*(.55f-.45f*y);
        float py=-.16f+y*.085f;
        // A glint samples the underlying track at its transformed coordinate,
        // so its separation remains .002 even away from the track's center.
        float offset=SurfaceOffset(coordinate)+(glint?.002f:0);
        return new(px,py,ActorFaceSurface.Height(px,py,offset));
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

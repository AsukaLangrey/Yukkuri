using System;
using System.Numerics;

// Static surface detail only. Injury severity and treatment visibility stay in
// ActorInjuryPresentation; neither the patch nor its colour creates an injury.
public static class ActorInjurySurface
{
    public const int Rows=5,Sides=32;

    private static float Radius(float radius)=>float.IsFinite(radius)?Math.Clamp(radius,0,1):0;
    private static float Angle(float angle)=>float.IsFinite(angle)?angle%MathF.Tau:0;

    public static Vector2 Surface(float radius,float angle,bool scar)
    {
        float r=Radius(radius),a=Angle(angle),x=MathF.Cos(a),y=MathF.Sin(a);
        if(scar)
        {
            // Keep the old horizontal ends, but draw the healed mark as a
            // narrow tapered trace. Every point remains inside the old ellipse.
            return new(r*x,r*y*(.55f+.45f*x*x));
        }
        float contour=.88f+.12f*(.5f+.5f*MathF.Cos(3*a))*(.5f+.5f*MathF.Sin(5*a));
        return new Vector2(x,y)*(r*contour);
    }

    // site is the original injury site (already 0.82 of its dressing size).
    public static Vector3 Point(BodySurfaceMark site,float radius,float angle,bool scar)
    {
        var mark=scar?site with {Size=site.Size*new Vector2(.7f,.3f)}:site;
        return ActorSurfaceMarks.Project(mark,Surface(radius,angle,scar),scar?.012f:.011f);
    }

    public static Vector3 Tint(float radius,float angle,bool scar)
    {
        float r=Radius(radius),a=Angle(angle),center=1-r*r;
        if(scar)
        {
            float ridge=Math.Clamp(1-MathF.Abs(Surface(r,a,true).Y)/.55f,0,1);
            float shade=.08f*center*(1-ridge*ridge);
            return new(1-shade,1-shade,1-shade);
        }
        // The angular variation vanishes at the shared centre, so the polar
        // seam and duplicated centre vertices have one continuous colour.
        float depth=center*(1+.15f*r*MathF.Sin(3*a));
        return new(1-.14f*depth,1-.10f*depth,1-.07f*depth);
    }

    public static float EdgeOpacity(float radius)
    {
        float r=Radius(radius);
        return MathF.Sqrt(MathF.Max(0,1-r*r));
    }
}

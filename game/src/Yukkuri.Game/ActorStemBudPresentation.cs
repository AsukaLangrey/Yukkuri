using System;

public readonly record struct StemBudGrowth(float Scale,float OffsetY);

// Read the existing brood progress; never advance growth or announce a birth.
public static class ActorStemBudPresentation
{
    public const float MatureHeight=.13f;

    public static StemBudGrowth Sample(float progress)
    {
        float p=float.IsFinite(progress)?Math.Clamp(progress,0,1):0;
        float size=.18f+.82f*p*p*(3-2*p);
        // Shrink about the old lower pole instead of the centre: the branch
        // remains attached, and the smaller ellipsoid stays inside the old one.
        return new(size,-MatureHeight*.5f*(1-size));
    }
}

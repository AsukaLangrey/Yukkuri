using System;
using System.Numerics;

public enum EyeCurve { Open, Relaxed, Joy, Sleep }
public enum EyeLayer { Outline, Iris, Pupil, MainLight, SmallLight }

// Face-conforming shapes. Layers share normalized eye coordinates, so narrowing
// the lids cannot leave a highlight floating above or outside the visible eye.
public static class ActorEyeSurface
{
    public static Vector2 Coordinate(EyeLayer layer,float radius,float angle)
    {
        Vector2 center=layer switch
        {EyeLayer.Iris=>new(0,-.14f),EyeLayer.Pupil=>new(0,.16f),EyeLayer.MainLight=>new(-.27f,.43f),EyeLayer.SmallLight=>new(.28f,-.53f),_=>Vector2.Zero};
        Vector2 radii=layer switch
        {EyeLayer.Iris=>new(.78f,.78f),EyeLayer.Pupil=>new(.59f,.62f),EyeLayer.MainLight=>new(.23f,.23f),EyeLayer.SmallLight=>new(.11f,.11f),_=>Vector2.One};
        return center+new Vector2(MathF.Cos(angle),MathF.Sin(angle))*radii*radius;
    }
    public static float Offset(EyeLayer layer)=>layer switch
    {EyeLayer.Outline=>.009f,EyeLayer.Iris=>.011f,EyeLayer.Pupil=>.013f,EyeLayer.MainLight=>.017f,_=>.016f};
    public static bool Visible(EyeLayer layer,float openness)=>layer==EyeLayer.Outline||
        openness>=(layer is EyeLayer.MainLight or EyeLayer.SmallLight?.35f:.25f);
    public static Vector3 Point(float side,float openness,EyeCurve curve,EyeLayer layer,float radius,float angle)
    {
        var p=Coordinate(layer,radius,angle);
        float arc=curve switch {EyeCurve.Relaxed=>.016f,EyeCurve.Joy=>.033f,EyeCurve.Sleep=>-.018f,_=>0};
        float x=side*.23f+p.X*.08f;
        float y=.04f+p.Y*.12f*Math.Clamp(openness,.05f,1)+arc*(1-p.X*p.X);
        return new(x,y,ActorFaceSurface.Height(x,y,Offset(layer)));
    }
}

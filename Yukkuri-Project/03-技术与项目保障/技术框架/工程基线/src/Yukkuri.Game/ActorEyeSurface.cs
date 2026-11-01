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
    public static float Arc(EyeCurve curve)=>curve switch
    {EyeCurve.Relaxed=>.016f,EyeCurve.Joy=>.033f,EyeCurve.Sleep=>-.018f,_=>0};
    public static float Reveal(EyeLayer layer,float openness)
    {
        if(layer==EyeLayer.Outline)return 1;
        float end=layer is EyeLayer.MainLight or EyeLayer.SmallLight?.50f:.42f;
        float t=Math.Clamp((openness-.20f)/(end-.20f),0,1);
        return t*t*(3-2*t);
    }
    public static Vector2 ProjectedCoordinate(EyeLayer layer,float openness,float radius,float angle)=>
        Coordinate(layer,radius,angle)*Reveal(layer,openness);
    public static bool Visible(EyeLayer layer,float openness)=>layer==EyeLayer.Outline||Reveal(layer,openness)>0;
    public static Vector3 Point(float side,float openness,EyeCurve curve,EyeLayer layer,float radius,float angle)
        =>Point(side,openness,Arc(curve),layer,radius,angle);
    public static Vector3 Point(float side,float openness,float arc,EyeLayer layer,float radius,float angle)
    {
        // Inner geometry shrinks within the same normalized lid aperture, then is
        // absent at closure. No transparent iris or glint survives behind the lid.
        var p=ProjectedCoordinate(layer,openness,radius,angle);
        arc=Math.Clamp(arc,-.018f,.033f);
        float x=side*.23f+p.X*.08f;
        float y=.04f+p.Y*.12f*Math.Clamp(openness,.05f,1)+arc*(1-p.X*p.X);
        return new(x,y,ActorFaceSurface.Height(x,y,Offset(layer)));
    }
}

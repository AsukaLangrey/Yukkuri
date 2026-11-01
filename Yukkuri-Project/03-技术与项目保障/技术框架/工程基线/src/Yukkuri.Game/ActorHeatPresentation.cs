using System;
using System.Numerics;

public readonly record struct HeatBodyFrame(Vector3 Scale,float Pitch,float? Heading);

// Heat changes local muscle tension, never the body's actual position. A heading
// is supplied only for a current escape that has already travelled in the world.
public static class ActorHeatPresentation
{
    public static HeatBodyFrame? Sample(double heatTension,bool restrained,bool fleeing,Vector2 actualTravel)
    {
        if(!double.IsFinite(heatTension)||heatTension<=0)return null;
        float tension=(float)Math.Clamp(heatTension/100,0,1);
        if(restrained)return new(new(1+tension*.025f,1-tension*.065f,1+tension*.025f),0,null);

        bool travelled=fleeing&&float.IsFinite(actualTravel.X)&&float.IsFinite(actualTravel.Y)&&
            float.IsFinite(actualTravel.LengthSquared())&&actualTravel.LengthSquared()>1e-8f;
        float compression=tension*(travelled?.04f:.025f);
        return new(new(1+compression*.4f,1-compression,1+compression*.4f),travelled?-tension*4:0,
            travelled?MathF.Atan2(actualTravel.X,actualTravel.Y):null);
    }
}

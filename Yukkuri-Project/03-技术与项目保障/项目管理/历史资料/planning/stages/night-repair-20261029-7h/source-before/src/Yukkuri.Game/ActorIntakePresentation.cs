using System;
using Yukkuri.Simulation;

// Finite prebuilt mouth shapes, sampled without allocating meshes during a gesture.
public readonly record struct IntakeAppearance(bool Active,int MouthFrame,float Width,float Height);
public static class ActorIntakePresentation
{
    public static IntakeAppearance Sample(IntakeKind kind,double remaining,bool offering)
    {
        if(remaining<=0)return offering?new(true,0,1,1):new(false,-1,1,1);
        double age=Math.Max(0,InteractionLife.IntakeDuration(kind)-remaining);
        if(remaining<.25)
            return new(true,4,1,(float)(1-.018*Math.Sin(remaining/.25*Math.PI)));
        if(kind==IntakeKind.Water)
            return new(true,3,1,(float)(1+Math.Sin(age*7)*.006));
        float chew=(float)Math.Abs(Math.Sin(age*11));
        return new(true,chew>.52f?2:1,1+chew*.022f,1-chew*.017f);
    }
}

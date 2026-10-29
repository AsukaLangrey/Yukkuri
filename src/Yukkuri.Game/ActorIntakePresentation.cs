using System;
using Yukkuri.Simulation;

// Finite prebuilt mouth shapes, sampled without allocating meshes during a gesture.
public readonly record struct IntakeAppearance(bool Active,int MouthFrame,float Width,float Height);
public static class ActorIntakePresentation
{
    public static IntakeAppearance Sample(IntakeKind kind,double remaining,bool offering,bool groundIntake=false)
    {
        if(remaining<=0)return offering?new(true,0,1,1):new(false,-1,1,1);
        double age=Math.Max(0,InteractionLife.IntakeDuration(kind)-remaining);
        var shape=NaturalIntakeGeometry.Deformation(kind,remaining,groundIntake);
        if(remaining<.25)
            return new(true,4,shape.X,shape.Y);
        if(kind==IntakeKind.Water)
            return new(true,3,shape.X,shape.Y);
        if(groundIntake&&age<=NaturalResources.BiteContactSeconds)return new(true,0,shape.X,shape.Y);
        if(groundIntake)age-=NaturalResources.BiteContactSeconds;
        float chew=(float)Math.Abs(Math.Sin(age*11));
        return new(true,chew>.52f?2:1,shape.X,shape.Y);
    }
}

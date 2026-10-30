using System;
using Yukkuri.Simulation;

// Legacy keyframes remain available alongside a continuous pure surface sample.
public readonly record struct IntakeAppearance(bool Active,int MouthFrame,float Width,float Height);
// This file is linked into Simulation; keep its shape data independent of the
// Game-only surface builder and Godot so both assemblies share one definition.
public readonly record struct IntakeMouthShape(bool Active,float Width,float Height,float Tongue)
{
    public static IntakeMouthShape Inactive=>new(false,.06f,.012f,0);
    public static IntakeMouthShape ForFrame(int frame)=>frame switch
    {
        0=>new(true,.085f,.09f,0),1=>new(true,.11f,.018f,0),
        2=>new(true,.11f,.065f,1),3=>new(true,.032f,.045f,0),
        4=>new(true,.06f,.012f,0),_=>Inactive
    };
    public static IntakeMouthShape Blend(IntakeMouthShape a,IntakeMouthShape b,float amount)
    {
        float t=Math.Clamp(amount,0,1);
        return new(a.Active||b.Active,a.Width+(b.Width-a.Width)*t,
            a.Height+(b.Height-a.Height)*t,a.Tongue+(b.Tongue-a.Tongue)*t);
    }
}
public static class ActorIntakePresentation
{
    public static IntakeMouthShape SampleMouth(IntakeKind kind,double remaining,bool offering,bool groundIntake=false)
    {
        if(remaining<=0)return offering?IntakeMouthShape.ForFrame(0):IntakeMouthShape.Inactive;
        double duration=InteractionLife.IntakeDuration(kind);
        double age=Math.Max(0,duration-remaining);
        var ready=IntakeMouthShape.ForFrame(groundIntake&&kind==IntakeKind.Water?3:0);
        if(groundIntake&&age<=NaturalResources.BiteContactSeconds)return ready;
        double preparation=groundIntake?NaturalResources.BiteContactSeconds:0;
        age=Math.Max(0,age-preparation);
        IntakeMouthShape Eating(double progress)
        {
            IntakeMouthShape target;
            if(kind==IntakeKind.Water)
                target=new(true,.032f,.045f+(float)Math.Sin(progress*7)*.002f,0);
            else
            {
                float chew=(float)Math.Abs(Math.Sin(progress*11));chew=chew*chew*(3-2*chew);
                target=IntakeMouthShape.Blend(IntakeMouthShape.ForFrame(1),IntakeMouthShape.ForFrame(2),chew);
            }
            float arrival=(float)Math.Clamp(progress/.12,0,1);arrival=arrival*arrival*(3-2*arrival);
            return IntakeMouthShape.Blend(ready,target,arrival);
        }
        if(remaining>=.25)return Eating(age);
        float swallow=(float)Math.Clamp((.25-remaining)/.25,0,1);swallow=swallow*swallow*(3-2*swallow);
        return IntakeMouthShape.Blend(Eating(Math.Max(0,duration-.25-preparation)),IntakeMouthShape.ForFrame(4),swallow);
    }
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

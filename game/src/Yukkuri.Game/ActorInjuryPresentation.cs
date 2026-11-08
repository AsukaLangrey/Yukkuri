using System;
using System.Numerics;
using Yukkuri.Simulation;

// Pure presentation rules; this never changes health, position or physical ability.
public static class ActorInjuryPresentation
{
    public static string GuardPose(Creature c,string requested)
    {
        if(c.Condition==PhysicalCondition.Dead)return "dead";
        if(c.Condition==PhysicalCondition.Critical)
            return c.Interaction.Falling?"fall":c.Interaction.Carried?"carry-tense":"help";
        if(c.Pain>=40&&requested is "stroke" or "nuzzle" or "nuzzle-ready" or "invite" or "sing" or "proud" or "learn" or "rock")
            return c.Interaction.Carried?"carry-tense":"guard";
        return requested;
    }

    public static Expression ExpressionFor(Creature c)
    {
        if(c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical)return Expression.Exhausted;
        if(c.Pain>=40)return Expression.Hurt;
        if(c.Fear>=68&&c.Expression is Expression.Content or Expression.Delighted)return Expression.Frightened;
        return c.Expression;
    }

    public static float MarkOpacity(InteractionLife life,BodyPart part,SurfaceSide side,bool scar)
    {
        var site=life.Injuries.At(part,side);
        if(site==null||life.HasDressing(part,side))return 0;
        return (float)Math.Clamp(scar?site.PermanentLoss/100*.65:site.Damage/100*.62,0,.7);
    }

    public static float StepStrength(Creature c)
    {
        if(c.Condition!=PhysicalCondition.Active)return 0;
        if(!WorldSimulation.UsesGroundFoot(c))return 1;
        if(c.Interaction.FootIntegrity<=25)return 0;
        return (float)Math.Clamp(c.Interaction.FootIntegrity/100,.3,1);
    }

    public static HopFrame AdaptHop(Creature c,HopFrame normal)
    {
        float strength=StepStrength(c);
        if(strength>=1)return normal;
        // The simulation already shortens travel. Reduce the vertical effort, not
        // the horizontal position, collision shape or authored hop timing.
        return new(Vector3.Lerp(Vector3.One,normal.Scale,strength),normal.Lift*strength,
            normal.Pitch*strength,normal.Travel);
    }
}

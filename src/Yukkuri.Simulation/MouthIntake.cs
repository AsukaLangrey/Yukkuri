namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    // EatingRemaining is standard animation progress, not a separately stretched
    // duration. Geometry, reserved portions and cup levels use that same progress.
    private bool AdvanceMouthProgress(Creature c,double dt)
    {
        var i=c.Interaction;
        if(c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical||c.Family.ColdStorage.Stored)return false;
        double progress=dt*MouthFunction.IntakeRate(c,i.EatingKind);
        bool finishing=i.EatingRemaining>0&&i.EatingRemaining<=progress;
        i.NaturalBitePause=Math.Max(0,i.NaturalBitePause-dt);
        i.EatingRemaining=Math.Max(0,i.EatingRemaining-progress);
        if(i.NaturalIntakePending&&InteractionLife.IntakeDuration(i.EatingKind)-i.EatingRemaining>=NaturalResources.BiteContactSeconds)
            CompleteNaturalIntake(c);
        if(finishing&&i.GroundIntake)i.NaturalBitePause=.4;
        if(i.EatingRemaining<=0)i.GroundIntakeSourceId=null;
        return finishing;
    }

    private bool CanAcceptLegacyIntake(Creature c)=>c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering&&
        !c.Family.ColdStorage.Stored&&!FamilyRules.UnderIntervention(c)&&c.Interaction.EatingRemaining<=0;

    private void BeginLegacyIntake(Creature c,bool recordMealExperience=true)
    {
        c.Interaction.BeginIntake(IntakeKind.Meal);
        if(recordMealExperience&&!c.Interaction.Digestion.Symptomatic)LearnFoodExperience(c,RoomObjectKind.Meal,true);
    }
}

namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    // Leave the centre landing and the last approach to it clear. These are
    // intentions only: ordinary full-body collision and ground movement still apply.
    private static bool BlocksWaterPassage(Creature helper,Creature target,double x,double z)
    {
        double radius=InteractionFootprint(target),landingZ=Math.Clamp(target.Z,-7+radius,7-radius);
        double landingX=WoodlandLayout.RiverEdge(landingZ)-radius-.18;
        return SegmentMeetsCircle(landingX,landingZ,target.X,target.Z,x,z,
            radius+InteractionFootprint(helper)+.12);
    }

    private bool ClearOfWitnessedWaterPassages(Creature helper,double x,double z)=>
        !State.Creatures.Any(target=>target.Condition!=PhysicalCondition.Dead&&
            WaterEnvironment.IsInWater(State,target)&&CanWitnessWater(helper,target)&&
            BlocksWaterPassage(helper,target,x,z));

    private bool TryWaterSupportPoint(Creature helper,Creature target,out (double X,double Z) point)
    {
        double gap=InteractionFootprint(helper)+InteractionFootprint(target)+.35;
        double sign=helper.Z>=target.Z?1:-1;
        foreach(double side in new[]{sign,-sign})
        {
            double z=Math.Clamp(target.Z+side*gap,-7+InteractionFootprint(helper),7-InteractionFootprint(helper));
            double x=WoodlandLayout.RiverEdge(z)-InteractionFootprint(helper)-.18;
            if(ClearOfWitnessedWaterPassages(helper,x,z)&&CanOccupyRoom(helper,x,z))
            {point=(x,z);return true;}
        }
        point=default;return false;
    }

    private bool TryYieldWaterPassage(Creature helper,Creature target,double dt)
    {
        var i=helper.Interaction;
        // A concerned caregiver may yield without abandoning its dependent,
        // but occupied, player-controlled or incapacitated bodies are never forced.
        if(!CanRespondToWater(helper)||helper.Pain>=25||helper.Fear>=55||helper.Energy<=30||
            NaturalResources.WantsFood(helper)||NaturalResources.WantsWater(helper)||
            i.Digestion.Symptomatic||helper.Quarantine||helper.IsIntruder||
            helper.Family.ColdStorage.RecoveryRemaining>0||helper.Personality.HungerTargetId!=null||
            i.Resting||i.RestRemaining>0||i.WaitRemaining>0||i.Lowering||i.Height>.05||
            i.ActiveCue!=null||i.FollowPlayer||InteractionGestureTarget==helper.Id||
            i.Injuries.SelfCare.Active||helper.Family.FlightRemaining>0||helper.RetreatRemaining>0||
            helper.Family.Conflict.TargetId!=null||helper.Family.ChildThreat.TargetId!=null||
            !BlocksWaterPassage(helper,target,helper.X,helper.Z))return false;
        double radius=InteractionFootprint(target),gap=radius+InteractionFootprint(helper)+.3;
        double landingX=WoodlandLayout.RiverEdge(target.Z)-radius-.18;
        double sign=helper.Z>=target.Z?1:-1;
        var points=new[]{(X:landingX-gap,Z:helper.Z),(X:helper.X,Z:target.Z+sign*gap),
            (X:helper.X,Z:target.Z-sign*gap),(X:landingX-gap,Z:target.Z+sign*gap),
            (X:landingX-gap,Z:target.Z-sign*gap)};
        foreach(var point in points)
        {
            if(!ClearOfWitnessedWaterPassages(helper,point.X,point.Z)||!CanOccupyRoom(helper,point.X,point.Z))continue;
            double x=helper.X,z=helper.Z;
            RoomMove(helper,point.X,point.Z,dt);
            if(Distance(x,z,helper.X,helper.Z)<=.00001)continue;
            helper.Activity=$"给{target.Name}腾出上岸的落脚处";
            return true;
        }
        return false;
    }
}


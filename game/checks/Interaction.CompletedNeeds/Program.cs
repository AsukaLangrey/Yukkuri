using System.Text.Json;
using Yukkuri.Simulation;

void Require(bool condition,string reason){if(!condition)throw new Exception(reason);}
try
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261028);var c=sim.Selected!;
    sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();sim.State.UnlimitedResources=false;
    c.X=0;c.Z=0;c.TargetX=0;c.TargetZ=0;c.Fullness=80;c.Interaction.Hydration=20;c.Energy=90;c.Trust=70;c.Fear=0;
    c.Interaction.NextAutonomyAt=10000;
    // Keep ordinary DecisionIn and idle targets. After setup, all body positions,
    // targets, needs and intake state are changed only by the real product loop.
    var destination=new RoomObject{Id="destination",Kind=RoomObjectKind.Water,X=2,Z=0,Portions=5};
    var held=new RoomObject{Id="held",Kind=RoomObjectKind.Water,X=-3,Z=0,Portions=2};
    sim.State.RoomObjects.Add(destination);sim.State.RoomObjects.Add(held);
    Require(sim.HoldSupply(held.Id),"could not pick up the real water supply");
    var input=new InteractionInput(InteractionAction.Drink,BodyPart.Mouth,Force:.2,Duration:.05,ObjectId:held.Id);
    Require(sim.BeginInteractionGesture(c.Id,input).Success&&sim.InteractionGestureOwnsPose,"offer did not begin at the mouth");
    int stock=sim.State.Food,exposures=c.Interaction.Digestion.Exposures;
    bool accepted=false,sawHandSwallow=false,sawDestinationPending=false,sawDestinationAccepted=false;
    bool finishedSatisfiedMouth=false,movedAfterNeed=false;
    double travelAfterNeed=0,peakX=c.X;string lastPose="";
    for(int n=0;n<240;n++)
    {
        double x=c.X,z=c.Z;
        bool alreadyFinished=finishedSatisfiedMouth;
        var reply=sim.ContinueInteractionGesture(input,true);
        sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);
        lastPose=sim.InteractionGestureOwnsPose?reply.Pose:InteractionPresentation.Pose(c);
        peakX=Math.Max(peakX,c.X);
        Require(sim.HasInteractionGesture,"the held gesture ended or restarted to free movement");
        accepted|=sim.InteractionIntakeAccepted;
        Require(held.HeldByHand&&held.Portions==(accepted?1:2)&&sim.State.Food==stock,"held intake repeated, refunded or put down its container");
        if(accepted)Require(sim.InteractionIntakeAccepted&&!sim.InteractionGestureOwnsPose,"completed hand feeding reclaimed body pose");
        if(!sawDestinationPending&&lastPose=="swallow")sawHandSwallow=true;
        if(c.Interaction.GroundIntake&&c.Interaction.EatingRemaining>0&&c.Interaction.GroundIntakeSourceId==destination.Id)
        {
            sawDestinationPending|=c.Interaction.NaturalIntakePending;
            sawDestinationAccepted|=!c.Interaction.NaturalIntakePending;
            Require(!sim.InteractionGestureOwnsPose,"natural drinking was mistaken for unfinished hand feeding");
        }
        if(sawDestinationAccepted&&c.Interaction.EatingRemaining==0&&!NaturalResources.WantsWater(c))
            finishedSatisfiedMouth=true;
        double travel=WorldSimulation.Distance(x,z,c.X,c.Z);
        if(alreadyFinished&&travel>.000001)
        {
            Require(c.Interaction.EatingRemaining==0&&!NaturalResources.WantsWater(c),"observed another needs approach instead of post-drink movement");
            Require(lastPose is "idle" or "guard" or "stomach" or "careful-step","post-drink movement retained an intake pose: "+lastPose);
            movedAfterNeed=true;travelAfterNeed+=travel;
        }
    }
    string diagnostics=JsonSerializer.Serialize(new{accepted,sawHandSwallow,sawDestinationPending,sawDestinationAccepted,
        finishedSatisfiedMouth,movedAfterNeed,travelAfterNeed,endX=c.X,endZ=c.Z,peakX,c.TargetX,c.TargetZ,c.DecisionIn,
        c.Activity,c.Interaction.Hydration,c.Interaction.EatingRemaining,c.Interaction.NaturalIntakePending,
        destinationPortions=destination.Portions,heldPortions=held.Portions,c.Interaction.Digestion.Exposures,lastPose,
        sim.HasInteractionGesture,sim.InteractionIntakeAccepted,sim.InteractionGestureOwnsPose});
    Require(accepted&&sawHandSwallow&&sawDestinationPending&&sawDestinationAccepted&&finishedSatisfiedMouth&&movedAfterNeed,
        "real hand drink, autonomous drink and later ordinary movement did not all occur: "+diagnostics);
    Require(destination.Portions==4&&c.Interaction.Digestion.Exposures==exposures+2,
        "hand and destination did not each settle exactly one real intake: "+diagnostics);
    Console.WriteLine("PASS unisolated satisfied drinking resumes ordinary movement; "+diagnostics);
    return 0;
}
catch(Exception e){Console.WriteLine("FAIL unisolated completed need: "+e);return 1;}

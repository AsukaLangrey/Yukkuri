using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

var results=new List<object>();int failures=0;
var observations=new Dictionary<string,object>();
double[] Interval(int count,int n)
{
    double p=(double)count/n,z=1.96,denominator=1+z*z/n;
    double center=(p+z*z/(2*n))/denominator,margin=z*Math.Sqrt(p*(1-p)/n+z*z/(4*n*n))/denominator;
    return [center-margin,center+margin];
}
void Check(string name,Action test)
{
    try{test();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}
    catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.Message});Console.WriteLine("FAIL "+name+": "+e.Message);}
}
void Require(bool v,string why){if(!v)throw new Exception(why);}
WorldSimulation Fresh(ulong seed=20261029)
{
    var s=WorldSimulation.CreateInteractionPlaytest(seed);var c=s.Selected!;
    s.State.Creatures.RemoveAll(o=>o.Id!=c.Id);s.State.RoomObjects.Clear();
    c.X=0;c.Z=0;c.TargetX=0;c.TargetZ=0;c.Fullness=90;c.Interaction.Hydration=90;c.Energy=90;
    c.Interaction.NextAutonomyAt=10000;return s;
}
void Tick(WorldSimulation s,double seconds){for(int n=0;n<(int)Math.Round(seconds*20);n++){s.AdvanceInteractionPresentation(.05);s.Advance(.05);}}
RoomObject Source(WorldSimulation s,RoomObjectKind kind,double x=1.5,double z=0,double contamination=0,int portions=10)
{
    var o=new RoomObject{Id="source-"+s.State.RoomObjects.Count,Kind=kind,X=x,Z=z,Contamination=contamination,Portions=portions};s.State.RoomObjects.Add(o);return o;
}
WorldSimulation Reload(WorldSimulation s)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(s.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
Check("excretion keeps digestive care and injury recovery advancing in ordinary time",()=>
{
    var s=Fresh();var c=s.Selected!;var i=c.Interaction;c.DecisionIn=10000;
    c.Condition=PhysicalCondition.Recovering;c.RecoveryRemaining=20;
    i.FootTreated=true;i.FootIntegrity=50;i.FootRecovery=10;
    i.Digestion=new(){Exposures=1,Episodes=1,Severity=40,SymptomsRemaining=100,CareRemaining=60};
    i.Physiology.Bladder=90;Tick(s,.25);Require(i.Physiology.Active,"excretion never began");
    double symptoms=i.Digestion.SymptomsRemaining,care=i.Digestion.CareRemaining;
    double foot=i.FootRecovery,recovery=c.RecoveryRemaining,x=c.X,z=c.Z;
    Tick(s,1);
    Require(i.Physiology.Active&&!i.Resting&&c.X==x&&c.Z==z,"active excretion moved or gained sleep support");
    Require(i.Digestion.SymptomsRemaining<symptoms-1.4&&i.Digestion.CareRemaining<care-.9,
        "excretion froze digestive illness or care time");
    Require(i.FootRecovery<foot-.9&&c.RecoveryRemaining<recovery-.9,
        "excretion froze foot treatment or injury recovery");
});
Check("boxed excretion retains restraint time and fear without repeating help over the body action",()=>
{
    var s=Fresh();var c=s.Selected!;var i=c.Interaction;
    i.Restraint=RestraintKind.Box;i.RestraintSeconds=20;i.Physiology.Bladder=90;
    Tick(s,.25);Require(i.Physiology.Active,"boxed excretion never began");
    i.LastHelpAt=-100;c.ReactionRemaining=0;
    double restraint=i.RestraintSeconds,fear=c.Fear,containerFear=i.ContainerFear;
    Tick(s,1);
    Require(i.Physiology.Active&&i.RestraintSeconds>restraint+.9&&c.Fear>fear&&i.ContainerFear>containerFear,
        "excretion suspended the effects of the actual restraint");
    Require(i.LastHelpAt==-100&&i.EatingRemaining==0,"routine help or intake displaced active excretion");
});
Check("excretion suspends sleep benefits and then resumes the pending rest",()=>
{
    var s=Fresh();var c=s.Selected!;var i=c.Interaction;c.Energy=10;i.Physiology.Bladder=90;
    Tick(s,.25);Require(i.Physiology.Active,"resting creature never began excretion");
    Require(!i.Resting,"the first excretion frame retained the previous sleeping state");
    double energy=c.Energy,remaining=i.RestRemaining;
    Tick(s,1);
    Require(i.Physiology.Active&&!i.Resting&&c.Energy<energy&&i.RestRemaining==remaining,
        "excretion granted sleep recovery or consumed the pending rest duration");
    Tick(s,3);
    Require(i.Physiology.Urinations==1&&i.Resting&&c.Energy>energy&&i.RestRemaining<remaining,
        "completed excretion lost the pending rest");
});
Check("urgent nearby food and water resume after excretion for free and boxed creatures",()=>
{
    foreach(bool water in new[]{false,true})foreach(bool boxed in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;var i=c.Interaction;c.Energy=10;
        if(boxed)i.Restraint=RestraintKind.Box;
        i.Physiology.Bladder=90;Tick(s,.25);Require(i.Physiology.Active,"excretion never began");
        if(water)i.Hydration=10;else c.Fullness=10;
        var supply=Source(s,water?RoomObjectKind.Water:RoomObjectKind.Meal,0,.4,portions:1);
        i.LastHelpAt=s.State.WorldSeconds;i.LastThirstRequestAt=s.State.WorldSeconds;
        Tick(s,1);
        Require(i.Physiology.Active&&i.EatingRemaining==0&&supply.Portions==1,
            "active excretion also began an autonomous mouthful");
        Tick(s,5);
        Require(i.Physiology.Urinations==1&&i.Digestion.Exposures==1&&(water?i.Hydration:c.Fullness)>15,
            "urgent intake did not resume after the body action completed");
        Require(c.X==0&&c.Z==0,"reachable nearby intake unexpectedly moved the body");
    }
});
Check("close-up cancels interrupted excretion without advancing or satisfying its needs",()=>
{
    foreach(string interruption in new[]{"carry","feed","supine","critical","dead"})
    {
        var s=Fresh();var c=s.Selected!;var i=c.Interaction;c.DecisionIn=10000;c.Trust=90;
        i.Physiology.Bladder=90;Tick(s,.25);Require(i.Physiology.Active,"excretion never began");
        var p=i.Physiology;double bladder=p.Bladder,bowel=p.Bowel,time=s.State.WorldSeconds;
        s.State.Mode=PlayMode.CloseUp;
        if(interruption=="carry")
        {
            Require(s.BeginInteractionGesture(c.Id,new(InteractionAction.Lift,Height:.8)).Success,"lift did not begin");
            s.EndInteractionGesture();
        }
        else if(interruption=="feed")
        {
            i.Hydration=10;var input=new InteractionInput(InteractionAction.Drink,BodyPart.Mouth,.2,.1);
            Require(s.BeginInteractionGesture(c.Id,input).Success,"drink did not begin");
            for(int frame=0;frame<9;frame++)s.ContinueInteractionGesture(input,true);
            Require(i.EatingRemaining>0,"offered water was not accepted");s.EndInteractionGesture();
        }
        else if(interruption=="supine")i.Supine=true;
        else if(interruption=="critical"){c.Condition=PhysicalCondition.Critical;c.CriticalRemaining=20;}
        else {c.Condition=PhysicalCondition.Dead;c.Health=0;}
        Tick(s,.05);
        Require(!p.Active&&p.Excreting==ExcretionKind.None&&p.Remaining==0,
            $"{interruption} retained excretion until ordinary time resumed");
        Require(s.State.WorldSeconds==time&&p.Bladder==bladder&&p.Bowel==bowel&&
            p.Urinations==0&&p.Defecations==0&&!s.State.RoomObjects.Any(o=>WorldSimulation.IsWaste(o.Kind)),
            "presentation cancellation advanced physiology or credited a completed excretion");
    }
});
Check("ordinary world updates cancel stale excretion before choosing the active mouthful activity",()=>
{
    var s=Fresh();var c=s.Selected!;var i=c.Interaction;i.Physiology.Bladder=90;
    Tick(s,.25);Require(i.Physiology.Active,"excretion never began");
    i.BeginIntake(IntakeKind.Water);
    // Exercise the ordinary entry independently of presentation, as callers may do.
    s.Advance(.25);
    Require(!i.Physiology.Active&&c.Activity=="小口喝水"&&i.EatingRemaining>0,
        "the first interrupted world step still selected the old excretion activity");
    Require(i.Physiology.Urinations==0&&!s.State.RoomObjects.Any(o=>WorldSimulation.IsWaste(o.Kind)),
        "intake interruption completed excretion instead of preserving its unmet need");
});
Check("following uses a currently perceived hand rather than saved human coordinates",()=>
{
    foreach(string visibility in new[]{"absent","visible","box","other-region","reloaded"})
    {
        var s=Fresh();var c=s.Selected!;c.DecisionIn=10000;c.Interaction.FollowPlayer=true;
        s.State.PlayerX=3;s.State.PlayerZ=0;
        if(visibility!="absent")s.SetInteractionHand(3,0,true);
        if(visibility=="box")
        {
            var box=WorldSimulation.CreateInteractionPlaytest().State.Creatures[1];box.X=1.5;box.Z=0;
            box.Interaction.Restraint=RestraintKind.Box;s.State.Creatures.Add(box);
        }
        if(visibility=="other-region")
        {s.State.Regions.Add(new(){Id="elsewhere",Name="另一片林地",Kind=RegionKind.Sanctuary});s.State.ActiveRegionId="elsewhere";}
        if(visibility=="reloaded"){s=Reload(s);c=s.Selected!;}
        Tick(s,.5);
        Require(visibility=="visible"?c.X>0:c.X==0&&c.Z==0,$"{visibility} hand drove follow movement: {c.X}/{c.Z}");
    }
});
Check("a follow lesson cannot gain travel while its guiding hand is out of sight",()=>
{
    var s=Fresh();var c=s.Selected!;c.DecisionIn=10000;s.State.PlayerX=3;
    s.ExecuteInteraction(c.Id,new(InteractionAction.TeachFollow));c.Interaction.CueUnderstood=true;
    Tick(s,2);Require(c.X==0&&c.Interaction.CueTravel==0,"unseen hand advanced the lesson");
    s.SetInteractionHand(3,0,true);Tick(s,.5);
    Require(c.X>0&&c.Interaction.CueTravel>0,"visible hand did not resume the unfinished lesson");
});
Check("a failed follow lesson does not leave permanent following switched on",()=>
{
    var s=Fresh();var c=s.Selected!;c.DecisionIn=10000;
    s.ExecuteInteraction(c.Id,new(InteractionAction.TeachFollow));c.Interaction.CueUnderstood=false;
    Tick(s,2);Require(c.Interaction.ActiveCue==null&&!c.Interaction.FollowPlayer,"failed attempt left a persistent follow order");
});
Check("completed follow intent yields to autonomous rest with or without a visible hand",()=>
{
    foreach(bool hasHand in new[]{false,true})foreach(bool cushion in new[]{false,true})foreach(bool reload in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;c.Energy=21;c.Fear=0;c.Pain=0;
        c.Interaction.FollowPlayer=true;c.Interaction.ActiveCue=null;
        if(cushion)Source(s,RoomObjectKind.Cushion,0,0);
        if(reload){s=Reload(s);c=s.Selected!;}
        for(int n=0;n<100;n++)
        {
            if(hasHand)s.SetInteractionHand(2,0,true);
            Tick(s,.05);
        }
        Require(!c.Interaction.FollowPlayer&&c.Interaction.Resting&&c.Energy>21&&
            c.Interaction.RestRemaining is >0 and <29,"persistent follow prevented actual rest or timer progress");
    }
});
Check("low trust can cautiously use the only supply while the hand remains visible",()=>
{
    foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Water})
    {
        var s=Fresh();var c=s.Selected!;c.Trust=5;c.Fear=0;c.Interaction.LastHelpDecisionAt=0;
        if(kind==RoomObjectKind.Water)c.Interaction.Hydration=35;else c.Fullness=35;
        var supply=Source(s,kind,1.5,portions:3);
        for(int n=0;n<10;n++){s.SetInteractionHand(supply.X,supply.Z,true);Tick(s,.05);}
        Require(supply.Portions==3,"low trust skipped its initial cautious look");
        for(int n=0;n<390;n++){s.SetInteractionHand(supply.X,supply.Z,true);Tick(s,.05);}
        Require(supply.Portions<3&&(kind==RoomObjectKind.Water?c.Interaction.Hydration:c.Fullness)>35,
            "continuous human presence excluded the only supply indefinitely");
    }
});
Check("immobile intake chooses the urgent need independently of object order",()=>
{
    foreach(bool waterFirst in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;c.Interaction.FootIntegrity=10;c.Fullness=35;c.Interaction.Hydration=10;
        foreach(var kind in waterFirst?new[]{RoomObjectKind.Water,RoomObjectKind.Meal}:new[]{RoomObjectKind.Meal,RoomObjectKind.Water})
            Source(s,kind,.5,portions:3);
        Tick(s,.25);
        Require(c.Interaction.GroundIntakeKind==RoomObjectKind.Water&&c.Interaction.NaturalIntakePending&&c.X==0&&c.Z==0,
            "immobile choice followed collection order instead of the more urgent water need");
    }
});
Check("hungry creature discovers reaches and consumes leaves without human input",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=35;var leaf=Source(s,RoomObjectKind.Leaves,2.5);
    Tick(s,15);Require(c.Fullness>60&&leaf.Portions<10&&c.Interaction.LastWalkAt>0,$"did not forage autonomously: fullness={c.Fullness:F2}, portions={leaf.Portions}, x={c.X:F2}, walkedAt={c.Interaction.LastWalkAt}");
    Require(c.Interaction.Resources.ContainsKey(leaf.Id),"source was never discovered");SaveStore.Validate(s.State);
});
Check("thirsty creature walks to the bank and drinks without filling its stomach",()=>
{
    var s=Fresh();var c=s.Selected!;c.Interaction.Hydration=20;double food=c.Fullness;var water=Source(s,RoomObjectKind.RiverWater,2.5,portions:1);
    for(int n=0;n<300&&c.Interaction.Hydration<=65;n++)Tick(s,.05);
    Require(c.Interaction.Hydration>65&&c.Fullness<=food&&water.Portions==1,"river failed or was consumed as a cup");
    Require(c.X>1&&c.X<water.X,"did not use a dry approach position");
});
Check("water supply is finite while a river remains available",()=>
{
    var s=Fresh();var c=s.Selected!;c.Interaction.Hydration=20;var water=Source(s,RoomObjectKind.Water,.5,portions:1);
    Tick(s,1);Require(c.Interaction.Hydration>40&&water.Portions==0&&s.State.RoomObjects.Contains(water),"last cup vanished before drinking finished or was consumed twice");
    Tick(s,1);Require(!s.State.RoomObjects.Contains(water),"finished empty cup retained an active serving");
});
Check("last leaf remains visible until contact without becoming available twice",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=65;var leaf=Source(s,RoomObjectKind.Leaves,.5,portions:1);
    Tick(s,.25);
    Require(leaf.Portions==0&&NaturalResources.VisibleFoodPortions(s.State,leaf)==1,"claimed leaf vanished before the bite reached it");
    Require(c.Interaction.NaturalIntakePending&&c.Interaction.Digestion.Exposures==0&&c.Fullness<=65,"food credited before mouth contact");
    double before=c.Fullness;
    s.State.Mode=PlayMode.CloseUp;Tick(s,.5);
    Require(NaturalResources.VisibleFoodPortions(s.State,leaf)==0&&leaf.Portions==0,"leaf still visible after being taken");
    int exposure=c.Interaction.Digestion.Exposures;double full=c.Fullness;
    Require(!c.Interaction.NaturalIntakePending&&exposure==1&&full==before+NaturalResources.FoodValue(leaf.Kind),"contact did not credit exactly one leaf");
    Tick(s,2);Require(c.Fullness==full&&c.Interaction.Digestion.Exposures==exposure,"presentation replayed consumption");
});
Check("last cup presentation survives reload and completes while the world is in close-up",()=>
{
    var s=Fresh();var c=s.Selected!;c.Interaction.Hydration=60;var water=Source(s,RoomObjectKind.Water,.5,portions:1);
    Tick(s,.25);s.State.Mode=PlayMode.CloseUp;Tick(s,.6);
    double fill=NaturalResources.VisibleWaterFill(s.State,water);Require(fill>0&&fill<1,"last sip did not lower water");
    var loaded=Reload(s);var cup=loaded.State.RoomObjects.Single();
    Require(NaturalResources.VisibleWaterFill(loaded.State,cup)==fill&&loaded.Selected!.Interaction.GroundIntakeSourceId==cup.Id,"reload restarted the last sip");
    double hydration=loaded.Selected!.Interaction.Hydration;int exposures=loaded.Selected.Interaction.Digestion.Exposures;
    Tick(loaded,2);Require(loaded.State.RoomObjects.Count==0&&loaded.Selected.Interaction.Hydration==hydration&&
        loaded.Selected.Interaction.Digestion.Exposures==exposures,"close-up repeated intake or left a ghost source");
});
Check("taking a supply away releases the ground pose but preserves the already taken mouthful",()=>
{
    var s=Fresh();var c=s.Selected!;c.Interaction.Hydration=20;var water=Source(s,RoomObjectKind.Water,.5,portions:2);
    Tick(s,.75);double hydration=c.Interaction.Hydration;
    Require(s.HoldSupply(water.Id)&&c.Interaction.GroundIntakeSourceId==null&&c.Interaction.EatingRemaining>0,"oil kept leaning toward removed cup");
    s.State.Mode=PlayMode.CloseUp;Tick(s,2);
    Require(water.Portions==1&&c.Interaction.Hydration==hydration&&c.Interaction.Digestion.Exposures==1,"taking supply replayed or undid the first sip");
});
Check("the last cup can still be picked up while its taken sip finishes",()=>
{
    var s=Fresh();var c=s.Selected!;c.Interaction.Hydration=60;var water=Source(s,RoomObjectKind.Water,.5,portions:1);
    Tick(s,.75);double hydration=c.Interaction.Hydration;
    Require(water.Portions==0&&s.HoldSupply(water.Id)&&c.Interaction.GroundIntakeSourceId==null,"last cup became unpickable during the sip");
    Require(s.ReleaseSupply(water.Id),"empty held cup could not be released");
    s.State.Mode=PlayMode.CloseUp;Tick(s,2);
    Require(c.Interaction.Hydration==hydration&&s.State.RoomObjects.Count==0,"empty cup created water or a ghost object");
});
Check("a moving touch interrupts the source pose and oil can resume its needs afterward",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;var leaves=Source(s,RoomObjectKind.Leaves,.5,portions:5);
    Tick(s,.75);int portions=leaves.Portions;
    var input=new InteractionInput(InteractionAction.Touch,BodyPart.Head,.12,.1,.02);
    s.BeginInteractionGesture(c.Id,input);s.ContinueInteractionGesture(input,true);Tick(s,.1);
    Require(c.Interaction.GroundIntakeSourceId==null&&c.Interaction.EatingRemaining>0&&leaves.Portions==portions,"touch left a phantom ground sip or duplicated food");
    s.EndInteractionGesture();Tick(s,5);
    Require(leaves.Portions<portions&&c.Fullness>37,"ending touch did not restore self feeding");
});
Check("two creatures take turns at one source without claiming the same serving",()=>
{
    var s=WorldSimulation.CreateInteractionPlaytest();s.State.Creatures.RemoveAt(2);s.State.RoomObjects.Clear();
    for(int n=0;n<2;n++)
    {var c=s.State.Creatures[n];c.X=n==0?-.79:.79;c.Z=0;c.Fullness=35;c.Energy=90;c.Interaction.Hydration=90;c.Interaction.NextAutonomyAt=10000;}
    var food=Source(s,RoomObjectKind.Meal,0,portions:2);Tick(s,.5);
    Require(food.Portions==1&&s.State.Creatures.Count(c=>c.Interaction.EatingRemaining>0)==1,"shared source was occupied by two mouths");
    Tick(s,2);
    Require(s.State.Creatures.All(c=>c.Interaction.Digestion.Exposures==1),"one finishing mouthful immediately reclaimed the source before the waiting neighbor");
});
Check("taking food or water before contact returns the reserved portion without intake",()=>
{
    foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Sweet,RoomObjectKind.Water})
    {
        var s=Fresh();var c=s.Selected!;c.Fullness=30;c.Interaction.Hydration=30;
        var source=Source(s,kind,.5,contamination:1,portions:1);Tick(s,.25);
        Require(c.Interaction.NaturalIntakePending&&source.Portions==0,"no pending reservation");
        double food=c.Fullness,water=c.Interaction.Hydration;
        Require(s.HoldSupply(source.Id)&&source.Portions==1&&!c.Interaction.NaturalIntakePending&&c.Interaction.EatingRemaining==0,"untaken portion was not restored");
        s.State.Mode=PlayMode.CloseUp;Tick(s,2);
        Require(c.Fullness==food&&c.Interaction.Hydration==water&&c.Interaction.Digestion.Exposures==0,"interrupted preparation credited intake");
        Require(s.ReleaseSupply(source.Id)&&source.Portions==1,"cancel duplicated or lost the returned portion");SaveStore.Validate(s.State);
    }
});
Check("putting away food before or after contact returns only uneaten inventory",()=>
{
    foreach(bool entered in new[]{false,true})
    {
        var s=Fresh();s.State.UnlimitedResources=false;s.State.Food=0;var c=s.Selected!;c.Fullness=30;
        var food=Source(s,RoomObjectKind.Meal,.5,portions:1);Tick(s,entered?.75:.25);
        Require(s.PutAwayRoomObject(food.Id)&&s.State.Food==(entered?0:1),"inventory did not distinguish reserved from eaten food");
        s.State.Mode=PlayMode.CloseUp;Tick(s,2);
        Require(c.Interaction.Digestion.Exposures==(entered?1:0)&&s.State.RoomObjects.Count==0,"stored food replayed consumption");
    }
});
Check("moving touch before contact leaves the food available without exposure",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;var leaves=Source(s,RoomObjectKind.Leaves,.5,contamination:1,portions:1);
    Tick(s,.25);double food=c.Fullness;s.State.Mode=PlayMode.CloseUp;
    var input=new InteractionInput(InteractionAction.Touch,BodyPart.Head,.12,.1,.02);
    s.BeginInteractionGesture(c.Id,input);s.ContinueInteractionGesture(input,true);Tick(s,.1);
    Require(leaves.Portions==1&&!c.Interaction.NaturalIntakePending&&c.Interaction.EatingRemaining==0&&c.Fullness==food&&c.Interaction.Digestion.Exposures==0,"touch consumed an untaken leaf");
    s.EndInteractionGesture();Tick(s,1);Require(leaves.Portions==1,"releasing touch refunded twice");
});
Check("pending intake survives pause and reload and commits exactly once at contact",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;Source(s,RoomObjectKind.Leaves,.5,contamination:1,portions:1);
    Tick(s,.25);Require(c.Interaction.NaturalIntakePending,"no pending mouthful to save");
    double remaining=c.Interaction.EatingRemaining,food=c.Fullness;s.State.Settings.Pause=true;Tick(s,2);
    Require(c.Interaction.EatingRemaining==remaining&&c.Fullness==food&&c.Interaction.Digestion.Exposures==0,"pause advanced preparation");
    s.State.Settings.Pause=false;s.State.Mode=PlayMode.CloseUp;var loaded=Reload(s);var interrupted=Reload(s);
    Tick(s,.5);Tick(loaded,.5);
    Require(!loaded.Selected!.Interaction.NaturalIntakePending&&loaded.Selected.Interaction.Digestion.Exposures==1&&
        loaded.Selected.Fullness==food+7&&loaded.State.RandomState==s.State.RandomState&&
        JsonSerializer.Serialize(loaded.Selected.Interaction.Digestion)==JsonSerializer.Serialize(c.Interaction.Digestion),"reload lost or repeated pending intake");
    Tick(loaded,2);Require(loaded.Selected.Interaction.Digestion.Exposures==1&&loaded.Selected.Fullness==food+7,"mouthful completion consumed again");
    interrupted.Selected!.Interaction.Carried=true;Tick(interrupted,.05);
    Require(interrupted.State.RoomObjects.Single().Portions==1&&!interrupted.Selected.Interaction.NaturalIntakePending&&interrupted.Selected.Interaction.Digestion.Exposures==0,"reloaded pending mouthful could not be cancelled");
});
Check("a pending portion is not deleted if its consumer dies before presentation cleanup",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;var meal=Source(s,RoomObjectKind.Meal,.5,portions:1);
    Tick(s,.25);Require(c.Interaction.NaturalIntakePending,"no reserved portion");
    c.Condition=PhysicalCondition.Dead;s.Advance(.25);
    Require(s.State.RoomObjects.Contains(meal),"world cleanup deleted an uneaten reservation");
    s.AdvanceInteractionPresentation(.05);
    Require(meal.Portions==1&&!c.Interaction.NaturalIntakePending&&c.Interaction.Digestion.Exposures==0,"death credited or lost pending food");
});
Check("satisfied creature leaves food and water for later",()=>
{
    var s=Fresh();var food=Source(s,RoomObjectKind.Leaves,.4);var water=Source(s,RoomObjectKind.Water,.5);
    Tick(s,5);Require(food.Portions==10&&water.Portions==10,"satisfied oil overconsumed");
});
Check("limited movement does not itself raise appetite or water demand",()=>
{
    foreach(bool food in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;c.Interaction.FootIntegrity=20;
        c.Fullness=80;c.Interaction.Hydration=80;
        var source=Source(s,food?RoomObjectKind.Meal:RoomObjectKind.Water,.4);
        Tick(s,2);Require(source.Portions==10,"injury caused extra consumption despite satisfied needs");
        if(food)c.Fullness=60;else c.Interaction.Hydration=60;
        Tick(s,1);Require(source.Portions==9,"injured creature failed to take nearby needed supply");
    }
});
Check("placed supplies sit on box and cushion surfaces and remain reachable without walking",()=>
{
    foreach(string environment in new[]{"box","frame","cushion","wrap-cushion"})
    foreach(double size in new[]{.73,1,1.03})
    foreach(bool water in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;c.IsYoung=size<1;c.IsElder=size>1;c.Interaction.FootIntegrity=10;
        c.Fullness=water?90:30;c.Interaction.Hydration=water?30:90;
        c.Interaction.Restraint=environment=="box"?RestraintKind.Box:environment=="frame"?RestraintKind.Frame:environment=="wrap-cushion"?RestraintKind.Wrap:RestraintKind.None;
        if(environment.Contains("cushion"))Source(s,RoomObjectKind.Cushion,0,0);
        Require(s.ExecuteInteraction(c.Id,new(water?InteractionAction.PlaceWater:InteractionAction.PlaceMeal,X:0,Z:.55*size)).Success,"could not place nearby supply");
        var supply=s.State.RoomObjects.Single(o=>NaturalResources.IsResource(o.Kind));
        Require(supply.Height>.04&&Math.Abs(supply.Height-RoomSupplySupport.Height(s.State,c.RegionId,supply.X,supply.Z,supply.Kind))<.00001,"supply was buried in support");
        Tick(s,.75);
        Require(c.Interaction.Digestion.Exposures==1&&c.X==0&&c.Z==0&&c.Interaction.GroundIntakeSourceHeight==supply.Height,$"raised supply was unreachable: {environment}/{size}/{water}");
    }
});
Check("wrapping cannot drink below its mouth but nearby cup water and unwrapping restore access",()=>
{
    foreach(double size in new[]{.73,1,1.03})
    {
        var s=Fresh();var c=s.Selected!;c.IsYoung=size<1;c.IsElder=size>1;c.Interaction.Restraint=RestraintKind.Wrap;c.Interaction.Hydration=30;
        var river=Source(s,RoomObjectKind.RiverWater,.7*size);Tick(s,2);
        Require(c.Interaction.Hydration<30&&c.Interaction.Digestion.Exposures==0&&c.Interaction.EatingRemaining==0&&c.Activity.Contains("够不到"),"wrap drank from below its supported mouth");
        var cup=Source(s,RoomObjectKind.Water,0,.55*size);Tick(s,1);
        Require(c.Interaction.Hydration>40&&c.Interaction.Digestion.Exposures==1&&cup.Portions==9,"low river also disabled reachable cup water");
        Tick(s,2);s.State.RoomObjects.Remove(cup);c.Interaction.Restraint=RestraintKind.None;c.Interaction.Hydration=30;Tick(s,2);
        Require(c.Interaction.Hydration>40&&c.Interaction.Digestion.Exposures>=2&&river.Portions==10,"unwrapping did not restore river access");
    }
});
Check("wrapped low-source access follows body size and height rather than a blanket resource ban",()=>
{
    foreach(bool young in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;c.IsYoung=young;c.Interaction.Restraint=RestraintKind.Wrap;c.Interaction.Hydration=30;
        var source=Source(s,RoomObjectKind.RiverWater,.4);source.Height=.04;Tick(s,1);
        Require(c.Interaction.Digestion.Exposures==(young?1:0),"low-source reach ignored body size");
        if(!young){source.Height=.08;Tick(s,1);Require(c.Interaction.Digestion.Exposures==1,"raising the source did not make it reachable");}
    }
});
Check("changing source support before entry cancels rather than consuming from its old height",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;var meal=Source(s,RoomObjectKind.Meal,0,.5,portions:1);
    Tick(s,.25);s.State.Mode=PlayMode.CloseUp;Source(s,RoomObjectKind.Cushion,0,0);Tick(s,.5);
    Require(!c.Interaction.NaturalIntakePending&&meal.Portions==1&&meal.Height>.2&&c.Interaction.Digestion.Exposures==0,"changed support consumed a suspended pending portion");
});
Check("unknown far source is not read through the whole map",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=20;var leaf=Source(s,RoomObjectKind.Leaves,7,7);
    Tick(s,.25);Require(!c.Interaction.Resources.ContainsKey(leaf.Id)&&c.Interaction.ResourceTargetId!=leaf.Id,"source known outside senses");
    Require(c.Activity.Contains("四处"),"no exploratory intent");
});
Check("source in another region is neither discovered nor consumed",()=>
{
    var s=Fresh();s.State.Regions.Add(new(){Id="wild",Name="野地",Kind=RegionKind.Wilderness});
    s.Selected!.Fullness=10;var leaf=Source(s,RoomObjectKind.Leaves,.2);leaf.RegionId="wild";Tick(s,3);
    Require(leaf.Portions==10&&!s.Selected.Interaction.Resources.ContainsKey(leaf.Id),"cross-region consumption or knowledge");
});
Check("empty leaves remain visibly exhausted and are not consumed again",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=20;var leaf=Source(s,RoomObjectKind.Leaves,.4,portions:1);
    Tick(s,5);Require(leaf.Portions==0&&s.State.RoomObjects.Contains(leaf),"leaf depletion removed its scene evidence");
    Require(c.Interaction.Digestion.Exposures==1&&!c.Interaction.Resources[leaf.Id].Available,"depleted source was reused");
});
Check("source hidden by a solid box is not discovered",()=>
{
    var s=Fresh();var original=WorldSimulation.CreateInteractionPlaytest().State.Creatures[1];original.X=1.5;original.Z=0;original.Interaction.Restraint=RestraintKind.Box;
    s.State.Creatures.Add(original);var leaf=Source(s,RoomObjectKind.Leaves,3);s.Selected!.Fullness=20;
    Tick(s,.25);Require(!s.Selected.Interaction.Resources.ContainsKey(leaf.Id),"saw source through solid box");
});
Check("immobile oil consumes accessible water but cannot teleport to distant river",()=>
{
    var s=Fresh();var c=s.Selected!;c.Interaction.FootIntegrity=10;c.Interaction.Hydration=20;
    var water=Source(s,RoomObjectKind.Water,.4);Tick(s,1);Require(c.Interaction.Hydration>20&&c.X==0,"near water inaccessible or foot healed");
    s.State.RoomObjects.Clear();Source(s,RoomObjectKind.RiverWater,4);c.Interaction.Hydration=20;Tick(s,4);
    Require(c.Interaction.Hydration<20&&c.X==0,"immobile oil reached distant river");
});
Check("low trust can accept food after the hand retreats",()=>
{
    var s=Fresh();var c=s.Selected!;c.Trust=5;c.Fullness=30;var meal=Source(s,RoomObjectKind.Meal,.5);
    s.SetInteractionHand(.5,0,true);Tick(s,1);Require(meal.Portions==10,"low trust instantly accepted hand-adjacent food");
    s.SetInteractionHand(6,6,true);Tick(s,6);Require(meal.Portions<10,"retreating did not allow independent eating");
});
Check("a natural appeal faces the perceived hand briefly and then gives movement its pose back",()=>
{
    int appeals=0;
    for(ulong seed=1;seed<=20;seed++)
    {
        var s=Fresh(seed);var c=s.Selected!;c.Trust=100;c.Fear=0;c.Fullness=40;s.SetInteractionHand(1,2,true);
        Tick(s,.25);
        if(c.Interaction.LastHelpAt<0)continue;
        appeals++;
        Require(InteractionPresentation.Pose(c)=="help-look"&&c.X==0&&c.Z==0,"appeal did not briefly wait");
        Require(Math.Abs(c.Interaction.ReactionDirectionX-1/Math.Sqrt(5))<.00001&&
            Math.Abs(c.Interaction.ReactionDirectionZ-2/Math.Sqrt(5))<.00001,"appeal faces a fixed world direction");
        Tick(s,2);Require(InteractionPresentation.Pose(c)!="help-look"&&c.Interaction.HelpWaitingUntil<s.State.WorldSeconds,"appeal froze its later life or retained a stationary gesture over walking");
    }
    Require(appeals>0,"no actual request exercised the look direction");
});
Check("urgent self care can call for aid without keeping a planted appeal over its search",()=>
{
    int appeals=0;
    for(ulong seed=1;seed<=20;seed++)
    {
        var s=Fresh(seed);var c=s.Selected!;c.Trust=100;c.Fear=0;c.Fullness=8;s.SetInteractionHand(1,2,true);
        Tick(s,.5);if(c.Interaction.LastHelpAt<0)continue;appeals++;
        Require(InteractionPresentation.Pose(c)!="help-look"&&(c.X!=0||c.Z!=0),"urgent request stopped searching or retained a planted pose");
    }
    Require(appeals>0,"no urgent request was exercised");
});
Check("high trust increases help requests without removing self care",()=>
{
    int low=0,high=0;var rng=new Random(1009);
    for(int n=0;n<150;n++)
    {
        ulong seed=(ulong)rng.NextInt64(1,long.MaxValue);
        foreach(bool trusting in new[]{false,true})
        {
            var s=Fresh(seed);var c=s.Selected!;c.Trust=trusting?90:5;c.Fullness=40;Source(s,RoomObjectKind.Leaves,1);
            s.SetInteractionHand(0,0,true);Tick(s,.25);
            if(c.Interaction.LastHelpAt>=0){if(trusting)high++;else low++;}
            Tick(s,10);Require(c.Fullness>40,"trust group failed to continue self care");
        }
    }
    Require(high>low+60&&low>0&&high<150,$"help tendency not probabilistic: low={low}, high={high}");
    Console.WriteLine($"paired help samples: low={low}/150, high={high}/150");
    observations["trust-help"]=new{seedGenerator=1009,samplesPerGroup=150,low,high,lowInterval95=Interval(low,150),highInterval95=Interval(high,150)};
});
Check("no nearby human means no automatic water begging for mobile oil",()=>
{
    var s=Fresh();var c=s.Selected!;c.Trust=90;c.Interaction.Hydration=20;Source(s,RoomObjectKind.RiverWater,1.2);
    Tick(s,5);Require(c.Interaction.LastHelpAt<0&&c.Interaction.Hydration>20,"mobile oil waited for absent hand");
});
Check("dirty intake is riskier than clean intake and is not guaranteed illness",()=>
{
    int dirty=0,clean=0;var rng=new Random(779);const int samples=400;
    for(int n=0;n<samples;n++)
    {
        ulong seed=(ulong)rng.NextInt64(1,long.MaxValue);
        foreach(bool polluted in new[]{false,true})
        {
            var s=Fresh(seed);s.Selected!.Fullness=30;Source(s,RoomObjectKind.Leaves,.4,contamination:polluted?1:0,portions:1);
            Tick(s,.75);Require(s.Selected.Interaction.Digestion.Exposures==1,"sample did not actually take one mouthful");
            if(s.Selected.Interaction.Digestion.IncubationRemaining>0){if(polluted)dirty++;else clean++;}
        }
    }
    Require(clean==0&&dirty>70&&dirty<200,$"unexpected incidence clean={clean}, dirty={dirty}");
    double p=(double)dirty/samples,se=Math.Sqrt(p*(1-p)/samples);
    Console.WriteLine($"dirty incidence {dirty}/{samples}; approx 95% interval [{p-1.96*se:F3},{p+1.96*se:F3}]; clean={clean}");
    observations["dirty-versus-clean"]=new{seedGenerator=779,samplesPerGroup=samples,dirty,clean,dirtyInterval95=Interval(dirty,samples),cleanInterval95=Interval(clean,samples)};
});
Check("illness appears after incubation and clean water does not instantly cure it",()=>
{
    var s=Fresh();var c=s.Selected!;c.Interaction.Digestion=new(){Exposures=1,Episodes=1,IncubationRemaining=2,Severity=40};
    Tick(s,1);Require(!c.Interaction.Digestion.Symptomatic&&c.Health==100,"symptoms began before incubation");
    Tick(s,2);Require(c.Interaction.Digestion.Symptomatic&&c.Interaction.Digestion.SymptomsRemaining>100,"onset failed");
    double remaining=c.Interaction.Digestion.SymptomsRemaining;c.Interaction.Hydration=10;Source(s,RoomObjectKind.Water,.4);
    double beforeSip=remaining;
    for(int n=0;n<100&&c.Interaction.Digestion.Exposures==1;n++)
    {beforeSip=c.Interaction.Digestion.SymptomsRemaining;Tick(s,.05);}
    Require(c.Interaction.Digestion.Exposures==2&&c.Interaction.Hydration>10,"sick creature did not actually drink clean water");
    Require(c.Interaction.Digestion.Symptomatic&&c.Interaction.Digestion.SymptomsRemaining>beforeSip-1,"clean water instantly cured or shortened disease");
});
Check("ordinary digestive illness is not transmitted between neighbors",()=>
{
    var s=WorldSimulation.CreateInteractionPlaytest();s.State.RoomObjects.Clear();
    var sick=s.Selected!;sick.Interaction.Digestion=new(){Exposures=1,Episodes=1,SymptomsRemaining=100,Severity=40};
    Tick(s,12);Require(s.State.Creatures.Where(c=>c.Id!=sick.Id).All(c=>c.Interaction.Digestion.Severity==0),"ordinary illness spread");
});
Check("close-up and pause freeze needs illness and source depletion",()=>
{
    var s=Fresh();var c=s.Selected!;c.Interaction.Digestion=new(){Exposures=1,Episodes=1,IncubationRemaining=10,Severity=40};
    c.Fullness=20;var leaf=Source(s,RoomObjectKind.Leaves,.4);
    s.State.Mode=PlayMode.CloseUp;Tick(s,10);Require(c.Fullness==20&&leaf.Portions==10&&c.Interaction.Digestion.IncubationRemaining==10,"close-up advanced life");
    s.State.Mode=PlayMode.Management;s.State.Settings.Pause=true;Tick(s,10);
    Require(c.Fullness==20&&leaf.Portions==10&&c.Interaction.Digestion.IncubationRemaining==10,"pause advanced life");
});
Check("save reload preserves incubation consumed source and random sequence",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=20;Source(s,RoomObjectKind.Leaves,.4,contamination:1);
    Tick(s,1);var loaded=Reload(s);string before=JsonSerializer.Serialize(c.Interaction.Digestion);
    Require(JsonSerializer.Serialize(loaded.Selected!.Interaction.Digestion)==before&&loaded.State.RandomState==s.State.RandomState,"saved exposure rerolled");
    Require(loaded.Selected.Interaction.GroundIntakeKind==RoomObjectKind.Leaves&&
        loaded.Selected.Interaction.GroundIntakeDistance==c.Interaction.GroundIntakeDistance&&
        loaded.Selected.Interaction.IntakeFacing==c.Interaction.IntakeFacing,"saved mouthful lost its source geometry");
    Tick(s,50);Tick(loaded,50);
    Require(Math.Abs(s.Selected!.Health-loaded.Selected!.Health)<.00001&&s.State.RandomState==loaded.State.RandomState,"reloaded future diverged");
});
Check("old save without natural fields gains empty memory and healthy defaults",()=>
{
    var s=Fresh();var json=JsonNode.Parse(JsonSerializer.Serialize(s.State,SaveStore.JsonOptions))!;
    var life=json["Creatures"]![0]!["Interaction"]!.AsObject();life.Remove("Resources");life.Remove("Digestion");
    life.Remove("GroundIntakeKind");life.Remove("GroundIntakeDistance");life.Remove("GroundIntakeSourceHeight");
    life.Remove("GroundIntakeSourceId");life.Remove("NaturalBitePause");life.Remove("NaturalIntakePending");
    var state=JsonSerializer.Deserialize<WorldState>(json.ToJsonString(),SaveStore.JsonOptions)!;SaveStore.Validate(state);
    Require(state.Creatures[0].Interaction.Resources.Count==0&&state.Creatures[0].Interaction.Digestion.Severity==0,"old defaults unsafe");
    Require(state.Creatures[0].Interaction.GroundIntakeKind==null&&state.Creatures[0].Interaction.GroundIntakeDistance==.792&&
        state.Creatures[0].Interaction.GroundIntakeSourceHeight==0&&state.Creatures[0].Interaction.GroundIntakeSourceId==null&&
        state.Creatures[0].Interaction.NaturalBitePause==0&&!state.Creatures[0].Interaction.NaturalIntakePending,"old mouthful geometry defaults unsafe");
});
Check("missing pending field resumes an already accepted mouthful without replaying its portion",()=>
{
    // Synthetic legacy shape, not an authorized historical-save fixture. Only
    // remove the flag after acceptance; a reserved modern bite is not old data.
    foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Water})
    foreach(bool omitGeometry in new[]{false,true})
    foreach(bool close in new[]{false,true})
    {
        var original=Fresh();var oil=original.Selected!;
        oil.Fullness=kind==RoomObjectKind.Meal?65:90;
        oil.Interaction.Hydration=kind==RoomObjectKind.Water?60:90;
        var source=Source(original,kind,.5,portions:1);
        Tick(original,.75);
        Require(!oil.Interaction.NaturalIntakePending&&oil.Interaction.EatingRemaining>0&&
            oil.Interaction.Digestion.Exposures==1&&source.Portions==0,"fixture did not accept exactly one unfinished mouthful");
        double fullness=oil.Fullness,hydration=oil.Interaction.Hydration;
        double remaining=oil.Interaction.EatingRemaining;
        var json=JsonNode.Parse(JsonSerializer.Serialize(original.State,SaveStore.JsonOptions))!;
        var life=json["Creatures"]![0]!["Interaction"]!.AsObject();
        life.Remove("NaturalIntakePending");
        if(omitGeometry)
            foreach(string field in new[]{"GroundIntakeKind","GroundIntakeDistance","GroundIntakeSourceHeight","GroundIntakeSourceId","NaturalBitePause"})life.Remove(field);
        var state=JsonSerializer.Deserialize<WorldState>(json.ToJsonString(),SaveStore.JsonOptions)!;
        SaveStore.Validate(state);var loaded=new WorldSimulation(state);var resumed=loaded.Selected!;
        loaded.State.Mode=close?PlayMode.CloseUp:PlayMode.Management;
        Require(!resumed.Interaction.NaturalIntakePending&&resumed.Interaction.EatingRemaining==remaining&&
            resumed.Fullness==fullness&&resumed.Interaction.Hydration==hydration&&resumed.Interaction.Digestion.Exposures==1,
            "loading changed accepted intake or turned it back into a reservation");
        for(int frame=0;frame<80;frame++)
        {
            loaded.AdvanceInteractionPresentation(.05);loaded.Advance(.05);
            Require(resumed.Fullness<=fullness&&resumed.Interaction.Hydration<=hydration&&
                resumed.Interaction.Digestion.Exposures==1&&
                loaded.State.RoomObjects.Where(o=>NaturalResources.IsResource(o.Kind)).Sum(o=>o.Portions)==0,
                "continuation granted nutrition, repeated exposure, or restored the swallowed portion");
        }
        Require(resumed.Interaction.EatingRemaining==0&&!resumed.Interaction.NaturalIntakePending,
            "accepted legacy mouthful did not finish");
        if(close)Require(resumed.Fullness==fullness&&resumed.Interaction.Hydration==hydration,
            "close-up continuation advanced passive needs");
        SaveStore.Validate(loaded.State);
    }
});
Check("save rejects invalid natural mouthful geometry",()=>
{
    foreach(var change in new Action<InteractionLife>[]
    {
        life=>life.GroundIntakeKind=RoomObjectKind.Cushion,life=>life.GroundIntakeDistance=double.NaN,
        life=>life.GroundIntakeDistance=-1,life=>life.GroundIntakeSourceHeight=double.PositiveInfinity,
        life=>life.GroundIntakeSourceId=" ",life=>life.NaturalBitePause=double.NaN,
        life=>life.NaturalIntakePending=true,
        life=>{life.NaturalIntakePending=true;life.GroundIntake=true;life.EatingRemaining=1;life.GroundIntakeSourceId="missing";life.GroundIntakeKind=RoomObjectKind.Leaves;}
    })
    {
        var s=Fresh();change(s.Selected!.Interaction);bool rejected=false;
        try{SaveStore.Validate(s.State);}catch(InvalidDataException){rejected=true;}
        Require(rejected,"invalid source geometry accepted");
    }
});
Check("self feeding completion does not invent a person holding a cup or spoon",()=>
{
    foreach(var kind in new[]{RoomObjectKind.Leaves,RoomObjectKind.Meal,RoomObjectKind.RiverWater,RoomObjectKind.Water})
    {
        var s=Fresh();var c=s.Selected!;
        c.Fullness=NaturalResources.IsFood(kind)?65:90;c.Interaction.Hydration=NaturalResources.IsWater(kind)?60:90;
        Source(s,kind,.4,portions:1);Tick(s,2);
        Require(c.Interaction.ReactionSituation=="mouthful-done","natural mouthful did not finish");
        Require(!c.LastReaction.Contains("收回")&&!c.LastReaction.Contains("手")&&!c.LastReaction.Contains("空勺"),"natural feeding requested phantom human action");
    }
});
// Reach completion through real intake, with no outstanding gesture or idle walk.
// Do not manufacture a swallow reaction: the presentation/world boundary is the subject.
WorldSimulation FinishedMouthful(bool handFed=false,bool water=false)
{
    var s=Fresh();var c=s.Selected!;c.DecisionIn=10000;c.Trust=70;c.Fear=0;
    c.Interaction.ToyInterest=0;c.Fullness=water?90:60;c.Interaction.Hydration=water?60:90;
    s.State.UnlimitedResources=false;s.State.Food=2;
    if(handFed)
    {
        var input=new InteractionInput(water?InteractionAction.Drink:InteractionAction.FeedMeal,BodyPart.Mouth,.2,.05);
        Require(s.BeginInteractionGesture(c.Id,input).Success,"could not begin the offered mouthful");
        for(int n=0;n<30&&c.Interaction.EatingRemaining<=0;n++)
        {s.ContinueInteractionGesture(input,true);Tick(s,.05);}
        Require(c.Interaction.EatingRemaining>0,"the offered mouthful never entered");
        s.EndInteractionGesture();
    }
    else Source(s,water?RoomObjectKind.Water:RoomObjectKind.Meal,0,.5,portions:1);
    for(int n=0;n<100&&c.Interaction.ReactionSituation!="mouthful-done";n++)Tick(s,.05);
    Require(!s.HasInteractionGesture&&c.Interaction.EatingRemaining==0&&InteractionPresentation.Pose(c)=="swallow"&&
        c.X==0&&c.Z==0,"fixture did not finish a real mouthful while planted and released");
    return s;
}
var swallowHandoffs=new List<object>();
(bool Moved,bool Handoff) SwallowAutonomyFrame(WorldSimulation s)
{
    var c=s.Selected!;s.AdvanceInteractionPresentation(.05);
    string beforePose=InteractionPresentation.Pose(c),speech=c.Speech;
    double x=c.X,z=c.Z,remaining=c.Interaction.ReactionPoseRemaining,world=s.State.WorldSeconds;
    double fullness=c.Fullness,water=c.Interaction.Hydration,trust=c.Trust;
    int serial=c.ReactionSerial,stock=s.State.Food,exposures=c.Interaction.Digestion.Exposures;
    s.Advance(.05);
    Require(!s.HasInteractionGesture,"a supposedly released mouthful retained a gesture");
    bool moved=c.X!=x||c.Z!=z,handoff=moved&&beforePose=="swallow";
    if(handoff)
    {
        string afterPose=InteractionPresentation.Pose(c);
        swallowHandoffs.Add(new{worldBefore=world,worldAfter=s.State.WorldSeconds,xBefore=x,zBefore=z,c.X,c.Z,
            remainingBefore=remaining,beforePose,afterPose,c.Interaction.NaturalBitePause,c.Activity});
        Require(afterPose!="swallow"&&c.Interaction.ReactionPoseRemaining==0,
            "actual autonomous travel retained the finished mouthful's planted pose");
        Require(c.Speech==speech&&c.ReactionSerial==serial&&c.Trust==trust&&s.State.Food==stock&&
            c.Interaction.Digestion.Exposures==exposures&&c.Fullness<=fullness&&c.Interaction.Hydration<=water,
            "pose handoff replayed a reaction, intake, or reward");
    }
    return(moved,handoff);
}
Check("released full mouthfuls hand swallowing back to following or ball approach on the actual movement frame",()=>
{
    foreach(bool handFed in new[]{false,true})foreach(bool water in new[]{false,true})foreach(bool follow in new[]{false,true})
    {
        var s=FinishedMouthful(handFed,water);var c=s.Selected!;
        if(follow)c.Interaction.FollowPlayer=true;
        else {c.Interaction.ToyInterest=1;Source(s,RoomObjectKind.Ball,2.5);}
        bool handedOff=false;
        for(int n=0;n<12&&!handedOff;n++)
        {
            if(follow)s.SetInteractionHand(2.5,0,true);
            handedOff=SwallowAutonomyFrame(s).Handoff;
        }
        Require(handedOff,$"no real swallow/movement boundary observed: handFed={handFed}, water={water}, follow={follow}");
    }
});
Check("seeking the other urgent resource releases swallowing across different presentation phases",()=>
{
    int handoffs=0;
    for(int closeFrames=0;closeFrames<=4;closeFrames++)
    {
        var s=FinishedMouthful();var c=s.Selected!;c.Interaction.Hydration=10;
        Source(s,RoomObjectKind.Water,2.5);c.Interaction.NextResourceScanAt=0;
        // A short real close-up shifts presentation relative to the frozen world
        // accumulator without assigning either timer or inventing a reaction.
        s.State.Mode=PlayMode.CloseUp;
        for(int n=0;n<closeFrames;n++)Tick(s,.05);
        s.State.Mode=PlayMode.Management;
        bool moved=false;
        for(int n=0;n<20;n++)
        {
            var frame=SwallowAutonomyFrame(s);moved|=frame.Moved;
            if(frame.Handoff)handoffs++;
        }
        Require(moved&&c.Interaction.ResourceTargetId!=null,"urgent water search stalled after the mouthful");
    }
    Require(handoffs>0,"phase cases never exercised movement before the swallow tail expired");
});
Check("close-up pause and a clamped movement request preserve a planted swallow",()=>
{
    var s=FinishedMouthful();var c=s.Selected!;s.State.Mode=PlayMode.CloseUp;s.State.Settings.Pause=true;
    double remaining=c.Interaction.ReactionPoseRemaining,world=s.State.WorldSeconds,fullness=c.Fullness,water=c.Interaction.Hydration;
    Tick(s,.2);Require(c.Interaction.ReactionPoseRemaining==remaining,"pause advanced swallowing");
    s.State.Settings.Pause=false;Tick(s,.2);
    Require(InteractionPresentation.Pose(c)=="swallow"&&s.State.WorldSeconds==world&&c.Fullness==fullness&&
        c.Interaction.Hydration==water&&c.X==0&&c.Z==0,"close-up advanced passive life or erased a planted swallow");
    s.State.Mode=PlayMode.Management;
    c.X=8-WorldSimulation.InteractionFootprint(c);c.TargetX=8;c.TargetZ=c.Z;
    double edge=c.X;Tick(s,.25);
    Require(s.State.WorldSeconds>world&&c.X==edge&&c.Z==0&&InteractionPresentation.Pose(c)=="swallow",
        "requesting movement beyond the yard edge erased swallowing without any actual travel");
});
Check("a newer painful response survives autonomous movement after swallowing",()=>
{
    var s=FinishedMouthful();var c=s.Selected!;
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Flick,BodyPart.Head,Force:.7)).Success,"pain fixture was rejected");
    int serial=c.ReactionSerial;string speech=c.Speech;
    Require(c.Interaction.ReactionSituation=="hurt"&&c.Interaction.ReactionPose=="flick","no newer pain response");
    c.Interaction.FollowPlayer=true;bool moved=false;
    for(int n=0;n<10&&!moved;n++)
    {
        s.SetInteractionHand(2.5,0,true);
        // The frame helper measures world movement after presentation impulses,
        // so a recoil alone cannot satisfy this coverage condition.
        moved=SwallowAutonomyFrame(s).Moved;
        Require(c.Interaction.ReactionSituation=="hurt"&&InteractionPresentation.Pose(c)=="flick"&&
            c.ReactionSerial==serial&&c.Speech==speech,"movement cleared or replaced newer pain feedback");
    }
    Require(moved,"new pain case never exercised actual autonomous movement");
});
observations["released-swallow-handoffs"]=swallowHandoffs;
Check("save rejects impossible illness and contaminated resource data",()=>
{
    var s=Fresh();var food=Source(s,RoomObjectKind.Leaves,.4);food.Contamination=double.NaN;
    bool rejected=false;try{SaveStore.Validate(s.State);}catch(InvalidDataException){rejected=true;}Require(rejected,"invalid resource accepted");
    food.Contamination=0;s.Selected!.Interaction.Digestion=new(){Exposures=1,Episodes=1,IncubationRemaining=5,SymptomsRemaining=10,Severity=40};
    rejected=false;try{SaveStore.Validate(s.State);}catch(InvalidDataException){rejected=true;}Require(rejected,"overlapping phases accepted");
});
Check("interrupting touch then releasing returns to the original life need",()=>
{
    var s=Fresh();var c=s.Selected!;c.Trust=60;c.Fullness=25;Source(s,RoomObjectKind.Leaves,2.5);
    Tick(s,.5);s.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));Tick(s,2);s.EndInteractionGesture();Tick(s,15);
    Require(c.Fullness>50&&!s.HasInteractionGesture,"creature remained a waiting toy");
});
Check("held supply is reserved and cannot be eaten from its old ground location",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=20;var meal=Source(s,RoomObjectKind.Meal,.4);
    Require(s.HoldSupply(meal.Id),"could not pick up supply");Tick(s,2);
    Require(meal.Portions==10&&c.Fullness<20,"autonomy consumed held supply");
    Require(s.ReleaseSupply(meal.Id),"could not cancel hold");Tick(s,10);
    Require(meal.Portions<10,"released supply stayed reserved");
});
Check("feeding held supply consumes that object rather than inventory a second time",()=>
{
    var s=Fresh();s.State.UnlimitedResources=false;s.State.Food=0;var c=s.Selected!;c.Fullness=30;
    var meal=Source(s,RoomObjectKind.Meal,.4,portions:1);s.HoldSupply(meal.Id);
    var input=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,.2,.1,ObjectId:meal.Id);
    s.BeginInteractionGesture(c.Id,input);for(int n=0;n<30;n++)s.ContinueInteractionGesture(input,true);
    Require(meal.Portions==0&&s.State.Food==0&&c.Fullness==49,"object and inventory were double charged or multiple bites consumed");
    s.EndInteractionGesture();s.ReleaseSupply(meal.Id);Require(!s.State.RoomObjects.Contains(meal),"empty supply stayed reusable");
});
Check("changing offered supply midway cannot reuse the first mouth dwell",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;var a=Source(s,RoomObjectKind.Meal,.4);var b=Source(s,RoomObjectKind.Meal,-.4);
    s.HoldSupply(a.Id);var input=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,.2,.1,ObjectId:a.Id);
    s.BeginInteractionGesture(c.Id,input);for(int n=0;n<5;n++)s.ContinueInteractionGesture(input,true);
    var reply=s.ContinueInteractionGesture(input with{ObjectId=b.Id},true);
    Require(!reply.Success&&a.Portions==10&&b.Portions==10&&!s.HasInteractionGesture,"supply switch reused dwell");
});
Check("cancelled supply stays at origin and reload releases the transient reservation",()=>
{
    var s=Fresh();var obj=Source(s,RoomObjectKind.Water,.5,1);s.HoldSupply(obj.Id);
    var loaded=Reload(s);Require(!loaded.State.RoomObjects[0].HeldByHand&&loaded.State.RoomObjects[0].X==.5,"save preserved phantom hand");
    Require(s.ReleaseSupply(obj.Id)&&obj.X==.5&&obj.Z==1&&obj.Portions==10,"cancel lost or moved portions");
});
Check("water placement remains distinct from food inventory and can be used independently",()=>
{
    var s=Fresh();s.State.UnlimitedResources=false;s.State.Food=0;var c=s.Selected!;c.Interaction.Hydration=20;
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.PlaceWater,X:.5,Z:0)).Success,"no food prevented placing clean water");
    Tick(s,1);Require(c.Interaction.Hydration>40&&s.State.Food==0,"placed water failed or used food");
});
Check("low trust alone can refuse hand feeding without disabling self feeding",()=>
{
    var s=Fresh();var c=s.Selected!;c.Trust=5;c.Fullness=30;
    var input=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,.2,.1);
    s.BeginInteractionGesture(c.Id,input);InteractionFeedback reply=new(false,"","","");
    for(int n=0;n<10;n++)reply=s.ContinueInteractionGesture(input,true);
    Require(reply.Refused&&c.Fullness==30,"unfamiliar hand was automatically trusted");
    s.EndInteractionGesture();Source(s,RoomObjectKind.Meal,.4);Tick(s,2);Require(c.Fullness>30,"refusal disabled self eating");
});
Check("natural drinking access fits the actual dry bank and never heals immersion damage",()=>
{
    var s=Fresh();NaturalResources.AddWoodland(s.State,"home");var c=s.Selected!;c.X=5;c.Z=WoodlandLayout.BankZ;c.Interaction.Hydration=10;
    c.Interaction.FootIntegrity=70;Tick(s,15);
    Require(c.Interaction.Hydration>60&&WoodlandLayout.GroundFits(s.State,"home",c.X,c.Z,WorldSimulation.InteractionFootprint(c)),"drinking required entering water");
    Require(c.Interaction.FootIntegrity==70,"drinking healed damaged feet");
    Require(!s.CanPlaceInteractionAt(c.Id,7,2),"river falsely treated as dry placement ground");
});
Check("young adult and elder can independently reach the river from dry ground",()=>
{
    var rows=new List<object>();
    foreach(var age in new[]{"young","adult","elder"})
    {
        var s=Fresh();NaturalResources.AddWoodland(s.State,"home");var c=s.Selected!;
        c.IsYoung=age=="young";c.IsElder=age=="elder";
        c.X=5;c.Z=WoodlandLayout.BankZ;c.Interaction.Hydration=10;
        Tick(s,20);
        rows.Add(new{age,hydration=c.Interaction.Hydration,c.X,c.Z,footprint=WorldSimulation.InteractionFootprint(c)});
        observations["river_access_by_age"]=rows;
        Require(c.Interaction.Hydration>60,$"{age} cannot drink: hydration={c.Interaction.Hydration:F2}, x={c.X:F3}");
        Require(WoodlandLayout.GroundFits(s.State,"home",c.X,c.Z,WorldSimulation.InteractionFootprint(c)),age+" entered the river");
    }
});
Check("curved river access works from either direction for all body sizes",()=>
{
    int cases=0;
    foreach(var age in new[]{"young","adult","elder"})
    foreach(double bankZ in new[]{-5.0,0,5})
    foreach(double side in new[]{-1.5,1.5})
    {
        var s=Fresh();var c=s.Selected!;c.IsYoung=age=="young";c.IsElder=age=="elder";
        double bankX=WoodlandLayout.RiverEdge(bankZ);
        Source(s,RoomObjectKind.RiverWater,bankX,bankZ,portions:1);
        c.X=bankX-2;c.Z=bankZ+side;c.Interaction.Hydration=10;
        for(int n=0;n<400&&c.Interaction.Hydration<30;n++)Tick(s,.05);
        Require(c.Interaction.Hydration>30,$"{age} cannot approach bank at z={bankZ} from {side}");
        Require(WoodlandLayout.GroundFits(s.State,"home",c.X,c.Z,WorldSimulation.InteractionFootprint(c)),"drank inside water");
        cases++;
    }
    observations["curved_bank_access_cases"]=cases;
});
Check("unreachable remembered resource receives retry delay instead of endless pushing",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;var leaf=Source(s,RoomObjectKind.Leaves,8.8);
    c.Interaction.Resources[leaf.Id]=new(){RegionId="home",Kind=leaf.Kind,X=leaf.X,Z=leaf.Z,Available=true};
    Tick(s,22);Require(c.Interaction.Resources[leaf.Id].AvoidUntil>s.State.WorldSeconds,"blocked goal never gave way");
});
Check("urgent hunger interrupts rest without granting sleep recovery while walking",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=10;c.Energy=10;c.Interaction.RestRemaining=30;
    Source(s,RoomObjectKind.Cushion,0);Source(s,RoomObjectKind.Leaves,2);
    Tick(s,1);Require(!c.Interaction.Resting&&c.X>0&&c.Energy<=10,"walking creature kept sleeping recovery");
});
Check("an inaccessible cushion gives way to ground rest without its healing bonus",()=>
{
    var s=Fresh();var c=s.Selected!;c.Energy=10;Source(s,RoomObjectKind.Cushion,8.8);
    Tick(s,45);Require(c.Energy>18&&c.Interaction.RestOnGroundUntil>0,"tired creature never stopped seeking unreachable bed");
    SaveStore.Validate(s.State);
});
Check("carrying interrupts cushion progress in ordinary time and close-up, including after reload",()=>
{
    foreach(bool closeUp in new[]{false,true})foreach(bool reload in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;c.Energy=10;
        var bed=Source(s,RoomObjectKind.Cushion,3);Tick(s,1);
        Require(c.Interaction.RestTargetId==bed.Id&&c.X>0,"initial cushion approach never began");
        double worldTime=s.State.WorldSeconds;
        c.Interaction.Carried=true;c.Interaction.Height=1;c.X=-4;c.TargetX=-4;
        if(closeUp)s.State.Mode=PlayMode.CloseUp;
        Tick(s,.25);
        if(closeUp)Require(s.State.WorldSeconds==worldTime,"close-up advanced passive rest time");
        c.Interaction.Carried=false;c.Interaction.Height=0;s.State.Mode=PlayMode.Management;
        if(reload){s=Reload(s);c=s.Selected!;}
        Tick(s,4);
        Require(c.Interaction.RestOnGroundUntil==0&&c.X>-1.2&&!c.Interaction.Resting,
            "normal return from a new location was mistaken for a blocked cushion");
        Tick(s,8);
        Require(c.Interaction.Resting&&Math.Abs(c.X-bed.X)<=.65&&c.Energy>10,
            "interrupted rest never reached the cushion and resumed recovery");
    }
});
Check("urgent food and water detours resume the cushion route without reusing its old best distance",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;c.Energy=10;
        var bed=Source(s,RoomObjectKind.Cushion,3);Tick(s,1);
        Require(c.Interaction.RestTargetId==bed.Id&&c.X>0,"initial cushion approach never began");
        if(water)c.Interaction.Hydration=10;else c.Fullness=10;
        Source(s,water?RoomObjectKind.Water:RoomObjectKind.Meal,-2.7,portions:1);
        c.Interaction.NextResourceScanAt=0;
        bool resumed=false;
        for(int frame=0;frame<240;frame++)
        {
            Tick(s,.05);
            if(c.Interaction.Digestion.Exposures==1&&c.Interaction.EatingRemaining<=0&&c.Interaction.RestTargetId==bed.Id)
            {resumed=true;break;}
        }
        Require(resumed&&c.X<-1.5,"urgent need did not actually divert and release the rest route");
        double startX=c.X;Tick(s,4);
        Require(c.Interaction.RestOnGroundUntil==0&&c.X>startX+2.5,
            "an urgent detour left old cushion progress that rejected the normal return");
        Tick(s,6);
        Require(c.Interaction.Resting&&Math.Abs(c.X-bed.X)<=.65&&c.Energy>10,
            "satisfied urgent need did not return to restorative cushion rest");
    }
});
Check("natural rest continues past the tired threshold instead of flickering awake",()=>
{
    var s=Fresh();var c=s.Selected!;c.Energy=21.9;Source(s,RoomObjectKind.Cushion,0);
    Tick(s,8);Require(c.Interaction.Resting&&c.Energy>26&&c.Interaction.RestRemaining>15,"rest stopped at its entry threshold");
});
Check("a sick well-fed creature can choose restorative rest without a command",()=>
{
    var s=Fresh();var c=s.Selected!;c.Energy=45;
    c.Interaction.Digestion=new(){Exposures=1,Episodes=1,Severity=40,SymptomsRemaining=180};
    Tick(s,10);Require(c.Interaction.Resting&&c.Energy>45&&c.Interaction.Digestion.SymptomsRemaining<167,"illness did not change rest choice");
});
Check("a started mouthful finishes before walking back to a cushion",()=>
{
    var s=Fresh();var c=s.Selected!;c.Energy=10;c.Interaction.RestRemaining=30;
    Source(s,RoomObjectKind.Cushion,4);c.Interaction.BeginIntake(IntakeKind.Meal);
    Tick(s,.5);Require(c.X==0&&!c.Interaction.Resting&&c.Energy<=10,"chewing creature walked or gained sleep recovery");
});
Check("production woodland tree trunks block bodies and held supply placement",()=>
{
    var s=Fresh();NaturalResources.AddWoodland(s.State,"home");var c=s.Selected!;var tree=WoodlandLayout.Trees[0];
    Require(!s.CanPlaceInteractionAt(c.Id,tree.X,tree.Z),"body can be put inside visible trunk");
    var food=Source(s,RoomObjectKind.Meal,.4);s.HoldSupply(food.Id);
    Require(!s.ReleaseSupply(food.Id,tree.X,tree.Z)&&food.HeldByHand,"supply entered visible trunk");
    s.ReleaseSupply(food.Id);
});
Check("the full curved bank footprint stays on dry ground",()=>
{
    var s=Fresh();NaturalResources.AddWoodland(s.State,"home");double radius=.7;
    for(double z=-6;z<6;z+=.13)
    {
        double x=WoodlandLayout.RiverEdge(z)-radius*1.006;
        Require(WoodlandLayout.GroundFits(s.State,"home",x,z,radius),"safe bank position rejected");
        for(int a=0;a<64;a++)
        {double angle=a*Math.Tau/64;Require(x+radius*Math.Cos(angle)<=WoodlandLayout.RiverEdge(z+radius*Math.Sin(angle)),"body edge crossed river");}
    }
});
Check("symptoms associate recent meals without identifying the hidden true cause",()=>
{
    var s=Fresh();var c=s.Selected!;var a=Source(s,RoomObjectKind.Leaves,1,contamination:1);var b=Source(s,RoomObjectKind.Water,-1);
    Tick(s,.25);
    c.Interaction.Digestion=new(){Exposures=2,Episodes=1,Severity=40,IncubationRemaining=.25,SuspectedSourceId=a.Id,
        RecentIntakes=[new(){SourceId=a.Id,At=0},new(){SourceId=b.Id,At=0}]};
    Tick(s,.25);
    Require(c.Interaction.Resources[a.Id].Suspicion==12.5&&c.Interaction.Resources[b.Id].Suspicion==12.5,"hidden infection source became certain knowledge");
    Require(c.Interaction.Digestion.SuspectedSourceId==null,"multiple meals were reduced to a certain single cause");
    Require(a.Contamination==1&&b.Contamination==0,"learning changed actual source risk");
});
Check("illness without recent remembered intake does not invent source knowledge",()=>
{
    var s=Fresh();var c=s.Selected!;var a=Source(s,RoomObjectKind.Leaves,1);Tick(s,.25);
    c.Interaction.Digestion=new(){Exposures=1,Episodes=1,Severity=40,IncubationRemaining=.25,SuspectedSourceId=a.Id};
    Tick(s,.25);Require(c.Interaction.Resources[a.Id].Suspicion==0,"legacy hidden source taught unsupported knowledge");
});
Check("sick oil pauses between food portions while water remains available",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;
    c.Interaction.Digestion=new(){Exposures=1,Episodes=1,Severity=40,SymptomsRemaining=180};
    var food=Source(s,RoomObjectKind.Leaves,.4);Tick(s,3);
    Require(food.Portions==9&&c.Fullness>30,"illness did not slow repeated eating");
    c.Interaction.Hydration=10;Source(s,RoomObjectKind.Water,.5);Tick(s,1);
    Require(c.Interaction.Hydration>30,"food pause also blocked drinking");
    Tick(s,7);Require(food.Portions<9,"appetite pause became permanent food refusal");
});
Check("urgent need can accept matching aid despite low trust and prior food suspicion",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;c.Trust=0;c.Interaction.FoodSuspicion=100;
        if(water)c.Interaction.Hydration=5;else c.Fullness=5;
        var input=new InteractionInput(water?InteractionAction.Drink:InteractionAction.FeedMeal,BodyPart.Mouth,.2,.1);
        s.BeginInteractionGesture(c.Id,input);for(int n=0;n<9;n++)s.ContinueInteractionGesture(input,true);
        Require((water?c.Interaction.Hydration:c.Fullness)>15,"trust permanently prevented emergency aid");
    }
});
Check("sick but satisfied oil can refuse extra food without refusing needed water",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=70;c.Interaction.Hydration=10;c.Interaction.Digestion=new(){Exposures=1,Episodes=1,Severity=40,SymptomsRemaining=180};
    var food=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,.2,.1);
    s.BeginInteractionGesture(c.Id,food);var reply=s.ContinueInteractionGesture(food,true);
    Require(reply.Refused&&c.Fullness==70,"illness appetite was ignored by hand feeding");s.EndInteractionGesture();
    var water=food with{Action=InteractionAction.Drink};s.BeginInteractionGesture(c.Id,water);
    for(int n=0;n<9;n++)s.ContinueInteractionGesture(water,true);
    Require(c.Interaction.Hydration>30,"food refusal poisoned a later water gesture");
});
Check("digestive care improves later recovery without instant cure or repeat rewards",()=>
{
    var cared=Fresh();var c=cared.Selected!;c.Interaction.Digestion=new(){Exposures=1,Episodes=1,Severity=40,SymptomsRemaining=180};
    var control=Reload(cared);double trust=c.Trust;
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Side,.2,.1);
    cared.BeginInteractionGesture(c.Id,input);for(int n=0;n<18;n++)cared.ContinueInteractionGesture(input,true);cared.EndInteractionGesture();
    Require(c.Interaction.Digestion.CareRemaining==60&&c.Interaction.Digestion.SymptomsRemaining==180,"care instantly cured or failed to support");
    double after=c.Trust;Require(after>trust,"completed care left no relationship experience");
    Require(!cared.BeginInteractionGesture(c.Id,input).Success&&c.Trust==after,"already treated digestion farmed another reward");
    Tick(cared,10);Tick(control,10);
    Require(c.Interaction.Digestion.SymptomsRemaining<control.Selected!.Interaction.Digestion.SymptomsRemaining-4,"care did not improve recovery over time");
});
Check("three dirty portions raise cumulative illness incidence compared with one",()=>
{
    int single=0,repeated=0,samples=400;var random=new Random(292107);
    for(int n=0;n<samples;n++)
    {
        ulong seed=(ulong)random.NextInt64(1,long.MaxValue);
        foreach(int portions in new[]{1,3})
        {
            var s=Fresh(seed);s.Selected!.Fullness=5;Source(s,RoomObjectKind.Leaves,.4,contamination:1,portions:portions);Tick(s,8);
            Require(s.Selected.Interaction.Digestion.Exposures==portions,"dose comparison did not consume its stated portions");
            if(s.Selected.Interaction.Digestion.Episodes>0){if(portions==1)single++;else repeated++;}
        }
    }
    Console.WriteLine($"dose audit: one={single}/{samples}, three={repeated}/{samples}");
    observations["repeated-dose"]=new{seedGenerator=292107,samplesPerGroup=samples,single,repeated,singleInterval95=Interval(single,samples),repeatedInterval95=Interval(repeated,samples)};
    Require(repeated>single+60&&repeated<360,"cumulative dose failed to increase risk or became nearly guaranteed");
});
Check("appetite pause and recent intake history survive reload and freeze in close-up",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;c.Interaction.Digestion=new(){Exposures=1,Episodes=1,Severity=40,SymptomsRemaining=180};
    Source(s,RoomObjectKind.Leaves,.4);Tick(s,.75);var loaded=Reload(s);
    var h=loaded.Selected!.Interaction.Digestion;Require(h.NextFoodAt>0&&h.RecentIntakes.Count==1,"reload lost appetite or observed intake");
    double time=loaded.State.WorldSeconds,timer=h.NextFoodAt;loaded.State.Mode=PlayMode.CloseUp;Tick(loaded,8);
    Require(loaded.State.WorldSeconds==time&&h.NextFoodAt==timer&&h.RecentIntakes.Count==1,"close-up advanced passive appetite or replayed intake");
    loaded.State.Mode=PlayMode.Management;Tick(loaded,8);Require(h.Exposures>2,"reloaded appetite never resumed");
});
Check("injured foot help does not replace an active intake preparation",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;c.Interaction.FootIntegrity=20;c.Interaction.LastHelpAt=-100;
    var meal=Source(s,RoomObjectKind.Meal,0,.4,portions:1);
    for(int frame=0;frame<10&&!c.Interaction.NaturalIntakePending;frame++)Tick(s,.05);
    Require(c.Interaction.NaturalIntakePending,"accessible food never entered preparation");
    Require(InteractionPresentation.Pose(c)=="eat","failed-step help replaced the mouth preparation");
    Tick(s,.5);Require(c.Interaction.Digestion.Exposures==1&&meal.Portions==0,"accessible prepared portion did not enter once");
});
Check("cushion edge restraint preserves unreachable food and exposes no phantom meal",()=>
{
    var s=Fresh();var c=s.Selected!;c.IsElder=true;c.Fullness=30;c.Z=.9;c.Interaction.Restraint=RestraintKind.Wrap;
    c.Interaction.Dress(BodyPart.Bottom,SurfaceSide.Whole,100);s.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Cushion});
    var food=Source(s,RoomObjectKind.Sweet,.412,.9,portions:1);Tick(s,20);
    Require(c.Interaction.Digestion.Exposures==0&&food.Portions==1&&c.Fullness<30,"restrained body consumed an unreachable portion");
    Require(Math.Abs(c.X)<.00001&&Math.Abs(c.Z-.9)<.00001,"unable body was relocated to make the test pass");
});
Check("a chosen alternate standing point survives reload and enables real intake",()=>
{
    var s=Fresh();var c=s.Selected!;c.Interaction.Hydration=30;s.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Cushion});
    var water=Source(s,RoomObjectKind.Water,0,.792,portions:1);
    for(int frame=0;frame<10&&Math.Abs(c.X)+Math.Abs(c.Z)<.001;frame++)Tick(s,.05);
    Require(!c.Interaction.NaturalIntakePending&&Math.Abs(c.X)+Math.Abs(c.Z)>.001,"body did not start repositioning from an unreachable cushion pose");
    string id=water.Id;var loaded=Reload(s);Tick(loaded,6);var oil=loaded.Selected!;
    Require(oil.Interaction.Digestion.Exposures==1&&oil.Interaction.Hydration>50,"reloaded oil did not finish moving and drinking");
    Require(!loaded.State.RoomObjects.Any(o=>o.Id==id&&o.Portions>0),"taken water stayed available");
});
Check("a recurring restraint help request waits until an active mouthful finishes",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=30;c.Interaction.Restraint=RestraintKind.Box;
    Source(s,RoomObjectKind.Meal,0,.4,portions:1);
    for(int frame=0;frame<10&&!c.Interaction.NaturalIntakePending;frame++)Tick(s,.05);
    Require(c.Interaction.NaturalIntakePending,"box food did not begin");
    c.Interaction.LastHelpAt=s.State.WorldSeconds-24;Tick(s,.25);
    Require(InteractionPresentation.Pose(c)=="eat"&&c.Interaction.NaturalIntakePending,"routine help replaced the in-progress taking motion");
});
Check("a new digestive reaction cancels only a portion that has not entered yet",()=>
{
    foreach(bool entered in new[]{false,true})
    {
        var s=Fresh();var c=s.Selected!;c.Fullness=30;var food=Source(s,RoomObjectKind.Meal,0,.4,portions:1);
        for(int frame=0;frame<20&&(entered?c.Interaction.Digestion.Exposures==0:!c.Interaction.NaturalIntakePending);frame++)Tick(s,.05);
        double fullness=c.Fullness;int exposures=c.Interaction.Digestion.Exposures;
        c.Interaction.Digestion.Severity=40;c.Interaction.Digestion.IncubationRemaining=.1;Tick(s,.25);
        Require(!c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeSourceId==null,"new body reaction left a source-bound taking pose active");
        Require(c.Interaction.Digestion.Exposures==exposures&&c.Fullness<=fullness,"discomfort granted another bite");
        Require(food.Portions==(entered?0:1),"discomfort refunded eaten food or lost an uneaten portion");
        if(!entered)Require(c.Interaction.EatingRemaining==0,"cancelled preparation kept swallowing empty air");
        else Require(c.Interaction.EatingRemaining>0,"already eaten portion stopped swallowing");
    }
});
Check("hand awareness belongs to the region where the hand was observed",()=>
{
    var s=Fresh();var c=s.Selected!;c.Trust=90;c.Fullness=40;s.SetInteractionHand(0,0,true);
    s.State.Regions.Add(new(){Id="wild",Name="野地",Kind=RegionKind.Wilderness});
    c.RegionId="wild";s.State.ActiveRegionId="wild";Tick(s,.25);
    Require(c.Interaction.LastHelpDecisionAt<0,"a remembered hand appeared in another region");
    s.SetInteractionHand(0,0,true);Tick(s,.25);
    Require(c.Interaction.LastHelpDecisionAt>=0,"actually engaging the new region failed to make the hand visible");
    var loaded=Reload(s);Require(!loaded.State.InteractionHandPresent,"save restored a transient human hand");
});
Check("solid boxes hide an active hand without disabling a visible hand nearby",()=>
{
    foreach(bool blocked in new[]{true,false})
    {
        var s=Fresh();var c=s.Selected!;c.Fullness=40;c.Trust=90;
        var box=WorldSimulation.CreateInteractionPlaytest().State.Creatures[1];box.X=1.5;box.Z=0;box.Interaction.Restraint=RestraintKind.Box;s.State.Creatures.Add(box);
        s.SetInteractionHand(blocked?3:0,blocked?0:2,true);Tick(s,.25);
        Require((c.Interaction.LastHelpDecisionAt<0)==blocked,"hand perception ignored the actual box occlusion");
    }
});
Check("woodland trunks occlude food and hand perception at ground level",()=>
{
    foreach(bool blocked in new[]{true,false})
    {
        var s=Fresh();var c=s.Selected!;var tree=WoodlandLayout.Trees[2];
        c.X=tree.X-1;c.Z=tree.Z;c.TargetX=c.X;c.TargetZ=c.Z;c.Fullness=40;c.Trust=90;
        NaturalResources.AddWoodland(s.State,c.RegionId);
        var food=Source(s,RoomObjectKind.Meal,blocked?tree.X+1:c.X,blocked?tree.Z:c.Z+2);
        s.SetInteractionHand(food.X,food.Z,true);Tick(s,.25);
        Require(c.Interaction.Resources.ContainsKey(food.Id)!=blocked,"food observation ignored trunk occlusion");
        Require((c.Interaction.LastHelpDecisionAt<0)==blocked,"hand observation ignored trunk occlusion");
    }
});
Check("a boxed creature can see beyond its front opening without eating distant supplies",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fullness=40;c.Interaction.Restraint=RestraintKind.Box;
    var front=Source(s,RoomObjectKind.Meal,0,2);var back=Source(s,RoomObjectKind.Meal,0,-2);
    Tick(s,.25);Require(c.Interaction.Resources.ContainsKey(front.Id),"short mouth reach incorrectly hid the visible front supply");
    Require(!c.Interaction.Resources.ContainsKey(back.Id),"the back wall was transparent to food perception");
    Require(c.Interaction.Digestion.Exposures==0&&front.Portions==10,"seeing beyond the bars granted distant intake");
});
Check("a hidden remembered source is not marked missing until its old location is visible",()=>
{
    var s=Fresh();var c=s.Selected!;var food=Source(s,RoomObjectKind.Meal,0,-.9);Tick(s,.25);
    Require(c.Interaction.Resources.ContainsKey(food.Id),"initial observation missing");
    c.Interaction.Restraint=RestraintKind.Box;s.State.RoomObjects.Remove(food);c.Interaction.NextResourceScanAt=0;Tick(s,.25);
    Require(c.Interaction.Resources[food.Id].Available,"oil learned that an occluded source was missing");
    s.ExecuteInteraction(c.Id,new(InteractionAction.OpenBox));c.Interaction.NextResourceScanAt=0;Tick(s,.25);
    Require(!c.Interaction.Resources[food.Id].Available,"visible empty location was never checked after opening");
});
Check("trust changes first food selection near an engaged hand while absent hands do not impose avoidance",()=>
{
    const int samples=80;int lowWild=0,highWild=0,absentWild=0;var rng=new Random(291044);
    for(int n=0;n<samples;n++)
    {
        ulong seed=(ulong)rng.NextInt64(1,long.MaxValue);
        foreach(string group in new[]{"low","high","absent"})
        {
            var s=Fresh(seed);var c=s.Selected!;c.Trust=group=="high"?90:5;c.Fullness=40;
            var meal=Source(s,RoomObjectKind.Meal,.7);var leaf=Source(s,RoomObjectKind.Leaves,-1.5,contamination:.75);
            for(int frame=0;frame<240&&c.Interaction.Digestion.Exposures==0;frame++)
            {if(group!="absent")s.SetInteractionHand(.7,0,true);Tick(s,.05);}
            Require(c.Interaction.Digestion.Exposures>0,"a trust group waited forever instead of finding food");
            string? chosen=c.Interaction.Digestion.RecentIntakes.First().SourceId;
            Require(chosen==meal.Id||chosen==leaf.Id,"first intake invented a source");
            if(chosen==leaf.Id){if(group=="low")lowWild++;else if(group=="high")highWild++;else absentWild++;}
        }
    }
    observations["first-food-choice"]=new{seedGenerator=291044,samplesPerGroup=samples,lowWild,highWild,absentWild,
        scope="Matched initial need, one near meal beside a continuously engaged hand, one farther leaf patch in the opposite direction. This controlled layout does not imply all low-trust oil always chooses leaves."};
    Console.WriteLine($"First food from wild patch: low={lowWild}/{samples}, high={highWild}/{samples}, absent-hand low={absentWild}/{samples}");
    Require(lowWild>highWild+50&&lowWild>absentWild+50,"trust or current hand perception did not change food selection");
});
var report=new{at=DateTimeOffset.UtcNow,scope="Pure .NET natural resources, trust and digestive health; no game or visual acceptance. Intervals are Wilson 95% game sampling diagnostics, not medical estimates.",success=failures==0,failures,observations,results};
if(args.Length>0){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);File.WriteAllText(args[0],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));}
Console.WriteLine($"Natural life: {results.Count-failures}/{results.Count}");return failures==0?0:1;

using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

var results=new List<object>();int failures=0;
void Check(string name,Action test)
{
    try{test();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}
    catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.Message});Console.WriteLine("FAIL "+name+": "+e.Message);}
}
void Require(bool condition,string why){if(!condition)throw new Exception(why);}
WorldSimulation Fresh()=>WorldSimulation.CreateInteractionPlaytest();
InteractionFeedback Feed(WorldSimulation sim,InteractionAction action,double seconds=.8)
{
    var c=sim.Selected!;var input=new InteractionInput(action,BodyPart.Mouth,.25,.1);
    var reply=sim.BeginInteractionGesture(c.Id,input);
    for(int n=0;n<(int)Math.Round(seconds*10)&&sim.HasInteractionGesture;n++)reply=sim.ContinueInteractionGesture(input,true);
    sim.EndInteractionGesture();return reply;
}
void Tick(WorldSimulation sim,int ticks){for(int n=0;n<ticks;n++){sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);}}
WorldSimulation CompletionFixture()
{
    var sim=Fresh();var c=sim.Selected!;
    sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();sim.State.UnlimitedResources=false;
    c.X=0;c.Z=0;c.TargetX=0;c.TargetZ=0;c.Fullness=80;c.Interaction.Hydration=80;c.Energy=90;c.Trust=70;c.Fear=0;
    c.Interaction.NextAutonomyAt=10000;return sim;
}
(InteractionFeedback Reply,string Pose,double Travel) HeldFrame(WorldSimulation sim,InteractionInput input,bool contact=true)
{
    var c=sim.Selected!;double x=c.X,z=c.Z;
    // Match the game: sample input, advance presentation, advance the world, then choose the rendered pose.
    var reply=sim.ContinueInteractionGesture(input with {Duration=.05},contact);
    sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);
    Require(sim.HasInteractionGesture,"completion unexpectedly ended/reopened the held gesture");
    string pose=sim.InteractionGestureOwnsPose?reply.Pose:InteractionPresentation.Pose(c);
    return(reply,pose,Math.Sqrt((c.X-x)*(c.X-x)+(c.Z-z)*(c.Z-z)));
}

Check("water relieves thirst without filling the stomach or healing an injured foot",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.UnlimitedResources=false;
    c.Interaction.Hydration=10;c.Interaction.FootIntegrity=20;double fullness=c.Fullness;int stock=sim.State.Food;
    Require(Feed(sim,InteractionAction.Drink).Success,"water refused despite thirst");
    Require(c.Interaction.Hydration>10,"drinking had no hydration effect");
    Require(c.Fullness==fullness&&c.Interaction.FootIntegrity==20&&sim.State.Food==stock,"water consumed food, filled stomach or healed a foot");
    Require(c.Interaction.EatingKind==IntakeKind.Water,"sipping indistinguishable from chewing");
});
Check("satisfied drinking refuses another cup without farming trust",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Hydration=100;double trust=c.Trust;
    var reply=Feed(sim,InteractionAction.Drink);
    Require(reply.Refused&&c.Trust==trust&&c.Interaction.EatingRemaining==0,"unlimited cups rewarded affection despite no thirst");
});
Check("sweet food preserves its own intake type and consumes one portion",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.UnlimitedResources=false;int stock=sim.State.Food;
    Feed(sim,InteractionAction.FeedSweet,3);
    Require(c.Interaction.EatingKind==IntakeKind.Sweet&&sim.State.Food==stock-1,"intake kind or portion count wrong");
});
Check("closing the mouth or pausing before acceptance consumes nothing",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Hydration=10;double trust=c.Trust;
    Feed(sim,InteractionAction.Drink,.3);
    Require(c.Interaction.Hydration==10&&c.Interaction.EatingRemaining==0&&c.Trust==trust,"cancelled offer applied early");
});
Check("passive thirst freezes in close-up while a started mouthful may finish",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Hydration=10;
    Feed(sim,InteractionAction.Drink);double hydrated=c.Interaction.Hydration;sim.State.Mode=PlayMode.CloseUp;
    Tick(sim,100);
    Require(c.Interaction.Hydration==hydrated&&c.Interaction.EatingRemaining==0,"close-up advanced passive thirst or stalled swallowing");
    sim.State.Mode=PlayMode.Management;Tick(sim,100);
    Require(c.Interaction.Hydration<hydrated,"ordinary time never advances thirst");
});
Check("thirsty immobilized oil can ask for water without pretending it can walk",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Hydration=5;c.Interaction.FootIntegrity=20;
    c.Interaction.LastThirstRequestAt=-100;Tick(sim,10);
    Require(c.Interaction.LastThirstRequestAt>=0&&c.Speech.Contains("水"),"immobility suppressed the water request");
    Require(!WorldSimulation.CanSelfMove(c),"water request restored movement");
});
Check("save rejects invalid hydration and intake types at its actual entry",()=>
{
    var sim=Fresh();sim.Selected!.Interaction.Hydration=101;bool rejected=false;
    try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"out of range hydration accepted");
    sim.Selected.Interaction.Hydration=75;sim.Selected.Interaction.EatingKind=(IntakeKind)999;rejected=false;
    try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"invalid mouthful type accepted");
});
Check("older saves acquire safe hydration defaults and keep an existing mouthful",()=>
{
    var sim=Fresh();Feed(sim,InteractionAction.FeedMeal);
    var root=JsonNode.Parse(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions))!;
    foreach(var oil in root[nameof(WorldState.Creatures)]!.AsArray())
    {var life=oil![nameof(Creature.Interaction)]!.AsObject();life.Remove(nameof(InteractionLife.Hydration));life.Remove(nameof(InteractionLife.EatingKind));life.Remove(nameof(InteractionLife.LastThirstRequestAt));}
    var loaded=JsonSerializer.Deserialize<WorldState>(root.ToJsonString(),SaveStore.JsonOptions)!;SaveStore.Validate(loaded);
    Require(loaded.Creatures[0].Interaction.Hydration==75&&loaded.Creatures[0].Interaction.EatingRemaining>0,"legacy defaults or mouthful lost");
});

Check("accepted food stays consumed after swallowing while the same mouse gesture is held",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.UnlimitedResources=false;int stock=sim.State.Food;
    var input=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,.25,.1);
    sim.BeginInteractionGesture(c.Id,input);
    for(int n=0;n<8;n++)sim.ContinueInteractionGesture(input,true);
    Require(sim.InteractionIntakeAccepted,"tool still presents a full unaccepted portion");
    for(int n=0;n<25;n++){sim.AdvanceInteractionPresentation(.1);sim.ContinueInteractionGesture(input,true);}
    Require(sim.InteractionIntakeAccepted&&sim.State.Food==stock-1&&c.Interaction.EatingRemaining==0,"held gesture recreated or consumed another portion");
    sim.EndInteractionGesture();Require(!sim.InteractionIntakeAccepted,"next offer inherited the previous accepted flag");
});
Check("a completed held meal sweet or drink releases needs after its actual swallowing response",()=>
{
    foreach(var action in new[]{InteractionAction.FeedMeal,InteractionAction.FeedSweet,InteractionAction.Drink})
    foreach(string need in new[]{"food","water","rest"})
    {
        var sim=CompletionFixture();var c=sim.Selected!;
        if(need=="food")c.Fullness=25;else if(need=="water")c.Interaction.Hydration=20;else c.Energy=10;
        var destination=new RoomObject{Id="destination",Kind=need=="food"?RoomObjectKind.Meal:need=="water"?RoomObjectKind.Water:RoomObjectKind.Cushion,X=2,Z=0,Portions=5};
        var held=new RoomObject{Id="held",Kind=action==InteractionAction.Drink?RoomObjectKind.Water:action==InteractionAction.FeedSweet?RoomObjectKind.Sweet:RoomObjectKind.Meal,X=-3,Z=0,Portions=2};
        sim.State.RoomObjects.Add(destination);sim.State.RoomObjects.Add(held);Require(sim.HoldSupply(held.Id),"could not pick up fixture supply");
        int stock=sim.State.Food;var input=new InteractionInput(action,BodyPart.Mouth,Force:.2,Duration:.05,ObjectId:held.Id);
        Require(sim.BeginInteractionGesture(c.Id,input).Success&&sim.InteractionGestureOwnsPose,"mouth invitation lost its contact phase");
        bool accepted=false,moved=false,sawSwallow=false,sawNaturalIntake=false;
        for(int n=0;n<240;n++)
        {
            var frame=HeldFrame(sim,input);
            accepted|=sim.InteractionIntakeAccepted;
            Require(held.HeldByHand&&held.Portions==(accepted?1:2)&&sim.State.Food==stock,"held portion was repeated, refunded or automatically put down");
            if(accepted)Require(sim.InteractionIntakeAccepted&&!sim.InteractionGestureOwnsPose,"completed food reclaimed body pose or lost acceptance");
            if(!moved&&frame.Pose=="swallow")sawSwallow=true;
            if(!moved&&frame.Travel>.000001)
            {
                Require(accepted&&frame.Pose is "idle" or "guard" or "stomach" or "careful-step",$"{action}/{need}: first movement kept {frame.Pose}");
                moved=true;
            }
            if(c.Interaction.GroundIntake&&c.Interaction.EatingRemaining>0)
            {
                sawNaturalIntake=true;
                Require(!sim.InteractionGestureOwnsPose,"later autonomous mouthful reacquired the completed feeding gesture");
            }
        }
        Require(accepted&&sawSwallow&&moved&&c.X>.5,$"{action}/{need}: holding the finished tool trapped the oil or skipped swallowing");
        Require(need=="rest"?c.Interaction.Resting&&c.Energy>10:sawNaturalIntake&&destination.Portions<5,"autonomous need never completed after release");
        double fullness=c.Fullness,water=c.Interaction.Hydration;
        sim.EndInteractionGesture(true);
        Require(!sim.InteractionIntakeAccepted&&held.HeldByHand&&held.Portions==1&&c.Fullness==fullness&&c.Interaction.Hydration==water,"cancellation refunded food or implicitly released the held container");
        Require(sim.ReleaseSupply(held.Id)&&!held.HeldByHand,"explicit put-down failed after completed feeding");
    }
});
Check("returning an unaccepted offer waits for pending and accepted autonomous bites without stealing their source",()=>
{
    foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Water})
    foreach(bool pending in new[]{true,false})foreach(bool offBody in new[]{true,false})
    {
        var sim=CompletionFixture();var c=sim.Selected!;
        if(kind==RoomObjectKind.Meal)c.Fullness=30;else c.Interaction.Hydration=30;
        var source=new RoomObject{Id="ground",Kind=kind,X=.5,Z=0,Portions=1};
        var held=new RoomObject{Id="held",Kind=kind,X=-3,Z=0,Portions=2};
        sim.State.RoomObjects.Add(source);sim.State.RoomObjects.Add(held);Require(sim.HoldSupply(held.Id),"could not hold reentry fixture supply");
        var mouth=new InteractionInput(kind==RoomObjectKind.Meal?InteractionAction.FeedMeal:InteractionAction.Drink,BodyPart.Mouth,Force:.2,Duration:.05,ObjectId:held.Id);
        var waiting=mouth with {Part=offBody?BodyPart.Mouth:BodyPart.Side};
        Require(sim.BeginInteractionGesture(c.Id,waiting).Success,"could not start unaccepted offer");
        for(int n=0;n<100;n++)
        {
            HeldFrame(sim,waiting,!offBody);
            Require(!sim.InteractionGestureOwnsPose&&!sim.InteractionIntakeAccepted,"off-mouth offer held the body or consumed food");
            if(c.Interaction.EatingRemaining>0&&c.Interaction.NaturalIntakePending==pending)break;
        }
        Require(c.Interaction.GroundIntakeSourceId==source.Id&&c.Interaction.EatingRemaining>0&&c.Interaction.NaturalIntakePending==pending,"fixture never reached the requested autonomous bite stage");
        Require(source.Portions==0&&held.Portions==2,"fixture portions were not separated");
        for(int n=0;n<60&&c.Interaction.EatingRemaining>0;n++)
        {
            HeldFrame(sim,mouth);
            Require(!sim.InteractionIntakeAccepted&&held.Portions==2&&source.Portions==0,"returning hand replaced or refunded the existing bite");
            if(c.Interaction.EatingRemaining>0)
                Require(!sim.InteractionGestureOwnsPose&&c.Interaction.GroundIntakeSourceId==source.Id,"mouth contact detached the autonomous source");
        }
        Require(c.Interaction.EatingRemaining==0&&c.Interaction.Digestion.Exposures==1,"autonomous bite was not accepted exactly once");
        for(int n=0;n<20&&c.Interaction.ReactionPose=="swallow"&&c.Interaction.ReactionPoseRemaining>0;n++)
        {
            HeldFrame(sim,mouth);
            Require(!sim.InteractionIntakeAccepted&&held.Portions==2,"next offer interrupted the swallowing response");
        }
        for(int n=0;n<12;n++)
        {
            HeldFrame(sim,mouth);
            Require(!sim.InteractionIntakeAccepted&&held.Portions==2,"old mouthful time counted toward the next offer dwell");
        }
        for(int n=0;n<4&&!sim.InteractionIntakeAccepted;n++)HeldFrame(sim,mouth);
        Require(sim.InteractionIntakeAccepted&&held.Portions==1&&source.Portions==0&&c.Interaction.Digestion.Exposures==2,"fresh offer did not consume exactly its own second bite");
        for(int n=0;n<80;n++)HeldFrame(sim,mouth);
        Require(sim.InteractionIntakeAccepted&&held.Portions==1&&c.Interaction.Digestion.Exposures==2,"held offer repeated after autonomous-to-hand transition");
    }
});
Check("completed care keeps its short response then releases food water and rest without repeated medicine",()=>
{
    foreach(string need in new[]{"food","water","rest"})
    {
        var sim=CompletionFixture();var c=sim.Selected!;c.Health=80;
        if(need=="food")c.Fullness=25;else if(need=="water")c.Interaction.Hydration=20;else c.Energy=10;
        sim.State.RoomObjects.Add(new(){Id="destination",Kind=need=="food"?RoomObjectKind.Meal:need=="water"?RoomObjectKind.Water:RoomObjectKind.Cushion,X=2,Z=0,Portions=5});
        var input=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.05);
        int medicine=sim.State.Medicine;double continuity=c.Interaction.CareContinuity;
        Require(sim.BeginInteractionGesture(c.Id,input).Success&&sim.InteractionGestureOwnsPose,"care never entered its real dwell phase");
        bool completed=false,moved=false,sawNursing=false;
        for(int n=0;n<240;n++)
        {
            var frame=HeldFrame(sim,input);
            completed|=sim.State.Medicine==medicine-1;
            if(!completed)Require(sim.State.Medicine==medicine&&c.X==0&&c.Z==0,"care applied or released before its dwell completed");
            else
            {
                Require(!sim.InteractionGestureOwnsPose&&sim.State.Medicine==medicine-1&&c.Interaction.CareContinuity==continuity+5,"completed care repeated or reclaimed the body");
                if(!moved&&frame.Pose=="nursing")sawNursing=true;
            }
            if(!moved&&frame.Travel>.000001)
            {
                Require(completed&&frame.Pose is "idle" or "guard" or "stomach" or "careful-step",$"first movement kept care pose {frame.Pose}");
                moved=true;
            }
        }
        Require(completed&&sawNursing&&moved&&c.X>.5,"completed nursing prevented autonomous movement");
        Require(need=="rest"?c.Interaction.Resting&&c.Energy>10:sim.State.RoomObjects.Find(o=>o.Id=="destination")!.Portions<5,"care never released the requested need");
        sim.EndInteractionGesture(true);Require(sim.State.Medicine==medicine-1,"cancel refunded completed care");
    }
});
Check("completed or already intact repair releases movement without repeating repairs",()=>
{
    foreach(bool intact in new[]{false,true})
    {
        var sim=CompletionFixture();var c=sim.Selected!;c.Fullness=25;
        var a=c.Interaction.Accessory;a.Place=AccessoryPlace.Hand;a.Integrity=intact?100:90;
        sim.State.RoomObjects.Add(new(){Id="destination",Kind=RoomObjectKind.Meal,X=2,Z=0,Portions=5});
        var input=new InteractionInput(InteractionAction.RepairAccessory,BodyPart.Accessory,Duration:.05);
        Require(sim.BeginInteractionGesture(c.Id,input).Success,"could not begin repair fixture");
        Require(sim.InteractionGestureOwnsPose!=intact,"intact accessory held control or unfinished repair released it");
        bool moved=false,sawFinished=false;double progress=0;
        for(int n=0;n<240;n++)
        {
            var frame=HeldFrame(sim,input);
            if(a.Integrity==100)
            {
                if(!sawFinished){sawFinished=true;progress=a.RepairProgress;}
                Require(!sim.InteractionGestureOwnsPose&&a.CompletedRepairs==(intact?0:1)&&a.RepairProgress==progress,"completion repeated repair work or held body control");
            }
            else Require(sim.InteractionGestureOwnsPose&&a.CompletedRepairs==0,"partial repair was treated as completed");
            if(!moved&&frame.Travel>.000001)
            {
                Require(sawFinished&&frame.Pose is "idle" or "guard" or "stomach" or "careful-step",$"first movement kept repair pose {frame.Pose}");
                moved=true;
            }
        }
        Require(sawFinished&&moved&&c.X>.5&&sim.State.RoomObjects.Find(o=>o.Id=="destination")!.Portions<5,"completed repair prevented self-feeding");
        sim.ExecuteInteraction(c.Id,new(InteractionAction.DamageAccessory,BodyPart.Accessory,Force:.2));
        double damaged=a.Integrity;int repairs=a.CompletedRepairs;
        for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);
        Require(sim.HasInteractionGesture&&a.Integrity==damaged&&a.CompletedRepairs==repairs,"finished held repair silently restarted after new damage");
    }
});
Check("releasing or cancelling completed repair clears only its own body response",()=>
{
    foreach(bool cancel in new[]{false,true})foreach(bool newerPain in new[]{false,true})
    {
        var sim=CompletionFixture();var c=sim.Selected!;var a=c.Interaction.Accessory;
        a.Place=AccessoryPlace.Hand;a.Integrity=99.5;c.Fullness=25;
        sim.State.RoomObjects.Add(new(){Id="destination",Kind=RoomObjectKind.Meal,X=2,Z=0,Portions=5});
        var input=new InteractionInput(InteractionAction.RepairAccessory,BodyPart.Accessory,Duration:.1);
        sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,true);
        Require(a.CompletedRepairs==1&&InteractionPresentation.Pose(c)=="accessory-watch","repair did not reach its completion response");
        if(newerPain)sim.ExecuteInteraction(c.Id,new(InteractionAction.Poke,BodyPart.Cheek,Force:.2));
        int serial=c.ReactionSerial;string pose=InteractionPresentation.Pose(c);
        sim.EndInteractionGesture(cancel);
        Require(!sim.HasInteractionGesture&&a.CompletedRepairs==1,"release repeated repair or kept the gesture");
        if(newerPain)Require(c.ReactionSerial==serial&&InteractionPresentation.Pose(c)==pose&&pose=="poke","release cleared a newer pain response");
        else
        {
            Require(InteractionPresentation.Pose(c)!="accessory-watch","release left its own stationary repair response");
            Tick(sim,3);Require(c.X>0&&InteractionPresentation.Pose(c)!="accessory-watch","released repair slid under its old pose");
        }
    }
});
Check("completed nursing does not replay treatment or erase a newer painful response",()=>
{
    var sim=CompletionFixture();var c=sim.Selected!;c.Health=80;
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.1);int medicine=sim.State.Medicine;
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<17;n++)sim.ContinueInteractionGesture(input,true);
    Require(sim.State.Medicine==medicine-1,"fixture did not finish care");
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Poke,BodyPart.Cheek,Force:.2));
    int serial=c.ReactionSerial;double health=c.Health,pain=c.Pain;
    var reply=sim.ContinueInteractionGesture(input with {Part=BodyPart.Cheek},true);
    Require(!sim.InteractionGestureOwnsPose&&reply.Pose=="poke"&&c.ReactionSerial==serial&&c.Health==health&&c.Pain==pain&&sim.State.Medicine==medicine-1,"finished care overwrote a new wound or repeated medicine");
    sim.EndInteractionGesture(true);Require(InteractionPresentation.Pose(c)=="poke","cancel cleared a newer pain response");
});
Check("continuing to wipe a clean cheek cannot farm affection after the first dirt was removed",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Dirt=.1;c.Interaction.SurfaceDirt.Clear();
    var input=new InteractionInput(InteractionAction.Clean,BodyPart.Cheek,.25,.1,.05);
    sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,true);
    Require(c.Interaction.DirtAt(BodyPart.Cheek)==0,"fixture did not clean the cheek");double trust=c.Trust;
    for(int n=0;n<50;n++)sim.ContinueInteractionGesture(input,true);
    Require(c.Trust==trust,"finished cleaning kept granting trust");
});
Check("wiping an already clean accessory does not invent another meaningful repair event",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Trust=70;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.TakeAccessoryCare,BodyPart.Accessory));c.Interaction.Accessory.Dirt=0;
    int memories=c.PersonalMemories.Count;
    var input=new InteractionInput(InteractionAction.CleanAccessory,BodyPart.Accessory,.25,.1,.05);
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(c.PersonalMemories.Count==memories&&!c.ActionCounts.ContainsKey(InteractionAction.CleanAccessory.ToString()),"clean item generated completed cleaning events");
});
Check("completing repair records one completion and a new tear resets work but not history",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Trust=70;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.TakeAccessoryCare,BodyPart.Accessory));var a=c.Interaction.Accessory;string id=a.Id;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.DamageAccessory,BodyPart.Accessory,.3));
    var input=new InteractionInput(InteractionAction.RepairAccessory,BodyPart.Accessory,.25,.1);
    sim.BeginInteractionGesture(c.Id,input);InteractionFeedback reply=new(true,"","","idle");
    for(int n=0;n<150;n++)reply=sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(a.Integrity==100&&a.CompletedRepairs==1&&a.Id==id,"repair completion absent, repeated, or replaced the object");
    Require(reply.Message.Contains("戴回"),"no completion cue");
    sim.ExecuteInteraction(c.Id,new(InteractionAction.DamageAccessory,BodyPart.Accessory,.2));
    Require(a.CompletedRepairs==1&&a.RepairProgress==0&&a.Id==id,"new damage erased history or reused the previous finished work");
});
Check("repair work cannot be negative in a persisted world",()=>
{
    var sim=Fresh();sim.Selected!.Interaction.Accessory.RepairProgress=-1;bool rejected=false;
    try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"negative repair progress accepted");
});
Check("a single mouse jump cannot turn a gentle stroke into recorded rough handling",()=>
{
    var sim=Fresh();var c=sim.Selected!;double fear=c.Fear,trust=c.Trust,health=c.Health;
    var input=new InteractionInput(InteractionAction.Stroke,BodyPart.Head,.2,.016,.15);
    sim.BeginInteractionGesture(c.Id,input);var reply=sim.ContinueInteractionGesture(input,true);
    Require(!reply.Refused&&c.Fear==fear&&c.Trust==trust&&c.Health==health,"one pointer jump triggered rough-touch consequences");
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input with {Motion=.003},true);
    sim.EndInteractionGesture();Require(!c.Interaction.Memories.ContainsKey("rough-touch"),"single jitter left an abusive memory");
});
Check("one delayed input frame cannot stand in for sustained fast rubbing",()=>
{
    var sim=Fresh();var c=sim.Selected!;double fear=c.Fear;
    var input=new InteractionInput(InteractionAction.Nuzzle,BodyPart.Cheek,.2,1,1);
    sim.BeginInteractionGesture(c.Id,input);var reply=sim.ContinueInteractionGesture(input,true);
    Require(!reply.Refused&&c.Fear==fear,"a delayed frame was treated as sustained roughness");
});
Check("repeating pain care does not restart an already running foot recovery delay",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.FootIntegrity=20;c.Pain=40;c.Health=100;
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Bottom,.25,.1);
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Tick(sim,200);double remaining=c.Interaction.FootRecovery;c.Pain=40;
    Require(remaining>0&&remaining<60,"fixture did not advance recovery delay");
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(c.Interaction.FootRecovery<=remaining,"helpful care restarted the wait before healing");
});
Check("a rest invitation does not display sleep while walking to a distant cushion",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.RoomObjects.Clear();
    sim.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Cushion,X=7,Z=5});
    var reply=sim.ExecuteInteraction(c.Id,new(InteractionAction.Rest));Tick(sim,10);
    Require(!c.Interaction.Resting&&c.Expression!=Expression.Sleeping&&reply.Pose!="rest","invitation made it sleep while still travelling");
    Require(c.Interaction.RestRemaining>=29.9,"walking consumed the intended actual rest time");
});
Check("rest command alone grants no cushioned healing bonus on a bare floor",()=>
{
    var a=Fresh();var b=Fresh();
    foreach(var sim in new[]{a,b}){sim.State.RoomObjects.Clear();var c=sim.Selected!;c.Interaction.FootIntegrity=10;c.Interaction.FootTreated=true;c.Interaction.FootRecovery=0;c.Health=100;c.Pain=0;}
    a.ExecuteInteraction(a.Selected!.Id,new(InteractionAction.Rest));Tick(a,50);Tick(b,50);
    Require(Math.Abs(a.Selected!.Interaction.FootIntegrity-b.Selected!.Interaction.FootIntegrity)<1e-6,"mere timer granted a cushion bonus");
});
Check("an explicit hurtful interruption wakes the oil and ends the rest action",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.RoomObjects.Clear();
    sim.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Cushion,X=c.X,Z=c.Z});
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Rest));Tick(sim,10);
    Require(c.Interaction.Resting||c.Expression==Expression.Sleeping,"fixture never reached rest presentation on its nearby cushion");
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Poke,BodyPart.Cheek));
    Require(!c.Interaction.Resting&&c.Interaction.RestRemaining==0,"hurtful interruption retained sleep and rest benefit");
});
Check("a rest invitation cancels an unfinished exercise without crediting a lesson",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Energy=80;c.Fullness=60;c.Fear=0;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.TeachFollow));
    Require(c.Interaction.ActiveCue==LearnedCue.Follow,"fixture did not start a follow lesson");
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Rest));
    Require(c.Interaction.ActiveCue==null&&!c.Interaction.FollowPlayer&&c.Interaction.WaitRemaining==0,"rest retained an active exercise");
    Require(c.Interaction.Learning[LearnedCue.Follow].Successes==0,"interruption was credited as learning");
});
Check("accepting a new exercise stops the current rest instead of sleeping through practice",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Energy=80;c.Fullness=60;c.Fear=0;sim.State.RoomObjects.Clear();
    sim.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Cushion,X=c.X,Z=c.Z});
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Rest));Tick(sim,10);
    Require(c.Interaction.Resting,"fixture is not resting");
    sim.ExecuteInteraction(c.Id,new(InteractionAction.TeachWait));
    Require(c.Interaction.ActiveCue==LearnedCue.Wait,"fixture did not start a wait lesson");
    Require(!c.Interaction.Resting&&c.Interaction.RestRemaining==0,"accepted practice still presents rest and receives its benefit");
});
Check("an autonomous invitation supplies the same body pose used by the renderer",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fullness=90;c.Interaction.Hydration=90;c.Energy=80;c.Fear=0;c.Trust=80;
    c.Interaction.NextAutonomyAt=0;c.Interaction.NuzzleFamiliarity=30;sim.State.RoomObjects.Clear();
    sim.SetInteractionHand(c.X,c.Z+.5,true);
    Tick(sim,5);
    Require(InteractionPresentation.Pose(c)=="invite"&&c.Speech.Contains("蹭"),$"autonomous invitation lacks a pose or matching speech: {InteractionPresentation.Pose(c)}, {c.Speech}");
    sim.State.Settings.Pause=true;double remaining=c.Interaction.ReactionPoseRemaining;
    sim.AdvanceInteractionPresentation(.1);Require(c.Interaction.ReactionPoseRemaining==remaining,"pause advanced the reaction");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;Tick(sim,30);
    Require(InteractionPresentation.Pose(c)=="idle","short invitation is stuck after the response window");
});
Check("releasing an accepted meal keeps chewing while releasing pressure stops deformation",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fullness=30;
    var reply=Feed(sim,InteractionAction.FeedMeal);
    Require(InteractionPresentation.Pose(c)=="eat","release hid the unfinished mouthful");
    c.Interaction.EatingRemaining=0;
    var input=new InteractionInput(InteractionAction.Press,BodyPart.Head,.3,.1);
    sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,true);reply=sim.EndInteractionGesture();
    Require(reply.Pose=="idle"&&InteractionPresentation.Pose(c)=="idle","released pressure remained visually applied");
});
Check("a release-triggered push keeps its recoil instead of immediately returning an idle pose",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    var input=new InteractionInput(InteractionAction.Push,BodyPart.Side,.3,.1,.1,DirectionX:1);
    sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,true);var reply=sim.EndInteractionGesture();
    Require(reply.Pose=="push"&&InteractionPresentation.Pose(c)=="push","the push consequence was erased by mouse release");
    Require(c.Interaction.VelocityX>0,"fixture did not apply a push");
});
Check("save files do not replay an expired held gesture as an active reaction on load",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.ExecuteInteraction(c.Id,new(InteractionAction.Sing));
    var restored=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(restored);
    Require(restored.Creatures.First(o=>o.Id==c.Id).Interaction.ReactionPose=="idle","save persisted a transient presentation pose");
});
Check("dressing records actual completed care at its body part and cannot appear on a cancelled attempt",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.FootIntegrity=15;c.Pain=40;c.Health=70;
    Require(c.Interaction.DressingPart==null,"injury invented a treatment dressing");
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Bottom,.25,.1);
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<10;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture(true);
    Require(c.Interaction.DressingPart==null,"partial care invented a dressing");
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(c.Interaction.DressingPart==BodyPart.Bottom&&c.Interaction.DressingRemaining>0,"foot treatment was not represented on the actual site");
    sim.State.Mode=PlayMode.CloseUp;double remaining=c.Interaction.DressingRemaining;Tick(sim,100);
    Require(c.Interaction.DressingRemaining==remaining,"close-up advanced dressing recovery time");
});
Check("an injured treated foot keeps its dressing after the initial settling time",()=>
{
    var sim=Fresh();var c=sim.Selected!;var i=c.Interaction;
    i.FootIntegrity=15;i.FootTreated=true;i.FootRecovery=100;i.DressingPart=BodyPart.Bottom;i.DressingRemaining=.1;
    Tick(sim,10);Require(i.DressingPart==BodyPart.Bottom,"unhealed foot lost its treatment marker");
    i.FootIntegrity=100;i.FootTreated=false;c.Health=100;c.Pain=0;Tick(sim,10);
    Require(i.DressingPart==null,"fully healed treatment marker never clears");
});
Check("voice intent follows the oil's actual response instead of the last selected tool group",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fear=0;c.Trust=80;c.Interaction.NuzzleFamiliarity=30;c.Interaction.NextAutonomyAt=0;c.Fullness=90;c.Interaction.Hydration=90;
    sim.SetInteractionHand(c.X,c.Z+.5,true);
    Tick(sim,5);Require(InteractionPresentation.VoiceCue(c)=="coo-nuzzle","autonomous nuzzle request has no matching sound intent");
    c.Fear=80;c.Trust=5;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Comfort));
    Require(InteractionPresentation.VoiceCue(c)=="coo-refuse","refusal sounds like accepted affection");
    c.Fear=0;sim.ExecuteInteraction(c.Id,new(InteractionAction.Flick,BodyPart.Head));
    Require(InteractionPresentation.VoiceCue(c)=="coo-hurt","hurt response has no matching sound intent");
});
Check("one mouse jump during gentle rocking is a slow-down cue rather than abusive handling",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.ExecuteInteraction(c.Id,new(InteractionAction.Wrap));
    Require(c.Interaction.Restraint==RestraintKind.Wrap,"fixture did not prepare wrapping");
    double fear=c.Fear,trust=c.Trust;
    var input=new InteractionInput(InteractionAction.Rock,BodyPart.Side,.2,.016,.15);
    sim.BeginInteractionGesture(c.Id,input);var reply=sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(!reply.Refused&&c.Fear==fear&&c.Trust==trust&&!c.Interaction.Memories.ContainsKey("rough-touch"),"single mouse jump changed gentle rocking into mistreatment");
});
Check("sustained harsh rocking prompts refusal while leaving the physical support in place",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.ExecuteInteraction(c.Id,new(InteractionAction.Wrap));
    var input=new InteractionInput(InteractionAction.Rock,BodyPart.Side,.2,.1,.21);
    sim.BeginInteractionGesture(c.Id,input);InteractionFeedback reply=new(true,"","","idle");
    for(int n=0;n<5;n++)reply=sim.ContinueInteractionGesture(input,true);double fear=c.Fear;
    Require(reply.Refused,"harsh rocking never gives a proper refusal");
    sim.ContinueInteractionGesture(input,true);Require(c.Fear==fear,"rejected soothing kept applying repeated distress");
    sim.EndInteractionGesture();Require(c.Interaction.Restraint==RestraintKind.Wrap,"refusal removed the support");
});
Check("teasing can move food off the body surface and a real withdrawal changes future food trust once",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fullness=50;c.Trust=70;c.Fear=0;
    var near=new InteractionInput(InteractionAction.Tease,BodyPart.Mouth,.25,.1,0,c.X+.4,c.Z);
    sim.BeginInteractionGesture(c.Id,near);
    for(int n=0;n<8;n++)sim.ContinueInteractionGesture(near,false);
    double suspicion=c.Interaction.FoodSuspicion;string anticipation=c.Speech;
    var away=near with{X=c.X+1.6,Motion=.15};
    sim.ContinueInteractionGesture(away,false);
    Require(c.Interaction.FoodSuspicion>suspicion,"moving bait off the body surface did not count as a real withdrawal");
    Require(c.Expression==Expression.Defiant&&c.Speech!=anticipation,$"food withdrawal kept the earlier cheerful expression or speech: {c.Expression}, {c.Speech}");
    double after=c.Interaction.FoodSuspicion;
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(away with{Motion=0},false);
    Require(c.Interaction.FoodSuspicion==after,"stationary unreachable bait repeatedly charged the same withdrawal");
    sim.EndInteractionGesture(true);
    Require(c.TargetX==c.X&&c.TargetZ==c.Z,"oil continues following food after the tool was cancelled");
});
Check("holding bait in reach without pulling it away does not continuously invent disappointed experiences",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fullness=50;
    var input=new InteractionInput(InteractionAction.Tease,BodyPart.Mouth,.25,.1,.01,c.X+.4,c.Z);
    sim.BeginInteractionGesture(c.Id,input);double before=c.Interaction.FoodSuspicion;
    for(int n=0;n<30;n++)sim.ContinueInteractionGesture(input,true);
    Require(c.Interaction.FoodSuspicion==before,"minor movement in reach was counted as repeated withdrawal");
    sim.EndInteractionGesture(true);
});
Check("wiping the left cheek leaves the right cheek dirty and makes only the left cheek wet",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Dirt=18;c.Interaction.Wetness=0;
    var input=new InteractionInput(InteractionAction.Clean,BodyPart.Cheek,.25,.1,.1,Side:SurfaceSide.Left);
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<15;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(c.Interaction.DirtAt(BodyPart.Cheek,SurfaceSide.Left)==0&&c.Interaction.DirtAt(BodyPart.Cheek,SurfaceSide.Right)==18,"left wipe changed both cheeks");
    Require(c.Interaction.WetnessAt(BodyPart.Cheek,SurfaceSide.Left)>0&&c.Interaction.WetnessAt(BodyPart.Cheek,SurfaceSide.Right)==0,"cleaning wet the opposite cheek");
    var restored=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;SaveStore.Validate(restored);
    var loaded=restored.Creatures.First(o=>o.Id==c.Id).Interaction;
    Require(loaded.DirtAt(BodyPart.Cheek,SurfaceSide.Left)==0&&loaded.DirtAt(BodyPart.Cheek,SurfaceSide.Right)==18,"save merged separate cheeks");
});
Check("drying one side preserves the opposite wet patch and whole-body soiling still reaches both",()=>
{
    var life=new InteractionLife{Dirt=0,Wetness=20};
    life.WipeSurface(BodyPart.Side,30,true,SurfaceSide.Right);
    Require(life.WetnessAt(BodyPart.Side,SurfaceSide.Right)==0&&life.WetnessAt(BodyPart.Side,SurfaceSide.Left)==20,"drying erased both sides");
    life.SoilSurface(7);
    Require(life.DirtAt(BodyPart.Side,SurfaceSide.Right)==7&&life.DirtAt(BodyPart.Side,SurfaceSide.Left)==7,"subsequent soiling ignored split regions");
    life.DrySurfaceOverTime(2);
    Require(life.WetnessAt(BodyPart.Side,SurfaceSide.Left)==18&&life.WetnessAt(BodyPart.Side,SurfaceSide.Right)==0,"passive drying double-applied or ignored local state");
});
Check("moving care to the opposite cheek restarts the actual treatment dwell and dresses that side",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Health=70;c.Pain=30;sim.State.UnlimitedResources=false;int medicine=sim.State.Medicine;
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Cheek,.25,.1,Side:SurfaceSide.Left);
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<12;n++)sim.ContinueInteractionGesture(input,true);
    input=input with{Side=SurfaceSide.Right};for(int n=0;n<6;n++)sim.ContinueInteractionGesture(input,true);
    Require(sim.State.Medicine==medicine&&c.Interaction.DressingPart==null,"two partial sites were combined into one treatment");
    for(int n=0;n<12;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(sim.State.Medicine==medicine-1&&c.Interaction.DressingPart==BodyPart.Cheek&&c.Interaction.DressingSide==SurfaceSide.Right,"dressing or consumption missed the completed site");
});
Check("switching cheeks requires a fresh nuzzle invitation instead of reusing contact on the other side",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Trust=80;c.Fear=0;
    var input=new InteractionInput(InteractionAction.Nuzzle,BodyPart.Cheek,.2,.1,Side:SurfaceSide.Left);
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<6;n++)sim.ContinueInteractionGesture(input,true);
    input=input with{Side=SurfaceSide.Right};var reply=sim.ContinueInteractionGesture(input,true);
    Require(reply.Pose=="invite"&&sim.CurrentInteractionSide==SurfaceSide.Right,"the new cheek inherited the previous invitation");
});
Check("inspection reports a remaining local wet patch even when the body average is low",()=>
{
    var life=new InteractionLife{Dirt=0,Wetness=0};life.SideWetness[BodyPart.Cheek]=new(){Left=0,Right=7};
    string report=string.Join("；",InteractionSurfaceObservation.Describe(life));
    Require(report.Contains("右脸")&&!report.Contains("左脸"),"local surface cue was lost in an average or attributed to the wrong side");
});
Check("partial face drying does not claim that the feet or the whole body are already dry",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Wetness=20;
    var input=new InteractionInput(InteractionAction.Dry,BodyPart.Cheek,.25,.1,.05,Side:SurfaceSide.Left);
    sim.BeginInteractionGesture(c.Id,input);var first=sim.ContinueInteractionGesture(input,true);
    Require(c.Interaction.ReactionSituation=="drying"&&!first.Speech.Contains("脚脚"),"partial cheek drying used a completed foot-drying line");
    for(int n=0;n<30;n++)sim.ContinueInteractionGesture(input,true);
    Require(c.Interaction.ReactionSituation=="dry-face"&&c.Interaction.WetnessAt(BodyPart.Cheek,SurfaceSide.Right)==20,"completion voice or opposite cheek state is wrong");
});
Check("a frightened oil can refuse wiping without becoming visibly pleased or losing the dirt",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fear=80;c.Trust=5;
    double dirt=c.Interaction.DirtAt(BodyPart.Cheek,SurfaceSide.Left);
    var input=new InteractionInput(InteractionAction.Clean,BodyPart.Cheek,.25,.1,.1,Side:SurfaceSide.Left);
    sim.BeginInteractionGesture(c.Id,input);var reply=sim.ContinueInteractionGesture(input,true);
    Require(reply.Refused&&c.Expression==Expression.Frightened&&c.Interaction.DirtAt(BodyPart.Cheek,SurfaceSide.Left)==dirt,"refused cleaning still looked affectionate or removed dirt");
});
Check("local touch sensitivity can receive matching care even after general health recovers",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Health=100;c.Pain=0;c.Interaction.HeadSensitivity=50;
    var wrong=new InteractionInput(InteractionAction.Care,BodyPart.Side,.25,.1);
    sim.BeginInteractionGesture(c.Id,wrong);for(int n=0;n<20;n++)sim.ContinueInteractionGesture(wrong,true);sim.EndInteractionGesture();
    Require(c.Interaction.HeadSensitivity==50,"care on an unrelated healthy site changed head sensitivity");
    var right=wrong with{Part=BodyPart.Head};var start=sim.BeginInteractionGesture(c.Id,right);
    Require(start.Success,"matching care was unavailable despite an identified sore spot");
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(right,true);sim.EndInteractionGesture();
    Require(c.Interaction.HeadSensitivity==40&&c.Interaction.DressingPart==BodyPart.Head,"care did not treat the actual sensitive area");
});
Check("turning a hat-wearing oil or placing its frame reserves the changed silhouette",()=>
{
    foreach(var action in new[]{InteractionAction.TurnDry,InteractionAction.Flip,InteractionAction.Pin})
    {
        var sim=Fresh();var c=sim.State.Creatures[1];var neighbor=sim.State.Creatures[0];
        c.X=c.Z=0;neighbor.X=1.7;neighbor.Z=0;sim.State.Creatures.RemoveAt(2);
        var reply=sim.ExecuteInteraction(c.Id,new(action));
        Require(!reply.Success&&!c.Interaction.Supine&&c.Interaction.Restraint==RestraintKind.None,$"{action} entered a neighbor's space");
        neighbor.X=4;reply=sim.ExecuteInteraction(c.Id,new(action));
        Require(reply.Success&&c.Interaction.Supine,$"{action} remained unavailable in clear space");
    }
});
Check("ground turning actions do not change posture in midair",()=>
{
    foreach(var action in new[]{InteractionAction.TurnDry,InteractionAction.Flip})
    {
        var sim=Fresh();var c=sim.Selected!;c.Interaction.Falling=true;c.Interaction.Height=1;
        var reply=sim.ExecuteInteraction(c.Id,new(action));
        Require(!reply.Success&&!c.Interaction.Supine,$"{action} turned a falling oil");
    }
});
Check("lifting or righting an oil ends its special upside-down drying period",()=>
{
    foreach(var action in new[]{InteractionAction.Lift,InteractionAction.Flip,InteractionAction.Wrap})
    {
        var sim=Fresh();var c=sim.Selected!;c.Interaction.Wetness=30;
        Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.TurnDry)).Success,"turning fixture failed");
        if(action==InteractionAction.Lift)sim.BeginInteractionGesture(c.Id,new(action,Height:1));
        else sim.ExecuteInteraction(c.Id,new(action));
        Require(!c.Interaction.Supine&&c.Interaction.WetRestRemaining==0,$"{action} left the upside-down drying period running");
    }
});
Check("rigid fixtures cannot be assembled through a resting cushion",()=>
{
    foreach(var action in new[]{InteractionAction.Box,InteractionAction.Pin,InteractionAction.Hang})
    {
        var sim=Fresh();var c=sim.Selected!;var bed=sim.State.RoomObjects.First(o=>o.Kind==RoomObjectKind.Cushion);
        c.X=bed.X;c.Z=bed.Z;
        var reply=sim.ExecuteInteraction(c.Id,new(action));
        Require(!reply.Success&&c.Interaction.Restraint==RestraintKind.None,$"{action} assembled its base through the cushion");
        bed.Stored=true;reply=sim.ExecuteInteraction(c.Id,new(action));
        Require(reply.Success,$"{action} remained blocked after the cushion was stored");
    }
});
Check("resuming a low carry keeps its existing height instead of lifting it again",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Carried=true;c.Interaction.Height=.4;
    Require(sim.BeginInteractionGesture(c.Id,new(InteractionAction.Lift,Height:.4)).Success,"resuming supported carry refused");
    Require(c.Interaction.Carried&&c.Interaction.Height==.4,"resuming carry jumped from the chosen low height");
});
Check("a height concern follows an actual rise and easing down changes the response without healing or dropping",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fear=45;c.Interaction.Memories["fall"]=4;c.Interaction.FootIntegrity=20;
    double trust=c.Trust,health=c.Health,fear=c.Fear;
    sim.BeginInteractionGesture(c.Id,new(InteractionAction.Lift,Height:1));
    var high=new InteractionInput(InteractionAction.Lift,Duration:.1,X:c.X,Z:c.Z,Height:2.6);
    sim.ContinueInteractionGesture(high,false);
    Require(c.Interaction.ReactionSituation=="lift-high"&&c.Expression==Expression.Frightened,"raising a wary oil never changed its request or pose");
    sim.ContinueInteractionGesture(high with {Height=1.5},false);
    Require(c.Interaction.ReactionSituation=="lift-high","height concern flickered off before the oil was lowered enough");
    sim.ContinueInteractionGesture(high with {Height=1},false);
    Require(c.Interaction.ReactionSituation=="lift-low"&&c.Interaction.Carried&&!c.Interaction.Falling,"lowering failed to acknowledge support or dropped the oil");
    Require(c.Health==health&&c.Trust==trust&&c.Fear==fear&&c.Interaction.FootIntegrity==20,"a presentation-only height response altered injury or rewarded repeated rises");
});
Check("an invitation to sing or stretch waits for the accepted mouthful to finish",()=>
{
    foreach(var action in new[]{InteractionAction.Sing,InteractionAction.Stretch})
    {
        var sim=Fresh();var c=sim.Selected!;Feed(sim,InteractionAction.FeedSweet);
        double mouthful=c.Interaction.EatingRemaining;string speech=c.Speech;
        Require(!sim.ExecuteInteraction(c.Id,new(action)).Success,$"{action} started with a full mouth");
        Require(c.Interaction.EatingRemaining==mouthful&&c.Speech==speech,"invitation interrupted or erased the accepted bite");
        Tick(sim,20);
        Require(sim.ExecuteInteraction(c.Id,new(action)).Success,$"{action} remained unavailable after swallowing");
    }
});
Check("hanging height controls use the actual body-size limits and reverse immediately at an endpoint",()=>
{
    foreach(var profile in new[]{(young:true,elder:false,scale:.73),(young:false,elder:false,scale:1.0),(young:false,elder:true,scale:1.03)})
    {
        var sim=Fresh();var c=sim.Selected!;c.IsYoung=profile.young;c.IsElder=profile.elder;
        sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();
        Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.Hang)).Success,"fixture setup failed");
        var input=new InteractionInput(InteractionAction.AdjustHanging,Duration:.1,X:c.X,Z:c.Z,Height:3);
        sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,false);
        double maximum=InteractionFixtureGeometry.HangingMaxHeight*profile.scale;
        Require(Math.Abs(c.Interaction.Height-maximum)<1e-8,"render and simulation height ranges disagree");
        double control=InteractionFixtureGeometry.ClampSupportHeight(3,profile.scale,true);
        control=InteractionFixtureGeometry.ClampSupportHeight(control-.15,profile.scale,true);
        sim.ContinueInteractionGesture(input with {Height=control},false);
        Require(c.Interaction.Height<maximum-.1,"reverse wheel movement was swallowed by an overshot input height");
        sim.ContinueInteractionGesture(input with {Height=0},false);sim.EndInteractionGesture(true);
        Require(Math.Abs(c.Interaction.Height-InteractionFixtureGeometry.HangingMinHeight*profile.scale)<1e-8&&c.Interaction.Restraint==RestraintKind.Hanging&&!c.Interaction.Falling,"lower endpoint lost support or used the adult limit for a small oil");
    }
});
Check("inspection and settled expression still expose a sore head or cheek after general pain has faded",()=>
{
    foreach(bool head in new[]{true,false})
    {
        var sim=Fresh();var c=sim.Selected!;sim.State.Mode=PlayMode.CloseUp;c.Health=100;c.Pain=0;c.Fear=0;
        c.Interaction.Dirt=0;c.Interaction.HeadSensitivity=head?50:0;c.Interaction.CheekSensitivity=head?0:50;
        var reply=sim.ExecuteInteraction(c.Id,new(InteractionAction.Inspect));
        Require(reply.Message.Contains(head?"额头":"脸侧")&&reply.Speech.Contains(head?"额头":"脸脸"),"inspection called an observable sore site healthy or did not identify it");
        Tick(sim,100);
        Require(c.Expression==Expression.Worried&&InteractionPresentation.Pose(c)=="guard","after speech ended the untreated sore spot became invisible in presentation");
    }
});
Check("a thirsty but otherwise healthy oil can explain thirst when inspected",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Hydration=10;c.Health=100;c.Pain=0;c.Interaction.Dirt=0;
    var reply=sim.ExecuteInteraction(c.Id,new(InteractionAction.Inspect));
    Require(reply.Message.Contains("水")&&reply.Speech.Contains("水"),"inspection denied the currently visible thirst");
});
Check("care at a second site preserves the first site's dressing and both cheek sides",()=>
{
    var sim=Fresh();var c=sim.Selected!;var life=c.Interaction;c.Health=60;c.Pain=20;life.FootIntegrity=20;life.HeadSensitivity=50;life.CheekSensitivity=70;
    foreach(var site in new[]{(BodyPart.Bottom,SurfaceSide.Whole),(BodyPart.Head,SurfaceSide.Whole),(BodyPart.Cheek,SurfaceSide.Left),(BodyPart.Cheek,SurfaceSide.Right)})
    {
        var input=new InteractionInput(InteractionAction.Care,site.Item1,Duration:.1,Side:site.Item2);
        Require(sim.BeginInteractionGesture(c.Id,input).Success,"care fixture refused");
        for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    }
    Require(life.Dressings.Count==4&&life.HasDressing(BodyPart.Bottom)&&life.HasDressing(BodyPart.Head)&&life.HasDressing(BodyPart.Cheek,SurfaceSide.Left)&&life.HasDressing(BodyPart.Cheek,SurfaceSide.Right),"later care erased an earlier site's visible treatment");
    var copy=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State))!;SaveStore.Validate(copy);
    Require(copy.Creatures.First(x=>x.Id==c.Id).Interaction.Dressings.Count==4,"save round trip lost treated sites");
});
Check("new bottom damage invalidates only the old bottom treatment without resurrecting it next tick",()=>
{
    var sim=Fresh();var c=sim.Selected!;var life=c.Interaction;life.FootIntegrity=30;life.FootTreated=true;life.FootRecovery=60;
    life.Dress(BodyPart.Bottom,SurfaceSide.Whole);life.Dress(BodyPart.Head,SurfaceSide.Whole);
    var input=new InteractionInput(InteractionAction.BurnFeet,BodyPart.Bottom,Duration:.1);
    sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(!life.FootTreated&&life.FootRecovery==0&&!life.HasDressing(BodyPart.Bottom)&&life.HasDressing(BodyPart.Head),"new foot damage kept the old treatment or erased another site's dressing");
    Tick(sim,10);Require(!life.HasDressing(BodyPart.Bottom),"legacy latest-site fields resurrected removed foot care");
});
Check("each dressing freezes in close-up and expires only after its site has settled",()=>
{
    var sim=Fresh();var c=sim.Selected!;var life=c.Interaction;c.Health=100;c.Pain=0;life.HeadSensitivity=50;life.FootTreated=true;life.FootIntegrity=20;life.FootRecovery=100;
    life.Dress(BodyPart.Bottom,SurfaceSide.Whole,.1);life.Dress(BodyPart.Head,SurfaceSide.Whole,.1);life.Dress(BodyPart.Side,SurfaceSide.Left,.1);
    sim.State.Mode=PlayMode.CloseUp;Tick(sim,20);Require(life.Dressings.All(d=>d.Remaining==.1),"close-up advanced one of the treatment clocks");
    sim.State.Mode=PlayMode.Management;Tick(sim,10);
    Require(life.HasDressing(BodyPart.Head)&&life.HasDressing(BodyPart.Bottom)&&!life.HasDressing(BodyPart.Side,SurfaceSide.Left),"unsettled sites lost their dressing or a healed site never cleared");
    life.HeadSensitivity=0;life.FootIntegrity=100;life.FootTreated=false;Tick(sim,10);
    Require(life.Dressings.Count==0&&life.DressingPart==null,"completed sites or legacy fields remained active");
});
Check("save validation rejects duplicate and invalid per-site dressing data",()=>
{
    var sim=Fresh();var life=sim.Selected!.Interaction;life.Dress(BodyPart.Head,SurfaceSide.Whole);
    life.Dressings.Add(new(){Part=BodyPart.Head,Side=SurfaceSide.Whole,Remaining=1});bool rejected=false;
    try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"duplicate site accepted");life.Dressings.RemoveAt(1);life.Dressings[0].Remaining=double.NaN;rejected=false;
    try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"non-finite dressing time accepted");
});
Check("one roll sample cannot teleport across the room or catch up a long frame",()=>
{
    foreach(double duration in new[]{.1,.8})
    {
        var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.X=0;c.Z=0;
        var input=new InteractionInput(InteractionAction.Roll,BodyPart.Side,.3,duration,4,X:4,Z:0);
        Require(sim.BeginInteractionGesture(c.Id,input).Success,"roll setup refused");sim.ContinueInteractionGesture(input,true);
        Require(c.X>0&&c.X<=.250001&&c.Z==0,"roll displacement bypassed the capped sample travel");sim.EndInteractionGesture(true);
    }
});
Check("a roll blocked at the room edge causes no stationary dirt or distress",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();
    c.X=8-InteractionFixtureGeometry.SupineFootprint;c.Z=0;
    var input=new InteractionInput(InteractionAction.Roll,BodyPart.Side,.5,.1,.2,X:c.X+.2,Z:0);
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"clear roll setup refused");double fear=c.Fear,dirt=c.Interaction.Dirt,x=c.X;
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);
    Require(c.X==x&&c.Fear==fear&&c.Interaction.Dirt==dirt,"blocked travel moved through the boundary or accrued rolling effects without moving");
    sim.EndInteractionGesture();Require(!c.ActionCounts.ContainsKey(nameof(InteractionAction.Roll)),"stationary attempt recorded a completed roll");
});
Check("starting a roll reserves turning room before it can overlap a neighbor",()=>
{
    var sim=Fresh();var c=sim.Selected!;var other=sim.State.Creatures.First(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();
    sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id&&o.Id!=other.Id);c.X=c.Z=0;other.X=1.8;other.Z=0;
    var reply=sim.BeginInteractionGesture(c.Id,new(InteractionAction.Roll));
    Require(!reply.Success&&!sim.HasInteractionGesture,"rolling started without clearance for the turned body and hat");
});
Check("roll orientation follows signed actual travel and freezes without contact or motion",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.X=c.Z=0;
    var input=new InteractionInput(InteractionAction.Roll,BodyPart.Side,.3,.1,.12,X:.12,Z:0);
    sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,true);var turn=c.Interaction.RollOrientation;
    Require(Math.Abs(turn.Z)>.1f&&Math.Abs(turn.X)<.00001f,"horizontal travel did not rotate around the matching axis");
    double dirt=c.Interaction.Dirt;
    for(int n=0;n<20;n++){sim.ContinueInteractionGesture(input with {Motion=0,X=c.X},true);sim.AdvanceInteractionPresentation(.1);}
    Require(c.Interaction.RollOrientation==turn&&c.Interaction.Dirt==dirt,"holding still continued the roll animation or effect");
    sim.ContinueInteractionGesture(input with {X=1},false);
    Require(c.Interaction.RollOrientation==turn&&Math.Abs(c.X-.12)<1e-8,"leaving contact moved or righted the body");
    sim.ContinueInteractionGesture(input with {X=0},true);
    Require(c.Interaction.RollOrientation==turn,"reentry pointer jump rotated the body before a fresh contact stroke");
    sim.ContinueInteractionGesture(input with {X=0},true);
    Require(Math.Abs(c.Interaction.RollOrientation.W-1)<.00001&&c.Interaction.RollOrientation.LengthSquared()>.9999,"reverse travel did not reverse the pose");
    sim.EndInteractionGesture(true);Require(!c.Interaction.Rolling,"cancel left a held rolling state behind");
});
Check("rolling a turned oil interrupts drying and never serializes a held input pose",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.X=c.Z=0;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.TurnDry));
    var input=new InteractionInput(InteractionAction.Roll,BodyPart.Side,.3,.1,.1,X:0,Z:.1);
    sim.BeginInteractionGesture(c.Id,input);var initial=c.Interaction.RollOrientation;sim.ContinueInteractionGesture(input,true);
    Require(initial!=System.Numerics.Quaternion.Identity&&initial!=c.Interaction.RollOrientation&&c.Interaction.WetRestRemaining==0,"supine pose overrode rolling or accelerated underside drying continued");
    var copy=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State))!;SaveStore.Validate(copy);
    Require(!copy.Creatures[0].Interaction.Rolling&&copy.Creatures[0].Interaction.RollOrientation==System.Numerics.Quaternion.Identity,"saved state resumed a live rolling input");sim.EndInteractionGesture();
});
Check("short contact keeps its actual response direction even when physical support prevents displacement",()=>
{
    foreach(bool carried in new[]{false,true})
    {
        var sim=Fresh();var c=sim.Selected!;c.Interaction.Carried=carried;
        var reply=sim.ExecuteInteraction(c.Id,new(InteractionAction.Slap,BodyPart.Cheek,DirectionX:-.6,DirectionZ:-.8,Side:SurfaceSide.Right));
        Require(reply.Success&&Math.Abs(c.Interaction.ReactionDirectionX+.6)<1e-8&&Math.Abs(c.Interaction.ReactionDirectionZ+.8)<1e-8,"reaction lost the touch direction");
        Require(carried?c.Interaction.VelocityX==0&&c.Interaction.VelocityZ==0:c.Interaction.VelocityX<0&&c.Interaction.VelocityZ<0,"support was ignored or movement disagreed with the visible recoil");
    }
});
Check("a sore cheek can refuse rubbing while the other side remains available",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Trust=90;c.Fear=0;c.Interaction.ChangeCheekSensitivity(SurfaceSide.Left,55);
    var input=new InteractionInput(InteractionAction.Nuzzle,BodyPart.Cheek,.2,.1,.03,Side:SurfaceSide.Left);
    sim.BeginInteractionGesture(c.Id,input);Require(sim.ContinueInteractionGesture(input,true).Refused,"sore cheek accepted rubbing");sim.EndInteractionGesture();
    input=input with {Side=SurfaceSide.Right};sim.BeginInteractionGesture(c.Id,input);InteractionFeedback? reply=null;
    for(int n=0;n<12;n++)reply=sim.ContinueInteractionGesture(input,true);
    Require(reply is {Success:true,Refused:false}&&c.Interaction.NuzzleFamiliarity>0,"healthy cheek inherited the other side's sensitivity");sim.EndInteractionGesture();
    var inspect=sim.ExecuteInteraction(c.Id,new(InteractionAction.Inspect));
    Require(inspect.Message.Contains("左脸侧")&&!inspect.Message.Contains("右脸侧")&&inspect.Speech.Contains("左边脸脸"),"observation and oil speech lost the sore side");
});
Check("care must reach the sore cheek and cannot treat it through the healthy side",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Health=100;c.Pain=0;c.Interaction.ChangeCheekSensitivity(SurfaceSide.Left,55);
    sim.State.UnlimitedResources=false;int stock=sim.State.Medicine;
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Cheek,Duration:.1,Side:SurfaceSide.Right);
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(sim.State.Medicine==stock&&c.Interaction.CheekSensitivityAt(SurfaceSide.Left)==55&&!c.Interaction.HasDressing(BodyPart.Cheek,SurfaceSide.Right),"healthy-side care consumed a dose or treated the opposite cheek");
    input=input with {Side=SurfaceSide.Left};sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();
    Require(sim.State.Medicine==stock-1&&c.Interaction.CheekSensitivityAt(SurfaceSide.Left)==45&&c.Interaction.CheekSensitivityAt(SurfaceSide.Right)==0,"local care did not stay local");
});
Check("old cheek sensitivity imports without healing either side and local sides survive saving",()=>
{
    var sim=Fresh();var life=sim.Selected!.Interaction;life.CheekSensitivity=60;
    Require(life.CheekSensitivityAt(SurfaceSide.Left)==60&&life.CheekSensitivityAt(SurfaceSide.Right)==60,"legacy sensitivity vanished");
    life.ChangeCheekSensitivity(SurfaceSide.Left,-10);
    var saved=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State))!;SaveStore.Validate(saved);
    var copy=saved.Creatures[0].Interaction;
    Require(copy.CheekSensitivityAt(SurfaceSide.Left)==50&&copy.CheekSensitivityAt(SurfaceSide.Right)==60&&copy.CheekSensitivity==60,"legacy import or save merged the two cheeks");
    copy.CheekSensitivitySides!.Right=double.NaN;bool rejected=false;try{SaveStore.Validate(saved);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"invalid split sensitivity accepted");
});
Check("aiming holds the selected ball and releases only that exact object",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    var first=new RoomObject{Kind=RoomObjectKind.Ball,X=4,Z=3};var selected=new RoomObject{Kind=RoomObjectKind.Ball,X=c.X+.3,Z=c.Z,VelocityX=1};
    sim.State.RoomObjects.Add(first);sim.State.RoomObjects.Add(selected);
    var input=new InteractionInput(InteractionAction.PlayBall,Duration:.1,Motion:.1,DirectionX:0,DirectionZ:-1,ObjectId:selected.Id);
    Require(sim.BeginInteractionGesture(c.Id,input).Success&&sim.InteractionGestureObject==selected.Id,"object binding failed");
    double x=selected.X,z=selected.Z;Tick(sim,15);
    Require(selected.X==x&&selected.Z==z&&selected.VelocityX==0&&selected.VelocityZ==0,"held ball drifted or an oil kicked it away");
    sim.ContinueInteractionGesture(input,false);sim.EndInteractionGesture();
    Require(first.VelocityX==0&&first.VelocityZ==0&&selected.VelocityX==0&&selected.VelocityZ<0,"release pushed a different ball or changed direction");
});
Check("removing or hiding the held ball cannot redirect the release to another ball",()=>
{
    foreach(bool stored in new[]{false,true})
    {
        var sim=Fresh();var c=sim.Selected!;var held=new RoomObject{Kind=RoomObjectKind.Ball};var other=new RoomObject{Kind=RoomObjectKind.Ball};
        sim.State.RoomObjects.Add(held);sim.State.RoomObjects.Add(other);
        var input=new InteractionInput(InteractionAction.PlayBall,Motion:.2,ObjectId:held.Id);
        sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,false);
        if(stored)held.Stored=true;else sim.State.RoomObjects.Remove(held);
        var reply=sim.ContinueInteractionGesture(input,false);sim.EndInteractionGesture();
        Require(!reply.Success&&!sim.HasInteractionGesture&&other.VelocityX==0&&other.VelocityZ==0,"missing held object was replaced silently");
    }
});
Check("a turned oil cannot kick the ball while it cannot orient itself to play",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.Fullness=90;c.Interaction.ToyInterest=1;c.Interaction.LastRequestAt=-100;
    c.Interaction.Supine=true;c.Interaction.SupineRemaining=30;c.Interaction.NextAutonomyAt=100;
    var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=c.X+.4,Z=c.Z};sim.State.RoomObjects.Add(ball);Tick(sim,10);
    Require(ball.VelocityX==0&&ball.VelocityZ==0&&c.Interaction.ToySkill==0,"supine body performed an ordinary standing ball return");
});
Check("a fast soft ball rebounds from the body with a brief reaction and no injury",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.X=c.Z=0;
    var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=-2,Z=0,VelocityX=8};sim.State.RoomObjects.Add(ball);double health=c.Health;
    for(int n=0;n<6;n++)sim.AdvanceInteractionPresentation(.1);
    Require(ball.X<-.8&&ball.VelocityX<0&&c.X==0&&c.Health==health,"ball tunneled through the body or applied damaging displacement");
    Require(c.Interaction.ReactionSituation=="ball-touch"&&c.Interaction.ToySkill==0,"impact feedback was absent or close-up collision awarded autonomous practice");
});
Check("ball collisions respect box walls and the visible yard fence instead of an inner invisible rectangle",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.X=c.Z=0;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Box));var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=-2,Z=0,VelocityX=6};sim.State.RoomObjects.Add(ball);
    for(int n=0;n<5;n++)sim.AdvanceInteractionPresentation(.1);
    Require(ball.X<-InteractionFixtureGeometry.BoxWidth*.5-InteractionFixtureGeometry.BallRadius&&ball.VelocityX<0,"ball entered the closed box");
    c.X=-5;c.Z=-5;ball.X=7;ball.Z=6;ball.VelocityX=1;ball.VelocityZ=1;sim.AdvanceInteractionPresentation(.1);
    Require(ball.X>7&&ball.Z>6&&ball.VelocityX>0&&ball.VelocityZ>0,"ball bounced in empty yard space");
    double edge=InteractionFixtureGeometry.YardFenceInner-InteractionFixtureGeometry.BallRadius;ball.X=edge-.01;ball.Z=0;ball.VelocityX=1;ball.VelocityZ=0;sim.AdvanceInteractionPresentation(.1);
    Require(ball.X<=edge&&ball.VelocityX<0,"ball passed the visible fence");
});
Check("a ball must be placed in clear space and a cushion absorbs its approach",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.X=-4;c.Z=0;
    Require(!sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveBall,X:c.X,Z:c.Z)).Success,"ball spawned through the selected oil");
    sim.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Cushion,X=0,Z=0});
    Require(!sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveBall,X:0,Z:0)).Success,"ball spawned in the cushion");
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveBall,X:-2,Z:0)).Success,"clear placement refused");
    var ball=sim.State.RoomObjects.Single(o=>o.Kind==RoomObjectKind.Ball);ball.VelocityX=3;
    for(int n=0;n<10;n++)sim.AdvanceInteractionPresentation(.1);
    Require(ball.X<-1.13&&ball.VelocityX<=0,"ball entered the soft cushion instead of stopping or rebounding");
});
Check("woodland balls rebound from the visible trunk section and cannot be placed inside trees",()=>
{
    foreach(var tree in WoodlandLayout.Trees)
    {
        var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
        c.X=c.Z=0;var trunk=tree.TrunkAt(InteractionFixtureGeometry.BallRadius);
        Require(!sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveBall,X:trunk.X,Z:trunk.Z)).Success,"ball spawned inside a tree");
        var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=trunk.X-1,Z=trunk.Z,VelocityX=6};sim.State.RoomObjects.Add(ball);
        for(int n=0;n<10&&ball.VelocityX>0;n++)
        {
            sim.AdvanceInteractionPresentation(.05);
            Require(Math.Sqrt(Math.Pow(ball.X-trunk.X,2)+Math.Pow(ball.Z-trunk.Z,2))>=trunk.Radius+InteractionFixtureGeometry.BallRadius-.00001,"ball entered a trunk");
        }
        Require(ball.X<trunk.X&&ball.VelocityX<0,"ball crossed the tree or never rebounded");
    }
});
Check("a rolling ball enters the river slows and floats instead of bouncing at an invisible bank",()=>
{
    var sim=Fresh();sim.State.Creatures.Clear();sim.State.SelectedId=null;sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
    double edge=WoodlandLayout.RiverEdge(0);var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=edge-.6,Z=0,VelocityX=3};sim.State.RoomObjects.Add(ball);
    for(int n=0;n<30;n++)sim.AdvanceInteractionPresentation(.1);
    Require(ball.X>edge+.2&&ball.VelocityX>=0&&ball.VelocityX<.1&&ball.VelocityZ>.05,"river blocked the ball or failed to slow it");
    float height=RoomBallEnvironment.CenterHeight(sim.State,ball);
    Require(height<InteractionFixtureGeometry.BallRadius&&height+InteractionFixtureGeometry.BallRadius>NaturalIntakeGeometry.WaterHeight,"floating ball is on the ground or fully submerged");
    Require(RoomResourceText.Label(ball,sim.State).Contains("漂"),"water state is invisible to inspection");
    var loaded=new WorldSimulation(JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!);SaveStore.Validate(loaded.State);
    for(int n=0;n<20;n++){sim.AdvanceInteractionPresentation(.1);loaded.AdvanceInteractionPresentation(.1);}
    var copy=loaded.State.RoomObjects.Single(o=>o.Kind==RoomObjectKind.Ball);
    Require(copy.X==ball.X&&copy.Z==ball.Z&&copy.VelocityX==ball.VelocityX&&copy.VelocityZ==ball.VelocityZ,"reloading changed river ball continuation");
});
Check("river current freezes in pause and does not start passive drift in close-up",()=>
{
    var sim=Fresh();sim.State.Creatures.Clear();sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
    var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=7,Z=0};sim.State.RoomObjects.Add(ball);
    sim.State.Settings.Pause=true;for(int n=0;n<20;n++)sim.AdvanceInteractionPresentation(.1);
    Require(ball.X==7&&ball.Z==0&&ball.VelocityZ==0,"pause advanced river current");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;for(int n=0;n<20;n++)sim.AdvanceInteractionPresentation(.1);
    Require(ball.X==7&&ball.Z==0&&ball.VelocityZ==0,"close-up started passive current");
    sim.State.Mode=PlayMode.Management;for(int n=0;n<20;n++)sim.AdvanceInteractionPresentation(.1);
    Require(ball.Z>.1,"ordinary time failed to restore current");
});
Check("small presentation steps do not erase gentle river drift",()=>
{
    var ends=new List<double>();
    foreach(int fps in new[]{30,60,144})
    {
        var sim=Fresh();sim.State.Creatures.Clear();sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
        var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=7,Z=0};sim.State.RoomObjects.Add(ball);
        for(int n=0;n<fps*5;n++)sim.AdvanceInteractionPresentation(1.0/fps);
        Require(ball.Z>.4,"small-frame current was repeatedly put to sleep");ends.Add(ball.Z);
    }
    Require(ends.Max()-ends.Min()<.005,"frame schedule changed gentle drift distance materially");
});
Check("oil observes a floating ball without repeatedly chasing it into the river",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
    c.X=WoodlandLayout.RiverEdge(0)-1;c.Z=0;c.TargetX=c.X;c.TargetZ=c.Z;c.Fullness=c.Energy=c.Interaction.Hydration=100;
    c.Interaction.ToyInterest=1;c.Interaction.NextAutonomyAt=0;
    var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=WoodlandLayout.RiverEdge(0)+.5,Z=0};sim.State.RoomObjects.Add(ball);
    double health=c.Health;Tick(sim,50);
    Require(c.Interaction.ToySkill==0&&c.Health==health&&c.Interaction.Wetness==0&&c.Activity!="和球球玩"&&
        WoodlandLayout.GroundFits(sim.State,c.RegionId,c.X,c.Z,InteractionFixtureGeometry.StandingFootprint),"oil kept pursuing or kicking a water ball");
    Require(c.LastReaction.Contains("没有追下去"),"floating ball has no readable reaction");
});
Check("a floating ball can be pushed back toward shore without moving the oil into water",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
    c.X=3;c.Z=0;c.Interaction.ToyInterest=0;c.Interaction.NextAutonomyAt=1000;
    var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=7.6,Z=0};sim.State.RoomObjects.Add(ball);
    for(int attempt=0;attempt<3&&RoomBallEnvironment.InWater(sim.State,ball);attempt++)
    {
        var input=new InteractionInput(InteractionAction.PlayBall,Duration:.1,Motion:.1,DirectionX:-1,DirectionZ:0,ObjectId:ball.Id);
        Require(sim.BeginInteractionGesture(c.Id,input).Success,"floating ball could not be selected");
        for(int n=0;n<15;n++)sim.ContinueInteractionGesture(input,false);
        sim.EndInteractionGesture();for(int n=0;n<15;n++)sim.AdvanceInteractionPresentation(.1);
    }
    Require(!RoomBallEnvironment.InWater(sim.State,ball)&&c.X==3&&c.Z==0&&c.Interaction.Wetness==0,"floating ball could not return to shore independently of the oil");
});
Check("practiced ball play returns closer to the player than a beginner's attempt",()=>
{
    double AimError(double skill)
    {
        var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.X=c.Z=0;c.Fullness=100;c.Interaction.Hydration=100;c.Energy=100;
        c.Interaction.ToyInterest=1;c.Interaction.ToySkill=skill;c.Interaction.LastRequestAt=-100;
        sim.State.PlayerX=3;sim.State.PlayerZ=2;
        var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=.83,Z=0};sim.State.RoomObjects.Add(ball);
        for(int n=0;n<10&&c.Interaction.ReactionSituation is not ("ball" or "ball-askew");n++)sim.Advance(.1);
        Require(c.Interaction.ReactionSituation is "ball" or "ball-askew","oil never attempted a return");
        double dx=sim.State.PlayerX-ball.X,dz=sim.State.PlayerZ-ball.Z;
        return Math.Abs(Math.Atan2(ball.VelocityZ,ball.VelocityX)-Math.Atan2(dz,dx));
    }
    double beginner=AimError(0),practiced=AimError(100);
    Require(beginner>.01&&practiced<.000001,"toy familiarity had no visible consequence for the returned direction");
});
Check("a finished mouthful acknowledges swallowing once without another portion or passive recovery",()=>
{
    foreach(var action in new[]{InteractionAction.FeedMeal,InteractionAction.FeedSweet,InteractionAction.Drink})
    {
        var sim=Fresh();var c=sim.Selected!;sim.State.Mode=PlayMode.CloseUp;sim.State.UnlimitedResources=false;c.Interaction.Hydration=10;
        Feed(sim,action);double fullness=c.Fullness,water=c.Interaction.Hydration,trust=c.Trust;int food=sim.State.Food;
        for(int n=0;n<25;n++)sim.AdvanceInteractionPresentation(.1);int serial=c.ReactionSerial;
        Require(c.Interaction.EatingRemaining==0&&c.Interaction.ReactionSituation=="mouthful-done"&&(c.Speech.Contains("咽")||c.Speech.Contains("吃好")),$"{action}: remaining {c.Interaction.EatingRemaining}, situation {c.Interaction.ReactionSituation}, speech {c.Speech}");
        for(int n=0;n<40;n++)sim.AdvanceInteractionPresentation(.1);
        Require(c.ReactionSerial==serial&&c.Fullness==fullness&&c.Interaction.Hydration==water&&c.Trust==trust&&sim.State.Food==food,"swallow completion repeated benefits or advanced passive time");
    }
});
Check("finishing a mouthful does not replace a newer pain response with cheerful speech",()=>
{
    var sim=Fresh();var c=sim.Selected!;Feed(sim,InteractionAction.FeedMeal);
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Flick,BodyPart.Head,Force:.7));int serial=c.ReactionSerial;string speech=c.Speech;
    for(int n=0;n<20;n++)sim.AdvanceInteractionPresentation(.1);
    Require(c.Interaction.EatingRemaining==0&&c.Interaction.ReactionSituation=="hurt"&&c.ReactionSerial==serial&&c.Speech==speech,"swallowing overwrote the later painful interaction");
});
Check("releasing the empty spoon does not erase the newer swallowing response",()=>
{
    var sim=Fresh();var c=sim.Selected!;var input=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,Duration:.1);
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<8;n++)sim.ContinueInteractionGesture(input,true);
    for(int n=0;n<25&&c.Interaction.EatingRemaining>0;n++)sim.AdvanceInteractionPresentation(.1);
    var held=sim.ContinueInteractionGesture(input,true);var released=sim.EndInteractionGesture();
    Require(held.Pose=="swallow"&&released.Pose=="swallow"&&c.Interaction.ReactionPoseRemaining>0,"old feeding gesture cleared the newer swallow response");
});
Check("conversation waits for swallowing without early affection or repeated invitations",()=>
{
    foreach(var action in new[]{InteractionAction.Greet,InteractionAction.Praise,InteractionAction.Comfort})
    {
        var sim=Fresh();var c=sim.Selected!;Feed(sim,InteractionAction.FeedMeal);double trust=c.Trust;int serial=c.ReactionSerial;
        var reply=sim.ExecuteInteraction(c.Id,new(action));
        Require(reply.Success&&!reply.Changed&&c.Trust==trust&&c.ReactionSerial==serial&&c.Interaction.PendingConversation==action,"invitation interrupted chewing or granted early affection");
        Require(!sim.ExecuteInteraction(c.Id,new(action)).Success,"repeated invitation replaced the pending response");
        while(c.Interaction.EatingRemaining>0)sim.AdvanceInteractionPresentation(.1);
        Require(c.Interaction.ReactionSituation=="mouthful-done"&&c.Trust==trust,"conversation skipped the swallowing beat");
        for(int n=0;n<8;n++)sim.AdvanceInteractionPresentation(.1);
        Require(c.Interaction.PendingConversation==null&&c.Trust>trust&&c.Interaction.ReactionSituation is "greet" or "praise-empty" or "comfort","queued response did not complete");
        trust=c.Trust;serial=c.ReactionSerial;for(int n=0;n<50;n++)sim.AdvanceInteractionPresentation(.1);
        Require(c.Trust==trust&&c.ReactionSerial==serial,"queued response repeated its reward");
    }
});
Check("a newer painful interaction cancels a waiting conversation",()=>
{
    var sim=Fresh();var c=sim.Selected!;Feed(sim,InteractionAction.FeedMeal);sim.ExecuteInteraction(c.Id,new(InteractionAction.Greet));
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Poke,BodyPart.Cheek,Force:.4));double trust=c.Trust;int serial=c.ReactionSerial;
    for(int n=0;n<40;n++)sim.AdvanceInteractionPresentation(.1);
    Require(c.Trust==trust&&c.ReactionSerial==serial&&c.Interaction.PendingConversation==null&&c.Interaction.ReactionSituation=="poke","stale greeting overrode protest or gave affection");
});
Check("timely praise keeps its event while swallowing without rewarding the same event twice",()=>
{
    var sim=Fresh();var c=sim.Selected!;Feed(sim,InteractionAction.FeedMeal);
    c.Interaction.PendingPraise="等待";c.Interaction.PraiseRemaining=.05;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Praise));Tick(sim,35);
    Require(c.Interaction.Memories.GetValueOrDefault("praise")==2&&c.Interaction.PendingPraise==null&&c.Interaction.ReactionSituation=="praise","timely praise lost its event while waiting");
    for(int n=0;n<30;n++)sim.AdvanceInteractionPresentation(.1);
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Praise));
    Require(c.Interaction.Memories.GetValueOrDefault("praise")==2&&c.Interaction.ReactionSituation=="praise-empty","event earned a second praise reward");
});
Check("resting oil replies gently without waking or granting passive recovery in close-up",()=>
{
    foreach(var action in new[]{InteractionAction.Greet,InteractionAction.Praise,InteractionAction.Comfort})
    {
        var sim=Fresh();var c=sim.Selected!;c.Interaction.Resting=true;c.Interaction.RestRemaining=20;c.Expression=Expression.Sleeping;
        sim.State.Mode=PlayMode.CloseUp;double energy=c.Energy,rest=c.Interaction.RestRemaining;int serial=c.ReactionSerial;
        sim.ExecuteInteraction(c.Id,new(action));
        Require(c.ReactionSerial==serial&&c.Expression==Expression.Sleeping,"rest invitation forced an immediate awake expression");
        Tick(sim,12);
        Require(c.Interaction.Resting&&c.Expression==Expression.Sleeping&&c.Interaction.ReactionSituation.EndsWith("-rest")&&c.Energy==energy&&c.Interaction.RestRemaining==rest,"gentle response woke the oil or advanced passive recovery");
    }
});
Check("pause freezes queued conversation and save cannot replay it as a reward",()=>
{
    var sim=Fresh();var c=sim.Selected!;Feed(sim,InteractionAction.FeedMeal);sim.ExecuteInteraction(c.Id,new(InteractionAction.Greet));
    double trust=c.Trust,remaining=c.Interaction.EatingRemaining;sim.State.Settings.Pause=true;
    for(int n=0;n<50;n++)sim.AdvanceInteractionPresentation(.1);
    Require(c.Trust==trust&&c.Interaction.EatingRemaining==remaining&&c.Interaction.PendingConversation.HasValue,"pause completed the pending interaction");
    string json=JsonSerializer.Serialize(c.Interaction);
    var loaded=JsonSerializer.Deserialize<InteractionLife>(json)!;
    Require(loaded.PendingConversation==null&&!json.Contains("Conversation"),"save serialized the pending response");
});
Check("verbal comfort refusal describes distance without inventing physical touch",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fear=90;c.Trust=5;
    var reply=sim.ExecuteInteraction(c.Id,new(InteractionAction.Comfort));
    Require(reply.Refused&&c.Interaction.ReactionSituation=="comfort-distance"&&!reply.Speech.Contains("碰")&&!reply.Speech.Contains("摸"),"verbal comfort used unrelated touch refusal");
});
Check("a stored treasure returns as the same object without farming a fresh gift",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveTreasure)).Success,"first treasure was refused");
    var treasure=sim.State.RoomObjects.Single(o=>o.Kind==RoomObjectKind.Treasure);string id=treasure.Id;
    Require(sim.PutAwayRoomObject(id)&&treasure.Stored&&sim.State.RoomObjects.Contains(treasure),"taking away destroyed the owned item");
    Require(!sim.PutAwayRoomObject(id),"the same stored item was taken again");
    double trust=c.Trust;
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveTreasure)).Success,"original treasure could not be returned");
    Require(treasure.Id==id&&!treasure.Stored&&sim.State.RoomObjects.Count(o=>o.Kind==RoomObjectKind.Treasure)==1&&c.Trust==trust,"return spawned another item or farmed gift affection");
    Require(c.Interaction.ReactionSituation=="treasure-return"&&c.Interaction.Memories.GetValueOrDefault("treasure")==2,"return erased its history or counted as another first gift");
});
Check("treasure placement clears a turned body and stays in its current region",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);
    sim.State.Regions.Add(new(){Id="second",Name="第二小院",Kind=RegionKind.Sanctuary});sim.State.ActiveRegionId=c.RegionId="second";
    c.Interaction.Supine=true;
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveTreasure)).Success,"clear position near a turned oil was not found");
    var item=sim.State.RoomObjects.Single();double dx=item.X-c.X,dz=item.Z-c.Z;
    Require(item.RegionId==c.RegionId&&Math.Sqrt(dx*dx+dz*dz)>1.5,"treasure spawned under the turned body or in the wrong region");
});
Check("stored treasure keeps ownership through save and cannot be displayed as present",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveTreasure));
    var item=sim.State.RoomObjects.Single(o=>o.Kind==RoomObjectKind.Treasure);sim.PutAwayRoomObject(item.Id);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);sim=new WorldSimulation(state);c=sim.Selected!;c.Fullness=90;c.Interaction.Hydration=90;c.Interaction.NextAutonomyAt=0;
    Tick(sim,5);
    Require(state.RoomObjects.Any(o=>o.Id==item.Id&&o.Stored&&o.OwnerId==c.Id)&&c.Interaction.ReactionSituation=="treasure-missing","saving lost ownership or autonomy displayed a stored object");
});
Check("rapid harsh speech shares a response interval instead of rewriting the relationship on every click",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.ExecuteInteraction(c.Id,new(InteractionAction.Cold));
    double trust=c.Trust,fear=c.Fear,resentment=c.Resentment;int memories=c.PersonalMemories.Count,serial=c.ReactionSerial;
    foreach(var action in new[]{InteractionAction.Scold,InteractionAction.Cold,InteractionAction.Belittle})
        for(int n=0;n<20;n++)Require(!sim.ExecuteInteraction(c.Id,new(action)).Success,"a burst bypassed the shared speech interval");
    Require(c.Trust==trust&&c.Fear==fear&&c.Resentment==resentment&&c.PersonalMemories.Count==memories&&c.ReactionSerial==serial,"rejected speech still changed the oil");
    sim.State.Settings.Pause=true;for(int n=0;n<40;n++)sim.AdvanceInteractionPresentation(.1);
    Require(c.Interaction.HarshResponseRemaining==2.2,"paused response interval advanced");
    sim.State.Settings.Pause=false;for(int n=0;n<23;n++)sim.AdvanceInteractionPresentation(.1);
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.Belittle)).Success&&c.Trust<trust,"next deliberate phrase remained unavailable");
});
Check("scolding interrupts a lesson without recording learning or a completed rule",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.ExecuteInteraction(c.Id,new(InteractionAction.TeachFollow));
    var learning=c.Interaction.Learning[LearnedCue.Follow];double familiarity=learning.Familiarity;int successes=learning.Successes;
    Require(c.Interaction.ActiveCue.HasValue,"fixture lesson did not start");
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Scold));Tick(sim,15);
    Require(c.Interaction.ActiveCue==null&&!c.Interaction.FollowPlayer&&learning.Familiarity==familiarity&&learning.Successes==successes&&c.Interaction.PendingPraise==null,"scolding completed or continued the lesson");
    Require(c.Interaction.Memories.GetValueOrDefault("scold-confused")==1,"ungrounded scolding lost its own context");
});
Check("a contextual scold requires a current observed unresolved incident for this oil",()=>
{
    foreach(string state in new[]{"valid","unknown","resolved","expired","wrong-actor"})
    {
        var sim=Fresh();var c=sim.Selected!;c.CurrentOffenseId="test-event";c.OffenseRemaining=state=="expired"?0:10;
        sim.State.Incidents.Add(new(){Id="test-event",Kind=IncidentKind.Theft,ActorId=state=="wrong-actor"?"other":c.Id,RegionId=c.RegionId,Resolved=state=="resolved"});
        if(state!="unknown")sim.State.PlayerKnownIncidents.Add("test-event");
        sim.ExecuteInteraction(c.Id,new(InteractionAction.Scold));
        Require(c.Interaction.ReactionSituation==(state=="valid"?"scold":"confused-scold"),state+" used an incorrect context");
        Require(sim.State.Incidents[0].Resolved==(state=="resolved"),"speech silently resolved the incident");
    }
});
Check("immobility and a real unanswered request have distinct harsh speech feedback",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.FootIntegrity=20;
    var reply=sim.ExecuteInteraction(c.Id,new(InteractionAction.Scold));
    Require(c.Interaction.ReactionSituation=="scold-immobile"&&reply.Message.Contains("本来就无法")&&!WorldSimulation.CanSelfMove(c),"immobility was treated as learned obedience");
    sim=Fresh();c=sim.Selected!;c.Trust=100;c.Interaction.Hydration=5;c.Interaction.LastThirstRequestAt=-100;
    sim.SetInteractionHand(c.X,c.Z+.5,true);Tick(sim,10);
    Require(c.Interaction.ReactionSituation=="thirsty","fixture did not advance to an actual thirst request");
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Cold));
    Require(c.Interaction.ReactionSituation=="cold-request"&&c.Interaction.Hydration<5,"cold response forgot the request or satisfied the need");
});
Check("belittling can provoke resistance or hurt without turning either into affection",()=>
{
    foreach(double courage in new[]{.1,.9})
    {
        var sim=Fresh();var c=sim.Selected!;c.Courage=courage;double trust=c.Trust;
        sim.ExecuteInteraction(c.Id,new(InteractionAction.Belittle));
        Require(c.Interaction.ReactionSituation==(courage>.6?"belittle-defiant":"belittle-hurt")&&c.Trust<trust&&c.Interaction.Memories.GetValueOrDefault("belittled")==1,"disposition or relationship consequence was lost");
    }
});
Check("moving the holding hand away releases only the hand restraint and contact can resume",()=>
{
    var sim=Fresh();var c=sim.Selected!;var input=new InteractionInput(InteractionAction.Hold,BodyPart.Side,Duration:.1);
    sim.BeginInteractionGesture(c.Id,input);Require(c.Interaction.Restraint==RestraintKind.Hand,"hold fixture failed");
    sim.ContinueInteractionGesture(input,false);
    Require(c.Interaction.Restraint==RestraintKind.None&&WorldSimulation.CanSelfMove(c),"hand restraint remained after contact ended");
    sim.ContinueInteractionGesture(input,true);Require(c.Interaction.Restraint==RestraintKind.Hand,"returning the hand failed to hold again");
    sim.ContinueInteractionGesture(input,false);sim.ExecuteInteraction(c.Id,new(InteractionAction.Pin));
    Require(!sim.ContinueInteractionGesture(input,true).Success&&c.Interaction.Restraint==RestraintKind.Frame,"returning hand replaced a real fixture");
    sim.EndInteractionGesture();Require(c.Interaction.Restraint==RestraintKind.Frame,"ending hand contact removed the fixture");
});
Check("push release off the body cannot apply saved force or a reentry pointer jump",()=>
{
    foreach(bool reenter in new[]{false,true})
    {
        var sim=Fresh();var c=sim.Selected!;var input=new InteractionInput(InteractionAction.Push,BodyPart.Side,Duration:.1,Motion:.1,DirectionX:1,DirectionZ:0);
        sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,true);sim.ContinueInteractionGesture(input,false);
        if(reenter)sim.ContinueInteractionGesture(input with{Motion=5},true);
        double health=c.Health,trust=c.Trust;sim.EndInteractionGesture();
        Require(c.Interaction.VelocityX==0&&c.Interaction.VelocityZ==0&&c.Health==health&&c.Trust==trust,"off-contact release or reentry jump applied an old push");
    }
});
Check("an off-contact pressed mouse does not freeze the oil's own movement or keep deforming it",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.X=c.Z=0;c.Fullness=90;
    sim.SetInteractionHand(3,0,true);c.Interaction.FollowPlayer=true;c.Interaction.Hydration=90;
    var input=new InteractionInput(InteractionAction.Press,BodyPart.Side,Force:.3,Duration:.1);
    sim.BeginInteractionGesture(c.Id,input);sim.ContinueInteractionGesture(input,true);double health=c.Health;
    sim.ContinueInteractionGesture(input,false);
    for(int frame=0;frame<15;frame++){sim.SetInteractionHand(3,0,true);Tick(sim,1);}
    Require(c.X>.1&&c.Health>=health&&InteractionPresentation.Pose(c)!="pressure",$"empty mouse hold froze movement or continued pressure: X={c.X}, health={c.Health}/{health}, pose={InteractionPresentation.Pose(c)}");
    sim.EndInteractionGesture();
});
Check("following can reach the rear yard without an invisible inner movement boundary",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();
    c.X=0;c.Z=4.8;c.Fullness=90;c.Energy=90;c.Interaction.Hydration=90;c.Interaction.FollowPlayer=true;
    for(int frame=0;frame<140;frame++){sim.SetInteractionHand(0,7.5,true);Tick(sim,1);}
    Require(c.Z>6.2&&Math.Abs(sim.State.PlayerZ-c.Z)<1.3,$"follow stopped at the old hidden rear boundary: {c.X}/{c.Z}; {c.Activity}; food={c.Fullness}, water={c.Interaction.Hydration}, energy={c.Energy}, follow={c.Interaction.FollowPlayer}, hand={sim.State.PlayerX}/{sim.State.PlayerZ}, time={sim.State.WorldSeconds}/{sim.State.InteractionHandUntil}");
    Require(Math.Abs(c.Z)<=8-WorldSimulation.InteractionFootprint(c)+.00001,"follow crossed the actual body clearance boundary");
});
Check("food in another region cannot be eaten through matching coordinates",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();c.Fullness=10;
    sim.State.Regions.Add(new(){Id="second",Name="第二小院",Kind=RegionKind.Sanctuary});
    var other=new RoomObject{Kind=RoomObjectKind.Meal,RegionId="second",X=c.X+.4,Z=c.Z,Portions=2};sim.State.RoomObjects.Add(other);
    Tick(sim,10);
    Require(other.Portions==2&&c.Fullness<=10&&c.Interaction.EatingRemaining==0,"oil ate a meal from an unseen region");
});
Check("wait practice and placed meals use actual food in the active region",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.RoomObjects.Clear();sim.State.UnlimitedResources=false;
    sim.State.Regions.Add(new(){Id="second",Name="第二小院",Kind=RegionKind.Sanctuary});sim.State.ActiveRegionId=c.RegionId="second";
    var foreign=new RoomObject{Kind=RoomObjectKind.Meal,RegionId="home",X=c.X+.4,Z=c.Z};sim.State.RoomObjects.Add(foreign);int stock=sim.State.Food;
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.TeachWait)).Success,"wait lesson refused");
    Require(sim.State.Food==stock-1&&sim.State.RoomObjects.Any(o=>o.Kind==RoomObjectKind.Meal&&o.RegionId==c.RegionId),"foreign meal substituted for the lesson's actual visible food");
    sim.ExecuteInteraction(c.Id,new(InteractionAction.PlaceSweet,X:c.X+.8,Z:c.Z));
    Require(sim.State.RoomObjects.Any(o=>o.Kind==RoomObjectKind.Sweet&&o.RegionId==c.RegionId),"placed meal silently appeared in the default region");
});
Check("changing active region during a gesture still cleans up the original oil's transient hold",()=>
{
    foreach(var action in new[]{InteractionAction.Hold,InteractionAction.Roll})
    {
        var sim=Fresh();var c=sim.Selected!;sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);
        var input=new InteractionInput(action,BodyPart.Side,Duration:.1);
        Require(sim.BeginInteractionGesture(c.Id,input).Success,"fixture could not start transient hold");
        sim.State.Regions.Add(new(){Id="second",Name="第二小院",Kind=RegionKind.Sanctuary});sim.State.ActiveRegionId="second";sim.State.SelectedId=null;
        Require(!sim.ContinueInteractionGesture(input,true).Success&&!sim.HasInteractionGesture,"region mismatch did not cancel the gesture");
        Require(c.Interaction.Restraint==RestraintKind.None&&!c.Interaction.Rolling,"off-region original oil kept a ghost hand or rolling pose");
        SaveStore.Validate(sim.State);
    }
});
Check("unhanging transfers support and lowers continuously without healing or passive time",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Mode=PlayMode.CloseUp;sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);
    c.Interaction.FootIntegrity=20;sim.ExecuteInteraction(c.Id,new(InteractionAction.Hang));double height=c.Interaction.Height,health=c.Health;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Lower));
    Require(c.Interaction.Carried&&c.Interaction.Lowering&&c.Interaction.Height==height&&!c.Interaction.Falling,"unhanging teleported to ground or lost support");
    sim.AdvanceInteractionPresentation(.1);Require(c.Interaction.Height<height&&c.Interaction.Height>0,"lowering has no intermediate height");
    height=c.Interaction.Height;sim.State.Settings.Pause=true;sim.AdvanceInteractionPresentation(.1);Require(c.Interaction.Height==height,"pause failed to hold support");
    sim.State.Settings.Pause=false;for(int n=0;n<25;n++)sim.AdvanceInteractionPresentation(.1);
    Require(c.Interaction.Height==0&&!c.Interaction.Carried&&!c.Interaction.Lowering&&!c.Interaction.Falling&&c.Interaction.FootIntegrity==20&&c.Health==health&&sim.State.WorldSeconds==0,"lowering healed, fell, or advanced passive time");
});
Check("saving during supported lowering preserves support without replaying its transient motion",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Hang));sim.ExecuteInteraction(c.Id,new(InteractionAction.Lower));sim.AdvanceInteractionPresentation(.1);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;SaveStore.Validate(state);
    sim=new WorldSimulation(state);c=sim.Selected!;double height=c.Interaction.Height;sim.AdvanceInteractionPresentation(.1);
    Require(c.Interaction.Carried&&!c.Interaction.Lowering&&!c.Interaction.Falling&&c.Interaction.Height==height,"load replayed a lowering gesture or dropped its support");
});
Check("taking manual control of supported lowering does not keep moving or drop on cancellation",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Hang));sim.ExecuteInteraction(c.Id,new(InteractionAction.Lower));sim.AdvanceInteractionPresentation(.1);
    double height=c.Interaction.Height;Require(sim.BeginInteractionGesture(c.Id,new(InteractionAction.Throw)).Success,"manual aim refused supported oil");
    sim.AdvanceInteractionPresentation(.1);sim.EndInteractionGesture(true);
    Require(c.Interaction.Carried&&!c.Interaction.Lowering&&!c.Interaction.Falling&&c.Interaction.Height==height,"manual aim kept auto-lowering or cancellation dropped the oil");
});
Check("fixed-seed mixed gestures keep saves valid and leave no ghost hands across cancellation and pause",()=>
{
    var actions=Enum.GetValues<InteractionAction>();int attempted=0,accepted=0;
    for(int seed=1;seed<=12;seed++)
    {
        var random=new Random(71000+seed);var sim=Fresh();
        for(int step=0;step<120;step++)
        {
            if(step>0&&step%30==0)sim=Fresh();
            var c=sim.State.Creatures[random.Next(sim.State.Creatures.Count)];sim.State.SelectedId=c.Id;
            sim.State.Mode=step%3==0?PlayMode.CloseUp:PlayMode.Management;sim.State.Settings.Pause=false;
            var action=actions[random.Next(actions.Length)];var choice=InteractionCatalog.Choice(action);
            var input=new InteractionInput(action,choice.DefaultPart,random.NextDouble(),.1,random.NextDouble()*.2,
                Math.Clamp(c.X+(random.NextDouble()-.5)*2,-7.7,7.7),Math.Clamp(c.Z+(random.NextDouble()-.5)*2,-7.7,7.7),random.NextDouble()*2.2,
                random.NextDouble()*2-1,random.NextDouble()*2-1,(SurfaceSide)random.Next(3));
            try
            {
                var reply=choice.Continuous?sim.BeginInteractionGesture(c.Id,input):sim.ExecuteInteraction(c.Id,input);attempted++;if(reply.Success)accepted++;
                if(choice.Continuous)for(int frame=0;frame<8&&sim.HasInteractionGesture;frame++)
                {
                    sim.ContinueInteractionGesture(input,frame%4!=2);sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);
                }
                if(step%7==0){sim.State.Settings.Pause=true;sim.ContinueInteractionGesture(input,false);}
                sim.EndInteractionGesture(step%2==0);sim.State.Settings.Pause=false;Tick(sim,3);
                Require(sim.State.Creatures.All(o=>o.Interaction.Restraint!=RestraintKind.Hand&&!o.Interaction.Rolling),"transient hand or roll survived gesture cleanup");
                Require(sim.State.Creatures.Count(o=>o.Interaction.Carried)<=1,"multiple oils shared the single support hand");
                SaveStore.Validate(sim.State);
                if(step%10==0)
                {
                    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
                    SaveStore.Validate(state);sim=new WorldSimulation(state);
                }
            }
            catch(Exception e){throw new Exception($"seed {seed}, step {step}, action {action}: {e.Message}");}
        }
    }
    Require(accepted>300,"audit mostly exercised refused operations");
    Console.WriteLine($"Mixed audit: {attempted} attempts, {accepted} accepted; 12 fixed seeds. This is not native play or full action coverage.");
});
var report=new{at=DateTimeOffset.UtcNow,scope="Pure .NET interaction continuity; no game, rendering or native input",success=failures==0,failures,results};
if(args.Length>0){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);File.WriteAllText(args[0],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));}
Console.WriteLine($"Continuity checks: {results.Count-failures}/{results.Count}");return failures==0?0:1;

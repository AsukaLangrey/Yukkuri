using System.Text.Json;
using Yukkuri.Simulation;

// Source-only on the development workstation. Run only on the authorised remote host.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation s)
{SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(Snapshot(s),SaveStore.JsonOptions)!;SaveStore.Validate(state);return new(state);}
WorldSimulation Fixture()
{
    var s=WorldSimulation.CreateInteractionPlaytest(20261031);s.State.RoomObjects.Clear();
    foreach(var c in s.State.Creatures)
    {
        c.Fullness=c.Energy=c.Interaction.Hydration=90;c.Pain=c.Fear=0;c.Courage=.1;
        c.Personality.Initialized=true;c.Personality.Kindness=.5;
        c.Family.NextCareAt=c.Family.NextSocialAt=c.Interaction.NextAutonomyAt=10000;
        c.X=c.TargetX=-6;c.Z=c.TargetZ=-6;
    }
    var a=s.State.Creatures[0];var t=s.State.Creatures[2];
    a.X=a.Z=a.TargetX=a.TargetZ=0;a.Fullness=5;a.Aggression=.5;a.Personality.Kindness=.05;
    t.X=t.TargetX=WorldSimulation.InteractionFootprint(a)+WorldSimulation.InteractionFootprint(t)+.1;
    t.Z=t.TargetZ=0;t.Interaction.FootIntegrity=0;
    // Existing intent and immobile target are explicit component fixtures.
    // All preparation, damage, entry and subsequent clocks use actual APIs.
    a.Personality.HungerTargetId=t.Id;a.Personality.NextHungerStrikeAt=0;
    return s;
}
void Step(WorldSimulation s){s.AdvanceInteractionPresentation(.1);s.Advance(.1);}
void Commit(WorldSimulation s)
{
    for(int n=0;n<150&&!s.State.Creatures[0].Personality.HungerFeeding.Committed;n++)Step(s);
    Require(s.State.Creatures[0].Personality.HungerFeeding.Committed,"actual preparation never entered a portion");
}
void FinishMouth(WorldSimulation s)
{for(int n=0;n<300&&s.State.Creatures[0].Interaction.EatingRemaining>0;n++)s.AdvanceInteractionPresentation(.1);s.AdvanceInteractionPresentation(.1);}
void Reject(WorldSimulation s,Action change)
{
    change();bool rejected=false;try{SaveStore.Validate(s.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"malformed source/contact state was accepted");
}

Check("real preparation precedes one bodily injury and one finite accepted portion",()=>
{
    var s=Fixture();var a=s.State.Creatures[0];var t=s.State.Creatures[2];double trust=t.Trust,harm=t.Interaction.HumanBond.LastHarmAt;
    Step(s);Require(s.HungerFeedingStatus(a)?.Phase==HungerFeedingPhase.Preparing&&a.Personality.HungerFeeding.ContactSeconds==0,
        "first observed contact borrowed time from before its start");
    for(int n=0;n<5;n++)Step(s);
    Require(t.Health==100&&t.Personality.RemainsNutrition==24&&a.Interaction.EatingRemaining==0,"preparation already applied harm or nutrition");
    Commit(s);double health=t.Health;
    Require(Math.Abs(100-health-6.5)<.1&&t.Pain>0&&t.Interaction.Injuries.At(BodyPart.Side)?.Damage>0,"accepted bite omitted or duplicated original injury");
    Require(t.Personality.RemainsNutrition==22&&a.Fullness>6&&a.Personality.HungerFeeding.TakenNutrition==2,"finite live portion was not conserved");
    Require(s.HungerFeedingStatus(a)?.Phase==HungerFeedingPhase.Swallowing&&s.CurrentInteractionThreatTarget(a)==null,"accepted mouthful remained an active attack");
    Require(t.Trust==trust&&t.Interaction.HumanBond.LastHarmAt==harm,"peer bite blamed the human");
    Require(s.State.Incidents.Count(e=>e.Kind==IncidentKind.Attack&&e.ActorId==a.Id&&e.TargetId==t.Id)==1,"one bite produced multiple incidents");
    FinishMouth(s);Require(t.Health==health&&t.Personality.RemainsNutrition==22&&s.HungerFeedingStatus(a)==null,"mouth clock applied another bite or source debit");
    SaveStore.Validate(s.State);
});
Check("human interruption before entry has no food or injury and pause is immutable",()=>
{
    var s=Fixture();var a=s.State.Creatures[0];var t=s.State.Creatures[2];for(int n=0;n<4;n++)Step(s);
    double fullness=a.Fullness;s.State.Settings.Pause=true;string before=Snapshot(s);
    s.InterruptInteractionThreat(a.Id);Step(s);Require(Snapshot(s)==before,"paused interruption or clocks mutated state");
    s.State.Settings.Pause=false;Require(s.InterruptInteractionThreat(a.Id).Contains("已制止"),"actual pending threat could not be stopped");
    Require(a.Personality.HungerTargetId==null&&a.Personality.HungerFeeding.EventId==null&&a.Fullness==fullness&&
        t.Health==100&&t.Personality.RemainsNutrition==24,"stopped preparation granted food or damage");
    Require(a.Personality.NextHungerConflictAt>=s.State.WorldSeconds+30,"interruption omitted its cooldown");
});
Check("a capable nearby guardian can interrupt preparation before the portion exists",()=>
{
    var s=Fixture();var a=s.State.Creatures[0];var t=s.State.Creatures[2];var guardian=s.State.Creatures[1];
    for(int n=0;n<4;n++)Step(s);guardian.Personality.Kindness=.9;guardian.Courage=.9;
    guardian.X=guardian.TargetX=t.X;guardian.Z=guardian.TargetZ=WorldSimulation.InteractionFootprint(t)+WorldSimulation.InteractionFootprint(guardian)+.1;
    Step(s);Require(a.Personality.HungerTargetId==null&&a.Interaction.EatingRemaining==0&&t.Health==100&&t.Personality.RemainsNutrition==24,
        "guardian arrived during preparation but could not stop the bite");
});
Check("separation resets actual contact and a new approach cannot reuse its old preparation",()=>
{
    var s=Fixture();var a=s.State.Creatures[0];var t=s.State.Creatures[2];for(int n=0;n<5;n++)Step(s);
    Require(a.Personality.HungerFeeding.ContactSeconds>0,"no initial partial contact");
    double x=t.X;s.MoveCreature(t.Id,3.5,0);s.AdvanceInteractionPresentation(.1);
    Require(a.Personality.HungerFeeding.ContactSeconds==0&&s.HungerFeedingStatus(a)?.ContactProgress==0,"separation kept borrowed contact");
    s.MoveCreature(t.Id,x,0);Step(s);Require(a.Personality.HungerFeeding.ContactSeconds==0&&t.Health==100,"recontact resumed a stale start");
    SaveStore.Validate(s.State);Commit(s);Require(t.Personality.RemainsNutrition==22,"recontact did not settle exactly once");
});
Check("reachable ground food wins during pursuit but held supplies cannot be eaten remotely",()=>
{
    var s=Fixture();var a=s.State.Creatures[0];var t=s.State.Creatures[2];Step(s);
    var meal=new RoomObject{Id="ordinary-food",RegionId=a.RegionId,Kind=RoomObjectKind.Meal,Portions=1,X=0,Z=-1.2,HeldByHand=true};
    s.State.RoomObjects.Add(meal);Require(s.CurrentInteractionThreatTarget(a)==t,"held food falsely ended a real threat");
    Require(s.ReleaseSupply(meal.Id,0,-1.2),"real supply release refused dry free location");
    Require(s.CurrentInteractionThreatTarget(a)==null,"available reachable ordinary food lost to assault");
    Step(s);Require(a.Personality.HungerTargetId==null&&t.Health==100&&t.Personality.RemainsNutrition==24,"food did not interrupt ongoing preparation");
});
Check("living and dead portions share a finite reserve through actual death and reload",()=>
{
    var s=Fixture();Commit(s);FinishMouth(s);var a=s.State.Creatures[0];var t=s.State.Creatures[2];
    s.MoveCreature(a.Id,-6,0); // Real separation prevents another autonomous portion during death's clock.
    s.Hurt(t,1000,"检查中的实际致死伤害",null,BodyPart.Side);
    for(int n=0;n<400&&t.Condition!=PhysicalCondition.Dead;n++)Step(s);
    Require(t.Condition==PhysicalCondition.Dead&&t.Personality.RemainsNutrition==22,"death replenished or erased the existing finite reserve");
    s=Reload(s);a=s.State.Creatures[0];t=s.State.Creatures[2];
    s.MoveCreature(a.Id,0,0);
    a.Personality.HungerTargetId=t.Id;a.Personality.NextHungerStrikeAt=0;
    Commit(s);Require(a.Personality.HungerFeeding.TakenNutrition==6&&t.Personality.RemainsNutrition==16,"corpse entry used a second unlimited source");
    var copy=Reload(s);Require(copy.State.Creatures[2].Personality.RemainsNutrition==16,"reload reset the consumed reserve");
});
Check("a final fractional live portion scales damage and cannot be taken again",()=>
{
    var s=Fixture();var a=s.State.Creatures[0];var t=s.State.Creatures[2];t.Personality.RemainsNutrition=.4;
    Commit(s);Require(t.Personality.RemainsNutrition==0&&Math.Abs(a.Personality.HungerFeeding.TakenNutrition-.4)<1e-9&&
        Math.Abs(100-t.Health-1.3)<.1,"last finite portion over-damaged or invented food");
    FinishMouth(s);a.Personality.HungerTargetId=t.Id;
    Require(s.CurrentInteractionThreatTarget(a)==null,"empty reserve remained a valid feeding target");
    Step(s);Require(a.Personality.HungerTargetId==null&&t.Personality.RemainsNutrition==0,"empty reserve refilled on a new intent");
});
Check("mouth disability blocks autonomous biting and moderate impairment slows real preparation",()=>
{
    foreach(double impairment in new[]{40d,65d})
    {
        var s=Fixture();var a=s.State.Creatures[0];var t=s.State.Creatures[2];
        a.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Mouth,Damage=impairment,PermanentLoss=impairment});
        for(int n=0;n<10;n++)Step(s);
        if(impairment==65)Require(a.Personality.HungerTargetId==null&&a.Interaction.EatingRemaining==0&&t.Health==100,"disabled mouth initiated a bite");
        else
        {
            Require(t.Health==100&&a.Personality.HungerFeeding.ContactSeconds<.8,"damaged mouth ignored actual preparation rate");
            Commit(s);Require(a.Interaction.EatingRemaining>0&&t.Personality.RemainsNutrition==22,"slower real entry did not use the ordinary mouth clock");
        }
    }
});
Check("pending contact survives save without granting pre-event time and all status reads are pure",()=>
{
    var s=Fixture();Step(s);s=Reload(s);var a=s.State.Creatures[0];
    Require(a.Personality.HungerFeeding.ContactSeconds==0,"load fabricated initial preparation");
    for(int n=0;n<4;n++)Step(s);double contact=a.Personality.HungerFeeding.ContactSeconds;
    s=Reload(s);a=s.State.Creatures[0];Require(a.Personality.HungerFeeding.ContactSeconds==contact,"load changed valid partial contact");
    string before=Snapshot(s);for(int n=0;n<100;n++){_ = s.HungerFeedingStatus(a);_ = s.CurrentInteractionThreatTarget(a);}
    Require(Snapshot(s)==before,"observing contact mutated its time or identity");
    s.State.Mode=PlayMode.CloseUp;double time=s.State.WorldSeconds;for(int n=0;n<20;n++)Step(s);
    Require(s.State.WorldSeconds==time&&a.Personality.HungerFeeding.ContactSeconds==contact,"close-up advanced ordinary attack time");
});
Check("ordinary feeding safely rolls over an exhausted entry sequence",()=>
{
    var s=Fixture();var a=s.State.Creatures[0];s.InterruptInteractionThreat(a.Id);
    a.Interaction.IntakeSequence=long.MaxValue;s.State.UnlimitedResources=false;s.State.Food=1;
    SaveStore.Validate(s.State);
    var input=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,Duration:.1);
    Require(s.BeginInteractionGesture(a.Id,input).Success,"ordinary feeding at maximum counter refused");
    for(int n=0;n<9;n++)s.ContinueInteractionGesture(input,true);
    s.EndInteractionGesture();
    Require(a.Interaction.IntakeSequence==1&&Guid.TryParseExact(a.Interaction.IntakeGeneration,"N",out _)&&s.State.Food==0&&
        a.Interaction.EatingRemaining>0,"ordinary entry overflowed or failed to consume its real portion");
    SaveStore.Validate(s.State);
});
Check("entry generation prevents an old accepted source attaching to a later mouthful",()=>
{
    var s=Fixture();Commit(s);var a=s.State.Creatures[0];var t=s.State.Creatures[2];
    Require(a.Personality.HungerFeeding.IntakeSequence==1,"fixture expected its first actual entry");
    // Explicit exhaustion fixture isolates token collision; not a new food source.
    a.Interaction.IntakeSequence=long.MaxValue;a.Interaction.BeginIntake(IntakeKind.Meal);
    Require(a.Interaction.IntakeSequence==1&&s.HungerFeedingStatus(a)==null&&t.Personality.RemainsNutrition==22,
        "counter rollover relabelled ordinary food as the old bite");
    SaveStore.Validate(s.State);s.AdvanceInteractionPresentation(.1);
    Require(a.Personality.HungerFeeding.EventId==null&&t.Personality.RemainsNutrition==22,"stale source cleared with a refund");
});
Check("invalid contact clocks duplicate event identities and fabricated entry states are rejected",()=>
{
    var original=Fixture();Commit(original);
    foreach(int variant in Enumerable.Range(0,6))
    {
        var s=Reload(original);var f=s.State.Creatures[0].Personality.HungerFeeding;
        Reject(s,()=>
        {
            switch(variant)
            {
                case 0:f.ContactSeconds=double.NaN;break;
                case 1:f.LastContactAt=s.State.WorldSeconds+1;break;
                case 2:f.TakenNutrition=0;break;
                case 3:f.IntakeSequence=s.State.Creatures[0].Interaction.IntakeSequence+1;break;
                case 4:f.IntakeGeneration="not-a-generation";break;
                case 5:
                    s.State.Creatures[1].Personality.HungerFeeding=new(){EventId=f.EventId,TargetId=f.TargetId,StartedAt=s.State.WorldSeconds};
                    s.State.Creatures[1].Personality.HungerTargetId=f.TargetId;break;
            }
        });
    }
});

Check("changing region or target cannot carry preparation to another body",()=>
{
    foreach(bool region in new[]{true,false})
    {
        var s=Fixture();var a=s.State.Creatures[0];var t=s.State.Creatures[2];for(int n=0;n<5;n++)Step(s);
        string? old=a.Personality.HungerFeeding.EventId;
        if(region){s.State.Regions.Add(new(){Id="elsewhere",Name="另一区域"});t.RegionId="elsewhere";}
        else a.Personality.HungerTargetId=s.State.Creatures[1].Id;
        s.AdvanceInteractionPresentation(.1);
        Require(a.Personality.HungerFeeding.EventId==null&&a.Interaction.EatingRemaining==0&&t.Personality.RemainsNutrition==24&&t.Health==100,
            "target or region change reused another body's contact");
        Require(old!=null,"fixture never began a real event");SaveStore.Validate(s.State);
    }
});

Check("kindness and ordinary hunger do not become indiscriminate attacks",()=>
{
    foreach(bool kind in new[]{true,false})
    {
        var s=Fixture();var a=s.State.Creatures[0];var t=s.State.Creatures[2];
        a.Personality.HungerTargetId=null;
        if(kind)a.Personality.Kindness=.8;else a.Fullness=30;
        for(int n=0;n<200;n++)Step(s);
        Require(a.Personality.HungerTargetId==null&&a.Personality.HungerFeeding.EventId==null&&t.Health==100&&
            t.Personality.RemainsNutrition==24,"ordinary hunger or a kind personality became an assault");
    }
});

return failures==0?0:1;

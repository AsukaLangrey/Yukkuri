using System.Text.Json;
using Yukkuri.Simulation;

// Check source only. Run on an independently authorised remote machine, never this workstation.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation s)
{SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(Snapshot(s),SaveStore.JsonOptions)!;SaveStore.Validate(state);return new(state);}
void Step(WorldSimulation s){s.AdvanceInteractionPresentation(.1);s.Advance(.1);}
void Tick(WorldSimulation s,double seconds){for(int n=0;n<(int)Math.Ceiling(seconds*10);n++)Step(s);}
(double Care,double Familiarity,double Affection) Earned(Creature from,Creature to)=>
    from.Family.Peers.TryGetValue(to.Id,out var bond)?(bond.CareSeconds,bond.Familiarity,bond.Affection):(0,0,0);
void Meal(WorldSimulation s,Creature c)
{
    if(s.State.Food==0)Require(s.BuyFood(),"real food replenishment failed");
    var input=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,Duration:.1);
    Require(s.BeginInteractionGesture(c.Id,input).Success,"actual meal could not begin");
    for(int n=0;n<12&&!s.InteractionIntakeAccepted;n++){s.ContinueInteractionGesture(input,true);Step(s);}
    Require(s.InteractionIntakeAccepted,"meal did not actually enter");s.EndInteractionGesture();
    for(int n=0;n<100&&c.Interaction.EatingRemaining>0;n++)Step(s);
    Require(c.Interaction.EatingRemaining<=0,"meal did not finish its real mouth clock");
}
WorldSimulation AfterHarm(string need="warmth",bool adultRecipient=false,bool established=false)
{
    for(ulong seed=1;seed<=64;seed++)
    {
        var s=WorldSimulation.CreateInteractionPlaytest(seed);s.State.RoomObjects.Clear();s.State.PlayerX=0;s.State.PlayerZ=7;
        for(int n=0;n<s.State.Creatures.Count;n++)
        {
            var c=s.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
            c.Fullness=c.Energy=c.Interaction.Hydration=90;c.Pain=c.Fear=0;c.Courage=.1;
            c.Personality.Initialized=true;c.Personality.Kindness=.8;c.Aggression=.1;c.Patience=.8;
            c.Family.NextCareAt=c.Family.NextSocialAt=c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
            c.X=c.TargetX=n==0?0:-6;c.Z=c.TargetZ=n==0?0:-6;
        }
        var a=s.State.Creatures[0];var helper=s.State.Creatures[1];var r=s.State.Creatures[2];
        r.IsYoung=!adultRecipient;r.Body.AgeSeconds=r.InnerAgeSeconds=adultRecipient?2000:0;
        // Initial dependency and a pending dangerous intent are component
        // preconditions. Harm, its identity, stopping it and later care are real.
        r.Interaction.FootIntegrity=0;r.Family.Warmth=20;
        r.ParentId=r.Family.MotherId=a.Id;r.Family.FatherId=helper.Id;r.Family.BirthMethod=BirthMethod.Stem;
        r.X=r.TargetX=WorldSimulation.InteractionFootprint(a)+WorldSimulation.InteractionFootprint(r)+.1;r.Z=r.TargetZ=0;
        r.Fullness=need=="meal"?35:90;r.Interaction.Hydration=need=="water"?35:90;
        if(established)r.Family.CaregiverIds.Add(a.Id);
        a.Aggression=.5;a.Fullness=adultRecipient?5:19;
        if(adultRecipient)
        {a.Personality.Kindness=.05;a.Personality.HungerTargetId=r.Id;a.Personality.NextHungerStrikeAt=0;}
        else a.Family.ChildThreat=new(){TargetId=r.Id,WarningRemaining=0,Remaining=45,NextCheckAt=120,Cause="饥饿与求生压力"};
        for(int n=0;n<100&&!r.Family.Peers.ContainsKey(a.Id);n++)Step(s);
        Require(r.Family.Peers.TryGetValue(a.Id,out var memory)&&memory.LastHarmAt>=0&&r.Health<100&&r.Pain>0,
            "actual strike failed to record injury and source-specific memory");
        if(adultRecipient)
        {
            for(int n=0;n<160&&s.CurrentInteractionThreatTarget(a)==null;n++)Step(s);
            if(s.CurrentInteractionThreatTarget(a)!=r)continue;
        }
        Require(s.InterruptInteractionThreat(a.Id).Contains("已制止"),"actual danger could not be interrupted");
        int stock=s.State.Food,meals=adultRecipient?4:3;
        for(int meal=0;meal<meals;meal++)Meal(s,a);
        Require(s.State.Food==stock-meals&&a.Fullness>=70,"caregiver needs were not resolved with actual portions");
        Require(s.State.Incidents.Count(e=>e.Kind==IncidentKind.Attack&&e.ActorId==a.Id&&e.TargetId==r.Id)==1,
            "fixture suffered a second strike instead of a timely interruption");
        if(s.State.WorldSeconds-memory!.LastHarmAt<29&&FamilyRules.WillSocialize(a))return s;
    }
    throw new Exception("no actual interrupted early-recovery fixture was reached");
}
void BringBeside(WorldSimulation s,Creature caregiver,Creature recipient)
{
    // Waiting for recovery does not suspend ordinary wandering. Rejoin through
    // actual supported placement before testing close physical warmth/care.
    double gap=WorldSimulation.InteractionFootprint(caregiver)+WorldSimulation.InteractionFootprint(recipient)+.1;
    Require(s.BeginInteractionGesture(caregiver.Id,new(InteractionAction.Lift,BodyPart.Side,.1,Height:.8)).Success,
        "caregiver could not actually be lifted to rejoin the recipient");
    Require(s.ExecuteInteraction(caregiver.Id,new(InteractionAction.PutDown,BodyPart.Side,X:recipient.X,Z:recipient.Z+gap)).Success,
        "caregiver could not actually be placed beside the recipient");
    s.EndInteractionGesture();
    for(int n=0;n<100&&(caregiver.Interaction.Falling||caregiver.Interaction.Lowering||caregiver.Interaction.Height>.05);n++)Step(s);
    Require(!caregiver.Interaction.Falling&&!caregiver.Interaction.Lowering&&caregiver.Interaction.Height<=.05&&
        WorldSimulation.Distance(caregiver.X,caregiver.Z,recipient.X,recipient.Z)<=gap+.05,
        "real caregiver placement never established nearby ground contact");
}
void Arrange(WorldSimulation s,Creature caregiver,Creature recipient)
{
    Require(s.FamilyCareReason(caregiver.Id,recipient.Id)==null,"necessary physical care was refused: "+s.FamilyCareReason(caregiver.Id,recipient.Id));
    s.ArrangeCare(caregiver.Id,recipient.Id);Require(caregiver.Family.CareTargetId==recipient.Id,"care was not arranged");
}
RoomObject PlaceSupply(WorldSimulation s,Creature a,bool water)
{
    int stock=s.State.Food;var before=s.State.RoomObjects.Select(o=>o.Id).ToHashSet();
    Require(s.ExecuteInteraction(a.Id,new(water?InteractionAction.PlaceWater:InteractionAction.PlaceMeal,X:a.X,Z:a.Z-1.2)).Success,
        "real supply placement failed");
    Require(s.State.Food==stock-(water?0:1),"supply placement charged the wrong real inventory");
    return s.State.RoomObjects.Single(o=>!before.Contains(o.Id));
}
RoomObject TakeParcel(WorldSimulation s,Creature a)
{
    for(int n=0;n<150;n++)
    {
        Step(s);
        if(a.Family.CareParcelId is { } id&&s.State.RoomObjects.Find(o=>o.Id==id&&o.CarrierId==a.Id) is { } parcel)return parcel;
    }
    throw new Exception("caregiver never took a real parcel: "+a.Family.CareLastEvent);
}
void FinishDelivery(WorldSimulation s,Creature a,Creature r,RoomObject parcel,bool water)
{
    double before=water?r.Interaction.Hydration:r.Fullness;int exposures=r.Interaction.Digestion.Exposures;
    bool placed=false,pending=false,accepted=false;
    for(int n=0;n<300;n++)
    {
        Step(s);
        placed|=parcel.CarrierId==null&&WorldSimulation.Distance(r.X,r.Z,parcel.X,parcel.Z)<=NaturalResources.Reach(r);
        if(r.Interaction.GroundIntakeSourceId==parcel.Id&&r.Interaction.EatingRemaining>0)
        {pending|=r.Interaction.NaturalIntakePending;accepted|=!r.Interaction.NaturalIntakePending;}
        if(accepted&&r.Interaction.EatingRemaining<=0)break;
    }
    Require(placed&&pending&&accepted&&r.Interaction.EatingRemaining<=0,
        "real placement, reservation, entry and swallowing did not all happen: "+a.Family.CareLastEvent);
    Require((water?r.Interaction.Hydration:r.Fullness)>before&&r.Interaction.Digestion.Exposures==exposures+1&&parcel.Portions==0,
        "delivered portion was not accepted exactly once");
}

Check("recent harmful parent still provides real warmth without earning trust or stable-care credit",()=>
{
    foreach(bool established in new[]{false,true})
    {
        var s=AfterHarm(established:established);var a=s.State.Creatures[0];var r=s.State.Creatures[2];
        var beforeA=Earned(a,r);var beforeR=Earned(r,a);double warmth=r.Family.Warmth,harm=r.Family.Peers[a.Id].LastHarmAt;
        string? mother=r.Family.MotherId,parent=r.ParentId;Arrange(s,a,r);Tick(s,5);
        Require(r.Family.Warmth>warmth&&a.Family.CareLastEvent.Contains("照看"),"actual nearby physical care was blocked or lost its record");
        Require(Earned(a,r)==beforeA&&Earned(r,a)==beforeR&&!a.Family.Peers.ContainsKey(r.Id),
            "recently harmful care created a bond or earned relationship credit");
        Require(r.Family.CaregiverIds.Contains(a.Id)==established&&r.Family.MotherId==mother&&r.ParentId==parent&&
            r.Family.Peers[a.Id].LastHarmAt==harm&&r.Interaction.Emotion.Alarm>0,
            "care erased history, rewrote family or treated the aggressor as a new stable caregiver");
    }
});
Check("real meal and water delivery still enter dependent children and adults without rewarding the recent aggressor",()=>
{
    foreach(bool adult in new[]{false,true})foreach(bool water in new[]{false,true})
    {
        var s=AfterHarm(water?"water":"meal",adult);var a=s.State.Creatures[0];var r=s.State.Creatures[2];
        var beforeA=Earned(a,r);var beforeR=Earned(r,a);double harm=r.Family.Peers[a.Id].LastHarmAt;
        var source=PlaceSupply(s,a,water);Arrange(s,a,r);var parcel=TakeParcel(s,a);
        Require(source.Portions==0&&parcel.Portions==1&&parcel.Id!=source.Id,"delivery duplicated rather than moved one real portion");
        FinishDelivery(s,a,r,parcel,water);
        Require(s.State.WorldSeconds-harm<120&&!EmotionalReadiness.Settled(r),"delivery missed the recent unsettled recovery window");
        Require(Earned(a,r)==beforeA&&Earned(r,a)==beforeR&&!a.Family.Peers.ContainsKey(r.Id),
            "necessary delivered food bought care/affection credit during recent harmful aftermath");
        Require(s.State.RoomObjects.Where(o=>o.Kind==source.Kind).Sum(o=>o.Portions)==0,"one consumed delivered portion was duplicated");
    }
});
Check("safe other parent remains eligible for real physical care and ordinary relationship credit",()=>
{
    var s=AfterHarm();var helper=s.State.Creatures[1];var r=s.State.Creatures[2];
    double gap=WorldSimulation.InteractionFootprint(helper)+WorldSimulation.InteractionFootprint(r)+.1;
    Require(s.BeginInteractionGesture(helper.Id,new(InteractionAction.Lift,BodyPart.Side,.1,Height:.8)).Success,"safe parent's actual lift failed");
    Require(s.ExecuteInteraction(helper.Id,new(InteractionAction.PutDown,BodyPart.Side,X:r.X,Z:r.Z+gap)).Success,"safe parent's actual placement failed");
    s.EndInteractionGesture();for(int n=0;n<100&&(helper.Interaction.Falling||helper.Interaction.Height>.05);n++)Step(s);
    Arrange(s,helper,r);double warmth=r.Family.Warmth;Tick(s,4);
    Require(Earned(helper,r).Care>0&&Earned(r,helper).Care>0&&Earned(r,helper).Affection>0&&r.Family.Warmth>warmth,
        "another object's harm globally blocked this safe parent's care");
    Require(r.Family.Peers[s.State.Creatures[0].Id].LastHarmAt>=0&&r.Interaction.Emotion.Alarm>0,
        "safe care silently cleared the separate aggressor's history");
});
Check("care remains physical after thirty seconds while the same recent injury is still unsettled",()=>
{
    var s=AfterHarm();var a=s.State.Creatures[0];var r=s.State.Creatures[2];double harm=r.Family.Peers[a.Id].LastHarmAt;
    while(s.State.WorldSeconds-harm<35)Step(s);
    BringBeside(s,a,r);
    Require(!EmotionalReadiness.Settled(r),"the actual substantial injury unexpectedly lost its aftermath");
    var beforeA=Earned(a,r);var beforeR=Earned(r,a);double warmth=r.Family.Warmth;
    Arrange(s,a,r);Tick(s,4);
    Require(r.Family.Warmth>warmth&&Earned(a,r)==beforeA&&Earned(r,a)==beforeR,
        "thirty seconds alone converted necessary care into forgiveness"+
        $" warmth={warmth}->{r.Family.Warmth}, A={beforeA}->{Earned(a,r)}, R={beforeR}->{Earned(r,a)}, positions={a.X},{a.Z}/{r.X},{r.Z}, activity={a.Activity}, care={a.Family.CareLastEvent}");
});
Check("paused and reloaded transport preserves the portion and the original harmful-care restriction",()=>
{
    var s=AfterHarm("meal");var a=s.State.Creatures[0];var r=s.State.Creatures[2];PlaceSupply(s,a,false);Arrange(s,a,r);
    var parcel=TakeParcel(s,a);string id=parcel.Id;var beforeA=Earned(a,r);var beforeR=Earned(r,a);
    s.State.Settings.Pause=true;string before=Snapshot(s);s.ArrangeCare(a.Id,r.Id);Tick(s,2);
    Require(Snapshot(s)==before,"pause mutated physical care or earned relationship");
    s.State.Settings.Pause=false;s=Reload(s);a=s.State.Creatures[0];r=s.State.Creatures[2];parcel=s.State.RoomObjects.Single(o=>o.Id==id);
    Require(a.Family.CareParcelId==id&&parcel.CarrierId==a.Id&&parcel.Portions==1,"reload duplicated or lost the in-transit portion");
    FinishDelivery(s,a,r,parcel,false);
    Require(Earned(a,r)==beforeA&&Earned(r,a)==beforeR&&r.Family.Peers[a.Id].LastHarmAt>=0,
        "reload forgot the same object's recent harm restriction");
    SaveStore.Validate(s.State);
});
Check("real safe recovery and subsequent sustained care can establish a stable relationship without lowering its threshold",()=>
{
    var s=AfterHarm();var a=s.State.Creatures[0];var r=s.State.Creatures[2];double harmedAt=r.Family.Peers[a.Id].LastHarmAt;
    while(s.State.WorldSeconds-harmedAt<150||!EmotionalReadiness.Settled(r))
    {
        Require(s.State.WorldSeconds-harmedAt<600,"ordinary safety did not settle the actual injury");
        if(a.Fullness<75)Meal(s,a);Tick(s,1);
    }
    Require(Earned(a,r).Care==0&&!r.Family.CaregiverIds.Contains(a.Id),"waiting alone earned stable care credit");
    BringBeside(s,a,r);
    double start=s.State.WorldSeconds;
    while(!r.Family.CaregiverIds.Contains(a.Id)&&s.State.WorldSeconds-start<180)
    {
        if(a.Fullness<75)Meal(s,a);
        if(a.Family.CareTargetId==null)Arrange(s,a,r);
        Step(s);
        if(Earned(a,r).Care<60)Require(!r.Family.CaregiverIds.Contains(a.Id),"stable caregiver formed below the original sixty seconds");
    }
    Require(r.Family.CaregiverIds.Contains(a.Id)&&Earned(a,r).Care>=60&&Earned(r,a).Affection>0&&
        r.Family.Peers[a.Id].LastHarmAt==harmedAt&&r.Family.MotherId==a.Id,
        "genuine later care was permanently blocked or erased the old harm/family identity");
    SaveStore.Validate(s.State);
});
Environment.ExitCode=failures==0?0:1;

using System.Text.Json;
using Yukkuri.Simulation;

// Source-only on the development workstation. Execute only on the authorised remote host.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation s)
{SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(Snapshot(s),SaveStore.JsonOptions)!;SaveStore.Validate(state);return new(state);}
void Step(WorldSimulation s){s.AdvanceInteractionPresentation(.1);s.Advance(.1);}
void Tick(WorldSimulation s,double seconds){for(int n=0;n<(int)Math.Ceiling(seconds*10);n++)Step(s);}
void FinishMouth(WorldSimulation s,Creature c)
{for(int n=0;n<200&&c.Interaction.EatingRemaining>0;n++)Step(s);Require(c.Interaction.EatingRemaining<=0,"mouthful never finished");}
void Meal(WorldSimulation s,Creature c)
{
    if(s.State.Food==0)Require(s.BuyFood(),"actual free food purchase failed");
    var input=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,Duration:.1);
    Require(s.BeginInteractionGesture(c.Id,input).Success,"real food offer refused at begin");
    for(int n=0;n<12&&!s.InteractionIntakeAccepted;n++)
    {s.ContinueInteractionGesture(input,true);Step(s);}
    Require(s.InteractionIntakeAccepted,"real offered food did not enter");
    s.EndInteractionGesture();FinishMouth(s,c);
}
WorldSimulation Fresh(ulong seed)
{
    var s=WorldSimulation.CreateInteractionPlaytest(seed);s.State.RoomObjects.Clear();
    s.State.PlayerX=0;s.State.PlayerZ=7;
    for(int n=0;n<s.State.Creatures.Count;n++)
    {
        var c=s.State.Creatures[n];
        c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=90;c.Pain=c.Fear=0;c.Courage=.1;
        c.Personality.Initialized=true;c.Personality.Kindness=.8;
        c.Family.NextCareAt=c.Family.NextSocialAt=c.Interaction.NextAutonomyAt=10000;
        c.X=c.TargetX=n==0?0:n==1?-6:1.6;c.Z=c.TargetZ=n==1?-6:0;
    }
    return s;
}
WorldSimulation HarmStoppedAndFed()
{
    // Component preconditions: one existing hunger intent, a victim unable to
    // walk, and a previously familiar harmless companion outside the incident.
    // No alarm, fear, injury, harm timestamp or post-incident relationship is filled in.
    // Try actual seeded continuations until a second real warning is available
    // in the early recovery window; interrupt it before a second bite.
    for(ulong seed=1;seed<=64;seed++)
    {
        var s=Fresh(seed);var a=s.State.Creatures[0];var helper=s.State.Creatures[1];var v=s.State.Creatures[2];
        a.Fullness=5;a.Aggression=.5;a.Personality.Kindness=.05;
        v.Interaction.FootIntegrity=0;v.X=v.TargetX=WorldSimulation.InteractionFootprint(a)+WorldSimulation.InteractionFootprint(v)+.1;
        v.Family.Peers[helper.Id]=new(){Familiarity=50,Affection=40};
        helper.Family.Peers[v.Id]=new(){Familiarity=50,Affection=40};
        a.Personality.HungerTargetId=v.Id;a.Personality.NextHungerStrikeAt=0;
        for(int n=0;n<100&&!a.Personality.HungerFeeding.Committed;n++)Step(s);
        Require(a.Personality.HungerFeeding.Committed,"real first bite never entered");
        double harmedAt=v.Family.Peers[a.Id].LastHarmAt;
        for(int n=0;n<160&&s.CurrentInteractionThreatTarget(a)==null;n++)Step(s);
        if(s.CurrentInteractionThreatTarget(a)!=v)continue;
        Require(s.InterruptInteractionThreat(a.Id).Contains("已制止"),"actual renewed threat could not be stopped");
        int stock=s.State.Food;for(int meal=0;meal<4;meal++)Meal(s,a);
        Require(s.State.Food==stock-4&&a.Fullness>=70,"feeding bypassed actual food or did not resolve the unmet need");
        Require(s.State.Incidents.Count(e=>e.Kind==IncidentKind.Attack&&e.ActorId==a.Id&&e.TargetId==v.Id)==1,
            "fixture allowed another bite before interruption");
        Require(v.Health<100&&v.Interaction.Injuries.At(BodyPart.Side)?.Damage>0&&v.Interaction.Emotion.Alarm>0,
            "actual bite did not preserve bodily and emotional consequences");
        Require(v.Family.Peers[a.Id].LastHarmAt==harmedAt&&v.Interaction.HumanBond.LastHarmAt<0,
            "feeding/interruption erased peer identity or blamed human harm");
        if(s.State.WorldSeconds-harmedAt<29&&FamilyRules.WillSocialize(a)&&FamilyRules.WillSocialize(v))return s;
    }
    throw new Exception("no seeded real continuation reached the early, fed, interrupted fixture");
}
void PlaceBeside(WorldSimulation s,Creature c,Creature other)
{
    double gap=WorldSimulation.InteractionFootprint(c)+WorldSimulation.InteractionFootprint(other)+.1;
    Require(s.BeginInteractionGesture(c.Id,new(InteractionAction.Lift,BodyPart.Side,.1,Height:.8)).Success,"actual lift failed");
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.PutDown,BodyPart.Side,X:other.X,Z:other.Z+gap)).Success,"actual placement failed");
    s.EndInteractionGesture();
    for(int n=0;n<50&&(c.Interaction.Falling||c.Interaction.Height>.1);n++)Step(s);
    Require(!c.Interaction.Falling&&c.Interaction.Height<=.1,"placement never reached the ground");
}

Check("real bite then interruption and food does not buy immediate renewed companionship",()=>
{
    var s=HarmStoppedAndFed();var a=s.State.Creatures[0];var v=s.State.Creatures[2];
    string before=Snapshot(s);string response=s.InviteCompanion(a.Id,v.Id);
    Require(response.Contains("伤害")&&Snapshot(s)==before,"recent aggressor refusal changed state, clock or random stream");
    s.InviteCompanion(v.Id,a.Id);Require(Snapshot(s)==before,"reversing inviter bypassed the victim's harm memory");
    Require(v.Family.CompanionTargetId==null&&a.Family.CompanionTargetId==null,"food converted the aggressor into safe company");
});
Check("the existing thirty second affection floor does not erase still unsettled recent harm",()=>
{
    var s=HarmStoppedAndFed();var a=s.State.Creatures[0];var v=s.State.Creatures[2];double harm=v.Family.Peers[a.Id].LastHarmAt;
    while(s.State.WorldSeconds-harm<35)Step(s);
    Require(!EmotionalReadiness.Settled(v),"real injury fixture unexpectedly lost its alarm");
    string before=Snapshot(s);Require(s.InviteCompanion(a.Id,v.Id).Contains("伤害")&&Snapshot(s)==before,
        "thirty seconds alone overrode ongoing recent alarm");
});
Check("a harmless familiar companion can stay with the injured victim without cheerful feedback",()=>
{
    var s=HarmStoppedAndFed();var helper=s.State.Creatures[1];var v=s.State.Creatures[2];PlaceBeside(s,helper,v);
    Require(!EmotionalReadiness.Settled(v),"placement prematurely erased the actual aftermath");
    Require(s.InviteCompanion(helper.Id,v.Id).Contains("愿意"),"unrelated safe companion was blocked by another creature's harm");
    int serial=v.ReactionSerial;double alarm=v.Interaction.Emotion.Alarm;
    for(int n=0;n<40&&!(v.ReactionSerial>serial&&v.Speech.Contains("陪我待一会儿"));n++)Step(s);
    Require(v.Family.CompanionTargetId==helper.Id&&v.ReactionSerial>serial&&v.Expression==Expression.Worried&&v.Speech.Contains("陪我待一会儿"),
        "real safe companionship was absent or changed injured victim directly to happy");
    Require(v.Interaction.Emotion.Alarm>0&&v.Interaction.Emotion.Alarm<=alarm,"companionship cleared or invented alarm");
});
Check("automatic selection cannot choose the recently interrupted aggressor",()=>
{
    var s=HarmStoppedAndFed();var a=s.State.Creatures[0];var v=s.State.Creatures[2];
    // Scheduling eligibility is the explicit component input; actual pair
    // selection still uses the real remembered bite and current body state.
    // Isolate the social selection from the unrelated idle-walk destination
    // accumulated during real feeding. Mobility and social movement remain enabled.
    a.DecisionIn=10000;a.TargetX=a.X;a.TargetZ=a.Z;
    a.Family.NextSocialAt=0;double x=a.X,z=a.Z;Tick(s,.2);
    Require(a.Family.NextSocialAt>s.State.WorldSeconds&&a.Family.CompanionTargetId==null&&v.Family.CompanionTargetId==null,
        "automatic selection skipped its memory gate or the fixture never reached selection");
    Require(a.X==x&&a.Z==z,"failed automatic invitation moved toward its recent victim"+
        $" position={x},{z}->{a.X},{a.Z}, target={a.TargetX},{a.TargetZ}, activity={a.Activity}, decision={a.DecisionIn}");
});
Check("a stale saved companionship cannot continue movement or gain after actual harm",()=>
{
    var s=HarmStoppedAndFed();var a=s.State.Creatures[0];var v=s.State.Creatures[2];
    // An old save may contain a pairing accepted by the previous rules. Keep
    // the real injury, memories and relationship values; only restore that stale intent.
    a.Family.CompanionTargetId=v.Id;v.Family.CompanionTargetId=a.Id;
    a.Family.CompanionUntil=v.Family.CompanionUntil=s.State.WorldSeconds+24;
    s=Reload(s);a=s.State.Creatures[0];v=s.State.Creatures[2];
    double familiarity=v.Family.Peers[a.Id].Familiarity,affection=v.Family.Peers[a.Id].Affection;
    Tick(s,.2);
    Require(a.Family.CompanionTargetId==null&&v.Family.CompanionTargetId==null&&
        v.Family.Peers[a.Id].Familiarity==familiarity&&v.Family.Peers[a.Id].Affection==affection,
        "stale companionship accrued contact/relationship after the remembered strike");
});
Check("pause and save preserve object-specific refusal without synthesising a new harm",()=>
{
    var s=HarmStoppedAndFed();s.State.Settings.Pause=true;string before=Snapshot(s);
    s.InviteCompanion(s.State.Creatures[0].Id,s.State.Creatures[2].Id);Tick(s,3);
    Require(Snapshot(s)==before,"pause advanced companionship or remembered harm");
    s.State.Settings.Pause=false;s=Reload(s);before=Snapshot(s);
    Require(s.InviteCompanion(s.State.Creatures[0].Id,s.State.Creatures[2].Id).Contains("伤害")&&Snapshot(s)==before,
        "reload lost the refusal or re-applied harm");
});
Check("unharmed strangers still build a relationship through actual ordinary company",()=>
{
    var s=Fresh(55);var a=s.State.Creatures[0];var b=s.State.Creatures[2];
    Require(a.Family.Peers.Count==0&&b.Family.Peers.Count==0,"fixture started with manufactured affection");
    Require(s.InviteCompanion(a.Id,b.Id).Contains("愿意"),"default negative harm timestamp blocked a stranger");
    Tick(s,4);
    Require(a.Family.Peers[b.Id].Affection>0&&b.Family.Peers[a.Id].Affection>0&&
        a.Family.Peers[b.Id].LastHarmAt<0&&b.Family.Peers[a.Id].LastHarmAt<0,
        "actual safe company could not develop normally");
    SaveStore.Validate(s.State);
});
Check("old recovered harm does not become a permanent ban after an unrelated fright",()=>
{
    var s=HarmStoppedAndFed();var a=s.State.Creatures[0];var v=s.State.Creatures[2];
    double harmedAt=v.Family.Peers[a.Id].LastHarmAt;
    // Keep actual food needs satisfied through the same ordinary meal API;
    // do not fill health, emotional safety or relationship values to recover.
    while(s.State.WorldSeconds-harmedAt<150||!EmotionalReadiness.Settled(v))
    {
        Require(s.State.WorldSeconds-harmedAt<600,"ordinary safe time failed to settle this injury");
        if(a.Fullness<75)Meal(s,a);
        Tick(s,1);
    }
    // This is a separate human-caused fright, not a new peer injury. The old
    // bond remains untouched; its old timestamp cannot accuse a new offender.
    Require(s.ExecuteInteraction(v.Id,new(InteractionAction.Punch,BodyPart.Side,.1)).Success,"new real fright was not applied");
    Require(v.Family.Peers[a.Id].LastHarmAt==harmedAt&&!EmotionalReadiness.Settled(v),"unrelated fright rewrote the peer's harm identity");
    // A punch may briefly put the victim above the ordinary physical social
    // threshold. Let that real reaction/body recover, without erasing alarm.
    for(int n=0;n<300&&!FamilyRules.WillSocialize(v);n++)Step(s);
    Require(FamilyRules.WillSocialize(v)&&s.InviteCompanion(a.Id,v.Id).Contains("愿意"),
        "ancient peer harm was treated as a permanent bar after a later human action");
});
Environment.ExitCode=failures==0?0:1;

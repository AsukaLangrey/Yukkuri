using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Require(bool condition,string why){if(!condition)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation sim)=>JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(Snapshot(sim),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void Seconds(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Ceiling(seconds/.25);n++){sim.AdvanceInteractionPresentation(.1);sim.AdvanceInteractionPresentation(.1);sim.AdvanceInteractionPresentation(.05);sim.Advance(.25);}}
WorldSimulation Fresh()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var target=sim.Selected!;
    sim.State.Creatures.RemoveAll(c=>c!=target);
    // RiverWater also declares the actual river geometry. Clearing it would
    // leave X=7.1 on ordinary ground, with no immersion or rescue to observe.
    sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
    target.X=7.1;target.Z=0;target.TargetX=target.X;target.TargetZ=0;
    // Deep-water component fixture: no ability to self-jump out before rescue.
    target.Interaction.FootIntegrity=0;target.Fullness=90;target.Energy=90;target.Interaction.Hydration=90;
    target.Interaction.NextAutonomyAt=10000;target.Family.NextSocialAt=10000;
    return sim;
}
Creature Neighbor(WorldSimulation sim,double kindness=.8,double x=4.7,double z=1.5)
{
    var c=new Creature{Name="岸边同伴",X=x,Z=z,TargetX=x,TargetZ=z,Fullness=90,Energy=90,
        Body=new(){SpeciesId="reimu",AgeSeconds=2000},
        Interaction=new(){Enabled=true,Hydration=90,NextAutonomyAt=10000},
        Personality=new(){Initialized=true,Kindness=kindness,Pride=1-kindness},
        Family=new(){NextSocialAt=10000,NextCareAt=10000}};
    sim.State.Creatures.Add(c);return c;
}

Check("one immersion has stable cause and identity across save and a new immersion receives a new identity",()=>
{
    var sim=Fresh();Seconds(sim,.25);var c=sim.Selected!;string id=c.Interaction.Water.EventId!;
    Require(id.Length>0&&!c.Interaction.Water.PlayerCaused,"natural immersion identity or cause wrong");
    var copy=Reload(sim);Require(copy.Selected!.Interaction.Water.EventId==id,"load replaced water episode");
    Require(sim.RescueFromWater(c.Id).Success,"actual rescue failed");
    Require(c.Interaction.Water.HumanRescueCredited,"rescue event not settled");
    c.Interaction.Carried=true;c.Interaction.Height=1;
    sim.State.FamilySequence=0; // A valid old counter cannot recycle an existing water identity.
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.PutDown,X:7.1,Z:0)).Success,"actual release failed");
    Require(c.Interaction.Water.EventId!=id&&c.Interaction.Water.PlayerCaused&&!c.Interaction.Water.HumanRescueCredited,"new player-caused immersion reused old credit");
    Reload(sim);
});

Check("hungry helpers call without reserving their movement or blocking real food",()=>
{
    var sim=Fresh();var helper=Neighbor(sim);helper.Fullness=5;
    var food=new RoomObject{Kind=RoomObjectKind.Meal,RegionId=helper.RegionId,X=helper.X,Z=helper.Z-.9,Portions=3};
    sim.State.RoomObjects.Add(food);Seconds(sim,.25);
    Require(sim.WaterSupportStatus(helper)?.Phase!=WaterSupportPhase.Responding&&helper.Personality.NextWaterResponseAt>0,
        "hungry helper claimed active rescue or never requested help before eating");
    Seconds(sim,25);
    Require(food.Portions<3&&helper.Fullness>5,"water concern starved helper instead of allowing real eating");
    Require(helper.Personality.NextWaterResponseAt>0,"helper never made bounded help request");
});

Check("actual witnesses keep short shore concern while mean and remote creatures do not acquire it",()=>
{
    var sim=Fresh();var good=Neighbor(sim);
    // Keep this witness visible without occupying every legal rescue landing.
    // A body at the old (4.5,-1.7) plus the approaching good witness blocks them all.
    var mean=Neighbor(sim,.05,3.3,-1.7);var far=Neighbor(sim,.8,-6,-5);
    Seconds(sim,.25);string eventId=sim.Selected!.Interaction.Water.EventId!;
    Require(sim.WaterConcernTarget(good)==sim.Selected,"good helper did not notice actual water");
    var rescue=sim.RescueFromWater(sim.Selected.Id);Require(rescue.Success,"rescue failed: "+rescue.Message);
    Require(sim.WaterConcernTarget(good)==null,"shore survivor still represented as drowning");
    Require(good.Personality.WaterAftercare.EventId==eventId&&mean.Personality.WaterAftercare.TargetId==null&&far.Personality.WaterAftercare.TargetId==null,"aftercare ignored participation or personality");
    bool observed=false;
    for(int n=0;n<100;n++)
    {
        Seconds(sim,.25);
        if(sim.WaterSupportStatus(good)?.Phase==WaterSupportPhase.Watching)observed=true;
    }
    Require(observed&&good.Personality.WaterAftercare.Remembered,"actual approach never became sustained nearby care");
    var bond=sim.Selected.Family.Peers[good.Id];double affection=bond.Affection;
    Seconds(sim,8);Require(sim.WaterSupportStatus(good)==null&&bond.Affection==affection,"completed short care repeated reward or remained forever");
});

Check("shore contact yields to child care and hunger without false contact accumulation",()=>
{
    var sim=Fresh();var helper=Neighbor(sim);Seconds(sim,.25);Require(sim.RescueFromWater(sim.Selected!.Id).Success,"rescue failed");
    var child=Neighbor(sim,.8,helper.X-1.5,helper.Z);child.IsYoung=true;child.Body.AgeSeconds=0;child.Family.MotherId=helper.Id;
    child.Fullness=20;
    Require(sim.WaterSupportStatus(helper)==null,"hungry dependent child lost priority to shore watching");
    Seconds(sim,1);Require(helper.Personality.WaterAftercare.ContactSeconds==0,"interrupted care accumulated invisible contact");
    helper.Fullness=8;Require(sim.WaterSupportStatus(helper)==null,"hungry guardian remained in care presentation");
});

Check("actual companionship interrupts unfinished shore contact and only later uninterrupted care earns its reward",()=>
{
    var sim=Fresh();var target=sim.Selected!;var helper=Neighbor(sim);Seconds(sim,.25);
    Require(sim.RescueFromWater(target.Id).Success,"rescue failed");
    for(int n=0;n<60&&helper.Personality.WaterAftercare.ContactSeconds<2;n++)Seconds(sim,.25);
    Require(helper.Personality.WaterAftercare.ContactSeconds>=2&&!helper.Personality.WaterAftercare.Remembered,
        "fixture never reached real unfinished shore contact");
    Require(sim.InviteCompanion(target.Id,helper.Id).Contains("愿意")&&target.Family.CompanionTargetId==helper.Id,
        "real social invitation did not begin");
    Seconds(sim,1);
    Require(sim.WaterSupportStatus(helper)==null&&helper.Personality.WaterAftercare.ContactSeconds==0&&
        !helper.Personality.WaterAftercare.Remembered,"companionship secretly continued or completed shore care");
    Require(sim.EndCompanionship(target.Id,helper.Id).Contains("已结束"),"actual companionship did not end");
    double affection=target.Family.Peers[helper.Id].Affection;
    for(int n=0;n<80&&!helper.Personality.WaterAftercare.Remembered;n++)
    {
        Seconds(sim,.25);
        if(helper.Personality.WaterAftercare.ContactSeconds<6)
            Require(!helper.Personality.WaterAftercare.Remembered,"interrupted contact lowered the original six-second threshold");
    }
    Require(helper.Personality.WaterAftercare.Remembered&&helper.Personality.WaterAftercare.ContactSeconds==6&&
        Math.Abs(target.Family.Peers[helper.Id].Affection-affection-2)<.000001,
        "later uninterrupted contact did not earn exactly one shore-care reward");
});

Check("real rescue uses shared care budget and never removes water fear or recent human harm",()=>
{
    foreach(bool hurt in new[]{false,true})
    {
        var sim=Fresh();var c=sim.Selected!;c.Personality.Initialized=true;c.Personality.Kindness=.8;
        Seconds(sim,.25);
        if(hurt)sim.Hurt(c,2,"先前的人类伤害","player",BodyPart.Side);
        double trust=c.Trust,fear=c.Fear,budget=c.Interaction.HumanBond.CareBudget;
        Require(sim.RescueFromWater(c.Id).Success,"rescue failed");
        Require(c.Fear>=fear,"gratitude removed fear");
        Require(hurt?c.Trust==trust:c.Trust>trust&&c.Trust-trust<=budget,"rescue bypassed harm memory or care budget");
        var before=Snapshot(sim);Require(!sim.RescueFromWater(c.Id).Success&&Snapshot(sim)==before,"repeated rescue changed state");
    }
});

Check("entitled personality receives smaller real relationship change and no grateful shore duty",()=>
{
    double Gain(double kindness)
    {
        var sim=Fresh();var c=sim.Selected!;c.Personality.Initialized=true;c.Personality.Kindness=kindness;c.Personality.Pride=1-kindness;
        Seconds(sim,.25);double trust=c.Trust;Require(sim.RescueFromWater(c.Id).Success,"rescue failed");return c.Trust-trust;
    }
    Require(Gain(.8)>Gain(.05)&&Gain(.05)>0,"personality changed words only");
});

Check("episode cause outlives old elapsed attribution window and suppresses observer gratitude",()=>
{
    var sim=Fresh();var target=sim.Selected!;
    // Explicit saved long-immersion boundary, not a healthy survival journey.
    target.Interaction.Water=new(){InWater=true,EventId="water-old-player",EnteredAt=0,PlayerCaused=true,
        ExposureSeconds=125,LastPlayerSubmersionAt=0};sim.State.WorldSeconds=125;
    var observer=Neighbor(sim);sim=Reload(sim);target=sim.Selected!;observer=sim.State.Creatures.Single(c=>c.Id==observer.Id);
    double trust=target.Trust,witnessTrust=observer.Trust;
    Require(sim.RescueFromWater(target.Id).Success,"saved immersion rescue failed");
    Require(target.Trust==trust&&observer.Trust==witnessTrust,"waiting past 120 seconds laundered player-caused danger");
});

Check("query and pause are read-only, close-up and occlusion cannot create shore contact",()=>
{
    var sim=Fresh();var helper=Neighbor(sim);Seconds(sim,.25);Require(sim.RescueFromWater(sim.Selected!.Id).Success,"rescue failed");
    string before=Snapshot(sim);for(int n=0;n<100;n++)sim.WaterSupportStatus(helper);
    Require(Snapshot(sim)==before,"read-only view mutated episode or relation");
    sim.State.Settings.Pause=true;before=Snapshot(sim);Seconds(sim,10);
    Require(Snapshot(sim)==before,"pause advanced shore care");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;
    Require(sim.WaterSupportStatus(helper)==null,"close-up created live shore care");
    sim.State.Mode=PlayMode.Management;helper.Interaction.Restraint=RestraintKind.Box;
    Require(sim.WaterSupportStatus(helper)==null,"boxed helper claimed visible care");
});

Check("legacy migration is once-only and malformed causal or followup state rejects",()=>
{
    var sim=Fresh();sim.Selected!.Interaction.Water.InWater=true;
    var copy=Reload(sim);string eventId=copy.Selected!.Interaction.Water.EventId!;
    Require(Reload(copy).Selected!.Interaction.Water.EventId==eventId,"old save migration repeated episode");
    void Reject(Action<WorldSimulation> mutate)
    {
        var invalid=Reload(copy);mutate(invalid);bool rejected=false;
        try{SaveStore.Validate(invalid.State);}catch(InvalidDataException){rejected=true;}
        Require(rejected,"invalid causal state accepted");
    }
    Reject(s=>s.Selected!.Interaction.Water.EnteredAt=double.NaN);
    Reject(s=>s.Selected!.Interaction.Water.HumanRescueCredited=true);
    Reject(s=>s.Selected!.Personality.WaterAftercare=new(){TargetId=s.Selected.Id,EventId=eventId,Until=30});
    Reject(s=>{var duplicate=Neighbor(s);duplicate.Interaction.Water=new(){EventId=eventId,EnteredAt=0};});
    Reject(s=>{var wrong=Neighbor(s);wrong.Personality.WaterConcernId=s.Selected!.Id;wrong.Personality.WaterConcernEventId="unrelated-water";});
});

Check("self escape or peer reception can start shore concern within a tick without granting time before the event",()=>
{
    var sim=Fresh();var target=sim.Selected!;target.Interaction.FootIntegrity=100;target.X=6.55;target.TargetX=target.X;
    var helper=Neighbor(sim,.8,5,1.5);Seconds(sim,.25);
    Require(helper.Personality.WaterConcernEventId==target.Interaction.Water.EventId,"helper did not observe initial water episode");
    bool escaped=false;
    for(int n=0;n<240&&target.Condition!=PhysicalCondition.Dead;n++)
    {
        Seconds(sim,.25);
        if(!target.Interaction.Water.InWater)
        {
            escaped=true;
            var care=helper.Personality.WaterAftercare;
            Require(care.EventId==target.Interaction.Water.EventId,"actual departure lost participant identity");
            Require(care.ContactSeconds<=sim.State.WorldSeconds-care.StartedAt+.000001,"same tick granted pre-event contact");
            Reload(sim);break;
        }
    }
    Require(escaped,"near-bank escape or peer reception did not occur during real ordinary time");
});

Check("real thirsty river approach can slip, receive different peer responses, and require actual rescue and drinking",()=>
{
    bool completed=false;
    // Independent bounded component starts, not save/reload rerolls. Keep the
    // production river source and healthy movement. Initial demand/personality
    // and peer positions make this a drinking accident scenario. Neighbor's
    // existing autonomous/social timers isolate its water response; its initial
    // distance prevents an immediate successful peer reception on the fall tick.
    for(int seed=1;seed<=128&&!completed;seed++)
    {
        var sim=WorldSimulation.CreateInteractionPlaytest(20261031UL+(ulong)seed*7919);
        var target=sim.Selected!;
        sim.State.Creatures.RemoveAll(c=>c!=target);
        sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
        var source=sim.State.RoomObjects.Single();
        Require(source.X==WoodlandLayout.DrinkX&&source.Z==WoodlandLayout.BankZ,"default curved-bank source moved");
        target.X=source.X-2.4;target.Z=source.Z;target.TargetX=target.X;target.TargetZ=target.Z;
        target.Fullness=90;target.Energy=90;target.Interaction.Hydration=50;
        target.Personality=new(){Initialized=true,Kindness=.8,Carelessness=1,Pride=.2};
        // This component measures six uninterrupted seconds of shore care, not
        // an unrelated 24-second social invitation that legally preempts it.
        // Keep healthy locomotion and resource autonomy; isolate only new social invitations.
        target.Family.NextSocialAt=10000;
        var good=Neighbor(sim,.8,source.X-2.3,source.Z+2.2);
        var mean=Neighbor(sim,.05,source.X-2.3,source.Z-2.2);
        Require(WorldSimulation.CanSelfMove(target)&&!WaterEnvironment.IsInWater(sim.State,target)&&
            target.Interaction.Resources.Count==0&&target.Interaction.Water.EventId==null,"journey did not start as an uninformed dry mobile creature");
        double startX=target.X,initialHydration=target.Interaction.Hydration;
        double lastHarm=target.Interaction.HumanBond.LastHarmAt;
        long intake=target.Interaction.IntakeSequence;
        bool approached=false,discovered=false;
        for(int n=0;n<120&&target.Personality.NextWaterAccidentAt==0;n++)
        {
            Seconds(sim,.25);
            discovered|=target.Interaction.Resources.ContainsKey(source.Id);
            if(!target.Interaction.Water.InWater&&target.X>startX+.5)approached=true;
        }
        Require(discovered&&approached&&target.Personality.NextWaterAccidentAt>sim.State.WorldSeconds&&
            Math.Abs(target.Personality.NextWaterAccidentAt-sim.State.WorldSeconds-45)<.000001,
            "real source observation, walking, or first bounded accident draw did not occur");
        double attemptAt=sim.State.WorldSeconds;
        if(!target.Interaction.Water.InWater)
        {
            // A non-accident draw must really lead to drinking, without a hidden
            // water event or repeatedly rerolling the same mouthful.
            for(int n=0;n<32&&!target.Interaction.Digestion.RecentIntakes.Any(i=>i.SourceId==source.Id);n++)Seconds(sim,.25);
            Require(target.Interaction.Digestion.RecentIntakes.Any(i=>i.SourceId==source.Id&&i.Kind==RoomObjectKind.RiverWater)&&
                target.Interaction.Hydration>initialHydration&&target.Interaction.IntakeSequence==intake+1&&
                target.Interaction.Water.EventId==null&&!WaterEnvironment.IsInWater(sim.State,target)&&
                Math.Abs(target.Personality.NextWaterAccidentAt-attemptAt-45)<.000001,
                "non-accident attempt lost real intake, dry footing, or the original draw cooldown");
            continue;
        }
        var water=target.Interaction.Water;string eventId=water.EventId!;
        Require(!string.IsNullOrWhiteSpace(eventId)&&WaterEnvironment.IsInWater(sim.State,target)&&!water.PlayerCaused&&
            water.EnteredAt==attemptAt&&water.LastPlayerSubmersionAt<0&&
            target.Interaction.HumanBond.LastHarmAt==lastHarm&&target.Interaction.Hydration<=initialHydration&&
            target.Interaction.IntakeSequence==intake&&!target.Interaction.Digestion.RecentIntakes.Any(i=>i.SourceId==source.Id),
            "natural slip invented a drink or attributed its cause to the player");
        Require(sim.WaterConcernTarget(good)==target&&sim.WaterSupportStatus(good) is {Phase:WaterSupportPhase.Responding} support&&
            support.EventId==eventId&&good.Personality.NextWaterResponseAt>attemptAt&&good.Fear>0&&sim.State.HelpTargetId==target.Id,
            "good peer did not actually begin the water response");
        Require(mean.Personality.WaterConcernEventId==eventId&&mean.Personality.NextWaterResponseAt>attemptAt&&
            target.Family.Peers.TryGetValue(mean.Id,out var meanBond)&&meanBond.Resentment>=4&&
            sim.WaterSupportStatus(mean)==null,"mean peer changed words without its actual relational consequence");
        double fear=target.Fear,alarm=target.Interaction.Emotion.Alarm,trust=target.Trust;
        double goodTrust=good.Trust,meanTrust=mean.Trust,hydration=target.Interaction.Hydration;
        int supplies=sim.State.WaterRescueTools,purchases=sim.State.WaterRescuePurchases,money=sim.State.Money;
        Require(sim.BuyWaterRescueTool()&&sim.State.WaterRescueTools==supplies+1&&
            sim.State.WaterRescuePurchases==purchases+1&&sim.State.Money==money-WaterEnvironment.RescueToolPrice,"paid rescue purchase did not create exactly one supply for its price");
        Require(sim.RescueFromWater(target.Id).Success&&sim.State.WaterRescueTools==supplies&&
            !water.InWater&&!WaterEnvironment.IsInWater(sim.State,target)&&water.EventId==eventId&&water.HumanRescueCredited&&
            water.RecoverySeconds>0&&target.Interaction.Hydration==hydration&&
            target.Fear>=fear&&target.Interaction.Emotion.Alarm>=alarm&&target.Trust>trust&&good.Trust>goodTrust&&mean.Trust==meanTrust,
            "real rescue lost its single cost, observed personality effects, or preserved thirst/fear");
        Require(!EmotionalReadiness.Settled(target)&&good.Personality.WaterAftercare.EventId==eventId&&
            mean.Personality.WaterAftercare.TargetId==null,"rescue erased the aftermath or gave the mean peer shore care");
        string rescued=Snapshot(sim);
        Require(!sim.RescueFromWater(target.Id).Success&&Snapshot(sim)==rescued,"repeated rescue changed inventory or relationships");

        // The real cup still has to reach the mouth. Rescue itself gives no
        // hydration. Hold through this mouthful, then release for shore care.
        var drink=new InteractionInput(InteractionAction.Drink,BodyPart.Mouth,Duration:.1);
        Require(sim.BeginInteractionGesture(target.Id,drink).Success,"rescued creature could not accept the actual cup");
        for(int n=0;n<8;n++)
        {Require(sim.ContinueInteractionGesture(drink,true).Success,"cup lost mouth contact");sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);}
        Require(target.Interaction.IntakeSequence==intake+1&&target.Interaction.EatingKind==IntakeKind.Water&&
            target.Interaction.Hydration>hydration&&target.Interaction.Digestion.RecentIntakes.Any(i=>i.Kind==RoomObjectKind.Water&&i.SourceId==null),
            "post-rescue water was not actually accepted exactly once");
        for(int n=0;n<50&&(target.Interaction.EatingRemaining>0||target.Interaction.ReactionPose=="swallow"&&target.Interaction.ReactionPoseRemaining>0);n++)
        {sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);}
        Require(target.Interaction.EatingRemaining<=0,"actual drink never completed swallowing");
        sim.EndInteractionGesture();
        bool watching=false;
        while(sim.State.WorldSeconds<attemptAt+29)
        {
            Seconds(sim,.25);
            watching|=sim.WaterSupportStatus(good)?.Phase==WaterSupportPhase.Watching;
            Require(target.Condition==PhysicalCondition.Active&&!water.InWater&&water.EventId==eventId&&
                target.Interaction.HumanBond.LastHarmAt==lastHarm,"shore recovery lost the real accident's surviving identity");
        }
        Require(watching&&good.Personality.WaterAftercare.Remembered&&
            target.Family.Peers[good.Id].Affection>=2&&target.Family.Peers[mean.Id].Resentment>0&&
            target.Fear>0&&target.Interaction.Emotion.Alarm>0&&!EmotionalReadiness.Settled(target),
            "actual short shore contact or continuing fear/peer consequences was missing"+
            $" watching={watching}, remembered={good.Personality.WaterAftercare.Remembered}, affection={target.Family.Peers[good.Id].Affection}, resentment={target.Family.Peers[mean.Id].Resentment}, fear={target.Fear}, alarm={target.Interaction.Emotion.Alarm}, settled={EmotionalReadiness.Settled(target)}, helper={good.X},{good.Z}, target={target.X},{target.Z}, care={good.Personality.WaterAftercare.ContactSeconds}, status={sim.WaterSupportStatus(good)}");
        double affection=target.Family.Peers[good.Id].Affection;
        Seconds(sim,2);
        Require(sim.WaterSupportStatus(good)==null&&target.Family.Peers[good.Id].Affection==affection,
            "limited shore watching repeated its reward or did not end");
        Reload(sim);completed=true;
    }
    Require(completed,"no natural drinking accident completed within 128 independent bounded starts");
});

Console.WriteLine($"Failures: {failures}");return failures==0?0:1;

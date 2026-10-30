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
    sim.State.Creatures.RemoveAll(c=>c!=target);sim.State.RoomObjects.Clear();
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
    var sim=Fresh();var good=Neighbor(sim);var mean=Neighbor(sim,.05,4.5,-1.7);var far=Neighbor(sim,.8,-6,-5);
    Seconds(sim,.25);string eventId=sim.Selected!.Interaction.Water.EventId!;
    Require(sim.WaterConcernTarget(good)==sim.Selected,"good helper did not notice actual water");
    Require(sim.RescueFromWater(sim.Selected.Id).Success,"rescue failed");
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

Console.WriteLine($"Failures: {failures}");return failures==0?0:1;

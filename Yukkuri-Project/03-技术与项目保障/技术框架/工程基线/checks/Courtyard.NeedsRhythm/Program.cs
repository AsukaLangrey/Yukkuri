using System.Text.Json;
using Yukkuri.Simulation;

bool verify=args.Contains("--verify");int failures=0;
void Check(bool ok,string why){if(!ok){failures++;Console.WriteLine("FAIL "+why);}else Console.WriteLine("PASS "+why);}
WorldSimulation Yard(int speed,bool supply=true,RoomObjectKind food=RoomObjectKind.Meal)
{
    var sim=WorldSimulation.CreateCourtyardEconomy(20261102);var s=sim.State;
    var c=s.Creatures[0];s.Creatures.RemoveAll(x=>x.Id!=c.Id);s.SelectedId=c.Id;s.Shelters.Clear();s.RoomObjects.Clear();s.SupplyStations.Clear();s.AutonomousBreeding=false;
    sim.Config.WildGrassEnabled=false;s.WorldSeconds=6000;sim.SetCourtyardSpeed(speed);
    c.X=c.Z=c.TargetX=c.TargetZ=0;c.Fullness=c.Interaction.Hydration=100;c.Health=100;c.Energy=90;c.Pain=0;
    GrowthRules.Initialize(c,false);c.Interaction.Restraint=RestraintKind.Box;c.Family.Warmth=100;
    c.Family.NextCareAt=c.Family.NextSocialAt=c.Family.NextBreedingDecisionAt=1e9;
    if(supply){s.RoomObjects.Add(new(){Id="rhythm-food",RegionId=c.RegionId,Kind=food,X=-.25,Z=.6,Portions=10000});s.RoomObjects.Add(new(){Id="rhythm-water",RegionId=c.RegionId,Kind=RoomObjectKind.Water,X=.25,Z=.6,Portions=10000});}
    return sim;
}
void Step(WorldSimulation sim,double realDt=.05){sim.AdvanceInteractionPresentation(realDt);sim.Advance(realDt);}
var samples=new List<object>();
foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Leaves})
foreach(int speed in kind==RoomObjectKind.Meal?WorldSimulation.CourtyardSpeeds:new[]{1,10})
{
    var sim=Yard(speed,true,kind);var c=sim.Selected!;double start=sim.State.WorldSeconds,previous=0,foodTime=0,waterTime=0,needTime=0;
    var food=new List<double>();var water=new List<double>();long seq=c.Interaction.IntakeSequence;
    while(sim.State.WorldSeconds-start<3000-1e-8)
    {
        bool eating=c.Interaction.EatingRemaining>0;bool drinking=eating&&c.Interaction.EatingKind==IntakeKind.Water;
        Step(sim);double now=sim.State.WorldSeconds-start,dt=now-previous;previous=now;
        if(eating){if(drinking)waterTime+=dt;else foodTime+=dt;}
        if(NaturalResources.WantsFood(c)||NaturalResources.WantsWater(c)||eating)needTime+=dt;
        if(c.Interaction.IntakeSequence!=seq){seq=c.Interaction.IntakeSequence;(c.Interaction.EatingKind==IntakeKind.Water?water:food).Add(now);}
    }
    var meals=food.Where((t,i)=>i==0||t-food[i-1]>30).ToList();
    var byDay=Enumerable.Range(0,5).Select(day=>new{day=day+1,meals=meals.Count(t=>t>=day*600&&t<(day+1)*600),foodBites=food.Count(t=>t>=day*600&&t<(day+1)*600),drinks=water.Count(t=>t>=day*600&&t<(day+1)*600)}).ToArray();
    samples.Add(new{kind=kind.ToString(),speed,worldSeconds=previous,foodBusySeconds=foodTime,waterBusySeconds=waterTime,needSeconds=needTime,byDay,foodAt=food,waterAt=water,c.Fullness,c.Interaction.Hydration,c.Health,c.Condition});
    if(verify){Check(byDay.All(d=>d.meals is >=1 and <=3),$"{kind} {speed}x: 1-3 meals/day");Check(byDay[0].drinks<=1&&byDay.Skip(1).All(d=>d.drinks==1),$"{kind} {speed}x: one drink/day after initial full reserve");Check((foodTime+waterTime)/previous<.08,$"{kind} {speed}x: eating/drinking below 8% world time");Check(c.Health>=99,$"{kind} {speed}x: sufficient clean supplies sustain health");}
}
var openYards=new List<object>();
foreach(int speed in new[]{1,10})
{
    var sim=WorldSimulation.CreateCourtyardEconomy(20261102);sim.SetCourtyardSpeed(speed);sim.Config.WildGrassEnabled=false;
    sim.State.WorldSeconds=6000;sim.State.AutonomousBreeding=false;sim.State.RoomObjects.Clear();
    foreach(var c in sim.State.Creatures)
    {
        sim.State.RoomObjects.Add(new(){Id="free-food-"+c.Id,RegionId=c.RegionId,Kind=RoomObjectKind.Meal,X=c.X-.3,Z=c.Z+.7,Portions=10000});
        sim.State.RoomObjects.Add(new(){Id="free-water-"+c.Id,RegionId=c.RegionId,Kind=RoomObjectKind.Water,X=c.X+.3,Z=c.Z+.7,Portions=10000});
    }
    var counts=sim.State.Creatures.ToDictionary(c=>c.Id,c=>new int[5,2]);
    var sequences=sim.State.Creatures.ToDictionary(c=>c.Id,c=>c.Interaction.IntakeSequence);
    var foodTimes=sim.State.Creatures.ToDictionary(c=>c.Id,c=>new List<double>());var waterTimes=sim.State.Creatures.ToDictionary(c=>c.Id,c=>new List<double>());
    double start=sim.State.WorldSeconds,busy=0,total=0,nextTrace=0;
    string traceRoot=Environment.GetEnvironmentVariable("NEEDS_TRACE_DIR")??Path.GetTempPath();Directory.CreateDirectory(traceRoot);
    using var trace=new StreamWriter(Path.Combine(traceRoot,$"open-yard-{speed}.csv"));trace.WriteLine("world,id,full,water,health,x,z,tx,tz,hidden,sleep,rest,activity,resource");
    while(sim.State.WorldSeconds-start<3000-1e-8)
    {
        int occupied=sim.State.Creatures.Count(c=>c.Interaction.EatingRemaining>0);double before=sim.State.WorldSeconds;
        var previousNeeds=sim.State.Creatures.ToDictionary(c=>c.Id,c=>(c.Fullness,c.Interaction.Hydration));Step(sim);
        foreach(var c in sim.State.Creatures){if(c.Fullness>previousNeeds[c.Id].Fullness+.5)foodTimes[c.Id].Add(sim.State.WorldSeconds-start);if(c.Interaction.Hydration>previousNeeds[c.Id].Hydration+.5)waterTimes[c.Id].Add(sim.State.WorldSeconds-start);}
        busy+=occupied*(sim.State.WorldSeconds-before);total+=sim.State.Creatures.Count*(sim.State.WorldSeconds-before);
        if(sim.State.WorldSeconds-start>=nextTrace){nextTrace+=30;foreach(var c in sim.State.Creatures)trace.WriteLine(FormattableString.Invariant($"{sim.State.WorldSeconds-start:F2},{c.Id},{c.Fullness:F2},{c.Interaction.Hydration:F2},{c.Health:F2},{c.X:F3},{c.Z:F3},{c.TargetX:F3},{c.TargetZ:F3},{sim.IsShelteredHidden(c)},{c.Interaction.Sleepiness:F2},{c.Interaction.RestRemaining:F2},{c.Activity},{c.Interaction.ResourceTargetId}"));}
        foreach(var c in sim.State.Creatures)if(sequences[c.Id]!=c.Interaction.IntakeSequence){sequences[c.Id]=c.Interaction.IntakeSequence;int day=(int)((sim.State.WorldSeconds-start)/600);if(day<5)counts[c.Id][day,c.Interaction.EatingKind==IntakeKind.Water?1:0]++;}
    }
    File.WriteAllText(Path.Combine(traceRoot,$"open-yard-{speed}-state.json"),JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions));
    var oils=sim.State.Creatures.Select(c=>new{c.Id,adult=GrowthRules.IsAdult(c),c.Health,c.Condition,c.Fullness,c.Interaction.Hydration,foodAt=foodTimes[c.Id],waterAt=waterTimes[c.Id],byDay=Enumerable.Range(0,5).Select(d=>new{day=d+1,food=foodTimes[c.Id].Where((t,i)=>i==0||t-foodTimes[c.Id][i-1]>30).Count(t=>t>=d*600&&t<(d+1)*600),water=waterTimes[c.Id].Count(t=>t>=d*600&&t<(d+1)*600),foodAttempts=counts[c.Id][d,0],waterAttempts=counts[c.Id][d,1]}).ToArray()}).ToArray();
    openYards.Add(new{speed,worldSeconds=sim.State.WorldSeconds-start,busyFraction=busy/total,oils});
    if(verify){Check(oils.All(c=>c.Health>95&&c.Condition==PhysicalCondition.Active),$"free courtyard {speed}x: all three healthy after five days");Check(oils.Where(c=>c.adult).All(c=>c.byDay.All(d=>d.food is >=1 and <=3&&d.water<=1)), $"free courtyard {speed}x: adults <=3 food and <=1 water per day");}
}
var decays=new List<object>();
foreach(int speed in WorldSimulation.CourtyardSpeeds)
{
    var sim=Yard(speed,false);var c=sim.Selected!;double start=sim.State.WorldSeconds;
    while(sim.State.WorldSeconds-start<300-1e-8)Step(sim);
    decays.Add(new{speed,c.Fullness,c.Interaction.Hydration});
    if(verify)Check(Math.Abs(c.Fullness-70)<.001&&Math.Abs(c.Interaction.Hydration-70)<.001,$"{speed}x: same half-day depletion, no multiplier duplication");
}
var dry=Yard(10,false);var oil=dry.Selected!;oil.Interaction.Hydration=0;dry.Config.HungerPerSecond=0;double startDry=dry.State.WorldSeconds;
while(dry.State.WorldSeconds-startDry<60-1e-8)Step(dry);
if(verify)Check(oil.Health<94&&oil.InjuryCause=="持续脱水", "zero hydration damages health instead of passive healing");
if(verify)
{
    Check(ShelterGeometry.SweptCircleClear(1.5,1.5,1.6,1.6,.3,true),"rounded hut corner permits outward movement");
    Check(!ShelterGeometry.SweptCircleClear(1.6,0,1,0,.3,true),"hut wall still blocks crossing");
    Check(ShelterGeometry.SweptCircleClear(1.54,0,1.6,0,.3,true),"small growth overlap can clear outward without a teleport");
    foreach(var frozen in new[]{"pause","close","storage"})
    {
        var sim=Yard(10,false);var c=sim.Selected!;c.Interaction.Hydration=0;
        sim.State.Settings.Pause=frozen=="pause";sim.State.Mode=frozen=="close"?PlayMode.CloseUp:PlayMode.Management;c.Family.ColdStorage.Stored=frozen=="storage";
        double health=c.Health,age=c.Body.AgeSeconds;for(int i=0;i<200;i++)Step(sim);
        Check(c.Health==health&&c.Body.AgeSeconds==age,$"{frozen}: no dehydration or biological time leak");
    }
    var hand=Yard(10,false);var held=hand.Selected!;held.Interaction.BeginIntake(IntakeKind.Meal);hand.AdvanceInteractionPresentation(.1);
    Check(Math.Abs(held.Interaction.EatingRemaining-.6)<.001,"accepted hand-fed mouthful follows the world clock at 10x");
    var close=Yard(10);var actor=close.Selected!;actor.Interaction.Hydration=0;
    for(int i=0;i<20&&actor.Interaction.EatingRemaining<=0;i++)Step(close);
    close.State.Mode=PlayMode.CloseUp;double remaining=actor.Interaction.EatingRemaining;close.AdvanceInteractionPresentation(.1);
    Check(Math.Abs(actor.Interaction.EatingRemaining-(remaining-.1))<.001,"already accepted ground sip uses real time in close-up");
    close.State.Settings.Pause=true;remaining=actor.Interaction.EatingRemaining;close.AdvanceInteractionPresentation(.1);
    Check(actor.Interaction.EatingRemaining==remaining,"pause freezes mouth progress");
    var restore=Yard(10,false);var thirsty=restore.Selected!;thirsty.Interaction.Hydration=0;for(int i=0;i<120;i++)Step(restore);
    thirsty.Interaction.Hydration=60;double h=thirsty.Health;for(int i=0;i<20;i++)Step(restore);
    Check(thirsty.Health>h&&CourtyardSurvival.DehydrationStatus(thirsty)==""&&CourtyardSurvival.DehydrationMovementFactor(thirsty)==1,"drinking clears dehydration penalty and permits recovery");
    var saved=Yard(1);saved.Selected!.Interaction.MealSeeking=true;saved.Selected.Fullness=70;
    var loaded=new WorldSimulation(JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(saved.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!);
    Check(loaded.Selected!.Interaction.MealSeeking&&NaturalResources.WantsFood(loaded.Selected),"meal session survives save/load");
    var mover=Yard(1,false);var moving=mover.Selected!;moving.Interaction.Restraint=RestraintKind.None;
    var move=typeof(WorldSimulation).GetMethod("RoomMove",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
    moving.Interaction.Hydration=100;move.Invoke(mover,new object[]{moving,5d,0d,.25,true});double normal=moving.X;
    moving.X=moving.Z=0;moving.Interaction.Hydration=0;move.Invoke(mover,new object[]{moving,5d,0d,.25,true});
    Check(moving.X>0&&moving.X<normal*.7,"dehydration slows actual ground movement but leaves escape possible");
}
Console.WriteLine(JsonSerializer.Serialize(new{scope="5 simulated days per accessible-tray fixture, one adult in a box to remove navigation confounds; 50ms presentation frames plus real fixed simulation ticks; not wall-clock stress or full economy",samples,openYards,decays,deprivation=new{oil.Health,oil.Energy,oil.Pain,oil.InjuryCause}},new JsonSerializerOptions{WriteIndented=true}));
Environment.ExitCode=failures>0?1:0;

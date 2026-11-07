using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action run){try{run();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds){for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
string Json(WorldSimulation sim)=>JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation sim){SaveStore.Validate(sim.State);var s=JsonSerializer.Deserialize<WorldState>(Json(sim),SaveStore.JsonOptions)!;SaveStore.Validate(s);return new(s);}
WorldSimulation Fixture(int count=5)
{
    var sim=WorldSimulation.CreateCourtyardEconomy();var s=sim.State;
    s.RoomObjects.Clear();s.AutonomousBreeding=false;s.FamilyRelationshipEvents.Clear();s.Creatures.Clear();
    for(int n=0;n<count;n++)s.Creatures.Add(new(){
        Id="hut-oil-"+n,Name="住户"+n,RegionId="home",X=-4+n%5*2,Z=1+n/5*2,TargetX=-4+n%5*2,TargetZ=1+n/5*2,
        Fullness=100,Health=100,Energy=80,Trust=70,Fear=0,Pain=0,
        Body=new(){Id="hut-body-"+n,SpeciesId=n%2==0?"reimu":"marisa"},
        Registration=new(){IdentityId="hut-id-"+n},
        Family=new(){NextSocialAt=10000,NextCareAt=10000,NextBreedingDecisionAt=10000},
        Interaction=new(){Enabled=true,Hydration=100,NextAutonomyAt=10000,ToyInterest=0,Accessory=new(){Id="hut-accessory-"+n}}
    });
    s.SelectedId=s.Creatures[0].Id;s.WorldSeconds=360;s.PlayerX=7;s.PlayerZ=7;
    return new(s);
}
Check("new game provides one valid five-resident house without charging money",()=>{
    var sim=WorldSimulation.CreateCourtyardEconomy();
    Require(sim.State.Shelters.Count==1&&sim.State.Shelters[0].Capacity==5&&sim.State.Money==240&&sim.State.ShelterStock==0,"default house");
    SaveStore.Validate(sim.State);
    Require(Enumerable.Range(1,5).Select(WorldSimulation.ShelterComfort).SequenceEqual(new[]{"最舒适","最舒适","适用","拥挤","极度拥挤"}),"comfort tiers");
});
Check("five real arrivals, sixth excluded, pause/save resume and real dawn exits",()=>{
    var sim=Fixture(6);var home=sim.State.Shelters.Single();
    for(int n=0;n<1400;n++){
        var before=sim.State.Creatures.Select(c=>(c.X,c.Z)).ToArray();Tick(sim,.05);
        for(int i=0;i<before.Length;i++)Require(Math.Sqrt(Math.Pow(before[i].X-sim.State.Creatures[i].X,2)+Math.Pow(before[i].Z-sim.State.Creatures[i].Z,2))<.2,"teleport");
        if(sim.ShelterStatus(home.Id)!.OccupantIds.Length==5&&home.Residents.All(v=>v.Phase==ShelterVisitPhase.Inside))break;
    }
    Console.WriteLine(string.Join("\n",sim.State.Creatures.Select(c=>$"{c.Name} {c.X:F2},{c.Z:F2} {c.Activity} {sim.CreatureShelterStatus(c)}")));
    Require(sim.ShelterStatus(home.Id)!.OccupantIds.Length==5&&home.Residents.Count==5,"five did not enter");
    Tick(sim,1);
    Require(sim.State.Creatures.Count(c=>c.Interaction.Resting)==5,"five not sleeping");
    var sixth=sim.State.Creatures.Single(c=>!home.Residents.Any(v=>v.CreatureId==c.Id));
    Require(sim.ShelterEntryReason(sixth,home.Id)?.Contains("已满")==true,"sixth not refused");
    sim=Reload(sim);home=sim.State.Shelters.Single();sim.State.Settings.Pause=true;
    double paused=sim.State.WorldSeconds;Tick(sim,2);Require(sim.State.WorldSeconds==paused,"pause advanced");
    sim.State.Settings.Pause=false;sim.State.WorldSeconds=550;Tick(sim,45);
    Console.WriteLine("dawn: "+string.Join("\n",sim.State.Creatures.Select(c=>$"{c.Name} {c.X:F2},{c.Z:F2} {c.Activity} {sim.CreatureShelterStatus(c)}")));
    Require(home.Residents.Count==0&&sim.ShelterStatus(home.Id)!.OccupantIds.Length==0,"dawn exit stuck");
    SaveStore.Validate(sim.State);
});
Check("night housing improves mood relative to exposure; crowded rest is worse",()=>{
    WorldSimulation Resters(int count,bool housed){
        var sim=Fixture(count);var h=sim.State.Shelters.Single();
        (double X,double Z)[] beds=[(-1.6,-1.3),(1.6,-1.3),(-1.6,1.2),(1.6,1.2),(0,-.05)];
        if(housed)for(int n=0;n<count;n++){
            var c=sim.State.Creatures[n];c.X=c.TargetX=h.X+beds[n].X;c.Z=c.TargetZ=h.Z+beds[n].Z;
            h.Residents.Add(new(){CreatureId=c.Id,Slot=n,Phase=ShelterVisitPhase.Inside});
        }else sim.State.Shelters.Clear();
        return sim;
    }
    var sheltered=Resters(2,true);var outside=Resters(2,false);var crowded=Resters(5,true);
    Tick(sheltered,100);Tick(outside,100);Tick(crowded,100);
    double Mood(WorldSimulation s)=>s.State.Creatures.Average(c=>c.Interaction.Emotion.Manmanlai);
    Console.WriteLine($"mood sheltered={Mood(sheltered):F2}, exposed={Mood(outside):F2}, crowded={Mood(crowded):F2}");
    Require(Mood(sheltered)>Mood(outside)+5&&Mood(sheltered)>Mood(crowded)+5,"housing did not affect happiness");
    Require(outside.State.Creatures.All(c=>c.Interaction.NightHousingDiscomfort>10),"exposure penalty missing");
    double penalty=outside.State.Creatures[0].Interaction.NightHousingDiscomfort;
    outside.State.WorldSeconds=600;Tick(outside,30);
    Require(outside.State.Creatures[0].Interaction.NightHousingDiscomfort<penalty,"day recovery");
});
Check("closed door, sale frees slot, legacy save and invalid residency",()=>{
    var sim=Fixture(2);var h=sim.State.Shelters.Single();h.DoorOpen=false;Tick(sim,30);
    Require(h.Residents.Count==0&&sim.State.Creatures.All(c=>c.Interaction.NightHousingDiscomfort>0),"closed door bypassed");
    h.DoorOpen=true;Tick(sim,50);sim=Reload(sim);h=sim.State.Shelters.Single();
    var c=sim.State.Creatures[0];int money=sim.State.Money,price=CourtyardEconomy.Quote(c).Price;
    sim.SellCreatureToShop(c.Id,price);
    Require(sim.State.Money==money+price&&h.Residents.All(v=>v.CreatureId!=c.Id),"sale left stale resident");SaveStore.Validate(sim.State);
    var bad=JsonSerializer.Deserialize<WorldState>(Json(sim),SaveStore.JsonOptions)!;
    bad.Shelters[0].Residents.Add(new(){CreatureId="missing",Slot=4,Phase=ShelterVisitPhase.Inside});
    bool rejected=false;try{SaveStore.Validate(bad);}catch(InvalidDataException){rejected=true;}Require(rejected,"invalid resident accepted");
    var legacy=WorldSimulation.CreateInteractionPlaytest();legacy.State.ShelterStock=1;legacy.State.RoomObjects.Clear();
    Require(legacy.PlaceShelter(0,4).Success,"legacy placement");
    var json=JsonNode.Parse(Json(legacy))!;var hs=json["Shelters"]![0]!.AsObject();hs.Remove("Capacity");hs.Remove("Residents");
    var state=JsonSerializer.Deserialize<WorldState>(json.ToJsonString(),SaveStore.JsonOptions)!;SaveStore.Validate(state);
    Require(new WorldSimulation(state).State.Shelters.Single().Capacity==1,"legacy house changed size");
});
Check("shared geometry door controls, threshold obstruction, urgent hunger and death",()=>{
    var sim=Fixture(2);var h=sim.State.Shelters.Single();double gate=h.Z+1.25*ShelterGeometry.SharedScale;
    InteractionInput Door(InteractionAction action)=>new(action,BodyPart.Side,.2,X:h.X,Z:gate,Height:1,ObjectId:h.Id,TargetKind:InteractionTargetKind.Shelter);
    Require(sim.ExecuteShelterInteraction(h.Id,Door(InteractionAction.CloseShelterDoor)).Success,"scaled door hit failed");
    Require(sim.ExecuteShelterInteraction(h.Id,Door(InteractionAction.OpenShelterDoor)).Success,"open door failed");
    var c=sim.State.Creatures[0];double x=c.X,z=c.Z;c.X=h.X;c.Z=gate;
    Require(!sim.ExecuteShelterInteraction(h.Id,Door(InteractionAction.CloseShelterDoor)).Success,"door closed through body");
    c.X=x;c.Z=z;Tick(sim,50);Require(h.Residents.Count==2,"night reservations");
    c.Fullness=10;Tick(sim,25);
    Require(h.Residents.All(v=>v.CreatureId!=c.Id)&&!c.Interaction.Resting,"sleep suppressed urgent hunger or exit");
    var other=sim.State.Creatures[1];other.Condition=PhysicalCondition.Dead;Tick(sim,1);
    Require(h.Residents.All(v=>v.CreatureId!=other.Id),"dead resident reservation retained");
    SaveStore.Validate(sim.State);
});
Console.WriteLine("Housing failures="+failures);Environment.ExitCode=failures==0?0:1;

using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

// Source only on SHIKI-GAME-4080. Execute on the separately authorized remote host.
int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action body){try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation sim)=>JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(Snapshot(sim),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void Step(WorldSimulation sim,double duration=.05)
{sim.AdvanceInteractionPresentation(duration);sim.Advance(duration);}
void Seconds(WorldSimulation sim,double duration)
{for(int n=0;n<(int)Math.Round(duration/.05);n++)Step(sim);}
WorldSimulation Fresh()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261031);
    sim.State.RoomObjects.Clear();sim.State.PlayerX=6;sim.State.PlayerZ=6;
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.X=c.TargetX=-5+n*4;c.Z=c.TargetZ=-5;
        c.Fullness=c.Health=c.Energy=c.Interaction.Hydration=100;c.Fear=c.Pain=0;
        c.Interaction.Emotion.Alarm=0;c.Interaction.Emotion.SafeSeconds=100;
        c.Interaction.NextAutonomyAt=c.DecisionIn=c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    }
    sim.SetAutonomousBreeding(false);return sim;
}
ShelterState Home(WorldSimulation sim)=>sim.State.Shelters.Single();
WorldSimulation Built()
{
    var sim=Fresh();Require(sim.BuyShelter(),"purchase failed");
    Require(sim.PlaceShelter(0,0).Success,"placement failed");return sim;
}
InteractionInput Door(ShelterState home,InteractionAction action)=>new(action,BodyPart.Side,.2,
    X:home.X,Z:home.Z+1.25,Height:1,ObjectId:home.Id,TargetKind:InteractionTargetKind.Shelter);
WorldSimulation Entered()
{
    var sim=Built();var c=sim.State.Creatures[0];c.X=c.TargetX=0;c.Z=c.TargetZ=3;
    c.Fear=50;c.Interaction.Emotion.Alarm=30;c.Interaction.Emotion.SafeSeconds=0;
    Require(sim.ShelterEntryReason(c,Home(sim).Id)==null,"entry rejected at clear open door");
    for(int n=0;n<800&&Home(sim).Phase!=ShelterVisitPhase.Inside;n++)
    {
        double x=c.X,z=c.Z;Step(sim);
        Require(Math.Sqrt(Math.Pow(x-c.X,2)+Math.Pow(z-c.Z,2))<.25,"entry teleported beyond one world step");
    }
    Require(Home(sim).Phase==ShelterVisitPhase.Inside&&Home(sim).GuestId==c.Id,"autonomous entry did not finish");
    Require(ShelterGeometry.FullyInside(c.X,c.Z,WorldSimulation.ShelterPassageRadius(c)),"inside label before body passed door");
    return sim;
}

Check("paid inventory and failed placement preserve ownership",()=>
{
    var sim=Fresh();sim.State.Money=0;string before=Snapshot(sim);
    Require(!sim.BuyShelter()&&Snapshot(sim)==before,"failed purchase mutated state");
    sim.State.Money=100;sim.State.UnlimitedResources=true;
    Require(sim.BuyShelter()&&sim.State.Money==55&&sim.ShopQuantity("shelter")==1,"shelter bypassed paid shop");
    before=Snapshot(sim);Require(!sim.PlaceShelter(double.NaN,0).Success&&Snapshot(sim)==before,"bad ground consumed stock");
    var c=sim.State.Creatures[0];Require(!sim.PlaceShelter(c.X,c.Z).Success,"house built over body");
    Require(sim.PlaceShelter(0,0).Success&&sim.State.ShelterStock==0&&sim.ShopOwnedQuantity("shelter")==1,"placement duplicated ownership");
    sim=Reload(sim);Require(sim.State.ShelterSequence==1&&sim.State.Shelters.Count==1,"reload lost house identity");
});

Check("physical door entry and one resident survive reload",()=>
{
    var sim=Entered();var c=sim.State.Creatures[0];var home=Home(sim);
    Require(sim.ShelterEntryReason(sim.State.Creatures[1],home.Id)!=null,"second resident reserved occupied house");
    double fear=c.Fear;sim=Reload(sim);c=sim.State.Creatures[0];home=Home(sim);
    Require(home.GuestId==c.Id&&home.Phase==ShelterVisitPhase.Inside&&c.Fear==fear,"load reset fear or occupancy");
    c.Trust=20;
    Require(!sim.ShelterStatus(home.Id)!.CanCall&&sim.ShelterStatus(home.Id)!.CallRefusalReason!=null,"low-trust guest cannot explain refusal");
});

Check("close door, blocked help, open door and actual exit",()=>
{
    var sim=Entered();var home=Home(sim);var c=sim.State.Creatures[0];
    Require(sim.ExecuteShelterInteraction(home.Id,Door(home,InteractionAction.CloseShelterDoor)).Success,"clear doorway did not close");
    c.Fullness=10;Seconds(sim,2);
    Require(home.ExitRequested&&home.BlockedSeconds>=1&&c.Z<1,"closed door failed to hold exit");
    Require(sim.InteractionHelpFor(c)?.Kind==InteractionHelpKind.Shelter,"blocked exit not in help list");
    double x=c.X,z=c.Z;
    Require(sim.ExecuteShelterInteraction(home.Id,Door(home,InteractionAction.OpenShelterDoor)).Success,"opening failed");
    Require(c.X==x&&c.Z==z&&home.GuestId==c.Id,"opening teleported guest or freed occupied room");
    for(int n=0;n<500&&home.GuestId!=null;n++)Step(sim);
    Require(home.GuestId==null&&c.Z>ShelterGeometry.OuterDepth*.5,"opened house did not release after actual exit");
});

Check("call is an invitation and repeated click does not grant relationship",()=>
{
    var sim=Entered();var c=sim.State.Creatures[0];var home=Home(sim);
    c.Trust=80;c.Fear=10;c.Interaction.Emotion.Alarm=0;c.Interaction.Emotion.SafeSeconds=100;
    double x=c.X,z=c.Z,trust=c.Trust;
    Require(sim.ExecuteShelterInteraction(home.Id,Door(home,InteractionAction.CallFromShelter)).Success,"trusted call failed");
    Require(c.X==x&&c.Z==z&&home.ExitRequested&&c.Trust==trust,"call moved body or granted free trust");
    Require(!sim.ExecuteShelterInteraction(home.Id,Door(home,InteractionAction.CallFromShelter)).Success,"duplicate call accepted");
});

Check("real doorway object blocks exit and removal restores travel",()=>
{
    var sim=Entered();var home=Home(sim);var c=sim.State.Creatures[0];
    // A ball at the threshold is a physical obstacle, not a simulated closed-door flag.
    var ball=new RoomObject{Id="door-ball",RegionId=c.RegionId,Kind=RoomObjectKind.Ball,X=0,Z=1.25};
    sim.State.RoomObjects.Add(ball);c.Fullness=10;Seconds(sim,3);
    Require(home.GuestId==c.Id&&home.BlockedSeconds>=1&&sim.InteractionHelpFor(c)?.Kind==InteractionHelpKind.Shelter,"blocked doorway did not retain guest and help");
    Require(sim.MoveLooseObject(ball.Id,0,3.5),"could not remove threshold obstacle through the open door");
    for(int n=0;n<500&&home.GuestId!=null;n++)Step(sim);
    Require(home.GuestId==null,"removed obstacle left stale blocked occupancy");
});

Check("door rejects body, object and invalid surface",()=>
{
    var sim=Built();var c=sim.State.Creatures[0];var home=Home(sim);
    c.X=0;c.Z=1.25;string before=Snapshot(sim);
    Require(!sim.ExecuteShelterInteraction(home.Id,Door(home,InteractionAction.CloseShelterDoor)).Success&&Snapshot(sim)==before,"door closed through body");
    c.X=-5;c.Z=-5;
    Require(sim.PlaceInteractionSupply(InteractionAction.PlaceMeal,0,1.25).Success,"clear open threshold cannot accept food");
    Require(!sim.ExecuteShelterInteraction(home.Id,Door(home,InteractionAction.CloseShelterDoor)).Success,"door closed through real portion");
    Require(!sim.ExecuteShelterInteraction(home.Id,Door(home,InteractionAction.OpenShelterDoor) with {X=1.2,Z=0}).Success,"side wall operated door");
});

Check("wall sweep, thrown body and low carrying keep real boundaries",()=>
{
    var sim=Entered();var c=sim.State.Creatures[0];var home=Home(sim);
    var lift=new InteractionInput(InteractionAction.Lift,BodyPart.Side,Height:3,X:c.X,Z:c.Z);
    Require(sim.BeginInteractionGesture(c.Id,lift).Success,"resident could not be supported");
    Require(c.Interaction.Height+ShelterBodyEnvelope.Height(c)<ShelterGeometry.CeilingY,"pickup crossed roof");
    double x=c.X,z=c.Z;
    sim.ContinueInteractionGesture(lift with {X=4,Z=0},true);
    Require(c.X==x&&c.Z==z,"carried body crossed side wall");sim.EndInteractionGesture(true);
    c.Interaction.Carried=false;c.Interaction.Falling=true;c.Interaction.Height=.2;c.Interaction.VelocityX=8;c.Interaction.VerticalVelocity=1;
    Seconds(sim,.2);Require(c.X<1.25,"thrown body crossed wall");
    Require(!ShelterGeometry.SweptCircleClear(0,0,4,0,.2,true),"large cursor jump passed wall");
});

Check("pause and close-up do not advance or operate house",()=>
{
    var sim=Entered();var home=Home(sim);
    foreach(bool close in new[]{false,true})
    {
        sim.State.Settings.Pause=!close;sim.State.Mode=close?PlayMode.CloseUp:PlayMode.Management;
        string before=JsonSerializer.Serialize(home,SaveStore.JsonOptions);
        double clock=sim.State.WorldSeconds,x=sim.State.Creatures[0].X,z=sim.State.Creatures[0].Z;Seconds(sim,1);
        Require(JsonSerializer.Serialize(home,SaveStore.JsonOptions)==before&&sim.State.WorldSeconds==clock&&
            sim.State.Creatures[0].X==x&&sim.State.Creatures[0].Z==z,"paused visit advanced");
        Require(!sim.ExecuteShelterInteraction(home.Id,Door(home,InteractionAction.CloseShelterDoor)).Success,"paused door changed");
    }
});

Check("death releases reservation but body remains physical occupancy",()=>
{
    var sim=Entered();var c=sim.State.Creatures[0];c.Condition=PhysicalCondition.Dead;c.Health=0;
    sim=new WorldSimulation(sim.State);var home=Home(sim);
    Require(home.GuestId==null&&sim.ShelterStatus(home.Id)!.OccupantId==c.Id,"dead guest vanished or retained autonomy");
    Require(sim.ShelterEntryReason(sim.State.Creatures[1],home.Id)!=null,"dead body did not occupy floor");
});

Check("legacy defaults, corrupt references and sequence exhaustion",()=>
{
    var legacy=JsonNode.Parse(Snapshot(Fresh()))!.AsObject();legacy.Remove("Shelters");legacy.Remove("ShelterStock");legacy.Remove("ShelterSequence");
    var old=JsonSerializer.Deserialize<WorldState>(legacy.ToJsonString(),SaveStore.JsonOptions)!;SaveStore.Validate(old);
    Require(old.Shelters.Count==0&&old.ShelterStock==0,"legacy save was gifted houses");
    foreach(Action<WorldState> corrupt in new Action<WorldState>[]
    {s=>s.Shelters=null!,s=>s.ShelterSequence=0,s=>s.Shelters[0].X=double.NaN,
        s=>s.Shelters[0].GuestId="missing",s=>s.Shelters[0].BlockedSeconds=-1,s=>s.ShelterStock=100})
    {
        var sim=Built();corrupt(sim.State);bool rejected=false;
        try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
        Require(rejected,"bad shelter save accepted");
    }
    var exhausted=Fresh();Require(exhausted.BuyShelter(),"stock failed");exhausted.State.ShelterSequence=long.MaxValue;
    string before=Snapshot(exhausted);Require(!exhausted.PlaceShelter(0,0).Success&&Snapshot(exhausted)==before,"sequence wrapped or consumed stock");
});
Console.WriteLine($"Interaction.Shelters failures={failures}");
return failures==0?0:1;

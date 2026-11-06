using System.Text.Json;
using Yukkuri.Simulation;

int failures=0,checks=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body){checks++;try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Json<T>(T value)=>JsonSerializer.Serialize(value,SaveStore.JsonOptions);
WorldSimulation Fresh(double x=-2.3,double z=0,double homeX=0,double homeZ=0)
{
    var s=WorldSimulation.CreateInteractionPlaytest(20261106);
    var c=s.State.Creatures.Single(c=>c.IsYoung);s.State.SelectedId=c.Id;
    s.State.Creatures.RemoveAll(o=>o!=c);s.State.RoomObjects.Clear();
    c.X=c.TargetX=-5;c.Z=c.TargetZ=-5;
    s.State.PlayerX=6;s.State.PlayerZ=6;
    Require(s.BuyShelter()&&s.PlaceShelter(homeX,homeZ).Success,"real shelter purchase/placement failed");
    c.X=c.TargetX=x;c.Z=c.TargetZ=z;c.Fullness=55;c.Energy=90;c.Fear=c.Pain=0;
    c.Interaction.NextAutonomyAt=c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    Require(ShelterGeometry.FitsLocalCircle(x-homeX,z-homeZ,WorldSimulation.ShelterPassageRadius(c),true),"fixture body overlaps shelter");
    return s;
}
void RejectUnchanged(WorldSimulation s,InteractionAction action)
{
    var c=s.Selected!;int food=s.State.Food;ulong random=s.State.RandomState;
    string objects=Json(s.State.RoomObjects),learning=Json(c.Interaction.Learning),development=Json(c.Interaction.Development);
    var active=c.Interaction.ActiveCue;double fatigue=c.TrainingFatigue,wait=c.Interaction.WaitRemaining;
    var result=s.ExecuteInteraction(c.Id,new(action));
    Require(!result.Success,$"accepted {action}; food {food}->{s.State.Food}; objects {Json(s.State.RoomObjects)}");
    Require(s.State.Food==food&&Json(s.State.RoomObjects)==objects,"rejection consumed inventory or created/changed objects");
    Require(s.State.RandomState==random&&Json(c.Interaction.Learning)==learning&&Json(c.Interaction.Development)==development,"rejection advanced RNG or teaching evidence");
    Require(c.Interaction.ActiveCue==active&&c.Interaction.WaitRemaining==wait&&c.TrainingFatigue==fatigue,"rejection began a cue or changed fatigue");
    // ExecuteInteraction may still record player contact and its normal unavailable message.
}
foreach(var action in new[]{InteractionAction.Wait,InteractionAction.TeachWait})
{
    foreach(double offset in new[]{0.0,2.0})
    Check($"{action} rejects wall-overlapping automatic food at translated shelter {offset}",()=>{
        var s=Fresh(-2.3+offset,0,offset,0);var c=s.Selected!;
        Require(!ShelterGeometry.FitsLocalCircle(-1.5,0,.32,true),"bait fixture does not overlap wall");
        Require(!s.PlaceInteractionSupply(InteractionAction.PlaceMeal,c.X+.8,c.Z).Success,"ordinary meal placement unexpectedly accepted wall");
        RejectUnchanged(s,action);
    });
    Check($"{action} clear ground creates one paid meal and real attempt",()=>{
        var s=Fresh(-3,3);var c=s.Selected!;int food=s.State.Food;
        Require(s.ExecuteInteraction(c.Id,new(action)).Success,"clear ground rejected");
        var obj=s.State.RoomObjects.Single();
        Require(obj.Kind==RoomObjectKind.Meal&&Math.Abs(obj.X-(c.X+.8))<1e-9&&obj.Z==c.Z&&s.State.Food==food-1,"wrong meal placement or debit");
        Require(c.Interaction.ActiveCue==LearnedCue.Wait&&c.Interaction.Learning[LearnedCue.Wait].Attempts==1,"missing actual attempt");
        SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(Json(s.State),SaveStore.JsonOptions)!;
        SaveStore.Validate(state);Require(state.Food==food-1&&state.RoomObjects.Count==1,"roundtrip lost meal/stock");
    });
    Check($"{action} open doorway remains valid",()=>{
        // Body and bait both fit the actual opening; no synthetic wall-straddling body.
        var s=Fresh(-.8,1.9);var c=s.Selected!;
        Require(ShelterGeometry.FitsLocalCircle(0,1.9,.32,true),"doorway fixture blocked");
        Require(s.ExecuteInteraction(c.Id,new(action)).Success,"open doorway rejected");
        Require(s.State.RoomObjects.Count==1,"doorway meal missing");
    });
    foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Sweet})
    Check($"{action} reuses visible {kind} near wall with no stock",()=>{
        var s=Fresh();var c=s.Selected!;
        Require(s.PlaceInteractionSupply(kind==RoomObjectKind.Meal?InteractionAction.PlaceMeal:InteractionAction.PlaceSweet,-2.1,.7).Success,"existing supply could not be placed");
        s.State.Food=0;string before=Json(s.State.RoomObjects);
        Require(s.ExecuteInteraction(c.Id,new(action)).Success,"reusable food rejected");
        Require(s.State.Food==0&&Json(s.State.RoomObjects)==before&&c.Interaction.ActiveCue==LearnedCue.Wait,"existing food charged or changed");
    });
    foreach(string excluded in new[]{"held","stored","carried","empty","far","other-region"})
    Check($"{action} does not reuse {excluded} food to bypass wall",()=>{
        var s=Fresh();var c=s.Selected!;
        var o=new RoomObject{RegionId=c.RegionId,Kind=RoomObjectKind.Meal,X=-2.1,Z=.7,Portions=1};
        if(excluded=="held")o.HeldByHand=true;
        if(excluded=="stored")o.Stored=true;
        if(excluded=="carried")o.CarrierId=c.Id;
        if(excluded=="empty")o.Portions=0;
        if(excluded=="far")o.X=-6;
        if(excluded=="other-region")o.RegionId="elsewhere";
        s.State.RoomObjects.Add(o);RejectUnchanged(s,action);
    });
    foreach(string obstacle in new[]{"stock","count","station","bottle","river"})
    Check($"{action} preserves {obstacle} preflight",()=>{
        var s=Fresh(-3,3);var c=s.Selected!;
        switch(obstacle)
        {
            case "stock":s.State.Food=0;break;
            case "count":for(int n=0;n<24;n++)s.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Water,RegionId=c.RegionId,X= -6});break;
            case "station":s.State.SupplyStations.Add(new(){RegionId=c.RegionId,X=c.X+.8,Z=c.Z});break;
            case "bottle":s.State.DetachedStemBroods.Add(new(){RegionId=c.RegionId,X=c.X+.8,Z=c.Z});break;
            case "river":c.X=c.TargetX=6;s.State.RoomObjects.Add(new(){Kind=RoomObjectKind.RiverWater,RegionId=c.RegionId});break;
        }
        RejectUnchanged(s,action);
    });
}
Console.WriteLine($"RESULT {checks-failures}/{checks} passed; {failures} failed");
return failures==0?0:1;

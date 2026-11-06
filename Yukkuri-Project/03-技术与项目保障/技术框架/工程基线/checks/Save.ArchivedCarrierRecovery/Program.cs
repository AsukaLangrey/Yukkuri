using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

if(args.Length!=1||Directory.Exists(args[0]))throw new ArgumentException("Provide a fresh output directory.");
string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
var results=new List<object>();var fileEvidence=new List<object>();int failures=0;
void Require(bool value,string message){if(!value)throw new Exception(message);}
void Check(string name,Action action){try{action();results.Add(new{name,success=true});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;results.Add(new{name,success=false,error=e.Message});Console.WriteLine("FAIL "+name+": "+e);}}
string Json(WorldState s)=>JsonSerializer.Serialize(s,SaveStore.JsonOptions);
WorldState Parse(string json)=>JsonSerializer.Deserialize<WorldState>(json,SaveStore.JsonOptions)!;
void Rejected(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Expected controlled InvalidDataException");}
void Step(WorldSimulation sim){sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);}
void Sell(WorldSimulation sim,Creature c){int money=sim.State.Money,price=CourtyardEconomy.Quote(c).Price;string result=sim.SellCreatureToShop(c.Id,price);Require(!sim.State.Creatures.Contains(c)&&sim.State.CreatureSales.Any(x=>x.Creature.Id==c.Id)&&sim.State.Money==money+price,result);}
Dictionary<string,string> Hashes(string dir)=>Directory.GetFiles(dir).ToDictionary(p=>Path.GetFileName(p)!,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))!;
void Unchanged(string dir,Dictionary<string,string> before){var after=Hashes(dir);Require(before.Count==after.Count&&before.All(x=>after.TryGetValue(x.Key,out var hash)&&hash==x.Value),"Save files changed or a file appeared/disappeared");}
(string Good,string Bad,string Sold,string Parcel) Fixture(RoomObjectKind kind,bool reverse)
{
    var sim=WorldSimulation.CreateCourtyardEconomy(42);sim.State.AutonomousBreeding=false;
    var sold=sim.State.Creatures[0];Sell(sim,sold);
    var parcel=new RoomObject{Kind=kind,Portions=1,RegionId=sold.RegionId,X=0,Z=0};sim.State.RoomObjects.Add(parcel);
    if(reverse){sim.State.Creatures.Reverse();sim.State.RoomObjects.Reverse();sim.State.CreatureSales.Reverse();}
    SaveStore.Validate(sim.State);string good=Json(sim.State);
    var node=JsonNode.Parse(good)!;
    node["RoomObjects"]!.AsArray().Single(x=>x!["Id"]!.GetValue<string>()==parcel.Id)!["CarrierId"]=sold.Id;
    node["CreatureSales"]!.AsArray().Single(x=>x!["Creature"]!["Id"]!.GetValue<string>()==sold.Id)!["Creature"]!["Family"]!["CareParcelId"]=parcel.Id;
    return(good,node.ToJsonString(SaveStore.JsonOptions),sold.Id,parcel.Id);
}
foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Water,RoomObjectKind.Sweet})foreach(bool reverse in new[]{false,true})
{
    var f=Fixture(kind,reverse);string prefix=kind+"-"+(reverse?"reverse":"forward");
    Check(prefix+"-normal-sale-history-roundtrip",()=>{var s=Parse(f.Good);string before=Json(s);SaveStore.Validate(s);Require(Json(s)==before,"Validation mutated state");var store=new SaveStore(Path.Combine(output,prefix+"-valid"));store.Save(s,1);Require(Json(store.Load(1).State)==before,"Round trip changed archived history");Require(s.Creatures.Any(c=>c.Family.Peers.ContainsKey(f.Sold))&&s.FamilyRelationshipEvents.Any(x=>x.FirstId==f.Sold||x.SecondId==f.Sold),"Fixture lacks historical peer/event references");});
    Check(prefix+"-reject-archived-carrier",()=>{var s=Parse(f.Bad);string before=Json(s);try{Rejected(()=>SaveStore.Validate(s));}finally{Require(Json(s)==before,"Validation repaired state");}});
    foreach(string mode in new[]{"backup","no-backup","both-bad","main-priority","candidate","existing-main"})
    Check(prefix+"-"+mode,()=>{
        string dir=Path.Combine(output,prefix+"-"+mode);var store=new SaveStore(dir);string main=store.SlotPath(1);
        File.WriteAllText(main,mode is "candidate" or "main-priority"?f.Good:f.Bad);
        if(mode!="no-backup")File.WriteAllText(main+".bak",mode is "both-bad" or "main-priority"?f.Bad:f.Good);
        File.WriteAllText(main+".writing","preexisting interrupted write sentinel");var hashes=Hashes(dir);
        try{
            if(mode=="backup"){var loaded=store.Load(1);Require(loaded.RecoveredBackup&&Json(loaded.State)==Json(Parse(f.Good)),"Invalid main took precedence over valid backup");}
            else if(mode=="main-priority"){var loaded=store.Load(1);Require(!loaded.RecoveredBackup&&Json(loaded.State)==Json(Parse(f.Good)),"Valid main lost priority");}
            else if(mode=="candidate")Rejected(()=>store.Save(Parse(f.Bad),1));
            else if(mode=="existing-main")Rejected(()=>store.Save(Parse(f.Good),1));
            else Rejected(()=>store.Load(1));
        }finally{var after=Hashes(dir);fileEvidence.Add(new{scenario=prefix+"-"+mode,before=hashes,after,unchanged=hashes.Count==after.Count&&hashes.All(x=>after.TryGetValue(x.Key,out var h)&&h==x.Value)});Unchanged(dir,hashes);}
    });
    Check(prefix+"-archived-carrier-does-not-deliver-player-can-recover",()=>{
        // Deliberately construct the malformed state directly, even once validation rejects it.
        var sim=new WorldSimulation(Parse(f.Bad));var parcel=sim.State.RoomObjects.Single(o=>o.Id==f.Parcel);
        string archive=JsonSerializer.Serialize(sim.State.CreatureSales,SaveStore.JsonOptions);
        for(int n=0;n<30;n++)Step(sim);
        Require(parcel.CarrierId==f.Sold&&parcel.Portions==1&&JsonSerializer.Serialize(sim.State.CreatureSales,SaveStore.JsonOptions)==archive,"Unexpected archived simulation/delivery");
        Require(sim.HoldSupply(parcel.Id)&&parcel.CarrierId==null&&parcel.HeldByHand&&parcel.Portions==1,"Player recovery failed");
    });
}
Check("real-active-care-survives-roundtrip-and-sale-drops-parcel",()=>{
    var sim=WorldSimulation.CreateCourtyardEconomy(42);sim.State.AutonomousBreeding=false;sim.State.RoomObjects.Clear();sim.State.PlayerX=0;sim.State.PlayerZ=7;
    for(int n=0;n<sim.State.Creatures.Count;n++){var c=sim.State.Creatures[n];c.X=c.TargetX=n==0?0:n==2?3:-6;c.Z=c.TargetZ=0;c.Fullness=c.Energy=c.Health=c.Interaction.Hydration=90;c.Pain=c.Fear=0;c.Family.NextCareAt=c.Family.NextSocialAt=c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;}
    var adult=sim.State.Creatures[0];var child=sim.State.Creatures[2];child.IsYoung=true;child.Body.AgeSeconds=0;child.Family.MotherId=adult.Id;child.ParentId=adult.Id;child.Fullness=30;
    Require(sim.PlaceInteractionSupply(InteractionAction.PlaceMeal,0,-1.2).Success,"Actual supply placement failed");
    string result=sim.ArrangeCare(adult.Id,child.Id);Require(adult.Family.CareTargetId==child.Id,result);
    for(int n=0;n<150&&adult.Family.CareParcelId==null;n++)Step(sim);
    Require(adult.Family.CareParcelId!=null,"Actual caregiver did not pick up a portion: "+adult.Family.CareLastEvent);
    var parcel=sim.State.RoomObjects.Single(o=>o.Id==adult.Family.CareParcelId);Require(parcel.CarrierId==adult.Id&&parcel.Portions==1,"Active portion incorrect");
    var store=new SaveStore(Path.Combine(output,"active-care"));string before=Json(sim.State);store.Save(sim.State,1);Require(Json(store.Load(1).State)==before,"Active carry altered by round trip");
    Sell(sim,adult);Require(parcel.CarrierId==null&&parcel.Portions==1&&adult.Family.CareParcelId==null&&adult.Family.CareTargetId==null,"Sale did not drop actual parcel");SaveStore.Validate(sim.State);store.Save(sim.State,2);Require(Json(store.Load(2).State)==Json(sim.State),"Normal care sale invalid");
});
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{checks=results.Count,failures,results,fileEvidence},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"CHECKS {results.Count} FAILURES {failures}");return failures==0?0:1;

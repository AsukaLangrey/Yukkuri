using Yukkuri.Simulation;

int passed=0;
void Check(bool ok,string message) { if(!ok)throw new Exception(message);passed++;Console.WriteLine("PASS: "+message); }
void Reject(Action action,string message) { try{action();}catch(Exception e) when(e is InvalidDataException or ArgumentOutOfRangeException){Check(true,message);return;}throw new Exception("NOT REJECTED: "+message); }
string Json(WorldState s)=>System.Text.Json.JsonSerializer.Serialize(s,SaveStore.JsonOptions);
var sim=WorldSimulation.Create();
Check(WorldSimulation.FormatClock(32)=="00:32"&&WorldSimulation.FormatClock(59.9)=="00:59"&&WorldSimulation.FormatClock(60)=="01:00","clock display floors elapsed minutes instead of rounding at 30 seconds");
var initial=sim.Selected!;
string id=initial.Id,body=initial.Body.Id,identity=initial.Registration.IdentityId;
double hunger=initial.Fullness;
sim.Advance(1);
Check(initial.Fullness<hunger,"normal simulation consumes food needs");
sim.EnterCloseUp();
var frozen=Json(sim.State);
sim.Advance(7200);
Check(frozen==Json(sim.State),"all state and random state freeze during close-up");
int food=sim.State.Food;
Check(sim.Feed(id)&&sim.State.Food==food-1,"direct close-up feeding changes resources once");
double time=sim.State.WorldSeconds;sim.ExitCloseUp();sim.Advance(0.25);
Check(sim.State.WorldSeconds==time+0.25,"exit has no catch-up");
sim.Travel("wild");double before=initial.InnerAgeSeconds;sim.Advance(2);sim.Travel("home");
Check(initial.InnerAgeSeconds==before+2&&initial.Id==id&&initial.Body.Id==body&&initial.Registration.IdentityId==identity,"inactive region advances and stable identities survive travel");
sim.Select(id);initial.TrueDisposition="greedy";sim.Observe();string guessed=initial.Observation.Hypothesis;
sim.Feed(id);sim.Feed(id);sim.Observe();
Check(guessed!=initial.Observation.Hypothesis&&initial.TrueDisposition=="greedy","new evidence corrects uncertain observation, not the true trait");
Check(!System.Text.Json.JsonSerializer.Serialize(sim.View(initial)).Contains("greedy"),"UI projection does not expose true trait");
int earned=sim.State.Money;Check(sim.CompleteCareRecord()&&sim.State.Money==earned+40,"care evidence and wellbeing earn one commission reward");
Check(!sim.CompleteCareRecord()&&sim.State.Money==earned+40,"care reward cannot be claimed twice");
sim.Travel("wild");var wild=sim.Selected!;string wildId=wild.Id;
Check(sim.Rescue()&&wild.RegionId=="home"&&wild.Quarantine&&wild.Id==wildId,"rescue preserves identity and enters observation area");
sim.Travel("home");
int money=sim.State.Money;
Check(sim.Place(FacilityKind.Bed,0,0)&&sim.State.Money==money-sim.Config.BedCost,"placement consumes actual funds");
money=sim.State.Money;Check(!sim.Place(FacilityKind.Feeder,0,0)&&sim.State.Money==money,"overlapping placement rejected without charging");
sim.State.Food=0;hunger=sim.Selected!.Fullness;Check(!sim.Feed(sim.Selected.Id)&&sim.Selected.Fullness==hunger,"empty inventory cannot feed");
sim.State.Money=0;Check(!sim.BuyFood()&&!sim.Place(FacilityKind.Bed,7,7),"insufficient funds reject purchases and construction");
sim.State.Money=100;sim.State.Food=100;initial.Fullness=5;sim.Advance(10);
Check(initial.Fullness>5,"staff supplies a hungry resident without player action");
Check(initial.Registration.Grade=="未评级"&&!initial.Registration.IndependentStatus,"staff does not award certification or independent identity");
Reject(()=>sim.Advance(double.NaN),"nonfinite timestep rejected");
Reject(()=>SimulationConfig.Parse("{\"TickSeconds\":0}"),"zero simulation tick rejected");
string root=Path.Combine(Path.GetTempPath(),"yukkuri-checks",Guid.NewGuid().ToString("N"));
var store=new SaveStore(root);
sim.State.Mode=PlayMode.Management;store.Save(sim.State,1);
var restored=store.Load(1);
Check(Json(restored.State)==Json(sim.State),"save round-trip preserves entire state, no offline time");
var continuation=new WorldSimulation(restored.State);sim.Advance(3);continuation.Advance(3);
Check(Json(continuation.State)==Json(sim.State),"random state and tick remainder continue deterministically after load");
store.Save(sim.State,1);string backup=File.ReadAllText(store.SlotPath(1)+".bak");
File.WriteAllText(store.SlotPath(1),"{truncated");
Check(store.Load(1).RecoveredBackup,"truncated primary recovers from valid backup");
Reject(()=>store.Save(sim.State,1),"corrupt primary cannot overwrite good backup");
Check(File.ReadAllText(store.SlotPath(1)+".bak")==backup&&File.ReadAllText(store.SlotPath(1))=="{truncated","failed save preserves both source files");
store.Save(sim.State,2);Check(File.Exists(store.SlotPath(2)),"another manual slot remains usable");
Reject(()=>store.SlotPath(100),"invalid slot rejected");
sim.State.SchemaVersion=999;Reject(()=>store.Save(sim.State,3),"unknown schema refused");sim.State.SchemaVersion=1;
sim.State.Creatures[1].Id=sim.State.Creatures[0].Id;Reject(()=>SaveStore.Validate(sim.State),"duplicate identity refused");
var invalid=WorldSimulation.Create().State;invalid.Regions.Add(null!);Reject(()=>SaveStore.Validate(invalid),"null region rejected with controlled invalid-data result");
invalid=WorldSimulation.Create().State;invalid.Creatures[0].Observation.Notes=null!;Reject(()=>SaveStore.Validate(invalid),"null observations rejected before UI reads them");
invalid=WorldSimulation.Create().State;invalid.PlayerX=double.PositiveInfinity;Reject(()=>SaveStore.Validate(invalid),"nonfinite player position rejected");
// Synthetic corruption cases exercise fallback; they are not historical-save compatibility evidence.
var structureStore=new SaveStore(Path.Combine(root,"structure-corruption"));
var validInteraction=WorldSimulation.CreateInteractionPlaytest().State;
structureStore.Save(validInteraction,1);structureStore.Save(validInteraction,1);
string structureBackup=File.ReadAllText(structureStore.SlotPath(1)+".bak");
void RecoverStructuralCorruption(WorldState damaged,string description)
{
    string damagedJson=Json(damaged);File.WriteAllText(structureStore.SlotPath(1),damagedJson);
    Reject(()=>SaveStore.Validate(damaged),description+" is rejected as invalid data");
    var recovered=structureStore.Load(1);
    Check(recovered.RecoveredBackup&&Json(recovered.State)==Json(validInteraction),description+" falls back to the valid backup");
    Reject(()=>structureStore.Save(validInteraction,1),description+" cannot replace the good backup");
    Check(File.ReadAllText(structureStore.SlotPath(1))==damagedJson&&
        File.ReadAllText(structureStore.SlotPath(1)+".bak")==structureBackup,description+" leaves both files unchanged");
}
var pendingWithNull=WorldSimulation.CreateInteractionPlaytest().State;
var pendingLife=pendingWithNull.Creatures[0].Interaction;
var pendingSource=pendingWithNull.RoomObjects.First(o=>o.Kind==RoomObjectKind.Leaves);
pendingSource.Portions--;
pendingLife.GroundIntake=true;pendingLife.NaturalIntakePending=true;pendingLife.EatingRemaining=1;
pendingLife.GroundIntakeSourceId=pendingSource.Id;pendingLife.GroundIntakeKind=pendingSource.Kind;
pendingWithNull.RoomObjects.Insert(0,null!);
RecoverStructuralCorruption(pendingWithNull,"null object before a pending intake reference");
var missingLearning=WorldSimulation.CreateInteractionPlaytest().State;
missingLearning.Creatures[0].Interaction.ActiveCue=LearnedCue.Wait;
missingLearning.Creatures[0].Interaction.Learning.Clear();
RecoverStructuralCorruption(missingLearning,"active lesson without its learning record");
missingLearning.Creatures[0].Interaction.ActiveCue=null;
SaveStore.Validate(missingLearning);
Check(missingLearning.Creatures[0].Interaction.Learning.Count==0,"inactive empty learning records remain valid");
var nullSpeech=WorldSimulation.CreateInteractionPlaytest().State;
nullSpeech.Creatures[0].Speech=null!;nullSpeech.Creatures[0].ReactionRemaining=4;
RecoverStructuralCorruption(nullSpeech,"null speech with a live reaction");
// Omitting a field is different from explicitly corrupting it with null.
var omittedSpeech=System.Text.Json.Nodes.JsonNode.Parse(Json(validInteraction))!;
foreach(var creature in omittedSpeech["Creatures"]!.AsArray())creature!.AsObject().Remove("Speech");
File.WriteAllText(structureStore.SlotPath(2),omittedSpeech.ToJsonString());
var defaultSpeech=structureStore.Load(2);
Check(!defaultSpeech.RecoveredBackup&&defaultSpeech.State.Creatures.All(c=>c.Speech==""),
    "synthetic missing speech fields retain safe defaults without backup recovery");

// A directory in place of the primary deterministically exercises access-denied file opening,
// without changing permissions on real saves. These are isolated synthetic fixtures.
var accessStore=new SaveStore(Path.Combine(root,"inaccessible-primary"));
string accessPrimary=accessStore.SlotPath(1),accessBackup=accessPrimary+".bak";
Directory.CreateDirectory(accessPrimary);File.WriteAllText(accessBackup,Json(validInteraction));
string accessBackupBefore=File.ReadAllText(accessBackup);
var accessRecovered=accessStore.Load(1);
Check(accessRecovered.RecoveredBackup&&Json(accessRecovered.State)==Json(validInteraction),
    "access-denied primary falls back to a valid backup");
Check(Directory.Exists(accessPrimary)&&File.ReadAllText(accessBackup)==accessBackupBefore,
    "access-denied fallback leaves the primary path and backup unchanged");
Directory.CreateDirectory(accessStore.SlotPath(2));Directory.CreateDirectory(accessStore.SlotPath(2)+".bak");
Reject(()=>accessStore.Load(2),"two access-denied paths produce a controlled load failure");
Check(Directory.Exists(accessStore.SlotPath(2))&&Directory.Exists(accessStore.SlotPath(2)+".bak"),
    "failed access-denied load preserves both paths");

var writeFailureStore=new SaveStore(Path.Combine(root,"write-failure"));
writeFailureStore.Save(validInteraction,1);writeFailureStore.Save(validInteraction,1);
string protectedPrimary=File.ReadAllText(writeFailureStore.SlotPath(1));
string protectedBackup=File.ReadAllText(writeFailureStore.SlotPath(1)+".bak");
using(var lockedTemporary=new FileStream(writeFailureStore.SlotPath(1)+".writing",FileMode.Create,FileAccess.Write,FileShare.None))
{
    bool writeFailed=false;
    try{writeFailureStore.Save(validInteraction,1);}
    catch(Exception error) when(error is IOException or UnauthorizedAccessException){writeFailed=true;}
    Check(writeFailed,"locked temporary file refuses save");
    Check(File.ReadAllText(writeFailureStore.SlotPath(1))==protectedPrimary&&
        File.ReadAllText(writeFailureStore.SlotPath(1)+".bak")==protectedBackup,
        "temporary write failure preserves the valid primary and backup");
}
Reject(()=>writeFailureStore.Save(nullSpeech,1),"null speech cannot be saved over a valid primary");
Check(File.ReadAllText(writeFailureStore.SlotPath(1))==protectedPrimary&&
    File.ReadAllText(writeFailureStore.SlotPath(1)+".bak")==protectedBackup,
    "invalid candidate preserves both valid save generations");
var isolated=WorldSimulation.Create();isolated.Travel("wild");var rescued=isolated.Selected!;isolated.Rescue();isolated.Advance(120);
Check(rescued.X is >= -7 and <= -4.5&&rescued.Z is >= 4.2 and <= 6.6,"quarantine resident stays within the observation area");
var crowd=WorldSimulation.Create(100);crowd.Advance(20);
var home=crowd.State.Creatures.Where(c=>c.RegionId=="home").ToArray();
int overlapping=home.Count(a=>home.Any(b=>a.Id!=b.Id&&WorldSimulation.Distance(a.X,a.Z,b.X,b.Z)<.45));
Console.WriteLine($"CROWD_CHECK: {overlapping}/100 have a neighbor closer than 0.45 world units after 20 seconds.");
Check(overlapping<10,"100-resident feeder crowd avoids near-total body overlap");
var benchmark=WorldSimulation.Create(100);var watch=System.Diagnostics.Stopwatch.StartNew();benchmark.Advance(600);watch.Stop();
Console.WriteLine($"CORE_BENCHMARK: 106 total creatures, 600 simulated seconds in {watch.Elapsed.TotalMilliseconds:F1}ms; not GPU FPS.");
Console.WriteLine($"RESULT: {passed} checks passed; test files retained at {root}");

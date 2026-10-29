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

// Test the actual file boundary: property setters reject some malformed physiology before
// Validate can run, while Save must also reject inconsistent states built in memory.
var inconsistentPhysiology=WorldSimulation.CreateInteractionPlaytest().State;
inconsistentPhysiology.Creatures[0].Interaction.Physiology.Excreting=ExcretionKind.Urine;
RecoverStructuralCorruption(inconsistentPhysiology,"excretion kind without remaining time");
Reject(()=>writeFailureStore.Save(inconsistentPhysiology,1),"inconsistent physiology cannot replace a valid save");
Check(File.ReadAllText(writeFailureStore.SlotPath(1))==protectedPrimary&&
    File.ReadAllText(writeFailureStore.SlotPath(1)+".bak")==protectedBackup,
    "physiology validation failure preserves both valid save generations");
foreach(var (description,physiologyJson) in new (string,string)[]{
    ("explicit null physiology","null"),
    ("negative bladder","{\"Bladder\":-1}"),
    ("bowel above its limit","{\"Bowel\":101}"),
    ("remaining time above its limit","{\"Excreting\":\"Urine\",\"Remaining\":4.01}"),
    ("undefined numeric excretion kind","{\"Excreting\":99,\"Remaining\":1}"),
    ("negative urination count","{\"Urinations\":-1}"),
    ("overflowing defecation count","{\"Defecations\":2147483648}"),
    ("remaining time without an excretion kind","{\"Remaining\":1}")})
{
    var damaged=System.Text.Json.Nodes.JsonNode.Parse(Json(validInteraction))!;
    damaged["Creatures"]![0]!["Interaction"]!["Physiology"]=System.Text.Json.Nodes.JsonNode.Parse(physiologyJson);
    string damagedJson=damaged.ToJsonString();File.WriteAllText(structureStore.SlotPath(1),damagedJson);
    var recovered=structureStore.Load(1);
    Check(recovered.RecoveredBackup&&Json(recovered.State)==Json(validInteraction),description+" recovers the valid backup");
    Reject(()=>structureStore.Save(validInteraction,1),description+" cannot overwrite the good backup");
    Check(File.ReadAllText(structureStore.SlotPath(1))==damagedJson&&
        File.ReadAllText(structureStore.SlotPath(1)+".bak")==structureBackup,description+" preserves both files");
}
foreach(bool omitWholeObject in new[]{true,false})
{
    var oldFields=System.Text.Json.Nodes.JsonNode.Parse(Json(validInteraction))!;
    foreach(var creature in oldFields["Creatures"]!.AsArray())
    {
        var life=creature!["Interaction"]!.AsObject();
        if(omitWholeObject)life.Remove("Physiology");else life["Physiology"]=new System.Text.Json.Nodes.JsonObject();
    }
    File.WriteAllText(structureStore.SlotPath(2),oldFields.ToJsonString());
    var legacy=structureStore.Load(2);
    Check(!legacy.RecoveredBackup&&legacy.State.Creatures.All(c=>c.Interaction.Physiology is
        {Bladder:35,Bowel:20,Excreting:ExcretionKind.None,Remaining:0,Urinations:0,Defecations:0}),
        (omitWholeObject?"missing physiology object":"missing physiology fields")+" uses idle defaults without backup fallback");
}
var wasteState=WorldSimulation.CreateInteractionPlaytest().State;
var wasteOwner=wasteState.Creatures[0];
wasteOwner.Interaction.Physiology.Bladder=100;wasteOwner.Interaction.Physiology.Bowel=100;
wasteOwner.Interaction.Physiology.Urinations=int.MaxValue;wasteOwner.Interaction.Physiology.Defecations=int.MaxValue;
wasteOwner.Interaction.Physiology.Excreting=ExcretionKind.Feces;wasteOwner.Interaction.Physiology.Remaining=1.25;
foreach(var kind in new[]{RoomObjectKind.Urine,RoomObjectKind.Feces})
    for(int n=0;n<64;n++)wasteState.RoomObjects.Add(new(){Id=$"waste-{kind}-{n}",Kind=kind,
        OwnerId=wasteOwner.Id,RegionId=wasteOwner.RegionId,X=0,Z=0,Portions=1000,Contamination=1});
var wasteStore=new SaveStore(Path.Combine(root,"physiology-boundary"));
wasteStore.Save(wasteState,1);var wasteLoaded=wasteStore.Load(1);
Check(!wasteLoaded.RecoveredBackup&&Json(wasteLoaded.State)==Json(wasteState),
    "active excretion, saturated counters and both full waste collections survive exact save round-trip");
var wasteSimulation=new WorldSimulation(wasteLoaded.State);wasteSimulation.State.UnlimitedResources=false;
int wasteFood=wasteSimulation.State.Food,wasteCount=wasteSimulation.State.RoomObjects.Count;
string nonWasteBefore=System.Text.Json.JsonSerializer.Serialize(wasteSimulation.State.RoomObjects.Where(o=>!WorldSimulation.IsWaste(o.Kind)),SaveStore.JsonOptions);
int cleanedWaste=0;
foreach(var waste in wasteSimulation.State.RoomObjects.Where(o=>WorldSimulation.IsWaste(o.Kind)).ToArray())
    if(wasteSimulation.CleanRoomWaste(waste.Id))cleanedWaste++;
Check(cleanedWaste==128&&wasteSimulation.State.Food==wasteFood&&wasteSimulation.State.RoomObjects.Count==wasteCount-128&&
    System.Text.Json.JsonSerializer.Serialize(wasteSimulation.State.RoomObjects.Where(o=>!WorldSimulation.IsWaste(o.Kind)),SaveStore.JsonOptions)==nonWasteBefore,
    "cleaning loaded saturated waste refunds no food and preserves every unrelated object");
SaveStore.Validate(wasteSimulation.State);
// Automatic production must be reproducible from an actual saved state. Existing
// non-waste IDs deliberately occupy the first candidates, including a stored object abroad.
foreach(var kind in new[]{RoomObjectKind.Urine,RoomObjectKind.Feces})foreach(bool saturated in new[]{false,true})
{
    var pendingState=WorldSimulation.CreateInteractionPlaytest().State;
    pendingState.Creatures=[pendingState.Creatures[0]];pendingState.RoomObjects.Clear();
    var producer=pendingState.Creatures[0];producer.Fullness=90;producer.Energy=90;
    producer.DecisionIn=10000;producer.Interaction.NextAutonomyAt=10000;
    int initialCount=saturated?int.MaxValue:7;
    producer.Interaction.Physiology.Urinations=initialCount;producer.Interaction.Physiology.Defecations=initialCount;
    pendingState.Regions.Add(new(){Id="waste-id-other",Name="ID collision fixture",Kind=RegionKind.Sanctuary});
    string candidate=FormattableString.Invariant($"waste:{producer.Id}:{(int)kind}:{(long)initialCount+1}");
    pendingState.RoomObjects.Add(new(){Id=candidate,Kind=RoomObjectKind.Treasure,RegionId="waste-id-other",Stored=true});
    pendingState.RoomObjects.Add(new(){Id=candidate+":1",Kind=RoomObjectKind.Cushion,X=6,Z=6});
    var continuationStore=new SaveStore(Path.Combine(root,$"waste-continuation-{kind}-{saturated}"));
    // The second completion also checks uniqueness when the completed counter stays saturated.
    for(int completion=0;completion<2;completion++)
    {
        var pending=pendingState.Creatures[0].Interaction.Physiology;
        pending.Bladder=90;pending.Bowel=90;pending.Excreting=kind==RoomObjectKind.Urine?ExcretionKind.Urine:ExcretionKind.Feces;
        pending.Remaining=.25;
        int countBefore=kind==RoomObjectKind.Urine?pending.Urinations:pending.Defecations;
        var existingIds=pendingState.RoomObjects.Select(o=>o.Id).ToHashSet(StringComparer.Ordinal);
        string existingObjects=System.Text.Json.JsonSerializer.Serialize(pendingState.RoomObjects,SaveStore.JsonOptions);
        continuationStore.Save(pendingState,completion+1);
        var left=new WorldSimulation(continuationStore.Load(completion+1).State);
        var right=new WorldSimulation(continuationStore.Load(completion+1).State);
        Check(Json(left.State)==Json(right.State),"waste continuation starts from identical saved branches");
        for(int step=0;step<3;step++)
        {
            left.AdvanceInteractionPresentation(.1);left.Advance(.1);
            right.AdvanceInteractionPresentation(.1);right.Advance(.1);
        }
        Check(Json(left.State)==Json(right.State),$"{kind} completion {completion+1} resumes deterministically with saturated={saturated}");
        var generated=left.State.RoomObjects.Where(o=>!existingIds.Contains(o.Id)).ToArray();
        var finished=left.State.Creatures[0].Interaction.Physiology;
        int countAfter=kind==RoomObjectKind.Urine?finished.Urinations:finished.Defecations;
        Check(generated.Length==1&&generated[0].Kind==kind&&generated[0].OwnerId==producer.Id&&
            left.State.RoomObjects.Count==existingIds.Count+1&&
            left.State.RoomObjects.Select(o=>o.Id).Distinct(StringComparer.Ordinal).Count()==left.State.RoomObjects.Count&&
            !finished.Active&&countAfter==Math.Min(int.MaxValue-1,countBefore)+1,
            "automatic waste avoids all existing IDs and completes exactly once at the counter boundary");
        Check(System.Text.Json.JsonSerializer.Serialize(left.State.RoomObjects.Where(o=>existingIds.Contains(o.Id)),SaveStore.JsonOptions)==existingObjects,
            "automatic waste ID conflict resolution never renames, replaces or removes existing objects");
        SaveStore.Validate(left.State);SaveStore.Validate(right.State);
        pendingState=left.State;
    }
}
foreach(var kind in new[]{RoomObjectKind.Urine,RoomObjectKind.Feces})
{
    wasteOwner.Interaction.Physiology.Excreting=kind==RoomObjectKind.Urine?ExcretionKind.Urine:ExcretionKind.Feces;
    wasteOwner.Interaction.Physiology.Remaining=.25;
    string cappedObjects=System.Text.Json.JsonSerializer.Serialize(wasteState.RoomObjects,SaveStore.JsonOptions);
    wasteStore.Save(wasteState,2);
    var cappedLeft=new WorldSimulation(wasteStore.Load(2).State);
    var cappedRight=new WorldSimulation(wasteStore.Load(2).State);
    for(int step=0;step<3;step++)
    {
        cappedLeft.AdvanceInteractionPresentation(.1);cappedLeft.Advance(.1);
        cappedRight.AdvanceInteractionPresentation(.1);cappedRight.Advance(.1);
    }
    Check(Json(cappedLeft.State)==Json(cappedRight.State),"full waste collections continue deterministically for "+kind);
    var cappedPhysiology=cappedLeft.State.Creatures[0].Interaction.Physiology;
    Check(!cappedPhysiology.Active&&cappedPhysiology.Urinations==int.MaxValue&&cappedPhysiology.Defecations==int.MaxValue&&
        System.Text.Json.JsonSerializer.Serialize(cappedLeft.State.RoomObjects,SaveStore.JsonOptions)==cappedObjects,
        "completion at 64 objects and 1000 portions preserves existing IDs, objects and saturated counters");
    SaveStore.Validate(cappedLeft.State);SaveStore.Validate(cappedRight.State);
}
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

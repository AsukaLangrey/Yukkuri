using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

string output=Path.GetFullPath(args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"yukkuri-water-save-"+Guid.NewGuid().ToString("N")));
if(Directory.Exists(output))throw new IOException("Choose a new output directory; previous evidence must be retained.");
Directory.CreateDirectory(output);
var results=new List<object>();
int failures=0,checks=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
string Json(WorldState state)=>JsonSerializer.Serialize(state,SaveStore.JsonOptions);
// Malformed states cannot safely evaluate derived read-only getters such as CanWalk.
string RawJson(WorldState state)=>JsonSerializer.Serialize(state,new JsonSerializerOptions(SaveStore.JsonOptions){IgnoreReadOnlyProperties=true});
WorldState ReadJson(string json)=>JsonSerializer.Deserialize<WorldState>(json,SaveStore.JsonOptions)!;
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
InvalidDataException Reject(Action action)
{
    try{action();}
    catch(InvalidDataException e){return e;}
    throw new Exception("Expected a controlled InvalidDataException.");
}
void Check(string name,Action<string> test)
{
    checks++;string folder=Path.Combine(output,$"{checks:00}-{name}");Directory.CreateDirectory(folder);
    try{test(folder);results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}
    catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e);}
}
WorldState Fixture(string reference,bool targetFirst=false)
{
    var state=WorldSimulation.CreateCourtyardEconomy().State;state.WorldSeconds=10;
    var observer=state.Creatures[0];var target=state.Creatures[1];
    target.Interaction.Water.EventId="water-save-fixture";target.Interaction.Water.EnteredAt=0;
    if(reference=="concern")
    {
        observer.Personality.WaterConcernId=target.Id;
        observer.Personality.WaterConcernEventId=target.Interaction.Water.EventId;
    }
    if(reference=="aftercare")
        observer.Personality.WaterAftercare=new(){TargetId=target.Id,EventId=target.Interaction.Water.EventId,StartedAt=5,Until=35};
    if(targetFirst){state.Creatures.Remove(target);state.Creatures.Insert(0,target);}
    SaveStore.Validate(state);return state;
}
string CorruptWater(WorldState state,bool observer=false)
{
    string id=state.Creatures.Single(c=>observer?
        c.Personality.WaterConcernEventId!=null||c.Personality.WaterAftercare.TargetId!=null:
        c.Interaction.Water.EventId=="water-save-fixture").Id;
    var node=JsonNode.Parse(Json(state))!;
    var creature=node["Creatures"]!.AsArray().Single(c=>c!["Id"]!.GetValue<string>()==id)!;
    creature["Interaction"]!["Water"]=null;
    return node.ToJsonString();
}
void WithFiles(string folder,string primary,string backup,Action<SaveStore> action)
{
    var store=new SaveStore(folder);string main=store.SlotPath(1),bak=main+".bak";
    File.WriteAllText(main,primary);File.WriteAllText(bak,backup);
    string mainBefore=Hash(main),backupBefore=Hash(bak);
    try{action(store);}
    finally
    {
        string mainAfter=Hash(main),backupAfter=Hash(bak);
        File.WriteAllText(Path.Combine(folder,"hashes.json"),JsonSerializer.Serialize(new
        {mainBefore,mainAfter,backupBefore,backupAfter,primaryUnchanged=mainBefore==mainAfter,backupUnchanged=backupBefore==backupAfter},SaveStore.JsonOptions));
        Require(mainBefore==mainAfter&&backupBefore==backupAfter,"Original main or backup bytes changed.");
    }
}
foreach(string reference in new[]{"concern","aftercare"})foreach(bool targetFirst in new[]{false,true})
{
    string tag=reference+"-"+(targetFirst?"target-first":"observer-first");
    Check(tag+"-valid-read-only-round-trip",folder=>
    {
        var state=Fixture(reference,targetFirst);string before=Json(state);
        for(int n=0;n<3;n++)SaveStore.Validate(state);
        Require(Json(state)==before,"Validation changed legitimate state, time or RNG.");
        WithFiles(folder,before,CorruptWater(state),store=>
        {
            var result=store.Load(1);
            Require(!result.RecoveredBackup&&Json(result.State)==before,"A valid main did not round trip exactly.");
        });
    });
    Check(tag+"-null-target-controlled-rejection",folder=>
    {
        var state=ReadJson(CorruptWater(Fixture(reference,targetFirst)));string before=RawJson(state);
        Reject(()=>SaveStore.Validate(state));Require(RawJson(state)==before,"Rejecting malformed state mutated it.");
    });
    Check(tag+"-valid-backup-recovered-originals-preserved",folder=>
    {
        var state=Fixture(reference,targetFirst);string good=Json(state);
        WithFiles(folder,CorruptWater(state),good,store=>
        {
            var result=store.Load(1);
            Require(result.RecoveredBackup&&Json(result.State)==good,"Valid backup was not recovered exactly.");
            Reject(()=>store.Save(state,1));
        });
    });
    Check(tag+"-bad-existing-main-not-overwritten",folder=>
    {
        var state=Fixture(reference,targetFirst);string good=Json(state);
        WithFiles(folder,CorruptWater(state),good,store=>Reject(()=>store.Save(state,1)));
    });
    Check(tag+"-both-bad-controlled-aggregate",folder=>
    {
        string bad=CorruptWater(Fixture(reference,targetFirst));
        WithFiles(folder,bad,bad,store=>
        {
            var error=Reject(()=>store.Load(1));
            Require(error.InnerException is AggregateException aggregate&&aggregate.InnerExceptions.Count==2&&
                aggregate.InnerExceptions.All(e=>e is InvalidDataException),"Both read failures were not preserved as controlled errors.");
        });
    });
    Check(tag+"-bad-candidate-preserves-valid-history",folder=>
    {
        var state=Fixture(reference,targetFirst);string good=Json(state);var bad=ReadJson(CorruptWater(state));
        WithFiles(folder,good,good,store=>Reject(()=>store.Save(bad,1)));
    });
    Check(tag+"-valid-continuation-unchanged",folder=>
    {
        string good=Json(Fixture(reference,targetFirst));
        var plain=new WorldSimulation(ReadJson(good));var validated=new WorldSimulation(ReadJson(good));
        for(int step=0;step<40;step++)
        {
            SaveStore.Validate(validated.State);
            plain.AdvanceInteractionPresentation(.05);plain.Advance(.05);
            validated.AdvanceInteractionPresentation(.05);validated.Advance(.05);
        }
        SaveStore.Validate(plain.State);SaveStore.Validate(validated.State);
        Require(Json(plain.State)==Json(validated.State),"Repeated validation changed subsequent simulation.");
    });
}
Check("null-water-without-cross-reference-still-recovers",folder=>
{
    var state=Fixture("none");string good=Json(state);
    WithFiles(folder,CorruptWater(state),good,store=>Require(store.Load(1).RecoveredBackup,"Unreferenced corruption did not recover."));
});
Check("missing-legacy-water-keeps-default-and-does-not-recover",folder=>
{
    var state=WorldSimulation.CreateCourtyardEconomy().State;string good=Json(state);
    var node=JsonNode.Parse(good)!;
    foreach(var c in node["Creatures"]!.AsArray())c!["Interaction"]!.AsObject().Remove("Water");
    WithFiles(folder,node.ToJsonString(),good,store=>
    {
        var result=store.Load(1);
        Require(!result.RecoveredBackup&&Json(result.State)==good,"Missing legacy Water was not restored by its neutral default.");
    });
});
foreach(string reference in new[]{"concern","aftercare"})Check(reference+"-null-observer-still-recovers",folder=>
{
    var state=Fixture(reference);string good=Json(state);
    WithFiles(folder,CorruptWater(state,observer:true),good,store=>Require(store.Load(1).RecoveredBackup,"Observer corruption did not recover."));
});
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
{checks,passed=checks-failures,failures,scope="Synthetic water-reference validation, file recovery and deterministic continuation; no personal saves or UI coverage",results},SaveStore.JsonOptions));
Console.WriteLine($"CHECKS {checks} PASSED {checks-failures} FAILURES {failures}");
return failures==0?0:1;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

string output=Path.GetFullPath(args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"yukkuri-null-identifiers-"+Guid.NewGuid().ToString("N")));
if(Directory.Exists(output))throw new IOException("Choose a new output directory; retain previous evidence.");
Directory.CreateDirectory(output);
var results=new List<object>();int checks=0,failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
string Json(WorldState s)=>JsonSerializer.Serialize(s,SaveStore.JsonOptions);
WorldState Read(string json)=>JsonSerializer.Deserialize<WorldState>(json,SaveStore.JsonOptions)!;
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
InvalidDataException Reject(Action action)
{
    try{action();}catch(InvalidDataException e){return e;}
    throw new Exception("Expected controlled InvalidDataException.");
}
void Check(string name,Action<string> test)
{
    string folder=Path.Combine(output,$"{++checks:00}-{name}");Directory.CreateDirectory(folder);
    try{test(folder);results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}
    catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e);}
}
WorldState Fixture(bool reversed)
{
    var s=WorldSimulation.CreateCourtyardEconomy().State;s.WorldSeconds=10;
    s.Creatures[0].Family.CaregiverIds.Add(s.Creatures[1].Id);
    if(reversed)s.Creatures.Reverse();
    SaveStore.Validate(s);return s;
}
string Corrupt(WorldState state,string kind,string? invalidId=null)
{
    var node=JsonNode.Parse(Json(state))!;
    if(kind=="caregiver")
        node["Creatures"]!.AsArray().Single(c=>c!["Family"]!["CaregiverIds"]!.AsArray().Count>0)!["Family"]!["CaregiverIds"]![0]=invalidId;
    else
        node["FamilyRelationshipEvents"]![0]!["ReactedBy"]=new JsonArray { invalidId==null?null:JsonValue.Create(invalidId) };
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
        File.WriteAllText(Path.Combine(folder,"hashes.json"),JsonSerializer.Serialize(new{mainBefore,mainAfter,backupBefore,backupAfter},SaveStore.JsonOptions));
        Require(mainBefore==mainAfter&&backupBefore==backupAfter,"Primary or backup bytes changed.");
    }
}
foreach(string kind in new[]{"caregiver","reacted-by"})foreach(bool reversed in new[]{false,true})
{
    string tag=kind+(reversed?"-reversed":"-original");
    Check(tag+"-valid-main-round-trip",folder=>
    {
        var s=Fixture(reversed);string good=Json(s);
        for(int n=0;n<3;n++)SaveStore.Validate(s);
        Require(Json(s)==good,"Validation mutated legitimate state.");
        WithFiles(folder,good,Corrupt(s,kind),store=>{var r=store.Load(1);Require(!r.RecoveredBackup&&Json(r.State)==good,"Valid main did not win exactly.");});
    });
    Check(tag+"-null-rejected-without-mutation",folder=>
    {
        var bad=Read(Corrupt(Fixture(reversed),kind));string before=Json(bad);
        Reject(()=>SaveStore.Validate(bad));Require(Json(bad)==before,"Validation mutated malformed state.");
    });
    Check(tag+"-good-backup-recovered",folder=>
    {
        var s=Fixture(reversed);string good=Json(s);
        WithFiles(folder,Corrupt(s,kind),good,store=>{var r=store.Load(1);Require(r.RecoveredBackup&&Json(r.State)==good,"Valid backup not recovered exactly.");});
    });
    Check(tag+"-bad-existing-main-protected",folder=>
    {
        var s=Fixture(reversed);WithFiles(folder,Corrupt(s,kind),Json(s),store=>Reject(()=>store.Save(s,1)));
    });
    Check(tag+"-bad-candidate-protected",folder=>
    {
        var s=Fixture(reversed);string good=Json(s);var bad=Read(Corrupt(s,kind));
        WithFiles(folder,good,good,store=>Reject(()=>store.Save(bad,1)));
    });
    Check(tag+"-both-bad-controlled-aggregate",folder=>
    {
        string bad=Corrupt(Fixture(reversed),kind);
        WithFiles(folder,bad,bad,store=>{var e=Reject(()=>store.Load(1));Require(e.InnerException is AggregateException a&&a.InnerExceptions.Count==2&&a.InnerExceptions.All(x=>x is InvalidDataException),"Both controlled errors not retained.");});
    });
    Check(tag+"-unknown-id-still-recovers",folder=>
    {
        var s=Fixture(reversed);WithFiles(folder,Corrupt(s,kind,"unknown-creature"),Json(s),store=>Require(store.Load(1).RecoveredBackup,"Unknown ID not rejected."));
    });
    Check(tag+"-valid-continuation-unchanged",folder=>
    {
        string good=Json(Fixture(reversed));var plain=new WorldSimulation(Read(good));var validated=new WorldSimulation(Read(good));
        for(int step=0;step<40;step++){SaveStore.Validate(validated.State);plain.AdvanceInteractionPresentation(.05);plain.Advance(.05);validated.AdvanceInteractionPresentation(.05);validated.Advance(.05);}
        SaveStore.Validate(plain.State);SaveStore.Validate(validated.State);Require(Json(plain.State)==Json(validated.State),"Validation changed time, RNG or subsequent state.");
    });
}
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{checks,passed=checks-failures,failures,scope="Synthetic null family identifier recovery; no historical personal saves or UI coverage",results},SaveStore.JsonOptions));
Console.WriteLine($"CHECKS {checks} PASSED {checks-failures} FAILURES {failures}");
return failures==0?0:1;

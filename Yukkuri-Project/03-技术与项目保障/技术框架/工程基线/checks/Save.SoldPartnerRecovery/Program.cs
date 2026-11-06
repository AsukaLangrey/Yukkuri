using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

string output=Path.GetFullPath(args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"yukkuri-sold-partner-"+Guid.NewGuid().ToString("N")));
if(Directory.Exists(output))throw new IOException("Use a new output directory to retain earlier evidence.");
Directory.CreateDirectory(output);
var results=new List<object>();int checks=0,failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
string Json(WorldState s)=>JsonSerializer.Serialize(s,SaveStore.JsonOptions);
WorldState Read(string json)=>JsonSerializer.Deserialize<WorldState>(json,SaveStore.JsonOptions)!;
string? Hash(string path)=>File.Exists(path)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))):null;
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
    var sim=WorldSimulation.CreateCourtyardEconomy();
    var s=sim.State;SaveStore.Validate(s);
    var sold=s.Creatures[0];var partner=s.Creatures[1];var child=s.Creatures[2];
    // Legitimate ancestry in addition to the real starting peer/event history.
    child.Family.MotherId=sold.Id;child.Family.FatherId=partner.Id;
    SaveStore.Validate(s);
    int money=s.Money;var quote=CourtyardEconomy.Quote(sold);
    Require(sim.CreatureSaleReason(sold.Id)==null,"Starter partner cannot be sold.");
    sim.SellCreatureToShop(sold.Id,quote.Price);
    Require(!s.Creatures.Contains(sold)&&s.CreatureSales.Single().Creature==sold&&s.Money==money+quote.Price,"Real sale failed.");
    Require(partner.Family.PartnerIds.Count==0&&sold.Family.PartnerIds.Count==0,"Sale left active partners.");
    Require(partner.Family.Peers.ContainsKey(sold.Id)&&sold.Family.Peers.ContainsKey(partner.Id),"Sale erased historical peers.");
    Require(child.Family.MotherId==sold.Id&&child.Family.FatherId==partner.Id,"Sale erased bloodline.");
    Require(s.FamilyRelationshipEvents.Single().EndedAt==s.WorldSeconds,"Sale did not end historical relationship.");
    SaveStore.Validate(s);string good=Json(s);var roundtrip=Read(good);SaveStore.Validate(roundtrip);
    Require(Json(roundtrip)==good,"Sale history failed round trip.");
    if(reversed)s.Creatures.Reverse();
    return s;
}
string Corrupt(WorldState s,string kind)
{
    var node=JsonNode.Parse(Json(s))!;
    string sold=s.CreatureSales.Single().Creature.Id;
    string owner=s.FamilyRelationshipEvents.Single().SecondId;
    var current=node["Creatures"]!.AsArray();
    var family=current.Single(c=>c!["Id"]!.GetValue<string>()==owner)!["Family"]!;
    string? id=kind switch {"sold"=>sold,"null"=>null,"empty"=>"","unknown"=>"missing-creature","self"=>owner,
        "asymmetric"=>s.Creatures.Single(c=>c.Id!=owner).Id,_=>throw new ArgumentException(kind)};
    family["PartnerIds"]=new JsonArray{ id==null?null:JsonValue.Create(id) };
    return node.ToJsonString();
}
void WithFiles(string folder,string primary,string? backup,Action<SaveStore> action)
{
    var store=new SaveStore(folder);string main=store.SlotPath(1),bak=main+".bak";
    File.WriteAllText(main,primary);if(backup!=null)File.WriteAllText(bak,backup);
    string? mainBefore=Hash(main),backupBefore=Hash(bak);
    var filesBefore=Directory.GetFiles(folder).Select(Path.GetFileName).Order().ToArray();
    try{action(store);}
    finally
    {
        string? mainAfter=Hash(main),backupAfter=Hash(bak);
        var filesAfter=Directory.GetFiles(folder).Select(Path.GetFileName).Order().ToArray();
        File.WriteAllText(Path.Combine(folder,"hashes.json"),JsonSerializer.Serialize(new{mainBefore,mainAfter,backupBefore,backupAfter,filesBefore,filesAfter},SaveStore.JsonOptions));
        Require(mainBefore==mainAfter&&backupBefore==backupAfter&&filesBefore.SequenceEqual(filesAfter),"Load/save altered original files or created repair artifacts.");
    }
}
Check("normal-current-pair-round-trip",folder=>
{
    var s=WorldSimulation.CreateCourtyardEconomy().State;string good=Json(s);SaveStore.Validate(s);SaveStore.Validate(Read(good));
    Require(Json(s)==good,"Validation mutated current pair.");
    var backup=Read(good);backup.Money++;
    WithFiles(folder,good,Json(backup),store=>{var r=store.Load(1);Require(!r.RecoveredBackup&&Json(r.State)==good,"Current primary changed.");});
});
foreach(bool reversed in new[]{false,true})
{
    string tag=reversed?"reversed":"original";
    Check(tag+"-real-sale-historical-round-trip",folder=>
    {
        var s=Fixture(reversed);string good=Json(s);for(int n=0;n<3;n++)SaveStore.Validate(s);
        Require(Json(s)==good,"Valid sale mutated.");
        WithFiles(folder,good,Corrupt(s,"sold"),store=>{var r=store.Load(1);Require(!r.RecoveredBackup&&Json(r.State)==good,"Valid main not preferred.");});
    });
    foreach(string kind in new[]{"sold","null","empty","unknown","self","asymmetric"})
    {
        string name=tag+"-"+kind;
        Check(name+"-rejected-unchanged",folder=>
        {
            var bad=Read(Corrupt(Fixture(reversed),kind));string before=Json(bad);
            try{Reject(()=>SaveStore.Validate(bad));}finally{Require(Json(bad)==before,"Invalid state mutated.");}
        });
        Check(name+"-backup-recovered",folder=>
        {
            var s=Fixture(reversed);string good=Json(s);
            WithFiles(folder,Corrupt(s,kind),good,store=>{var r=store.Load(1);Require(r.RecoveredBackup&&Json(r.State)==good,"Backup not recovered exactly.");});
        });
    }
    Check(tag+"-sold-no-backup-controlled",folder=>
    {
        WithFiles(folder,Corrupt(Fixture(reversed),"sold"),null,store=>
        {
            var e=Reject(()=>store.Load(1));
            Require(e.InnerException is AggregateException a&&a.InnerExceptions.Count==2&&a.InnerExceptions[0] is InvalidDataException&&a.InnerExceptions[1] is FileNotFoundException,"Missing-backup causes not preserved.");
        });
    });
    Check(tag+"-sold-both-bad-controlled",folder=>
    {
        string bad=Corrupt(Fixture(reversed),"sold");
        WithFiles(folder,bad,bad,store=>{var e=Reject(()=>store.Load(1));Require(e.InnerException is AggregateException a&&a.InnerExceptions.Count==2&&a.InnerExceptions.All(x=>x is InvalidDataException),"Both bad causes not preserved.");});
    });
    Check(tag+"-sold-existing-main-protected",folder=>
    {
        var s=Fixture(reversed);WithFiles(folder,Corrupt(s,"sold"),Json(s),store=>Reject(()=>store.Save(s,1)));
    });
    Check(tag+"-sold-candidate-protected",folder=>
    {
        var s=Fixture(reversed);WithFiles(folder,Json(s),Json(s),store=>Reject(()=>store.Save(Read(Corrupt(s,"sold")),1)));
    });
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{checks,failures,results},SaveStore.JsonOptions));
Console.WriteLine($"Checks: {checks}; failures: {failures}");
return failures==0?0:1;

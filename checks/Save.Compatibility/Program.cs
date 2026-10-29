using System.Security.Cryptography;
using System.Text.Json;
using Yukkuri.Simulation;

if(args.Length<2)throw new ArgumentException("Provide a new output directory followed by explicit historical save files.");
string output=Path.GetFullPath(args[0]);
if(Directory.Exists(output))throw new IOException("Preserve previous audit output; choose a new directory.");
Directory.CreateDirectory(output);
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
void Preserve(JsonElement before,JsonElement after,string path)
{
    if(before.ValueKind!=after.ValueKind)throw new InvalidDataException($"Value kind changed at {path}");
    switch(before.ValueKind)
    {
        case JsonValueKind.Object:
            foreach(var property in before.EnumerateObject())
            {
                if(!after.TryGetProperty(property.Name,out var value))throw new InvalidDataException($"Lost saved field {path}.{property.Name}");
                Preserve(property.Value,value,path+"."+property.Name);
            }
            break;
        case JsonValueKind.Array:
            if(before.GetArrayLength()!=after.GetArrayLength())throw new InvalidDataException($"Array length changed at {path}");
            int i=0;foreach(var element in before.EnumerateArray()){Preserve(element,after[i],path+"["+i+"]");i++;}
            break;
        case JsonValueKind.Number:
            bool equal=before.TryGetDecimal(out var oldNumber)&&after.TryGetDecimal(out var newNumber)
                ?oldNumber==newNumber:before.GetDouble()==after.GetDouble();
            if(!equal)throw new InvalidDataException($"Saved number changed at {path}");
            break;
        default:
            if(before.ToString()!=after.ToString())throw new InvalidDataException($"Saved value changed at {path}");
            break;
    }
}
var results=new List<object>();
int failures=0;
foreach(var sourceArgument in args.Skip(1))
{
    string source=Path.GetFullPath(sourceArgument);
    string originalHash=Hash(source);
    string caseDirectory=Path.Combine(output,$"case-{results.Count+1:00}");
    var store=new SaveStore(caseDirectory);
    File.Copy(source,store.SlotPath(1));
    try
    {
        var loaded=store.Load(1);
        if(loaded.RecoveredBackup)throw new InvalidDataException("Unexpected backup fallback in intact historical sample");
        store.Save(loaded.State,2);
        using var oldJson=JsonDocument.Parse(File.ReadAllText(source));
        using var roundTrip=JsonDocument.Parse(File.ReadAllText(store.SlotPath(2)));
        Preserve(oldJson.RootElement,roundTrip.RootElement,"world");
        int identities=loaded.State.Creatures.Count;
        // Continue only the copied scenario, explicitly resuming its clock for this check.
        loaded.State.Settings.Pause=false;loaded.State.Mode=PlayMode.Management;
        var simulation=new WorldSimulation(loaded.State);
        // Interactive intake/falling timers use the presentation entry point before
        // world updates, just like InteractionRoom. A world-only jump misses them.
        if(loaded.State.InteractionPlaytest)
            for(int step=0;step<1200;step++)
            {simulation.AdvanceInteractionPresentation(.1);simulation.Advance(.1);}
        else simulation.Advance(120);
        SaveStore.Validate(simulation.State);
        store.Save(simulation.State,3);
        store.Load(3);
        if(Hash(source)!=originalHash)throw new IOException("Original historical save changed");
        results.Add(new{source,source_sha256=originalHash,success=true,all_original_fields_preserved=true,identities,continued_simulated_seconds=120,
            continuation_mode=loaded.State.InteractionPlaytest?"presentation-then-world-1200x0.1s":"world-120s",original_untouched=true});
    }
    catch(Exception error)
    {
        failures++;
        results.Add(new{source,source_sha256=originalHash,success=false,error=error.ToString(),original_untouched=Hash(source)==originalHash});
    }
}
var report=new{checked_at=DateTimeOffset.Now,scope="Explicit historical save samples only. Read/write round trip preserves every existing field; copied world continues for 120 simulated seconds. Not real-time play or all-save compatibility.",samples=results.Count,failures,results};
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(new{samples=results.Count,failures,output}));
return failures==0?0:1;

using System.Text.Json;
using Yukkuri.Simulation;

if(args.Length==0)throw new ArgumentException("Supply the evidence JSON path.");
var scenarios=new List<object>();
int failures=0;
var cases=args.Length>1?new[]{args[1]}:new[]{"woodland","clean-supply","fatigue","blocked-cushion","illness","scarcity"};
var seeds=args.Length>2?new[]{ulong.Parse(args[2])}:new ulong[]{72341,592813,1597331,20261029};
void WriteEvidence()=>File.WriteAllText(args[0],JsonSerializer.Serialize(new{at=DateTimeOffset.UtcNow,scope="Deterministic one-hour unattended simulation traces and bounded scenario assertions, saved after each case; no game or native input",success=failures==0,failures,scenarios},new JsonSerializerOptions{WriteIndented=true}));
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
foreach(string scenario in cases)
foreach(ulong seed in seeds)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(seed);
    if(scenario is not ("woodland" or "scarcity"))
    {
        sim.State.RoomObjects.RemoveAll(o=>NaturalResources.IsResource(o.Kind));
        foreach(var c in sim.State.Creatures)
        {
            bool limited=scenario is "injured-ground" or "injured-cushion" or "wrapped";
            double nearby=.55*(c.IsYoung?.73:c.IsElder?1.03:1);
            sim.State.RoomObjects.Add(new(){Id=c.Id+"-food",Kind=RoomObjectKind.Meal,X=c.X+(limited?nearby:.7),Z=c.Z+(limited?0:.7),Portions=100});
            sim.State.RoomObjects.Add(new(){Id=c.Id+"-water",Kind=RoomObjectKind.Water,X=c.X-(limited?nearby:.7),Z=c.Z+(limited?0:.7),Portions=100});
        }
    }
    if(scenario is "injured-ground" or "injured-cushion" or "wrapped")
    {
        sim.State.RoomObjects.RemoveAll(o=>o.Kind==RoomObjectKind.Cushion);
        foreach(var c in sim.State.Creatures)
        {
            c.Energy=10;c.Interaction.FootIntegrity=20;
            if(scenario=="wrapped")c.Interaction.Restraint=RestraintKind.Wrap;
            if(scenario=="injured-cushion")sim.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Cushion,X=c.X,Z=c.Z});
        }
    }
    if(scenario is "fatigue" or "blocked-cushion")foreach(var c in sim.State.Creatures)c.Energy=10;
    if(scenario=="blocked-cushion")
    {
        sim.State.RoomObjects.RemoveAll(o=>o.Kind==RoomObjectKind.Cushion);
        sim.State.RoomObjects.Add(new(){Id="inaccessible-bed",Kind=RoomObjectKind.Cushion,X=8.8,Z=0});
    }
    if(scenario=="illness")foreach(var c in sim.State.Creatures)
        c.Interaction.Digestion=new(){Exposures=1,Episodes=1,Severity=45,SymptomsRemaining=190};
    if(scenario=="scarcity")foreach(var o in sim.State.RoomObjects.Where(o=>NaturalResources.IsFood(o.Kind)))o.Portions=1;
    var measures=sim.State.Creatures.ToDictionary(c=>c.Id,c=>new Measure{Id=c.Id,Name=c.Name,LastX=c.X,LastZ=c.Z});
    var snapshots=new List<object>();
    for(int step=1;step<=36000;step++)
    {
        sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);
        if(step%10!=0)continue;
        foreach(var c in sim.State.Creatures)
        {
            var m=measures[c.Id];double distance=Math.Sqrt(Math.Pow(c.X-m.LastX,2)+Math.Pow(c.Z-m.LastZ,2));
            m.Distance+=distance;m.LastX=c.X;m.LastZ=c.Z;
            m.MinimumFullness=Math.Min(m.MinimumFullness,c.Fullness);m.MinimumWater=Math.Min(m.MinimumWater,c.Interaction.Hydration);
            m.MinimumEnergy=Math.Min(m.MinimumEnergy,c.Energy);m.MaximumEnergy=Math.Max(m.MaximumEnergy,c.Energy);
            if(c.Interaction.Resting){m.RestSeconds++;if(!m.WasResting)m.RestStarts++;}m.WasResting=c.Interaction.Resting;
            if(c.Activity=="回到软垫旁")m.SeekingBedSeconds++;else m.SeekingBedSeconds=0;
            m.LongestBedSearch=Math.Max(m.LongestBedSearch,m.SeekingBedSeconds);
            if(c.Interaction.Digestion.Symptomatic)m.SickSeconds++;
            m.Exposures=c.Interaction.Digestion.Exposures;m.Episodes=c.Interaction.Digestion.Episodes;
            m.FinalHealth=c.Health;m.FinalEnergy=c.Energy;m.FinalFullness=c.Fullness;m.FinalWater=c.Interaction.Hydration;
            // A real water attempt may advance within water, but only a valid
            // full-body landing may release it to ordinary dry-ground movement.
            if(c.Interaction.Water.EventId!=null&&!WaterEnvironment.IsInWater(sim.State,c)&&
                !c.Interaction.Carried&&!c.Interaction.Falling&&c.Interaction.Height<=.1&&
                !WoodlandLayout.GroundFits(sim.State,c.RegionId,c.X,c.Z,WorldSimulation.InteractionFootprint(c)))
                m.InvalidDryBankSeconds++;
        }
        if(step%600==0)
        {
            SaveStore.Validate(sim.State);
            snapshots.Add(new{time=sim.State.WorldSeconds,creatures=sim.State.Creatures.Select(c=>new{c.Name,c.X,c.Z,c.Fullness,c.Energy,c.Health,c.Activity,
                c.Interaction.Hydration,c.Interaction.ResourceTargetId,c.Interaction.Resting,c.Interaction.Digestion.SymptomsRemaining}).ToArray()});
        }
    }
    var errors=new List<string>();
    foreach(var m in measures.Values)
    {
        if(scenario!="scarcity"&&m.MinimumEnergy<1)errors.Add(m.Name+": exhausted despite reachable rest");
        if(m.LongestBedSearch>60)errors.Add(m.Name+": cushion search did not fall back within a minute");
        if(m.RestStarts>40)errors.Add(m.Name+": rest oscillated instead of finishing sustained sessions");
        if(m.InvalidDryBankSeconds>0)errors.Add(m.Name+": water exit left body straddling an impassable dry bank");
        if(scenario is "clean-supply" or "fatigue" or "blocked-cushion" or "illness" or "injured-ground" or "injured-cushion" or "wrapped")
        {
            if(m.MinimumFullness<12||m.MinimumWater<12)errors.Add(m.Name+": available clean supply did not sustain basic needs");
            if(scenario!="illness"&&m.Episodes!=0)errors.Add(m.Name+": clean supplies invented illness");
        }
        if(scenario is "injured-ground" or "injured-cushion" or "wrapped")
        {
            if(m.Distance>.00001)errors.Add(m.Name+": limited body moved to fake food access");
            if(m.MaximumEnergy<22||m.RestSeconds<10)errors.Add(m.Name+": limited body could not rest where supported");
        }
    }
    if(scenario=="illness"&&sim.State.Creatures.Any(c=>c.Interaction.Digestion.Symptomatic))errors.Add("initial illness did not resolve within the simulated hour");
    if(scenario=="scarcity"&&!sim.State.Creatures.Any(c=>c.Fullness<5))errors.Add("scarcity was silently replaced with infinite food");
    failures+=errors.Count;
    scenarios.Add(new{scenario,seed,seconds=sim.State.WorldSeconds,passed=errors.Count==0,errors,measures=measures.Values,snapshots});
    WriteEvidence();
    Console.WriteLine($"{scenario}/{seed}: "+string.Join("; ",measures.Values.Select(m=>$"{m.Name} food={m.FinalFullness:F1} water={m.FinalWater:F1} energy={m.FinalEnergy:F1} restStarts={m.RestStarts} maxBedSearch={m.LongestBedSearch:F0}s sick={m.SickSeconds:F0}s")));
    foreach(var error in errors)Console.WriteLine("FAIL "+error);
}
return failures==0?0:1;

sealed class Measure
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public double LastX {get;set;}
    public double LastZ {get;set;}
    public double Distance {get;set;}
    public double MinimumFullness {get;set;}=100;
    public double MinimumWater {get;set;}=100;
    public double MinimumEnergy {get;set;}=100;
    public double MaximumEnergy {get;set;}
    public double RestSeconds {get;set;}
    public int RestStarts {get;set;}
    public bool WasResting {get;set;}
    public double SeekingBedSeconds {get;set;}
    public double LongestBedSearch {get;set;}
    public double SickSeconds {get;set;}
    public int Exposures {get;set;}
    public int Episodes {get;set;}
    public double FinalHealth {get;set;}
    public double FinalEnergy {get;set;}
    public double FinalFullness {get;set;}
    public double FinalWater {get;set;}
    public double InvalidDryBankSeconds {get;set;}
}

using System.Diagnostics;
using System.Text.Json;
using Yukkuri.Simulation;

if(args.Length!=1)throw new ArgumentException("Supply a new output directory for isolated simulation exploration.");
string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
string Json(WorldState state)=>JsonSerializer.Serialize(state,SaveStore.JsonOptions);
void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
var timer=Stopwatch.StartNew();int operations=0,roundTrips=0,deadSeen=0;
for(int seed=1;seed<=64;seed++)
{
    var random=new Random(seed);var sim=WorldSimulation.CreateMilestone();sim.State.RandomState=(ulong)seed;
    var store=new SaveStore(Path.Combine(output,$"seed-{seed:000}"));var trace=new List<string>();
    var dead=new HashSet<string>();
    try
    {
        for(int step=0;step<600;step++)
        {
            int action=random.Next(22);string label=$"step={step}, action={action}, time={sim.State.WorldSeconds:F2}, selected={sim.State.SelectedId}";
            trace.Add(label);
            switch(action)
            {
                case 0:case 1:case 2:case 3:
                    double before=sim.State.WorldSeconds;bool frozen=sim.State.Settings.Pause||sim.State.Mode==PlayMode.CloseUp;
                    sim.Advance(random.NextDouble()*30);
                    if(frozen)Require(sim.State.WorldSeconds==before,"World clock advanced while paused or in close-up.");
                    break;
                case 4:sim.Travel(random.Next(2)==0?"home":"wild");break;
                case 5:
                    var residents=sim.State.Creatures.Where(c=>c.RegionId==sim.State.ActiveRegionId).ToArray();
                    if(residents.Length>0)sim.Select(residents[random.Next(residents.Length)].Id);break;
                case 6:if(sim.Selected!=null)sim.Feed(sim.Selected.Id);break;
                case 7:sim.Observe();break;
                case 8:case 9:
                    if(sim.Selected!=null)sim.Contact(sim.Selected.Id,new((ContactAction)random.Next(7),(ContactArea)random.Next(3),(ContactTool)random.Next(3),random.NextDouble(),.05+random.NextDouble()*5.95));
                    break;
                case 10:sim.ProtectSelected();break;
                case 11:sim.FocusHelp();break;
                case 12:sim.InviteToSanctuary();break;
                case 13:sim.AdmitSelected();break;
                case 14:sim.ClaimCareContract();break;
                case 15:if(random.Next(2)==0)sim.BuyFood();else sim.BuyMedicine();break;
                case 16:sim.Refill();break;
                case 17:sim.EnterCloseUp();break;
                case 18:sim.ExitCloseUp();break;
                case 19:sim.State.Settings.Pause=!sim.State.Settings.Pause;break;
                case 20:
                    switch(random.Next(5))
                    {
                        case 0:sim.State.Settings.Invasions=!sim.State.Settings.Invasions;break;
                        case 1:sim.State.Settings.Accidents=!sim.State.Settings.Accidents;break;
                        case 2:sim.State.Settings.LethalInjuries=!sim.State.Settings.LethalInjuries;break;
                        case 3:sim.State.Settings.ProtectCareTools=!sim.State.Settings.ProtectCareTools;break;
                        case 4:sim.State.UnlimitedResources=!sim.State.UnlimitedResources;break;
                    }
                    break;
                case 21:if(sim.Selected!=null)sim.RegisterImpact(sim.Selected.Id,2+random.NextDouble()*10);break;
            }
            SaveStore.Validate(sim.State);
            foreach(var c in sim.State.Creatures)
            {
                Require(!dead.Contains(c.Id)||c.Condition==PhysicalCondition.Dead,"A permanently dead individual became living without a supported transfer system.");
                if(c.Condition==PhysicalCondition.Dead&&dead.Add(c.Id))deadSeen++;
            }
            operations++;
            if(step%50==49)
            {
                store.Save(sim.State,1);var loaded=store.Load(1);
                Require(!loaded.RecoveredBackup&&Json(loaded.State)==Json(sim.State),"Save round trip changed world state or fell back unexpectedly.");
                var branch=new WorldSimulation(loaded.State,WorldSimulation.MilestoneConfig());
                sim.Advance(1.75);branch.Advance(1.75);
                Require(Json(branch.State)==Json(sim.State),"Save reload changed subsequent deterministic world evolution.");
                sim=branch;roundTrips++;
            }
        }
        Console.WriteLine($"EXPLORE_PASS seed={seed} operations=600 world={sim.State.WorldSeconds:F2}");
    }
    catch(Exception e)
    {
        File.WriteAllLines(Path.Combine(output,$"failed-seed-{seed}.trace.txt"),trace);
        File.WriteAllText(Path.Combine(output,$"failed-seed-{seed}.state.json"),Json(sim.State));
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{success=false,seed,operations,roundTrips,error=e.ToString(),scope="Seeded simulation API exploration, not GUI play or natural elapsed time"}));
        throw;
    }
}
File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{success=true,seeds=64,operations,roundTrips,deadSeen,wallSeconds=timer.Elapsed.TotalSeconds,scope="Seeded simulation API actions, state validity, save round trips, deterministic continuation and permanent-death invariants. Synthetic time; not GUI, native stability or user acceptance."},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"EXPLORATION_COMPLETE operations={operations} roundTrips={roundTrips} deadSeen={deadSeen}");

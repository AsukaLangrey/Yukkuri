using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Yukkuri.Simulation;

public partial class Main
{
    private async void StartMilestoneStress()
    {
        try
        {
            await Wait(.5);StartNewMilestone();
            ulong started=Time.GetTicksMsec();int transitions=0;
            for(int cycle=0;cycle<60;cycle++)
            {
                if(Sim.State.Mode==PlayMode.CloseUp)ExitCloseUp();
                if(cycle%8==0)StartNewMilestone();
                Travel(cycle%2==0?"wild":"home");transitions++;
                Sim.EnterCloseUp();await Wait(.2);
                foreach(var expression in Enum.GetValues<Yukkuri.Simulation.Expression>())
                {Sim.Selected!.Expression=expression;await Wait(.025);}
                Sim.Contact(Sim.Selected!.Id,new(ContactAction.Stroke));
                await Wait(.2);ExitCloseUp();Save(3,false);
                if(cycle%3==0){Load(3);transitions++;}
                if(cycle%5==0)
                {
                    GD.Print($"STRESS_GC: cycle={cycle}; managed={GC.GetTotalMemory(false)}");
                    GC.Collect();
                }
                await Wait(.8);
                Assert(Actors.Count==Sim.State.Creatures.Count(c=>c.RegionId==Sim.State.ActiveRegionId),"stress actor membership "+cycle);
                SaveStore.Validate(Sim.State);
            }
            await Screenshot("m01-stress-final");
            var result=new{success=true,kind="repeated scene replacement, every expression, physics freeze transitions, save/load and forced managed collection",cycles=60,transitions,wallSeconds=(Time.GetTicksMsec()-started)/1000.0,renderer=RenderingServer.GetCurrentRenderingMethod(),shadows=!_noShadows};
            File.WriteAllText(Path.Combine(EvidenceDirectory,"m01-integration-result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("M01_INTEGRATION_COMPLETE");QuitGameCleanly(0);
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGameCleanly(6);}
    }
}

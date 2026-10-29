using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class Main
{
    private async Task ChooseRoster(string name)
    {
        var button=_residents.GetChildren().OfType<Button>().Single(b=>b.Text==name);
        await ClickVisibleButton(button);
    }
    private async Task HeldAction(string action,double duration)
    {
        await ClickButton(action);await Wait(.6);
        var actor=Actors[Sim.Selected!.Id];
        var point=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(0,.2f,.26f));
        Mouse(point,true);await Wait(duration);Mouse(point,false);await Wait(.2);
        ExitCloseUp();UpdateUi();await Wait(.3);
    }
    private async void StartMilestoneSoak()
    {
        try
        {
            ulong started=Time.GetTicksMsec();await Wait(.5);await ClickButton("start");
            foreach(var oil in Sim.State.Creatures.Where(c=>c.RegionId=="home").ToArray())
            {await ChooseRoster(oil.Name);await ClickButton("feed");}
            await ChooseRoster("豆豆");await HeldAction("stroke",1);
            for(int i=0;i<3;i++)await HeldAction("train",2);
            await ClickButton("commission");await ClickButton("wild");
            bool wildProtected=false,invited=false,naturalDefense=false;
            int cycles=0,saves=0;double lastMaintenance=-100,lastSnapshot=-100;
            while(Time.GetTicksMsec()-started<(ulong)_milestoneSoakSeconds*1000)
            {
                Assert(!Sim.State.Settings.Pause&&Sim.State.Settings.Speed==1,"soak normal world clock "+cycles);
                if(Sim.State.HelpTargetId!=null)
                {
                    await ClickButton("help");await Wait(.3);
                    var helped=Sim.Selected!;bool wild=helped.RegionId=="wild";
                    int knownProtection=Sim.State.Incidents.Count(e=>e.Kind==IncidentKind.Protection);
                    await ClickButton("protect");
                    if(Sim.State.Incidents.Count(e=>e.Kind==IncidentKind.Protection)>knownProtection)
                    {
                        if(wild)
                        {
                            wildProtected=true;await Screenshot("m01-natural-wild-protection");
                            await ClickButton("rescue");await Wait(.3);
                            if(helped.RegionId=="home")
                            {
                                await ClickButton("home");await ChooseRoster(helped.Name);await ClickButton("admit");invited=true;
                                await Screenshot("m01-natural-admission");
                            }
                        }
                        else {naturalDefense=true;await Screenshot("m01-natural-defense");}
                    }
                    if(helped.RegionId==Sim.State.ActiveRegionId&&helped.Health<75&&helped.Condition!=PhysicalCondition.Dead)
                        await HeldAction("treat",1);
                }
                // Before the first wild encounter, remain in the wild and observe. No event timer is changed.
                if(!wildProtected&&Sim.State.WorldSeconds<165&&Sim.State.ActiveRegionId=="wild")
                    await ClickButton("observe");
                else
                {
                    if(Sim.State.ActiveRegionId!="home")await ClickButton("home");
                    if(Sim.State.WorldSeconds-lastMaintenance>=35)
                    {
                        lastMaintenance=Sim.State.WorldSeconds;
                        if(Sim.State.Food<15&&Sim.State.Money>=10)await ClickButton("supplies");
                        if(Sim.State.Medicine<2&&Sim.State.Money>=15)await ClickButton("medicine");
                        foreach(var oil in Sim.State.Creatures.Where(c=>c.RegionId=="home"&&c.Condition==PhysicalCondition.Active&&!c.IsIntruder).OrderBy(c=>c.Fullness).Take(3).ToArray())
                        {await ChooseRoster(oil.Name);await ClickButton("feed");}
                        await ClickButton("refill");await ClickButton("commission");
                        await ClickButton("save");saves++;
                    }
                }
                SaveStore.Validate(Sim.State);
                if(Sim.State.WorldSeconds-lastSnapshot>=60)
                {
                    lastSnapshot=Sim.State.WorldSeconds;await Screenshot("m01-natural-"+((int)Sim.State.WorldSeconds).ToString("0000"));
                    GD.Print($"SOAK_PROGRESS: world={Sim.State.WorldSeconds:F1}; money={Sim.State.Money}; food={Sim.State.Food}; help={Sim.State.HelpTargetId}; phase={Sim.State.Threat.Phase}");
                }
                await Wait(5);cycles++;
            }
            Assert(wildProtected&&invited,"natural wild conflict can be protected and rescued through UI");
            Assert(naturalDefense&&Sim.State.Progress.Defenses>0,"default encounter schedule leads to real sanctuary defense");
            Assert(Sim.State.ScenarioComplete,"natural normal-speed session completes all M01 objectives");
            var identities=Sim.State.Creatures.Select(c=>c.Id).ToArray();
            Save(2,false);Assert(Load(2),"long-session save loads");SaveStore.Validate(Sim.State);
            Assert(identities.SequenceEqual(Sim.State.Creatures.Select(c=>c.Id)),"long-session load preserves every identity");
            await Screenshot("m01-natural-final");
            var result=new{success=true,kind="Real normal-speed graphical play, actual UI input; no encounter/clock/resource mutations",requestedWallSeconds=_milestoneSoakSeconds,wallSeconds=(Time.GetTicksMsec()-started)/1000.0,worldSeconds=Sim.State.WorldSeconds,wildProtected,invited,naturalDefense,cycles,saves,money=Sim.State.Money,food=Sim.State.Food,defenses=Sim.State.Progress.Defenses,contracts=Sim.State.Progress.CareContracts,homeAlive=Sim.State.Creatures.Count(c=>c.RegionId=="home"&&c.Condition!=PhysicalCondition.Dead)};
            File.WriteAllText(Path.Combine(EvidenceDirectory,"m01-integration-result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("M01_INTEGRATION_COMPLETE");QuitGameCleanly(0);
        }
        catch(Exception e){GD.PushError(e.ToString());try{await Screenshot("m01-natural-failure");}catch{}QuitGameCleanly(7);}
    }
}

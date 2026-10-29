using Godot;
using System;
using System.IO;
using System.Text.Json;

public partial class Main
{
    private async void StartMilestoneAutosave()
    {
        try
        {
            ulong started=Time.GetTicksMsec();
            await Wait(.5);await ClickButton("start");
            await Wait(62);
            string path=_saves.SlotPath(0);
            Assert(File.Exists(path),"normal autosave timer creates slot zero after real elapsed play");
            var first=_saves.Load(0).State;
            await ClickButton("feed");await Wait(62);
            Assert(File.Exists(path+".bak"),"second natural autosave preserves the earlier snapshot as backup");
            var second=_saves.Load(0).State;
            Assert(second.WorldSeconds>first.WorldSeconds+50,"natural autosaves capture successive world progress");
            await LogicalShortcut(Key.Escape);await Screenshot("m01-autosave-menu");
            await ClickButton("continue-auto");
            Assert(!_welcome.Visible&&Sim.State.WorldSeconds>=second.WorldSeconds&&Sim.State.WorldSeconds<second.WorldSeconds+2,"player menu restores the latest automatic save");
            // Deliberately corrupt only this run's isolated main file; keep its actual natural backup intact.
            File.WriteAllText(path,"{ deliberately broken autosave");
            await LogicalShortcut(Key.Escape);await ClickButton("continue-auto");
            Assert(_recovered&&Sim.State.WorldSeconds<second.WorldSeconds-40,"same menu route recovers earlier backup after a corrupted main file");
            Assert(File.ReadAllText(path)=="{ deliberately broken autosave","recovery keeps the damaged original for inspection");
            Save(2,false);
            Assert(_saves.Load(2).State.Creatures.Count==Sim.State.Creatures.Count,"recovered progress can be preserved in another manual slot");
            await Screenshot("m01-autosave-recovered");
            File.WriteAllText(Path.Combine(EvidenceDirectory,"m01-integration-result.json"),JsonSerializer.Serialize(new
            {success=true,scope="Two real 60-second autosave intervals, actual menu loading, and isolated deliberate corruption to verify backup recovery",wallSeconds=(Time.GetTicksMsec()-started)/1000.0,firstWorldSeconds=first.WorldSeconds,secondWorldSeconds=second.WorldSeconds,recovered=_recovered}));
            GD.Print("M01_INTEGRATION_COMPLETE");QuitGameCleanly(0);
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGameCleanly(10);}
    }
}

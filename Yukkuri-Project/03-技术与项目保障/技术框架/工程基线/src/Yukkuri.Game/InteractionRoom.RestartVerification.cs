using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Exercises real settings buttons with synthetic data only. Never use ordinary saves.
    private async void StartRestartVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,"--restart-check-output");
        if(flag<0)return;
        try
        {
            int profile=Array.IndexOf(args,"--interaction-test-profile");
            if(flag+1>=args.Length||profile<0||profile+1>=args.Length)
                throw new ArgumentException("Restart verification requires an output and an isolated test profile.");
            if(Enumerable.Range(0,4).Any(slot=>File.Exists(_saves.SlotPath(slot))||
                File.Exists(_saves.SlotPath(slot)+".bak")||File.Exists(_saves.SlotPath(slot)+".writing")))
                throw new InvalidOperationException("Restart verification requires a fresh test profile; existing files are preserved.");
            // Keep fixtures deterministic while invoking actual scene controls and persistence.
            SetProcess(false);_autoClock=-10000;SetRoomMuted(true);
            var results=new List<object>();int failures=0;
            void Require(bool ok,string why){if(!ok)throw new Exception(why);}
            void Check(string name,Action action)
            {
                try{action();results.Add(new{name,passed=true});GD.Print("PASS "+name);}
                catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.Message});GD.Print("FAIL "+name+": "+e.Message);}
            }
            string Json(WorldState state)=>JsonSerializer.Serialize(state,SaveStore.JsonOptions);
            void Click(string text)
            {
                var button=_modalContents.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==text);
                Require(!button.Disabled,"requested button was disabled: "+text);button.EmitSignal(BaseButton.SignalName.Pressed);
            }
            void OpenRestart(){ShowSettings();Click("重新开一间小院");}
            bool StartingPair()
            {
                var a=Sim.State.Creatures[0];var b=Sim.State.Creatures[1];
                return a.Family.PartnerIds.Contains(b.Id)&&b.Family.PartnerIds.Contains(a.Id)&&
                    Sim.State.FamilyRelationshipEvents.Any(e=>e.Id=="economy-starting-pair");
            }
            Check("first launch enables economy progression",()=>Require(Sim.State.EconomyProgression,"initial economy disabled"));
            Check("first launch retains starting partners",()=>Require(StartingPair(),"initial pair missing"));
            Check("first launch locks progression tools",()=>Require(Sim.ShopUnlockReason("food-station")!=null,"initial tools unlocked"));

            // A manual slot must survive both cancellation and confirmed restarts byte-for-byte.
            Sim.State.Money=321;Sim.State.Creatures[0].Name="restart-manual-fixture";
            Require(SaveRoom(2,false),"could not create isolated manual fixture");
            byte[] manual=File.ReadAllBytes(_saves.SlotPath(2));
            Sim.State.Money=654;Sim.State.Creatures[0].Name="restart-live-fixture";
            foreach(bool pause in new[]{false,true})
            {
                Sim.State.Settings.Pause=pause;var original=Sim;string before=Json(Sim.State);
                OpenRestart();Click("继续原来的相处");
                Check("cancel preserves original world and pause="+pause,()=>Require(ReferenceEquals(Sim,original)&&Json(Sim.State)==before,"cancel replaced or changed world"));
            }
            Check("cancel creates no automatic save",()=>Require(!File.Exists(_saves.SlotPath(0)),"cancel wrote automatic save"));
            Check("cancel preserves manual slot",()=>Require(File.ReadAllBytes(_saves.SlotPath(2)).SequenceEqual(manual),"cancel changed manual slot"));

            // Real sale records provide legal nonzero progression, not fabricated ledger values.
            Sim.State.Settings.Pause=false;var sold=Sim.State.Creatures.Last();
            Sim.SellCreatureToShop(sold.Id,CourtyardEconomy.Quote(sold).Price);BuildActors();
            Require(Sim.State.CreatureSales.Count>0&&Sim.State.CreatureSalesRevenue>0,"sale fixture did not create progression");
            var old=Sim;string savedExpected=Json(old.State);
            OpenRestart();Click("保存并重新开始");
            Check("restart saves previous world before replacement",()=>Require(Json(_saves.Load(0).State)==savedExpected,"automatic save does not preserve prior state/pause"));
            Check("restart replaces the world and clears old sale progress",()=>Require(!ReferenceEquals(Sim,old)&&Sim.State.CreatureSales.Count==0&&Sim.State.CreatureSalesRevenue==0,"restart retained old world/progress"));
            Check("restart enables economy progression",()=>Require(Sim.State.EconomyProgression,"restart disabled economy"));
            Check("restart restores initial partners",()=>Require(Sim.State.Creatures.Count==3&&StartingPair(),"restart lost starting pair"));
            Check("restart locks progression tools",()=>Require(Sim.ShopUnlockReason("food-station")!=null,"restart bypassed unlock gates"));
            Check("restart restores starting supplies",()=>Require(Sim.State.Money==240&&Sim.State.Food==50&&Sim.State.StationWaterPortions==20&&Sim.State.Creatures.Count==3,"restart supplies changed"));
            Check("restart leaves manual slots unchanged",()=>Require(File.ReadAllBytes(_saves.SlotPath(2)).SequenceEqual(manual),"restart overwrote manual slot"));
            Check("restarted world is save-valid",()=>SaveStore.Validate(Sim.State));

            // A validation failure exercises the actual SaveRoom error path before reset.
            var beforeFailed=Sim;Sim.State.Money=-1;byte[] autoBefore=File.ReadAllBytes(_saves.SlotPath(0));
            OpenRestart();Click("保存并重新开始");
            Check("save failure prevents restart",()=>Require(ReferenceEquals(Sim,beforeFailed)&&Sim.State.Money==-1&&_modalOpen,"failed save reset world or dismissed error"));
            Check("save failure preserves prior automatic and manual files",()=>Require(File.ReadAllBytes(_saves.SlotPath(0)).SequenceEqual(autoBefore)&&File.ReadAllBytes(_saves.SlotPath(2)).SequenceEqual(manual),"failed save changed existing files"));
            Sim.State.Money=240;CloseModal();
            OpenRestart();Click("保存并重新开始");
            Check("repeated restart remains a canonical economy start",()=>Require(Sim.State.EconomyProgression&&StartingPair()&&Sim.ShopUnlockReason("food-station")!=null&&Sim.State.Money==240&&Sim.State.Food==50&&Sim.State.StationWaterPortions==20&&Sim.State.Creatures.Count==3&&Sim.State.CreatureSales.Count==0&&Sim.State.CreatureSalesRevenue==0,"second restart used legacy factory"));
            Check("repeated restart preserves manual fixture",()=>Require(File.ReadAllBytes(_saves.SlotPath(2)).SequenceEqual(manual),"second restart changed manual slot"));
            string output=Path.GetFullPath(args[flag+1]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,JsonSerializer.Serialize(new{success=failures==0,failures,checks=results.Count,renderer=DisplayServer.GetName(),scope="Real scene button signals and isolated synthetic saves; not native input or visual acceptance",results},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"RESTART_CHECKS: {results.Count-failures}/{results.Count}; failures={failures}");
            await RenderFrames(3); // Flush controls/actors queued for disposal by restarts.
            // Let the audio thread consume stop/detach before test-driven engine shutdown.
            _sound.Stop();_sound.Stream=null;_roomMusic.Stop();_roomMusic.Stream=null;
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
            GetTree().Quit(failures==0?0:1);
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(2);}
    }
}

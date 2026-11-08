using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private void StartHousingVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,"--housing-check-output");
        if(flag>=0&&flag+1<args.Length)_=VerifyHousing(Path.GetFullPath(args[flag+1]));
    }
    private async Task VerifyHousing(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);_autoClock=-10000;_muted=true;Sim.State.Settings.Pause=true;
            await RenderFrames(12);
            var home=Sim.State.Shelters.Single();
            if(home.Capacity!=5||Sim.State.Money!=240||!_shelterActors.ContainsKey(home.Id))throw new Exception("default house actor or free starting inventory missing");
            async Task Capture(string name)
            {
                RefreshRoomUi();RefreshDayNightLighting();await RenderFrames(8);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var picture=GetViewport().GetTexture().GetImage();
                if(picture.SavePng(Path.Combine(directory,name+".png"))!=Error.Ok)throw new Exception("render save failed");
            }
            SelectShelter(home.Id);await Capture("default-house-day");
            if(!_shelterDescription.Text.Contains("0/5")||!_shelterDescription.Text.Contains("极度拥挤"))throw new Exception("house comfort UI missing");
            Sim.State.AutonomousBreeding=false;Sim.State.WorldSeconds=360;
            foreach(var c in Sim.State.Creatures){
                c.Fullness=c.Interaction.Hydration=100;c.Energy=80;c.Fear=c.Pain=0;
                c.Family.NextCareAt=c.Family.NextSocialAt=c.Family.NextBreedingDecisionAt=10000;
            }
            Sim.State.Settings.Pause=false;
            for(int n=0;n<1200;n++){Sim.AdvanceInteractionPresentation(.05);Sim.Advance(.05);}
            Sim.State.Settings.Pause=true;
            await Capture("three-residents-night");
            if(Sim.ShelterStatus(home.Id)!.OccupantIds.Length!=3||Sim.State.Creatures.Count(c=>c.Interaction.Resting)!=3||
                !_shelterDescription.Text.Contains("3/5")||!_shelterDescription.Text.Contains("适用"))
                throw new Exception("night entry/sleep/occupancy UI failed: "+string.Join("; ",Sim.State.Creatures.Select(c=>c.Name+" "+c.Activity)));
            var selected=Sim.State.Creatures[0];SelectOil(selected.Id);await Capture("sleeping-creature-status");
            if(!_condition.Text.Contains("夜间住宿"))throw new Exception("creature housing status missing");
            SaveStore.Validate(Sim.State);
            File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: real scene default free house actor, comfort tiers, three autonomous night arrivals, sleep, occupancy UI, creature status and save validation. Renderer: "+DisplayServer.GetName());
            GetTree().Quit();
        }
        catch(Exception e){File.WriteAllText(Path.Combine(directory,"result.txt"),"FAIL: "+e);GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}

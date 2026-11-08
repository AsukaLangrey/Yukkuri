using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;
public partial class InteractionRoom
{
    private void StartSleepHousingVerification(string[] args)
    {int flag=Array.IndexOf(args,"--sleep-housing-check-output");if(flag>=0&&flag+1<args.Length)_=VerifySleepHousing(Path.GetFullPath(args[flag+1]));}
    private async Task VerifySleepHousing(string directory)
    {
        Directory.CreateDirectory(directory);int assertions=0;_autoClock=-100000;_muted=true;
        void Check(bool condition,string why){if(!condition)throw new Exception(why);assertions++;}
        void Tick(double seconds)
        {
            Sim.State.Settings.Pause=false;double end=Sim.State.WorldSeconds+seconds;
            while(Sim.State.WorldSeconds<end-1e-8){Sim.AdvanceInteractionPresentation(.025);Sim.Advance(.025);}
            Sim.State.Settings.Pause=true;
        }
        async Task Capture(string name)
        {RefreshRoomUi();await RenderFrames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var picture=GetViewport().GetTexture().GetImage();picture.SavePng(Path.Combine(directory,name+".png"));}
        void CheckColliders(bool suppressed,string stage)
        {
            foreach(var c in Sim.State.Creatures)
            {
                var actor=_actors[c.Id];
                Check(Sim.IsShelterCollisionSuppressed(c)==suppressed,stage+": simulation collider "+c.Name);
                Check(actor.CollisionLayer==(suppressed?0u:2u)&&actor.CollisionMask==(suppressed?0u:3u),stage+": Godot collider "+c.Name);
            }
        }
        try
        {
            await RenderFrames(8);GetWindow().Size=new(1440,900);
            foreach(int speed in WorldSimulation.CourtyardSpeeds)
            {
                Sim=WorldSimulation.CreateCourtyardEconomy(20261103);Sim.SetCourtyardSpeed(speed);Sim.Config.WildGrassEnabled=false;
                Sim.State.Shelters.Clear();Sim.State.RoomObjects.Clear();Sim.State.SupplyStations.Clear();Sim.State.AutonomousBreeding=false;
                int n=0;foreach(var c in Sim.State.Creatures)
                {
                    GrowthRules.Initialize(c,false);c.X=c.TargetX=(n++-1)*1.5;c.Z=c.TargetZ=1;
                    c.Fullness=c.Energy=c.Interaction.Hydration=100;c.Fear=c.Pain=0;c.Interaction.Sleepiness=60;
                    c.Interaction.NextAutonomyAt=c.DecisionIn=c.Family.NextCareAt=c.Family.NextSocialAt=c.Family.NextBreedingDecisionAt=1e9;
                    c.Family.CompanionTargetId=null;c.Family.CompanionUntil=0;c.Interaction.Emotion.Alarm=0;c.Interaction.Emotion.Manmanlai=80;
                }
                Sim.State.ShelterStock=1;Check(Sim.PlaceShelter(0,-5).Success,"place house");Sim.State.WorldSeconds=350;Sim.State.Settings.Pause=true;
                BuildActors();await RenderFrames(3);CheckColliders(false,"outdoor "+speed);
                Tick(.25);await RenderFrames(3);CheckColliders(true,"entry "+speed);
                Check(Sim.State.Creatures.All(c=>_actors[c.Id].Visible),"approach hid bodies");
                Sim=new WorldSimulation(JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(Sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!);
                Sim.Config.WildGrassEnabled=false;BuildActors();await RenderFrames(3);CheckColliders(true,"load entry "+speed);
                Tick(30);await RenderFrames(3);CheckColliders(true,"inside "+speed);
                Check(Sim.State.Creatures.All(c=>c.Interaction.Resting&&!_actors[c.Id].Visible),"inside did not sleep/hide");
                if(speed==1)await Capture("night-inside");
                Sim.State.WorldSeconds=550;Tick(25);await RenderFrames(3);CheckColliders(false,"sunrise outside "+speed);
                Check(Sim.State.Shelters[0].Residents.Count==0&&Sim.State.Creatures.All(c=>_actors[c.Id].Visible&&!c.Interaction.Resting),"sunrise did not wake/show");
                if(speed==1)await Capture("sunrise-outside");
                // Cancellation must restore the real actor mask without a permanent saved flag.
                var body=Sim.State.Creatures[0];var home=Sim.State.Shelters[0];home.Residents.Add(new(){CreatureId=body.Id,Slot=0});
                await RenderFrames(3);Check(_actors[body.Id].CollisionLayer==0,"reservation did not disable actor");
                home.Residents.Clear();await RenderFrames(3);Check(_actors[body.Id].CollisionLayer==2&&_actors[body.Id].CollisionMask==3,"cancel retained disabled actor");
                SaveStore.Validate(Sim.State);
            }
            File.WriteAllText(Path.Combine(directory,"result.txt"),$"PASS {assertions} actual-scene assertions: five speeds, visible approach, loaded entry, sleeping inside, sunrise exit, cancellation and real collision layers/masks. Scripted fixtures; no desktop control or long stress test.");GetTree().Quit();
        }
        catch(Exception e){File.WriteAllText(Path.Combine(directory,"failure.txt"),e.ToString());GD.PushError(e.ToString());GetTree().Quit(2);}
    }
}

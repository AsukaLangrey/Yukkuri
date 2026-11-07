using Godot;
using System;
using System.IO;
using System.Threading.Tasks;
using Yukkuri.Simulation;
public partial class InteractionRoom
{
    private void StartNeedsVerification(string[] args)
    {int flag=Array.IndexOf(args,"--needs-check-output");if(flag>=0&&flag+1<args.Length)_=VerifyNeeds(Path.GetFullPath(args[flag+1]));}
    private async Task VerifyNeeds(string directory)
    {
        Directory.CreateDirectory(directory);int assertions=0;_autoClock=-100000;_muted=true;
        void Check(bool condition,string why){if(!condition)throw new Exception(why);assertions++;}
        async Task Capture(string name){await RenderFrames(8);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(directory,name+".png"));}
        try
        {
            await RenderFrames(12);GetWindow().Size=new(1440,900);Sim=WorldSimulation.CreateCourtyardEconomy();BuildActors();
            var c=Sim.State.Creatures[0];Sim.State.RoomObjects.Clear();Sim.Config.WildGrassEnabled=false;
            c.Interaction.Hydration=0;Sim.SetCourtyardSpeed(10);Sim.Advance(5);Sim.State.Settings.Pause=true;SelectOil(c.Id);RefreshRoomUi();
            Check(c.Health<100,"dry creature did not lose health");Check(_healthLabel.Text.Contains("严重脱水"),"health still displays normal at zero hydration");
            Check(_hydrationWarningLabel.Visible&&_hydrationWarningLabel.Text.Contains("生命持续下降"),"missing visible dehydration consequence");
            await Capture("dehydration");Check(_hydrationWarningLabel.GetGlobalRect().End.X<=_rightPanel.GetGlobalRect().End.X,"dehydration warning overflows inspector");
            c.Interaction.Hydration=10;RefreshRoomUi();Check(_healthLabel.Text.Contains("缺水")&&_hydrationWarningLabel.Text.Contains("行动变慢"),"low-water warning missing");await Capture("low-water");
            c.Interaction.Hydration=60;RefreshRoomUi();Check(!_hydrationWarningLabel.Visible&&_healthLabel.Text.Contains("正常"),"restored hydration still displays active dehydration");await Capture("hydrated");
            c.Fullness=50;c.Interaction.Hydration=35;c.Energy=85;c.Interaction.Sleepiness=95;
            c.Interaction.Resting=false;c.Interaction.RestRemaining=0;c.Interaction.Restraint=RestraintKind.None;
            c.Interaction.CleaningTargetId=null;c.Family.CareTargetId=null;c.Interaction.ActiveCue=null;c.Interaction.EatingRemaining=0;
            RefreshRoomUi();
            Check(_fullnessLabel.Text.Contains("想吃")&&_hydrationLabel.Text.Contains("口渴"),"need labels disagree with autonomy thresholds");
            Check(_familyNeedsLabel.Text.Contains("体力 85")&&_familyNeedsLabel.Text.Contains("十分困倦"),"energy and sleep debt not distinguished");
            Check(_lifeOverview.Visible&&_lifeOverview.Text.Contains("食物与水分"),"ordinary need explanation is hidden");
            await Capture("parallel-needs");
            foreach(var label in new[]{_fullnessLabel,_hydrationLabel,_familyNeedsLabel,_lifeOverview})
                Check(label.GetGlobalRect().End.X<=_rightPanel.GetGlobalRect().End.X,"state label overflows inspector");
            string before=System.Text.Json.JsonSerializer.Serialize(Sim.State,SaveStore.JsonOptions);
            for(int n=0;n<10;n++)RefreshRoomUi();
            Check(before==System.Text.Json.JsonSerializer.Serialize(Sim.State,SaveStore.JsonOptions),"UI observation changed world state");
            c.Interaction.Restraint=RestraintKind.Frame;RefreshRoomUi();
            Check(_lifeOverview.Visible&&_lifeOverview.Text.Contains("固定具"),"restraint explanation missing");await Capture("restrained-needs");
            c.Interaction.Restraint=RestraintKind.None;c.Fullness=c.Interaction.Hydration=100;c.Interaction.Sleepiness=20;
            c.Fear=c.Pain=0;c.Interaction.Emotion.Alarm=30;RefreshRoomUi();
            Check(_lifeOverview.Visible&&_lifeOverview.Text.Contains("惊惧"),"emotional recovery explanation missing");await Capture("emotional-recovery");
            c.Family.ColdStorage.Stored=true;RefreshRoomUi();
            Check(!_familyNeedsLabel.Visible&&!_continuityLabel.Visible&&_condition.Text.Contains("生理时间暂停"),"cold storage still shows active needs");
            c.Family.ColdStorage.Stored=false;c.Interaction.Emotion.Alarm=0;c.Interaction.Water.RecoverySeconds=0;
            RefreshRoomUi();Check(!_lifeOverview.Visible,"healthy idle state displays redundant explanation");
            GetWindow().Size=new(1920,1080);c.Interaction.Hydration=35;RefreshRoomUi();await Capture("wide-needs");
            Check(_lifeOverview.Visible&&_lifeOverview.GetGlobalRect().End.X<=_rightPanel.GetGlobalRect().End.X,"wide inspector explanation missing or overflowing");
            File.WriteAllText(Path.Combine(directory,"result.txt"),$"PASS {assertions} rendered hydration checks; screenshots are scripted fixtures, not manual gameplay.");GetTree().Quit();
        }
        catch(Exception e){File.WriteAllText(Path.Combine(directory,"failure.txt"),e.ToString());GD.PushError(e.ToString());GetTree().Quit(2);}
    }
}

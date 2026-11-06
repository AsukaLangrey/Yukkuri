using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private void StartDayNightVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,"--day-night-check-output");
        if(flag<0)return;
        if(flag+1>=args.Length||!args.Contains("--interaction-test-profile"))
            throw new ArgumentException("Day/night checks require output and an isolated interaction test profile.");
        // SaveStore creates the profile directory during _Ready. Reject any existing
        // contents before the harness can write slot 3 or run scene frames.
        string profileDirectory=Path.GetDirectoryName(_saves.SlotPath(3))!;
        if(Directory.EnumerateFileSystemEntries(profileDirectory).Any())
            throw new InvalidOperationException("Day/night checks require a fresh, empty test profile directory.");
        _=VerifyDayNight(Path.GetFullPath(args[flag+1]));
    }
    private async Task VerifyDayNight(string output)
    {
        var checks=new List<string>();
        void Require(bool value,string message) { if(!value)throw new Exception(message); }
        object Snapshot()=>new {seconds=Sim.State.WorldSeconds,sun=_courtyardSun.LightEnergy,moon=_courtyardMoon.LightEnergy,
            ambient=_courtyardEnvironment.AmbientLightEnergy,background=_courtyardEnvironment.BackgroundColor.ToHtml(),
            pitch=_courtyardSun.RotationDegrees.X,yaw=_courtyardSun.RotationDegrees.Y};
        string Light()=>JsonSerializer.Serialize(Snapshot());
        try
        {
            _autoClock=-10000;_muted=true;Sim.State.Settings.Pause=true;
            await RenderFrames(3);
            var left=_leftPanel.Modulate;var right=_rightPanel.Modulate;
            var leftSelf=_leftPanel.SelfModulate;var rightSelf=_rightPanel.SelfModulate;
            Require(_ui.GetParent() is CanvasLayer,"UI is not on separate canvas layer");
            for(int season=0;season<4;season++)
            {
                double midnight=(season*7+1)*600-200;
                var date=CourtyardCalendar.FromWorldSeconds(midnight);
                foreach(int minute in new[]{0,date.SunriseMinute-30,date.SunriseMinute,date.SunriseMinute+30,720,date.SunsetMinute-30,date.SunsetMinute,date.SunsetMinute+30})
                {
                    Sim.State.WorldSeconds=midnight+minute*5.0/12;await RenderFrames(2);
                    var expected=CourtyardLightCycle.Sample(Sim.State.WorldSeconds);
                    Require(Math.Abs(_courtyardSun.LightEnergy-.34*expected.Daylight)<1e-6,"sun energy mismatch");
                    Require(Math.Abs(_courtyardMoon.LightEnergy-.16*(1-expected.Daylight))<1e-6,"moon energy mismatch");
                    Require(_leftPanel.Modulate==left&&_rightPanel.Modulate==right&&_leftPanel.SelfModulate==leftSelf&&_rightPanel.SelfModulate==rightSelf,"sidebar tint changed");
                }
            }
            checks.Add("32 season/time samples applied to actual scene; separate CanvasLayer and sidebar modulation unchanged");
            Sim.State.WorldSeconds=550;await RenderFrames(2);string frozen=Light();await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);await RenderFrames(2);
            Require(Light()==frozen,"pause drift");checks.Add("pause freezes clock and lighting");
            Sim.State.Settings.Pause=false;ToggleClose();await RenderFrames(2);frozen=Light();await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);await RenderFrames(2);
            Require(Sim.State.Mode==PlayMode.CloseUp&&Light()==frozen,"closeup drift");ToggleClose();
            checks.Add("closeup freezes clock and lighting");
            ShowSettings();await RenderFrames(2);frozen=Light();await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);await RenderFrames(2);
            Require(Light()==frozen,"settings drift");CloseModal();checks.Add("settings freeze clock and lighting");
            Sim.State.Settings.Pause=true;Sim.State.WorldSeconds=550.125;await RenderFrames(2);frozen=Light();
            Require(SaveRoom(3,false),"synthetic save failed");
            Sim.State.WorldSeconds=100;await RenderFrames(2);LoadRoom(3);await RenderFrames(2);
            Require(Light()==frozen,"reload lighting mismatch");checks.Add("synthetic save reload restores exact lighting without offline advance");
            foreach(double seconds in new[]{double.MaxValue,1e30,0})
            {Sim.State.WorldSeconds=seconds;await RenderFrames(2);Require(float.IsFinite(_courtyardSun.LightEnergy)&&float.IsFinite(_courtyardMoon.LightEnergy),"extreme nonfinite lighting");}
            checks.Add("extreme save-valid timestamps remain finite in actual scene");
            Sim.State.WorldSeconds=550;Sim.State.Settings.Pause=false;await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);await RenderFrames(2);
            Require(Sim.State.WorldSeconds>550&&_lastLightingSeconds==Sim.State.WorldSeconds,$"resume does not update lighting: time={Sim.State.WorldSeconds}, light={_lastLightingSeconds}, mode={Sim.State.Mode}, pause={Sim.State.Settings.Pause}, modal={_modalOpen}");checks.Add("resume advances clock and updates lighting");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,JsonSerializer.Serialize(new {status="passed",checks,renderer=DisplayServer.GetName(),visualAcceptance=false,final=Snapshot()},new JsonSerializerOptions{WriteIndented=true}));
            GetTree().Quit();
        }
        catch(Exception e)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,JsonSerializer.Serialize(new {status="failed",checks,error=e.ToString()}));
            GD.PushError(e.ToString());GetTree().Quit(1);
        }
    }
}
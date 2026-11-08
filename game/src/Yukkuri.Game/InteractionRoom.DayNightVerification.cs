using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private void StartDayNightVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,"--day-night-check-output");
        if(flag>=0&&flag+1<args.Length)_=VerifyDayNight(Path.GetFullPath(args[flag+1]));
    }
    private async Task VerifyDayNight(string directory)
    {
        int assertions=0;
        void Check(bool ok,string why){if(!ok)throw new Exception(why);assertions++;}
        try
        {
            Directory.CreateDirectory(directory);_autoClock=-10000;_muted=true;Sim.State.Settings.Pause=true;
            await RenderFrames(20);
            void LayoutCheck()
            {
                var canvas=GameUiLayout.Canvas(_ui);
                Check(Math.Abs(_leftPanel.Position.Y-14)<1&&Math.Abs(_rightPanel.Position.Y-14)<1,"top bar gap remains");
                Check(_clockLabel.GetParent()==_ui&&canvas.Encloses(_clockLabel.GetGlobalRect()),"clock outside transparent canvas");
                Check(canvas.Encloses(_resourceBar.GetGlobalRect())&&!_clockLabel.GetGlobalRect().Intersects(_resourceBar.GetGlobalRect()),"clock/resource overlap");
                Check(IsScenePoint(_clockLabel.GetGlobalRect().GetCenter())&&IsScenePoint(_resourceBar.GetGlobalRect().GetCenter()),"transparent text blocks scene input");
                Check(_eventFeed.Position.Y>=Math.Max(_clockLabel.GetRect().End.Y,_resourceBar.GetRect().End.Y),"event feed overlap");
                Check(!_systemControls.IsVisibleInTree(),"system actions remain in sidebar");
                Check(canvas.Encloses(_speedButton.GetGlobalRect())&&!IsScenePoint(_speedButton.GetGlobalRect().GetCenter()),"speed control overlaps scene input");
            }
            async Task Capture(string name)
            {
                RefreshRoomUi();RefreshDayNightLighting();await RenderFrames(12);LayoutCheck();
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var picture=GetViewport().GetTexture().GetImage();
                Check(picture.SavePng(Path.Combine(directory,name+".png"))==Error.Ok,"save render");
            }
            foreach(var sample in new[]{("spring-day",1,12.0),("spring-dusk",1,22.0),("spring-night",1,23.5),
                ("summer-dawn",8,5.5),("autumn-day",15,12.0),("winter-night",22,22.0)})
            {
                Sim.State.WorldSeconds=(sample.Item2+sample.Item3/24-8.0/24)*600;
                await Capture(sample.Item1);
                var t=CourtyardCalendar.At(Sim.State.WorldSeconds);
                Check(Math.Abs(_sunLight.LightEnergy-.34*t.Daylight)<.0001,"sun lighting out of sync");
                Check(Math.Abs(_moonLight.LightEnergy-.22*(1-t.Daylight))<.0001,"moon lighting out of sync");
            }
            foreach(var size in new[]{new Vector2I(1440,900),new Vector2I(1600,900),new Vector2I(1440,1000)})
            {
                GetWindow().Size=size;await RenderFrames(15);
                SelectToolbarCategory(7);await RenderFrames(5);await Capture("layout-"+size.X+"x"+size.Y);
                _sidebarToggle.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);LayoutCheck();
                _sidebarToggle.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
            }
            Sim.State.Money=Sim.State.Food=Sim.State.StationWaterPortions=Sim.State.Medicine=int.MaxValue;
            await Capture("large-resources");
            foreach(bool paused in new[]{false,true})
            {
                Sim.State.Settings.Pause=paused;double before=Sim.State.WorldSeconds;
                ShowSettings();await RenderFrames(8);
                Check(Sim.State.WorldSeconds==before,"settings advanced clock");
                CloseModal();Check(Sim.State.Settings.Pause==paused,"settings changed pause state");
                if(!paused){await ToSignal(GetTree().CreateTimer(1.2),SceneTreeTimer.SignalName.Timeout);Check(Sim.State.WorldSeconds>before,"clock did not resume");}
            }
            _pauseButton.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
            Check(!Sim.State.Settings.Pause,"pause control did not resume");
            _pauseButton.EmitSignal(BaseButton.SignalName.Pressed);
            double frozen=Sim.State.WorldSeconds;await RenderFrames(8);
            Check(Sim.State.WorldSeconds==frozen,"pause control did not freeze");
            ShowHelp();await RenderFrames(5);LayoutCheck();CloseActivityPanel();
            ToggleDebugPanel();await RenderFrames(5);Check(DebugPanelVisible,"debug unreachable");ToggleDebugPanel();
            File.WriteAllText(Path.Combine(directory,"result.txt"),$"PASS: {assertions} assertions; real rendered scene, six seasonal lighting samples, three window sizes, collapsed navigation, large resource values, transparent input, system controls and pause restoration.\n"+DisplayServer.GetName());
            GetTree().Quit();
        }
        catch(Exception e){File.WriteAllText(Path.Combine(directory,"result.txt"),"FAIL: "+e);GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}

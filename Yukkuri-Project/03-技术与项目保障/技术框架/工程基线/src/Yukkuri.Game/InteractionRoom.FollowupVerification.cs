using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Yukkuri.Simulation;
public partial class InteractionRoom
{
    private void StartFollowupVerification(string[] args)
    {int f=Array.IndexOf(args,"--followup-check-output");if(f>=0&&f+1<args.Length)_=VerifyFollowup(Path.GetFullPath(args[f+1]));}
    private async Task VerifyFollowup(string directory)
    {
        int assertions=0;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);assertions++;}
        async Task Capture(string name)
        {await RenderFrames(8);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(directory,name+".png"));}
        try
        {
            Directory.CreateDirectory(directory);_muted=true;_autoClock=-1000000;await RenderFrames(10);
            Sim=WorldSimulation.CreateCourtyardEconomy();BuildActors();Sim.State.Settings.Pause=true;
            foreach(var size in new[]{new Vector2I(1440,900),new Vector2I(1600,900),new Vector2I(1440,1000)})
            {
                GetWindow().Size=size;await RenderFrames(8);SelectOil(Sim.State.Creatures[0].Id);RefreshRoomUi();await RenderFrames(5);
                Check(Math.Abs(_clockLabel.GlobalPosition.Y-_speedLinks.GlobalPosition.Y)<5,"clock and speeds not on one line");
                Check(_speedButton is RichTextLabel,"speed control is a button");
                Check(GameUiLayout.Canvas(_ui).Encloses(_clockLine.GetGlobalRect()),"clock row overflows canvas");
                Check(!_rightPanel.GetGlobalRect().Intersects(_clockLine.GetGlobalRect()),"clock overlaps inspector");
                Check(_selectionName.GetGlobalRect().End.X<=_closeButton.GetGlobalRect().Position.X,"name overlaps focus icon");
                foreach(int speed in WorldSimulation.CourtyardSpeeds){_speedLinks.EmitSignal(RichTextLabel.SignalName.MetaClicked,speed.ToString());Check(Sim.CourtyardSpeed==speed,"speed did not change");}
                await Capture($"ui-{size.X}x{size.Y}");
            }
            Sim.SetCourtyardSpeed(1);ShowContainer();await RenderFrames(5);
            Check(_sidebarToggle.Size.X>=31,"navigation toggle squeezed after box selection");
            Check(_selectionName.Text=="回收箱"&&!_inspectorTabs.IsVisibleInTree()&&!_closeButton.IsVisibleInTree(),"recycle box mixed with creature identity");
            Check(Sim.Selected==null,"box retained previous selected creature");await Capture("recycle-box");
            SelectOil(Sim.State.Creatures[1].Id);Check(!InspectingRecycleBox,"box content survived selecting another object");
            ToggleToolbarCategory(2);ShowContainer();ReturnToCourtyard();Check(_roomMode==RoomMode.Observe&&!ActivityPanelVisible&&Sim.State.Settings.Pause,"return target or pause state incorrect");
            var house=Sim.State.Shelters.First();SelectShelter(house.Id);await RenderFrames(8);
            Check(house.Capacity==5,"hut capacity changed");Check(!_shelterOpenButton.IsVisibleInTree()&&!_shelterCloseButton.IsVisibleInTree()&&!_shelterCallButton.IsVisibleInTree()&&!_shelterShopButton.IsVisibleInTree(),"obsolete house buttons remain");
            await Capture("house-day");
            var oil=Sim.State.Creatures[0];oil.Family.Pregnancy.Phase=PregnancyPhase.Gestating;oil.Family.Pregnancy.Method=BirthMethod.Stem;
            oil.Family.Pregnancy.EventId="followup-stem";oil.Family.Pregnancy.PlannedChildren=3;oil.Family.Pregnancy.Elapsed=FamilyRules.GestationSeconds(BirthMethod.Stem)*.75;
            oil.Family.Pregnancy.Stem=new(){EventId="followup-stem"};SelectOil(oil.Id);_yardFocus=new((float)oil.X,0,(float)oil.Z);_zoom=7;
            await Capture("model-stem");
            _zoom=16*(float)CourtyardGround.Expansion;_yardFocus=Vector3.Zero;Sim.State.WorldSeconds=(24-8)*25;RefreshRoomUi();await Capture("night-sky");
            Check(_moonDisc.IsVisibleInTree()&&_stars.Any(s=>s.IsVisibleInTree()),"night sky missing");
            Sim.State.WorldSeconds=(12-8)*25;await RenderFrames(5);Check(_sunDisc.IsVisibleInTree(),"sun missing");await Capture("day-sky");
            File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{assertions,status="passed",scope="rendered controls and scripted input; not human play or long-duration stability"}));GetTree().Quit();
        }
        catch(Exception e){File.WriteAllText(Path.Combine(directory,"failure.txt"),e.ToString());GetTree().Quit(2);}
    }
}

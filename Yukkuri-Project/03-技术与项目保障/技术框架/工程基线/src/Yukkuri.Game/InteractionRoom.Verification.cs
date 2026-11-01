using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Render checks exercise the real scene and controls. They are not native mouse-play evidence.
    private void StartRoomVerificationIfRequested(string[] args)
    {
        int contactFlag=Array.IndexOf(args,"--contact-check-output");
        if(contactFlag>=0&&contactFlag+1<args.Length){_=VerifyContactGeometry(args[contactFlag+1]);return;}
        int flag=Array.IndexOf(args,"--room-render-output");
        if(flag>=0&&flag+1<args.Length)_=VerifyRoomRender(Path.GetFullPath(args[flag+1]));
    }
    private async Task RenderFrames(int count=5)
    {for(int n=0;n<count;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task VerifyRoomRender(string directory)
    {
        var results=new List<object>();
        try
        {
            Directory.CreateDirectory(directory);_autoClock=-10000;_muted=true;
            await RenderFrames(20);
            async Task Capture(string name)
            {
                await RenderFrames(10);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var picture=GetViewport().GetTexture().GetImage();
                var error=picture.SavePng(Path.Combine(directory,name+".png"));
                if(error!=Error.Ok)throw new IOException("截图失败："+error);
                Rect2 viewport=_ui.GetGlobalRect();
                bool sidebarsFit=viewport.Encloses(_leftPanel.GetGlobalRect())&&viewport.Encloses(_rightPanel.GetGlobalRect());
                bool forceFits=GameUiLayout.Canvas(_ui).Encloses(_forceWidget.GetGlobalRect());
                bool forceSingleLine=_forceLabel.Size.Y<40;
                if(!sidebarsFit||!forceFits||!forceSingleLine)throw new InvalidOperationException("侧栏或画布参数组件超出布局范围");
                results.Add(new{name,sidebarsFit,forceFits,forceSingleLine,viewport=new{viewport.Size.X,viewport.Size.Y},
                    feedback=new{x=_feedback.GlobalPosition.X,y=_feedback.GlobalPosition.Y,width=_feedback.Size.X,height=_feedback.Size.Y},
                    action=_action.ToString(),mode=Sim.State.Mode.ToString()});
            }
            Sim.State.Settings.Pause=true;await Capture("01-yard");
            Sim.State.Settings.Pause=false;ToggleClose();await RenderFrames(90);await Capture("02-close");
            SelectGroup(InteractionCatalog.Groups.Single(g=>g.Id=="P06"));await Capture("03-feeding-controls");
            SetToolTab(true);SelectGroup(InteractionCatalog.Groups.Single(g=>g.Id=="P15"));await Capture("04-force-controls");
            ShowHelp();await Capture("05-help");CloseActivityPanel();
            // Gameplay panels must preserve both running and explicitly paused states.
            foreach(bool paused in new[]{false,true})
            {
                Sim.State.Settings.Pause=paused;
                foreach(var page in new (string name,Action open)[]{
                    ("help",ShowHelp),("shop",()=>ShowShop()),("arrival",ShowCreatureArrival),
                    ("family",ShowFamily),("commands",ShowCommandChoices),("teaching",ShowTeachingChoices),
                    ("stem",ShowStemChoices),("breeding",ShowBreedingStatus),("container",ShowContainer),
                    ("shelters",()=>ShowShelterSupplies()),("details",ShowFamilyDetails)})
                {
                    page.open();await RenderFrames(2);
                    bool valid=ActivityPanelVisible&&!_modalOpen&&!_modalShade.Visible&&Sim.State.Settings.Pause==paused&&
                        IsScenePoint(GameUiLayout.Canvas(_ui).GetCenter())&&!IsScenePoint(_activityPanel.GetGlobalRect().GetCenter());
                    if(!valid)throw new InvalidOperationException("玩法侧栏阻断场景或改变暂停状态："+page.name);
                    CloseActivityPanel();
                    if(Sim.State.Settings.Pause!=paused)throw new InvalidOperationException("关闭侧栏改变暂停状态："+page.name);
                    results.Add(new{name="sidebar-"+page.name,paused,sceneInputAvailable=true});
                }
                ShowSettings();
                if(!_modalOpen||!Sim.State.Settings.Pause)throw new InvalidOperationException("系统设置未保持模态暂停");
                CloseModal();
                if(Sim.State.Settings.Pause!=paused)throw new InvalidOperationException("系统设置未恢复原暂停状态");
            }
            var canvas=GameUiLayout.Canvas(_ui);
            if(!IsScenePoint(new Vector2(canvas.Position.X+20,canvas.End.Y-20)))
                throw new InvalidOperationException("旧底部区域仍有不可操作的空白带");
            if(_forceWidget.Visible&&(IsScenePoint(_forceSlider.GetGlobalRect().GetCenter())||!IsScenePoint(_forceLabel.GetGlobalRect().GetCenter())))
                throw new InvalidOperationException("力度控件与透明区域的输入边界不正确");
            ShowCreatureArrival();await RenderFrames(2);
            var draft=_panelContents.GetChildren().OfType<LineEdit>().Single();draft.Text="保留输入";
            ShowHelp();ShowCreatureArrival();await RenderFrames(2);
            if(_panelContents.GetChildren().OfType<LineEdit>().Single().Text!="保留输入")
                throw new InvalidOperationException("切换右侧页面丢失了未提交输入");
            CloseActivityPanel();
            Sim.State.Settings.Pause=false;
            if(OS.GetCmdlineUserArgs().Contains("--art-gallery"))
            {
                // Deliberately authored pose gallery, not evidence of player input or simulation outcomes.
                foreach(var c in Sim.State.Creatures)
                {
                    Sim.State.SelectedId=c.Id;Sim.State.Mode=PlayMode.CloseUp;
                    c.Interaction.Dirt=0;c.Interaction.SurfaceDirt.Clear();c.Interaction.SideDirt.Clear();
                    c.Interaction.Wetness=0;c.Interaction.SurfaceWetness.Clear();c.Interaction.SideWetness.Clear();
                    c.Expression=Yukkuri.Simulation.Expression.Neutral;
                    await RenderFrames(80);await Capture("art-"+c.Name+"-idle");
                    c.Interaction.Carried=true;c.Interaction.Height=.8;
                    await RenderFrames(80);await Capture("art-"+c.Name+"-carried");
                    c.Interaction.Carried=false;c.Interaction.Height=0;
                    c.Interaction.Accessory.Place=AccessoryPlace.Hand;c.Interaction.Accessory.X=c.X+1;
                    c.Interaction.Accessory.Z=c.Z;c.Interaction.Accessory.Height=.7;
                    c.Interaction.Accessory.Dirt=30;
                    await RenderFrames(40);await Capture("art-"+c.Name+"-accessory-dirty");
                    c.Interaction.Accessory.Dirt=12;await Capture("art-"+c.Name+"-accessory-partly-clean");
                    c.Interaction.Accessory.Dirt=3;await Capture("art-"+c.Name+"-accessory-nearly-clean");
                    c.Interaction.Accessory.Dirt=0;c.Interaction.Accessory.Integrity=40;
                    await Capture("art-"+c.Name+"-accessory-torn");
                    c.Interaction.Accessory.Integrity=100;c.Interaction.Accessory.RepairProgress=36;
                    await Capture("art-"+c.Name+"-accessory-repaired");
                    c.Interaction.Accessory.Place=AccessoryPlace.Ground;c.Interaction.Accessory.Height=0;
                    await Capture("art-"+c.Name+"-accessory-ground");
                    c.Interaction.Accessory.Place=AccessoryPlace.Worn;c.Interaction.Accessory.RepairProgress=0;
                    foreach(var fixture in new[]{RestraintKind.Wrap,RestraintKind.Box,RestraintKind.Frame,RestraintKind.Hanging})
                    {
                        c.Interaction.Restraint=fixture;c.Interaction.Height=fixture==RestraintKind.Hanging?.7:0;
                        await RenderFrames(30);await Capture("art-"+c.Name+"-"+fixture);
                    }
                    c.Interaction.Restraint=RestraintKind.None;c.Interaction.Height=0;c.Interaction.Supine=true;
                    await RenderFrames(30);await Capture("art-"+c.Name+"-supine");
                    c.Interaction.Dirt=40;c.Interaction.SurfaceDirt[BodyPart.Bottom]=60;
                    await Capture("art-"+c.Name+"-underside-dirt");
                    c.Interaction.Dirt=0;c.Interaction.SurfaceDirt.Clear();c.Interaction.SideDirt.Clear();c.Interaction.FootIntegrity=35;
                    await Capture("art-"+c.Name+"-underside-injury");
                    c.Interaction.FootIntegrity=100;c.Interaction.Supine=false;
                    foreach(string pose in new[]{"pressure","roll","nuzzle","stretch"})
                    {
                        c.Interaction.ReactionPose=pose;c.Interaction.ReactionPoseRemaining=1000;_force=.9f;
                        await RenderFrames(22);await Capture("art-"+c.Name+"-"+pose);
                    }
                    c.Interaction.ReactionPose="idle";c.Interaction.ReactionPoseRemaining=0;
                }
            }
            var frameTimes=new List<double>();ulong previous=Time.GetTicksUsec();
            for(int n=0;n<180;n++)
            {
                await RenderFrames(1);ulong current=Time.GetTicksUsec();
                frameTimes.Add((current-previous)/1000.0);previous=current;
            }
            frameTimes.Sort();
            var performance=new{scope="180 process-frame intervals in this verification window; not a general hardware benchmark",samples=frameTimes.Count,
                medianMs=frameTimes[90],p95Ms=frameTimes[171],maxMs=frameTimes[^1],
                drawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
                renderedObjects=Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame),
                primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)};
            File.WriteAllText(Path.Combine(directory,"render-checks.json"),JsonSerializer.Serialize(new{kind="engine-render-not-native-play",at=DateTimeOffset.UtcNow,results,performance},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("INTERACTION_RENDER_COMPLETE: "+directory);GetTree().Quit();
        }
        catch(Exception ex){GD.PushError(ex.ToString());File.WriteAllText(Path.Combine(directory,"error.txt"),ex.ToString());GetTree().Quit(2);}
    }
}

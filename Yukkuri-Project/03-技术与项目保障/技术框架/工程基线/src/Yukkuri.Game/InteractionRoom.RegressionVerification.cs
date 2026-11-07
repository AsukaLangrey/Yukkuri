using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yukkuri.Simulation;
public partial class InteractionRoom
{
    private void StartRegressionVerification(string[] args)
    {int flag=Array.IndexOf(args,"--regression-check-output");if(flag>=0&&flag+1<args.Length)_=VerifyRegression(Path.GetFullPath(args[flag+1]));}
    private async Task VerifyRegression(string directory)
    {
        Directory.CreateDirectory(directory);int assertions=0;_autoClock=-100000;_muted=true;
        void Check(bool condition,string why){if(!condition)throw new Exception(why);assertions++;}
        async Task Capture(string name){RefreshRoomUi();await RenderFrames(5);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(directory,name+".png"));}
        try
        {
            await RenderFrames(12);GetWindow().Size=new(1440,900);Sim=WorldSimulation.CreateCourtyardEconomy();Sim.State.Settings.Pause=true;BuildActors();
            foreach(var kind in new[]{RoomObjectKind.Feces,RoomObjectKind.Urine})
            {
                DeselectInspector();var obj=new RoomObject{Id="ui-waste-"+kind,RegionId=Sim.State.ActiveRegionId,Kind=kind,X=0,Z=4};Sim.State.RoomObjects.Add(obj);
                _selectedRoomObjectId=obj.Id;RefreshRoomUi();await Capture(kind.ToString());
                Check(_selectionName.Text==(kind==RoomObjectKind.Feces?"粪便":"尿液")&&_selectionName.IsVisibleInTree(),"waste title absent");
                Check(_objectDetails.Text.Contains("环境评分")&&_speechLabel.Text.Length==0,"waste details or stale empty-selection prompt");
            }
            var adult=Sim.State.Creatures.First(c=>GrowthRules.IsAdult(c)&&c.Body.ReproductionRole==ReproductionRole.Paternal);
            var child=Sim.State.Creatures.First(c=>!GrowthRules.IsAdult(c));
            SelectOil(adult.Id);_familySecondId=child.Id;ShowFamily();await RenderFrames(3);
            var choices=_panelContents.GetChildren().OfType<InlineChoice>().ToArray();Check(choices.Length==2,"family choices absent");
            RememberActivityPage();_autoRefreshingActivity=true;_familySecondId=null;ShowFamily();_autoRefreshingActivity=false;await RenderFrames(3);
            choices=_panelContents.GetChildren().OfType<InlineChoice>().ToArray();var second=Sim.State.Creatures.Single(c=>c.Id==_familySecondId);
            Check(GrowthRules.IsAdult(second),"default partner was a child");
            Check(choices[1].GetItemText(choices[1].Selected)==OilListName(second),"restored row index desynchronized displayed partner");
            var home=Sim.State.Shelters.Single();double oldX=second.X,oldZ=second.Z;
            second.X=home.X;second.Z=home.Z;home.Residents.Add(new(){CreatureId=second.Id,Slot=2,Phase=ShelterVisitPhase.Inside,VisualHidden=true});
            _familySecondId=null;ShowFamily();await RenderFrames(3);
            Check(_familySecondId==second.Id,"hidden adult omitted from partner candidates");
            Check(_panelContents.GetChildren().OfType<Button>().Any(b=>b.Text=="请"+second.Name+"先出屋"),"missing exit action for hidden partner");
            home.Residents.Clear();second.X=oldX;second.Z=oldZ;
            Sim.State.Settings.Pause=false;_familySecondId=child.Id;ShowFamily();await RenderFrames(3);
            var breeding=_panelContents.GetChildren().OfType<HBoxContainer>().SelectMany(r=>r.GetChildren().OfType<Button>()).Where(b=>b.Text.StartsWith("安排")&&b.Text.EndsWith("孕育")).ToArray();
            Check(breeding.Length==2&&breeding.All(b=>b.Disabled),"child breeding controls enabled");
            Check(_panelContents.GetChildren().OfType<Label>().Any(l=>l.Text.Contains("成熟")),"no visible breeding blocker");
            await Capture("family-blocked");Sim.State.Settings.Pause=true;CloseActivityPanel();SelectOil(adult.Id);adult.Personality.Kindness=1;adult.Interaction.RuleLearning=100;RefreshRoomUi();
            Check(_objectDetails.Text.Contains("环境评分")&&_objectDetails.Text.Contains("卫生要求"),"missing sanitation status");await Capture("sanitation");
            // Render actual system menus: wrapped buttons in HBox must share width.
            foreach(var size in new[]{new Vector2I(1440,900),new Vector2I(1920,1080)})
            {
                GetWindow().Size=size;await RenderFrames(5);CloseActivityPanel();
                foreach(bool pausedBefore in new[]{false,true})
                {
                    Sim.State.Settings.Pause=pausedBefore;ShowSystemMenu();await RenderFrames(4);
                    Check(_closeButton.Size.X<=40,"focus icon expanded into title");
                    Check(_modalContents.GetChildren().OfType<Button>().Any(b=>b.Text=="保存游戏 · F5"),"save hidden behind settings");
                    Check(_modalContents.GetChildren().OfType<Button>().Any(b=>b.Text=="读取游戏 · F9"),"load hidden behind settings");
                    var speeds=_modalContents.GetChildren().OfType<HBoxContainer>().SelectMany(r=>r.GetChildren().OfType<Button>()).ToArray();
                    Check(speeds.Length==5&&speeds.All(b=>b.Size.X>=90&&b.Size.Y>=32),"collapsed speed buttons");
                    await Capture("system-"+size.X+"-"+pausedBefore);
                    _Input(new InputEventKey{Pressed=true,Keycode=Key.F5});await RenderFrames(4);
                    var saves=_modalContents.GetChildren().OfType<Button>().Where(b=>b.Text.StartsWith("保存到 手动槽位")).ToArray();
                    Check(saves.Length==3&&saves.All(b=>b.Size.X>=500&&b.Size.Y>=48),"save buttons unreadable");
                    await Capture("save-"+size.X);
                    saves[2].EmitSignal(Godot.Button.SignalName.Pressed);await RenderFrames(3);
                    Check(!_modalOpen&&Sim.State.Settings.Pause==pausedBefore,"save changed pause state");
                    var saved=_saves.Load(3).State;Check(saved.Settings.Pause==pausedBefore,"saved menu pause instead of user pause");
                    _Input(new InputEventKey{Pressed=true,Keycode=Key.F9});await RenderFrames(4);
                    var loads=_modalContents.GetChildren().OfType<Button>().Where(b=>b.Text.StartsWith("读取 ")).ToArray();
                    Check(loads.Length==4&&loads.All(b=>b.Size.X>=500&&b.Size.Y>=48),"load rows unreadable");
                    Check(loads.Single(b=>b.Text.Contains("手动槽位 3")).Disabled==false,"existing save disabled");
                    await Capture("load-"+size.X);
                    loads.Single(b=>b.Text.Contains("手动槽位 3")).EmitSignal(Godot.Button.SignalName.Pressed);await RenderFrames(3);
                    Check(!_modalOpen&&Sim.State.Settings.Pause==pausedBefore,"load changed saved pause");
                    ShowSettings();await RenderFrames(3);CloseModal();Check(Sim.State.Settings.Pause==pausedBefore,"nested settings pause");
                }
            }
            GetWindow().Size=new(1440,900);
            // Exercise the real actor at five speeds, including a cosmetic reaction
            // while walking, fixed-tick positions, stopping and reversing direction.
            using var trace=new StreamWriter(Path.Combine(directory,"actor-motion.csv"));trace.WriteLine("speed,frame,logic_z,display_z,lag,pose");
            foreach(int speed in WorldSimulation.CourtyardSpeeds)
            {
                Sim=WorldSimulation.CreateCourtyardEconomy();Sim.State.Settings.Pause=true;Sim.State.Shelters.Clear();Sim.State.RoomObjects.Clear();
                var c=Sim.State.Creatures[0];c.X=c.TargetX=0;c.Z=c.TargetZ=3;c.Fullness=c.Interaction.Hydration=100;c.Pain=c.Fear=0;
                BuildActors();await RenderFrames(3);var actor=_actors[c.Id];double elapsed=0,remainder=0,maxLag=0;
                for(int n=0;n<360;n++)
                {
                    double worldDt=speed/60.0;elapsed+=worldDt;remainder+=worldDt;
                    while(remainder>=.25){remainder-=.25;double before=c.Z;c.Z=c.TargetZ=elapsed<2?3+elapsed*.65:elapsed<4?4.3-(elapsed-2)*.65:3;if(c.Z!=before)c.Interaction.AutonomousMotionSerial++;}
                    string pose=n%120<60?"sing":"idle";
                    actor.RefreshInteraction(c,true,false,1f/60,pose,0,Vector2.Zero,false,roomObjects:Sim.State.RoomObjects,worldSpeed:speed);
                    var shown=actor.Position+actor.VisualMotionOffset;double lag=Math.Abs(c.Z-shown.Z);maxLag=Math.Max(maxLag,lag);
                    Check(Math.Abs(actor.Position.Z-c.Z)<.00001,"node/logical drift");
                    trace.WriteLine(FormattableString.Invariant($"{speed},{n},{c.Z:F6},{shown.Z:F6},{lag:F6},{actor.PoseTag}"));
                }
                Check(maxLag<.85,"unbounded locomotion lag "+speed+" "+maxLag);
                Check(Math.Abs(actor.Position.Z+actor.VisualMotionOffset.Z-c.Z)<.03,"actor never settled after stopping "+speed);
                var clock=(ActorPoseClock)typeof(CreatureActor).GetField("_interactionPoseClock",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(actor)!;
                Check(Math.Abs(clock.Age-59*speed/60f)<.01,"actual actor pose clock not scaled "+speed+" age="+clock.Age);
            }
            File.WriteAllText(Path.Combine(directory,"result.txt"),$"PASS {assertions} real-scene assertions: names, family identity, breeding blockers, sanitation status, actors at five speeds. Scripted fixture, not long-duration gameplay.");GetTree().Quit();
        }
        catch(Exception e){File.WriteAllText(Path.Combine(directory,"failure.txt"),e.ToString());GD.PushError(e.ToString());GetTree().Quit(2);}
    }
}

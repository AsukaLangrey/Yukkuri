using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Allocated only by the explicit isolated check. Normal play has no trace allocations.
    private List<string>? _speedTrace;

    private bool StartSpeedVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,"--courtyard-speed-check-output");
        if(flag<0)return false;
        SetProcess(false);_muted=true;_autoClock=-10000;
        _=VerifyCourtyardSpeed(args[flag+1]);return true;
    }

    private async Task VerifyCourtyardSpeed(string output)
    {
        var results=new List<object>();
        int assertions=0;
        string initialState=JsonSerializer.Serialize(WorldSimulation.CreateCourtyardEconomy(48271).State,SaveStore.JsonOptions);
        void Require(bool condition,string message)
        {assertions++;if(!condition)throw new InvalidOperationException(message);}
        void Near(double actual,double expected,string message,double tolerance=1e-6)
            =>Require(Math.Abs(actual-expected)<=tolerance,message+": "+actual+" / "+expected);
        async Task Reset(double speed=1,int population=3)
        {
            if(_modalOpen)CloseModal();
            Sim=new WorldSimulation(JsonSerializer.Deserialize<WorldState>(initialState,SaveStore.JsonOptions)!);
            Sim.State.AutonomousBreeding=false;
            // Default yard for the first measurements; the second fixture adds three
            // authored copies for a bounded CPU-only load probe, not a saved gameplay world.
            while(Sim.State.Creatures.Count<population)
            {
                Sim.State.Money+=10000;
                var source=Sim.State.Creatures[0];
                var copy=JsonSerializer.Deserialize<Creature>(JsonSerializer.Serialize(source,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
                int n=Sim.State.Creatures.Count;
                copy.Id="speed-crowd-"+n;copy.Body.Id="speed-crowd-body-"+n;
                copy.Registration.IdentityId="speed-crowd-identity-"+n;
                copy.X=copy.TargetX=-3+n;copy.Z=copy.TargetZ=3;
                Sim.State.Creatures.Add(copy);
            }
            Sim.State.Settings.Speed=speed;
            ResetRoomUiSession();BuildActors();_windowFocused=false;_uiClock=0;_autoClock=-10000;
            _speedTrace=null;
            await RenderFrames(2);
            _Process(0); // Initialize the same static actor state on both paths.
        }
        string StateSnapshot()
        {
            double speed=Sim.State.Settings.Speed;Sim.State.Settings.Speed=1;
            try{return JsonSerializer.Serialize(Sim.State,SaveStore.JsonOptions);}
            finally{Sim.State.Settings.Speed=speed;}
        }
        string ActorSnapshot()=>string.Join("\n",_actors.OrderBy(p=>p.Key).Select(pair=>
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var age=(ActorPoseClock)typeof(CreatureActor).GetField("_interactionPoseClock",flags)!.GetValue(pair.Value)!;
            var hop=(ActorHopMotion)typeof(CreatureActor).GetField("_hopMotion",flags)!.GetValue(pair.Value)!;
            return pair.Key+":"+age.Age+":"+hop.Position+":"+hop.Heading+":"+hop.Active+":"+hop.Frame+":"+pair.Value.Transform;
        }));
        string BallSnapshot()=>string.Join("\n",_objects.OrderBy(p=>p.Key).Where(p=>p.Value is RoomBallActor).Select(pair=>
        {
            var motion=(InteractionBallMotion)typeof(RoomBallActor).GetField("_motion",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pair.Value)!;
            return pair.Key+":"+motion.Rotation;
        }));
        InlineChoice Choice()=>_modalContents.GetChildren().OfType<InlineChoice>().Single(c=>c.Name=="CourtyardSpeedChoice");
        void Next(InlineChoice choice)=>choice.GetChildren().OfType<Button>().Last().EmitSignal(BaseButton.SignalName.Pressed);
        try
        {
            await RenderFrames(2);
            foreach(int speed in new[]{1,2,3,5,10})
            {
                await Reset(speed);
                _speedTrace=new();
                for(int frame=0;frame<8;frame++)_Process(.05);
                string actual=StateSnapshot(),actors=ActorSnapshot(),balls=BallSnapshot();
                Require(_speedTrace.Count(e=>e=="presentation")==8*speed,"presentation substep count");
                Require(_speedTrace.Count(e=>e=="world")==8*speed,"world substep count");
                Require(_speedTrace.Count(e=>e.StartsWith("actor:"))==8*speed*3,"actual actor refresh count");
                for(int offset=0;offset<_speedTrace.Count;offset+=5)
                {
                    Require(_speedTrace[offset]=="presentation"&&_speedTrace[offset+1]=="world","presentation/world order");
                    Require(_speedTrace.Skip(offset+2).Take(3).All(e=>e.StartsWith("actor:")),"actors observe each world step");
                }
                await Reset();
                for(int frame=0;frame<8*speed;frame++)_Process(.05);
                if(StateSnapshot()!=actual)
                {
                    File.WriteAllText(output+".actual-state.json",actual);
                    File.WriteAllText(output+".reference-state.json",StateSnapshot());
                }
                Require(StateSnapshot()==actual,"actual process state differs from repeated 1x frames at "+speed);
                Require(ActorSnapshot()==actors,"actual actor pose/hop differs from repeated 1x frames at "+speed);
                Require(BallSnapshot()==balls,"ball path rotation differs from repeated 1x frames at "+speed);
                results.Add(new{name="process-reference",speed,frames=8,referenceFrames=8*speed,actorUpdates=8*speed*3,passed=true});
            }
            await Reset();
            var ballObject=new RoomObject{Id="speed-path-ball",Kind=RoomObjectKind.Ball,RegionId=Sim.State.ActiveRegionId};
            Sim.State.RoomObjects.Add(ballObject);
            ballObject.X=0;ballObject.Z=0;RefreshCourtyardBalls(false);
            var pathReference=new InteractionBallMotion();pathReference.Present(0,0,false);
            ballObject.X=.1;RefreshCourtyardBalls(false);pathReference.Present(.1,0,false);
            ballObject.Z=.1;RefreshCourtyardBalls(false);pathReference.Present(.1,.1,false);
            var actualBall=(InteractionBallMotion)typeof(RoomBallActor).GetField("_motion",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(_objects[ballObject.Id])!;
            Require(actualBall.Rotation==pathReference.Rotation,"real ball actor lost L-shaped substep path");
            var beforeBall=actualBall.Rotation;RefreshCourtyardBalls(false);
            Require(actualBall.Rotation==beforeBall,"final static ball observation doubled rotation");
            results.Add(new{name="ball-actor-substep-L-path",passed=true});
            foreach(int speed in new[]{1,5,10})
            {
                await Reset(speed);_windowFocused=true;_speedTrace=new();
                double elapsed=_elapsed,auto=_autoClock;
                _Process(.05);
                Require(_speedTrace.Count(e=>e=="input-redesign")==1&&_speedTrace.Count(e=>e=="input-room")==1,"input sampled more than once");
                Near(_elapsed-elapsed,(float).05,"elapsed multiplied");Near(_autoClock-auto,(float).05,"autosave multiplied");
                _windowFocused=false;
                results.Add(new{name="real-frame-input",speed,redesignCalls=1,roomCalls=1,passed=true});
            }
            await Reset(10);
            Sim=WorldSimulation.CreateInteractionPlaytest();Sim.State.AutonomousBreeding=false;Sim.State.RoomObjects.Clear();
            Sim.State.Creatures.RemoveAt(2);
            for(int n=0;n<Sim.State.Creatures.Count;n++)
            {
                var c=Sim.State.Creatures[n];c.IsYoung=false;c.Fullness=c.Interaction.Hydration=c.Health=c.Energy=100;
                c.Pain=c.Fear=0;c.X=c.TargetX=n*3;c.Z=c.TargetZ=0;
                c.Interaction.NextAutonomyAt=c.Family.NextSocialAt=c.Family.NextCareAt=10000;
            }
            var mother=Sim.State.Creatures[0];var father=Sim.State.Creatures[1];
            mother.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Live,FatherId=father.Id,
                EventId="speed-birth",PlannedChildren=1,Elapsed=FamilyRules.GestationSeconds(BirthMethod.Live)-.1};
            Sim.State.Settings.Speed=10;SaveStore.Validate(Sim.State);BuildActors();await RenderFrames(2);
            _speedTrace=new();_Process(.05);
            Require(Sim.State.CompletedBirths.Contains("speed-birth")&&_actors.ContainsKey("speed-birth-0"),"birth actor not synchronized");
            Require(_speedTrace.Count(e=>e=="actor:speed-birth-0")==6,"newborn did not observe birth substep and all later substeps");
            SaveStore.Validate(Sim.State);
            results.Add(new{name="real-birth-at-substep-five-actor-six-observations",passed=true});

            foreach(int speed in new[]{1,10})
            {
                await Reset(speed);_camera.Position=new Vector3(9,10,12);_yardFocus=Vector3.Zero;
                Vector3 expected=_camera.Position.Lerp(new Vector3(0,11,16),.45f);
                _Process(.05);Require(_camera.Position.DistanceTo(expected)<1e-5,"camera multiplied real-frame smoothing");
                Sim.State.Mode=PlayMode.CloseUp;Sim.State.SelectedId=Sim.State.Creatures[0].Id;
                _camera.Position=new Vector3(9,10,12);_Process(.05);
                var focus=new Vector3((float)Sim.Selected!.X,.62f+(float)Sim.Selected.Interaction.Height,(float)Sim.Selected.Z);
                var orbit=InteractionCameraOrbit.Offset(_closeYaw,_closePitch,focus.Y);
                expected=new Vector3(9,10,12).Lerp(focus+new Vector3(orbit.X,orbit.Y,orbit.Z),.45f);
                Require(_camera.Position.DistanceTo(expected)<1e-5,"closeup camera did not follow post-step focus at real-time speed");
            }
            results.Add(new{name="camera-real-frame-yard-and-closeup-follow",passed=true});
            await Reset(10);
            Sim.State.Mode=PlayMode.CloseUp;
            foreach(var c in Sim.State.Creatures)c.Interaction.ReactionPoseRemaining=3;
            double world=Sim.State.WorldSeconds,remainder=Sim.State.TickRemainder;
            _speedTrace=new();_Process(.1);
            Near(Sim.State.WorldSeconds,world,"closeup world moved");Near(Sim.State.TickRemainder,remainder,"closeup remainder moved");
            Require(_speedTrace.Count(e=>e=="presentation")==1,"closeup scaled");
            foreach(var c in Sim.State.Creatures)Near(c.Interaction.ReactionPoseRemaining,2.9,"closeup existing presentation semantics changed");
            Sim.State.Settings.Pause=true;
            string pausedState=StateSnapshot(),pausedActors=ActorSnapshot();_speedTrace.Clear();_Process(.1);
            Require(StateSnapshot()==pausedState,"explicit pause progressed simulation");
            Require(ActorSnapshot()==pausedActors,"explicit pause progressed actor clocks/hops");
            Require(!_speedTrace.Contains("world")&&!_speedTrace.Contains("presentation"),"pause executed steps");
            results.Add(new{name="closeup-and-explicit-pause",passed=true});

            foreach(bool paused in new[]{false,true})
            {
                await Reset();Sim.State.Settings.Pause=paused;
                foreach(double historic in new[]{.25,1.5})
                {
                    Sim.State.Settings.Speed=historic;ShowSettings();ShowSettings();
                    var choice=Choice();
                    Require(choice.ItemCount==6&&choice.Selected==5&&choice.GetItemText(5).Contains(historic.ToString(System.Globalization.CultureInfo.InvariantCulture)),"historical speed not shown honestly");
                    Require(Sim.State.Settings.Speed==historic,"settings migrated old speed");
                    string before=StateSnapshot();_Process(.1);
                    Require(StateSnapshot()==before,"modal progression");
                    Next(choice);Require(Sim.State.Settings.Speed==1&&Choice().ItemCount==5,"historical option did not disappear");
                    for(int index=0;index<5;index++)
                    {
                        choice=Choice();choice.Select((index+4)%5);Next(choice);
                        Require(Sim.State.Settings.Speed==CourtyardSpeedChoices[index],"picker selection failed");
                    }
                    CloseModal();Require(Sim.State.Settings.Pause==paused,"settings changed pause choice");
                }
            }
            results.Add(new{name="settings-five-speeds-historical-values-pause",passed=true});

            await Reset(10);Sim.State.Settings.Pause=true;
            ShowSettings();Require(SaveRoom(1,false),"test save failed");CloseModal();
            Sim.State.Settings.Speed=2;Sim.State.Settings.Pause=false;
            LoadRoom(1);Require(Sim.State.Settings.Speed==10&&Sim.State.Settings.Pause,"load speed or pause not restored");
            ShowSettings();
            _modalContents.GetChildren().OfType<Button>().Single(b=>b.Text=="重新开一间小院").EmitSignal(BaseButton.SignalName.Pressed);
            _modalContents.GetChildren().OfType<Button>().Single(b=>b.Text=="保存并重新开始").EmitSignal(BaseButton.SignalName.Pressed);
            Require(Sim.State.Settings.Speed==1&&!Sim.State.Settings.Pause&&!_modalOpen,"restart did not reset speed/pause");
            LoadRoom(1);Require(Sim.State.Settings.Speed==10&&Sim.State.Settings.Pause,"manual saved speed lost after restart");
            results.Add(new{name="actual-settings-save-load-restart",passed=true});

            await Reset(10);_speedTrace=new();_Process(60);
            Require(_speedTrace.Count(e=>e=="world")==10,"long-frame work exceeded ten");
            Near(Sim.State.WorldSeconds+Sim.State.TickRemainder,10*(double)(float).1,"old real frame cap lost");
            double beforeChange=Sim.State.WorldSeconds+Sim.State.TickRemainder;
            Sim.State.Settings.Speed=1;_Process(.05);
            Near(Sim.State.WorldSeconds+Sim.State.TickRemainder-beforeChange,(float).05,"speed transition caught up old debt");
            results.Add(new{name="long-frame-cap-speed-change-no-debt",passed=true});

            foreach(int population in new[]{3,6})foreach(int speed in new[]{1,5,10})
            {
                await Reset(speed,population);
                for(int n=0;n<3;n++)_Process(1.0/60);
                var times=new double[24];var watch=new Stopwatch();
                for(int n=0;n<times.Length;n++)
                {watch.Restart();_Process(1.0/60);watch.Stop();times[n]=watch.Elapsed.TotalMilliseconds;}
                Array.Sort(times);
                results.Add(new{name="headless-process-timing",population,speed,frames=times.Length,stepsPerFrame=speed,
                    medianMs=times[times.Length/2],p95Ms=times[(int)Math.Ceiling(times.Length*.95)-1],maxMs=times[^1],
                    note="CPU _Process only; bounded authored 6-creature fixture is not a validated save; not rendered FPS"});
            }
            _speedTrace=null;
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,JsonSerializer.Serialize(new{status="passed",assertions,renderer=DisplayServer.GetName(),results},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("COURTYARD_SPEED_CHECK: PASS "+assertions);GetTree().Quit();
        }
        catch(Exception e)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,JsonSerializer.Serialize(new{status="failed",assertions,error=e.ToString(),results},new JsonSerializerOptions{WriteIndented=true}));
            GD.PushError(e.ToString());GetTree().Quit(1);
        }
    }
}
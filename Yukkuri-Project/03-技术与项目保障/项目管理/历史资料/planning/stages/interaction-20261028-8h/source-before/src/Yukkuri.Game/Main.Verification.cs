using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class Main
{
    private readonly List<double> _frameTimes=new();
    private readonly List<double> _processTimes=new();
    private ulong _previousFrameTicks;
    private double _benchmarkElapsed;
    private bool _benchmarkDone;
    private string EvidenceDirectory=>Path.GetDirectoryName(Path.GetFullPath(_smokeOutput??"artifacts/integration.png"))!;
    private void CollectBenchmark(double delta)
    {
        if(_population<50||_verification||_smokeOutput==null||_benchmarkDone)return;
        ulong ticks=Time.GetTicksUsec();
        if(_previousFrameTicks==0){_previousFrameTicks=ticks;return;}
        double wallDelta=(ticks-_previousFrameTicks)/1000000.0;_previousFrameTicks=ticks;
        _benchmarkElapsed+=wallDelta;
        if(_benchmarkElapsed>2){_frameTimes.Add(wallDelta*1000);_processTimes.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000);}
        if(_benchmarkElapsed<32)return;
        _benchmarkDone=true;
        var sorted=_frameTimes.Order().ToArray();
        var report=new { population=Actors.Count, totalPopulation=Sim.State.Creatures.Count, seconds=_benchmarkElapsed-2, frames=sorted.Length,
            meanFrameMs=sorted.Average(),p50FrameMs=sorted[sorted.Length/2],p95FrameMs=sorted[(int)(sorted.Length*.95)],
            fps=1000/sorted.Average(),meanEngineProcessMs=_processTimes.Average(),processMetricNote="Godot TimeProcess includes rendering/wait; not isolated script CPU cost",drawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),gpu=RenderingServer.GetVideoAdapterName(),renderer="Compatibility",resolution="1440x900",vsync="Enabled; launched hidden; wall-clock frame intervals",scope="Basic movement + needs + staff + UI; not final game workload" };
        Directory.CreateDirectory(EvidenceDirectory);File.WriteAllText(Path.Combine(EvidenceDirectory,$"benchmark-{_population}.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        GD.Print("BENCHMARK_READY: "+JsonSerializer.Serialize(report));CaptureAndQuit();
    }
    private async Task Wait(double seconds=.2)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Assert(bool condition,string description)
    {if(!condition)throw new InvalidOperationException("INTEGRATION_FAILED: "+description);GD.Print("INTEGRATION_PASS: "+description);}
    private void Mouse(Vector2 position,bool down)
    {
        // ParseInputEvent receives window coordinates, while controls/camera return viewport coordinates.
        position=GetViewport().GetFinalTransform()*position;
        Input.ParseInputEvent(new InputEventMouseMotion{Position=position,GlobalPosition=position});
        Input.ParseInputEvent(new InputEventMouseButton{Position=position,GlobalPosition=position,ButtonIndex=MouseButton.Left,Pressed=down});
    }
    private async Task Click(Vector2 position){Mouse(position,true);await Wait(.05);Mouse(position,false);await Wait(.1);}
    private async Task ClickButton(string id)
    {
        await ClickVisibleButton(Buttons[id]);
    }
    private async Task ClickVisibleButton(Button button)
    {
        for(Node? ancestor=button.GetParent();ancestor!=null;ancestor=ancestor.GetParent())
            if(ancestor is ScrollContainer scroll){scroll.EnsureControlVisible(button);await Wait(.12);}
        await Click(button.GetGlobalRect().GetCenter());
    }
    private async Task Screenshot(string name)
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Directory.CreateDirectory(EvidenceDirectory);var path=Path.Combine(EvidenceDirectory,name+".png");
        var err=GetViewport().GetTexture().GetImage().SavePng(path);Assert(err==Error.Ok,"screenshot "+name);
    }
    private async void StartIntegration()
    {
        try
        {
            await Wait(1.5);
            await Screenshot("framework-home");
            var candidate=Sim.State.Creatures.Where(c=>c.RegionId=="home").MinBy(c=>c.X*c.X+c.Z*c.Z)!;
            await Click(Camera.UnprojectPosition(Actors[candidate.Id].GlobalPosition));
            Assert(Sim.State.SelectedId==candidate.Id,"mouse selects an actual creature");
            await ClickButton("observe");Assert(candidate.Observation.Samples>0,"observation UI writes evidence");
            await ClickButton("close");await Wait(1);
            Assert(Sim.State.Mode==PlayMode.CloseUp,"UI enters close-up");
            double frozenTime=Sim.State.WorldSeconds;ulong rng=Sim.State.RandomState;
            var actor=Actors[candidate.Id];var before=actor.GlobalPosition;
            var screen=Camera.UnprojectPosition(before);
            Mouse(screen,true);await Wait(.1);
            Input.ParseInputEvent(new InputEventMouseMotion{Position=screen+new Vector2(100,-35),GlobalPosition=screen+new Vector2(100,-35),ButtonMask=MouseButtonMask.Left});
            await Wait(.7);Mouse(screen+new Vector2(100,-35),false);await Wait(.8);
            Assert(actor.GlobalPosition.DistanceTo(before)>.25f,"spring grab physically moves the body");
            Assert(actor.GlobalPosition.Y>.3f,"released body collides with ground");
            int food=Sim.State.Food;
            await ClickButton("feed");await Click(Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(0,0,.45f)));
            Assert(Sim.State.Food==food-1,"mouth contact consumes exactly one food");
            Assert(Sim.State.WorldSeconds==frozenTime&&Sim.State.RandomState==rng,"close-up including drag/feed freezes passive world and RNG");
            await Screenshot("framework-closeup");
            await ClickButton("close");await Wait(.5);
            Assert(Sim.Selected!.Id==candidate.Id&&Sim.State.WorldSeconds>frozenTime&&Sim.State.WorldSeconds<frozenTime+2,"exit retains identity without time catch-up");
            await ClickButton("feed");await ClickButton("observe");int funds=Sim.State.Money;
            await ClickButton("commission");Assert(Sim.State.Money==funds+40,"care commission UI completes earn-and-spend loop");
            await ClickButton("commission");Assert(Sim.State.Money==funds+40,"UI cannot duplicate the same reward");
            int count=Sim.ActiveRegion.Facilities.Count;
            await ClickButton("feeder");await Click(Camera.UnprojectPosition(new Vector3(3,0,3)));
            Assert(Sim.ActiveRegion.Facilities.Count==count+1,"mouse placement creates facility");
            await ClickButton("refill");
            Assert(Sim.ActiveRegion.Facilities.Any(f=>f.Kind==FacilityKind.Feeder&&f.X==3&&f.Z==3&&f.Stock>0),"new feeder receives inventory");
            await ClickButton("human");double px=Sim.State.PlayerX,pz=Sim.State.PlayerZ;
            Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.W,Pressed=true});await Wait(.5);
            Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.W,Pressed=false});
            Assert(WorldSimulation.Distance(px,pz,Sim.State.PlayerX,Sim.State.PlayerZ)>.5,"WASD moves visible human");
            await Screenshot("framework-human");await ClickButton("human");
            await ClickButton("save");var saved=_saves.Load(1).State;
            Assert(saved.Creatures.Any(c=>c.Id==candidate.Id),"manual save contains persistent creature");
            double age=candidate.InnerAgeSeconds;
            await ClickButton("wild");await Wait(.6);
            Assert(Sim.State.ActiveRegionId=="wild"&&candidate.InnerAgeSeconds>age,"travel preserves and advances inactive region");
            await Screenshot("framework-wild");
            string wildId=Sim.Selected!.Id;await ClickButton("observe");await ClickButton("rescue");
            Assert(Sim.State.Creatures.Single(c=>c.Id==wildId).Quarantine,"rescue UI sends same individual to observation area");
            await ClickButton("home");await ClickButton("load");
            Assert(Sim.State.Creatures.Single(c=>c.Id==wildId).RegionId=="wild","load restores prior region membership");
            Assert(Sim.State.WorldSeconds>=saved.WorldSeconds&&Sim.State.WorldSeconds<saved.WorldSeconds+1,"load has no offline or catch-up simulation");
            await ClickButton("guide");await Screenshot("framework-guide");await ClickButton("dismiss");
            Assert(_music.Playing&&_music.Stream!=null,"region music is loaded and playing");
            File.WriteAllText(Path.Combine(EvidenceDirectory,"integration-result.json"),JsonSerializer.Serialize(new{success=true,driver="Godot Input.ParseInputEvent through actual GUI and game input, not OS mouse",tested="select/observe/close-up/physics grab/feed/build/refill/human/travel/rescue/save/load/audio",worldSeconds=Sim.State.WorldSeconds},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("INTEGRATION_COMPLETE");await Screenshot("framework-final");QuitGameCleanly();
        }
        catch(Exception e){GD.PushError(e.ToString());await Screenshot("framework-failure");QuitGameCleanly(4);}
    }
}

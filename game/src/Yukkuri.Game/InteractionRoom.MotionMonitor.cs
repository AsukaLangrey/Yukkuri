using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Yukkuri.Simulation;
public partial class InteractionRoom
{
    private Action<float>? _controlledMotionProbe;
    private void StartMotionMonitor(string[] args)
    {
        int flag=Array.IndexOf(args,"--motion-monitor-output");
        if(flag>=0&&flag+1<args.Length)_=MonitorMotion(Path.GetFullPath(args[flag+1]));
    }
    private async Task MonitorMotion(string directory)
    {
        Directory.CreateDirectory(directory);_autoClock=-1000000;_muted=true;
        var summary=new List<object>();int frame=0,events=0;
        using var log=new StreamWriter(Path.Combine(directory,"frames.csv"));
        log.WriteLine("frame,case,speed,real_s,world_s,id,logical_x,logical_z,node_x,node_z,physics_x,physics_z,display_x,display_z,logical_step,display_step,lag,pose,falling,carried");
        var ring=new Queue<(int Frame,byte[] Png)>();int recordUntil=-1;
        try
        {
            await RenderFrames(15);
            foreach(int speed in WorldSimulation.CourtyardSpeeds)
            foreach(string scenario in new[]{"walk","crowd","landing","head-on","pose-switch"})
            {
                CancelGesture();CloseActivityPanel();Sim=WorldSimulation.CreateCourtyardEconomy();Sim.SetCourtyardSpeed(speed);
                Sim.State.Shelters.Clear();Sim.State.RoomObjects.Clear();Sim.Config.WildGrassEnabled=false;
                for(int k=0;k<Sim.State.Creatures.Count;k++)
                {
                    var c=Sim.State.Creatures[k];c.X=scenario is "walk" or "pose-switch"?-3+k*2:scenario=="head-on"?-2+k*2:-.9+k*.9;c.Z=2;
                    c.TargetX=scenario is "walk" or "pose-switch"?c.X+4:scenario=="head-on"?-c.X:0;c.TargetZ=scenario is "walk" or "pose-switch"?2:2.1;
                    c.Fullness=c.Interaction.Hydration=95;c.Family.Warmth=80;c.Interaction.NextAutonomyAt=1e9;
                    c.Family.NextSocialAt=c.Family.NextBreedingDecisionAt=1e9;
                }
                if(scenario=="landing")
                {var a=Sim.State.Creatures[0];var b=Sim.State.Creatures[1];a.X=b.X;a.Z=b.Z;a.Interaction.Height=.2;a.Interaction.Falling=true;}
                _controlledMotionProbe=null;Sim.State.Settings.Pause=true;BuildActors();await RenderFrames(8);
                // Controlled traffic uses the real collision-aware RoomMove; autonomous choices cannot replace its target.
                if(scenario!="landing")
                {
                    var move=typeof(WorldSimulation).GetMethod("RoomMove",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
                    double remainder=0;_controlledMotionProbe=dt=>{remainder+=dt*Sim.CourtyardSpeed;while(remainder>=Sim.Config.TickSeconds){remainder-=Sim.Config.TickSeconds;Sim.State.WorldSeconds+=Sim.Config.TickSeconds;foreach(var c in Sim.State.Creatures)move.Invoke(Sim,new object[]{c,c.TargetX,c.TargetZ,Sim.Config.TickSeconds,true});}};
                }
                Sim.State.Settings.Pause=false;
                var previous=new Dictionary<string,(Vector2 Logic,Vector2 Display)>();
                ulong started=Time.GetTicksMsec();double maxLag=0,maxStep=0;int anomalyCount=0;
                ring.Clear();recordUntil=-1;
                while(Time.GetTicksMsec()-started<2200)
                {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    frame++;bool anomaly=false;
                    if(scenario=="pose-switch"){var life=Sim.State.Creatures[0].Interaction;bool active=Time.GetTicksMsec()-started is >700 and <1300;life.ReactionPose=active?"stroke":"idle";life.ReactionPoseRemaining=active?1:0;}
                    foreach(var c in Sim.State.Creatures)
                    {
                        if(!_actors.TryGetValue(c.Id,out var a)||!a.Visible)continue;
                        var logical=new Vector2((float)c.X,(float)c.Z);var world=a.Position+a.VisualMotionOffset;
                        var body=PhysicsServer3D.BodyGetState(a.GetRid(),PhysicsServer3D.BodyState.Transform).AsTransform3D().Origin;
                        var shown=new Vector2(world.X,world.Z);var before=previous.GetValueOrDefault(c.Id,(Logic:logical,Display:shown));
                        float ls=logical.DistanceTo(before.Logic),ds=shown.DistanceTo(before.Display),lag=logical.DistanceTo(shown);
                        maxLag=Math.Max(maxLag,lag);maxStep=Math.Max(maxStep,ds);
                        bool suspect=lag>.65||ds>.35&&ds>ls*2+.12||ls>.4;
                        anomaly|=suspect;previous[c.Id]=(logical,shown);
                        log.WriteLine(FormattableString.Invariant($"{frame},{scenario},{speed},{(Time.GetTicksMsec()-started)/1000.0:F4},{Sim.State.WorldSeconds:F4},{c.Id},{c.X:F5},{c.Z:F5},{a.Position.X:F5},{a.Position.Z:F5},{body.X:F5},{body.Z:F5},{world.X:F5},{world.Z:F5},{ls:F5},{ds:F5},{lag:F5},{a.PoseTag},{c.Interaction.Falling},{c.Interaction.Carried}"));
                    }
                    using var picture=GetViewport().GetTexture().GetImage();var png=picture.SavePngToBuffer();
                    ring.Enqueue((frame,png));while(ring.Count>8)ring.Dequeue();
                    if(anomaly&&frame>recordUntil){events++;anomalyCount++;recordUntil=frame+8;foreach(var item in ring)File.WriteAllBytes(Path.Combine(directory,$"event-{events:000}-frame-{item.Frame:00000}.png"),item.Png);}
                    else if(frame<=recordUntil)File.WriteAllBytes(Path.Combine(directory,$"event-{events:000}-frame-{frame:00000}.png"),png);
                    log.Flush();
                    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                }
                summary.Add(new{scenario,speed,realSeconds=(Time.GetTicksMsec()-started)/1000.0,worldSeconds=Sim.State.WorldSeconds,maxLag,maxStep,anomalyCount});
            }
            File.WriteAllText(Path.Combine(directory,"summary.json"),JsonSerializer.Serialize(new{scope="25 warmed rendered scenarios. Walk/crowd/head-on/pose-switch drive the real private RoomMove at fixed simulation ticks with controlled targets; landing uses normal simulation. Not full autonomous play, performance benchmark or long-term stability.",frame,events,physicsTicks=Engine.PhysicsTicksPerSecond,results=summary},new JsonSerializerOptions{WriteIndented=true}));
            GetTree().Quit();
        }
        catch(Exception e){File.WriteAllText(Path.Combine(directory,"failure.txt"),e.ToString());GetTree().Quit(2);}
    }
}

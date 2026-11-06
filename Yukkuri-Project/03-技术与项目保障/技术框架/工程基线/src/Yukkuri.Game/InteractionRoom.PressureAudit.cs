using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;
public partial class InteractionRoom
{
    private static void GuardPressureAudit(string[] args)
    {
        const string flag="--pressure-boundary-audit-output";
        if(!args.Any(x=>x.StartsWith("--pressure-boundary-audit",StringComparison.Ordinal)))return;
        int p=Array.IndexOf(args,"--interaction-test-profile"),o=Array.IndexOf(args,flag);
        if(DisplayServer.GetName()!="headless"||args.Length!=4||p<0||o<0||p+1>=args.Length||o+1>=args.Length||
            args.Count(x=>x==flag)!=1||args.Count(x=>x=="--interaction-test-profile")!=1||
            !System.Text.RegularExpressions.Regex.IsMatch(args[p+1],@"\Apressure-audit-[a-zA-Z0-9_-]{1,40}\z")||!Path.IsPathFullyQualified(args[o+1]))
            throw new ArgumentException("Pressure audit requires headless-only exact flags, fresh profile and absolute output.");
        string save=ProjectSettings.GlobalizePath("user://interaction-test-saves/"+args[p+1]);
        string output=Path.GetFullPath(args[o+1]);string dir=Path.GetDirectoryName(output)!;
        string allowed=Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../.local/testing"))+Path.DirectorySeparatorChar;
        if(Directory.Exists(save)||File.Exists(save)||Directory.Exists(dir)||File.Exists(output)||!dir.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fresh isolated profile and output directory required before world and SaveStore.");
        Directory.CreateDirectory(dir);Directory.CreateDirectory(save);
        File.WriteAllText(Path.Combine(dir,"guard.json"),JsonSerializer.Serialize(new{save,output,beforeWorldAndSaveStore=true,renderer=DisplayServer.GetName()}));
        GD.Print("PRESSURE_AUDIT_GUARD_OK");
    }
    private void StartPressureAudit(string[] args)
    {if(!args.Contains("--pressure-boundary-audit-output"))return;SetProcess(false);_autoClock=-10000;SetRoomMuted(true);_=RunPressureAudit(args[Array.IndexOf(args,"--pressure-boundary-audit-output")+1]);}
    private async Task RunPressureAudit(string output)
    {
        var results=new List<object>();int failures=0;
        void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        void Check(string name,bool ok,object detail){results.Add(new{name,passed=ok,detail});if(!ok)failures++;GD.Print((ok?"PASS ":"FAIL ")+name);File.WriteAllText(output,JsonSerializer.Serialize(new{failures,results},new JsonSerializerOptions{WriteIndented=true}));}
        void Button(Node root,string label){var b=root.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==label&&b.IsVisibleInTree());Need(!b.Disabled,"disabled "+label);b.EmitSignal(BaseButton.SignalName.Pressed);}
        Vector2 FindLowerSide(string id)
        {
            var center=_camera.UnprojectPosition(_actors[id].ContactPoint(BodyPart.Side));
            for(int radius=0;radius<130;radius+=2)for(int x=-radius;x<=radius;x+=2)for(int y=-radius;y<=radius;y+=2)
            {if(Math.Max(Math.Abs(x),Math.Abs(y))!=radius)continue;var p=center+new Vector2(x,y);var hit=ContactAt(p);if(IsScenePoint(p)&&hit is {Blocked:false,TargetKind:InteractionTargetKind.Body,Part:BodyPart.Side}&&hit.Id==id&&ObjectAt(p)==null&&_actors[id].ContactCoordinate(hit.World,hit.Part).Y<=ActorFaceSurface.CenterY&&IsScenePoint(p-new Vector2(0,50)))return p;}
            throw new InvalidOperationException("No geometrically visible lower Side for "+id);
        }
        void Move(Vector2 p,Vector2 previous)
        {Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p,Relative=p-previous,ButtonMask=MouseButtonMask.Left});Need(GetViewport().GetMousePosition().DistanceTo(p)<1,"engine pointer mismatch");}
        void Sample(){ProcessRedesignGestures(.016f);ProcessRoomGesture(.016f,.016);}
        void Release(Vector2 p)
        {Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=p,GlobalPosition=p});_Input(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=p,GlobalPosition=p});}
        async Task Fresh()
        {
            Release(new Vector2(20,200));CancelGesture();Sim=WorldSimulation.CreateCourtyardEconomy();ResetRoomUiSession();BuildActors();DisarmTool();_windowFocused=true;_recovered=false;_yardFocus=Vector3.Zero;_zoom=16;
            UpdateRoomCamera(1);foreach(var creature in Sim.State.Creatures){var actor=_actors[creature.Id];actor.Visible=HasVisibleFamilyBody(creature);actor.RefreshInteraction(creature,creature.Id==Sim.State.SelectedId,false,0,InteractionPresentation.Pose(creature),_force,Vector2.Zero,true,SurfaceSide.Whole,Sim.State.RoomObjects);}RefreshRoomUi();await RenderFrames(3);
            Button(_groupList,"空手");Button(_choiceList,"按压");await RenderFrames(2);
            Need(!Sim.State.Settings.Pause&&Sim.State.Mode==PlayMode.Management,"fixture must remain normal yard");
        }
        try
        {
            Input.UseAccumulatedInput=false;await RenderFrames(3);
            foreach(string mode in new[]{"in-scene-upward-control","left-sidebar-no-flip","right-sidebar-no-flip","outside-release-control","reentry-observation","focus-loss-control"})
            {
                await Fresh();var c=Sim.Selected!;Need(!c.Interaction.Supine&&c.Interaction.Restraint==RestraintKind.None,"ordinary upright free fixture");
                Vector2 start=FindLowerSide(c.Id);var pressHit=ContactAt(start)!;var local=_actors[c.Id].ContactCoordinate(pressHit.World,pressHit.Part);
                Move(start,start);Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=start,GlobalPosition=start});
                _UnhandledInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=start,GlobalPosition=start});
                Need(Input.IsMouseButtonPressed(MouseButton.Left)&&_mouseHeld&&Sim.HasInteractionGesture&&_pressureOrigin?.Id==c.Id&&_action==InteractionAction.Squeeze,"real lower-side press failed to arm pressure gesture");
                var samples=new List<object>();
                void Record(string stage){var point=GetViewport().GetMousePosition();samples.Add(new{stage,point=new[]{point.X,point.Y},scene=IsScenePoint(point),c.Interaction.Supine,c.Interaction.SupineRemaining,c.Interaction.ReactionPose,c.ReactionSerial,_mouseHeld,gesture=Sim.HasInteractionGesture,origin=_pressureOrigin?.Id});}
                Record("pressed");Vector2 below=start-new Vector2(0,20);Move(below,start);Sample();Record("below-threshold");bool noEarlyFlip=!c.Interaction.Supine;
                Vector2 outside=new(mode=="right-sidebar-no-flip"?GetViewport().GetVisibleRect().Size.X-30:30,start.Y),up=start-new Vector2(0,50);
                if(mode!="in-scene-upward-control")
                {Move(outside,below);Sample();Record("horizontal-exit");Need(!IsScenePoint(outside),"sidebar coordinate unexpectedly inside scene");Need(!c.Interaction.Supine,"unexpected horizontal flip");}
                if(mode=="focus-loss-control"){GetWindow().EmitSignal(Window.SignalName.FocusExited);Record("focus-lost");Check(mode,noEarlyFlip&&!c.Interaction.Supine&&!_mouseHeld&&!Sim.HasInteractionGesture&&_pressureOrigin==null,new{samples});continue;}
                if(mode=="outside-release-control")
                {Release(outside);Record("released-outside");Check(mode,noEarlyFlip&&!c.Interaction.Supine&&!_mouseHeld&&!Sim.HasInteractionGesture&&_pressureOrigin==null,new{samples,pressTarget=pressHit.Id,pressPart=pressHit.Part.ToString(),localY=local.Y});continue;}
                if(mode=="left-sidebar-no-flip"||mode=="right-sidebar-no-flip")up=new Vector2(outside.X,start.Y-50);
                if(mode=="reentry-observation"){Move(start,outside);Sample();Record("reentered-below-threshold");}
                Move(up,mode=="in-scene-upward-control"?below:mode=="reentry-observation"?start:outside);Sample();Record("above-threshold");
                bool observed=c.Interaction.Supine&&c.Interaction.SupineRemaining>0&&c.Interaction.ReactionPose=="flip";
                bool sidebar=mode=="left-sidebar-no-flip"||mode=="right-sidebar-no-flip";
                Check(mode,noEarlyFlip&&(sidebar?!observed&& !c.Interaction.Supine:observed),new{observedFlip=observed,observedOffSceneDefect=sidebar&&observed&&!IsScenePoint(up),samples,pressTarget=pressHit.Id,pressPart=pressHit.Part.ToString(),localY=local.Y});Release(up);
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{scope="Frozen ordinary courtyard economy fixture; real Godot scene callbacks and actual ray geometry with synthetic engine pointer/button state. ProcessRedesignGestures then ProcessRoomGesture sampled in product order without autonomous simulation ticks. Not native input, event propagation or visual acceptance. Sidebar assertions require no flip; baseline defects are reported as failures.",success=failures==0,failures,results},new JsonSerializerOptions{WriteIndented=true}));GetTree().Quit(failures==0?0:1);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"error.txt"),e.ToString());GD.PushError(e.ToString());GetTree().Quit(2);}
    }
}

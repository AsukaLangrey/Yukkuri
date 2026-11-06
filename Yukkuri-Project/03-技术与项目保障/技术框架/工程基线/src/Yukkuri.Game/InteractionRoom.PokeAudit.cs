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
    private static void GuardPokeAudit(string[] args)
    {
        const string flag="--poke-contact-audit-output";
        if(!args.Any(x=>x.StartsWith("--poke-contact-audit",StringComparison.Ordinal)))return;
        int p=Array.IndexOf(args,"--interaction-test-profile"),o=Array.IndexOf(args,flag);
        if(DisplayServer.GetName()!="headless"||args.Length!=4||p<0||o<0||p+1>=args.Length||o+1>=args.Length||
            args.Count(x=>x==flag)!=1||args.Count(x=>x=="--interaction-test-profile")!=1||
            !System.Text.RegularExpressions.Regex.IsMatch(args[p+1],@"\Apoke-audit-[a-zA-Z0-9_-]{1,40}\z")||!Path.IsPathFullyQualified(args[o+1]))
            throw new ArgumentException("Poke audit requires headless-only exact flags, fresh profile and absolute output.");
        string save=ProjectSettings.GlobalizePath("user://interaction-test-saves/"+args[p+1]);
        string output=Path.GetFullPath(args[o+1]);string dir=Path.GetDirectoryName(output)!;
        string allowed=Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../.local/testing"))+Path.DirectorySeparatorChar;
        if(Directory.Exists(save)||File.Exists(save)||Directory.Exists(dir)||File.Exists(output)||!dir.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fresh isolated profile and output directory required before world and SaveStore.");
        Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"guard.json"),JsonSerializer.Serialize(new{save,output,beforeWorldAndSaveStore=true,renderer=DisplayServer.GetName()}));
        GD.Print("POKE_AUDIT_GUARD_OK");
    }
    private void StartPokeAudit(string[] args)
    {if(!args.Contains("--poke-contact-audit-output"))return;SetProcess(false);_autoClock=-10000;SetRoomMuted(true);_=RunPokeAudit(args[Array.IndexOf(args,"--poke-contact-audit-output")+1]);}
    private async Task RunPokeAudit(string output)
    {
        var results=new List<object>();int failures=0;
        void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        void Check(string name,bool ok,object detail){results.Add(new{name,passed=ok,detail});if(!ok)failures++;GD.Print((ok?"PASS ":"FAIL ")+name);File.WriteAllText(output,JsonSerializer.Serialize(new{failures,results},new JsonSerializerOptions{WriteIndented=true}));}
        void Button(Node root,string label){var b=root.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==label&&b.IsVisibleInTree());Need(!b.Disabled,"disabled "+label);b.EmitSignal(BaseButton.SignalName.Pressed);}
        Vector2 FindHead(string id, bool head=true)
        {
            var center=_camera.UnprojectPosition(_actors[id].ContactPoint(BodyPart.Head));
            for(int radius=0;radius<100;radius+=3)for(int x=-radius;x<=radius;x+=3)for(int y=-radius;y<=radius;y+=3)
            {if(Math.Max(Math.Abs(x),Math.Abs(y))!=radius)continue;var p=center+new Vector2(x,y);var hit=ContactAt(p);if(IsScenePoint(p)&&hit is {Blocked:false,TargetKind:InteractionTargetKind.Body}&&(head?hit.Part==BodyPart.Head:hit.Part==BodyPart.Cheek||hit.Part==BodyPart.Side)&&hit.Id==id&&ObjectAt(p)==null)return p;}
            throw new InvalidOperationException("No geometrically visible head for "+id);
        }
        Vector2 FindEmpty()
        {var rect=GameUiLayout.Canvas(_ui);for(float x=rect.Position.X+30;x<rect.End.X-30;x+=25)for(float y=rect.Position.Y+30;y<rect.End.Y-30;y+=25){var p=new Vector2(x,y);if(IsScenePoint(p)&&ContactAt(p)==null&&ObjectAt(p)==null)return p;}throw new InvalidOperationException("No empty canvas");}
        async Task Fresh()
        {
            CancelGesture();Sim=WorldSimulation.CreateCourtyardEconomy();ResetRoomUiSession();BuildActors();DisarmTool();_windowFocused=true;_recovered=false;_yardFocus=Vector3.Zero;_zoom=16;
            UpdateRoomCamera(1);foreach(var creature in Sim.State.Creatures){var actor=_actors[creature.Id];actor.Visible=HasVisibleFamilyBody(creature);actor.RefreshInteraction(creature,creature.Id==Sim.State.SelectedId,false,0,InteractionPresentation.Pose(creature),_force,Vector2.Zero,true,SurfaceSide.Whole,Sim.State.RoomObjects);}RefreshRoomUi();await RenderFrames(3);
            Button(_groupList,"空手");Button(_choiceList,"戳碰");await RenderFrames(2);
            Need(!Sim.State.Settings.Pause&&Sim.State.Mode==PlayMode.Management,"fixture must remain normal yard");
        }
        try
        {
            await RenderFrames(3);
            foreach(string mode in new[]{"valid-head-click","quick-drag-off","release-other-oil","release-outside-canvas","same-actor-new-surface","stationary-pointer-actor-moved"})
            {
                await Fresh();var c=Sim.Selected!;var other=Sim.State.Creatures.First(x=>x.Id!=c.Id);
                Vector2 from=FindHead(c.Id),to=mode is "valid-head-click" or "stationary-pointer-actor-moved"?from:mode=="same-actor-new-surface"?FindHead(c.Id,false):mode=="release-other-oil"?FindHead(other.Id):mode=="release-outside-canvas"?new Vector2(-10,-10):FindEmpty();
                var before=new{c.Health,c.Pain,c.ReactionSerial,otherHealth=other.Health,otherSerial=other.ReactionSerial};
                var pressHit=ContactAt(from);
                _UnhandledInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=from,GlobalPosition=from});
                Need(_pokeContact!=null&&!_pulled,"real press callback did not create pending poke");
                if(mode=="stationary-pointer-actor-moved")
                {
                    c.X+=4;
                    _actors[c.Id].RefreshInteraction(c,true,false,0,InteractionPresentation.Pose(c),_force,Vector2.Zero,true,SurfaceSide.Whole,Sim.State.RoomObjects);
                    Need(ContactAt(to)?.Id!=c.Id,"moved actor still under pointer");
                }
                var releaseHit=ContactAt(to);
                // No simulation tick or artificial Contact object. The moving fixture changes the
                // simulation position and refreshes the actual actor before re-casting the ray.
                _Input(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=to,GlobalPosition=to});
                bool hit=c.ReactionSerial!=before.ReactionSerial||c.Health!=before.Health||c.Pain!=before.Pain;
                bool expectedHit=mode is "valid-head-click" or "same-actor-new-surface";
                bool currentSurface=!expectedHit||_tapContact==releaseHit;
                Check(mode,(expectedHit?hit:!hit)&&currentSurface&&other.Health==before.otherHealth&&(expectedHit||other.ReactionSerial==before.otherSerial)&&_pokeContact==null,new{from=new[]{from.X,from.Y},to=new[]{to.X,to.Y},pressTarget=pressHit?.Id,pressPart=pressHit?.Part.ToString(),releaseTarget=releaseHit?.Id,releasePart=releaseHit?.Part.ToString(),before,after=new{c.Health,c.Pain,c.ReactionSerial,pose=c.Interaction.ReactionPose,otherHealth=other.Health,otherSerial=other.ReactionSerial},currentSurface,cachedCleared=_pokeContact==null});
            }
            // Engine input state is synthetic; the actual gesture processor, ray tests and
            // simulation pull/sweep code run unchanged. No pending-gesture fields are fabricated.
            foreach(bool pull in new[]{true,false})
            {
                await Fresh();var c=Sim.Selected!;Vector2 head=FindHead(c.Id),empty=FindEmpty();
                // Nearby empty start avoids sweeping through a different oil first.
                if(!pull)
                {
                    bool found=false;
                    for(int x=30;x<=120&&!found;x+=3)foreach(int sign in new[]{-1,1})
                    {var point=head+new Vector2(x*sign,0);if(IsScenePoint(point)&&ContactAt(point)==null&&ObjectAt(point)==null){empty=point;found=true;break;}}
                    Need(found,"no nearby empty sweep start");
                }
                Vector2 from=pull?head:empty,to=pull?empty:head;
                Input.UseAccumulatedInput=false;
                Input.ParseInputEvent(new InputEventMouseMotion{Position=from,GlobalPosition=from});
                Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=from,GlobalPosition=from});
                _UnhandledInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=from,GlobalPosition=from});
                Need(Input.IsMouseButtonPressed(MouseButton.Left),"synthetic engine button state absent");
                Need(pull?_pokeContact!=null:_swingArmed,"real press did not arm expected gesture");
                double health=c.Health,pain=c.Pain;int serial=c.ReactionSerial;
                Input.ParseInputEvent(new InputEventMouseMotion{Position=to,GlobalPosition=to,Relative=to-from,ButtonMask=MouseButtonMask.Left});
                Need(GetViewport().GetMousePosition().DistanceTo(to)<1,"synthetic engine pointer mismatch");
                ProcessRedesignGestures(pull?.4f:.11f);
                bool exercised=pull?_pulled&&c.Interaction.PullRemaining>0&&c.Interaction.PullAmount>0:
                    c.ReactionSerial!=serial&&c.Health<health&&_tapContact?.Id==c.Id;
                var beforeRelease=(c.Health,c.Pain,c.ReactionSerial);
                Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=to,GlobalPosition=to});
                _Input(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=to,GlobalPosition=to});
                Check(pull?"sustained-pull-no-release-poke":"empty-start-sweep-no-release-poke",
                    exercised&&beforeRelease==(c.Health,c.Pain,c.ReactionSerial)&&_pokeContact==null&&!_swingArmed,
                    new{exercised,c.Health,c.Pain,c.ReactionSerial,c.Interaction.PullRemaining,c.Interaction.PullAmount,tapTarget=_tapContact?.Id,from=new[]{from.X,from.Y},to=new[]{to.X,to.Y}});
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{scope="Real Godot scene input callbacks and actual current ray-contact geometry; normal-yard fixtures and explicit actor-move fixture; synthetic engine input state for pull/sweep; not native input, viewport propagation or visual acceptance.",success=failures==0,failures,results},new JsonSerializerOptions{WriteIndented=true}));
            GetTree().Quit(failures==0?0:1);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"error.txt"),e.ToString());GD.PushError(e.ToString());GetTree().Quit(2);}
    }
}

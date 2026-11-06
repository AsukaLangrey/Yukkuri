using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Shared by both entry scenes, before world creation, legacy routing, or SaveStore.
    internal static void GuardGrabSupplyDiagnostic(string[] args,bool roomEntry=true)
    {
        const string flag="--grab-supply-check-output",profileFlag="--interaction-test-profile";
        if(!args.Any(a=>a.StartsWith("--grab-supply",StringComparison.Ordinal)))return;
        try
        {
            int i=Array.IndexOf(args,flag),p=Array.IndexOf(args,profileFlag);
            if(!roomEntry||args.Length!=4||args.Count(a=>a==flag)!=1||args.Count(a=>a==profileFlag)!=1||i<0||i+1>=args.Length||p<0||p+1>=args.Length||
                !Path.IsPathFullyQualified(args[i+1])||!Regex.IsMatch(args[p+1],@"\A[a-zA-Z0-9_-]{1,64}\z")||DisplayServer.GetName()!="headless")
                throw new ArgumentException("Grab diagnostic requires only one absolute output and one fresh isolated profile at the headless room entry.");
            string root=Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../.local/testing"))+Path.DirectorySeparatorChar;
            string output=Path.GetFullPath(args[i+1]),dir=Path.GetDirectoryName(output)!;
            string save=Path.GetFullPath(ProjectSettings.GlobalizePath("user://interaction-test-saves/"+args[p+1]));
            string parent=Path.GetFullPath(ProjectSettings.GlobalizePath("user://interaction-test-saves"));
            string app=Path.GetFullPath(System.Environment.GetEnvironmentVariable("APPDATA")??"");
            foreach(string name in new[]{"APPDATA","LOCALAPPDATA","TEMP","USERPROFILE","DOTNET_CLI_HOME"})
            {
                string value=System.Environment.GetEnvironmentVariable(name)??"";
                if(!Path.IsPathFullyQualified(value)||!Path.GetFullPath(value).StartsWith(root,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Diagnostic environment must be isolated under .local/testing: "+name);
            }
            if(!dir.StartsWith(root,StringComparison.OrdinalIgnoreCase)||File.Exists(output)||Directory.Exists(dir)||Directory.Exists(save)||
                !string.Equals(Path.GetDirectoryName(save),parent,StringComparison.OrdinalIgnoreCase)||!save.StartsWith(app+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fresh output/profile under isolated APPDATA required; earlier evidence is preserved.");
            GD.Print("GRAB_SUPPLY_GUARD_BEFORE_WORLD");
        }
        catch
        {
            GD.Print("GRAB_SUPPLY_GUARD_REJECTED_BEFORE_WORLD");
            throw;
        }
    }
    private void StartGrabSupplyDiagnostic(string[] args)
    {
        int i=Array.IndexOf(args,"--grab-supply-check-output");if(i>=0)_=VerifyGrabSupplyDiagnostic(args[i+1]);
    }
    private async Task VerifyGrabSupplyDiagnostic(string output)
    {
        var results=new List<object>();var checkpoints=new List<object>();int failures=0,fixtureFailures=0;
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        void Persist()=>File.WriteAllText(output,JsonSerializer.Serialize(new{success=failures==0&&fixtureFailures==0,failures,fixtureFailures,
            scope="Headless fresh courtyard. Actual toolbar signals and projected _UnhandledInput presses. _Input Space/right click/release. Input.ParseInputEvent for normal release pointer synchronization, verified explicitly. No native OS input or visual acceptance. Focus events are emitted engine signals, not OS focus automation.",results,checkpoints},new JsonSerializerOptions{WriteIndented=true}));
        void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        object Snapshot()=>new{mode=_roomMode.ToString(),action=_action.ToString(),_toolArmed,_mouseHeld,_heldSupplyId,title=_title.Text,hint=_hint.Text,message=_message,
            food=Sim.State.Food,water=Sim.State.StationWaterPortions,paused=Sim.State.Settings.Pause,worldSeconds=Sim.State.WorldSeconds,
            motions=Sim.State.PortableMotions.Select(m=>new{m.Id,m.Held,m.Height}).ToArray(),
            objects=Sim.State.RoomObjects.Select(o=>new{o.Id,kind=o.Kind.ToString(),o.Portions,o.HeldByHand,o.Stored,o.X,o.Z,o.Height}).ToArray()};
        void Record(string name){checkpoints.Add(new{name,state=Snapshot()});Persist();}
        void Check(string name,bool ok){if(!ok)failures++;results.Add(new{name,passed=ok,state=Snapshot()});GD.Print((ok?"PASS ":"FAIL ")+name);Persist();}
        void Click(Node root,string label)
        {
            var b=root.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==label);
            Need(!b.Disabled,"Disabled button "+label);b.EmitSignal(BaseButton.SignalName.Pressed);
        }
        void Press(Vector2 p)=>_UnhandledInput(new InputEventMouseButton{Position=p,ButtonIndex=MouseButton.Left,Pressed=true});
        void Fresh()
        {
            CloseModal();CloseActivityPanel();CancelGesture();Sim=WorldSimulation.CreateCourtyardEconomy();
            Sim.State.AutonomousBreeding=false;Sim.State.Settings.Pause=false;ResetRoomUiSession();BuildActors();DisarmTool();
            _windowFocused=true;_autoClock=-10000;UpdateRoomCamera(1);_Process(0);RefreshRoomUi();
        }
        Vector2 EmptyGround(Vector3? away=null)
        {
            for(int z=-5;z<=5;z++)for(int x=-5;x<=5;x++)
            {
                var world=new Vector3(x,0,z);if(away is {} a&&world.DistanceTo(a)<2)continue;
                var point=_camera.UnprojectPosition(world);
                if(!IsScenePoint(point)||ContactAt(point)!=null||ObjectAt(point)!=null||!TryGroundPoint(point,0,out var g)||g.DistanceTo(world)>.05f)continue;
                if(!WoodlandLayout.GroundFits(Sim.State,Sim.State.ActiveRegionId,g.X,g.Z,.4))continue;
                if(Sim.State.Creatures.Any(c=>new Vector2((float)c.X,(float)c.Z).DistanceTo(new(g.X,g.Z))<2))continue;
                if(Sim.State.RoomObjects.Any(o=>!o.Stored&&new Vector2((float)o.X,(float)o.Z).DistanceTo(new(g.X,g.Z))<1))continue;
                return point;
            }
            throw new InvalidOperationException("No empty projected dry ground within bounded grid.");
        }
        Vector2 SupplyPoint(RoomObject obj)
        {
            var center=_camera.UnprojectPosition(new((float)obj.X,(float)obj.Height+.15f,(float)obj.Z));
            for(int y=-30;y<=30;y+=2)for(int x=-30;x<=30;x+=2)
            {var p=center+new Vector2(x,y);if(IsScenePoint(p)&&ContactAt(p)==null&&ObjectAt(p)?.Id==obj.Id)return p;}
            throw new InvalidOperationException("No visible supply hit for "+obj.Id+" at "+center);
        }
        try
        {
            SetProcess(false);SetRoomMuted(true);_autoClock=-10000;await RenderFrames(3);
            // Real held-source mouth contact: no action, stock, portion, or need injection.
            foreach(var kind in new[]{RoomObjectKind.Water,RoomObjectKind.Meal,RoomObjectKind.Sweet})
            {
                string name=kind+" / held-source-feeding";
                try
                {
                    Fresh();await RenderFrames(2);
                    int Stock()=>kind==RoomObjectKind.Water?Sim.State.StationWaterPortions:Sim.State.Food;
                    int stock=Stock();var initialIds=Sim.State.RoomObjects.Select(o=>o.Id).ToHashSet();
                    Click(_groupList,"食物");Click(_choiceList,kind==RoomObjectKind.Water?"清水":kind==RoomObjectKind.Sweet?"零食":"饭饭");
                    Press(EmptyGround());_Process(0);await RenderFrames(1);
                    var source=Sim.State.RoomObjects.Single(o=>!initialIds.Contains(o.Id));
                    Need(source.Kind==kind&&source.Portions==1&&Stock()==stock-1,"Feeding fixture must use one UI-purchased portion");
                    Click(_groupList,"空手");Click(_choiceList,"抓取");
                    var pickup=SupplyPoint(source);
                    // Update engine button state without dispatching an extra press into the scene.
                    SetProcessInput(false);SetProcessUnhandledInput(false);
                    var screen=GetViewport().GetFinalTransform()*pickup;
                    Input.ParseInputEvent(new InputEventMouseButton{Position=screen,GlobalPosition=screen,ButtonIndex=MouseButton.Left,Pressed=true});
                    Input.FlushBufferedEvents();await RenderFrames(1);
                    SetProcessInput(true);SetProcessUnhandledInput(true);
                    Need(Input.IsMouseButtonPressed(MouseButton.Left),"Engine left button is not held");
                    Press(pickup);Need(_heldSupplyId==source.Id&&source.HeldByHand,"Feeding must start from actual Grab");
                    var eater=Sim.Selected!;var actor=_actors[eater.Id];
                    var center=_camera.UnprojectPosition(actor.GlobalPosition);Vector2? mouth=null;
                    for(int y=-65;y<=65&&mouth==null;y++)for(int x=-65;x<=65;x++)
                    {
                        var point=center+new Vector2(x,y);
                        if(IsScenePoint(point)&&ContactAt(point) is {Part:BodyPart.Mouth,Blocked:false,TargetKind:InteractionTargetKind.Body} hit&&hit.Id==eater.Id)
                        {mouth=point;break;}
                    }
                    Need(mouth!=null,"No actual unobstructed mouth ray hit for feeding");
                    screen=GetViewport().GetFinalTransform()*mouth!.Value;
                    Input.ParseInputEvent(new InputEventMouseMotion{Position=screen,GlobalPosition=screen,ButtonMask=MouseButtonMask.Left});
                    Input.FlushBufferedEvents();await RenderFrames(1);
                    Need(GetViewport().GetMousePosition().DistanceTo(mouth.Value)<1,"Feeding pointer did not synchronize");
                    double fullness=eater.Fullness,hydration=eater.Interaction.Hydration;
                    ProcessHeldSupply(1f/60,1.0/60);
                    Check(name+": first mouth contact preserves portion and stock",source.Portions==1&&Stock()==stock-1);
                    for(int frame=0;frame<90&&!_supplyBiteTaken;frame++)ProcessHeldSupply(1f/60,1.0/60);
                    Check(name+": held original portion feeds without inventory purchase",_supplyBiteTaken&&source.Portions==0&&Stock()==stock-1&&
                        (kind==RoomObjectKind.Water?eater.Interaction.Hydration>hydration:eater.Fullness>fullness));
                    double fedFullness=eater.Fullness,fedHydration=eater.Interaction.Hydration;
                    for(int frame=0;frame<30;frame++)ProcessHeldSupply(1f/60,1.0/60);
                    Check(name+": continued hold cannot consume a second portion",source.Portions==0&&Stock()==stock-1&&eater.Fullness==fedFullness&&eater.Interaction.Hydration==fedHydration);
                    SetProcessInput(false);SetProcessUnhandledInput(false);
                    Input.ParseInputEvent(new InputEventMouseButton{Position=screen,GlobalPosition=screen,ButtonIndex=MouseButton.Left,Pressed=false});Input.FlushBufferedEvents();await RenderFrames(1);
                    SetProcessInput(true);SetProcessUnhandledInput(true);
                    _Input(new InputEventKey{Keycode=Key.Space,Pressed=true});_Input(new InputEventKey{Keycode=Key.Space,Pressed=true});
                    Press(EmptyGround());
                    Check(name+": consumed-source cancellation restores Grab without new purchase",_action==InteractionAction.Lift&&_heldSupplyId==null&&!_mouseHeld&&Stock()==stock-1&&
                        !Sim.State.RoomObjects.Any(o=>o.Id==source.Id)&&Sim.State.RoomObjects.All(o=>initialIds.Contains(o.Id)));
                    Record(name+": completed");
                }
                catch(Exception ex){fixtureFailures++;results.Add(new{name=name+": fixture/action failure",passed=false,error=ex.ToString(),state=Snapshot()});Persist();GD.PushError(ex.ToString());}
                finally
                {
                    SetProcessInput(false);SetProcessUnhandledInput(false);
                    Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false});Input.FlushBufferedEvents();
                    SetProcessInput(true);SetProcessUnhandledInput(true);
                }
            }
            foreach(var kind in new[]{RoomObjectKind.Water,RoomObjectKind.Meal,RoomObjectKind.Sweet})
            foreach(string end in new[]{"pause-cancel","normal-release","focus-cancel","outside-release","camera-cancel","right-click-control","tool-switch-control"})
            {
                string name=kind+" / "+end;
                try
                {
                    Fresh();await RenderFrames(2);Record(name+": fresh");
                    int Stock()=>kind==RoomObjectKind.Water?Sim.State.StationWaterPortions:Sim.State.Food;
                    int initialStock=Stock();var initialIds=Sim.State.RoomObjects.Select(o=>o.Id).ToHashSet();
                    Click(_groupList,"食物");Click(_choiceList,kind==RoomObjectKind.Water?"清水":kind==RoomObjectKind.Sweet?"零食":"饭饭");
                    var source=EmptyGround();Press(source);_Process(0);RefreshRoomUi();await RenderFrames(1);
                    var created=Sim.State.RoomObjects.Where(o=>!initialIds.Contains(o.Id)).ToArray();
                    Record(name+": supply placed via UI");Need(created.Length==1&&created[0].Kind==kind&&Stock()==initialStock-1,"UI placement must create exactly one matching item and consume one stock");
                    var original=created[0];int portions=original.Portions;double originalX=original.X,originalZ=original.Z;
                    Click(_groupList,"空手");Click(_choiceList,"抓取");
                    Need(_roomMode==RoomMode.Grab&&_action==InteractionAction.Lift,"Grab button must select Lift");
                    var pickup=SupplyPoint(original);var destination=EmptyGround(new((float)original.X,0,(float)original.Z));
                    Press(pickup);Record(name+": original held");
                    Need(original.HeldByHand&&_heldSupplyId==original.Id&&_mouseHeld&&_roomMode==RoomMode.Grab,"Actual ground object press must hold original through Grab");
                    Check(name+": pickup preserves ID, portions, and stock",original.Portions==portions&&Stock()==initialStock-1&&Sim.State.RoomObjects.Count(o=>!initialIds.Contains(o.Id))==1);
                    if(end=="pause-cancel")
                    {
                        _Input(new InputEventKey{Keycode=Key.Space,Pressed=true});Record(name+": paused");
                        Need(Sim.State.Settings.Pause,"Space did not pause");
                        _Input(new InputEventKey{Keycode=Key.Space,Pressed=true});Need(!Sim.State.Settings.Pause,"Space did not resume");
                    }
                    else if(end=="normal-release")
                    {
                        var screen=GetViewport().GetFinalTransform()*destination;
                        Input.ParseInputEvent(new InputEventMouseMotion{Position=screen,GlobalPosition=screen});
                        Input.FlushBufferedEvents();await RenderFrames(1);
                        var actual=GetViewport().GetMousePosition();checkpoints.Add(new{name=name+": pointer synchronization",requested=new{x=destination.X,y=destination.Y},actual=new{x=actual.X,y=actual.Y},scene=IsScenePoint(actual)});Persist();
                        Need(actual.DistanceTo(destination)<1&&IsScenePoint(actual),"Viewport pointer not synchronized; normal-release branch unverified");
                        _Input(new InputEventMouseButton{Position=destination,ButtonIndex=MouseButton.Left,Pressed=false});
                        Need(new Vector2((float)original.X,(float)original.Z).DistanceTo(new((float)originalX,(float)originalZ))>1,"Original did not move through normal release");
                        Record(name+": release before settling");
                        // The production release starts an airborne motion; HeldByHand is its occupancy marker until landing.
                        for(int frame=0;frame<60&&Sim.State.PortableMotions.Any(m=>m.Id==original.Id);frame++)_Process(1.0/60);
                        Need(!Sim.State.PortableMotions.Any(m=>m.Id==original.Id)&&!original.HeldByHand,"Original did not settle within 60 simulation frames");
                    }
                    else if(end=="focus-cancel")
                    {
                        GetWindow().EmitSignal(Window.SignalName.FocusExited);
                        Need(!_windowFocused,"FocusExited did not reach production callback");
                        GetWindow().EmitSignal(Window.SignalName.FocusEntered);
                        Need(_windowFocused,"FocusEntered did not reach production callback");
                    }
                    else if(end=="outside-release")
                    {
                        var outside=new Vector2(10,10);var screen=GetViewport().GetFinalTransform()*outside;
                        Input.ParseInputEvent(new InputEventMouseMotion{Position=screen,GlobalPosition=screen});Input.FlushBufferedEvents();await RenderFrames(1);
                        Need(!IsScenePoint(GetViewport().GetMousePosition()),"Outside release must be outside canvas");
                        _Input(new InputEventMouseButton{Position=outside,ButtonIndex=MouseButton.Left,Pressed=false});
                    }
                    else if(end=="camera-cancel")
                    {
                        _UnhandledInput(new InputEventMouseButton{Position=destination,ButtonIndex=MouseButton.Middle,Pressed=true});
                        Need(_panning,"Middle press did not enter camera pan");
                        _Input(new InputEventMouseButton{Position=destination,ButtonIndex=MouseButton.Middle,Pressed=false});
                        Need(!_panning,"Middle release did not end camera pan");
                    }
                    else if(end=="right-click-control")_Input(new InputEventMouseButton{Position=destination,ButtonIndex=MouseButton.Right,Pressed=true});
                    else Click(_choiceList,"抚摸");
                    RefreshRoomUi();Record(name+": gesture ended");
                    Check(name+": ending preserves original portion and spends no extra stock",!original.HeldByHand&&_heldSupplyId==null&&!_mouseHeld&&original.Portions==portions&&Stock()==initialStock-1);
                    if(_roomMode==RoomMode.Grab)
                        Check(name+": Grab restores Lift after ending",_toolArmed&&_action==InteractionAction.Lift&&_title.Text.Contains("抓取"));
                    if(end!="normal-release")
                        Check(name+": cancellation preserves original ground position",original.X==originalX&&original.Z==originalZ);
                    var next=EmptyGround(new((float)original.X,0,(float)original.Z));
                    Need(ContactAt(next)==null&&ObjectAt(next)==null&&IsScenePoint(next),"Next press must be empty scene ground");
                    Press(next);RefreshRoomUi();Record(name+": next empty click");
                    Check(name+": next empty click does not create supply or spend stock",Stock()==initialStock-1&&Sim.State.RoomObjects.Count(o=>!initialIds.Contains(o.Id))==1);
                    Check(name+": original ID and portions remain distinguishable",Sim.State.RoomObjects.Single(o=>o.Id==original.Id).Portions==portions);
                    // Repeat pickup of the same actual object, then cancel via Space.
                    Click(_groupList,"空手");Click(_choiceList,"抓取");_Process(0);await RenderFrames(1);
                    Press(SupplyPoint(original));
                    var feed=kind==RoomObjectKind.Water?InteractionAction.Drink:kind==RoomObjectKind.Sweet?InteractionAction.FeedSweet:InteractionAction.FeedMeal;
                    Check(name+": repeated Grab retains feeding action while held",_heldSupplyId==original.Id&&original.HeldByHand&&_mouseHeld&&_action==feed&&Stock()==initialStock-1);
                    _Input(new InputEventKey{Keycode=Key.Space,Pressed=true});_Input(new InputEventKey{Keycode=Key.Space,Pressed=true});
                    Press(EmptyGround(new((float)original.X,0,(float)original.Z)));
                    Check(name+": repeated cancellation cannot purchase supply",_action==InteractionAction.Lift&&Stock()==initialStock-1&&original.Portions==portions&&Sim.State.RoomObjects.Count(o=>!initialIds.Contains(o.Id))==1);
                    // Explicit food selection must still purchase/place exactly one new object.
                    Click(_groupList,"食物");Click(_choiceList,kind==RoomObjectKind.Water?"清水":kind==RoomObjectKind.Sweet?"零食":"饭饭");
                    var beforeExplicit=Sim.State.RoomObjects.Select(o=>o.Id).ToHashSet();
                    Press(EmptyGround(new((float)original.X,0,(float)original.Z)));
                    var explicitItems=Sim.State.RoomObjects.Where(o=>!beforeExplicit.Contains(o.Id)).ToArray();
                    Check(name+": explicit food tool still creates one supply",explicitItems.Length==1&&explicitItems[0].Kind==kind&&explicitItems[0].Portions==1&&Stock()==initialStock-2&&original.Portions==portions);
                    await RenderFrames(1);
                }
                catch(Exception ex){fixtureFailures++;results.Add(new{name=name+": fixture/action failure",passed=false,error=ex.ToString(),state=Snapshot()});Persist();GD.PushError(ex.ToString());}
            }
        }
        catch(Exception ex){fixtureFailures++;results.Add(new{name="harness",passed=false,error=ex.ToString()});Persist();}
        Persist();GD.Print($"GRAB_SUPPLY_RESULT: failures={failures}; fixtureFailures={fixtureFailures}; checks={results.Count}");
        _sound.Stop();_sound.Stream=null;_roomMusic.Stop();_roomMusic.Stream=null;await RenderFrames(3);GetTree().Quit(failures==0&&fixtureFailures==0?0:1);
    }
}

using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Validate before SaveStore is constructed: never fall back to ordinary user saves.
    private static void ValidateAutosavePlacementVerification(string[] args,string saveDirectory)
    {
        int flag=Array.IndexOf(args,"--autosave-placement-check-output");
        if(flag<0)return;
        if(flag+1>=args.Length||!Path.IsPathFullyQualified(args[flag+1])||
            Array.IndexOf(args,"--interaction-test-profile")<0||
            !saveDirectory.StartsWith("user://interaction-test-saves/",StringComparison.Ordinal))
            throw new ArgumentException("Autosave placement verification requires an absolute output and an isolated test profile.");
        if(DisplayServer.GetName()!="headless")
            throw new ArgumentException("Autosave placement verification is headless only.");
        string directory=ProjectSettings.GlobalizePath(saveDirectory);
        if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())
            throw new InvalidOperationException("Autosave placement verification requires a fresh test profile; existing files are preserved.");
        if(File.Exists(args[flag+1]))
            throw new InvalidOperationException("Verification output already exists; preserve the earlier evidence.");
    }

    private async void StartAutosavePlacementVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,"--autosave-placement-check-output");
        if(flag<0)return;
        try
        {
            SetProcess(false);SetRoomMuted(true);_autoClock=-10000;
            await RenderFrames(3);
            _windowFocused=true;
            var results=new List<object>();int failures=0;
            void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
            void Check(string name,Func<bool> test)
            {
                bool passed=false;string? error=null;
                try{passed=test();if(!passed)error="Assertion failed.";}catch(Exception e){error=e.Message;}
                if(!passed)failures++;
                results.Add(new{name,passed,error});GD.Print((passed?"PASS ":"FAIL ")+name+(error==null?"":": "+error));
            }
            void Click(Node root,string label)
            {
                var b=root.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==label);
                Require(!b.Disabled,"Disabled button: "+label);b.EmitSignal(BaseButton.SignalName.Pressed);
            }
            string Resources()=>JsonSerializer.Serialize(new{Sim.State.Money,Sim.State.Food,Sim.State.StationWaterPortions,
                Sim.State.ShelterStock,Sim.State.FoodStationStock,Sim.State.WaterStationStock});
            byte[] Auto()=>File.ReadAllBytes(_saves.SlotPath(0));
            void Tick(){_Process(.05);}
            void Cancel()=>_Input(new InputEventKey{Keycode=Key.Escape,Pressed=true});
            bool Active(string kind)=>kind switch
            {
                "arrival"=>_arrivingCreature!=null,"shelter"=>_placingShelter,
                "food-station"=>_placingSupplyStationKind==SupplyStationKind.Food,
                _=>_placingSupplyStationKind==SupplyStationKind.Water
            };
            void Arm(string kind)
            {
                if(kind=="arrival"){ShowCreatureArrival();Click(_panelContents,"选择落点");}
                else if(kind=="shelter"){ShowShelterSupplies();Click(_panelContents,"拿小家 · 点击地面摆放");}
                else
                {
                    Click(_groupList,"食物");
                    Click(_choiceList,kind=="food-station"?"食物供给器":"饮水器");
                }
                Require(Active(kind),"UI callback did not arm "+kind);
            }
            Vector2 Point(string kind)
            {
                for(float z=-5;z<=5;z+=.5f)for(float x=-5;x<=5;x+=.5f)
                {
                    var p=_camera.UnprojectPosition(new Vector3(x,0,z));
                    if(!IsScenePoint(p)||ContactAt(p)!=null||ObjectAt(p)!=null)continue;
                    bool valid=kind=="arrival"?Sim.NewCreaturePlacementReason(_arrivingCreature!,x,z)==null:
                        kind=="shelter"?Sim.CanPlaceShelterAt(x,z):
                        Sim.CanPlaceSupplyStationAt(_placingSupplyStationKind!.Value,x,z);
                    if(valid)return p;
                }
                throw new InvalidOperationException("No valid visible placement fixture point.");
            }
            void Place(Vector2 p)=>_UnhandledInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=p});
            foreach(string kind in new[]{"arrival","shelter","food-station","water-station"})
            {
                CancelGesture();CloseActivityPanel();
                Sim=WorldSimulation.CreateCourtyardEconomy();
                Sim.State.Money=2000;Sim.State.ShelterStock=3;Sim.State.FoodStationStock=3;Sim.State.WaterStationStock=3;
                BuildActors();RefreshRoomUi();_autoClock=-10000;Tick();UpdateRoomCamera(1);
                Require(SaveRoom(0,false),"Could not write synthetic autosave fixture.");
                byte[] before=Auto();string inventory=Resources();
                Arm(kind);string prompt=_message;
                _autoClock=59.99;Tick();
                Check(kind+": overdue autosave preserves placement",()=>Active(kind));
                Check(kind+": overdue autosave preserves prompt",()=>_message==prompt);
                Check(kind+": overdue autosave preserves money and inventory",()=>Resources()==inventory);
                Check(kind+": overdue autosave leaves slot byte-identical",()=>Auto().SequenceEqual(before));
                Check(kind+": deferred timer remains overdue",()=>_autoClock>60);
                Cancel();Sim.State.Money++;Tick();
                Check(kind+": cancel releases overdue autosave",()=>!Active(kind)&&_autoClock==0&&_saves.Load(0).State.Money==Sim.State.Money);
                Check(kind+": cancel does not charge or consume stock",()=>Sim.State.Money==2001&&Sim.State.ShelterStock==3&&Sim.State.FoodStationStock==3&&Sim.State.WaterStationStock==3);

                Arm(kind);_autoClock=59.99;Tick();
                int money=Sim.State.Money,creatures=Sim.State.Creatures.Count,shelters=Sim.State.ShelterStock,
                    food=Sim.State.FoodStationStock,water=Sim.State.WaterStationStock;
                if(Active(kind))
                {
                    Vector2 point=Point(kind);Place(point);
                    Check(kind+": valid click completes placement once",()=>!Active(kind)&&
                        Sim.State.Creatures.Count==creatures+(kind=="arrival"?1:0)&&
                        Sim.State.Money==money-(kind=="arrival"?WorldSimulation.NewCreaturePrice:0)&&
                        Sim.State.ShelterStock==shelters-(kind=="shelter"?1:0)&&
                        Sim.State.FoodStationStock==food-(kind=="food-station"?1:0)&&
                        Sim.State.WaterStationStock==water-(kind=="water-station"?1:0));
                    string after=Resources();int count=Sim.State.Creatures.Count;Place(point);
                    Check(kind+": second click cannot double-charge",()=>Resources()==after&&Sim.State.Creatures.Count==count);
                    Tick();
                    Check(kind+": completion releases overdue autosave",()=>_autoClock==0&&_saves.Load(0).State.Money==Sim.State.Money);
                }
                else
                {
                    Check(kind+": valid click completes placement once",()=>false);
                    Check(kind+": second click cannot double-charge",()=>false);
                    Check(kind+": completion releases overdue autosave",()=>false);
                }
                CancelGesture();Arm(kind);string manualResources=Resources();
                Require(SaveRoom(1),"Manual save failed.");
                Check(kind+": manual save still cancels placement without charge",()=>!Active(kind)&&Resources()==manualResources&&File.Exists(_saves.SlotPath(1)));
                CancelGesture();_autoClock=59.99;Sim.State.Money++;Tick();
                Check(kind+": normal idle autosave remains enabled",()=>_autoClock==0&&_saves.Load(0).State.Money==Sim.State.Money);
            }
            string output=args[flag+1];Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,JsonSerializer.Serialize(new{success=failures==0,failures,checks=results.Count,
                renderer=DisplayServer.GetName(),scope="Actual scene button signals, input callbacks, process frames and synthetic isolated saves; no native input or visual acceptance.",results},
                new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"AUTOSAVE_PLACEMENT_CHECKS: {results.Count-failures}/{results.Count}; failures={failures}");
            await RenderFrames(3);
            _sound.Stop();_sound.Stream=null;_roomMusic.Stop();_roomMusic.Stream=null;
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
            GetTree().Quit(failures==0?0:1);
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(2);}
    }
}

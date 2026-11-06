using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Real scene input dispatch with synthetic fixtures. Never use ordinary saves.
    private async void StartSelectionVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,"--selection-check-output");
        if(flag<0)return;
        try
        {
            int profile=Array.IndexOf(args,"--interaction-test-profile");
            if(flag+1>=args.Length||profile<0||profile+1>=args.Length)
                throw new ArgumentException("Selection verification requires an output and an isolated test profile.");
            if(Enumerable.Range(0,4).Any(slot=>File.Exists(_saves.SlotPath(slot))||
                File.Exists(_saves.SlotPath(slot)+".bak")||File.Exists(_saves.SlotPath(slot)+".writing")))
                throw new InvalidOperationException("Selection verification requires a fresh test profile; existing files are preserved.");
            SetProcess(false);_autoClock=-10000;SetRoomMuted(true);
            var results=new List<object>();int failures=0;
            void Require(bool ok,string why){if(!ok)throw new Exception(why);}
            void Check(string name,Action action)
            {
                try{action();results.Add(new{name,passed=true});GD.Print("PASS "+name);}
                catch(Exception e)
                {
                    failures++;results.Add(new{name,passed=false,error=e.Message,mode=Sim.State.Mode.ToString(),
                        selectedId=Sim.State.SelectedId,time=Sim.State.WorldSeconds,pause=Sim.State.Settings.Pause,message=_message});
                    GD.Print("FAIL "+name+": "+e.Message);
                }
            }
            void Fresh()
            {
                CloseModal();CloseActivityPanel();CancelGesture();
                Sim=WorldSimulation.CreateCourtyardEconomy();ResetRoomUiSession();BuildActors();
                _selectedRoomObjectId=null;_observedStemMotherId=null;_selectedDetachedStemEventId=null;
                _selectedSupplyStationId=null;_selectedShelterId=null;_recovered=false;
                DisarmTool();RefreshRoomUi();
            }
            void Press(Key key)
            {
                _windowFocused=true;GetViewport().GuiReleaseFocus();
                GetViewport().PushInput(new InputEventKey{Keycode=key,PhysicalKeycode=key,Pressed=true},true);
                GetViewport().PushInput(new InputEventKey{Keycode=key,PhysicalKeycode=key,Pressed=false},true);
            }
            void Tab()=>Press(Key.Tab);
            void Frames(int count=10){for(int n=0;n<count;n++)_Process(.1);}
            void ExpectManagement(string why)=>Require(Sim.State.Mode==PlayMode.Management,why);
            void ExpectSaveable()
            {
                byte[]? previous=File.Exists(_saves.SlotPath(1))?File.ReadAllBytes(_saves.SlotPath(1)):null;
                if(!SaveRoom(1,false))
                {
                    Require(previous==null?!File.Exists(_saves.SlotPath(1)):File.ReadAllBytes(_saves.SlotPath(1)).SequenceEqual(previous),"failed save changed the previous isolated slot");
                    throw new InvalidOperationException(_message+"; previous isolated slot preserved");
                }
                SaveStore.Validate(Sim.State);var loaded=_saves.Load(1).State;
                Require(loaded.Mode==Sim.State.Mode&&loaded.SelectedId==Sim.State.SelectedId&&loaded.Settings.Pause==Sim.State.Settings.Pause,
                    "saved selection, close-up mode or explicit pause did not survive loading");
            }

            Fresh();
            Check("normal Tab enters close-up and freezes natural time",()=>
            {
                double before=Sim.State.WorldSeconds;Tab();Require(Sim.State.Mode==PlayMode.CloseUp,"Tab did not enter close-up");
                Frames();Require(Sim.State.WorldSeconds==before,"close-up advanced natural time");ExpectSaveable();
            });
            Check("normal second Tab resumes natural time",()=>
            {
                Tab();ExpectManagement("second Tab did not leave close-up");double before=Sim.State.WorldSeconds;
                Frames();Require(Sim.State.WorldSeconds>before,"yard did not resume");ExpectSaveable();
            });
            await RenderFrames(2);

            Fresh();var stored=Sim.Selected!;
            Sim.RecycleCourtyardObject(stored.Id);RefreshRoomUi();
            Check("recycling selected oil leaves a valid unselected yard",()=>
            {
                Require(stored.RegionId=="courtyard-container"&&Sim.Selected==null&&Sim.State.Creatures.Count(c=>c.RegionId=="home")==2,"fixture did not use real recycle transition");
                Require(_closeButton.Disabled,"empty-selection close-up button is enabled");ExpectManagement("recycle did not leave management");ExpectSaveable();
            });
            Tab();
            Check("Tab rejects empty selection after recycling",()=>ExpectManagement("Tab entered close-up without a selected oil"));
            Check("recycled selection keeps world advancing and saveable",()=>
            {
                double before=Sim.State.WorldSeconds;Frames();Require(Sim.State.WorldSeconds>before,"empty close-up stopped world time");ExpectSaveable();
            });
            Check("empty-selection save survives keyboard shortcut",ExpectSaveable);
            Check("repeated Tab with empty selection remains management",()=>
            {for(int n=0;n<3;n++){Tab();ExpectManagement("repeated Tab entered empty close-up");}});
            await RenderFrames(2);

            Fresh();
            foreach(var oil in Sim.State.Creatures.ToArray())
            {
                Sim.State.SelectedId=oil.Id;ShowCreatureMarket(confirmingId:oil.Id,expectedPrice:CourtyardEconomy.Quote(oil).Price);
                var confirm=_panelContents.GetChildren().OfType<Button>().Single(b=>b.Text=="确认卖给商店");
                Require(!confirm.Disabled,"sale button unexpectedly disabled");confirm.EmitSignal(BaseButton.SignalName.Pressed);
            }
            CloseActivityPanel();RefreshRoomUi();
            Check("real shop buttons can sell all three fixture oils",()=>Require(Sim.State.Creatures.Count==0&&Sim.State.CreatureSales.Count==3&&Sim.Selected==null,"sale fixture incomplete"));
            Tab();
            Check("Tab rejects empty yard after all sales",()=>ExpectManagement("Tab entered empty sold-out yard"));
            Check("sold-out yard remains saveable",ExpectSaveable);
            await RenderFrames(2);

            Fresh();
            var cushion=Sim.State.RoomObjects.First(o=>o.Kind==RoomObjectKind.Cushion);
            SelectObserved(null,cushion);
            Check("selected room object disables close-up button",()=>Require(_closeButton.Disabled,"object button was not disabled"));
            Tab();
            Check("Tab obeys room-object close-up restriction",()=>ExpectManagement("Tab entered prior oil while a room object was selected"));
            await RenderFrames(2);

            foreach(bool pause in new[]{false,true})
            {
                Fresh();Sim.State.Settings.Pause=pause;Tab();
                Check("valid close-up preserves explicit pause="+pause,()=>Require(Sim.State.Mode==PlayMode.CloseUp&&Sim.State.Settings.Pause==pause,"close-up changed pause or failed"));
                Sim.State.SelectedId=null;Frames(1);
                Check("missing close-up target recovers with pause="+pause,()=>
                {ExpectManagement("missing close-up target was not recovered");Require(Sim.State.Settings.Pause==pause,"recovery changed explicit pause");ExpectSaveable();});
                await RenderFrames(2);
            }
            foreach(bool memorialized in new[]{false,true})
            {
                Fresh();Tab();var target=Sim.Selected!;Sim.State.Settings.Pause=true;
                if(memorialized)target.Family.Memorialized=true;else target.Family.ColdStorage.Stored=true;
                Frames(1);
                Check("unavailable body exits close-up and preserves identity: "+(memorialized?"memorial":"cold"),()=>
                {ExpectManagement("unavailable body stayed in close-up");Require(Sim.State.SelectedId==target.Id&&Sim.State.Settings.Pause,"recovery changed identity or pause");});
                await RenderFrames(2);
            }
            Fresh();var moved=Sim.Selected!;Sim.RecycleCourtyardObject(moved.Id);Sim.State.SelectedId=moved.Id;RefreshRoomUi();
            Tab();
            Check("contained oil cannot be viewed as a current-yard body",()=>ExpectManagement("contained oil entered close-up"));
            Sim.State.Mode=PlayMode.CloseUp;Frames(1);
            Check("close-up target outside active region recovers",()=>ExpectManagement("outside-region body stayed in close-up"));

            foreach(var key in new[]{Key.Tab,Key.Escape})
            foreach(bool outsideRegion in new[]{false,true})
            {
                Fresh();var departing=Sim.Selected!;
                if(outsideRegion){Sim.RecycleCourtyardObject(departing.Id);Sim.State.SelectedId=departing.Id;}
                else Sim.State.SelectedId="removed-fixture-oil";
                Sim.State.Mode=PlayMode.CloseUp;Sim.State.Settings.Pause=true;
                // Do not process a frame before the keyboard exit: this is the ordering boundary.
                Press(key);
                Check("keyboard-before-frame recovery: "+key+" / "+(outsideRegion?"transferred":"missing"),()=>
                {
                    ExpectManagement("keyboard did not leave invalid close-up");
                    Require(Sim.State.SelectedId==null&&Sim.State.Settings.Pause,"keyboard exit retained stale selection or changed pause");
                    ExpectSaveable();
                });
                await RenderFrames(2);
            }
            string output=Path.GetFullPath(args[flag+1]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,JsonSerializer.Serialize(new{success=failures==0,failures,checks=results.Count,
                renderer=DisplayServer.GetName(),scope="Real Godot viewport keyboard dispatch, shop button signals, public recycle API and isolated synthetic saves; not native input or visual acceptance",results},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"SELECTION_CHECKS: {results.Count-failures}/{results.Count}; failures={failures}");
            await RenderFrames(3);_sound.Stop();_sound.Stream=null;_roomMusic.Stop();_roomMusic.Stream=null;
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);GetTree().Quit(failures==0?0:1);
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(2);}
    }
}

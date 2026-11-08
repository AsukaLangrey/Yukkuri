using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Opt-in isolated integration check; normal play never invokes this entry.
    private void StartColdStorageVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,"--cold-storage-check-output");
        if(flag>=0&&flag+1<args.Length)_=VerifyColdStorageUi(Path.GetFullPath(args[flag+1]));
    }
    private async Task VerifyColdStorageUi(string directory)
    {
        int assertions=0;
        void Check(bool ok,string why){if(!ok)throw new Exception(why);assertions++;}
        void Esc()=>_Input(new InputEventKey{Pressed=true,Keycode=Key.Escape});
        try
        {
            Directory.CreateDirectory(directory);_autoClock=-100000;_muted=true;
            await RenderFrames(12);_windowFocused=true;CancelGesture();DisarmTool();CloseActivityPanel();
            foreach(bool paused in new[]{false,true})
            {
                Sim.State.Settings.Pause=paused;double before=Sim.State.WorldSeconds;
                Esc();Check(_modalOpen&&Sim.State.Settings.Pause,"Esc did not open system menu");
                await RenderFrames(5);Check(Sim.State.WorldSeconds==before,"menu advanced world");
                ShowSettings();Esc();Check(!_modalOpen&&Sim.State.Settings.Pause==paused,"nested settings lost pause choice");
                ShowSystemMenu();Esc();Check(Sim.State.Settings.Pause==paused,"menu close lost pause choice");
            }
            Sim.State.Settings.Pause=false;_toolArmed=true;_mouseHeld=true;Esc();
            Check(!_modalOpen&&!_mouseHeld,"Esc did not cancel active gesture first");
            ShowContainer();Esc();Check(!ActivityPanelVisible&&!_modalOpen,"Esc did not close inline page first");
            Sim.State.Settings.Pause=true;
            foreach(var size in new[]{new Vector2I(1440,900),new Vector2I(1600,900),new Vector2I(1440,1000)})
            {
                // Closing box management clears object selection; a zero-tab inspector is hidden.
                SelectOil(Sim.State.Creatures[0].Id);
                GetWindow().Size=size;await RenderFrames(10);RefreshRoomUi();
                var canvas=GameUiLayout.Canvas(_ui);
                Check(Math.Abs(_rightPanel.Size.X-266)<1,"inspector grew wider than 266");
                Check(!IsScenePoint(_rightPanel.GetGlobalRect().GetCenter())&&IsScenePoint(canvas.GetCenter()),"sidebar/canvas boundary");
                Check(!IsScenePoint(_speedButton.GetGlobalRect().GetCenter()),"speed click leaks into yard");
                Check(!_recycleBox.Visible&&!_systemControls.IsVisibleInTree(),"idle recycle/system controls still visible");
                ShowContainer();await RenderFrames(5);
                Check(_panelContents.GetGlobalRect().End.X<=_rightPanel.GetGlobalRect().End.X,"12-slot grid overflow");
                CloseActivityPanel();
            }
            Sim.State.Settings.Pause=false;string id=Sim.State.Creatures[0].Id;
            _roomMode=RoomMode.Grab;_toolArmed=true;_sceneDragId=id;_sceneDragKind="body";
            _recycleReady=true;UpdateRecycleReceipt(0);RefreshRecycleDrop(_recycleBox.GetGlobalRect().GetCenter());
            Check(_recycleBox.Visible&&GameUiLayout.Canvas(_ui).Encloses(_recycleBox.GetGlobalRect()),$"grab drop target missing/outside canvas: visible={_recycleBox.Visible}, focused={_windowFocused}, pause={Sim.State.Settings.Pause}, modal={_modalOpen}, ready={_recycleReady}, box={_recycleBox.GetGlobalRect()}, canvas={GameUiLayout.Canvas(_ui)}");
            int receipts=_recycleSuccessCount;
            HandleRedesignRelease(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=_recycleBox.GetGlobalRect().GetCenter()});
            Check(Sim.State.Creatures[0].Family.ColdStorage.Stored&&_recycleSuccessCount==receipts+1&&_recycleReceipt>0,"successful drop missing receipt");
            TogglePause();Check(!_recycleBox.Visible&&_recycleReceipt==0,"pause did not clean receipt");TogglePause();
            // Fill actual core capacity, leaving a real visible oil to test rejection feedback.
            var template=Sim.State.Creatures[2];
            while(Sim.State.Creatures.Count(c=>c.Family.ColdStorage.Stored)<12)
            {
                int n=Sim.State.Creatures.Count;
                var c=JsonSerializer.Deserialize<Creature>(JsonSerializer.Serialize(template,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
                c.Id="ui-cold-"+n;c.Body.Id=c.Id+"-body";c.Registration.IdentityId=c.Id+"-identity";
                c.Interaction.Accessory.Id=c.Id+"-accessory";c.Family=new();c.Family.ColdStorage.Stored=true;Sim.State.Creatures.Add(c);
            }
            id=template.Id;_sceneDragId=id;_sceneDragKind="body";_recycleReady=true;UpdateRecycleReceipt(0);RefreshRecycleDrop(_recycleBox.GetGlobalRect().GetCenter());
            receipts=_recycleSuccessCount;string region=template.RegionId;
            HandleRedesignRelease(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=_recycleBox.GetGlobalRect().GetCenter()});
            Check(!template.Family.ColdStorage.Stored&&template.RegionId==region&&_recycleSuccessCount==receipts,"full-box rejection animated success/lost body");
            ShowContainer();Check(!_recycleBox.Visible&&ActivityPanelVisible&&!Sim.State.Settings.Pause,"management entry changed pause or kept drop target");
            File.WriteAllText(Path.Combine(directory,"result.txt"),$"PASS: {assertions} assertions; Esc/pause restoration, sidebar bounds, 12-slot management, successful/failed drop receipts. Not a manual playthrough.");
            GetTree().Quit();
        }
        catch(Exception e){File.WriteAllText(Path.Combine(directory,"result.txt"),"FAIL: "+e);GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}

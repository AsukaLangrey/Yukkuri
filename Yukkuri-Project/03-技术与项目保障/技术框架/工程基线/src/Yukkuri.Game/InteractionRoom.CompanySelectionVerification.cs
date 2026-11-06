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
    // Run before legacy scene routing: no diagnostic flag may fall through to another save domain.
    private static void ValidateCompanySelectionEntryArguments(string[] args)
    {
        const string flag="--company-selection-check-output";
        if(!args.Contains(flag))return;
        if(args.Any(a=>a.StartsWith("--")&&a!=flag&&a!="--interaction-test-profile"))
            throw new ArgumentException("Other modes or diagnostic flags may not run concurrently with company selection checks.");
    }
    // Opt-in headless regression. Guard MUST run before any SaveStore construction.
    private static void ValidateCompanySelectionVerification(string[] args,string saveDirectory)
    {
        const string flag="--company-selection-check-output";
        int i=Array.IndexOf(args,flag);if(i<0)return;
        int p=Array.IndexOf(args,"--interaction-test-profile");
        if(args.Count(a=>a==flag)!=1||i+1>=args.Length||!Path.IsPathFullyQualified(args[i+1])||
            p<0||p+1>=args.Length||args.Count(a=>a=="--interaction-test-profile")!=1||
            saveDirectory!="user://interaction-test-saves/"+args[p+1]||DisplayServer.GetName()!="headless")
            throw new ArgumentException("Company selection diagnostic requires headless, absolute output, and one isolated profile.");
        if(args.Any(a=>a.StartsWith("--")&&a!=flag&&a!="--interaction-test-profile"))
            throw new ArgumentException("Other diagnostic flags may not run concurrently.");
        string save=Path.GetFullPath(ProjectSettings.GlobalizePath(saveDirectory));
        string parent=Path.GetFullPath(ProjectSettings.GlobalizePath("user://interaction-test-saves"));
        if(Path.GetDirectoryName(save)!=parent||Path.GetFileName(save)!=args[p+1])
            throw new ArgumentException("Resolved profile escaped the isolated save root.");
        string output=Path.GetFullPath(args[i+1]);string directory=Path.GetDirectoryName(output)!;
        string expectedRoot=Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../.local/testing"))+Path.DirectorySeparatorChar;
        if(!directory.StartsWith(expectedRoot,StringComparison.OrdinalIgnoreCase)||
            (Directory.Exists(save)&&Directory.EnumerateFileSystemEntries(save).Any())||
            (Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())||File.Exists(output))
            throw new InvalidOperationException("Fresh isolated profile and fresh diagnostic output directory required; earlier evidence is preserved.");
        GD.Print("COMPANY_SELECTION_GUARD: "+save+" | "+output);
    }
    private void StartCompanySelectionVerificationIfRequested(string[] args)
    {
        int i=Array.IndexOf(args,"--company-selection-check-output");if(i>=0)_=VerifyCompanySelection(args[i+1]);
    }
    private async Task VerifyCompanySelection(string output)
    {
        var results=new List<object>();int failures=0;
        void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        void Check(string name,Func<bool> test)
        {
            bool passed=false;string? error=null;
            try{passed=test();if(!passed)error="Assertion failed";}catch(Exception ex){error=ex.ToString();}
            if(!passed)failures++;results.Add(new{name,passed,error,pending=_companyFirst,message=_message,
                companions=Sim.State.Creatures.Select(c=>new{c.Id,c.Family.CompanionTargetId}).ToArray()});
            GD.Print((passed?"PASS ":"FAIL ")+name);
        }
        void Click(Node root,string label)
        {
            var b=root.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==label);
            Require(!b.Disabled,"Disabled: "+label);b.EmitSignal(BaseButton.SignalName.Pressed);
        }
        void Company(){Click(_groupList,"交流");Click(_choiceList,"相处");}
        void Settings()=>Click(_ui,"存档与设置");
        string Social()=>JsonSerializer.Serialize(Sim.State.Creatures.Select(c=>new{c.Id,c.Family.Peers,c.Family.CompanionTargetId,c.Family.CompanionUntil}),SaveStore.JsonOptions);
        void Fresh()
        {
            CloseModal();CloseActivityPanel();CancelGesture();_companyFirst=null; // Fixture boundary only, never used within a tested sequence.
            Sim=WorldSimulation.CreateCourtyardEconomy();Sim.State.AutonomousBreeding=false;
            Sim.State.Settings.Pause=false;ResetRoomUiSession();BuildActors();DisarmTool();RefreshRoomUi();
            _windowFocused=true;_autoClock=-10000;UpdateRoomCamera(1);_Process(0);
        }
        Vector2 BodyPoint(string id)
        {
            var c=Sim.State.Creatures.Single(c=>c.Id==id);
            var center=_camera.UnprojectPosition(new Vector3((float)c.X,.5f,(float)c.Z));
            for(int y=-30;y<=30;y+=3)for(int x=-30;x<=30;x+=3)
            {
                var point=center+new Vector2(x,y);var hit=ContactAt(point);
                if(IsScenePoint(point)&&hit is {TargetKind:InteractionTargetKind.Body,Blocked:false}&&hit.Id==id&&ObjectAt(point)==null)return point;
            }
            throw new InvalidOperationException("No visible projected body point for "+id+" at "+center);
        }
        void Tap(string id){var point=BodyPoint(id);_UnhandledInput(new InputEventMouseButton{Position=point,ButtonIndex=MouseButton.Left,Pressed=true});}
        async Task Group(string name,Func<Task> body)
        {
            try{Fresh();await RenderFrames(2);await body();}
            catch(Exception ex){failures++;results.Add(new{name=name+": fixture/action completed",passed=false,error=ex.ToString()});GD.PushError(ex.ToString());}
        }
        const string a="interaction-oil-0",b="interaction-oil-1";
        try
        {
            SetProcess(false);SetRoomMuted(true);_autoClock=-10000;await RenderFrames(3);
            await Group("normal",async()=>
            {
                Company();string before=Social();Tap(a);
                Check("normal: first projected click only chooses A",()=>_companyFirst==a&&Social()==before);
                Tap(b);Check("normal: second projected click creates companionship",()=>_companyFirst==null&&Sim.State.Creatures[0].Family.CompanionTargetId==b&&Sim.State.Creatures[1].Family.CompanionTargetId==a);
                await RenderFrames(1);
            });
            foreach(string mode in new[]{"escape","right click","actual tool switch"})await Group(mode,async()=>
            {
                Company();Tap(a);string before=Social();
                if(mode=="escape")_Input(new InputEventKey{Keycode=Key.Escape,Pressed=true});
                else if(mode=="right click")_Input(new InputEventMouseButton{Position=BodyPoint(a),ButtonIndex=MouseButton.Right,Pressed=true});
                else Click(_choiceList,"打招呼");
                Check(mode+": exits company mode without world mutation",()=>_roomMode!=RoomMode.Company&&Social()==before);
                Company();Tap(b);
                Check(mode+": first new click only chooses B",()=>_companyFirst==b&&Social()==before);
                await RenderFrames(1);
            });
            await Group("page preservation",async()=>
            {
                Company();Tap(a);string before=Social();Click(_ui,"怎么玩");await RenderFrames(2);Click(_panelContents,"收起帮助");
                Check("page: keeps selected company tool and world unchanged",()=>_roomMode==RoomMode.Company&&_toolArmed&&Social()==before);
                Tap(b);Check("page: preserved unfinished pair completes normally",()=>_companyFirst==null&&Sim.State.Creatures[0].Family.CompanionTargetId==b);
            });
            await Group("repeat cancel",async()=>
            {
                Company();Tap(a);string before=Social();var point=BodyPoint(a);
                for(int n=0;n<2;n++)_Input(new InputEventMouseButton{Position=point,ButtonIndex=MouseButton.Right,Pressed=true});
                Check("repeat cancel: pending is cleared without social mutation",()=>_companyFirst==null&&Social()==before);
                Company();Tap(b);Check("repeat cancel: next choice starts a fresh pair",()=>_companyFirst==b&&Social()==before);
                Tap(a);Check("repeat cancel: next pair still completes normally",()=>_companyFirst==null&&Sim.State.Creatures[0].Family.CompanionTargetId==b);
                await RenderFrames(1);
            });
            await Group("same tool",async()=>
            {
                Company();Tap(a);string before=Social();Click(_choiceList,"相处");
                Check("same tool: reselection preserves pending invitation",()=>_companyFirst==a&&Social()==before);
                Tap(b);Check("same tool: second click still completes",()=>_companyFirst==null&&Sim.State.Creatures[0].Family.CompanionTargetId==b);
                await RenderFrames(1);
            });
            await Group("action owner",async()=>
            {
                Company();Tap(a);string before=Social();SelectQuickTool(InteractionAction.Dry);
                Check("action owner: shortcut switching clears pending only",()=>_roomMode!=RoomMode.Company&&_companyFirst==null&&Social()==before);
                Company();Tap(b);Check("action owner: next click starts fresh pair",()=>_companyFirst==b&&Social()==before);
                await RenderFrames(1);
            });
            await Group("missing target",async()=>
            {
                Company();Tap(a);Sim.State.Creatures.RemoveAll(c=>c.Id==a);BuildActors();RefreshRoomUi();await RenderFrames(1);
                string before=Social();Tap(b);
                Check("missing target: stale absent identity cannot create social action",()=>Social()==before&&Sim.State.Creatures.All(c=>c.Family.CompanionTargetId==null));
                _Input(new InputEventMouseButton{Position=BodyPoint(b),ButtonIndex=MouseButton.Right,Pressed=true});Company();Tap(b);
                Check("missing target: explicit cancel permits a fresh valid first target",()=>_companyFirst==b&&Social()==before);
            });
            await Group("arrival tool",async()=>
            {
                Company();Tap(a);string before=Social();Click(_ui,"投放新油");await RenderFrames(2);
                Check("arrival: browsing preserves current tool and pending",()=>_roomMode==RoomMode.Company&&_companyFirst==a&&Social()==before);
                Click(_panelContents,"选择落点");
                Check("arrival: arming placement clears pending only",()=>_arrivingCreature!=null&&_companyFirst==null&&Social()==before);
                Company();Tap(b);Check("arrival: returning to company begins fresh pair",()=>_companyFirst==b&&Social()==before);
            });
            await Group("shelter tool",async()=>
            {
                Sim.State.ShelterStock=1;Company();Tap(a);string before=Social();ShowShelterSupplies();await RenderFrames(2);
                Check("shelter: browsing preserves current tool and pending",()=>_roomMode==RoomMode.Company&&_companyFirst==a&&Social()==before);
                Click(_panelContents,"拿小家 · 点击地面摆放");
                Check("shelter: direct placement callback clears pending only",()=>_placingShelter&&_companyFirst==null&&Social()==before);
                Company();Tap(b);Check("shelter: returning to company begins fresh pair",()=>_companyFirst==b&&Social()==before);
            });
            await Group("rescue tool",async()=>
            {
                Sim.State.WaterRescueTools=1;Company();Tap(a);string before=Social();
                // Callback-level tool selection control, not a visible/native rescue-button acceptance test.
                _immediateRescue.EmitSignal(BaseButton.SignalName.Pressed);
                Check("rescue: immediate-tool callback clears pending only",()=>_waterRescueToolArmed&&_companyFirst==null&&Social()==before);
                Company();Tap(b);Check("rescue: returning to company begins fresh pair",()=>_companyFirst==b&&Social()==before);
                await RenderFrames(1);
            });
            await Group("save preserves",async()=>
            {
                Company();Tap(a);string before=Social();Settings();Click(_modalContents,"保存到槽位 1");
                Check("save: saving without replacement preserves pending pair",()=>_companyFirst==a&&Social()==before);
                Tap(b);Check("save: second click still completes pair",()=>_companyFirst==null&&Sim.State.Creatures[0].Family.CompanionTargetId==b);
                await RenderFrames(1);
            });
            await Group("load",async()=>
            {
                Settings();Click(_modalContents,"保存到槽位 1");Company();Tap(a);
                Settings();Click(_modalContents,"读取槽位 1");await RenderFrames(2);_Process(0);
                string before=Social();Tap(b);
                Check("load: first click in loaded world only chooses B",()=>_companyFirst==b&&Social()==before);
            });
            await Group("restart",async()=>
            {
                Company();Tap(a);Settings();Click(_modalContents,"重新开一间小院");Click(_modalContents,"保存并重新开始");
                await RenderFrames(2);_Process(0);string before=Social();Tap(b);
                Check("restart: first click in new world only chooses B",()=>_companyFirst==b&&Social()==before);
            });
        }
        catch(Exception ex){failures++;results.Add(new{name="harness",passed=false,error=ex.ToString()});}
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output,JsonSerializer.Serialize(new{success=failures==0,failures,checks=results.Count,renderer=DisplayServer.GetName(),
            scope="Headless isolated synthetic worlds; actual toolbar/settings button signals and projected ContactAt body clicks through _UnhandledInput; Escape/right-click through _Input. No native input or visual acceptance. Restart assertion covers only pending selection, not economy initialization.",results},new JsonSerializerOptions{WriteIndented=true}));
        GD.Print($"COMPANY_SELECTION_CHECKS: {results.Count-failures}/{results.Count}; failures={failures}");
        _sound.Stop();_sound.Stream=null;_roomMusic.Stop();_roomMusic.Stream=null;await RenderFrames(3);GetTree().Quit(failures==0?0:1);
    }
}
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
    // This diagnostic is headless only and must be admitted before creating a world or SaveStore.
    private static void GuardShelterUnlockDiagnostic(string[] args)
    {
        const string flag="--shelter-unlock-audit-output";
        if(!args.Any(a=>a.StartsWith("--shelter-unlock",StringComparison.Ordinal)||a.StartsWith("shelter-unlock-",StringComparison.Ordinal)))return;
        int p=Array.IndexOf(args,"--interaction-test-profile"),o=Array.IndexOf(args,flag);
        if(DisplayServer.GetName()!="headless"||args.Length!=4||p<0||o<0||p+1>=args.Length||o+1>=args.Length||
            args.Count(a=>a==flag)!=1||args.Count(a=>a=="--interaction-test-profile")!=1||
            !System.Text.RegularExpressions.Regex.IsMatch(args[p+1],@"\Ashelter-unlock-[a-zA-Z0-9_-]{1,48}\z")||!Path.IsPathFullyQualified(args[o+1]))
            throw new ArgumentException("Shelter audit requires only its output flag, a fresh test profile, and headless execution.");
        string allowed=Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../.local/testing"))+Path.DirectorySeparatorChar;
        string save=Path.GetFullPath(ProjectSettings.GlobalizePath("user://interaction-test-saves/"+args[p+1]));
        string output=Path.GetFullPath(args[o+1]),directory=Path.GetDirectoryName(output)!;
        if(!save.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!directory.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||
            Directory.Exists(save)||File.Exists(save)||Directory.Exists(directory)||File.Exists(output))
            throw new InvalidOperationException("Shelter audit requires never-used save and output directories within .local/testing.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"guard.json"),JsonSerializer.Serialize(new{save,output,guardBeforeWorldAndSaveStore=true}));
        GD.Print("SHELTER_UNLOCK_GUARD: "+save);
    }
    private void StartShelterUnlockDiagnostic(string[] args)
    {
        int i=Array.IndexOf(args,"--shelter-unlock-audit-output");if(i<0)return;
        SetProcess(false);_autoClock=-10000;SetRoomMuted(true);_=AuditShelterUnlock(args[i+1]);
    }
    private async Task AuditShelterUnlock(string output)
    {
        var results=new List<object>();var snapshots=new List<object>();int failures=0;
        void Check(string name,bool passed){results.Add(new{name,passed});if(!passed)failures++;GD.Print((passed?"PASS ":"FAIL ")+name);}
        void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        Button ButtonAt(Node root,string label)=>root.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==label&&b.IsVisibleInTree());
        void Click(Node root,string label){var b=ButtonAt(root,label);Require(!b.Disabled,"Disabled: "+label);b.EmitSignal(BaseButton.SignalName.Pressed);}
        Button Buy()=>ButtonAt(_panelContents,$"购买一间 · {WorldSimulation.ShelterPrice} 元");
        string[] Labels()=>_panelContents.GetChildren().OfType<Label>().Select(l=>l.Text).ToArray();
        string Ledger()=>JsonSerializer.Serialize(new{Sim.State.Money,Sim.State.ShelterStock,Sim.State.CreatureSalesRevenue,sales=Sim.State.CreatureSales.Select(s=>new{s.Creature.Id,s.Amount}),shelters=Sim.State.Shelters.Count});
        void Snapshot(string stage){snapshots.Add(new{stage,Sim.State.Money,Sim.State.ShelterStock,Sim.State.CreatureSalesRevenue,Sim.State.EconomyProgression,buyDisabled=Buy().Disabled,labels=Labels()});File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"checkpoint.json"),JsonSerializer.Serialize(new{results,snapshots},new JsonSerializerOptions{WriteIndented=true}));}
        async Task Open(){if(ActivityPanelVisible)Click(_panelContents,"返回小院");Click(_groupList,"工具");await RenderFrames(3);Click(_choiceList,"小家与藏身处");await RenderFrames(3);}
        async Task GeneralParity(string stage,bool locked)
        {
            CloseActivityPanel();Click(_ui,"商店");await RenderFrames(3);
            InlineChoice Category()=>_panelContents.GetChildren().OfType<HBoxContainer>().SelectMany(r=>r.GetChildren().OfType<InlineChoice>()).Single();
            for(int i=0;i<10&&Category().GetItemText(Category().Selected)!="居所";i++){Click(Category(),"›");await RenderFrames(2);}
            Require(Category().GetItemText(Category().Selected)=="居所","Missing shelter category");
            var b=ButtonAt(_panelContents,$"购买 · 总价 {WorldSimulation.ShelterPrice} 元");
            Check(stage+" general shop lock parity",b.Disabled==locked&&Labels().Any(l=>l==Sim.ShopUnlockReason("shelter"))==locked);
            CloseActivityPanel();ShowShelterSupplies();await RenderFrames(3);
        }
        try
        {
            await RenderFrames(3);Check("default economy world untouched",Sim.State.Money==240&&Sim.State.ShelterStock==0&&Sim.State.CreatureSalesRevenue==0&&Sim.State.EconomyProgression);
            string before=Ledger();bool pause=Sim.State.Settings.Pause;await Open();Snapshot("default new game actual toolbar route");
            Check("locked new-game button disabled",Buy().Disabled);
            Check("locked new-game reason shown inline",Labels().Contains(Sim.ShopUnlockReason("shelter")));
            if(!Buy().Disabled){Click(_panelContents,Buy().Text);await RenderFrames(3);Snapshot("baseline enabled locked button click");}
            Check("locked opening or attempted purchase preserves money and stock",Ledger()==before);
            Check("locked attempted purchase never misreports money or inventory",!Labels().Any(l=>l.Contains("未能购买，请检查金钱和库存")));
            Check("gameplay panel leaves pause and modal state unchanged",Sim.State.Settings.Pause==pause&&!_modalOpen);
            Check("empty stock placement button disabled",ButtonAt(_panelContents,"拿小家 · 点击地面摆放").Disabled);
            await GeneralParity("new game",true);
            for(int n=0;n<3;n++){await Open();Check($"locked reopen {n+1} keeps reason and disabled button",Buy().Disabled&&Labels().Contains(Sim.ShopUnlockReason("shelter")));}

            // Earn unlock revenue through production sales, not a revenue assignment.
            CloseActivityPanel();Sim.State.AutonomousBreeding=false;
            for(int n=0;n<8&&Sim.ShopUnlockReason("shelter")!=null;n++)
            {
                if(Sim.State.Creatures.Count==0){var arrival=Sim.PlaceNewCreature(new("reimu",true),0,0);Require(arrival.Success,arrival.Message);}
                var c=Sim.State.Creatures.First();int price=CourtyardEconomy.Quote(c).Price;long previous=Sim.State.CreatureSalesRevenue;
                string reply=Sim.SellCreatureToShop(c.Id,price);Require(Sim.State.CreatureSalesRevenue==previous+price,reply);
            }
            Require(Sim.ShopUnlockReason("shelter")==null,"Production sales did not reach unlock threshold");SaveStore.Validate(Sim.State);BuildActors();
            Check("production sales cross 500 and preserve matching ledger",Sim.State.CreatureSalesRevenue>=500&&Sim.State.CreatureSales.Sum(s=>(long)s.Amount)==Sim.State.CreatureSalesRevenue);
            await Open();Snapshot("unlocked by production sales");Check("earned unlock enables dedicated purchase",!Buy().Disabled&&!Labels().Any(l=>l.Contains("元解锁")));
            await GeneralParity("earned unlock",false);
            int money=Sim.State.Money,stock=Sim.State.ShelterStock;long revenue=Sim.State.CreatureSalesRevenue;
            Click(_panelContents,Buy().Text);await RenderFrames(3);Snapshot("unlocked purchase");
            Check("unlocked purchase debits exactly 45 and adds one",Sim.State.Money==money-WorldSimulation.ShelterPrice&&Sim.State.ShelterStock==stock+1);
            Check("purchase preserves permanent unlock revenue",Sim.State.CreatureSalesRevenue==revenue&&Sim.ShopUnlockReason("shelter")==null);
            Check("purchase gives inline success and enables placement",Labels().Any(l=>l.Contains("小家已入库"))&&!ButtonAt(_panelContents,"拿小家 · 点击地面摆放").Disabled);
            Click(_panelContents,"拿小家 · 点击地面摆放");await RenderFrames(3);
            Check("placement button arms real tool and closes panel",_placingShelter&&_toolArmed&&!ActivityPanelVisible);
            CancelGesture();Check("placement cancellation consumes no stock",!_placingShelter&&Sim.State.ShelterStock==stock+1);
            ArmShelterPlacement();Vector2? spot=null;
            for(int x=-4;x<=4&&spot==null;x++)for(int z=-4;z<=4&&spot==null;z++)
            {var p=_camera.UnprojectPosition(new Vector3(x,0,z));if(IsScenePoint(p)&&Sim.CanPlaceShelterAt(x,z)&&ContactAt(p)==null&&ObjectAt(p)==null)spot=p;}
            if(spot is not { } placementPoint)throw new InvalidOperationException("No empty on-canvas placement point");
            PlaceShelterAt(placementPoint);await RenderFrames(3);
            Check("production pointer placement consumes one and creates shelter",Sim.State.ShelterStock==stock&&Sim.State.Shelters.Count==1&&!_placingShelter);
            SaveStore.Validate(Sim.State);

            // Exact boundaries and legacy behavior below are controlled, validated ledger fixtures.
            for(int target=499;target<=500;target++)
            {
                var last=Sim.State.CreatureSales[^1];long others=Sim.State.CreatureSales.Take(Sim.State.CreatureSales.Count-1).Sum(s=>(long)s.Amount);
                Require(target>others,"Fixture cannot adjust final positive sale");Sim.State.CreatureSales[^1]=last with {Amount=(int)(target-others)};Sim.State.CreatureSalesRevenue=target;
                SaveStore.Validate(Sim.State);ShowShelterSupplies();await RenderFrames(3);Snapshot("controlled ledger boundary "+target);
                Check($"controlled {target} boundary matches rule and label",Buy().Disabled==(target<500)&&Labels().Any(l=>l==Sim.ShopUnlockReason("shelter"))==(target<500));
                await GeneralParity("controlled "+target,target<500);
            }
            money=Sim.State.Money;Sim.State.Money=WorldSimulation.ShelterPrice-1;ShowShelterSupplies();await RenderFrames(3);before=Ledger();
            Check("insufficient funds disables dedicated button",Buy().Disabled);Check("insufficient funds rule rejects without mutation",!Sim.BuyShelter()&&Ledger()==before);
            Sim.State.Money=money;Sim.State.ShelterStock=99;ShowShelterSupplies();await RenderFrames(3);before=Ledger();
            Check("full inventory disables dedicated button",Buy().Disabled);Check("full inventory rule rejects without mutation",!Sim.BuyShelter()&&Ledger()==before);
            Sim.State.ShelterStock=0;Sim=WorldSimulation.CreateInteractionPlaytest();BuildActors();ShowShelterSupplies();await RenderFrames(3);SaveStore.Validate(Sim.State);
            Check("legacy catalogue fixture retains open shelter",!Sim.State.EconomyProgression&&Sim.State.CreatureSalesRevenue==0&&!Buy().Disabled&&!Labels().Any(l=>l.Contains("元解锁")));
            await GeneralParity("legacy",false);money=Sim.State.Money;stock=Sim.State.ShelterStock;Click(_panelContents,Buy().Text);await RenderFrames(3);
            Check("legacy purchase still works",Sim.State.Money==money-45&&Sim.State.ShelterStock==stock+1);SaveStore.Validate(Sim.State);
            Sim.State.Settings.Pause=true;ShowShelterSupplies();await RenderFrames(3);Click(_panelContents,"返回小院");await RenderFrames(3);ShowShelterSupplies();await RenderFrames(3);
            Check("paused open close and reopen preserve pause without modal",Sim.State.Settings.Pause&&!_modalOpen);Snapshot("legacy final");
        }
        catch(Exception ex){failures++;results.Add(new{name="harness completed",passed=false,error=ex.ToString()});GD.PushError(ex.ToString());}
        File.WriteAllText(output,JsonSerializer.Serialize(new{success=failures==0,failures,checks=results.Count,results,snapshots,scope="Headless production scene/button signals; default baseline, production sale progress, labelled controlled boundaries and legacy fixtures, production placement mapping. No native mouse or visual acceptance."},new JsonSerializerOptions{WriteIndented=true}));
        _sound.Stop();_sound.Stream=null;_roomMusic.Stop();_roomMusic.Stream=null;await RenderFrames(3);GetTree().Quit(failures==0?0:1);
    }
}

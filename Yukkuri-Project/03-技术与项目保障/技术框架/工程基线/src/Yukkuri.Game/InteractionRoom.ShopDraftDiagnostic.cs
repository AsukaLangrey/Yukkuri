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
    private static void GuardShopDraftDiagnostic(string[] args)
    {
        const string flag="--shop-draft-audit-output";
        if(!args.Contains(flag))return;
        int p=Array.IndexOf(args,"--interaction-test-profile"),o=Array.IndexOf(args,flag);
        if(DisplayServer.GetName()!="headless"||args.Length!=4||p<0||o<0||p+1>=args.Length||o+1>=args.Length||p==o||
           args.Count(a=>a==flag)!=1||args.Count(a=>a=="--interaction-test-profile")!=1||
           !System.Text.RegularExpressions.Regex.IsMatch(args[p+1],@"\Ashop-draft-[a-zA-Z0-9_-]{1,48}\z")||!Path.IsPathFullyQualified(args[o+1]))
            throw new ArgumentException("Requires only headless audit, fresh profile and absolute output; no other flags.");
        string save=Path.GetFullPath(ProjectSettings.GlobalizePath("user://interaction-test-saves/"+args[p+1]));
        string root=Path.GetFullPath(ProjectSettings.GlobalizePath("user://interaction-test-saves"));
        string output=Path.GetFullPath(args[o+1]),directory=Path.GetDirectoryName(output)!;
        string allowed=Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../.local/testing"))+Path.DirectorySeparatorChar;
        if(Path.GetDirectoryName(save)!=root||Directory.Exists(save)||File.Exists(save)||Directory.Exists(directory)||File.Exists(output)||!directory.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Requires never-used isolated save and output directories.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"guard.json"),JsonSerializer.Serialize(new{save,output,guardBeforeSaveStore=true}));
        GD.Print("SHOP_DRAFT_GUARD: "+save);
    }
    private void StartShopDraftDiagnostic(string[] args)
    {
        int i=Array.IndexOf(args,"--shop-draft-audit-output");if(i<0)return;
        SetProcess(false);_autoClock=-10000;SetRoomMuted(true);_=AuditShopDraft(args[i+1]);
    }
    private async Task AuditShopDraft(string output)
    {
        var results=new List<object>();var snapshots=new List<object>();int failures=0;
        void Check(string name,bool passed){results.Add(new{name,passed});if(!passed)failures++;GD.Print((passed?"PASS ":"FAIL ")+name);}
        void Require(bool b,string m){if(!b)throw new InvalidOperationException(m);}
        void Click(Node root,string label){var b=root.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==label&&b.IsVisibleInTree());Require(!b.Disabled,"Disabled "+label);b.EmitSignal(BaseButton.SignalName.Pressed);}
        InlineChoice Category()=>_panelContents.GetChildren().OfType<HBoxContainer>().SelectMany(r=>r.GetChildren().OfType<InlineChoice>()).Single();
        (ShopProduct p,SpinBox q,Button buy)[] Rows(){string category=Category().GetItemText(Category().Selected);var products=CourtyardShop.Products.Where(p=>p.Category==category).ToArray();var rows=_panelContents.GetChildren().OfType<HBoxContainer>().Where(r=>r.GetChildren().OfType<SpinBox>().Any()).ToArray();Require(rows.Length==products.Length,"rows");return rows.Select((r,i)=>(products[i],r.GetChildren().OfType<SpinBox>().Single(),r.GetChildren().OfType<Button>().Single())).ToArray();}
        string Ledger()=>JsonSerializer.Serialize(new{Sim.State.Money,stock=CourtyardShop.Products.ToDictionary(p=>p.Id,p=>Sim.ShopQuantity(p.Id)),owned=CourtyardShop.Products.ToDictionary(p=>p.Id,p=>Sim.ShopOwnedQuantity(p.Id)),Sim.State.CreatureSalesRevenue,ids=Sim.State.Creatures.Select(c=>c.Id)},SaveStore.JsonOptions);
        void Snapshot(string stage){snapshots.Add(new{stage,category=Category().GetItemText(Category().Selected),rows=Rows().Select(r=>new{r.p.Id,quantity=r.q.Value,price=r.buy.Text,r.buy.Disabled}).ToArray(),cache=new Dictionary<string,double>(_shopQuantities),ledger=Ledger()});File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"checkpoint.json"),JsonSerializer.Serialize(new{results,snapshots},new JsonSerializerOptions{WriteIndented=true}));}
        async Task Switch(string category,string direction="›"){for(int i=0;i<10&&Category().GetItemText(Category().Selected)!=category;i++){Click(Category(),direction);await RenderFrames(3);}Require(Category().GetItemText(Category().Selected)==category,"category not reached");}
        bool Quant(params double[] values)=>Rows().Select(r=>r.q.Value).SequenceEqual(values);
        bool Prices()=>Rows().All(r=>r.buy.Text==$"购买 · 总价 {r.p.Price*(int)r.q.Value} 元");
        try
        {
            await RenderFrames(3);string original=Ledger();
            Click(_ui,"商店");await RenderFrames(3);await Switch("食物与饮水");Check("new yard food defaults",Quant(1,1));
            Rows()[0].q.Value=7;Rows()[1].q.Value=8;Snapshot("food edited 7,8");
            Click(_panelContents,"返回小院");await RenderFrames(3);Click(_ui,"商店");await RenderFrames(3);
            Check("same category return retains product drafts",Quant(7,8));Check("same category prices",Prices());Snapshot("same category return");
            await Switch("食水供给设施");Check("direct category switch defaults untouched products",Quant(1,1));Snapshot("direct facilities");
            await Switch("食物与饮水");Check("direct return preserves food drafts",Quant(7,8));
            Click(_panelContents,"返回小院");await RenderFrames(3);
            Check("public missing inventory route available",Sim.State.FoodStationStock==0&&Sim.State.WaterStationStock==0);
            Click(_groupList,"食物");await RenderFrames(3);Click(_choiceList,"食物供给器");await RenderFrames(3);
            Require(ActivityPanelVisible&&Category().GetItemText(Category().Selected)=="食水供给设施","actual missing-stock route did not open facilities");
            Snapshot("missing inventory facilities reopen");
            Check("cross category return leaves never-edited facilities at 1",Quant(1,1));
            Check("cross category return does not corrupt product cache",!_shopQuantities.ContainsKey("food-station")&&!_shopQuantities.ContainsKey("water-station"));
            Check("cross category prices match quantities",Prices());Check("navigation and drafts never debit or change inventory",Ledger()==original);
            Rows()[0].q.Value=3;Rows()[1].q.Value=4;Snapshot("facilities intentionally edited 3,4");
            await Switch("食物与饮水");Check("editing facilities does not alter food quantities",Quant(7,8));
            await Switch("食水供给设施");Check("direct switch returns separately edited facilities",Quant(3,4));
            Click(_panelContents,"返回小院");await RenderFrames(3);Click(_ui,"商店");await RenderFrames(3);Check("same category facilities retain own drafts",Quant(3,4));
            Snapshot("facilities same category return");Check("all drafts leave ledger unchanged",Ledger()==original);SaveStore.Validate(Sim.State);
            bool Affordable()=>Rows().All(r=>r.buy.Disabled==(Sim.ShopUnlockReason(r.p.Id)!=null||Sim.State.Money<(long)r.p.Price*(int)r.q.Value));
            for(int cycle=1;cycle<=3;cycle++)
            {
                await Switch("食物与饮水","‹");Check($"cycle {cycle} backward food drafts and price",Quant(7,8)&&Prices()&&Affordable());
                Click(_panelContents,"返回小院");await RenderFrames(3);
                Click(_groupList,"食物");await RenderFrames(3);Click(_choiceList,"食物供给器");await RenderFrames(3);
                Check($"cycle {cycle} cross category facilities retain own drafts",Quant(3,4)&&Prices()&&Affordable());
                Check($"cycle {cycle} shop input cache excludes owned quantities",!_pageInputs.TryGetValue("商店",out var shopInputs)||shopInputs.Count==0);
                Click(_panelContents,"返回小院");await RenderFrames(3);Click(_ui,"商店");await RenderFrames(3);
                Check($"cycle {cycle} reopen preserves selected category",_shopCategory=="食水供给设施"&&Category().GetItemText(Category().Selected)=="食水供给设施"&&Quant(3,4));
            }
            Click(_panelContents,"购入新油 · 选择场景落点");await RenderFrames(3);
            var arrivalName=_panelContents.GetChildren().OfType<LineEdit>().Single();arrivalName.Text="数量草稿回归";
            foreach(var choice in _panelContents.GetChildren().OfType<InlineChoice>())Click(choice,"›");
            Click(_panelContents,"返回小院");await RenderFrames(3);Click(_ui,"商店");await RenderFrames(3);
            await Switch("食物与饮水","‹");await Switch("食水供给设施");
            Check("forward and backward category edits remain independent",Quant(3,4)&&_shopQuantities["food"]==7&&_shopQuantities["water"]==8);
            await Switch("养护用品");
            // Headless viewport can fit every shop row; force overflow only in this test fixture.
            var originalMinimum=_panelContents.CustomMinimumSize;_panelContents.CustomMinimumSize=new(originalMinimum.X,1600);await RenderFrames(3);
            _panelScroll.ScrollVertical=80;await RenderFrames(3);int savedScroll=_panelScroll.ScrollVertical;
            Check("scroll fixture actually scrolls",savedScroll>0);
            Click(_panelContents,"购入新油 · 选择场景落点");await RenderFrames(3);
            Check("nonshop name draft survives shop navigation",_panelContents.GetChildren().OfType<LineEdit>().Single().Text=="数量草稿回归");
            Check("nonshop selection drafts survive shop navigation",_panelContents.GetChildren().OfType<InlineChoice>().All(c=>c.Selected==1));
            Click(_panelContents,"返回小院");await RenderFrames(3);Click(_ui,"商店");await RenderFrames(3);
            Check("shop scroll survives other panel navigation",_panelScroll.ScrollVertical==savedScroll);
            _panelContents.CustomMinimumSize=originalMinimum;await RenderFrames(3);
            await Switch("食水供给设施");
            Check("other panel navigation preserves quantities and prices",Quant(3,4)&&Prices()&&Affordable());
            Check("extended navigation leaves ledger unchanged",Ledger()==original);
            Snapshot("extended navigation final");
            var old=Sim.State;Click(_ui,"存档与设置");await RenderFrames(3);Click(_modalContents,"重新开一间小院");await RenderFrames(3);Click(_modalContents,"保存并重新开始");await RenderFrames(3);
            Check("real new game replaced world",!ReferenceEquals(old,Sim.State));Check("real new game clears shop and page drafts",_shopQuantities.Count==0&&_pageInputs.Count==0);
            Click(_ui,"商店");await RenderFrames(3);await Switch("食物与饮水");Check("real new game food defaults",Quant(1,1));await Switch("食水供给设施");Check("real new game facility defaults",Quant(1,1));Snapshot("real new game defaults");SaveStore.Validate(Sim.State);
            Click(_panelContents,"购入新油 · 选择场景落点");await RenderFrames(3);Check("real new game clears nonshop name draft",_panelContents.GetChildren().OfType<LineEdit>().Single().Text=="");
        }
        catch(Exception ex){failures++;results.Add(new{name="harness completed",passed=false,error=ex.ToString()});GD.PushError(ex.ToString());}
        File.WriteAllText(output,JsonSerializer.Serialize(new{success=failures==0,failures,checks=results.Count,results,snapshots,scope="Headless actual button signals and SpinBox changes; default yard, no money/unlock fixture, no purchases. Real new-game saves only guarded isolated profile. Not visual acceptance."},new JsonSerializerOptions{WriteIndented=true}));
        _sound.Stop();_sound.Stream=null;_roomMusic.Stop();_roomMusic.Stream=null;await RenderFrames(3);GetTree().Quit(failures==0?0:1);
    }
}
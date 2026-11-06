using System.Text.Json;
using Yukkuri.Simulation;

int passed=0,failed=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action test){try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
var cases=new[]{("wrap",InteractionAction.Wrap,InteractionAction.Unwrap,RestraintKind.Wrap),
    ("frame",InteractionAction.Pin,InteractionAction.Release,RestraintKind.Frame),
    ("box",InteractionAction.Box,InteractionAction.OpenBox,RestraintKind.Box),
    ("hanger",InteractionAction.Hang,InteractionAction.Lower,RestraintKind.Hanging)};
WorldSimulation Installed(string item,InteractionAction install,RestraintKind kind,bool full)
{
    // Synthetic stock boundary only. All other playtest defaults are retained.
    var s=WorldSimulation.CreateInteractionPlaytest();var c=s.Selected!;
    s.State.ShopStock[item]=int.MaxValue;SaveStore.Validate(s.State);
    var result=s.ExecuteInteraction(c.Id,new(install,BodyPart.Side));
    Require(result.Success&&result.Changed&&!result.Refused,"real installation failed: "+result.Message);
    Require(c.Interaction.Restraint==kind&&s.ShopQuantity(item)==int.MaxValue-1,"installation did not debit exactly once");
    if(full){int money=s.State.Money;Require(s.BuyShopProduct(item),"replacement purchase failed");
        Require(s.State.Money==money-CourtyardShop.Products.Single(p=>p.Id==item).Price,"wrong price");}
    SaveStore.Validate(s.State);return s;
}
foreach(var (item,install,release,kind) in cases)
{
    foreach(string route in new[]{"execute","gesture","recycle"})foreach(bool full in new[]{true,false})
    Check($"{item} {route} {(full?"full atomic rejection":"exact capacity and no double return")}",()=>
    {
        var s=Installed(item,install,kind,full);string id=s.Selected!.Id;var c=s.Selected!;
        string before=Snapshot(s);int money=s.State.Money;
        string Act()
        {
            if(route=="recycle")return s.RecycleCourtyardObject(id);
            var r=route=="gesture"?s.BeginInteractionGesture(id,new(release,BodyPart.Side)):s.ExecuteInteraction(id,new(release,BodyPart.Side));
            if(full)Require(!r.Success&&!r.Changed,"full return succeeded or changed");
            else if(c.Interaction.Restraint==RestraintKind.None)Require(r.Success&&r.Changed,"first return failed");
            return r.Message;
        }
        string message=Act();
        if(full){Require(message.Contains("仓库已满"),"missing capacity feedback");Require(Snapshot(s)==before,"rejected return mutated world");
            Require(Act().Contains("仓库已满")&&Snapshot(s)==before,"repeated rejection mutated world");}
        else
        {
            Require(s.ShopQuantity(item)==int.MaxValue&&c.Interaction.Restraint==RestraintKind.None,"return lost item or overflowed");
            if(route=="recycle")Require(c.RegionId=="courtyard-container"&&s.State.SelectedId==null,"recycle did not contain creature");
            if(route=="recycle")s.RecycleCourtyardObject(id);
            else
            {
                var again=route=="gesture"?s.BeginInteractionGesture(id,new(release,BodyPart.Side)):s.ExecuteInteraction(id,new(release,BodyPart.Side));
                Require(!again.Success&&!again.Changed&&!again.Message.Contains("仓库已满"),"duplicate release no longer follows no-installed-item behavior");
            }
            Require(s.ShopQuantity(item)==int.MaxValue,"double return changed stock");
        }
        Require(s.State.Money==money,"return changed money");SaveStore.Validate(s.State);
    });
    foreach(var other in cases.Where(v=>v.Item3!=release))
    Check($"{item} mismatched {other.Item3} keeps original rejection",()=>
    {
        var s=Installed(item,install,kind,true);
        var control=new WorldSimulation(JsonSerializer.Deserialize<WorldState>(Snapshot(s),SaveStore.JsonOptions)!);
        control.State.ShopStock[item]=int.MaxValue-1;
        var r=s.ExecuteInteraction(s.Selected!.Id,new(other.Item3,BodyPart.Side));
        Require(!r.Success&&!r.Message.Contains("仓库已满"),"unrelated action capacity-blocked");
        var expected=control.ExecuteInteraction(control.Selected!.Id,new(other.Item3,BodyPart.Side));
        control.State.ShopStock[item]=int.MaxValue;
        Require(r==expected&&Snapshot(s)==Snapshot(control),"full stock changed normal mismatched-action behavior");SaveStore.Validate(s.State);
    });
    Check($"{item} normal mode and wrong target not capacity-blocked",()=>
    {
        var s=Installed(item,install,kind,true);string id=s.Selected!.Id;
        var r=s.ExecuteInteraction(id,new(release,BodyPart.Side,TargetKind:InteractionTargetKind.Stem));
        Require(!r.Success&&!r.Message.Contains("仓库已满"),"wrong target capacity-blocked");
        s.State.InteractionPlaytest=false;string before=Snapshot(s);
        r=s.ExecuteInteraction(id,new(release,BodyPart.Side));
        Require(!r.Success&&!r.Message.Contains("仓库已满")&&Snapshot(s)==before,"normal mode changed behavior");
    });
    foreach(string route in new[]{"execute","gesture","recycle"})
    Check($"{item} {route} full preserves existing gesture",()=>
    {
        var s=Installed(item,install,kind,true);string id=s.Selected!.Id;
        var started=s.BeginInteractionGesture(id,new(InteractionAction.Stroke,BodyPart.Head));
        Require(started.Success&&s.CurrentInteractionAction==InteractionAction.Stroke,"gesture setup failed");
        string before=Snapshot(s);string? target=s.InteractionGestureTarget;double seconds=s.InteractionGestureSeconds;
        if(route=="gesture")s.BeginInteractionGesture(id,new(release,BodyPart.Side));
        else if(route=="execute")s.ExecuteInteraction(id,new(release,BodyPart.Side));
        else s.RecycleCourtyardObject(id);
        Require(Snapshot(s)==before&&s.CurrentInteractionAction==InteractionAction.Stroke&&s.InteractionGestureTarget==target&&s.InteractionGestureSeconds==seconds,"blocked return cancelled or changed ongoing gesture");
        SaveStore.Validate(s.State);
    });
}
foreach(string route in new[]{"execute","recycle"})
Check($"wrap {route} two fresh disk loads preserve atomic rejection",()=>
{
    var s=Installed("wrap",InteractionAction.Wrap,RestraintKind.Wrap,true);string id=s.Selected!.Id;
    string directory=Path.Combine(Path.GetFullPath(args.Length>0?args[0]:".local/testing/installed-return-capacity"),route);
    var store=new SaveStore(directory);store.Save(s.State,0);
    for(int n=0;n<2;n++){var load=store.Load(0);Require(!load.RecoveredBackup,"unexpected backup recovery");SaveStore.Validate(load.State);s=new WorldSimulation(load.State);SaveStore.Validate(s.State);}
    string before=Snapshot(s);
    if(route=="execute")s.ExecuteInteraction(id,new(InteractionAction.Unwrap,BodyPart.Side));else s.RecycleCourtyardObject(id);
    Require(Snapshot(s)==before,"loaded return mutated world");SaveStore.Validate(s.State);
    store.Save(s.State,1);SaveStore.Validate(store.Load(1).State);
});
Console.WriteLine($"RESULT passed={passed}; failed={failed}");return failed==0?0:1;

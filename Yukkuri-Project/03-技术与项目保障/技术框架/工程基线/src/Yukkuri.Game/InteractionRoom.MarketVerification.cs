using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private void StartMarketVerificationIfRequested(string[] args)
    {
        int index=Array.IndexOf(args,"--economy-check-output");
        if(index>=0&&index+1<args.Length)_=VerifyMarket(Path.GetFullPath(args[index+1]));
    }
    private async Task VerifyMarket(string output)
    {
        try
        {
            _autoClock=-10000;_muted=true;
            await RenderFrames(12);
            Sim.State.Settings.Pause=true;
            ShowShop(category:"食水供给设施");await RenderFrames(3);
            if(_panelContents.FindChildren("*","Button",true,false).OfType<Button>().Where(b=>b.Text.StartsWith("购买 ·")).Any(b=>!b.Disabled))
                throw new Exception("locked tools are purchasable in UI");
            var c=Sim.State.Creatures.Last();Sim.State.SelectedId=c.Id;
            int quote=CourtyardEconomy.Quote(c).Price,money=Sim.State.Money;
            ShowCreatureMarket(confirmingId:c.Id,expectedPrice:quote);await RenderFrames(3);
            _refreshActivityPanel?.Invoke();await RenderFrames(3);
            var confirm=_panelContents.GetChildren().OfType<Button>().Single(b=>b.Text=="确认卖给商店");
            confirm.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(8);
            if(Sim.State.Money!=money+quote||Sim.State.Creatures.Contains(c)||_actors.ContainsKey(c.Id)||!Sim.State.Settings.Pause)
                throw new Exception("sale settlement / actor removal / pause mismatch");
            SaveStore.Validate(Sim.State);
            ShowShop(category:"食物与饮水");await RenderFrames(3);
            var buy=_panelContents.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.StartsWith("购买 ·"));
            int food=Sim.State.Food;buy.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(3);
            if(Sim.State.Food<=food)throw new Exception("sale revenue could not buy food");
            CloseActivityPanel();Sim.State.Settings.Pause=false;await RenderFrames(20);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,"PASS: real scene, locked shop controls, confirmation refresh, sale payment, actor removal, pause preservation, food purchase, save validation.\nRenderer: "+DisplayServer.GetName());
            GetTree().Quit();
        }
        catch(Exception e)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,"FAIL: "+e);GD.PushError(e.ToString());GetTree().Quit(1);
        }
    }
}

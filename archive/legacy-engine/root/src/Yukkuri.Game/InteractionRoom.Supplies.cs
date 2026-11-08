using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private Label _supplyStockLabel=null!;
    private void RefreshSupplyStock()
    {
        if(_supplyStockLabel!=null)_supplyStockLabel.Text=$"余额 {Sim.State.Money}元\n食物 {Sim.State.Food} · 清水 {Sim.State.StationWaterPortions}\n护理 {Sim.State.Medicine}";
    }
    private bool SupplyStockMissing(InteractionAction action)=>action switch
    {
        InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Tease=>Sim.State.Food<=0,
        InteractionAction.Drink=>Sim.State.StationWaterPortions<=0,
        InteractionAction.Care or InteractionAction.SupportStem or InteractionAction.UnbottleStem=>Sim.State.Medicine<=0,
        InteractionAction.BottleStem=>Sim.State.StemVessels<=0,
        _=>false
    };
    private void ShowMissingToolSupplies(InteractionAction action)=>ShowShop("所选物品库存不足，请购买后再拿取。");
    private void ShowSuppliesOverview(string? result=null,InteractionAction? resumeAction=null)=>ShowShop(result);
    private void ShowCourtshipSupplies(string? result=null)=>ShowShop(result,"繁育与油茎用品");
}
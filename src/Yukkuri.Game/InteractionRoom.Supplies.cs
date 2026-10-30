using Godot;
using System;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private Label _supplyStockLabel=null!;

    private void RefreshSupplyStock()
    {
        if(_supplyStockLabel==null)return;
        _supplyStockLabel.Text=Sim.UsesConsumableStock?$"食物 {Sim.State.Food} · 护理 {Sim.State.Medicine}":"食物与护理：沙盒不限量";
        _supplyStockLabel.TooltipText=$"救援用品 {Sim.State.WaterRescueTools} · 空瓶 {Sim.State.StemVessels} · 诱导用品 {Sim.State.CourtshipTools}";
    }

    private bool SupplyStockMissing(InteractionAction action)=>action switch
    {
        InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Tease=>Sim.UsesConsumableStock&&Sim.State.Food<=0,
        InteractionAction.Care=>Sim.UsesConsumableStock&&Sim.State.Medicine<=0,
        InteractionAction.SupportStem=>Sim.State.Medicine<=0,
        InteractionAction.BottleStem=>Sim.State.StemVessels<=0,
        _=>false
    };

    private void ShowMissingToolSupplies(InteractionAction action)
    {
        if(action is InteractionAction.Care or InteractionAction.SupportStem)ShowMedicineSupplies("手边没有护理包，先补充用品。",action);
        else if(action==InteractionAction.BottleStem)ShowStemVessels("手边没有空瓶，先补充一个。");
        else ShowSuppliesOverview("食物已经用完，补充后再拿起来。",action);
    }

    private void ShowSuppliesOverview(string? result=null,InteractionAction? resumeAction=null)
    {
        OpenModal("用品补给");
        _modalContents.AddChild(Text("手边的食物与照护用品",20));
        _modalContents.AddChild(Text(Sim.State.UnlimitedResources?
            "测试阶段购买不扣钱；本小院喂食、护理和救援仍消耗实际用品。":$"现有金钱：{Sim.State.Money} 元"));
        if(!string.IsNullOrEmpty(result))_modalContents.AddChild(Text(result,14));
        void Item(string name,int stock,int count,int price,Func<bool> purchase,bool hasSpace)
        {
            var row=Row(_modalContents,8);
            var label=Text($"{name} · 库存 {stock}",14);label.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;row.AddChild(label);
            string cost=Sim.State.UnlimitedResources?"测试免费":$"{price} 元";
            var buy=Button($"补充 {count} · {cost}",()=>
            {
                bool success=purchase();
                ShowSuppliesOverview(success?$"{name}已入库 {count} 份。":"未能补充，请检查金钱与库存；原用品没有减少。",resumeAction);
                RefreshSupplyStock();
            });
            buy.Disabled=!hasSpace||!Sim.State.UnlimitedResources&&Sim.State.Money<price;row.AddChild(buy);
        }
        Item("饭饭 / 零食",Sim.State.Food,WorldSimulation.FoodBundleCount,WorldSimulation.FoodBundlePrice,Sim.BuyFood,Sim.State.Food<=int.MaxValue-WorldSimulation.FoodBundleCount);
        Item("护理包",Sim.State.Medicine,WorldSimulation.MedicineBundleCount,WorldSimulation.MedicineBundlePrice,Sim.BuyMedicine,Sim.State.Medicine<=int.MaxValue-WorldSimulation.MedicineBundleCount);
        Item("救援用品",Sim.State.WaterRescueTools,1,WaterEnvironment.RescueToolPrice,Sim.BuyWaterRescueTool,Sim.State.WaterRescueTools<99);
        Item("空瓶",Sim.State.StemVessels,1,WorldSimulation.StemVesselPrice,Sim.BuyStemVessel,Sim.State.StemVessels<99);
        Item("繁育诱导用品",Sim.State.CourtshipTools,1,WorldSimulation.CourtshipToolPrice,Sim.BuyCourtshipTool,Sim.State.CourtshipTools<99);
        _modalContents.AddChild(Text("清水可直接取用。买到用品后仍需拿工具，在场景中实际递送、护理或救援。",13,"78816e"));
        if(resumeAction is { } action)
        {
            var equip=Button("拿起刚才选择的工具",()=>{CloseModal();SelectQuickTool(action);});
            equip.Disabled=SupplyStockMissing(action);_modalContents.AddChild(equip);
        }
        _modalContents.AddChild(Button("返回小院",CloseModal));
    }

    private void ShowCourtshipSupplies(string? result=null)
    {
        OpenModal("繁育诱导用品");
        int price=WorldSimulation.CourtshipToolPrice;
        _modalContents.AddChild(Text($"普通诱导用品 · 每份 {price} 元",20));
        _modalContents.AddChild(Text("在家庭面板选择成年油，再使用用品。它会短暂提高繁育兴趣；是否愿意配对仍取决于关系、身体和安全感。"));
        _modalContents.AddChild(Text(Sim.State.UnlimitedResources?"测试阶段：无限金钱，购买不扣钱；使用仍消耗库存。":$"现有金钱：{Sim.State.Money} 元"));
        _modalContents.AddChild(Text($"诱导用品库存：{Sim.State.CourtshipTools} / 99 份"));
        if(!string.IsNullOrEmpty(result))_modalContents.AddChild(Text(result,14));
        var buy=Button("购买1份",()=>
        {
            bool purchased=Sim.BuyCourtshipTool();
            ShowCourtshipSupplies(purchased?"已入库1份诱导用品。":Sim.State.CourtshipTools>=99?"库存已满，先使用现有用品。":
                !Sim.State.UnlimitedResources&&Sim.State.Money<price?"金钱不足，尚未购买。":"当前不能购买，库存与金钱未改变。");
        });
        buy.Disabled=Sim.State.CourtshipTools>=99||!Sim.State.UnlimitedResources&&Sim.State.Money<price;
        _modalContents.AddChild(buy);
        _modalContents.AddChild(Button("返回同伴与家庭",ShowFamily));
        _modalContents.AddChild(Button("返回小院",CloseModal));
    }
}

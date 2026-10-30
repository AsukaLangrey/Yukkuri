using Yukkuri.Simulation;

public partial class InteractionRoom
{
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

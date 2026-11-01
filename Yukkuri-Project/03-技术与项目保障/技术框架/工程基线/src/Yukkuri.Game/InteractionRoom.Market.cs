using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private void ShowCreatureMarket(string? result=null,string? confirmingId=null,int expectedPrice=0)
    {
        OpenActivityPanel("商店收购",()=>ShowCreatureMarket(result,confirmingId,expectedPrice));
        _panelContents.AddChild(Text($"余额 {Sim.State.Money} 元 · 累计售油 {Sim.State.CreatureSalesRevenue} 元",16));
        _panelContents.AddChild(Text("商店按底价立即收购。素质、能力、牌照和年龄决定核心价值，身体与精神状态只作修正。",14));
        if(result!=null)_panelContents.AddChild(Text(result,14));
        var oils=Sim.State.Creatures.Where(c=>c.RegionId==Sim.State.ActiveRegionId).ToArray();
        var choice=new InlineChoice();foreach(var oil in oils)choice.AddItem(OilListName(oil));
        choice.Select(Math.Max(0,Array.FindIndex(oils,c=>c.Id==Sim.State.SelectedId)));
        choice.ItemSelected+=index=>{Sim.State.SelectedId=oils[(int)index].Id;ShowCreatureMarket();};
        _panelContents.AddChild(choice);
        if(Sim.Selected is { } c)
        {
            var q=CourtyardEconomy.Quote(c);
            _panelContents.AddChild(Text($"{c.Name} · {(c.IsYoung?"幼体":"成体")} · {c.Registration.Grade}",18));
            _panelContents.AddChild(Text($"素质 {q.Quality:F0}/100 · 能力 {q.Ability:F0}/100\n年龄基础价 {q.AgeValue:F0} · 核心估值 {q.CoreValue:F1} 元",14));
            _panelContents.AddChild(Text($"清洁 ×{q.Clean:F2} · 精神 ×{q.Spirit:F2} · 健康 ×{q.Health:F2}\n饥饱 ×{q.Food:F2} · 水分 ×{q.Water:F2}（安全线均为25）",14));
            _panelContents.AddChild(Text($"商店收购价 {q.Price} 元",20));
            if(c.IsYoung)_panelContents.AddChild(Text($"成长 {Math.Min(100,c.Body.AgeSeconds/FamilyRules.YoungSeconds*100):F0}% · 满30分钟模拟时间后，还需饥饱>35、健康>50、保暖>40。",13));
            _panelContents.AddChild(Text("留种可繁衍后代；好素质更容易遗传。年龄溢价会随成长下降，实际能力和牌照可提高价值。",13));
            if(Sim.CreatureSaleReason(c.Id) is { } reason)_panelContents.AddChild(Text(reason,14));
            else if(confirmingId==c.Id)
            {
                _panelContents.AddChild(Text($"确认出售 {c.Name}，报价 {expectedPrice} 元？成交后离开小院，血缘和交易档案保留。",14));
                _panelContents.AddChild(PanelButton("确认卖给商店",()=>{string message=Sim.SellCreatureToShop(c.Id,expectedPrice);ShowCreatureMarket(message);RefreshRoomUi();}));
                _panelContents.AddChild(PanelButton("取消出售",()=>ShowCreatureMarket()));
            }
            else _panelContents.AddChild(PanelButton("出售 · 查看确认",()=>ShowCreatureMarket(confirmingId:c.Id,expectedPrice:q.Price)));
        }
        foreach(var sale in Sim.State.CreatureSales.TakeLast(5).Reverse())
            _panelContents.AddChild(Text($"已售 {sale.Creature.Name} · +{sale.Amount} 元",13));
        _panelContents.AddChild(PanelButton("购买补给与道具",()=>ShowShop()));
    }
}

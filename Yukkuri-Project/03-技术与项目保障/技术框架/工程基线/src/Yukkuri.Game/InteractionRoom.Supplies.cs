using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private HBoxContainer _resourceBar=null!;
    private readonly System.Collections.Generic.List<Label> _resourceValues=new();
    private void BuildResourceBar()
    {
        _resourceBar=new HBoxContainer{MouseFilter=Control.MouseFilterEnum.Ignore};
        _resourceBar.AddThemeConstantOverride("separation",18);
        _ui.AddChild(_resourceBar);
        foreach(var icon in new[]{"coin","food","water","care"})
        {
            var row=new HBoxContainer{MouseFilter=Control.MouseFilterEnum.Ignore};
            row.AddThemeConstantOverride("separation",5);_resourceBar.AddChild(row);
            row.AddChild(new TextureRect{
                Texture=GD.Load<Texture2D>($"res://Assets/Icons/Actions/{icon}.svg"),
                CustomMinimumSize=new(18,18),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
                Modulate=new Color("405247"),MouseFilter=Control.MouseFilterEnum.Ignore});
            var label=Text("",14);label.AutowrapMode=TextServer.AutowrapMode.Off;
            label.MouseFilter=Control.MouseFilterEnum.Ignore;
            label.AddThemeColorOverride("font_outline_color",new Color("e6ebd9"));
            label.AddThemeConstantOverride("outline_size",3);
            row.AddChild(label);_resourceValues.Add(label);
        }
        RefreshSupplyStock();
    }
    private void LayoutResourceBar()=>LayoutCalendarStrip();
    private void RefreshSupplyStock()
    {
        if(_resourceValues.Count!=4)return;
        var values=new[]{Sim.State.Money,Sim.State.Food,Sim.State.StationWaterPortions,Sim.State.Medicine};
        for(int i=0;i<values.Length;i++)_resourceValues[i].Text=values[i].ToString();
        float daylight=(float)CourtyardCalendar.At(Sim.State.WorldSeconds).Daylight;
        Color ink=new Color("fff8e6").Lerp(new Color("405247"),daylight);
        Color outline=new Color("253b32").Lerp(new Color("e6ebd9"),daylight);
        for(int i=0;i<_resourceValues.Count;i++)
        {
            _resourceValues[i].AddThemeColorOverride("font_color",ink);
            _resourceValues[i].AddThemeColorOverride("font_outline_color",outline);
            ((TextureRect)_resourceBar.GetChild(i).GetChild(0)).Modulate=ink;
        }
        LayoutResourceBar();
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

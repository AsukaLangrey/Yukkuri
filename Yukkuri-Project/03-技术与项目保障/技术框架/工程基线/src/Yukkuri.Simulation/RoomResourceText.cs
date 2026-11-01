namespace Yukkuri.Simulation;

/// <summary>Player-facing observations, never a creature's hidden knowledge or exact risk.</summary>
public static class RoomResourceText
{
    public static string Label(RoomObject obj,WorldState? state=null)
    {
        if(obj.Portions==0&&state!=null&&NaturalResources.Consumer(state,obj)!=null)
            return obj.Kind switch
            {
                RoomObjectKind.Leaves=>"叶簇 · 最后一口还没吃完",
                RoomObjectKind.Meal=>"饭饭 · 最后一口还没吃完",
                RoomObjectKind.Sweet=>"甜甜 · 最后一口还没吃完",
                RoomObjectKind.Water=>"水杯 · 最后一口还没喝完",
                _=>""
            };
        return obj.Kind switch
    {
        RoomObjectKind.Leaves=>obj.Portions>0?"叶簇 · 可以自己啃":"叶簇 · 已经吃空",
        RoomObjectKind.RiverWater=>"河岸 · 可以靠岸喝水",
        RoomObjectKind.Meal=>obj.Portions>0?"饭饭 · 空手按住拿起":"饭饭 · 已经吃空",
        RoomObjectKind.Sweet=>obj.Portions>0?"甜甜 · 空手按住拿起":"甜甜 · 已经吃空",
        RoomObjectKind.Water=>obj.Portions>0?"水杯 · 空手按住拿起":"水杯 · 已经喝空",
        RoomObjectKind.Ball=>state!=null&&RoomBallEnvironment.InWater(state,obj)?"软球 · 漂在水上":"软球 · 空手拖动，松开推球",
        RoomObjectKind.Treasure=>"小宝物",
        _=>""
        };
    }
    public static string Describe(RoomObject obj,WorldState? state=null)=>obj.Kind switch
    {
        RoomObjectKind.Leaves when obj.Portions==0&&state!=null&&NaturalResources.Consumer(state,obj)!=null=>"这一簇的最后一口还没吃完，暂时没有多余的叶子。",
        RoomObjectKind.Leaves=>obj.Portions>0?"这里还有可食的叶子。油饿了会自己来找，但野外叶子不如备好的饭饭稳妥，吃后可能不舒服。":"这簇叶子已经吃空，只剩残叶；需要再找别的食物。",
        RoomObjectKind.RiverWater=>"这里可以站在岸边喝水。河水未经处理，喝后可能不舒服；饮水不需要下河。",
        RoomObjectKind.Ball when state!=null&&RoomBallEnvironment.InWater(state,obj)=>"软球被水托住，慢慢漂着。油会留在岸边；空手按住球，朝岸边拖动后松开，可以把它推回来。",
        _=>Label(obj,state)
    };
}

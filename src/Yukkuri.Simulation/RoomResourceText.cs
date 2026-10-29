namespace Yukkuri.Simulation;

/// <summary>Player-facing observations, never a creature's hidden knowledge or exact risk.</summary>
public static class RoomResourceText
{
    public static string Label(RoomObject obj,WorldState? state=null)
    {
        if(obj.Portions==0&&state!=null&&NaturalResources.Consumer(state,obj)!=null)
            return obj.Kind==RoomObjectKind.Water?"水杯 · 正在喝最后一口":"食物 · 正在吃最后一口";
        return obj.Kind switch
    {
        RoomObjectKind.Leaves=>obj.Portions>0?"叶簇 · 可以自己啃":"叶簇 · 已经吃空",
        RoomObjectKind.RiverWater=>"河岸 · 可以靠岸喝水",
        RoomObjectKind.Meal=>"饭饭 · 按住拿起",
        RoomObjectKind.Sweet=>"甜甜 · 按住拿起",
        RoomObjectKind.Water=>"水杯 · 按住拿起",
        RoomObjectKind.Ball=>state!=null&&RoomBallEnvironment.InWater(state,obj)?"软球 · 漂在水上":"软球 · 按住后拖出方向",
        RoomObjectKind.Treasure=>"小宝物",
        _=>""
        };
    }
    public static string Describe(RoomObject obj,WorldState? state=null)=>obj.Kind switch
    {
        RoomObjectKind.Leaves when obj.Portions==0&&state!=null&&NaturalResources.Consumer(state,obj)!=null=>"这一簇已经没有下一口了，它还在慢慢嚼最后一点叶子。",
        RoomObjectKind.Leaves=>obj.Portions>0?"这里还有可食的叶子。油饿了会自己来找，但野外叶子不如备好的饭饭稳妥，吃后可能不舒服。":"这簇叶子已经吃空，只剩残叶；需要再找别的食物。",
        RoomObjectKind.RiverWater=>"这里可以站在岸边喝水。河水未经处理，喝后可能不舒服；饮水不需要下河。",
        RoomObjectKind.Ball when state!=null&&RoomBallEnvironment.InWater(state,obj)=>"软球被水托住，慢慢漂着。油会留在岸边；按住球，仍可以把它朝岸边推回来。",
        _=>Label(obj,state)
    };
}

using System.Text.Json;
using Yukkuri.Simulation;

// Remote-only check source. Never execute on the original development workstation.
int failures=0;
void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Near(double value,double expected,string reason,double tolerance=1e-7)=>Require(Math.Abs(value-expected)<=tolerance,reason+$" ({value} != {expected})");
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(138731);sim.State.RoomObjects.Clear();
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.Fullness=c.Energy=c.Interaction.Hydration=100;c.Health=100;c.Pain=c.Fear=0;
        c.Body.AgeSeconds=c.InnerAgeSeconds=2000;c.TrueDisposition="gentle";c.Aggression=.08;c.Patience=.7;
        c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;c.Family.NextSocialAt=10000;c.Family.NextCareAt=10000;
        c.X=c.TargetX=n==0?0:n==1?-5:4;c.Z=c.TargetZ=n==1?-5:0;
    }
    return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
(WorldSimulation Sim,Creature Adult,Creature Child,RoomObject Source) CareFixture(bool water)
{
    var sim=Fixture();var adult=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    child.IsYoung=true;child.Body.AgeSeconds=child.InnerAgeSeconds=0;
    child.ParentId=adult.Id;child.Family.MotherId=adult.Id;child.Family.CaregiverIds.Add(adult.Id);
    child.Interaction.FootIntegrity=20; // A dependent child cannot walk over and fetch its own supply.
    child.Fullness=water?100:20;child.Interaction.Hydration=water?20:100;
    var source=new RoomObject{Id=water?"care-source-water":"care-source-food",Kind=water?RoomObjectKind.Water:RoomObjectKind.Meal,
        RegionId=adult.RegionId,X=0,Z=.95,Portions=3};
    sim.State.RoomObjects.Add(source);
    sim.ArrangeCare(adult.Id,child.Id);
    Require(adult.Family.CareTargetId==child.Id,"照护请求未进入真实行动");
    return(sim,adult,child,source);
}
RoomObject WaitForParcel(WorldSimulation sim,Creature adult)
{
    for(int n=0;n<120;n++)
    {
        Tick(sim,.25);
        if(adult.Family.CareParcelId is { } id&&sim.State.RoomObjects.Find(o=>o.Id==id&&o.CarrierId==adult.Id) is { } parcel)return parcel;
    }
    throw new Exception("30秒内未从真实食水取出运输份额；"+adult.Activity);
}
int ResourcePortions(WorldSimulation sim,RoomObjectKind kind)=>sim.State.RoomObjects.Where(o=>o.Kind==kind&&!o.Stored).Sum(o=>o.Portions);

Check("冷藏十倍延缓实际年龄与消耗，普通时间仍前进",()=>
{
    var warm=Fixture();var cold=Reload(warm);var a=warm.State.Creatures[0];var b=cold.State.Creatures[0];
    double age=a.Body.AgeSeconds,inner=a.InnerAgeSeconds,full=a.Fullness,water=a.Interaction.Hydration;
    cold.SetColdStorage(b.Id,true);Require(b.Family.ColdStorage.Stored,"未进入冷藏");
    Tick(warm,10);Tick(cold,10);
    Near(a.Body.AgeSeconds-age,10,"普通年龄未推进");Near(b.Body.AgeSeconds-age,1,"冷藏年龄不是十分之一");
    Near(b.InnerAgeSeconds-inner,(a.InnerAgeSeconds-inner)/10,"内馅年龄未同速延缓");
    Near(full-b.Fullness,(full-a.Fullness)/10,"冷藏饥饿未十倍延缓");
    Near(water-b.Interaction.Hydration,(water-a.Interaction.Hydration)/10,"冷藏口渴未十倍延缓");
    Near(cold.State.WorldSeconds,10,"冷藏错误地减慢全世界");
    Require(!cold.ExecuteInteraction(b.Id,new(InteractionAction.Greet)).Success,"冷藏中仍能正常互动");
});
Check("濒危救治时间只延缓，取出和读档不重新派发",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];c.Health=1;sim.Hurt(c,2,"冷藏前重伤",null);
    Require(c.Condition==PhysicalCondition.Critical,"前置未进入濒危");double before=c.CriticalRemaining;
    sim.SetColdStorage(c.Id,true);Tick(sim,20);
    Near(before-c.CriticalRemaining,2,"冷藏把救治期冻结或按正常速度耗尽");
    double saved=c.CriticalRemaining;var loaded=Reload(sim);var copy=loaded.State.Creatures[0];
    Near(copy.CriticalRemaining,saved,"读档重置救治窗口");
    loaded.SetColdStorage(copy.Id,false);Near(copy.CriticalRemaining,saved,"取出重新派发救治期");
    Require(copy.Condition==PhysicalCondition.Critical&&!copy.Family.ColdStorage.Stored,"取出自动治愈或没取出");
});
Check("冷藏有保存期限，到期不能无限暂停生命过程",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];sim.SetColdStorage(c.Id,true);
    // Near-expiry fixture tests the actual boundary without fabricating elapsed biological age.
    c.Family.ColdStorage.Elapsed=WorldSimulation.ColdStorageLimitSeconds-.5;Tick(sim,1);
    Require(!c.Family.ColdStorage.Stored,"空院保存到期仍未取出");
    Near(WorldSimulation.BiologicalTimeScale(c),1,"到期仍维持十倍延缓");
    sim.SetColdStorage(c.Id,true);Require(!c.Family.ColdStorage.Stored,"复苏期立刻重新冷藏");
});
Check("到期没有空位时仍留冷藏但不再延缓，腾空后可以取出",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];sim.SetColdStorage(c.Id,true);
    for(int x=-8;x<=8;x++)for(int z=-8;z<=8;z++)
    {
        string id=$"cold-blocker-{x}-{z}";
        sim.State.Creatures.Add(new(){Id=id,Name="占位",RegionId=c.RegionId,Condition=PhysicalCondition.Dead,
            X=x,Z=z,TargetX=x,TargetZ=z,Body=new(){Id=id+"-body",SpeciesId="reimu"},Registration=new(){IdentityId=id+"-identity"},
            Interaction=new(){Enabled=true,Accessory=new(){Id=id+"-accessory"}}});
    }
    c.Family.ColdStorage.Elapsed=WorldSimulation.ColdStorageLimitSeconds-.5;Tick(sim,1);
    Require(c.Family.ColdStorage.Stored,"满院没有空位却穿透其他身体取出");
    Near(WorldSimulation.BiologicalTimeScale(c),1,"满院导致保存期限无限延长");
    double age=c.Body.AgeSeconds,full=c.Fullness;Tick(sim,2);
    Near(c.Body.AgeSeconds-age,2,"超时无空位仍减慢年龄");
    Near(full-c.Fullness,sim.Config.HungerPerSecond*2,"超时无空位仍减慢代谢");
    sim.State.Creatures.RemoveAll(o=>o.Id.StartsWith("cold-blocker-",StringComparison.Ordinal));sim.SetColdStorage(c.Id,false);
    Require(!c.Family.ColdStorage.Stored,"腾出空间仍不能取出");
});
Check("冷藏中死者仍能取出，不能复活或再次制造死亡",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];sim.SetColdStorage(c.Id,true);
    c.FillingIntegrity=1;sim.Hurt(c,30,"冷藏中不可逆重伤",null);
    Require(c.Condition==PhysicalCondition.Dead,"前置未死亡");
    int deaths=sim.State.Incidents.Count(e=>e.Kind==IncidentKind.Death&&e.TargetId==c.Id);
    sim.SetColdStorage(c.Id,false);
    Require(!c.Family.ColdStorage.Stored&&c.Condition==PhysicalCondition.Dead,"死者永久困在冷藏或被复活");
    sim.SetColdStorage(c.Id,false);Tick(sim,1);
    Require(sim.State.Incidents.Count(e=>e.Kind==IncidentKind.Death&&e.TargetId==c.Id)==deaths&&deaths==1,"取出重放死亡结算");
});
Check("暂停与近看不推进冷藏期限，原暂停不改变保存状态",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];sim.State.Settings.Pause=true;sim.SetColdStorage(c.Id,true);
    Require(!c.Family.ColdStorage.Stored,"暂停仍进入冷藏");
    sim.State.Settings.Pause=false;sim.SetColdStorage(c.Id,true);double age=c.Body.AgeSeconds;
    sim.State.Mode=PlayMode.CloseUp;Tick(sim,20);
    Require(c.Family.ColdStorage.Elapsed==0&&c.Body.AgeSeconds==age,"近看推进冷藏期限或年龄");
    sim.State.Mode=PlayMode.Management;sim.State.Settings.Pause=true;sim.SetColdStorage(c.Id,false);Tick(sim,20);
    Require(c.Family.ColdStorage.Stored&&c.Family.ColdStorage.Elapsed==0,"暂停仍取出或推进冷藏");
});
Check("亲代从实际食水分出一份并搬运，不隔空增加幼体需求",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var (sim,adult,child,source)=CareFixture(water);double before=water?child.Interaction.Hydration:child.Fullness;
        var parcel=WaitForParcel(sim,adult);
        Require(parcel.Id!=source.Id&&parcel.Portions==1&&source.Portions==2,"未从有限来源分出真实份额");
        Require(ResourcePortions(sim,source.Kind)==3,"取份时复制或丢失食水");
        Require((water?child.Interaction.Hydration:child.Fullness)<=before,"食水仍在远处就改变了幼体需求");
        Tick(sim,.25);Require(parcel.CarrierId==adult.Id&&parcel.Height>0,"运输份额变为地面物或丢失搬运者");
        Require(WorldSimulation.Distance(parcel.X,parcel.Z,adult.X,adult.Z)<1,"食水没有跟随照护者");
        for(int n=0;n<160&&parcel.CarrierId!=null;n++)Tick(sim,.25);
        Require(parcel.CarrierId==null&&WorldSimulation.Distance(parcel.X,parcel.Z,child.X,child.Z)<NaturalResources.Reach(child)+.05,"食水没有实际送到幼体可取范围");
        for(int n=0;n<80&&(water?child.Interaction.Hydration:child.Fullness)<=before;n++)Tick(sim,.25);
        Require((water?child.Interaction.Hydration:child.Fullness)>before,"送达后幼体没有真实吃喝");
    }
});
Check("用户拿走运输份额立即解除亲代占用，放下不能复制",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var (sim,adult,child,source)=CareFixture(water);var parcel=WaitForParcel(sim,adult);
        string parcelId=parcel.Id;
        Require(sim.HoldSupply(parcelId),"不能从亲代拿走实际食水");
        Require(parcel.HeldByHand&&parcel.CarrierId==null,"手持后仍被亲代占用");
        Require(ResourcePortions(sim,source.Kind)==3,"拿走时复制份额");
        Require(sim.ReleaseSupply(parcelId,-4,0),"不能放到干燥空地");Tick(sim,.25);
        Require(!parcel.HeldByHand&&parcel.CarrierId==null&&sim.State.RoomObjects.Count(o=>o.Id==parcelId)==1,"松手后旧亲代重新占有或物品复制");
        Near(parcel.X,-4,"亲代把已放下食水瞬移回去");Near(parcel.Z,0,"亲代改写放置位置");
        Require(ResourcePortions(sim,source.Kind)==3,"手与亲代交接后份额不守恒");
    }
});
Check("搬运中存档继续保留唯一份额与照护来源",()=>
{
    var (sim,adult,child,source)=CareFixture(false);var parcel=WaitForParcel(sim,adult);Tick(sim,.25);
    var loaded=Reload(sim);var copiedParcel=loaded.State.RoomObjects.Single(o=>o.Id==parcel.Id);
    Require(copiedParcel.CarrierId==adult.Id&&ResourcePortions(loaded,source.Kind)==3,"存读丢失实际搬运关系或份额");
    Tick(sim,1);Tick(loaded,1);
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(loaded.State,SaveStore.JsonOptions),"搬运同档继续不一致");
    Require(loaded.State.Creatures.Single(c=>c.Id==child.Id).Family.MotherId==adult.Id,"照护过程改写血缘");
});
Environment.ExitCode=failures==0?0:1;

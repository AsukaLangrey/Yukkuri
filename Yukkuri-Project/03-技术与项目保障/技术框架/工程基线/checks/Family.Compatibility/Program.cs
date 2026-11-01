using System.Text.Json;
using Yukkuri.Simulation;

// Source only on the original workstation. Execute only on the authorised independent remote host.
int failures=0;
string[] species=["reimu","marisa","alice","patchouli","remilia","flandre"];
BirthMethod[] methods=[BirthMethod.Stem,BirthMethod.Live];
var successful=new Dictionary<(string Mother,string Father,BirthMethod Method),(WorldSimulation Sim,ulong Seed)>();
var unsuccessful=new Dictionary<(string Mother,string Father,BirthMethod Method),ulong>();
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
bool Predator(string id)=>id is "remilia" or "flandre";
bool Cross(string mother,string father)=>Predator(mother)!=Predator(father);
WorldSimulation Fixture(string maternal,string paternal,ulong seed=1)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(unchecked(seed*11400714819323198485UL));
    sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAt(2);
    for(int n=0;n<2;n++)
    {
        var c=sim.State.Creatures[n];c.Body.SpeciesId=n==0?maternal:paternal;
        // These are two different adults with fixed body roles, not one identity switching roles.
        c.Body.ReproductionRole=n==0?ReproductionRole.Maternal:ReproductionRole.Paternal;
        c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=c.Health=100;c.Trust=80;c.Fear=c.Pain=c.Resentment=0;
        c.TrueDisposition="gentle";c.Aggression=.08;c.Patience=.6;c.Courage=.2;
        c.X=c.TargetX=n*3;c.Z=c.TargetZ=0;c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    }
    var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];
    a.Family.Peers[b.Id]=new(){Familiarity=90,Affection=90};
    b.Family.Peers[a.Id]=new(){Familiarity=90,Affection=90};
    return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void Request(WorldSimulation sim,BirthMethod method)
{
    var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    string response=sim.RequestPregnancy(m.Id,f.Id,method);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"普通安排未进入准备期："+response);
    Require(!m.Family.Pregnancy.Forced&&m.Family.Pregnancy.EventId==null&&sim.State.Creatures.Count==2,"普通安排偷换强制或直接生成孕育/宝宝");
    Require(m.Body.ReproductionRole==ReproductionRole.Maternal&&f.Body.ReproductionRole==ReproductionRole.Paternal,"安排改写出生角色");
}
bool Settle(WorldSimulation sim,BirthMethod method)
{
    var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    for(int n=0;n<120&&m.Family.Pregnancy.Phase==PregnancyPhase.Preparing;n++)Tick(sim,.25);
    var p=m.Family.Pregnancy;
    Require(p.Phase is PregnancyPhase.None or PregnancyPhase.Gestating,"30秒内没有完成普通准备："+m.Family.LastEvent+" / "+m.Activity);
    Require(m.Family.PartnerIds.Contains(f.Id)&&f.Family.PartnerIds.Contains(m.Id),"未完成真实相处与接受就返回配对结果");
    Require(p.Method==method&&!p.Forced&&sim.State.Creatures.Count==2,"普通过程改变方式、偷用强制或提前出生");
    if(p.Phase==PregnancyPhase.Gestating)
    {
        Require(p.EventId!=null&&p.FatherId==f.Id&&p.PlannedChildren>0,"成功没有实际孕育事件和来源");
        return true;
    }
    Require(p.EventId==null&&p.PlannedChildren==0&&p.RetryAt>sim.State.WorldSeconds,"受孕失败仍生成出生数据或允许即时重抽");
    return false;
}
bool Attempt(WorldSimulation sim,BirthMethod method)
{
    Request(sim,method);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    double distance=WorldSimulation.Distance(m.X,m.Z,f.X,f.Z);Tick(sim,1);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"省去了接受与准备时间");
    Require(WorldSimulation.Distance(m.X,m.Z,f.X,f.Z)<distance,"未实际靠近，普通准备没有落实到空间行为");
    return Settle(sim,method);
}

foreach(string maternal in species)foreach(string paternal in species)foreach(var method in methods)
{
    Check($"普通入口 {maternal}母体 × {paternal}父体 · {method}",()=>
    {
        var key=(maternal,paternal,method);bool cross=Cross(maternal,paternal);
        for(ulong seed=1;seed<=(cross?1024UL:1UL);seed++)
        {
            var sim=Fixture(maternal,paternal,seed);bool begun=Attempt(sim,method);
            if(begun&&!successful.ContainsKey(key))successful.Add(key,(sim,seed));
            if(!begun&&!unsuccessful.ContainsKey(key))unsuccessful.Add(key,seed);
            if(successful.ContainsKey(key)&&(!cross||unsuccessful.ContainsKey(key)))break;
        }
        Require(successful.ContainsKey(key),"六种范围内实际普通入口始终无法孕育");
        if(cross)Require(unsuccessful.ContainsKey(key),"跨类被写成必定受孕");
        Console.WriteLine($"  reached seed={successful[key].Seed}"+(cross?$", failed seed={unsuccessful[key]}":""));
    });
}
Check("跨类极少受孕，相同条件下显著低于普通种内部与捕食种内部",()=>
{
    const int samples=256;
    foreach(var method in methods)
    {
        var counts=new Dictionary<(string,string),int>();
        foreach(var pair in new[]{("reimu","marisa"),("remilia","flandre"),("reimu","remilia"),("remilia","reimu")})
        {
            int count=0;
            for(ulong seed=1;seed<=samples;seed++)if(Attempt(Fixture(pair.Item1,pair.Item2,seed),method))count++;
            counts[pair]=count;Console.WriteLine($"  sample {pair.Item1} × {pair.Item2} {method}: {count}/{samples}");
        }
        int ordinary=counts[("reimu","marisa")],predatory=counts[("remilia","flandre")];
        Require(ordinary>samples/2&&predatory>samples/2,"同类正常兼容失效");
        foreach(var pair in new[]{("reimu","remilia"),("remilia","reimu")})
        {
            int rare=counts[pair];
            // A broad regression ceiling, not a claim that the design's exact chance is 10%.
            Require(rare<samples/10&&rare*5<Math.Min(ordinary,predatory),"跨类没有体现极少且显著更低的受孕率");
            Require(successful.ContainsKey((pair.Item1,pair.Item2,method)),"低概率被实现为绝对禁止");
        }
    }
});
Check("全部跨类方向在相同失养条件下累积负担，普通种母体与捕食种母体分别生效",()=>
{
    Require(successful.Count==72,"普通入口矩阵不完整，不能用人工孕态替代方向比较");
    foreach(string common in species.Take(4))foreach(string predator in species.Skip(4))foreach(var method in methods)
    {
        double AddedBurden(string maternal,string paternal)
        {
            var sim=Reload(successful[(maternal,paternal,method)].Sim);var mother=sim.State.Creatures[0];
            double before=mother.Family.Pregnancy.Burden;mother.Interaction.Hydration=20;
            // Change only the environment/need after genuine conception. Never assign pregnancy or its outcome.
            Tick(sim,10);
            Require(mother.Family.Pregnancy.Phase==PregnancyPhase.Gestating&&mother.Family.Pregnancy.Method==method,"短时失养静默切换方式或跳过孕育");
            return mother.Family.Pregnancy.Burden-before;
        }
        double commonBase=AddedBurden(common,common),predatorBase=AddedBurden(predator,predator);
        double commonMother=AddedBurden(common,predator),predatorMother=AddedBurden(predator,common);
        Require(commonBase>0&&predatorBase>0,"失养没有实际母体负担");
        Require(commonMother>commonBase&&predatorMother>predatorBase,"跨类负担没有高于各自同类对照");
        Require(commonMother>predatorMother,"两种母体方向没有分别体现当前设定的负担差异");
    }
});
Check("六种的母父角色、双方未成熟和单方陌生均由普通入口拒绝",()=>
{
    foreach(string maternal in species)foreach(string paternal in species)foreach(var method in methods)
    foreach(int reason in new[]{0,1,2,3,4,5})
    {
        var sim=Fixture(maternal,paternal);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
        if(reason==0)m.Body.ReproductionRole=ReproductionRole.Paternal;
        if(reason==1)f.Body.ReproductionRole=ReproductionRole.Maternal;
        if(reason==2)m.IsYoung=true;if(reason==3)f.IsYoung=true;
        if(reason==4)m.Family.Peers.Remove(f.Id);if(reason==5)f.Family.Peers.Remove(m.Id);
        var originalM=m.Body.ReproductionRole;var originalF=f.Body.ReproductionRole;
        string response=sim.RequestPregnancy(m.Id,f.Id,method);Tick(sim,1);
        Require(m.Family.Pregnancy.Phase==PregnancyPhase.None&&m.Family.Pregnancy.EventId==null&&sim.State.Creatures.Count==2,
            $"{maternal}/{paternal}/{method} 未拒绝条件 {reason}：{response}");
        Require(m.Body.ReproductionRole==originalM&&f.Body.ReproductionRole==originalF,"拒绝时自动改写身体角色");
    }
});
Check("跨类关系门槛高于同类，且双方各自必须愿意",()=>
{
    foreach(string maternal in species)foreach(string paternal in species)foreach(var method in methods)
    foreach(bool maternalSide in new[]{true,false})
    {
        var sim=Fixture(maternal,paternal);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
        var bond=maternalSide?m.Family.Peers[f.Id]:f.Family.Peers[m.Id];bond.Familiarity=50;bond.Affection=50;
        sim.RequestPregnancy(m.Id,f.Id,method);
        Require(m.Family.Pregnancy.Phase==(Cross(maternal,paternal)?PregnancyPhase.None:PregnancyPhase.Preparing),
            $"关系门槛没有区分跨类或单方同意：{maternal}/{paternal}/{method}/{maternalSide}");
    }
});
Check("普通准备存读保持后续随机流，成功与失败不重抽、不换方式",()=>
{
    Require(successful.Count==72,"矩阵缺少真实成功种子");
    foreach(var entry in successful)
    {
        var key=entry.Key;
        var seeds=new List<ulong>{entry.Value.Seed};
        if(unsuccessful.TryGetValue(key,out ulong failedSeed))seeds.Add(failedSeed);
        foreach(ulong seed in seeds)
        {
            var sim=Fixture(key.Mother,key.Father,seed);Request(sim,key.Method);Tick(sim,3);
            Require(sim.State.Creatures[0].Family.Pregnancy.Phase==PregnancyPhase.Preparing,"存档点没有覆盖随机决策之前");
            var restored=Reload(sim);bool begun=Settle(sim,key.Method),copied=Settle(restored,key.Method);
            Require(begun==copied,"同档重抽受孕结果");Tick(sim,2);Tick(restored,2);
            Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(restored.State,SaveStore.JsonOptions),"普通孕育读档后完整状态不一致");
            Require(sim.State.RandomState==restored.State.RandomState&&sim.Next()==restored.Next(),"存读后下一个随机决策已偏离");
            var m=restored.State.Creatures[0];var f=restored.State.Creatures[1];
            var phase=m.Family.Pregnancy.Phase;string? eventId=m.Family.Pregnancy.EventId;ulong random=restored.State.RandomState;
            restored.RequestPregnancy(m.Id,f.Id,key.Method==BirthMethod.Stem?BirthMethod.Live:BirthMethod.Stem);
            Require(m.Family.Pregnancy.Phase==phase&&m.Family.Pregnancy.EventId==eventId&&m.Family.Pregnancy.Method==key.Method&&
                restored.State.RandomState==random,"已有孕育或失败冷却被点击改方式/重抽");
        }
    }
});
Environment.ExitCode=failures==0?0:1;

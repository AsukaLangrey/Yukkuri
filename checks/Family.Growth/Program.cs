using System.Text.Json;
using Yukkuri.Simulation;

// Only run on an independently authorised remote machine.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action run)
{try{run();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*4);n++){sim.AdvanceInteractionPresentation(.25);sim.Advance(.25);}}
WorldSimulation Fixture(double age=FamilyRules.YoungSeconds-.5)
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var child=sim.State.Creatures.Single(c=>c.IsYoung);
    sim.State.Creatures.RemoveAll(c=>c.Id!=child.Id);sim.State.SelectedId=child.Id;sim.State.RoomObjects.Clear();
    sim.State.UnlimitedResources=false;sim.State.Food=4;
    child.Body.AgeSeconds=child.InnerAgeSeconds=age;
    child.X=child.TargetX=0;child.Z=child.TargetZ=0;child.DecisionIn=10000;
    child.Fullness=child.Energy=child.Interaction.Hydration=90;child.Health=100;child.Family.Warmth=80;
    child.Interaction.NextAutonomyAt=10000;
    Require(child.Family.BirthEventId==null&&sim.State.CompletedBirths.Count==0,"fixture manufactured a birth history");
    return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}

Check("默认及旧档幼体用真实身体年龄成长，身份和固定角色保留",()=>
{
    foreach(var role in new[]{ReproductionRole.Maternal,ReproductionRole.Paternal})
    {
        var sim=Fixture();var c=sim.Selected!;c.Body.ReproductionRole=role;
        string id=c.Id,body=c.Body.Id,identity=c.Registration.IdentityId;
        c.Memories.Add("先前的生活经历");c.Family.LifeKnowledge["safe:Water"]=false;
        Tick(sim,.25);Require(c.IsYoung,"未到实际成熟年龄就长大");
        Tick(sim,.5);
        Require(!c.IsYoung&&c.Body.AgeSeconds>=FamilyRules.YoungSeconds,"没有出生事件的幼体永久无法成年");
        Require(c.Id==id&&c.Body.Id==body&&c.Registration.IdentityId==identity&&c.Body.ReproductionRole==role,
            "成长更换了持续身份、身体或出生固定角色");
        Require(c.Memories.Contains("先前的生活经历")&&!c.Family.LifeKnowledge["safe:Water"],"成长抹去原记忆或个人认知");
        Require(c.Family.BirthEventId==null&&sim.State.CompletedBirths.Count==0,"为成长虚构出生结算");
        Tick(sim,3);Require(c.Interaction.Memories.GetValueOrDefault("grown")==3,"成长事件重复记账");
        SaveStore.Validate(sim.State);
    }
});
Check("到龄但失养仍需真实吃到一份食物，成长不代替照护",()=>
{
    var sim=Fixture(FamilyRules.YoungSeconds+1);var c=sim.Selected!;c.Fullness=10;
    Tick(sim,1);Require(c.IsYoung,"失养幼体只靠时间就成熟");
    int stock=sim.State.Food;
    Require(sim.Feed(c.Id),"既有真实喂食入口拒绝了可进食幼体");
    Require(sim.State.Food==stock-1&&c.Interaction.EatingRemaining>0,"喂食没有实际消耗或嘴部过程");
    Tick(sim,.5);Require(!c.IsYoung&&c.Fullness>35,"照护后仍被不存在的出生记录挡住成长");
    Require(c.Family.BirthEventId==null&&sim.State.CompletedBirths.Count==0,"照护生成伪出生事件");
});
Check("暂停和近看不推进成长，恢复普通时间后才到龄",()=>
{
    var sim=Fixture();var c=sim.Selected!;double age=c.Body.AgeSeconds,inner=c.InnerAgeSeconds;
    sim.State.Settings.Pause=true;Tick(sim,5);
    Require(c.IsYoung&&c.Body.AgeSeconds==age&&c.InnerAgeSeconds==inner,"暂停推进了成长");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;Tick(sim,5);
    Require(c.IsYoung&&c.Body.AgeSeconds==age&&c.InnerAgeSeconds==inner,"近看推进了成长");
    sim.ExitCloseUp();Tick(sim,1);Require(!c.IsYoung,"恢复普通时间仍不成长");
});
Check("冷藏中成长使用十分之一身体时间而非全局时间",()=>
{
    var sim=Fixture();var c=sim.Selected!;double age=c.Body.AgeSeconds;
    sim.SetColdStorage(c.Id,true);Require(c.Family.ColdStorage.Stored,"冷藏前置失败");
    Tick(sim,1);
    Require(c.IsYoung&&Math.Abs(c.Body.AgeSeconds-age-.1)<1e-7,"冷藏用全速年龄使幼体过早成熟");
    Tick(sim,5);
    Require(!c.IsYoung&&Math.Abs(c.Body.AgeSeconds-age-.6)<1e-7,"有限冷藏被误当成完全冻结成长");
    Require(c.Family.BirthEventId==null&&sim.State.CompletedBirths.Count==0,"冷藏成长生成伪出生记录");
    SaveStore.Validate(sim.State);
});
Check("到龄前旧档续接一次成长，重新读取不重放或重置年龄",()=>
{
    var sim=Fixture();Tick(sim,.25);var loaded=Reload(sim);
    Tick(sim,1);Tick(loaded,1);
    Require(!sim.Selected!.IsYoung&&!loaded.Selected!.IsYoung,"旧档读取后永久幼年");
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(loaded.State,SaveStore.JsonOptions),
        "同档成长续接不一致");
    var after=Reload(loaded);double age=after.Selected!.Body.AgeSeconds;Tick(after,1);
    Require(after.Selected.Body.AgeSeconds>age&&after.Selected.Interaction.Memories.GetValueOrDefault("grown")==3,
        "成年读档重置年龄或重复结算成长");
});
Environment.ExitCode=failures==0?0:1;

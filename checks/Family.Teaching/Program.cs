using System.Text.Json;
using Yukkuri.Simulation;

// Execute only on the authorised independent remote host, never the original workstation.
int failures=0;const string key="safe:Meal";
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture(ulong seed=1,BirthMethod method=BirthMethod.Stem,bool belief=false,double distance=1.45)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(unchecked(seed*11400714819323198485UL));
    sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAt(1);
    var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
    teacher.IsYoung=false;teacher.Body.AgeSeconds=teacher.InnerAgeSeconds=2000;
    child.IsYoung=true;child.Body.AgeSeconds=child.InnerAgeSeconds=10;
    for(int n=0;n<2;n++)
    {
        var c=sim.State.Creatures[n];c.X=c.TargetX=n*distance;c.Z=c.TargetZ=0;
        c.Fullness=c.Energy=c.Interaction.Hydration=c.Health=100;c.Trust=90;c.Fear=c.Pain=c.Resentment=0;
        c.TrueDisposition="gentle";c.Patience=.8;c.Aggression=.05;c.Courage=.2;
        c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    }
    teacher.Family.LifeKnowledge[key]=belief;
    child.Family.MotherId=teacher.Id;child.ParentId=teacher.Id;child.Family.BirthMethod=method;
    teacher.Family.Peers[child.Id]=new(){Familiarity=70,Affection=70};
    child.Family.Peers[teacher.Id]=new(){Familiarity=70,Affection=70};
    return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void Start(WorldSimulation sim)
{
    var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
    string response=sim.TeachFamilyKnowledge(teacher.Id,child.Id,key);
    Require(teacher.Family.Lesson?.LearnerId==child.Id,"未开始教导："+response);
    Require(!child.Family.LifeKnowledge.ContainsKey(key)&&!child.Family.LearningSources.ContainsKey(key),"点击立即写入知识");
}
void Finish(WorldSimulation sim)
{
    var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
    for(int n=0;n<80&&teacher.Family.Lesson!=null;n++)Tick(sim,.25);
    Require(teacher.Family.Lesson==null&&child.Family.LifeKnowledge.ContainsKey(key),"20秒内真实相处没有完成教导");
    var record=child.Family.LearningSources[key];
    Require(record.Source==LifeKnowledgeSource.Taught&&record.FromId==teacher.Id,"没有记录实际长辈与教导来源");
}
WorldSimulation Understood(bool belief,ulong firstSeed=1)
{
    for(ulong seed=firstSeed;seed<firstSeed+128;seed++)
    {
        var sim=Fixture(seed,belief:belief);Start(sim);Finish(sim);
        if(!sim.State.Creatures[1].Family.LearningSources[key].Misunderstood)return sim;
    }
    throw new Exception("128个种子没有正确听懂的分支");
}
RoomObject EatOne(WorldSimulation sim,double contamination=0)
{
    var child=sim.State.Creatures[1];child.Fullness=15;
    var source=new RoomObject{Id="teaching-meal",Kind=RoomObjectKind.Meal,RegionId=child.RegionId,
        X=child.X,Z=child.Z+.4,Portions=1,Contamination=contamination};sim.State.RoomObjects.Add(source);
    int exposures=child.Interaction.Digestion.Exposures;double before=child.Fullness;
    for(int n=0;n<80&&child.Interaction.Digestion.Exposures==exposures;n++)Tick(sim,.25);
    Require(child.Interaction.Digestion.Exposures==exposures+1&&child.Fullness>before&&source.Portions==0,"没有实际吃下唯一一份饭团");
    return source;
}

Check("近处教导必须听满六秒，不是点击或半途即获得知识",()=>
{
    var sim=Fixture();var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];Start(sim);
    double started=sim.State.WorldSeconds;Tick(sim,5.75);
    Require(teacher.Family.Lesson!=null&&teacher.Family.Lesson.Elapsed>0&&teacher.Family.Lesson.Elapsed<6&&
        !child.Family.LifeKnowledge.ContainsKey(key),"不到六秒就写入知识或没有实际交流");
    Tick(sim,.5);Require(teacher.Family.Lesson==null&&child.Family.LearningSources[key].At>=started+6,"听满后未结算或记录提前");
    Require(child.Family.BirthMethod==BirthMethod.Stem,"教导修改了茎生背景");
});
Check("远处先实际靠近，路上时间不能当作已听完",()=>
{
    var sim=Fixture(distance:3);var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];Start(sim);
    double distance=WorldSimulation.Distance(teacher.X,teacher.Z,child.X,child.Z);Tick(sim,1);
    Require(WorldSimulation.Distance(teacher.X,teacher.Z,child.X,child.Z)<distance,"没有实际靠近");
    Require(teacher.Family.Lesson!=null&&teacher.Family.Lesson.Elapsed<1&&!child.Family.LifeKnowledge.ContainsKey(key),"移动被冒充已完成的教导");
    Finish(sim);Require(child.Family.LearningSources[key].At>6,"远处没有额外接近过程");
});
Check("未知常识、跨区、双方饥渴与恐惧关系都不能强行教会",()=>
{
    foreach(int reason in new[]{0,1,2,3,4,5,6,7})
    {
        var sim=Fixture();var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
        if(reason==0)teacher.Family.LifeKnowledge.Clear();
        if(reason==1){sim.State.Regions.Add(new(){Id="away",Name="远处",Kind=RegionKind.Wilderness});child.RegionId="away";}
        if(reason==2)teacher.Fullness=40;if(reason==3)child.Fullness=40;
        if(reason==4)teacher.Interaction.Hydration=40;if(reason==5)child.Interaction.Hydration=40;
        if(reason==6)child.Family.Peers[teacher.Id].Fear=60;
        string requested=reason==7?"unknown:secret":key;
        string response=sim.TeachFamilyKnowledge(teacher.Id,child.Id,requested);
        Require(teacher.Family.Lesson==null&&!child.Family.LifeKnowledge.ContainsKey(key),"不满足条件仍启动/写知识："+reason+" / "+response);
    }
});
Check("暂停拒绝新教导，暂停与近看都不推进已有课程",()=>
{
    var sim=Fixture();var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
    sim.State.Settings.Pause=true;sim.TeachFamilyKnowledge(teacher.Id,child.Id,key);
    Require(teacher.Family.Lesson==null,"暂停仍开始教导");sim.State.Settings.Pause=false;Start(sim);Tick(sim,2);
    double elapsed=teacher.Family.Lesson!.Elapsed,time=sim.State.WorldSeconds;
    sim.State.Settings.Pause=true;Tick(sim,20);
    Require(teacher.Family.Lesson!.Elapsed==elapsed&&sim.State.WorldSeconds==time&&!child.Family.LifeKnowledge.ContainsKey(key),"暂停推进教导");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;Tick(sim,20);
    Require(teacher.Family.Lesson!.Elapsed==elapsed&&sim.State.WorldSeconds==time&&!child.Family.LifeKnowledge.ContainsKey(key),"近看推进教导");
    sim.State.Mode=PlayMode.Management;Finish(sim);
});
Check("冷藏打断不学会，重复点击不跳过过程或完成冷却",()=>
{
    var sim=Fixture();var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];Start(sim);Tick(sim,2);
    double elapsed=teacher.Family.Lesson!.Elapsed;
    for(int n=0;n<20;n++)sim.TeachFamilyKnowledge(teacher.Id,child.Id,key);
    Require(teacher.Family.Lesson!.Elapsed==elapsed&&!child.Family.LifeKnowledge.ContainsKey(key),"重复点击跳过教导");
    sim.SetColdStorage(child.Id,true);
    Require(child.Family.ColdStorage.Stored&&teacher.Family.Lesson==null&&!child.Family.LifeKnowledge.ContainsKey(key),"冷藏打断未清课程或奖励了没听完的知识");
    Require(teacher.Family.NextLessonAt>sim.State.WorldSeconds&&child.Family.NextLessonAt>sim.State.WorldSeconds,"打断没有留下消化间隔");
    var complete=Fixture();Start(complete);Finish(complete);teacher=complete.State.Creatures[0];child=complete.State.Creatures[1];
    ulong random=complete.State.RandomState;bool belief=child.Family.LifeKnowledge[key];double learnedAt=child.Family.LearningSources[key].At;
    for(int n=0;n<20;n++)complete.TeachFamilyKnowledge(teacher.Id,child.Id,key);
    Require(teacher.Family.Lesson==null&&complete.State.RandomState==random&&child.Family.LifeKnowledge[key]==belief&&
        child.Family.LearningSources[key].At==learnedAt,"完成后刷教导立即重抽理解或改写来源");
});
Check("茎生能学且可能误解，两个方式同档继续都不重抽理解",()=>
{
    foreach(var method in new[]{BirthMethod.Stem,BirthMethod.Live})
    {
        bool right=false,wrong=false;
        for(ulong seed=1;seed<=256&&(!right||!wrong);seed++)
        {
            var sim=Fixture(seed,method);Start(sim);Tick(sim,3);var restored=Reload(sim);Finish(sim);Finish(restored);
            Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(restored.State,SaveStore.JsonOptions),"教导中存读改变完整状态");
            var child=sim.State.Creatures[1];var record=child.Family.LearningSources[key];
            Require(child.Family.LifeKnowledge[key]==record.Misunderstood,"理解与听错记录不对应原长辈判断");
            if(record.Misunderstood)wrong=true;else right=true;
            var learned=Reload(sim).State.Creatures[1];
            Require(learned.Family.LearningSources[key].Misunderstood==record.Misunderstood&&learned.Family.LifeKnowledge[key]==child.Family.LifeKnowledge[key],"完成后重载重新解释知识");
        }
        Require(right&&wrong,"缺少正确学习或误解分支："+method);
    }
});
Check("真实吃下干净食物可修正长辈错误判断并改记亲身经验",()=>
{
    var sim=Understood(false);var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
    Require(!child.Family.LifeKnowledge[key]&&child.Family.LearningSources[key].Source==LifeKnowledgeSource.Taught,"没有实际教导的谨慎判断");
    double taughtAt=child.Family.LearningSources[key].At;EatOne(sim);
    var record=child.Family.LearningSources[key];
    Require(child.Family.LifeKnowledge[key]&&record.Source==LifeKnowledgeSource.Experience&&record.FromId==null&&!record.Misunderstood&&record.At>taughtAt,"亲身摄入未修正教导或仍假称长辈来源");
    Require(!teacher.Family.LifeKnowledge[key],"孩子经验隔空修改长辈认知");
});
Check("足底失能幼体在近处仍能原地听完，不能借教导恢复行走",()=>
{
    var sim=Fixture();var child=sim.State.Creatures[1];child.Interaction.FootIntegrity=20;
    Require(!WorldSimulation.CanSelfMove(child),"失能夹具仍可自行移动");
    double x=child.X,z=child.Z;Start(sim);Finish(sim);
    Require(child.X==x&&child.Z==z&&!WorldSimulation.CanSelfMove(child),"原地听课使失能幼体移动或恢复足底");
    Require(child.Family.LearningSources[key].Source==LifeKnowledgeSource.Taught,"失能幼体没有从真实教导学会");
});
Check("伪造已听时间或脱离双向相处的课程存档拒绝",()=>
{
    foreach(bool falseElapsed in new[]{true,false})
    {
        var sim=Fixture();Start(sim);Tick(sim,1);
        var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
        if(falseElapsed)state.Creatures[0].Family.Lesson!.Elapsed=5;
        else state.Creatures[1].Family.CompanionTargetId=null;
        bool rejected=false;
        try{SaveStore.Validate(state);}catch(InvalidDataException){rejected=true;}
        Require(rejected,falseElapsed?"经过1秒却伪造听满5秒的档案被接受":"课程脱离双向相处仍被接受");
    }
});
Check("长辈说安全不改变真实污染，最后一份吃完后不适仍能修正判断",()=>
{
    bool observed=false;
    for(ulong seed=1;seed<=128&&!observed;seed++)
    {
        var sim=Fixture(seed,belief:true);Start(sim);Finish(sim);var child=sim.State.Creatures[1];
        if(child.Family.LearningSources[key].Misunderstood)continue;
        var meal=EatOne(sim,1);
        if(child.Interaction.Digestion.Episodes==0)continue;
        Tick(sim,40);Require(!sim.State.RoomObjects.Any(o=>o.Id==meal.Id),"已吃完的最后一份没有正常清除");
        Require(child.Interaction.Digestion.Symptomatic&&!child.Family.LifeKnowledge[key],"长辈判断挡住真实患病或最后一份消失就无法反思");
        Require(child.Family.LearningSources[key].Source==LifeKnowledgeSource.Experience&&child.Family.LearningSources[key].FromId==null,"不适后的修正来源错误");
        observed=true;
    }
    Require(observed,"有限独立种子内没有观察到污染食物后的真实不适");
});
Environment.ExitCode=failures==0?0:1;

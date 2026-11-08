using System.Text.Json;
using Yukkuri.Simulation;

// Static source on the original workstation. Independent authorised host only.
int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception error){failures++;Console.WriteLine("FAIL "+name+": "+error);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
Creature Adult(WorldSimulation sim)=>sim.State.Creatures.Single(c=>c.Id=="interaction-oil-0");
Creature Child(WorldSimulation sim)=>sim.State.Creatures.Single(c=>c.Id=="interaction-oil-2");
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261104);sim.State.RoomObjects.Clear();sim.State.UnlimitedResources=false;
    sim.State.Regions.Add(new(){Id="bioparent-away",Name="亲代所在另一处",Kind=RegionKind.Sanctuary});
    foreach(var c in sim.State.Creatures)
    {
        c.Fullness=c.Interaction.Hydration=c.Energy=c.Health=100;c.Fear=c.Pain=0;c.Trust=20;
        c.TrueDisposition="gentle";c.Patience=.8;c.Aggression=.08;c.Interaction.ToyInterest=0;
        c.Interaction.NextAutonomyAt=10000;c.Family.NextSocialAt=c.Family.NextCareAt=0;
        c.Family.Peers.Clear();c.Family.CaregiverIds.Clear();c.Family.CompanionTargetId=null;
    }
    var adult=Adult(sim);var child=Child(sim);var parent=sim.State.Creatures.Single(c=>c.Id=="interaction-oil-1");
    adult.X=adult.TargetX=0;adult.Z=adult.TargetZ=0;
    child.X=child.TargetX=1.5;child.Z=child.TargetZ=0;child.Family.Warmth=45;
    // A child with a stable old foot impairment can converse in place. It needs
    // genuine nearby care and cannot secretly walk to the distant food source.
    child.Interaction.FootIntegrity=20;
    child.ParentId=child.Family.FatherId=parent.Id;parent.RegionId="bioparent-away";
    sim.State.RoomObjects.Add(new(){Id="adoption-meal",RegionId=adult.RegionId,Kind=RoomObjectKind.Meal,X=-.75,Z=0,Portions=6});
    sim.State.RoomObjects.Add(new(){Id="adoption-water",RegionId=adult.RegionId,Kind=RoomObjectKind.Water,X=0,Z=-.75,Portions=6});
    return sim;
}
void WaitFor(WorldSimulation sim,Func<bool> reached,double maximum,string message)
{
    for(int n=0;n<(int)(maximum*4)&&!reached();n++)Tick(sim,.25);
    Require(reached(),message+" / "+Adult(sim).Activity+" / "+Child(sim).Activity);
}
void AssertBlood(WorldSimulation sim,string? mother,string? father,string? parent)
{
    var child=Child(sim);
    Require(child.Family.MotherId==mother&&child.Family.FatherId==father&&child.ParentId==parent,"实际照护改写了血缘来源");
    Require(!FamilyRules.IsCloseFamily(Adult(sim),child),"养亲被并入血缘判定");
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}

Check("陌生非亲代从实际相处到照护养亲再到后续教导",()=>
{
    var sim=Fixture();var adult=Adult(sim);var child=Child(sim);
    string? mother=child.Family.MotherId,father=child.Family.FatherId,parent=child.ParentId;
    double humanTrust=child.Trust;
    Require(adult.Family.Peers.Count==0&&child.Family.Peers.Count==0&&child.Family.CaregiverIds.Count==0,"初始状态偷塞了关系或养亲");
    Tick(sim,.5);
    Require(adult.Family.CompanionTargetId==child.Id&&child.Family.CompanionTargetId==adult.Id,"善意陌生成年没有自主认识幼体");
    Require(adult.Family.Peers[child.Id].Affection<1&&adult.Family.Peers[child.Id].CareSeconds==0&&child.Family.CaregiverIds.Count==0,"初次碰面虚构亲近或抚育经历");
    WaitFor(sim,()=>child.Family.CaregiverIds.Contains(adult.Id),600,"自主过程未形成养亲");
    Require(adult.Family.Peers[child.Id].CareSeconds>=60&&child.Family.Peers[adult.Id].CareSeconds>=60,"没有实际照护就授予养亲身份");
    Require(child.Family.Warmth>35&&child.Trust==humanTrust,"真实保暖未发生或偷改对人信任");
    AssertBlood(sim,mother,father,parent);
    // Only the teacher's input knowledge is supplied; the child must acquire it
    // after adoption through a new autonomous, physically completed lesson.
    adult.Family.LifeKnowledge["safe:Sweet"]=false;
    Require(!child.Family.LifeKnowledge.ContainsKey("safe:Sweet"),"教导前置已有答案");
    WaitFor(sim,()=>child.Family.LearningSources.TryGetValue("safe:Sweet",out var source)&&source.Source==LifeKnowledgeSource.Taught,300,"养亲无法继续自动相处教导");
    Require(child.Family.LearningSources["safe:Sweet"].FromId==adult.Id,"知识不是来自实际养亲讲述");
    AssertBlood(sim,mother,father,parent);SaveStore.Validate(sim.State);
});
Check("认识后食物仍须真实搬运和入口消费，点击或关系不生成营养",()=>
{
    var sim=Fixture();var adult=Adult(sim);var child=Child(sim);
    WaitFor(sim,()=>adult.Family.Peers.TryGetValue(child.Id,out var bond)&&bond.Affection>=20,360,"没有通过相处建立初步亲近");
    child.Fullness=50;double fullness=child.Fullness;int portions=sim.State.RoomObjects.Where(o=>o.Kind==RoomObjectKind.Meal).Sum(o=>o.Portions);
    Require(adult.Family.CareTargetId==null&&child.Family.CaregiverIds.Count==0,"未覆盖实际育幼的起点");
    WaitFor(sim,()=>adult.Family.CareParcelId!=null,90,"需要食物时没有取得实际照护份额");
    Require(child.Fullness<=fullness&&sim.State.RoomObjects.Where(o=>o.Kind==RoomObjectKind.Meal).Sum(o=>o.Portions)==portions,"取份阶段隔空喂饱或改变总量");
    WaitFor(sim,()=>child.Fullness>fullness,90,"送达后没有通过幼体实际入口消费");
    Require(sim.State.RoomObjects.Where(o=>o.Kind==RoomObjectKind.Meal).Sum(o=>o.Portions)<portions&&child.Interaction.Digestion.Exposures>0,"幼体营养不是来自实际食物消费");
});
Check("冷淡高攻击或任一方向的恐惧怨恨拒绝初次接纳",()=>
{
    foreach(string reason in new[]{"impatient","aggressive","adult-fear","child-fear","adult-resentment","child-resentment"})
    {
        var sim=Fixture();var adult=Adult(sim);var child=Child(sim);
        if(reason=="impatient")adult.Patience=.3;
        if(reason=="aggressive")adult.Aggression=.7;
        if(reason.StartsWith("adult-",StringComparison.Ordinal))adult.Family.Peers[child.Id]=new(){Fear=reason.EndsWith("fear",StringComparison.Ordinal)?30:0,Resentment=reason.EndsWith("resentment",StringComparison.Ordinal)?30:0};
        if(reason.StartsWith("child-",StringComparison.Ordinal))child.Family.Peers[adult.Id]=new(){Fear=reason.EndsWith("fear",StringComparison.Ordinal)?30:0,Resentment=reason.EndsWith("resentment",StringComparison.Ordinal)?30:0};
        Tick(sim,35);
        Require(adult.Family.CompanionTargetId==null&&child.Family.CaregiverIds.Count==0&&
            (!adult.Family.Peers.TryGetValue(child.Id,out var bond)||bond.Affection==0&&bond.CareSeconds==0),"不愿意的组合仍自行亲近收养："+reason);
    }
});
Check("自己的饥渴疲劳优先，不靠陌生幼体刷出关系",()=>
{
    foreach(string reason in new[]{"hungry-adult","thirsty-child","tired-adult"})
    {
        var sim=Fixture();var adult=Adult(sim);var child=Child(sim);sim.State.RoomObjects.Clear();
        if(reason=="hungry-adult")adult.Fullness=40;
        if(reason=="thirsty-child")child.Interaction.Hydration=40;
        if(reason=="tired-adult")adult.Energy=30;
        Tick(sim,1);
        Require(adult.Family.CompanionTargetId==null&&adult.Family.Peers.Count==0&&child.Family.CaregiverIds.Count==0,"自己的需要没有优先："+reason);
    }
});
Check("已形成养亲后即使性格变冷也保留养育事实和自动相处入口",()=>
{
    var sim=Fixture();var adult=Adult(sim);var child=Child(sim);
    WaitFor(sim,()=>child.Family.CaregiverIds.Contains(adult.Id),600,"养亲前置未真实完成");
    adult.Patience=.3;adult.Family.LifeKnowledge["safe:Sweet"]=true;
    WaitFor(sim,()=>child.Family.LearningSources.TryGetValue("safe:Sweet",out var source)&&source.Source==LifeKnowledgeSource.Taught,300,"已经建立的养亲仍被初次接纳性格门槛拦截");
    Require(child.Family.CaregiverIds.Contains(adult.Id)&&!FamilyRules.IsCloseFamily(adult,child),"养亲关系丢失或被改写为血缘");
});
Check("初识过程暂停不增长，存读继续得到同样照护与养亲事实",()=>
{
    var sim=Fixture();Tick(sim,90);var adult=Adult(sim);var child=Child(sim);
    Require(adult.Family.Peers.TryGetValue(child.Id,out var relation)&&relation.Affection>0&&!child.Family.CaregiverIds.Contains(adult.Id),"存档点没有覆盖尚未收养的实际初识");
    double affection=relation!.Affection,care=relation.CareSeconds,world=sim.State.WorldSeconds;
    sim.State.Settings.Pause=true;Tick(sim,10);
    Require(relation.Affection==affection&&relation.CareSeconds==care&&sim.State.WorldSeconds==world,"暂停中增加关系或抚育经历");
    sim.State.Settings.Pause=false;var loaded=Reload(sim);Tick(sim,510);Tick(loaded,510);
    Require(Child(sim).Family.CaregiverIds.Contains(Adult(sim).Id)&&Child(loaded).Family.CaregiverIds.Contains(Adult(loaded).Id),"读档前后没有走完自主接纳");
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(loaded.State,SaveStore.JsonOptions),"同一初识存档继续得到不同的家庭状态");
});

Environment.ExitCode=failures==0?0:1;

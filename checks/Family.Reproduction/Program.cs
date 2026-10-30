using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

// Remote-only check source. Never execute on the original development workstation.
int failures=0;
void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(72381);
    sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAt(2);
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=100;c.Health=100;c.Pain=c.Fear=0;
        c.TrueDisposition="gentle";c.Aggression=.08;c.Patience=.6;c.Courage=.2;
        c.X=c.TargetX=n*1.65;c.Z=c.TargetZ=0;c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.NextSocialAt=10000;c.Family.NextCareAt=10000;
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
void Begin(WorldSimulation sim,BirthMethod method)
{
    var mother=sim.State.Creatures[0];var father=sim.State.Creatures[1];
    string response=sim.RequestPregnancy(mother.Id,father.Id,method);
    Require(mother.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"安排未进入准备期："+response);
    for(int n=0;n<100&&mother.Family.Pregnancy.Phase==PregnancyPhase.Preparing;n++)Tick(sim,.25);
    Require(mother.Family.Pregnancy.Phase==PregnancyPhase.Gestating,"实际靠近与接受后仍未孕育："+mother.Family.LastEvent);
}
Creature[] Children(WorldSimulation sim)=>sim.State.Creatures.Where(c=>c.Family.BirthEventId!=null).ToArray();
void WaitForBirth(WorldSimulation sim)
{
    // Delivery may wait for an already active swallow or excretion to finish.
    // The bounded wait never changes pregnancy, health, position, or needs.
    for(int n=0;n<80&&Children(sim).Length==0;n++)Tick(sim,.25);
    var m=sim.State.Creatures[0];
    Require(Children(sim).Length>0,$"期满后20秒仍未生产：{m.Family.LastEvent} / {m.Activity} / {m.Family.Pregnancy.Phase} / {m.Family.Pregnancy.Elapsed}");
}

Check("安排先经过准备与接受，不能点击即生或换方式",()=>
{
    var sim=Fixture();var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    sim.RequestPregnancy(m.Id,f.Id,BirthMethod.Stem);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing&&sim.State.Creatures.Count==2,"点击立即孕育或出生");
    Tick(sim,1);Require(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"省掉了双方接受过程");
    sim.RequestPregnancy(m.Id,f.Id,BirthMethod.Live);
    Require(m.Family.Pregnancy.Method==BirthMethod.Stem&&sim.State.Creatures.Count==2,"进行中的方式被第二次点击改写");
});
Check("固定角色、未成熟、单方意愿与身体不足分别拒绝",()=>
{
    foreach(int kind in new[]{0,1,2,3,4})
    {
        var sim=Fixture();var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
        if(kind==0)f.Body.ReproductionRole=ReproductionRole.Maternal;
        if(kind==1)m.IsYoung=true;
        if(kind==2)f.Family.Peers[m.Id].Affection=0;
        if(kind==3)m.Interaction.Hydration=30;
        if(kind==4)m.Pain=50;
        sim.RequestPregnancy(m.Id,f.Id,BirthMethod.Live);
        Require(m.Family.Pregnancy.Phase==PregnancyPhase.None&&sim.State.Creatures.Count==2,"条件分支仍接受："+kind);
    }
});
Check("茎生与胎生都能完整经过孕育出生恢复，时间数量负担有差别",()=>
{
    var counts=new Dictionary<BirthMethod,int>();var damage=new Dictionary<BirthMethod,double>();
    foreach(var method in new[]{BirthMethod.Stem,BirthMethod.Live})
    {
        var sim=Fixture();var m=sim.State.Creatures[0];Begin(sim,method);
        Tick(sim,FamilyRules.GestationSeconds(method)-1);
        Require(Children(sim).Length==0,"未到孕育期末就提前出生："+method);
        WaitForBirth(sim);var babies=Children(sim);counts[method]=babies.Length;damage[method]=100-m.Health;
        Require(babies.Length>0&&m.Family.Pregnancy.Phase==PregnancyPhase.Recovering,"完整普通时间后未出生并进入恢复："+method);
        Require(sim.State.CompletedBirths.Count==1,"出生未记录唯一结算");
        Require(babies.All(c=>c.IsYoung&&c.Family.BirthMethod==method),"子代年龄或生产方式记录错误");
        Require(babies.All(c=>c.Fullness>0&&c.Interaction.Hydration>0),"出生没有可继续照护的需求状态");
        // This branch starts from the explicit deep-bond fixture above. Birth,
        // the whole brood's actual nursery care and recovery use real clocks/actions.
        FamilyNurseryJourney.Run(sim,m,sim.State.Creatures[1],m.Family.Pregnancy.EventId!);
    }
    Require(FamilyRules.GestationSeconds(BirthMethod.Live)>FamilyRules.GestationSeconds(BirthMethod.Stem),"胎生周期没有更长");
    Require(counts[BirthMethod.Live]<counts[BirthMethod.Stem],"胎生数量没有更少");
    Require(damage[BirthMethod.Live]>damage[BirthMethod.Stem],"相同照护条件下胎生生产负担没有更大");
});
Check("孕育中存读继续一致，出生结算不会读档重放",()=>
{
    var sim=Fixture();Begin(sim,BirthMethod.Stem);Tick(sim,120);var loaded=Reload(sim);
    Tick(sim,125);Tick(loaded,125);WaitForBirth(sim);WaitForBirth(loaded);
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(loaded.State,SaveStore.JsonOptions),"同档继续得到不同家庭结果");
    int count=Children(loaded).Length;string[] ids=Children(loaded).Select(c=>c.Id).Order().ToArray();
    loaded=Reload(loaded);Tick(loaded,5);
    Require(count>=2&&Children(loaded).Length==count&&ids.SequenceEqual(Children(loaded).Select(c=>c.Id).Order()),"读档再次生出同一批宝宝");
    Require(loaded.State.CompletedBirths.Count==1,"重复记录同次出生");
});
Check("亲缘与出生角色保留，新生不复制亲代伤势技能或仇恨",()=>
{
    var sim=Fixture();var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];
    m.Body.SpeciesId="alice";f.Body.SpeciesId="patchouli";
    m.SharingSkill=93;m.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Bottom,PermanentLoss=5,Cause="旧伤"});
    m.Family.LifeKnowledge["unfamiliar-food-is-safe"]=false;
    // A harmless pre-existing scar is fixture evidence, not a freshly imposed wound.
    m.Interaction.FootIntegrity=95;
    Begin(sim,BirthMethod.Live);Tick(sim,FamilyRules.GestationSeconds(BirthMethod.Live));WaitForBirth(sim);
    var baby=Children(sim).Single();
    Require(baby.Family.MotherId==m.Id&&baby.Family.FatherId==f.Id&&baby.ParentId==m.Id,"血缘没有保留实际父母");
    Require(baby.Family.CaregiverIds.Contains(m.Id)&&baby.Family.CaregiverIds.Contains(f.Id),"未记录初始照护者");
    Require(baby.Body.SpeciesId is "alice" or "patchouli","子代不属于任一亲代物种");
    Require(m.Interaction.Injuries.PermanentFootLoss==5&&m.Interaction.FootIntegrity==95,"母体旧伤被生产抹去");
    Require(baby.Interaction.Injuries.PermanentFootLoss==0&&baby.Interaction.Injuries.Sites.Count==0&&baby.Interaction.FootIntegrity==100,"新生复制亲代伤残");
    Require(baby.SharingSkill!=m.SharingSkill&&baby.Interaction.Learning.Count==0,"子代复制成熟技能");
    Require(baby.Family.Peers.Count==2&&baby.Family.Peers.Values.All(b=>b.Resentment==0&&b.Fear==0),"子代复制旧仇恨或无关关系");
    Require(baby.Family.LifeKnowledge.TryGetValue("unfamiliar-food-is-safe",out bool known)&&!known,"胎生没有继承母体有限认知内容");
    string babyId=baby.Id;var role=baby.Body.ReproductionRole;
    var copy=Reload(sim).State.Creatures.Single(c=>c.Id==babyId);
    Require(copy.Body.ReproductionRole==role&&copy.Family.MotherId==m.Id,"读档重抽角色或改写血缘");
});
Check("出生空间不足时整批等待，腾出位置后只结算一次",()=>
{
    var sim=Fixture();Begin(sim,BirthMethod.Stem);var m=sim.State.Creatures[0];
    Tick(sim,FamilyRules.GestationSeconds(BirthMethod.Stem)-1);
    // A deliberately overcrowded inert yard exercises the real occupancy rule.
    for(int x=-8;x<=8;x++)for(int z=-8;z<=8;z++)
    {
        string id=$"blocker-{x}-{z}";
        sim.State.Creatures.Add(new(){Id=id,Name="占位",RegionId=m.RegionId,Condition=PhysicalCondition.Dead,
            X=x,Z=z,TargetX=x,TargetZ=z,Body=new(){Id=id+"-body",SpeciesId="reimu"},Registration=new(){IdentityId=id+"-identity"},
            Interaction=new(){Enabled=true,Accessory=new(){Id=id+"-accessory"}}});
    }
    Tick(sim,2);
    Require(Children(sim).Length==0&&sim.State.CompletedBirths.Count==0&&m.Family.Pregnancy.Phase==PregnancyPhase.Gestating,"没位置却半批出生或提前标记完成");
    sim.State.Creatures.RemoveAll(c=>c.Id.StartsWith("blocker-",StringComparison.Ordinal));WaitForBirth(sim);
    int count=Children(sim).Length;Require(count==m.Family.Pregnancy.PlannedChildren&&count>=2,"腾出位置没有完整出生");
    Tick(sim,2);Require(Children(sim).Length==count&&sim.State.CompletedBirths.Count==1,"腾出位置后重复结算");
});
Check("原暂停与近看不推进孕育，关闭新繁育不会清掉已有孕育",()=>
{
    var sim=Fixture();Begin(sim,BirthMethod.Live);var m=sim.State.Creatures[0];double elapsed=m.Family.Pregnancy.Elapsed;
    sim.State.Settings.Pause=true;Tick(sim,10);Require(m.Family.Pregnancy.Elapsed==elapsed,"暂停推进孕育");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;Tick(sim,10);
    Require(m.Family.Pregnancy.Elapsed==elapsed,"近看推进孕育");
    sim.SetBreedingPermission(m.Id,false);sim.SetAutonomousBreeding(false);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Gestating,"关闭许可抹去已孕");
    sim.State.Mode=PlayMode.Management;Tick(sim,1);Require(m.Family.Pregnancy.Elapsed>elapsed,"回小院后不能继续已有孕育");
});
Check("家庭显式空、非法父体角色与重复生育事件作为坏档拒绝",()=>
{
    var sim=Fixture();Begin(sim,BirthMethod.Stem);SaveStore.Validate(sim.State);
    string valid=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    bool Rejected(JsonNode node)
    {
        try{SaveStore.Validate(JsonSerializer.Deserialize<WorldState>(node.ToJsonString(),SaveStore.JsonOptions)!);return false;}
        catch(InvalidDataException){return true;}
    }
    var empty=JsonNode.Parse(valid)!;empty["Creatures"]![0]!["Family"]=null;
    Require(Rejected(empty),"显式null家庭被当成旧档默认");
    var invalidFather=JsonNode.Parse(valid)!;invalidFather["Creatures"]![1]!["Body"]!["ReproductionRole"]="Maternal";
    Require(Rejected(invalidFather),"孕育引用母体角色作为父体仍被接受");
    var duplicate=JsonSerializer.Deserialize<WorldState>(valid,SaveStore.JsonOptions)!;
    var original=duplicate.Creatures[0];
    var secondMother=new Creature{Id="duplicate-mother",Name="第二母体",RegionId=original.RegionId,
        X=-4,Z=0,TargetX=-4,TargetZ=0,Body=new(){Id="duplicate-mother-body",SpeciesId="reimu",ReproductionRole=ReproductionRole.Maternal},
        Registration=new(){IdentityId="duplicate-mother-identity"},Interaction=new(){Enabled=true,Accessory=new(){Id="duplicate-mother-accessory"}}};
    secondMother.Family.Pregnancy=JsonSerializer.Deserialize<PregnancyState>(JsonSerializer.Serialize(original.Family.Pregnancy,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    duplicate.Creatures.Add(secondMother);bool eventRejected=false;
    try{SaveStore.Validate(duplicate);}catch(InvalidDataException e){eventRejected=e.Message.Contains("孕育事件重复归属",StringComparison.Ordinal);}
    Require(eventRejected,"同一孕育事件被多个母体占有，或没有到达事件唯一性校验");
});
Environment.ExitCode=failures==0?0:1;

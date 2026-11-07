using Yukkuri.Simulation;

// Descriptor and rule checks. These do not prove Godot layout, animation or audio.
int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*4);n++){sim.AdvanceInteractionPresentation(.25);sim.Advance(.25);}}
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateCourtyardEconomy(160);
    sim.State.AutonomousBreeding=false;sim.Config.WildGrassEnabled=false;
    sim.State.RoomObjects.Clear();sim.State.Shelters.Clear();
    var c=sim.Selected!;GrowthRules.Initialize(c,false);
    c.Fullness=70;c.Energy=65;c.Interaction.Hydration=80;c.Health=63;
    c.Memories.Add("冷藏前的生活经历");
    foreach(var resident in sim.State.Creatures)
    {
        sim.SetColdStorage(resident.Id,true);
        Require(resident.Family.ColdStorage.Stored,"真实冷藏入口未保存对象");
    }
    SaveStore.Validate(sim.State);return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    var store=new SaveStore(Path.Combine(Path.GetTempPath(),"Yukkuri.Family.Presentation",Guid.NewGuid().ToString("N")));
    store.Save(sim.State,0);var result=store.Load(0);
    Require(!result.RecoveredBackup,"新存档意外使用备份");
    var loaded=new WorldSimulation(result.State);SaveStore.Validate(loaded.State);return loaded;
}
(string,string,string,ReproductionRole) Identity(Creature c)=>(c.Id,c.Body.Id,c.Registration.IdentityId,c.Body.ReproductionRole);
(double,double,double,double,double,double,double,double,double) Biology(Creature c)=>
    (c.Body.AgeSeconds,c.InnerAgeSeconds,c.Fullness,c.Energy,c.Interaction.Hydration,c.Health,c.CriticalRemaining,c.Interaction.Sleepiness,c.Family.Warmth);
void FrozenDescription(Creature c)
{
    Require(c.Family.ColdStorage.Stored&&WorldSimulation.BiologicalTimeScale(c)==0,"冷藏未暂停生理时间");
    string text=FamilyRules.Describe(c);
    Require(text.Contains("回收箱保存中")&&text.Contains("生理时间暂停")&&!text.Contains("十倍")&&
        !text.Contains("超时")&&!text.Contains("延缓已结束")&&!text.Contains("正在缓慢恢复"),"冷藏说明仍沿用旧延缓、到期或复苏规则");
}
Check("真实保存期内说明与生理暂停一致，暂停和存读档保留身份",()=>
{
    var sim=Fixture();var c=sim.Selected!;var identity=Identity(c);var biology=Biology(c);
    Tick(sim,WorldSimulation.ColdStorageLimitSeconds-1);
    Require(sim.State.WorldSeconds==599&&c.Family.ColdStorage.Elapsed==599,"未实际推进至旧期限前");
    Require(Biology(c)==biology,"保存期内生理状态仍在推进");FrozenDescription(c);
    sim.State.Settings.Pause=true;Tick(sim,5);
    Require(sim.State.WorldSeconds==599&&c.Family.ColdStorage.Elapsed==599&&Biology(c)==biology,"暂停推进了世界、保存计时或身体");
    sim=Reload(sim);c=sim.Selected!;
    Require(sim.State.Settings.Pause&&sim.State.WorldSeconds==599&&c.Family.ColdStorage.Elapsed==599&&
        Identity(c)==identity&&Biology(c)==biology&&c.Memories.Contains("冷藏前的生活经历"),"暂停存读档丢失保存状态、持续身份或经历");
    FrozenDescription(c);sim.State.Settings.Pause=false;Tick(sim,.25);
    Require(sim.State.WorldSeconds==599.25&&c.Family.ColdStorage.Elapsed==599.25&&Biology(c)==biology,"恢复世界时间后未继续冻结身体");
});
Check("真实跨过旧600秒期限仍冻结，读档后手动取回才继续身体时间",()=>
{
    var sim=Fixture();var c=sim.Selected!;var identity=Identity(c);var biology=Biology(c);
    var condition=c.Condition;var ids=sim.State.Creatures.Select(Identity).ToArray();
    Tick(sim,WorldSimulation.ColdStorageLimitSeconds-1);sim=Reload(sim);c=sim.Selected!;
    foreach(double extra in new[]{0d,10d,100d})
    {
        double deadline=WorldSimulation.ColdStorageLimitSeconds+extra;
        Tick(sim,deadline-sim.State.WorldSeconds);
        Require(sim.State.WorldSeconds==deadline&&c.Family.ColdStorage.Elapsed==deadline,"未真实跨过旧期限");
        Require(Biology(c)==biology&&Identity(c)==identity,"跨旧期限改变身体或身份");FrozenDescription(c);
    }
    sim=Reload(sim);c=sim.Selected!;FrozenDescription(c);
    Require(Biology(c)==biology&&c.Family.ColdStorage.Elapsed==700&&sim.State.WorldSeconds==700&&
        sim.State.Creatures.Select(Identity).SequenceEqual(ids),"超期存读档改变身体、计时或对象数量/身份");
    sim.SetColdStorage(c.Id,false);
    Require(!c.Family.ColdStorage.Stored&&WorldSimulation.BiologicalTimeScale(c)==1&&c.Family.ColdStorage.RecoveryRemaining==0&&
        c.Condition==condition&&Biology(c)==biology&&Identity(c)==identity,"手动取回改变原身体、身份或强加复苏等待");
    string text=FamilyRules.Describe(c);
    Require(!text.Contains("生理时间暂停")&&!text.Contains("取出冷藏后正在缓慢恢复")&&!text.Contains("超时"),"自然取回仍显示保存、旧到期或强制复苏");
    double age=c.Body.AgeSeconds,inner=c.InnerAgeSeconds;Tick(sim,1);
    Require(c.Body.AgeSeconds==age+1&&c.InnerAgeSeconds==inner+1&&Identity(c)==identity,"手动取回后身体时间或身份未正常续接");
    sim=Reload(sim);Require(!sim.Selected!.Family.ColdStorage.Stored&&Identity(sim.Selected)==identity&&
        sim.Selected.Body.AgeSeconds==age+1&&sim.Selected.Memories.Contains("冷藏前的生活经历"),"取回后存档重置身体或经历");
});
Check("取出后旧超期计时不能盖过实际复苏状态",()=>
{
    var c=new Creature();c.Family.ColdStorage.Elapsed=WorldSimulation.ColdStorageLimitSeconds+10;
    c.Family.ColdStorage.Stored=false;c.Family.ColdStorage.RecoveryRemaining=20;
    string text=FamilyRules.Describe(c);
    Require(text.Contains("取出冷藏后正在缓慢恢复")&&!text.Contains("冷藏已超时")&&!text.Contains("按十倍延缓"),"取出后的旧计时造成错误保存提示");
});
Environment.ExitCode=failures==0?0:1;

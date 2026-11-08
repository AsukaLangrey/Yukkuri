using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

// Source only: run solely on an independently authorised test machine, never on the original developer host.
int failures=0;
void Require(bool condition,string message){if(!condition)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int i=0;i<(int)Math.Ceiling(seconds*20);i++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
string Json(WorldSimulation sim)=>JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
WorldSimulation Fresh()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();sim.State.RoomObjects.Clear();sim.State.AutonomousBreeding=false;
    sim.State.PlayerX=0;sim.State.PlayerZ=7;
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Interaction.Hydration=c.Energy=c.Health=100;c.Trust=80;c.Fear=c.Pain=0;
        c.Personality.Kindness=.3;c.Personality.Pride=.3;c.Aggression=.05;c.Patience=.8;
        c.X=c.TargetX=n==0?0:n==1?-5:4;c.Z=c.TargetZ=n==1?-5:0;
        c.DecisionIn=c.Interaction.NextAutonomyAt=c.Family.NextSocialAt=c.Family.NextCareAt=10000;
        c.Interaction.ToyInterest=0;
    }
    return sim;
}
RoomObject Waste(WorldSimulation sim,double x=.2,double z=0)
{var waste=new RoomObject{Kind=RoomObjectKind.Feces,RegionId="home",X=x,Z=z};sim.State.RoomObjects.Add(waste);return waste;}
int Completed(Creature c,LifeSkill skill)=>c.Interaction.Development.Skills.GetValueOrDefault(skill)?.Completed??0;
WorldSimulation Reload(WorldSimulation sim)
{SaveStore.Validate(sim.State);var state=JsonSerializer.Deserialize<WorldState>(Json(sim),SaveStore.JsonOptions)!;SaveStore.Validate(state);return new(state);}

Check("性格友善、规则教育或铜银金牌分别触发自主清洁",()=>
{
    foreach(string basis in new[]{"kind","education","铜牌","银牌","金牌"})
    {
        var sim=Fresh();var c=sim.Selected!;var waste=Waste(sim);
        if(basis=="kind")c.Personality.Kindness=.8;
        else if(basis=="education")c.Interaction.RuleLearning=65;
        else c.Registration.Grade=basis;
        Tick(sim,4);
        Require(!sim.State.RoomObjects.Contains(waste)&&Completed(c,LifeSkill.Cleaning)==1,"没有实际自主清理："+basis);
        Require(c.Interaction.Development.Duty==LifeDuty.None,"把自主行为误写为玩家命令");
    }
});
Check("坏性格会做也不主动，明确命令后才动手",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=.2;c.Interaction.CleaningLearning=100;
    var waste=Waste(sim);Tick(sim,4);
    Require(sim.State.RoomObjects.Contains(waste)&&Completed(c,LifeSkill.Cleaning)==0,"把技能熟练误当成意愿");
    sim.SetLifeDuty(c.Id,LifeDuty.Cleaning);Tick(sim,4);
    Require(!sim.State.RoomObjects.Contains(waste)&&Completed(c,LifeSkill.Cleaning)==1,"命令未进入真实清理流程");
});
Check("示范不冒充独立完成，也不会把坏性格直接变成勤快",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=.2;var waste=Waste(sim);
    Require(sim.CleaningLessonReason(c.Id)==null,"真实示范条件不成立");sim.TeachCleaning(c.Id);
    Require(!sim.State.RoomObjects.Contains(waste)&&c.Interaction.Development.Skills[LifeSkill.Cleaning].Demonstrations==1&&Completed(c,LifeSkill.Cleaning)==0,"示范证据混入独立完成");
    var next=Waste(sim);Tick(sim,4);Require(sim.State.RoomObjects.Contains(next),"一次教学覆盖了性格意愿");
});
Check("饥饿打断劳动，安排保留，恢复后重新完成",()=>
{
    var sim=Fresh();var c=sim.Selected!;var waste=Waste(sim,2,0);sim.SetLifeDuty(c.Id,LifeDuty.Cleaning);Tick(sim,.5);
    Require(c.Interaction.CleaningTargetId==waste.Id,"没有开始真实接近");
    c.Fullness=10;Tick(sim,.3);
    Require(c.Interaction.CleaningTargetId==null&&c.Interaction.Development.Duty==LifeDuty.Cleaning&&Completed(c,LifeSkill.Cleaning)==0,"中断未释放目标或被计为完成");
    c.Fullness=100;Tick(sim,18);
    Require(!sim.State.RoomObjects.Contains(waste)&&Completed(c,LifeSkill.Cleaning)==1,"恢复后未重新评估分工");
});
Check("暂停时说明与预览只读，时间与工作不前进",()=>
{
    var sim=Fresh();var c=sim.Selected!;Waste(sim);sim.SetLifeDuty(c.Id,LifeDuty.Cleaning);sim.State.Settings.Pause=true;
    string before=Json(sim);
    for(int i=0;i<20;i++){sim.ExplainLifeActivity(c.Id);sim.AbilitySummary(c.Id);sim.DutySummary(c.Id);sim.PreviewBodyInteraction(c.Id,new(InteractionAction.Care));}
    Require(Json(sim)==before,"只读页面改变了世界、随机状态或经历");
    Tick(sim,2);Require(Json(sim)==before,"暂停时推进了分工");
});
Check("同一脏处只能被一只油认领和计功",()=>
{
    var sim=Fresh();var a=sim.Selected!;var b=sim.State.Creatures[1];b.X=b.TargetX=1;b.Z=b.TargetZ=0;
    sim.SetLifeDuty(a.Id,LifeDuty.Cleaning);sim.SetLifeDuty(b.Id,LifeDuty.Cleaning);var waste=Waste(sim,.5,0);Tick(sim,5);
    Require(!sim.State.RoomObjects.Contains(waste)&&Completed(a,LifeSkill.Cleaning)+Completed(b,LifeSkill.Cleaning)==1,"多人重复结算同一劳动");
});
Check("特别恶劣的油可以指使同伴，但完成归实际劳动者",()=>
{
    var sim=Fresh();var issuer=sim.Selected!;issuer.Personality.Kindness=.05;issuer.Personality.Pride=.95;
    var worker=sim.State.Creatures[1];worker.X=worker.TargetX=1;worker.Z=worker.TargetZ=0;worker.Personality.Kindness=.8;
    var waste=Waste(sim,.6,0);Tick(sim,4);
    Require(!sim.State.RoomObjects.Contains(waste)&&Completed(issuer,LifeSkill.Cleaning)==0&&Completed(worker,LifeSkill.Cleaning)==1,"指使者冒领劳动或没有实际完成");
    Require(issuer.Interaction.Development.LastChoreMessage.Contains("推给")&&worker.Interaction.Development.Recent.Any(e=>e.Description.Contains("其他油")),"缺少指使与承接证据");
});
Check("同伴可以拒绝指使，不能靠请求远程删除脏污",()=>
{
    var sim=Fresh();var issuer=sim.Selected!;issuer.Personality.Kindness=.05;issuer.Personality.Pride=.95;
    var worker=sim.State.Creatures[1];worker.X=worker.TargetX=1;worker.Z=worker.TargetZ=0;worker.Personality.Kindness=.2;
    var waste=Waste(sim,.6,0);Tick(sim,3);
    Require(sim.State.RoomObjects.Contains(waste)&&Completed(worker,LifeSkill.Cleaning)==0&&issuer.Interaction.Development.LastChoreMessage.Contains("没有答应"),"拒绝后仍凭空完成劳动");
});
Check("工作中存读档保留安排与证据，不补发完成",()=>
{
    var sim=Fresh();var c=sim.Selected!;Waste(sim,2,0);sim.SetLifeDuty(c.Id,LifeDuty.Cleaning);Tick(sim,.5);
    int before=c.Interaction.Development.Skills[LifeSkill.Cleaning].Attempts;sim=Reload(sim);c=sim.Selected!;
    Require(c.Interaction.Development.Duty==LifeDuty.Cleaning&&Completed(c,LifeSkill.Cleaning)==0,"读档重置安排或伪造成功");
    Tick(sim,8);Require(Completed(c,LifeSkill.Cleaning)==1&&c.Interaction.Development.Skills[LifeSkill.Cleaning].Attempts==before,"读档重复创建同一尝试");
});
Check("缺少新版能力字段的旧存档默认自由生活且无虚构经历",()=>
{
    var sim=Fresh();var root=JsonNode.Parse(Json(sim))!;
    foreach(var c in root["Creatures"]!.AsArray())c!["Interaction"]!.AsObject().Remove("Development");
    var state=JsonSerializer.Deserialize<WorldState>(root.ToJsonString(),SaveStore.JsonOptions)!;SaveStore.Validate(state);
    Require(state.Creatures.All(c=>c.Interaction.Development.Duty==LifeDuty.None&&c.Interaction.Development.Recent.Count==0),"旧存档被补造能力记录");
});
Check("非法分工对象与非有限数值必须被拒绝",()=>
{
    var sim=Fresh();var d=sim.Selected!.Interaction.Development;d.Duty=LifeDuty.Care;d.RecipientId="missing";
    bool rejected=false;try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}Require(rejected,"无效对象穿过存档校验");
    d.Duty=LifeDuty.None;d.RecipientId=null;d.NextDelegationAt=double.NaN;
    rejected=false;try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}Require(rejected,"NaN穿过存档校验");
});
Check("照护分工等待余力，恢复后送真实食水，取消时保留实物",()=>
{
    var sim=Fresh();var adult=sim.Selected!;var child=sim.State.Creatures[2];
    child.IsYoung=true;child.Body.AgeSeconds=child.InnerAgeSeconds=0;child.ParentId=child.Family.MotherId=adult.Id;
    child.Interaction.FootIntegrity=20;child.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Bottom,PermanentLoss=80,Cause="既有伤残"});
    child.Fullness=20;adult.Family.NextCareAt=0;adult.Energy=10;
    var source=new RoomObject{Kind=RoomObjectKind.Meal,RegionId="home",X=0,Z=.95,Portions=3};sim.State.RoomObjects.Add(source);
    sim.SetLifeDuty(adult.Id,LifeDuty.Care,child.Id);Tick(sim,2);
    Require(adult.Family.CareTargetId==null&&source.Portions==3,"无余力仍开工取份");
    adult.Energy=100;
    for(int i=0;i<120&&adult.Family.CareParcelId==null;i++)Tick(sim,.25);
    Require(adult.Family.CareParcelId!=null&&source.Portions==2,"未从实际物资取出一份");
    string parcelId=adult.Family.CareParcelId!;sim.SetLifeDuty(adult.Id,LifeDuty.None);
    var parcel=sim.State.RoomObjects.Single(o=>o.Id==parcelId);
    Require(parcel.CarrierId==null&&parcel.Portions==1&&Completed(adult,LifeSkill.Care)==0,"取消未送达工作却消失资源或记成功");
    SaveStore.Validate(sim.State);
});
Check("完成实际保暖陪护才记照护成功",()=>
{
    var sim=Fresh();var adult=sim.Selected!;var child=sim.State.Creatures[2];
    child.IsYoung=true;child.Body.AgeSeconds=child.InnerAgeSeconds=0;child.ParentId=child.Family.MotherId=adult.Id;
    child.X=child.TargetX=.6;child.Z=child.TargetZ=0;child.Family.Warmth=30;adult.Family.NextCareAt=0;
    sim.SetLifeDuty(adult.Id,LifeDuty.Care,child.Id);
    Require(Completed(adult,LifeSkill.Care)==0,"仅派工就记成功");
    Tick(sim,17);
    Require(Completed(adult,LifeSkill.Care)>=1&&child.Family.Warmth>30,"没有实际暖身或完成证据");
    SaveStore.Validate(sim.State);
});
Console.WriteLine($"Life development checks: {failures} failure(s)");
return failures==0?0:1;

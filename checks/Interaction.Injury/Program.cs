using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action check)
{try{check();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
void Care(WorldSimulation sim,Creature c,BodyPart part,SurfaceSide side=SurfaceSide.Whole)
{
    var input=new InteractionInput(InteractionAction.Care,part,Duration:.1,Side:side);
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"护理入口拒绝");
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);
    sim.EndInteractionGesture();
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
Check("重大底部损伤有持久上限，护理和食物不会即刻恢复",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    sim.State.Settings.LethalInjuries=false;
    sim.Hurt(c,100,"底部严重碰撞","player",BodyPart.Bottom);
    Require(c.Interaction.Injuries.PermanentFootLoss>=75,"未留下不可逆功能损失");
    Care(sim,c,BodyPart.Bottom);
    Require(c.Interaction.FootIntegrity==0&&c.Condition==PhysicalCondition.Recovering,"护理直接修复底部");
    c.Fullness=100;c.Energy=100;c.Interaction.Hydration=100;
    Tick(sim,180);
    Require(c.Interaction.FootIntegrity<=25&&!WorldSimulation.CanSelfMove(c),"不可逆底部损伤被普通恢复抹去");
    var copy=Reload(sim).Selected!;
    Require(copy.Interaction.Injuries.PermanentFootLoss==c.Interaction.Injuries.PermanentFootLoss,"存档丢失伤残上限");
});
Check("轻伤有普通时间恢复过程且近看暂停",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    sim.Hurt(c,20,"底部扭伤","player",BodyPart.Bottom);Care(sim,c,BodyPart.Bottom);
    var wound=c.Interaction.Injuries.At(BodyPart.Bottom)!;double damage=wound.Damage,foot=c.Interaction.FootIntegrity;
    sim.State.Mode=PlayMode.CloseUp;Tick(sim,90);
    Require(wound.Damage==damage&&c.Interaction.FootIntegrity==foot,"近看偷走恢复时间");
    sim.State.Mode=PlayMode.Management;Tick(sim,90);
    Require(wound.Damage<damage&&c.Interaction.FootIntegrity>foot,"真实时间护理没有改善功能");
    Require(wound.PermanentLoss==0,"轻伤被写为永久损伤");
});
Check("不同伤处可连续护理，左右伤势不串位",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;sim.State.UnlimitedResources=false;
    sim.Hurt(c,15,"左脸碰撞","player",BodyPart.Cheek,SurfaceSide.Left);
    sim.Hurt(c,12,"右脸碰撞","player",BodyPart.Cheek,SurfaceSide.Right);
    Care(sim,c,BodyPart.Cheek,SurfaceSide.Left);double health=c.Health;
    Care(sim,c,BodyPart.Cheek,SurfaceSide.Right);
    Require(c.Health==health,"第二部位在同一窗口重复全身回血");
    Require(c.Interaction.HasDressing(BodyPart.Cheek,SurfaceSide.Left)&&c.Interaction.HasDressing(BodyPart.Cheek,SurfaceSide.Right),"第二部位抹掉第一处敷料");
    sim.Hurt(c,1,"再次左脸碰撞","player",BodyPart.Cheek,SurfaceSide.Left);
    Require(!c.Interaction.HasDressing(BodyPart.Cheek,SurfaceSide.Left)&&c.Interaction.HasDressing(BodyPart.Cheek,SurfaceSide.Right),"左脸再伤清掉右脸敷料");
});
Check("重复护理不刷健康或额外扣物资",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;sim.State.UnlimitedResources=false;
    c.Health=60;c.Pain=20;Care(sim,c,BodyPart.Head);
    double health=c.Health;int stock=sim.State.Medicine;
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Head,Duration:.1);
    for(int attempt=0;attempt<20;attempt++)
    {sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture();}
    Require(c.Health==health&&sim.State.Medicine==stock,"重复点击仍能刷新身体恢复");
});
Check("再次濒危继承原救治余量且读档不重置",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    c.Health=1;sim.Hurt(c,2,"重伤",null);Tick(sim,25);
    double remaining=c.CriticalRemaining;Care(sim,c,BodyPart.Side);
    sim.Hurt(c,9,"再次失养",null);
    Require(c.Condition==PhysicalCondition.Critical&&c.CriticalRemaining<remaining&&c.CriticalRemaining<70,"护理重新派发90秒救治窗口");
    var copy=Reload(sim).Selected!;
    Require(copy.CriticalRemaining==c.CriticalRemaining&&copy.Interaction.Injuries.CriticalCareBudget==c.Interaction.Injuries.CriticalCareBudget,"读档刷新救治窗口");
});
Check("危重即时清理尚未入口食物，不伪造消耗",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    var food=new RoomObject{Id="injury-pending-food",Kind=RoomObjectKind.Meal,RegionId=c.RegionId,X=c.X+.5,Z=c.Z,Portions=2};sim.State.RoomObjects.Add(food);
    // Autonomous intake reserves a portion before the mouth reaches it.
    food.Portions--;
    var i=c.Interaction;i.BeginIntake(IntakeKind.Meal);i.GroundIntake=true;i.GroundIntakeKind=food.Kind;i.GroundIntakeSourceId=food.Id;i.NaturalIntakePending=true;
    c.Health=1;sim.Hurt(c,2,"重伤",null);
    Require(i.EatingRemaining==0&&!i.NaturalIntakePending&&i.GroundIntakeSourceId==null,"危重仍占用摄入动作");
    Require(food.Portions==2,"中断未入口食物却发生消耗");
});
Check("死亡一次结算并清理手持摄入及学习占用",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;var i=c.Interaction;
    i.Carried=true;i.Height=1;i.BeginIntake(IntakeKind.Water);i.FollowPlayer=true;i.WaitRemaining=3;
    c.FillingIntegrity=1;sim.Hurt(c,30,"致命撞击","player");
    Require(c.Condition==PhysicalCondition.Dead&&!i.Carried&&!i.Falling&&i.Height==0&&i.EatingRemaining==0&&!i.FollowPlayer&&i.WaitRemaining==0,"死亡后仍有活体动作占用");
    Require(i.Injuries.DeathCause=="致命撞击","死因丢失");
    int count=sim.State.Incidents.Count(e=>e.Kind==IncidentKind.Death&&e.TargetId==c.Id);
    sim.Hurt(c,30,"重复伤害","player");var copy=Reload(sim);Tick(copy,2);
    Require(copy.State.Incidents.Count(e=>e.Kind==IncidentKind.Death&&e.TargetId==c.Id)==count&&count==1,"读档或重复伤害重放死亡");
    Require(copy.Selected!.Interaction.Injuries.DeathCause=="致命撞击","后续输入重写死因");
});
Check("关闭致命后果仍保留伤残且允许护理",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;sim.State.Settings.LethalInjuries=false;
    c.FillingIntegrity=1;sim.Hurt(c,100,"严重事故",null,BodyPart.Bottom);
    Tick(sim,95);Require(c.Condition!=PhysicalCondition.Dead,"保护设置被绕过");
    Require(c.Interaction.Injuries.PermanentFootLoss>=75,"保护设置抹去了伤残");
    Care(sim,c,BodyPart.Bottom);Require(c.Interaction.FootTreated,"保护模式禁止需要的护理");
});
Check("系统性伤害不编造玩家局部接触",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    sim.Hurt(c,10,"孕育负担",null);
    Require(c.Interaction.Injuries.Sites.Count==0&&c.Interaction.HumanBond.Caution.Count==0,"孕育伤害错误写为玩家接触");
});
Check("旧档默认身体状态，显式空或坏伤势拒绝",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();
    var root=JsonNode.Parse(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions))!;
    var life=root["Creatures"]![0]!["Interaction"]!.AsObject();life.Remove("Injuries");
    SaveStore.Validate(JsonSerializer.Deserialize<WorldState>(root.ToJsonString(),SaveStore.JsonOptions)!);
    life["Injuries"]=null;bool rejected=false;
    try{SaveStore.Validate(JsonSerializer.Deserialize<WorldState>(root.ToJsonString(),SaveStore.JsonOptions)!);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"显式空伤势被当作旧档默认");
    c_bad(sim.Selected!);
    void c_bad(Creature c)
    {c.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Bottom,Damage=double.NaN});bool rejected=false;try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}Require(rejected,"非法伤势数值被接受");}
});
Check("冷藏只以十分之一速度累积生理负荷",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;var p=c.Interaction.Physiology;
    p.Bladder=10;p.Bowel=10;
    sim.SetColdStorage(c.Id,true);Require(c.Family.ColdStorage.Stored,"冷藏夹具未进入");
    Tick(sim,10);
    // At hydration/fullness 0..100 the rates bound the biological ten-second interval.
    Require(p.Bladder>=10.099&&p.Bladder<=10.221,"冷藏尿量按普通世界时间积累或停止积累");
    Require(p.Bowel>=10.059&&p.Bowel<=10.141,"冷藏肠道负荷没有使用身体时间");
    Require(!p.Active&&p.Urinations==0&&p.Defecations==0,"冷藏启动了空间排泄动作");
});
Check("冷藏达到排泄阈值不产物，取出后才完成一次",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;var p=c.Interaction.Physiology;
    p.Bladder=99;p.Bowel=10;sim.SetColdStorage(c.Id,true);Tick(sim,20);
    Require(!p.Active&&p.Urinations==0&&!sim.State.RoomObjects.Any(o=>o.OwnerId==c.Id&&WorldSimulation.IsWaste(o.Kind)),"冷藏个体在院内留下排泄物");
    var copy=Reload(sim);var patient=copy.Selected!;
    copy.SetColdStorage(patient.Id,false);Require(!patient.Family.ColdStorage.Stored,"夹具无法取出冷藏");
    patient.Interaction.WaitRemaining=10;
    Tick(copy,6);
    Require(patient.Interaction.Physiology.Urinations==1&&copy.State.RoomObjects.Count(o=>o.OwnerId==patient.Id&&o.Kind==RoomObjectKind.Urine)==1,"取出后没有正常排泄一次，或补发了冷藏中的排泄");
});
Check("旧场景的通用护理能选到实际底部伤处",()=>
{
    var sim=WorldSimulation.CreateMilestone();var c=sim.Selected!;
    sim.Hurt(c,20,"旧场景底部碰撞","player",BodyPart.Bottom);
    var result=sim.Contact(c.Id,new(ContactAction.Treat));
    Require(result.Success&&c.Interaction.FootTreated&&c.Interaction.HasDressing(BodyPart.Bottom),"旧场景没有底部选择器，通用护理却处理了健康额头");
    Require(c.Interaction.FootIntegrity==80,"旧场景护理立即修好了底部");
});
Check("死亡释放手持但保留原有固定装置的支撑",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;var i=c.Interaction;
    i.Restraint=RestraintKind.Hanging;i.Height=1.2;
    c.FillingIntegrity=1;sim.Hurt(c,30,"严重伤害",null);
    Require(c.Condition==PhysicalCondition.Dead&&i.Restraint==RestraintKind.Hanging&&i.Height==1.2,"死亡擅自解除了固定装置或穿过其支撑");
    var copy=Reload(sim).Selected!;
    Require(copy.Interaction.Height==1.2,"读档重复清理改变了固定装置中的位置");
});
Check("冷藏危重时钟只减一次身体时间",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    c.Health=1;sim.Hurt(c,2,"待救伤势",null);sim.SetColdStorage(c.Id,true);
    double before=c.CriticalRemaining;Tick(sim,10);
    Require(Math.Abs(c.CriticalRemaining-(before-1))<.0001,"冷藏救治时钟多减、未减或未按十分之一速度");
    Require(Math.Abs(c.Interaction.Injuries.CriticalCareBudget-c.CriticalRemaining)<.0001,"累计救治余量和实际时钟分离");
});
Environment.ExitCode=failures==0?0:1;

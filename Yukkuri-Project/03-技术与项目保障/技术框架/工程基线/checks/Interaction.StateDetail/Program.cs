using System.Text.Json;
using Yukkuri.Simulation;

int passed=0,failed=0;
void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action test){try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
WorldSimulation Fresh()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();sim.State.AutonomousBreeding=false;
    sim.State.Shelters.Clear();sim.State.RoomObjects.Clear();sim.Config.WildGrassEnabled=false;
    foreach(var c in sim.State.Creatures){c.Fullness=c.Interaction.Hydration=c.Energy=100;c.Pain=c.Fear=0;c.Interaction.Sleepiness=20;c.Family.Warmth=100;}
    return sim;
}
void Tick(WorldSimulation sim,double seconds){for(int n=0;n<(int)(seconds*40);n++){sim.AdvanceInteractionPresentation(.025);sim.Advance(.025);}}
string Description(WorldSimulation sim)=>WorldSimulation.DescribeInteractionCondition(sim.Selected!);
Check("主动找食物时观察说明同步显示进食需要",()=>{var sim=Fresh();sim.Selected!.Fullness=50;Require(NaturalResources.WantsFood(sim.Selected),"fixture");Require(Description(sim).Contains("想吃"),Description(sim));});
Check("主动找水时观察说明同步显示口渴",()=>{var sim=Fresh();sim.Selected!.Interaction.Hydration=35;Require(NaturalResources.WantsWater(sim.Selected),"fixture");Require(Description(sim).Contains("口渴"),Description(sim));});
Check("体力充足但困意很高也显示睡眠需要",()=>{var sim=Fresh();sim.Selected!.Interaction.Sleepiness=95;Require(Description(sim).Contains("困倦"),Description(sim));});
Check("冷藏对象不提示正在寻找食水",()=>{var sim=Fresh();var c=sim.Selected!;c.Fullness=c.Interaction.Hydration=0;c.Family.ColdStorage.Stored=true;Require(Description(sim).Contains("冷藏")&&!Description(sim).Contains("肚子饿了"),Description(sim));});
Check("受限个体的行为说明指出限制原因",()=>{var sim=Fresh();sim.Selected!.Interaction.Restraint=RestraintKind.Frame;Require(sim.ExplainLifeActivity(sim.Selected.Id).Reason.Contains("固定具"),sim.ExplainLifeActivity(sim.Selected.Id).ToString());});
Check("惊惧余波解释原地观察的原因",()=>{var sim=Fresh();var c=sim.Selected!;c.Interaction.Emotion.Alarm=30;c.Activity="留在原处观察，等自己慢慢安定下来";Require(sim.ExplainLifeActivity(c.Id).Reason.Contains("惊惧"),sim.ExplainLifeActivity(c.Id).ToString());});
Check("排泄行为不会被旧休息待办掩盖",()=>{var sim=Fresh();var c=sim.Selected!;c.Interaction.RestRemaining=30;c.Interaction.Physiology.Excreting=ExcretionKind.Urine;c.Interaction.Physiology.Remaining=2;Require(sim.ExplainLifeActivity(c.Id).Action=="正在撒尿",sim.ExplainLifeActivity(c.Id).ToString());});
foreach(int speed in WorldSimulation.CourtyardSpeeds)Check($"{speed}倍速身体干预不能同时获得睡眠恢复",()=>
{
    var sim=Fresh();var c=sim.Selected!;var father=sim.State.Creatures[1];
    sim.SetCourtyardSpeed(speed);
    sim.CollectBreedingMaterial(father.Id);sim.RequestForcedPregnancy(c.Id,father.Id,BirthMethod.Live);
    Require(FamilyRules.UnderIntervention(c),"fixture failed to start intervention");
    c.Interaction.Sleepiness=95;c.Energy=70;double sleep=c.Interaction.Sleepiness,energy=c.Energy;
    Tick(sim,.25);Require(FamilyRules.UnderIntervention(c),"intervention ended unexpectedly");
    Require(!c.Interaction.Resting&&c.Interaction.Sleepiness>=sleep&&c.Energy<=energy,"干预中仍在睡眠恢复");
    Require(sim.ExplainLifeActivity(c.Id).Action.Contains("干预"),sim.ExplainLifeActivity(c.Id).ToString());
});
Check("饥渴阈值与一餐中的持续需要一致",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    foreach(double water in new[]{0,19.9,20,34.9,35,39.9,40,100})
    {c.Interaction.Hydration=water;Require(CreatureNeedsObservation.Needs(c).Any(s=>s.Contains("喝到水"))==NaturalResources.WantsWater(c),"water threshold "+water);}
    c.Interaction.Hydration=100;
    foreach(bool seeking in new[]{false,true})foreach(double full in new[]{0,29.9,30,54.9,55,59.9,60,84.9,85,100})
    {c.Fullness=full;c.Interaction.MealSeeking=seeking;Require(CreatureNeedsObservation.Needs(c).Any(s=>s.Contains("吃到食物"))==NaturalResources.WantsFood(c),"meal threshold "+full);}
});
Check("并存需要不会互相覆盖，恢复后旧提示消失",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fullness=20;c.Interaction.Hydration=10;c.Energy=10;c.Interaction.Sleepiness=95;c.Family.SeekingWarmth=true;
    var needs=string.Join(" ",CreatureNeedsObservation.Needs(c));Require(needs.Contains("进食")&&needs.Contains("缺水")&&needs.Contains("困倦")&&needs.Contains("发冷"),needs);
    c.Fullness=c.Interaction.Hydration=c.Energy=100;c.Interaction.Sleepiness=20;c.Family.SeekingWarmth=false;
    Require(!CreatureNeedsObservation.Needs(c).Any(),"恢复后残留需要");
});
Check("资源接近受阻与寻找目标分别解释",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Hydration=30;
    Require(sim.ExplainLifeActivity(c.Id).Reason.Contains("尚未确定"),"未选目标");
    c.Interaction.ResourceTargetId="water";c.Interaction.Resources["water"]=new(){Kind=RoomObjectKind.Water};
    Require(sim.ExplainLifeActivity(c.Id).Reason.Contains("水源"),"已选目标");c.Interaction.ResourceStuckSeconds=2;
    Require(sim.ExplainLifeActivity(c.Id).Reason.Contains("受阻"),"受阻原因");
});
Check("状态观察只读且存读档保持含义",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Fullness=50;c.Interaction.Hydration=35;
    string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),description=Description(sim);var activity=sim.ExplainLifeActivity(c.Id);
    for(int n=0;n<30;n++){Description(sim);sim.ExplainLifeActivity(c.Id);}
    Require(before==JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),"观察改变世界状态");
    SaveStore.Validate(sim.State);var copy=new WorldSimulation(JsonSerializer.Deserialize<WorldState>(before,SaveStore.JsonOptions)!);
    Require(Description(copy)==description&&copy.ExplainLifeActivity(c.Id)==activity,"读档改变状态含义");
});
Console.WriteLine($"RESULT {passed} passed, {failed} failed");Environment.ExitCode=failed==0?0:1;

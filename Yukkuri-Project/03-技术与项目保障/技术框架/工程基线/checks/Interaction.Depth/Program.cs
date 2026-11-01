using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action)
{
    try{action();Console.WriteLine("PASS "+name);}
    catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}
}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
Check("高信任不盖过疼痛和局部失能",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    c.Trust=100;c.Attachment=100;c.Pain=60;
    Require(!InteractionReadiness.WillAcceptAffection(c,BodyPart.Head,SurfaceSide.Whole),"疼痛时仍接受亲昵");
    c.Pain=0;c.Interaction.FootIntegrity=20;
    Require(!InteractionReadiness.WillAcceptAffection(c,BodyPart.Bottom,SurfaceSide.Whole),"失能部位被好感覆盖");
    Require(!WorldSimulation.CanSelfMove(c),"底部受损仍能移动");
});
Check("近看重复安慰不能刷满关系或清空恐惧",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    c.Trust=40;c.Fear=50;sim.State.Mode=PlayMode.CloseUp;
    for(int n=0;n<100;n++){sim.ExecuteInteraction(c.Id,new(InteractionAction.Comfort));Tick(sim,3);}
    Require(c.Trust<=43.001&&c.Fear>=49.5,"重复安慰绕过同一场相处的收益预算");
    Require(sim.State.WorldSeconds==0,"近看推进了被动时间");
});
Check("受伤部位警戒存读档后不重抽",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    sim.Hurt(c,20,"测试接触伤害","player");
    var saved=Reload(sim).Selected!;
    Require(saved.Interaction.HumanBond.Caution[BodyPart.Side]==60,"警戒未保存");
    Require(saved.Interaction.HumanBond.CareBudget==0,"受伤后仍可立即刷安全经历");
    saved.Pain=0;saved.Trust=100;saved.Fear=0;
    Require(!InteractionReadiness.WillAcceptAffection(saved,BodyPart.Side,SurfaceSide.Whole),"伤好了就忘掉警戒");
});
Check("显式空相处记忆拒绝，旧档缺字段有默认值",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();
    var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions))!;
    var interaction=node["Creatures"]![0]!["Interaction"]!;
    interaction.AsObject().Remove("HumanBond");
    SaveStore.Validate(JsonSerializer.Deserialize<WorldState>(node.ToJsonString(),SaveStore.JsonOptions)!);
    interaction["HumanBond"]=null;
    bool rejected=false;
    try{SaveStore.Validate(JsonSerializer.Deserialize<WorldState>(node.ToJsonString(),SaveStore.JsonOptions)!);}
    catch(InvalidDataException){rejected=true;}
    Require(rejected,"显式null被静默当成旧档");
});
Environment.ExitCode=failures==0?0:1;

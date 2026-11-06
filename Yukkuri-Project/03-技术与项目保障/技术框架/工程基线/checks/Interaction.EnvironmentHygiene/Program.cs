using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Require(bool condition,string message){if(!condition)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
WorldSimulation Fresh(){var sim=WorldSimulation.CreateInteractionPlaytest();sim.State.RoomObjects.Clear();return sim;}
RoomObject Waste(WorldState state,RoomObjectKind kind=RoomObjectKind.Feces,int portions=1,string region="home")
{var obj=new RoomObject{Kind=kind,Portions=portions,RegionId=region};state.RoomObjects.Add(obj);return obj;}
int Score(WorldSimulation sim)=>EnvironmentHygiene.Score(sim.State,"home");
WorldSimulation Reload(WorldSimulation sim)
{SaveStore.Validate(sim.State);var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;SaveStore.Validate(state);return new(state);}
Check("clean and exact per-portion penalties",()=>
{
    var sim=Fresh();Require(Score(sim)==100,"clean");Waste(sim.State);Require(Score(sim)==92,"feces");
    Waste(sim.State,RoomObjectKind.Urine);Require(Score(sim)==87,"urine");
    Waste(sim.State,RoomObjectKind.Meal,100);Require(Score(sim)==87,"non-waste");
});
Check("stacks preserve actual portions and zero floor",()=>
{
    var sim=Fresh();var a=Waste(sim.State,portions:3);var b=Waste(sim.State,RoomObjectKind.Urine,2);
    Require(Score(sim)==66,"stack arithmetic");a.Portions=1;Waste(sim.State,portions:2);Require(Score(sim)==66,"split changed pollution");
    b.Portions=1000;Require(Score(sim)==0,"negative score");b.Portions=int.MaxValue;Require(Score(sim)==0,"overflow");
});
Check("regions and empty portions are independent",()=>
{
    var sim=Fresh();Waste(sim.State,portions:0);var other=Waste(sim.State,RoomObjectKind.Urine,3,"other");
    Require(Score(sim)==100&&EnvironmentHygiene.Score(sim.State,"other")==85,"region leakage");
    other.RegionId="home";Require(Score(sim)==85&&EnvironmentHygiene.Score(sim.State,"other")==100,"move cache");
});
Check("stored hand carried and raised objects excluded live",()=>
{
    var sim=Fresh();var obj=Waste(sim.State);
    obj.Stored=true;Require(Score(sim)==100,"stored");obj.Stored=false;
    obj.HeldByHand=true;Require(Score(sim)==100,"held");obj.HeldByHand=false;
    obj.CarrierId=sim.Selected!.Id;Require(Score(sim)==100,"carried");obj.CarrierId=null;
    obj.Height=.001;Require(Score(sim)==100,"raised");obj.Height=0;Require(Score(sim)==92,"returned ground");
});
Check("motion survives save and grounded residual record is harmless",()=>
{
    var sim=Fresh();var obj=Waste(sim.State);sim.State.PortableMotions.Add(new(){Id=obj.Id,Height=.6});
    Require(Score(sim)==100,"flight");var loaded=Reload(sim);Require(Score(loaded)==100,"flight reload");
    var motion=loaded.State.PortableMotions.Single();motion.Height=0;motion.VerticalVelocity=0;loaded.State.RoomObjects.Single().HeldByHand=false;
    Require(Score(loaded)==92,"grounded record excluded forever");motion.Held=true;Require(Score(loaded)==100,"held motion");
});
Check("actual throw landing resumes ground score",()=>
{
    var sim=Fresh();var obj=Waste(sim.State);sim.LaunchReleasedSupply(obj.Id,Vector2.Zero);
    Require(Score(sim)==100&&sim.State.PortableMotions.Count==1,"launch");
    for(int n=0;n<30;n++)sim.AdvanceInteractionPresentation(.05);
    Require(sim.State.PortableMotions.Count==0&&!obj.HeldByHand&&Score(sim)==92,"landing");
});
Check("real cleaning and paused reads immediately recover without ticking",()=>
{
    var sim=Fresh();var obj=Waste(sim.State,portions:4);Require(Score(sim)==68,"initial");
    Require(sim.CleanSceneObject(obj.Id)=="已清理干净。"&&Score(sim)==100,"actual cleanup");
    obj=Waste(sim.State);sim.State.Settings.Pause=true;Require(Score(sim)==92,"paused read");
    obj.Stored=true;Require(Score(sim)==100,"paused derived refresh");
});
Check("reload derives current objects and does not serialize a score",()=>
{
    var sim=Fresh();var obj=Waste(sim.State,portions:2);Require(Score(sim)==84,"before");
    string json=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);Require(!json.Contains("EnvironmentScore")&&!json.Contains("EnvironmentHygiene"),"cache leaked");
    var loaded=Reload(sim);Require(Score(loaded)==84,"reload");loaded.State.RoomObjects.Clear();Require(Score(loaded)==100,"stale score");
    obj.HeldByHand=true;Require(Score(sim)==100,"hand before save");Require(Score(Reload(sim))==84,"existing transient hand restore semantics");
});
Check("waste feedback names counts and nominal penalties without claiming recovery",()=>
{
    var sim=Fresh();var obj=Waste(sim.State,portions:1000);string label=EnvironmentHygiene.DescribeWaste(sim.State,obj);
    Require(label.Contains("粪便")&&label.Contains("1000 份")&&label.Contains("每份扣 8")&&label.Contains("最低 0"),"feces label");
    obj.Kind=RoomObjectKind.Urine;Require(EnvironmentHygiene.DescribeWaste(sim.State,obj).Contains("每份扣 5"),"urine label");
    obj.Stored=true;Require(EnvironmentHygiene.DescribeWaste(sim.State,obj).Contains("当前不计"),"excluded label");
    Require(RoomResourceText.Label(obj,sim.State)=="尿液"&&RoomResourceText.Describe(obj,sim.State).Contains("当前不计"),"observe panel label and impact");
});
Console.WriteLine($"Environment hygiene: {failures} failures");return failures==0?0:1;

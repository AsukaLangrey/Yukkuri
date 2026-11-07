using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;
int failures=0,passes=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action action){try{action();Console.WriteLine("PASS "+name);passes++;}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
WorldSimulation Yard(int count=1)
{
    var sim=WorldSimulation.CreateCourtyardEconomy(20261102);sim.State.Shelters.Clear();sim.State.RoomObjects.Clear();sim.Config.WildGrassEnabled=false;
    sim.State.Creatures.RemoveRange(count,sim.State.Creatures.Count-count);sim.State.AutonomousBreeding=false;
    for(int n=0;n<count;n++)
    {
        var c=sim.State.Creatures[n];GrowthRules.Initialize(c,false);c.X=c.TargetX=n*2;c.Z=c.TargetZ=3;
        c.Fullness=c.Energy=c.Interaction.Hydration=c.Health=100;c.Fear=c.Pain=c.Resentment=0;c.Trust=80;c.Patience=.8;
        c.Interaction.Emotion.Alarm=0;c.Interaction.Emotion.Manmanlai=80;c.Interaction.Emotion.SafeSeconds=30;
        c.Interaction.NextAutonomyAt=1e9;c.Family.NextSocialAt=c.Family.NextCareAt=c.Family.NextBreedingDecisionAt=1e9;
        c.DecisionIn=1e9;c.Interaction.ToyInterest=0;c.Personality.Kindness=.1;c.Interaction.RuleLearning=0;c.Registration.Grade="";
    }
    return sim;
}
void Tick(WorldSimulation sim,double realSeconds){for(double t=0;t<realSeconds-1e-9;t+=.025){sim.AdvanceInteractionPresentation(.025);sim.Advance(.025);}}
RoomObject Waste(WorldSimulation sim,RoomObjectKind kind=RoomObjectKind.Feces,int portions=1,double x=1.8)
{
    var c=sim.State.Creatures[0];var o=new RoomObject{Id="waste-"+sim.State.RoomObjects.Count,Kind=kind,RegionId=c.RegionId,X=x,Z=c.Z,Portions=portions};sim.State.RoomObjects.Add(o);return o;
}
Check("waste names and descriptions are nonempty",()=>{foreach(var kind in new[]{RoomObjectKind.Feces,RoomObjectKind.Urine}){var sim=Yard();var o=Waste(sim,kind);Require(RoomResourceText.Label(o).Length>0&&RoomResourceText.Describe(o).Contains("环境评分"),"missing label");}});
Check("score counts portions, isolates regions and recovers on cleaning/reload",()=>{
    var sim=Yard();var c=sim.Selected!;Require(sim.EnvironmentScore(c.RegionId)==100,"clean");var a=Waste(sim,RoomObjectKind.Feces,2);var b=Waste(sim,RoomObjectKind.Urine);
    Require(sim.EnvironmentScore(c.RegionId)==79,"weights");b.Stored=true;Require(sim.EnvironmentScore(c.RegionId)==84,"storage");b.Stored=false;b.RegionId="elsewhere";Require(sim.EnvironmentScore(c.RegionId)==84,"region");
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;var reload=new WorldSimulation(state);Require(reload.EnvironmentScore(c.RegionId)==84,"reload");
    Require(sim.CleanRoomWaste(a.Id)&&sim.EnvironmentScore(c.RegionId)==100,"cleaning");
});
Check("kindness and education lower dirt tolerance, very poor disposition tolerates filth",()=>{
    var sim=Yard();var c=sim.Selected!;Waste(sim,RoomObjectKind.Feces,30);Require(sim.EnvironmentDiscomfort(c)==0,"poor intolerance");double previous=0;
    foreach(double k in new[]{.2,.4,.65,.8,1.0}){c.Personality.Kindness=k;double value=sim.EnvironmentDiscomfort(c);Require(value>=previous,"non-monotonic");previous=value;}
    c.Personality.Kindness=.1;c.Interaction.RuleLearning=100;Require(sim.EnvironmentDiscomfort(c)>50,"education ignored");
});
Check("dirty environment reduces actual Manmanlai and cleaning permits recovery",()=>{
    var sim=Yard();var c=sim.Selected!;c.Personality.Kindness=1;c.Interaction.Restraint=RestraintKind.Box;var o=Waste(sim,RoomObjectKind.Feces,20);
    Tick(sim,20);double dirty=c.Interaction.Emotion.Manmanlai;Require(dirty<65,"mood did not fall "+dirty);sim.CleanRoomWaste(o.Id);Tick(sim,20);Require(c.Interaction.Emotion.Manmanlai>dirty,"mood did not recover");
});
foreach(var mode in new[]{"kind","educated","poor"})Check("real voluntary cleaning "+mode,()=>{
    var sim=Yard();var c=sim.Selected!;c.Personality.Kindness=mode=="kind"?1:.1;c.Interaction.RuleLearning=mode=="educated"?100:0;
    var o=Waste(sim,RoomObjectKind.Feces,2);Tick(sim,30);bool removed=!sim.State.RoomObjects.Contains(o);Require(removed==(mode!="poor"),"clean result "+c.Activity);
    if(removed)Require(c.Interaction.Development.Skills[LifeSkill.Cleaning].Completed>0&&sim.EnvironmentScore(c.RegionId)==100,"no real evidence or score recovery");
});
foreach(int speed in WorldSimulation.CourtyardSpeeds)foreach(var kind in new[]{ExcretionKind.Urine,ExcretionKind.Feces})Check("leave shared hut before "+kind+" speed="+speed,()=>{
    var sim=Yard();sim.SetCourtyardSpeed(speed);var c=sim.Selected!;sim.State.ShelterStock=1;Require(sim.PlaceShelter(0,-5).Success,"place");var h=sim.State.Shelters.Single();
    c.X=c.TargetX=h.X;c.Z=c.TargetZ=h.Z;c.Interaction.Physiology.Bladder=kind==ExcretionKind.Urine?99:0;c.Interaction.Physiology.Bowel=kind==ExcretionKind.Feces?99:0;
    h.Residents.Add(new(){CreatureId=c.Id,Slot=2,Phase=ShelterVisitPhase.Inside,VisualHidden=true});
    bool produced=false;
    for(int n=0;n<2400&&!produced;n++)
    {
        Tick(sim,.025);foreach(var o in sim.State.RoomObjects.Where(o=>WorldSimulation.IsWaste(o.Kind)))
        {Require(!ShelterGeometry.OverlapsInterior(o.X-h.X,o.Z-h.Z,0,ShelterGeometry.Scale(h)),"indoor waste");produced=true;}
    }
    Require(produced,"never exited/excreted "+c.Activity+" "+c.X+","+c.Z);Require(c.Interaction.Physiology.Bladder<85||kind!=ExcretionKind.Urine,"urine not relieved");
});
Check("interrupted indoor excretion holds needs without making indoor waste",()=>{
    var sim=Yard();var c=sim.Selected!;sim.State.ShelterStock=1;sim.PlaceShelter(0,-5);var h=sim.State.Shelters.Single();c.X=c.TargetX=h.X;c.Z=c.TargetZ=h.Z;
    c.Interaction.Restraint=RestraintKind.Frame;c.Interaction.Physiology.Bladder=99;c.Interaction.Physiology.Excreting=ExcretionKind.Urine;c.Interaction.Physiology.Remaining=.1;
    Tick(sim,10);Require(!sim.State.RoomObjects.Any(o=>WorldSimulation.IsWaste(o.Kind))&&!c.Interaction.Physiology.Active&&c.Interaction.Physiology.Bladder>=99,"indoor cancellation consumed need");
});
foreach(int speed in WorldSimulation.CourtyardSpeeds)Check("reaction and pose clocks follow speed "+speed,()=>{
    var sim=Yard();sim.SetCourtyardSpeed(speed);var c=sim.Selected!;c.Interaction.ReactionPose="sing";c.Interaction.ReactionPoseRemaining=10;c.ReactionRemaining=10;
    for(int n=0;n<10;n++)sim.AdvanceInteractionPresentation(.05);
    Require(Math.Abs(c.Interaction.ReactionPoseRemaining-(10-.5*speed))<1e-7,"reaction clock");
    var clock=new ActorPoseClock();clock.Advance("sing",1,0,false);for(int n=0;n<10;n++)clock.Advance("sing",1,.05f*speed,false);Require(Math.Abs(clock.Age-.5*speed)<1e-5,"pose clamp");
    sim.State.Settings.Pause=true;double before=c.Interaction.ReactionPoseRemaining;sim.AdvanceInteractionPresentation(.1);Require(c.Interaction.ReactionPoseRemaining==before,"pause");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;sim.AdvanceInteractionPresentation(.1);Require(Math.Abs(c.Interaction.ReactionPoseRemaining-(before-.1))<1e-7,"close clock");
});
foreach(int speed in WorldSimulation.CourtyardSpeeds)Check("hop planted phases, turns and full cadence at "+speed+"x",()=>{
    var hop=new ActorHopMotion();hop.Reset(Vector2.Zero);var target=new Vector2(0,1);float dt=.001f/speed;
    for(int n=0;n<50;n++)hop.Advance(target,dt,true,false,speed);Require(hop.Position==Vector2.Zero,"anticipation slides");
    for(int n=50;n<400;n++)hop.Advance(target,dt,true,false,speed);Require(Vector2.Distance(hop.Position,target)<1e-5,"not landed");
    var planted=hop.Position;var next=new Vector2(1,1);for(int n=0;n<40;n++)hop.Advance(next,dt,true,false,speed);Require(hop.Position==planted,"landing slides");
    for(int n=0;n<60;n++)hop.Advance(next,dt,true,false,speed);var position=hop.Position;hop.Advance(next,.1f,true,true,speed);Require(hop.Position==position,"pause slides");
});
Check("large animation delta preserves cycle remainder",()=>{var a=new ActorHopMotion();var b=new ActorHopMotion();a.Reset(Vector2.Zero);b.Reset(Vector2.Zero);var p=new Vector2(0,1);a.Advance(p,.65f,true,false);for(int n=0;n<65;n++)b.Advance(p,.01f,true,false);Require(Vector2.Distance(a.Position,b.Position)<1e-5&&a.Active==b.Active,"discarded time");});
Check("breeding reason and action share live eligibility without side effects",()=>{
    var sim=Yard(2);var m=sim.State.Creatures[0];var f=sim.State.Creatures[1];m.Body.ReproductionRole=ReproductionRole.Maternal;f.Body.ReproductionRole=ReproductionRole.Paternal;
    m.Family.Peers[f.Id]=new(){Affection=90,Familiarity=90};f.Family.Peers[m.Id]=new(){Affection=90,Familiarity=90};
    Require(sim.PregnancyRequestReason(m.Id,f.Id,BirthMethod.Stem)==null,"adult pair: "+sim.PregnancyRequestReason(m.Id,f.Id,BirthMethod.Stem));Require(m.Family.Pregnancy.Phase==PregnancyPhase.None,"query mutated");
    GrowthRules.Initialize(m,true);string reason=sim.PregnancyRequestReason(m.Id,f.Id,BirthMethod.Stem)!;Require(reason.Contains("成熟")&&sim.RequestPregnancy(m.Id,f.Id,BirthMethod.Stem)==reason,"child validation mismatch");
    GrowthRules.Initialize(m,false);sim.RequestPregnancy(m.Id,f.Id,BirthMethod.Stem);Require(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"preparing");Tick(sim,25);Require(m.Family.Pregnancy.Phase==PregnancyPhase.Gestating,"acceptance "+m.Family.LastEvent+" "+m.Activity);
    Require(sim.PregnancyRequestReason(m.Id,f.Id,BirthMethod.Live)!=null,"restarted pregnancy");
});
int savedFlag=Array.IndexOf(args,"--save-directory");
if(savedFlag>=0&&savedFlag+1<args.Length)Check("existing preview save validates and advances without identity loss",()=>{
    var store=new SaveStore(args[savedFlag+1]);var load=store.Load(0);var sim=new WorldSimulation(load.State);var ids=sim.State.Creatures.Select(c=>c.Id).ToArray();
    double score=sim.EnvironmentScore(sim.State.ActiveRegionId);Require(double.IsFinite(score)&&score>=0&&score<=100,"invalid derived score");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.Management;Tick(sim,2);SaveStore.Validate(sim.State);
    Require(ids.All(id=>sim.State.Creatures.Any(c=>c.Id==id)),"existing identity lost");
    Console.WriteLine($"Save audit: {ids.Length} original individuals; world={sim.State.WorldSeconds:0.00}; environment={score:0.0}; original file not written.");
});
Console.WriteLine($"RESULT {passes} passed, {failures} failed");return failures==0?0:1;

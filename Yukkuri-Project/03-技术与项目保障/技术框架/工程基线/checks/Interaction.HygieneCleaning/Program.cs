using System.Numerics;
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

Check("qualified comfortable cleaners do not start; boundary is strict",()=>
{
    foreach(string basis in new[]{"kind","education","bronze"})
    {
        var sim=Fresh();var c=sim.Selected!;var waste=Waste(sim);
        if(basis=="kind")c.Personality.Kindness=.65;
        else if(basis=="education")c.Interaction.RuleLearning=50;
        else c.Registration.Grade="铜牌";
        Require(EnvironmentHygiene.Discomfort(sim.State,c)==0,"fixture not comfortable");
        Tick(sim,4);Require(c.Interaction.CleaningTargetId==null&&Completed(c,LifeSkill.Cleaning)==0&&sim.State.RoomObjects.Contains(waste),basis);
    }
});
Check("kind educated and licensed uncomfortable cleaners really approach and finish once",()=>
{
    foreach(string basis in new[]{"kind","education","bronze"})
    {
        var sim=Fresh();var c=sim.Selected!;var waste=Waste(sim,2);
        if(basis=="kind")c.Personality.Kindness=.65;
        else if(basis=="education"){c.Personality.Kindness=.05;c.Interaction.RuleLearning=50;}
        else c.Registration.Grade="铜牌";
        var background=Waste(sim,7,7);background.Portions=10;
        Require(EnvironmentHygiene.Discomfort(sim.State,c)>0,"fixture not uncomfortable");
        Tick(sim,.5);Require(c.Interaction.CleaningTargetId==waste.Id&&c.X>0&&c.X<1&&Completed(c,LifeSkill.Cleaning)==0,"no actual approach "+basis);
        Tick(sim,6);Require(!sim.State.RoomObjects.Contains(waste)&&Completed(c,LifeSkill.Cleaning)==1,"no completion "+basis);
        Tick(sim,3);Require(Completed(c,LifeSkill.Cleaning)==1,"duplicate completion");
    }
});
Check("discomfort and skill do not replace eligibility",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=.6;c.Interaction.RuleLearning=49;c.Interaction.CleaningLearning=100;
    var waste=Waste(sim);waste.Portions=10;Require(EnvironmentHygiene.Discomfort(sim.State,c)>0,"fixture");
    Tick(sim,5);Require(sim.State.RoomObjects.Contains(waste)&&c.Interaction.CleaningTargetId==null,"ineligible started");
});
Check("external cleanup resolves discomfort but current portion finishes; next does not start",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=.65;var target=Waste(sim,2);
    var remaining=Waste(sim,2.8);remaining.Kind=RoomObjectKind.Urine;var extra=Waste(sim,7,7);extra.Portions=3;
    Tick(sim,.5);Require(c.Interaction.CleaningTargetId==target.Id,"did not begin");
    Require(sim.CleanRoomWaste(extra.Id),"external cleanup");remaining.Stored=true;
    Require(EnvironmentHygiene.Discomfort(sim.State,c)==0,"did not become comfortable");
    Tick(sim,6);Require(!sim.State.RoomObjects.Contains(target)&&Completed(c,LifeSkill.Cleaning)==1,"abandoned current portion");
    remaining.Stored=false;Tick(sim,20);
    Require(sim.State.RoomObjects.Contains(remaining)&&Completed(c,LifeSkill.Cleaning)==1&&c.Interaction.CleaningTargetId==null,"started comfortable next work");
});
Check("explicit cleaning instruction can still clean comfortable waste",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=.65;var waste=Waste(sim);
    Require(EnvironmentHygiene.Discomfort(sim.State,c)==0,"fixture");sim.SetLifeDuty(c.Id,LifeDuty.Cleaning);Tick(sim,4);
    Require(!sim.State.RoomObjects.Contains(waste)&&Completed(c,LifeSkill.Cleaning)==1,"instruction incorrectly gated");
});
Check("needs pain danger rest and care still interrupt without completing",()=>
{
    foreach(string reason in new[]{"food","water","energy","pain","fear","rest","care"})
    {
        var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=1;var waste=Waste(sim,2);Tick(sim,.5);
        Require(c.Interaction.CleaningTargetId==waste.Id,"not started");
        switch(reason){case "food":c.Fullness=10;break;case "water":c.Interaction.Hydration=10;break;case "energy":c.Energy=10;break;case "pain":c.Pain=40;break;case "fear":c.Fear=70;break;case "rest":c.Interaction.RestRemaining=10;c.Interaction.Resting=true;break;case "care":var child=sim.State.Creatures[2];child.IsYoung=true;child.InnerAgeSeconds=child.Body.AgeSeconds=0;child.ParentId=c.Id;child.Family.Warmth=30;c.Family.CareTargetId=child.Id;break;}
        Tick(sim,.3);Require(c.Interaction.CleaningTargetId==null&&Completed(c,LifeSkill.Cleaning)==0&&sim.State.RoomObjects.Contains(waste),reason);
    }
});
void Exclude(WorldSimulation sim,RoomObject waste,string kind)
{
    switch(kind){case "empty":waste.Portions=0;break;case "motion-held":sim.State.PortableMotions.Add(new(){Id=waste.Id,Held=true});break;case "stored":waste.Stored=true;break;case "hand":waste.HeldByHand=true;break;case "carrier":waste.CarrierId=sim.State.Creatures[1].Id;break;case "height":waste.Height=.5;break;case "flight":sim.LaunchReleasedSupply(waste.Id,Vector2.Zero);break;}
}
Check("non-ground waste excluded from search and demonstration",()=>
{
    foreach(string kind in new[]{"empty","motion-held","stored","hand","carrier","height","flight"})
    {
        var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=1;var waste=Waste(sim);Exclude(sim,waste,kind);
        Require(sim.CleaningLessonReason(c.Id)!=null,"lesson allowed "+kind);sim.TeachCleaning(c.Id);
        sim.Advance(sim.Config.TickSeconds);Require(c.Interaction.CleaningTargetId==null&&(kind=="empty"||sim.State.RoomObjects.Contains(waste))&&Completed(c,LifeSkill.Cleaning)==0,"search selected "+kind);
    }
});
Check("in-transit current target interrupts before timed completion without awarding evidence",()=>
{
    foreach(string kind in new[]{"empty","motion-held","stored","hand","carrier","height","flight"})
    {
        var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=1;var waste=Waste(sim);
        Tick(sim,1);Require(c.Interaction.CleaningTargetId==waste.Id&&c.Interaction.CleaningSeconds>0,"not timing");Exclude(sim,waste,kind);
        sim.Advance(sim.Config.TickSeconds);Require(c.Interaction.CleaningTargetId==null&&(kind=="empty"||sim.State.RoomObjects.Contains(waste))&&Completed(c,LifeSkill.Cleaning)==0,"cleaned transit "+kind);
        Require(c.Interaction.Development.Skills[LifeSkill.Cleaning].Interrupted==1,"missing interruption "+kind);
    }
});
Check("real throw lands then starts a fresh approach and timed completion",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=1;var waste=Waste(sim,2);sim.LaunchReleasedSupply(waste.Id,Vector2.Zero);
    sim.Advance(sim.Config.TickSeconds);Require(c.Interaction.CleaningTargetId==null,"airborne search");
    for(int n=0;n<30;n++)sim.AdvanceInteractionPresentation(.05);
    Require(EnvironmentHygiene.IsGroundWaste(sim.State,waste),"did not actually land");
    Tick(sim,.5);Require(c.Interaction.CleaningTargetId==waste.Id&&c.X>0&&Completed(c,LifeSkill.Cleaning)==0,"landing not reconsidered");
    Tick(sim,7);Require(Completed(c,LifeSkill.Cleaning)==1&&!sim.State.RoomObjects.Contains(waste),"landed waste not completed");
});
Check("synthetic SaveStore reload retains active work and completion occurs once",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=1;var waste=Waste(sim,2);Tick(sim,.5);
    int attempts=c.Interaction.Development.Skills[LifeSkill.Cleaning].Attempts;sim=Reload(sim);c=sim.Selected!;
    Require(c.Interaction.CleaningTargetId==waste.Id&&Completed(c,LifeSkill.Cleaning)==0,"reload fabricated or lost work");
    Tick(sim,7);Require(Completed(c,LifeSkill.Cleaning)==1&&c.Interaction.Development.Skills[LifeSkill.Cleaning].Attempts==attempts,"reload duplicated work");
    sim=Reload(sim);Tick(sim,15);Require(Completed(sim.Selected!,LifeSkill.Cleaning)==1,"completion replay");
});
Check("delegation cannot reserve held waste and in-flight order expires without credit",()=>
{
    var sim=Fresh();var issuer=sim.Selected!;issuer.Personality.Kindness=.05;issuer.Personality.Pride=.95;
    var worker=sim.State.Creatures[1];worker.X=worker.TargetX=1;worker.Z=worker.TargetZ=0;worker.Personality.Kindness=.2;
    worker.Family.Peers[issuer.Id]=new(){Affection=30};
    var waste=Waste(sim,.6);waste.HeldByHand=true;sim.Advance(sim.Config.TickSeconds);
    Require(worker.Interaction.CleaningTargetId==null&&worker.Interaction.Development.CleaningOrderedById==null,"delegated hand-held object");
    waste.HeldByHand=false;Tick(sim,2);
    Require(worker.Interaction.CleaningTargetId==waste.Id&&worker.Interaction.Development.CleaningOrderedById==issuer.Id,"real peer order not begun");
    sim.LaunchReleasedSupply(waste.Id,Vector2.Zero);sim.Advance(sim.Config.TickSeconds);
    Require(worker.Interaction.CleaningTargetId==null&&worker.Interaction.Development.CleaningOrderedById==null&&Completed(worker,LifeSkill.Cleaning)==0,"stale order survived or awarded credit");
});
Check("active target thrown and reloaded interrupts until actual landing then reattempts once",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Personality.Kindness=1;var waste=Waste(sim,2);Tick(sim,.5);
    sim.LaunchReleasedSupply(waste.Id,Vector2.Zero);sim=Reload(sim);c=sim.Selected!;
    var loaded=sim.State.RoomObjects.Single(o=>o.Id==waste.Id);Require(!EnvironmentHygiene.IsGroundWaste(sim.State,loaded),"flight lost on reload");
    sim.Advance(sim.Config.TickSeconds);Require(c.Interaction.CleaningTargetId==null&&Completed(c,LifeSkill.Cleaning)==0,"stale loaded target completed");
    Tick(sim,15);Require(!sim.State.RoomObjects.Contains(loaded)&&Completed(c,LifeSkill.Cleaning)==1&&c.Interaction.Development.Skills[LifeSkill.Cleaning].Attempts==2,"landing did not produce exactly one new attempt");
});
Console.WriteLine($"Hygiene cleaning checks: {failures} failure(s)");return failures==0?0:1;
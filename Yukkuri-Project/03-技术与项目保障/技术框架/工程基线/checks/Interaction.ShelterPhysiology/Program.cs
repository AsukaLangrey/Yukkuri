using System.Text.Json;
using Yukkuri.Simulation;
int failures=0,count=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action test){count++;try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
WorldSimulation Inside(bool doorOpen=false)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261106);var c=sim.Selected!;
    sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);sim.State.RoomObjects.Clear();sim.SetAutonomousBreeding(false);
    c.X=c.TargetX=-5;c.Z=c.TargetZ=-5;
    Require(sim.BuyShelter()&&sim.PlaceShelter(0,0).Success,"house fixture failed");
    c.X=c.TargetX=0;c.Z=c.TargetZ=0;c.Fullness=c.Energy=c.Health=c.Interaction.Hydration=100;
    c.Fear=50;c.Pain=0;c.Interaction.Emotion.Alarm=30;c.Interaction.Emotion.SafeSeconds=0;
    c.Interaction.NextAutonomyAt=c.DecisionIn=c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    var s=sim.State.Shelters.Single();s.GuestId=c.Id;s.Phase=ShelterVisitPhase.Inside;s.DoorOpen=doorOpen;
    c.Interaction.Physiology.Bladder=c.Interaction.Physiology.Bowel=0;
    return sim;
}
void Step(WorldSimulation sim){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}
void Seconds(WorldSimulation sim,double seconds){for(int n=0;n<(int)Math.Round(seconds/.05);n++)Step(sim);}
bool Overlaps(Creature c,ShelterState s)=>ShelterGeometry.OverlapsInterior(c.X-s.X,c.Z-s.Z,WorldSimulation.ShelterPassageRadius(c));
foreach(var kind in new[]{ExcretionKind.Urine,ExcretionKind.Feces})
{
Check(kind+" cannot begin indoors or lose its need",()=>
{
    var sim=Inside();var c=sim.Selected!;var p=c.Interaction.Physiology;
    if(kind==ExcretionKind.Urine)p.Bladder=100;else p.Bowel=100;
    Seconds(sim,5);
    Require(!p.Active&&p.Urinations==0&&p.Defecations==0&&!sim.State.RoomObjects.Any(o=>WorldSimulation.IsWaste(o.Kind)),"indoor excretion occurred");
    Require((kind==ExcretionKind.Urine?p.Bladder:p.Bowel)==100,"unmet need was consumed");
});
Check(kind+" already active is cancelled indoors before completion",()=>
{
    var sim=Inside();var c=sim.Selected!;var p=c.Interaction.Physiology;
    p.Bladder=p.Bowel=100;p.Excreting=kind;p.Remaining=.25;
    sim.AdvanceInteractionPresentation(.05);
    Require(!p.Active&&p.Remaining==0&&p.Excreting==ExcretionKind.None,"presentation did not interrupt indoor action");
    Seconds(sim,.5);Require(p.Bladder==100&&p.Bowel==100&&p.Urinations==0&&p.Defecations==0,"indoor completion consumed need");
});
}
Check("daytime burden 80 requests real exit before outdoor threshold",()=>
{
    var sim=Inside();var c=sim.Selected!;var s=sim.State.Shelters.Single();c.Interaction.Physiology.Bladder=80;
    Seconds(sim,2);Require(s.ExitRequested&&s.BlockedSeconds>=1,"burden did not request blocked-door exit");
    Require(Overlaps(c,s)&&c.Interaction.Physiology.Bladder>=80,"need vanished or exit teleported");
    Require(sim.InteractionHelpFor(c)?.Kind==InteractionHelpKind.Shelter,"blocked exit did not request help");
});
Check("resting exit cannot receive sleep benefit while moving",()=>
{
    var sim=Inside(true);var c=sim.Selected!;var s=sim.State.Shelters.Single();
    c.Fear=0;c.Interaction.Emotion.Alarm=0;c.Interaction.Emotion.SafeSeconds=100;
    c.Energy=50;c.Interaction.RestRemaining=10;s.ExitRequested=true;
    double energy=c.Energy,remaining=c.Interaction.RestRemaining,x=c.X,z=c.Z;
    sim.Advance(.25);
    Require(WorldSimulation.Distance(x,z,c.X,c.Z)>0,"fixture did not move through exit");
    Require(!c.Interaction.Resting&&c.Energy<=energy&&c.Interaction.RestRemaining==remaining,$"moving body rested: energy {energy}->{c.Energy}, remaining {remaining}->{c.Interaction.RestRemaining}, resting={c.Interaction.Resting}");
});
Check("partial body across doorway cannot excrete without a reservation",()=>
{
    var sim=Inside(true);var c=sim.Selected!;var s=sim.State.Shelters.Single();var p=c.Interaction.Physiology;
    c.Z=c.TargetZ=ShelterGeometry.InteriorDepth*.5+WorldSimulation.ShelterPassageRadius(c)-.01;
    s.GuestId=null;s.Phase=ShelterVisitPhase.Empty;p.Bladder=100;
    Require(Overlaps(c,s),"fixture does not overlap interior");sim.Advance(.25);
    Require(!p.Active&&p.Urinations==0&&p.Bladder==100,"partial body began excretion");
});
Check("outdoor body at same coordinates as a different-region shelter is not blocked",()=>
{
    var sim=Inside(true);var c=sim.Selected!;var s=sim.State.Shelters.Single();
    s.RegionId="other-region";s.GuestId=null;s.Phase=ShelterVisitPhase.Empty;c.Interaction.Physiology.Bladder=100;
    sim.Advance(.25);Require(c.Interaction.Physiology.Active,"unrelated-region house blocked excretion");
});
Check("closed door preserves stationary supported rest and unmet burden",()=>
{
    var sim=Inside();var c=sim.Selected!;var s=sim.State.Shelters.Single();var p=c.Interaction.Physiology;
    c.Energy=10;c.Interaction.RestRemaining=10;p.Bladder=100;double x=c.X,z=c.Z;
    Seconds(sim,2);
    Require(c.X==x&&c.Z==z&&c.Interaction.Resting&&c.Energy>10,"closed-door supported rest lost");
    Require(p.Bladder==100&&!p.Active&&p.Urinations==0&&s.ExitRequested,"closed door lost urgent need");
});
Check("opening blocked door resumes physical exit before completing excretion",()=>
{
    var sim=Inside();var c=sim.Selected!;var s=sim.State.Shelters.Single();var p=c.Interaction.Physiology;
    c.Energy=10;c.Interaction.RestRemaining=10;p.Bladder=100;Seconds(sim,2);s.DoorOpen=true;
    bool moved=false,sawOutsideWaste=false;
    for(int n=0;n<600&&p.Urinations==0;n++)
    {
        double x=c.X,z=c.Z,energy=c.Energy;Step(sim);double distance=WorldSimulation.Distance(x,z,c.X,c.Z);
        Require(distance<.25,"exit teleported");
        if(distance>.00001){moved=true;Require(!c.Interaction.Resting&&c.Energy<=energy,"moving exit received rest");}
        Require(!Overlaps(c,s)||!p.Active,"partly indoor body started excretion");
        if(p.Urinations>0)sawOutsideWaste=!Overlaps(c,s)&&sim.State.RoomObjects.Count(o=>o.Kind==RoomObjectKind.Urine)==1;
    }
    Require(moved&&sawOutsideWaste&&p.Urinations==1,"open door did not complete one real outdoor excretion");
});
Check("physical doorway obstacle allows rest then removal resumes exit",()=>
{
    var sim=Inside(true);var c=sim.Selected!;var s=sim.State.Shelters.Single();
    sim.State.RoomObjects.Add(new(){Id="door-block",RegionId=c.RegionId,Kind=RoomObjectKind.Ball,X=0,Z=1.25});
    c.Energy=10;c.Interaction.RestRemaining=10;c.Interaction.Physiology.Bladder=100;
    bool rested=false;
    for(int n=0;n<80;n++)
    {
        double x=c.X,z=c.Z,energy=c.Energy;Step(sim);
        if(WorldSimulation.Distance(x,z,c.X,c.Z)>.00001)Require(!c.Interaction.Resting&&c.Energy<=energy,"blocked route moved while resting");
        rested|=c.Interaction.Resting;
    }
    Require(rested&&s.BlockedSeconds>=1&&Overlaps(c,s),"physical obstacle lost stationary rest or waiting");
    sim.State.RoomObjects.RemoveAll(o=>o.Id=="door-block");double before=c.Z;
    Seconds(sim,.5);Require(c.Z>before&&!c.Interaction.Resting,"cleared route did not resume movement");
});
Check("save reload cannot complete a saved indoor action or erase its burden",()=>
{
    var sim=Inside();var c=sim.Selected!;var p=c.Interaction.Physiology;p.Bladder=100;p.Excreting=ExcretionKind.Urine;p.Remaining=.25;
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);sim=new(state);c=sim.Selected!;p=c.Interaction.Physiology;Seconds(sim,1);
    Require(!p.Active&&p.Bladder==100&&p.Urinations==0&&!sim.State.RoomObjects.Any(o=>WorldSimulation.IsWaste(o.Kind)),"reload consumed indoor need");
});
Check("immobile resident retains need without receiving a fake exit",()=>
{
    var sim=Inside(true);var c=sim.Selected!;var s=sim.State.Shelters.Single();c.Interaction.FootIntegrity=20;c.Energy=10;c.Interaction.Physiology.Bladder=100;
    Seconds(sim,2);Require(Overlaps(c,s)&&c.Interaction.Physiology.Bladder==100&&c.Interaction.Physiology.Urinations==0&&s.ExitRequested,"immobile resident lost need or teleported");
    Require(c.Energy>10,"immobile resident cannot rest in place");
});
Check("below-threshold sheltered resident does not get an excretion exit request",()=>
{var sim=Inside();var c=sim.Selected!;c.Interaction.Physiology.Bladder=79;sim.Advance(.25);Require(!sim.State.Shelters.Single().ExitRequested,"below-threshold need caused exit");});Console.WriteLine($"RESULT {count-failures}/{count} passed; {failures} failed");return failures==0?0:1;

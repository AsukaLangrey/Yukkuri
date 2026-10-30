using System.Text.Json;
using Yukkuri.Simulation;

// Source-only here; execute only on the authorised independent remote test machine.
int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action test)
{try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Ceiling(seconds/.05);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();
    sim.State.Creatures.RemoveAt(2);sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
    for(int n=0;n<2;n++)
    {
        var c=sim.State.Creatures[n];c.Body.SpeciesId="reimu";
        c.Body.ReproductionRole=n==0?ReproductionRole.Maternal:ReproductionRole.Paternal;
        c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=c.Health=100;c.Trust=80;c.Fear=c.Pain=c.Resentment=0;
        c.TrueDisposition="gentle";c.Aggression=.08;c.Patience=.6;c.Courage=.2;
        c.Personality.Initialized=true;c.Personality.Kindness=.8;c.Personality.Carelessness=0;
        c.X=c.TargetX=n*2;c.Z=c.TargetZ=0;c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.NextSocialAt=c.Family.NextCareAt=10000;
        c.Interaction.Emotion.Manmanlai=80;c.Interaction.Emotion.SafeSeconds=60;
    }
    var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];
    a.Family.Peers[b.Id]=new(){Familiarity=90,Affection=90};b.Family.Peers[a.Id]=new(){Familiarity=90,Affection=90};
    a.Family.PartnerIds.Add(b.Id);b.Family.PartnerIds.Add(a.Id);
    return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void ReadyAutonomy(WorldSimulation sim)
{
    sim.State.AutonomousBreeding=true;
    foreach(var c in sim.State.Creatures){c.Family.BreedingInterest=60;c.Family.NextBreedingDecisionAt=0;}
}

Check("paused shop accounts for money and stock without advancing biology",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];sim.State.Settings.Pause=true;
    sim.State.UnlimitedResources=false;sim.State.CourtshipTools=0;sim.State.Money=23;
    double time=sim.State.WorldSeconds;
    Require(!sim.BuyCourtshipTool(),"insufficient money accepted");
    sim.State.Money=24;Require(sim.BuyCourtshipTool(),"paused purchase refused");
    Require(sim.State.Money==0&&sim.State.CourtshipTools==1,"wrong debit or stock");
    Require(sim.BreedingInductionReason(c.Id)==null,"read-only eligibility cannot be inspected in paused panel");
    Require(!sim.InduceBreedingInterest(c.Id).Success,"paused action accepted");
    Tick(sim,10);Require(sim.State.WorldSeconds==time&&sim.State.CourtshipTools==1&&c.Family.BreedingInterest==0,"purchase or pause advanced state");
    sim.State.UnlimitedResources=true;sim.State.CourtshipTools=99;
    Require(!sim.BuyCourtshipTool()&&sim.State.CourtshipTools==99,"inventory cap bypassed");
});

Check("induction consumes one supply but never creates trust consent or pregnancy",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];var other=sim.State.Creatures[1];
    c.Family.PartnerIds.Clear();other.Family.PartnerIds.Clear();c.Family.Peers.Clear();other.Family.Peers.Clear();
    double trust=c.Trust;int stock=sim.State.CourtshipTools;
    Require(sim.InduceBreedingInterest(c.Id).Success,"eligible induction refused");
    Require(sim.State.CourtshipTools==stock-1&&c.Family.BreedingInterest==20,"effect or finite consumption wrong");
    Require(c.Trust==trust&&c.Family.PartnerIds.Count==0&&c.Family.Pregnancy.Phase==PregnancyPhase.None,"induction fabricated consent or pregnancy");
    sim.State.AutonomousBreeding=true;Tick(sim,10);
    Require(c.Family.BreedingInterest>20&&c.Family.Pregnancy.Phase==PregnancyPhase.None,"interest did not grow or bypassed missing bond");
    Require(!sim.InduceBreedingInterest(c.Id).Success,"cooldown bypassed");
    var copy=Reload(sim);var saved=copy.State.Creatures[0];
    Require(saved.Family.InducedInterestRemaining==c.Family.InducedInterestRemaining&&saved.Family.NextInductionAt==c.Family.NextInductionAt&&
        copy.State.CourtshipTools==sim.State.CourtshipTools&&!copy.InduceBreedingInterest(saved.Id).Success,"save reset duration cooldown or stock");
});

Check("young ill pregnant and physically submerged bodies refuse without consumption",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];int stock=sim.State.CourtshipTools;
    c.IsYoung=true;Require(!sim.InduceBreedingInterest(c.Id).Success,"young accepted");c.IsYoung=false;
    c.Pain=30;Require(!sim.InduceBreedingInterest(c.Id).Success,"pain ignored");c.Pain=0;
    c.Family.Pregnancy.Phase=PregnancyPhase.Recovering;
    Require(!sim.InduceBreedingInterest(c.Id).Success,"recovery ignored");c.Family.Pregnancy.Phase=PregnancyPhase.None;
    c.X=7.1;c.Z=0;Require(!c.Interaction.Water.InWater,"fixture needs stale cache");
    Require(!sim.InduceBreedingInterest(c.Id).Success&&sim.State.CourtshipTools==stock,"physical water or refusal consumption wrong");
    sim.RequestPregnancy(c.Id,sim.State.Creatures[1].Id,BirthMethod.Stem);
    Require(c.Family.Pregnancy.Phase==PregnancyPhase.None,"direct breeding request bypassed geometric water before the next tick");
});

Check("deep safe reciprocal partners develop natural interest",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];Tick(sim,10);
    Require(c.Family.BreedingInterest>.5&&c.Family.InducedInterestRemaining==0,"safe partnership did not develop natural desire");
    var alone=Fixture();var a=alone.State.Creatures[0];var b=alone.State.Creatures[1];
    a.Family.Peers[b.Id].Affection=40;Tick(alone,10);
    Require(a.Family.BreedingInterest==0&&b.Family.BreedingInterest==0,"shallow one-sided bond counted as deep mutual attachment");
});

Check("autonomous request requires readiness and both deep bonds",()=>
{
    var sim=Fixture();ReadyAutonomy(sim);Tick(sim,.25);
    Require(sim.State.Creatures[0].Family.Pregnancy.Phase==PregnancyPhase.Preparing,"safe willing adults never requested pregnancy");
    Require(sim.State.Creatures.Count==2&&!sim.State.Creatures[0].Family.Pregnancy.Forced,"autonomy skipped ordinary approach or forced pregnancy");
    var scared=Fixture();ReadyAutonomy(scared);scared.State.Creatures[0].Interaction.Emotion.Alarm=20;Tick(scared,.25);
    Require(scared.State.Creatures[0].Family.Pregnancy.Phase==PregnancyPhase.None,"recent threat allowed autonomous pregnancy");
    var shallow=Fixture();ReadyAutonomy(shallow);shallow.State.Creatures[1].Family.Peers[shallow.State.Creatures[0].Id].Affection=40;Tick(shallow,.25);
    Require(shallow.State.Creatures[0].Family.Pregnancy.Phase==PregnancyPhase.None,"one-sided deep bond bypassed");
});

Check("visible water distress prevents autonomous pregnancy",()=>
{
    var sim=Fixture();ReadyAutonomy(sim);var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];
    a.X=a.TargetX=3.5;b.X=b.TargetX=4.8;
    var victim=new Creature{Name="落水者",X=7.1,Z=0,TargetX=7.1,Body=new(){SpeciesId="reimu"},
        Interaction=new(){Enabled=true,FootIntegrity=0,NextAutonomyAt=10000},Personality=new(){Initialized=true,Kindness=.5}};
    sim.State.Creatures.Add(victim);Tick(sim,.25);
    Require(a.Family.Pregnancy.Phase==PregnancyPhase.None,"nearby visible water crisis allowed autonomous breeding");
});

foreach(var method in new[]{BirthMethod.Stem,BirthMethod.Live})
Check(method+" waits in water and delivers only after rescue to dry space",()=>
{
    var sim=Fixture();var mother=sim.State.Creatures[0];var father=sim.State.Creatures[1];
    var p=mother.Family.Pregnancy;p.Phase=PregnancyPhase.Gestating;p.Method=method;p.FatherId=father.Id;
    p.EventId="water-delivery-"+method;p.PlannedChildren=method==BirthMethod.Stem?2:1;
    p.Elapsed=FamilyRules.GestationSeconds(method);p.NextDecisionAt=10000;
    mother.Interaction.FootIntegrity=0;mother.X=mother.TargetX=7.1;
    Tick(sim,3);
    Require(p.Phase==PregnancyPhase.Gestating&&sim.State.Creatures.Count==2&&p.Burden>0,"water delivery occurred or risk was suspended");
    var copy=Reload(sim);var saved=copy.State.Creatures[0];
    Require(saved.Family.Pregnancy.Elapsed==p.Elapsed&&saved.Interaction.Water.InWater,"save lost overdue submerged pregnancy");
    Require(copy.RescueFromWater(saved.Id).Success,"valid maternal rescue failed");
    copy.MoveCreature(saved.Id,0,0);Tick(copy,1);
    Require(saved.Family.Pregnancy.Phase==PregnancyPhase.Recovering&&copy.State.CompletedBirths.Contains(p.EventId!),"dry-space delivery did not resume");
    Require(copy.State.Creatures.Count==2+p.PlannedChildren,"incorrect child count after rescue");
    int count=copy.State.Creatures.Count;var again=Reload(copy);Tick(again,1);
    Require(again.State.Creatures.Count==count,"reload duplicated overdue birth");
});

Check("invalid new persistent values are rejected individually",()=>
{
    var sim=Fixture();var f=sim.State.Creatures[0].Family;
    bool Rejected(){try{SaveStore.Validate(sim.State);return false;}catch{return true;}}
    f.BreedingInterest=101;Require(Rejected(),"unbounded interest accepted");f.BreedingInterest=0;
    f.InducedInterestRemaining=double.NaN;Require(Rejected(),"NaN duration accepted");f.InducedInterestRemaining=0;
    f.NextInductionAt=-1;Require(Rejected(),"negative cooldown accepted");f.NextInductionAt=0;
    sim.State.CourtshipTools=100;Require(Rejected(),"unbounded inventory accepted");sim.State.CourtshipTools=1;
    SaveStore.Validate(sim.State);
});

Environment.ExitCode=failures==0?0:1;

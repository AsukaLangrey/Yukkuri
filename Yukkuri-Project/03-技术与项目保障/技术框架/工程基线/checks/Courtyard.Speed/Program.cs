using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;

// Pure simulation and linked actor helpers only. No player profile is discovered.
// Require a new, absolute output directory before any SaveStore or file creation.
if(args.Length!=1||!Path.IsPathFullyQualified(args[0])||Directory.Exists(args[0]))
    throw new ArgumentException("Supply one new absolute directory for synthetic test saves and results.");
string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
int failures=0,passed=0;var results=new List<object>();var suite=Stopwatch.StartNew();
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Near(double actual,double expected,string why,double epsilon=1e-8)=>Require(Math.Abs(actual-expected)<=epsilon,$"{why}: {actual:R} != {expected:R}");
void Reject(Action body,string why)
{
    bool rejected=false;try{body();}catch(ArgumentOutOfRangeException){rejected=true;}catch(InvalidDataException){rejected=true;}
    Require(rejected,why);
}
void Check(string name,Action body)
{
    var timer=Stopwatch.StartNew();
    try{body();passed++;results.Add(new{name,status="passed",milliseconds=timer.Elapsed.TotalMilliseconds});Console.WriteLine("PASS "+name);}
    catch(Exception e){failures++;results.Add(new{name,status="failed",milliseconds=timer.Elapsed.TotalMilliseconds,error=e.Message});Console.WriteLine("FAIL "+name+": "+e);}
}
string Json(WorldState state)=>JsonSerializer.Serialize(state,SaveStore.JsonOptions);
WorldSimulation Clone(WorldSimulation sim)=>new(JsonSerializer.Deserialize<WorldState>(Json(sim.State),SaveStore.JsonOptions)!);
string Observation(WorldSimulation sim)
{
    // Settings are an input, not an outcome. All other serialized state (including RNG,
    // world/remainder, needs, positions, inventories and portable state) must match exactly.
    double speed=sim.State.Settings.Speed;sim.State.Settings.Speed=1;
    string state=Json(sim.State);sim.State.Settings.Speed=speed;
    return state+JsonSerializer.Serialize(sim.State.Creatures.Select(c=>new{
        c.Interaction.ReactionPose,c.Interaction.ReactionPoseRemaining,c.Interaction.ReactionSituation,
        c.Interaction.EatingRemaining,c.Interaction.NaturalIntakePending,c.Interaction.HitRemaining,
        c.Interaction.BallContactCooldown}));
}
void Step(WorldSimulation sim,double seconds)
{
    // This is the actual scene's legacy 1x float boundary, not a scaled large delta.
    float dt=(float)seconds;sim.AdvanceInteractionPresentation(dt);sim.Advance(dt);
}
void Frame(WorldSimulation sim,double delta,bool modal=false)
{
    var plan=CourtyardFramePlan.Create(delta,sim.State.Settings.Speed,sim.State.Settings.Pause||modal,sim.State.Mode==PlayMode.CloseUp);
    for(int step=0;step<plan.StepCount;step++)Step(sim,plan.StepSeconds);
}
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(42);var c=sim.Selected!;
    sim.State.AutonomousBreeding=false;sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAll(o=>o!=c);
    c.X=c.Z=c.TargetX=c.TargetZ=0;c.Fullness=55;c.Energy=90;c.Trust=80;c.Interaction.Hydration=90;
    c.Family.NextCareAt=c.Family.NextSocialAt=c.Interaction.NextAutonomyAt=10000;
    sim.State.RoomObjects.Add(new(){Id="speed-meal",Kind=RoomObjectKind.Meal,RegionId=c.RegionId,X=.5,Z=0,Portions=1});
    sim.State.RoomObjects.Add(new(){Id="speed-portable",Kind=RoomObjectKind.Cushion,RegionId=c.RegionId,X=-4,Z=-4});
    sim.State.RoomObjects.Add(new(){Id="speed-ball",Kind=RoomObjectKind.Ball,RegionId=c.RegionId,X=4,Z=-4,VelocityX=.8,VelocityZ=.2});
    sim.Advance(.25);
    Require(c.Interaction.NaturalIntakePending&&sim.State.RoomObjects.Single(o=>o.Id=="speed-meal").Portions==0,"fixture failed actual natural reservation");
    Require(sim.BeginPortableCarry("speed-portable")&&sim.MovePortableCarry("speed-portable",-4,-4,1.2),"fixture portable pickup failed");
    sim.ReleasePortableCarry("speed-portable",new Vector2(2,0));
    Require(sim.State.PortableMotions.Count==1,"fixture release missing");
    return sim;
}

Check("plan bounds, integer exact counts and old real-frame cap",()=>
{
    foreach(double speed in new[]{1d,2,3,5,10})
    foreach(double delta in new[]{double.Epsilon,.001,1d/60,.1,.10000000149,1,1e100,double.MaxValue})
    {
        var p=CourtyardFramePlan.Create(delta,speed,false,false);
        Require(p.StepCount==(int)speed&&p.StepCount<=10&&double.IsFinite(p.StepSeconds)&&p.StepSeconds>0&&p.StepSeconds<=.1,"unsafe plan");
        Require(p.StepSeconds==Math.Min(.1,delta),"integer step changed accepted dt");
    }
});
Check("continuous historic speed range keeps proportional bounded work",()=>
{
    foreach(double speed in new[]{.25,.5,.999,1,1.5,1.999,2.001,2.75,3})
    foreach(double delta in new[]{.001,1d/60,.1,10})
    {
        var p=CourtyardFramePlan.Create(delta,speed,false,false);
        Require(p.StepCount==(int)Math.Ceiling(speed)&&p.StepSeconds<=.1,"historical fractional split failed");
        Near(p.StepCount*p.StepSeconds,Math.Min(.1,delta)*speed,"historical total changed");
    }
});
Check("invalid inputs rejected and zero, explicit pause and modal produce no debt",()=>
{
    foreach(double invalid in new[]{0d,.249,3.001,4,6,9,10.001,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
    {
        Require(!CourtyardFramePlan.IsSupportedSpeed(invalid,true),"unsupported courtyard speed accepted");
        Reject(()=>CourtyardFramePlan.Create(.05,invalid,false,false),"invalid speed was not rejected");
    }
    foreach(double invalid in new[]{-.001,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        Reject(()=>CourtyardFramePlan.Create(invalid,1,false,false),"invalid frame time accepted");
    foreach(double speed in new[]{.25,1,1.5,2,3,5,10})
    {
        Require(CourtyardFramePlan.Create(0,speed,false,false).StepCount==0,"zero frame advances");
        Require(CourtyardFramePlan.Create(300,speed,true,false).StepCount==0,"pause advances");
        var close=CourtyardFramePlan.Create(300,speed,false,true);
        Require(close.StepCount==1&&close.StepSeconds==.1,"closeup is not unscaled");
        var next=CourtyardFramePlan.Create(.02,speed,false,false);
        Near(next.StepCount*next.StepSeconds,.02*speed,"prior pause/closeup created debt");
    }
});

foreach(double speed in new[]{1d,2,3,5,10})
foreach(double delta in new[]{.05,.1})
Check($"actual world, mouth, resources, ball and portable equal repeated old 1x frames: {speed}x dt={delta}",()=>
{
    var seed=Fixture();var actual=Clone(seed);var reference=Clone(seed);actual.State.Settings.Speed=speed;
    Require(actual.Selected!.Interaction.NaturalIntakePending,"fixture reservation lost on clone");
    int exposureBefore=actual.Selected.Interaction.Digestion.Exposures;
    double fullBefore=actual.Selected.Fullness;
    for(int frame=0;frame<200;frame++)
    {
        Frame(actual,delta);
        for(int n=0;n<(int)speed;n++)Step(reference,Math.Min(.1,delta));
        if(frame is 0 or 9 or 199)Require(Observation(actual)==Observation(reference),"reference mismatch at real frame "+frame);
    }
    Require(!actual.State.RoomObjects.Any(o=>o.Id=="speed-meal")&&actual.Selected.Interaction.Digestion.Exposures==exposureBefore+1,"mouth did not complete and dispose exactly one reserved resource");
    Require(!actual.Selected.Interaction.NaturalIntakePending&&actual.Selected.Interaction.EatingRemaining==0&&actual.Selected.Fullness>fullBefore,"intake failed to finish");
    Require(actual.State.PortableMotions.Count==0&&actual.State.RoomObjects.Single(o=>o.Id=="speed-portable").X>-4,"released portable never moved and landed");
    Require(actual.State.RoomObjects.Single(o=>o.Id=="speed-ball").X>4,"ball never moved");
    Near(actual.State.WorldSeconds+actual.State.TickRemainder,.25+200*(int)speed*(double)(float)delta,"clock total lost a substep",1e-6);
});
foreach(double speed in new[]{.25,.5,1.5,2.75})
Check($"historical fractional simulation equals its defined substep reference: {speed}x",()=>
{
    var seed=Fixture();var actual=Clone(seed);var reference=Clone(seed);actual.State.Settings.Speed=speed;
    int steps=(int)Math.Ceiling(speed);double substep=.05*(speed/steps);
    for(int frame=0;frame<80;frame++){Frame(actual,.05);for(int n=0;n<steps;n++)Step(reference,substep);}
    Require(Observation(actual)==Observation(reference),"fractional reference mismatch");
});
Check("pause and modal freeze both clocks, mouth and portable; resume does not catch up",()=>
{
    foreach(bool modal in new[]{false,true})
    {
        var seed=Fixture();var actual=Clone(seed);var reference=Clone(seed);actual.State.Settings.Speed=10;actual.State.Settings.Pause=!modal;
        string before=Observation(actual);for(int n=0;n<20;n++)Frame(actual,30,modal);
        Require(Observation(actual)==before,"paused state changed");
        actual.State.Settings.Pause=false;
        Frame(actual,.02);for(int n=0;n<10;n++)Step(reference,.02);
        Require(Observation(actual)==Observation(reference),"resumed frame caught up");
    }
});
Check("closeup presentation remains 1x, world frozen, mode and speed changes have no debt",()=>
{
    var seed=Fixture();var actual=Clone(seed);var reference=Clone(seed);
    actual.State.Mode=reference.State.Mode=PlayMode.CloseUp;actual.State.Settings.Speed=10;
    double world=actual.State.WorldSeconds,remainder=actual.State.TickRemainder;
    for(int n=0;n<20;n++){Frame(actual,.05);Step(reference,.05);}
    Require(Observation(actual)==Observation(reference),"closeup presentation scaled");
    Require(actual.State.WorldSeconds==world&&actual.State.TickRemainder==remainder,"closeup world advanced");
    actual.State.Mode=reference.State.Mode=PlayMode.Management;
    foreach(double speed in new[]{1d,10,2,5,3,1})
    {
        actual.State.Settings.Speed=speed;Frame(actual,.05);
        for(int n=0;n<(int)speed;n++)Step(reference,.05);
        Require(Observation(actual)==Observation(reference),"speed transition multiplied old work");
    }
});
Check("stalled real frame admits .1 only, no deferred catchup",()=>
{
    var actual=Fixture();var reference=Clone(actual);actual.State.Settings.Speed=10;
    Frame(actual,60);for(int n=0;n<10;n++)Step(reference,.1);
    Frame(actual,.01);for(int n=0;n<10;n++)Step(reference,.01);
    Require(Observation(actual)==Observation(reference),"long frame exceeded old cap or accumulated debt");
});

foreach(double speed in new[]{1d,2,3,5,10})
Check($"linked ActorPoseClock and ActorHopMotion consume every step without .1 truncation: {speed}x",()=>
{
    var clock=new ActorPoseClock();var referenceClock=new ActorPoseClock();
    clock.Advance("idle",0,0,false);referenceClock.Advance("idle",0,0,false);
    var hop=new ActorHopMotion();var referenceHop=new ActorHopMotion();hop.Reset(Vector2.Zero);referenceHop.Reset(Vector2.Zero);
    var target=new Vector2(0,1);
    for(int frame=0;frame<4;frame++)
    {
        var p=CourtyardFramePlan.Create(.05,speed,false,false);
        for(int n=0;n<p.StepCount;n++){clock.Advance("idle",0,(float)p.StepSeconds,false);hop.Advance(target,(float)p.StepSeconds,true,false);}
        for(int n=0;n<(int)speed;n++){referenceClock.Advance("idle",0,.05f,false);referenceHop.Advance(target,.05f,true,false);}
        Require(clock.Age==referenceClock.Age&&hop.Position==referenceHop.Position&&hop.Frame==referenceHop.Frame&&hop.Active==referenceHop.Active,"actor repeated-step mismatch");
    }
    Near(clock.Age,.2*speed,"actor clock lost scaled time",1e-5);
    if(speed>=3)Require(hop.Position==target&&!hop.Active,"hop failed to reach destination through substeps");
    float age=clock.Age;var position=hop.Position;
    clock.Advance("idle",0,.1f,true);hop.Advance(target,.1f,true,true);
    Require(clock.Age==age&&hop.Position==position,"pause changed actor state");
    if(speed>=3){var lumped=new ActorPoseClock();lumped.Advance("idle",0,0,false);lumped.Advance("idle",0,(float)(.2*speed),false);Require(lumped.Age<clock.Age,"negative control did not expose large-dt clamp");}
});

foreach(string kind in new[]{"courtyard","legacy-m01"})
foreach(double speed in new[]{.25,.5,1,1.5,2,3,5,10})
Check($"SaveStore disk contract {kind} speed={speed}",()=>
{
    var sim=kind=="courtyard"?WorldSimulation.CreateInteractionPlaytest():WorldSimulation.CreateMilestone();
    var store=new SaveStore(Path.Combine(output,"saves",kind+"-"+speed.ToString(CultureInfo.InvariantCulture).Replace('.','_')));
    sim.State.Settings.Speed=speed;
    if(kind=="legacy-m01"&&speed>3){Reject(()=>store.Save(sim.State,1),"legacy save accepted new speed");Require(!File.Exists(store.SlotPath(1)),"rejected save wrote a slot");return;}
    sim.State.WorldSeconds=123.25;sim.State.TickRemainder=.125;sim.State.Settings.Pause=true;
    store.Save(sim.State,1);var loaded=store.Load(1);
    Require(!loaded.RecoveredBackup&&loaded.State.Settings.Speed==speed&&loaded.State.Settings.Pause&&loaded.State.WorldSeconds==123.25&&loaded.State.TickRemainder==.125,"disk roundtrip changed speed/pause/time");
    Require(loaded.State.SchemaVersion==sim.State.SchemaVersion,"schema changed");
});
foreach(string kind in new[]{"courtyard","legacy-m01"})
Check($"SaveStore invalid speeds rejected without modifying existing slot: {kind}",()=>
{
    var sim=kind=="courtyard"?WorldSimulation.CreateInteractionPlaytest():WorldSimulation.CreateMilestone();
    var store=new SaveStore(Path.Combine(output,"saves",kind+"-invalid"));store.Save(sim.State,1);
    byte[] baseline=File.ReadAllBytes(store.SlotPath(1));
    foreach(double speed in new[]{0d,.249,3.001,4,6,9,10.001,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
    {
        sim.State.Settings.Speed=speed;Reject(()=>store.Save(sim.State,1),"invalid save accepted speed "+speed);
        Require(baseline.SequenceEqual(File.ReadAllBytes(store.SlotPath(1))),"invalid save overwrote existing slot");
        Require(!File.Exists(store.SlotPath(1)+".writing"),"invalid save left temporary payload");
    }
});
Check("new factories default to 1x without schema change",()=>
{
    Require(WorldSimulation.CreateInteractionPlaytest().State.Settings.Speed==1&&WorldSimulation.CreateCourtyardEconomy().State.Settings.Speed==1&&WorldSimulation.CreateMilestone().State.Settings.Speed==1,"new world speed default changed");
});

Check("closeup keeps existing non-selected creature presentation semantics",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();sim.State.Settings.Speed=10;
    Require(sim.EnterCloseUp(),"could not enter closeup");
    foreach(var c in sim.State.Creatures)c.Interaction.HitRemaining=1;
    double clock=sim.State.WorldSeconds,remainder=sim.State.TickRemainder;
    for(int n=0;n<10;n++)Frame(sim,.05);
    foreach(var c in sim.State.Creatures)Near(c.Interaction.HitRemaining,1-10*(double).05f,"selected or nonselected presentation did not run exactly 1x");
    Require(sim.State.WorldSeconds==clock&&sim.State.TickRemainder==remainder,"closeup tick progressed");
});
Check("linked InteractionBallMotion preserves L-shaped substep path rather than endpoint shortcut",()=>
{
    var actual=new InteractionBallMotion();var reference=new InteractionBallMotion();var shortcut=new InteractionBallMotion();
    actual.Present(0,0,false);reference.Present(0,0,false);shortcut.Present(0,0,false);
    var p=CourtyardFramePlan.Create(.05,2,false,false);
    Require(p.StepCount==2,"2x not two observations");
    var path=new[]{new Vector2(.35f,0),new Vector2(.35f,.35f)};
    for(int n=0;n<p.StepCount;n++)actual.Present(path[n].X,path[n].Y,false);
    foreach(var point in path)reference.Present(point.X,point.Y,false);
    shortcut.Present(path[^1].X,path[^1].Y,false);
    Require(actual.Rotation==reference.Rotation,"same ball path did not repeat exactly");
    Require(Math.Abs(Quaternion.Dot(actual.Rotation,shortcut.Rotation))<.999f,"negative control: endpoint shortcut unexpectedly preserved L-path rotation");
    var before=actual.Rotation;actual.Present(.7,.7,true);
    Require(actual.Rotation==before,"explicit pause rotated ball");
});
foreach(double speed in new[]{1d,2,3,5,10})
Check($"live autonomy consumes RNG and moves identically to repeated old frames: {speed}x",()=>
{
    var seed=WorldSimulation.CreateInteractionPlaytest(43);seed.State.AutonomousBreeding=false;
    var actual=Clone(seed);var reference=Clone(seed);actual.State.Settings.Speed=speed;
    var positions=actual.State.Creatures.Select(c=>(c.X,c.Z)).ToArray();ulong random=actual.State.RandomState;
    int frames=400/(int)speed;
    for(int frame=0;frame<frames;frame++)
    {
        Frame(actual,.05);for(int n=0;n<(int)speed;n++)Step(reference,.05);
    }
    Require(Observation(actual)==Observation(reference),"live autonomy reference mismatch");
    Require(actual.State.RandomState!=random,"fixture never exercised RNG");
    Require(actual.State.Creatures.Where((c,n)=>c.X!=positions[n].X||c.Z!=positions[n].Z).Any(),"fixture never exercised movement");
});
Check("near-term synthetic save gives real birth inside tenth-speed substeps",()=>
{
    var seed=WorldSimulation.CreateInteractionPlaytest();seed.State.AutonomousBreeding=false;seed.State.RoomObjects.Clear();
    seed.State.Creatures.RemoveAt(2);
    for(int n=0;n<seed.State.Creatures.Count;n++)
    {
        var c=seed.State.Creatures[n];c.IsYoung=false;c.Fullness=c.Interaction.Hydration=c.Health=c.Energy=100;
        c.Pain=c.Fear=0;c.X=c.TargetX=n*3;c.Z=c.TargetZ=0;
        c.Interaction.NextAutonomyAt=c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    }
    var mother=seed.State.Creatures[0];var father=seed.State.Creatures[1];
    mother.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Live,FatherId=father.Id,
        EventId="speed-birth",PlannedChildren=1,Elapsed=FamilyRules.GestationSeconds(BirthMethod.Live)-.1};
    SaveStore.Validate(seed.State);
    var actual=Clone(seed);var reference=Clone(seed);actual.State.Settings.Speed=10;
    var plan=CourtyardFramePlan.Create(.05,10,false,false);int birthStep=-1;
    for(int n=0;n<plan.StepCount;n++)
    {
        Step(actual,plan.StepSeconds);Step(reference,.05);
        if(actual.State.Creatures.Count==3&&birthStep<0)birthStep=n;
        Require(Observation(actual)==Observation(reference),"birth reference mismatch at substep "+n);
    }
    Require(birthStep==4&&actual.State.CompletedBirths.Contains("speed-birth")&&actual.State.Creatures.Any(c=>c.Id=="speed-birth-0"),"real birth was not settled on fifth substep");
    SaveStore.Validate(actual.State);
});
suite.Stop();
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed,failures,milliseconds=suite.Elapsed.TotalMilliseconds,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"RESULT passed={passed} failures={failures} elapsed_ms={suite.Elapsed.TotalMilliseconds:F1}");
return failures==0?0:1;
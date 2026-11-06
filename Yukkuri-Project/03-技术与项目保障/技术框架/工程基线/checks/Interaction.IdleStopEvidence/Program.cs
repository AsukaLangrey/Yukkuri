using System.Text.Json;
using Yukkuri.Simulation;

// Synthetic yards only. Never load personal saves. The same harness runs before and after the fix.
var results = new List<object>();
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action action){try{action();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.Message});Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Tick(WorldSimulation s,double seconds){for(int n=0;n<(int)Math.Ceiling(seconds*20);n++){s.AdvanceInteractionPresentation(.05);s.Advance(.05);}}
WorldSimulation Fresh(ulong seed)
{
    var s=WorldSimulation.CreateInteractionPlaytest(seed);var c=s.Selected!;
    s.State.Creatures.RemoveAll(o=>o.Id!=c.Id);s.State.RoomObjects.Clear();s.State.AutonomousBreeding=false;
    s.State.PlayerX=0;s.State.PlayerZ=6;
    c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
    c.X=c.TargetX=0;c.Z=c.TargetZ=0;c.Fullness=c.Interaction.Hydration=c.Energy=c.Health=100;
    c.Trust=90;c.Fear=c.Pain=0;c.Patience=.8;c.Interaction.ToyInterest=0;
    c.DecisionIn=c.Interaction.NextAutonomyAt=c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    Tick(s,2);Require(s.State.WorldSeconds-c.Interaction.LastWalkAt>1.5&&c.X==0&&c.Z==0,"fixture did not remain actually stationary");
    return s;
}
int Completed(Creature c)=>c.Interaction.Development.Skills.GetValueOrDefault(LifeSkill.Stop)?.Completed??0;
void NoStopCredit(Creature c)
{
    Require(!c.Interaction.Learning.ContainsKey(LearnedCue.Stop),"idle Stop created learning entry");
    Require(!c.Interaction.Development.Skills.ContainsKey(LifeSkill.Stop),"idle Stop created ability evidence");
    Require(Completed(c)==0&&c.Interaction.PendingPraise==null,"idle Stop earned completion or praise");
    Require(CourtyardEconomy.Ability(c)==0,"idle Stop earned sale ability bonus");
}
var observations=new List<object>();
Check("stationary ordinary Stop has no learning or economy credit across 32 seeds",()=>
{
    for(ulong seed=1;seed<=32;seed++)
    {
        var s=Fresh(seed);var c=s.Selected!;var before=CourtyardEconomy.Quote(c);var rng=s.State.RandomState;
        var result=s.ExecuteInteraction(c.Id,new(InteractionAction.Stop));bool consumedRandom=s.State.RandomState!=rng;
        Tick(s,3.2);var after=CourtyardEconomy.Quote(c);var learning=c.Interaction.Learning.GetValueOrDefault(LearnedCue.Stop);
        observations.Add(new{seed,result.Success,consumedRandom,completed=Completed(c),applications=learning?.Applications??0,familiarity=learning?.Familiarity??0,praise=c.Interaction.PendingPraise,before,after,lastWalk=c.Interaction.LastWalkAt,time=s.State.WorldSeconds});
        // Run all seeds even on the buggy baseline, then evaluate the collected result.
    }
    Console.WriteLine("OBSERVATIONS "+JsonSerializer.Serialize(observations));
    foreach(ulong seed in Enumerable.Range(1,32).Select(n=>(ulong)n))
    {var s=Fresh(seed);var c=s.Selected!;ulong rng=s.State.RandomState;Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Success,"idle control rejected");Require(s.State.RandomState==rng,"idle control consumed learning RNG");Tick(s,3.2);NoStopCredit(c);}
});
Check("idle Stop holds position and repeat commands cannot earn credit",()=>
{
    var s=Fresh(42);var c=s.Selected!;
    for(int n=0;n<8;n++){c.TargetX=3;Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Success,"control rejected");Require(c.TargetX==c.X&&c.TargetZ==c.Z&&!c.Interaction.FollowPlayer,"control did not pin target");Tick(s,1);Require(c.X==0&&c.Z==0,"hold allowed walking");}
    Tick(s,3.2);NoStopCredit(c);
});
Check("idle Stop interrupts Follow and Wait without replacing their evidence",()=>
{
    foreach(var action in new[]{InteractionAction.Follow,InteractionAction.TeachWait})
    {
        var s=Fresh(42);var c=s.Selected!;c.Fullness=70;
        Require(s.ExecuteInteraction(c.Id,new(action)).Success,"prior cue not started");
        var skill=action==InteractionAction.Follow?LifeSkill.Follow:LifeSkill.Wait;
        Require(c.Interaction.ActiveCue!=null,"missing active prior cue");
        Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Success,"stop rejected");
        Require(c.Interaction.ActiveCue==null&&!c.Interaction.FollowPlayer&&c.Interaction.WaitRemaining==3,"stop left active cue or follow");
        Require(c.Interaction.Development.Skills[skill].Interrupted==1&&c.Interaction.Development.Skills[skill].Completed==0,"interruption evidence lost");
        Tick(s,3.2);NoStopCredit(c);
    }
});
Check("injured stationary creature still accepts Stop control",()=>
{
    var s=Fresh(42);var c=s.Selected!;c.Interaction.FootIntegrity=10;
    Require(!WorldSimulation.CanSelfMove(c),"injury fixture can walk");
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Success,"nonwalking control rejected");Tick(s,3.2);NoStopCredit(c);
});
Check("moving Stop and TeachStop still complete using actual movement and random understanding",()=>
{
    foreach(var action in new[]{InteractionAction.Stop,InteractionAction.TeachStop})
    {
        bool completed=false;
        for(ulong seed=1;seed<=32&&!completed;seed++)
        {
            var s=Fresh(seed);var c=s.Selected!;c.TargetX=3;Tick(s,.5);
            Require(c.X>.05&&s.State.WorldSeconds-c.Interaction.LastWalkAt<=1.5,"actual autonomous walking absent");
            Require(s.ExecuteInteraction(c.Id,new(action)).Success,"moving stop rejected");double x=c.X;Tick(s,3.2);
            completed=Completed(c)==1;
            if(completed){Require(c.X==x,"stop did not hold");Require(c.Interaction.Learning[LearnedCue.Stop].Successes==1,"missing actual success");Require(CourtyardEconomy.Ability(c)==2,"real completion lost ability");}
        }
        Require(completed,"no legitimate completion across seeds");
    }
});
Check("idle TeachStop and need or fatigue preflights stay intact",()=>
{
    var s=Fresh(42);var c=s.Selected!;Require(!s.ExecuteInteraction(c.Id,new(InteractionAction.TeachStop)).Success,"idle demonstration accepted");
    c.Energy=10;Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Refused,"needs gate bypassed");
    c.Energy=100;c.TrainingFatigue=80;Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Refused,"fatigue gate bypassed");
});
Check("idle Stop does not refresh praise from an earlier real completion",()=>
{
    var s=Fresh(1);var c=s.Selected!;c.TargetX=3;Tick(s,.5);
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Success,"moving stop rejected");Tick(s,3.2);
    Require(Completed(c)==1&&c.Interaction.PendingPraise!=null,"real prior completion missing");
    int applications=c.Interaction.Learning[LearnedCue.Stop].Applications;double familiarity=c.Interaction.Learning[LearnedCue.Stop].Familiarity;
    double praise=c.Interaction.PraiseRemaining;string? pending=c.Interaction.PendingPraise;
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Success,"idle control rejected");
    Require(c.Interaction.PendingPraise==pending&&c.Interaction.PraiseRemaining==praise,"idle Stop rewrote prior praise");
    Tick(s,3.2);
    Require(Completed(c)==1&&c.Interaction.Learning[LearnedCue.Stop].Applications==applications&&c.Interaction.Learning[LearnedCue.Stop].Familiarity==familiarity,"idle Stop inflated existing learning");
    Require(c.Interaction.PraiseRemaining<praise,"idle Stop refreshed prior praise timer");
    for(int n=0;n<5;n++){s.ExecuteInteraction(c.Id,new(InteractionAction.Stop));Tick(s,3);}
    Require(c.Interaction.PendingPraise==null&&Completed(c)==1,"expired praise was revived by idle Stop");
});
Check("stationary Stop save reload retains control without invented learning",()=>
{
    var s=Fresh(42);var c=s.Selected!;Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Success,"control rejected");
    SaveStore.Validate(s.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(s.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);s=new WorldSimulation(state);c=s.Selected!;
    Require(c.Interaction.ActiveCue==null&&!c.Interaction.FollowPlayer&&c.Interaction.WaitRemaining==3,"reload invented a lesson or lost hold");
    Tick(s,3.2);NoStopCredit(c);Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Stop)).Success,"post-load control rejected");Tick(s,3.2);NoStopCredit(c);
});
var report=JsonSerializer.Serialize(new{suite="Interaction.IdleStopEvidence",failures,results,observations},new JsonSerializerOptions{WriteIndented=true});
Console.WriteLine(report);
if(args.Length>0)File.WriteAllText(args[0],report);
Environment.ExitCode=failures==0?0:1;

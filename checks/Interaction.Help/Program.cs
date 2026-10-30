using System.Text.Json;
using Yukkuri.Simulation;

// Source only on the original workstation; authorised independent host only.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception error){failures++;Console.WriteLine("FAIL "+name+": "+error);}}
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();
    for(int n=0;n<3;n++)
    {
        var c=sim.State.Creatures[n];c.X=c.TargetX=n*1.8;c.Z=c.TargetZ=0;
        c.Fullness=c.Energy=c.Interaction.Hydration=90;c.Fear=c.Pain=0;
        c.Interaction.NextAutonomyAt=10000;c.Family.NextSocialAt=10000;
    }
    return sim;
}
void Hunger(Creature actor,Creature target)
{actor.Fullness=5;actor.Personality.Kindness=.05;actor.Personality.HungerTargetId=target.Id;}
string Snapshot(WorldSimulation sim)=>JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);

Check("actual crisis priority ignores an obsolete help id and resolves immediately",()=>
{
    var sim=Fixture();var critical=sim.State.Creatures[0];var water=sim.State.Creatures[1];
    sim.State.HelpTargetId=sim.State.Creatures[2].Id;
    critical.Condition=PhysicalCondition.Critical;critical.Health=0;
    water.X=7.1;
    Require(!water.Interaction.Water.InWater,"fixture requires a stale water cache");
    Require(sim.CurrentInteractionHelpTarget()==critical&&sim.InteractionHelpFor(critical)?.Kind==InteractionHelpKind.Critical,
        "stale id or water hid the critical patient");
    critical.Condition=PhysicalCondition.Active;critical.Health=100;
    Require(sim.CurrentInteractionHelpTarget()==water&&sim.InteractionHelpFor(water)?.Kind==InteractionHelpKind.Water,
        "real geometry was ignored before the next simulation tick");
    Require(sim.RescueFromWater(water.Id).Success,"actual rescue failed");
    Require(sim.CurrentInteractionHelpTarget()==null&&sim.InteractionHelpFor(water)==null,
        "resolved water crisis stayed in the request list");
});

Check("queries remain read-only while paused or in close-up and do not select or travel",()=>
{
    var sim=Fixture();var victim=sim.State.Creatures[1];victim.X=7.1;
    sim.State.Settings.Pause=true;sim.State.Mode=PlayMode.CloseUp;
    string before=Snapshot(sim);
    Require(sim.CurrentInteractionHelpTarget()==victim&&sim.InteractionHelpRequests().Count==1,
        "pause hid the existing crisis");
    Require(sim.InteractionHelpFor(new Creature{Condition=PhysicalCondition.Critical})==null,
        "foreign creature became a real courtyard request");
    Require(Snapshot(sim)==before,"help query mutated clock, selection, resources or pending actions");
    victim.RegionId="elsewhere";Require(sim.CurrentInteractionHelpTarget()==null,"query crossed the current courtyard boundary");
    victim.RegionId="home";victim.Family.ColdStorage.Stored=true;
    Require(sim.CurrentInteractionHelpTarget()==null,"cold storage appeared as an exposed crisis");
    victim.Family.ColdStorage.Stored=false;victim.Condition=PhysicalCondition.Dead;
    Require(sim.CurrentInteractionHelpTarget()==null,"death was presented as a rescuable crisis");
});

Check("threats share live capability and multiple attackers remain individually addressable",()=>
{
    var sim=Fixture();var first=sim.State.Creatures[0];var victim=sim.State.Creatures[1];var second=sim.State.Creatures[2];
    Hunger(first,victim);Hunger(second,victim);
    Require(sim.CurrentInteractionThreatTarget(first)==victim&&sim.CurrentInteractionThreatTarget(second)==victim,
        "only the nearest attacker remained addressable");
    victim.Condition=PhysicalCondition.Critical;victim.Health=0;
    Require(sim.InteractionHelpFor(victim) is {Kind:InteractionHelpKind.Critical,ThreateningCreature:not null},
        "critical priority lost ongoing attack information");
    first.Interaction.Carried=true;first.Interaction.Height=1;
    Require(sim.CurrentInteractionThreatTarget(first)==null&&sim.InteractionHelpFor(victim)?.ThreateningCreature==second,
        "lifting an attacker did not invalidate its danger immediately");
    second.Fullness=20;
    Require(sim.CurrentInteractionThreatTarget(second)==null&&sim.InteractionHelpFor(victim)?.ThreateningCreature==null,
        "ending real starvation left a stale active threat");
});

Check("paused interruption does nothing then actual current hunger interruption leaves its consequences",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var victim=sim.State.Creatures[1];Hunger(actor,victim);
    victim.Health=70;victim.Pain=20;victim.Interaction.Emotion.Alarm=20;
    sim.State.Settings.Pause=true;string before=Snapshot(sim);
    Require(sim.InterruptInteractionThreat(actor.Id)=="先恢复时间，再安排。"&&Snapshot(sim)==before,"pause allowed interruption mutation");
    sim.State.Settings.Pause=false;
    Require(sim.InterruptInteractionThreat(actor.Id).Contains("已制止")&&actor.Personality.HungerTargetId==null&&
        actor.Personality.NextHungerConflictAt>=30,"current hunger action did not stop");
    Require(actor.Fullness==5&&victim.Health==70&&victim.Pain==20&&victim.Interaction.Emotion.Alarm==20,
        "interruption fed, healed or erased the victim's memory");
});

Check("obsolete hunger id cannot mask a real child threat or consume the wrong interruption",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    actor.Fullness=15;actor.Personality.HungerTargetId=sim.State.Creatures[1].Id;
    actor.Family.ChildThreat=new(){TargetId=child.Id,Remaining=45,WarningRemaining=8,Cause="饥饿压力"};
    child.Family.MotherId=actor.Id;
    Require(sim.CurrentInteractionThreatTarget(actor)==child,"stale hunger field masked the real child danger");
    Require(sim.InterruptInteractionThreat(actor.Id).Contains("幼体")&&actor.Family.ChildThreat.TargetId==null,
        "interruption stopped the old hunger field instead of the actual danger");
    Require(actor.Family.ChildThreat.NextCheckAt>=120&&child.Health==100,"interruption removed the cooldown or injured the child");
});

Check("resolved child pressure is rechecked before another ordinary tick can inflict one final hit",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    actor.Fullness=50;child.X=1;child.TargetX=1;child.Family.MotherId=actor.Id;
    actor.Family.ChildThreat=new(){TargetId=child.Id,Remaining=45,WarningRemaining=0,Cause="旧饥饿压力"};
    Require(sim.CurrentInteractionThreatTarget(actor)==null,"resolved pressure remained an active threat");
    double health=child.Health;sim.Advance(.25);
    Require(child.Health==health&&actor.Family.ChildThreat.TargetId==null,"resolved pressure caused a last stale hit");
});

Check("selected attacker can stop eating remains without presenting the dead as needing rescue",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var remains=sim.State.Creatures[1];
    remains.Condition=PhysicalCondition.Dead;remains.Health=0;Hunger(actor,remains);
    Require(sim.CurrentInteractionThreatTarget(actor)==remains&&sim.InteractionHelpFor(remains)==null&&
        sim.InteractionHelpRequests().Count==0,"remains lost interruption access or became a rescue request");
    double nutrition=remains.Personality.RemainsNutrition;
    Require(sim.InterruptInteractionThreat(actor.Id).Contains("啃食残躯")&&actor.Personality.HungerTargetId==null,
        "selected attacker could not stop eating remains");
    Require(remains.Condition==PhysicalCondition.Dead&&remains.Personality.RemainsNutrition==nutrition&&actor.Fullness==5,
        "interruption revived, replenished or fed anyone");
    Hunger(actor,remains);remains.Personality.RemainsNutrition=0;
    Require(sim.CurrentInteractionThreatTarget(actor)==null,"exhausted remains still presented as an active meal");
});

Check("water rescue changes priority while retaining a still real family attacker",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var victim=sim.State.Creatures[1];
    actor.X=actor.TargetX=5;victim.X=victim.TargetX=7.1;
    actor.Family.Conflict=new(){TargetId=victim.Id,Remaining=60,WarningRemaining=8};
    Require(sim.InteractionHelpFor(victim) is {Kind:InteractionHelpKind.Water,ThreateningCreature:var attacker}&&attacker==actor,
        "water priority hid a capable family attacker");
    Require(sim.RescueFromWater(victim.Id).Success,"water rescue failed");
    Require(sim.InteractionHelpFor(victim) is {Kind:InteractionHelpKind.Threat,ThreateningCreature:var remaining}&&remaining==actor,
        "water rescue also erased an unrelated continuing threat");
    Require(sim.InterruptInteractionThreat(actor.Id).Contains("已阻止")&&sim.InteractionHelpFor(victim)==null,
        "actual family interruption did not clear the remaining request");
});

Console.WriteLine($"Interaction help: {failures} failure(s)");
return failures==0?0:1;

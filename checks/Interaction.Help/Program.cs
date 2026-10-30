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

Check("actual protection changes the rescued personality's relationship and creates one identified event",()=>
{
    double gratefulGain=0,entitledGain=0;
    foreach(bool entitled in new[]{false,true})
    {
        var sim=Fixture();var actor=sim.State.Creatures[0];var victim=sim.State.Creatures[1];
        victim.Personality.Initialized=true;victim.Personality.Kindness=entitled?.05:.8;victim.Personality.Pride=entitled?.9:.2;
        Hunger(actor,victim);double trust=victim.Trust,safety=victim.Interaction.HumanBond.Safety;
        sim.InterruptInteractionThreat(actor.Id);
        var protection=sim.State.Incidents.Single(e=>e.Kind==IncidentKind.Protection&&e.TargetId==victim.Id);
        Require(victim.PersonalMemories.Count(m=>m.IncidentId==protection.Id)==1&&victim.Trust>trust&&
            victim.Interaction.HumanBond.Safety>safety,"protection only changed dialogue, without a specific memory or relationship");
        if(entitled)entitledGain=victim.Trust-trust;else gratefulGain=victim.Trust-trust;
        string after=Snapshot(sim);sim.InterruptInteractionThreat(actor.Id);
        Require(Snapshot(sim)==after,"clicking an already stopped attacker manufactured another protection event");
    }
    Require(gratefulGain>entitledGain*4,"entitled and grateful personalities had indistinguishable consequences");
});
Check("only witnesses who can see both participants gain protection trust",()=>
{
    foreach(bool nearby in new[]{false,true})
    {
        var sim=Fixture();var actor=sim.State.Creatures[0];var victim=sim.State.Creatures[1];var witness=sim.State.Creatures[2];
        witness.Personality.Initialized=true;witness.Personality.Kindness=.8;
        if(!nearby){witness.X=-7;witness.Z=-7;}
        Hunger(actor,victim);double trust=witness.Trust;sim.InterruptInteractionThreat(actor.Id);
        Require(nearby?witness.Trust>trust:witness.Trust==trust,"protection gratitude ignored actual observation");
    }
});
Check("player harm followed by interrupting the resulting danger cannot buy gratitude",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var victim=sim.State.Creatures[1];
    Require(sim.ExecuteInteraction(actor.Id,new(InteractionAction.Punch,BodyPart.Side,0)).Success,"real player harm failed");
    Hunger(actor,victim);double trust=victim.Trust,safety=victim.Interaction.HumanBond.Safety;
    sim.InterruptInteractionThreat(actor.Id);
    Require(victim.Trust==trust&&victim.Interaction.HumanBond.Safety==safety&&
        victim.PersonalMemories.Any(m=>m.Text.Contains("人类伤害仍然记得")),"manufactured protection erased its cause or granted trust");
});
Check("saved protection cooldown prevents successive real interruptions farming a second reward",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var victim=sim.State.Creatures[1];Hunger(actor,victim);
    sim.InterruptInteractionThreat(actor.Id);SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(Snapshot(sim),SaveStore.JsonOptions)!;
    var restored=new WorldSimulation(state);actor=state.Creatures[0];victim=state.Creatures[1];
    double trust=victim.Trust,safety=victim.Interaction.HumanBond.Safety;Hunger(actor,victim);
    restored.InterruptInteractionThreat(actor.Id);
    Require(victim.Trust==trust&&victim.Interaction.HumanBond.Safety==safety&&
        state.Incidents.Count(e=>e.Kind==IncidentKind.Protection&&e.TargetId==victim.Id)==2,
        "a separate intervention should retain its fact without repeating the relationship reward");
    victim.Interaction.HumanBond.LastProtectionAt=double.NaN;
    bool rejected=false;try{SaveStore.Validate(state);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"non-finite saved protection cooldown was accepted");
});

Check("a visible warning creates continuous caution before injury, and obsolete intent cannot keep renewing it",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var victim=sim.State.Creatures[1];
    actor.Family.Conflict=new(){TargetId=victim.Id,Remaining=60,WarningRemaining=8};
    double health=victim.Health;sim.Advance(.25);
    Require(victim.Health==health&&victim.Interaction.Emotion.Alarm>0&&!EmotionalReadiness.Settled(victim),
        "a live warning was ignored until the first injury");
    double alarm=victim.Interaction.Emotion.Alarm,lastThreat=victim.Interaction.Emotion.LastThreatAt;
    sim.InterruptInteractionThreat(actor.Id);
    actor.Family.Conflict=new(){TargetId=victim.Id,Remaining=0};
    sim.Advance(.25);
    Require(victim.Interaction.Emotion.Alarm==alarm&&victim.Interaction.Emotion.LastThreatAt==lastThreat&&
        !EmotionalReadiness.Settled(victim),"stopping erased real caution, or stale intent kept renewing the threat");
});

Console.WriteLine($"Interaction help: {failures} failure(s)");
return failures==0?0:1;

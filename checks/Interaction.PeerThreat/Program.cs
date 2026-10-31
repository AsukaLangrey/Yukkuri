using System.Text.Json;
using Yukkuri.Simulation;

// Source only here. Execute on the separately authorised remote host.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
WorldSimulation Fixture(string kind="family")
{
    var sim=WorldSimulation.CreateInteractionPlaytest();sim.State.RoomObjects.Clear();
    var actor=sim.State.Creatures[0];var observer=sim.State.Creatures[1];var victim=sim.State.Creatures[2];
    foreach(var c in sim.State.Creatures)
    {
        c.Fullness=c.Energy=c.Interaction.Hydration=90;c.Fear=c.Pain=0;c.Aggression=.5;c.Courage=.1;
        c.Family.NextCareAt=c.Family.NextSocialAt=c.Interaction.NextAutonomyAt=10000;
        c.Personality.Initialized=true;c.Personality.Kindness=.5;c.Z=c.TargetZ=0;
    }
    actor.X=actor.TargetX=0;victim.X=victim.TargetX=1.5;observer.X=observer.TargetX=3.2;
    if(kind=="hunger"){actor.Fullness=5;actor.Personality.Kindness=.05;actor.Personality.HungerTargetId=victim.Id;}
    else if(kind=="child")
    {
        actor.Fullness=19;victim.Family.MotherId=actor.Id;
        actor.Family.ChildThreat=new(){TargetId=victim.Id,Remaining=45,WarningRemaining=0,Cause="饥饿与求生压力"};
    }
    else actor.Family.Conflict=new(){TargetId=victim.Id,Remaining=60,WarningRemaining=0};
    return sim;
}
Incident Attack(WorldSimulation sim)=>sim.State.Incidents.Single(e=>e.Kind==IncidentKind.Attack&&
    e.ActorId==sim.State.Creatures[0].Id&&e.TargetId==sim.State.Creatures[2].Id);

Check("all three peer strike paths apply one injury, one incident and one victim memory without blaming humans",()=>
{
    foreach(string kind in new[]{"family","child","hunger"})
    {
        var sim=Fixture(kind);var actor=sim.State.Creatures[0];var observer=sim.State.Creatures[1];var victim=sim.State.Creatures[2];
        double trust=victim.Trust,observerTrust=observer.Trust,humanHarm=victim.Interaction.HumanBond.LastHarmAt;
        double damage=kind=="family"?10.5:kind=="child"?12:6.5;
        double alarmBeforeStrike=victim.Interaction.Emotion.Alarm;
        if(kind=="hunger")
        {
            // Hunger now has a real interruptible contact preparation. Keep the
            // original one-injury/one-memory assertions at the first real strike.
            for(int n=0;n<40&&!sim.State.Incidents.Any(e=>e.Kind==IncidentKind.Attack&&e.ActorId==actor.Id&&e.TargetId==victim.Id);n++)
            {alarmBeforeStrike=victim.Interaction.Emotion.Alarm;sim.Advance(.25);}
        }
        else sim.Advance(.25);
        var incident=Attack(sim);
        Require(100-victim.Health>=damage-.1&&100-victim.Health<=damage+.1,"bodily damage was omitted or applied twice");
        // The real interruptible preparation may already have raised alarm.
        // Keep that history; this strike must add exactly one damage contribution
        // plus at most one ordinary 0.25-second warning sample (0.15).
        if(kind=="hunger")Require(alarmBeforeStrike>0,"real interruptible hunger preparation never warned the victim");
        Require(alarmBeforeStrike<=sim.State.WorldSeconds*.6+.000001,
            "pre-strike warning alarm exceeded its actual elapsed-time budget");
        Require(victim.Interaction.Emotion.Alarm>=alarmBeforeStrike+damage*2&&
            victim.Interaction.Emotion.Alarm<=alarmBeforeStrike+damage*2+.2,
            "actual harm was absent from continuity, or old hunger/warning consequences were double counted"+
            $" kind={kind}, alarm={victim.Interaction.Emotion.Alarm}, expectedDamageAlarm={damage*2}, world={sim.State.WorldSeconds}");
        Require(victim.PersonalMemories.Count(m=>m.IncidentId==incident.Id)==1&&incident.Severity==(kind=="family"?damage:8),
            "strike identity, original severity, or victim memory duplicated");
        Require(victim.Trust==trust&&observer.Trust==observerTrust&&victim.Interaction.HumanBond.LastHarmAt==humanHarm,
            "a peer's injury was attributed to the human");
        Require(observer.Fear>0&&observer.Interaction.Emotion.Alarm>0&&observer.Family.Peers[actor.Id].Fear>0,
            "a real identified witness did not remember the actual aggressor");
    }
});
Check("hearing a strike creates fear without identifying an unseen aggressor",()=>
{
    var sim=Fixture();var observer=sim.State.Creatures[1];observer.X=observer.TargetX=6.8;
    sim.Advance(.25);var incident=Attack(sim);
    var knowledge=observer.Knowledge.Single(k=>k.IncidentId==incident.Id);
    Require(knowledge.Source==KnowledgeSource.Heard&&!knowledge.KnowsActor&&observer.Fear>0&&
        observer.Interaction.Emotion.Alarm>0,"hearing failed to keep its limited source");
    Require((observer.Family.Peers.GetValueOrDefault(sim.State.Creatures[0].Id)?.Fear??0)==0,
        "a sound invented knowledge of who attacked");
});
Check("seeing only the victim does not identify someone outside the witness's sight",()=>
{
    var sim=Fixture();var observer=sim.State.Creatures[1];observer.X=observer.TargetX=5;
    sim.Advance(.25);var incident=Attack(sim);var knowledge=observer.Knowledge.Single(k=>k.IncidentId==incident.Id);
    Require(knowledge.Source==KnowledgeSource.Witness&&!knowledge.KnowsActor&&observer.Fear>0,
        "seeing the injury was incorrectly equated with seeing its aggressor");
    Require((observer.Family.Peers.GetValueOrDefault(sim.State.Creatures[0].Id)?.Fear??0)==0,
        "an obscured aggressor was blamed by identity");
});
Check("distant and other-region creatures acquire neither the strike nor its emotional consequences",()=>
{
    foreach(bool otherRegion in new[]{false,true})
    {
        var sim=Fixture();var observer=sim.State.Creatures[1];
        if(otherRegion){sim.State.Regions.Add(new(){Id="other",Name="另一区域"});observer.RegionId="other";}
        else {observer.X=observer.TargetX=-7;observer.Z=observer.TargetZ=-7;}
        sim.Advance(.25);var incident=Attack(sim);
        Require(!observer.Knowledge.Any(k=>k.IncidentId==incident.Id)&&observer.Fear==0&&observer.Interaction.Emotion.Alarm==0,
            "a peer attack affected an uninformed creature across distance or region");
    }
});
Check("stopping and reloading retain the injury aftermath without duplicating a settled strike",()=>
{
    var sim=Fixture("child");var actor=sim.State.Creatures[0];var victim=sim.State.Creatures[2];
    sim.Advance(.25);string eventId=Attack(sim).Id;double alarm=victim.Interaction.Emotion.Alarm;
    Require(sim.InterruptInteractionThreat(actor.Id).Contains("已制止"),"valid child protection failed");
    Require(victim.Interaction.Emotion.Alarm==alarm&&!EmotionalReadiness.Settled(victim),"protection erased the real injury aftermath");
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    var copy=new WorldSimulation(state);copy.Advance(.25);
    Require(Attack(copy).Id==eventId&&state.Creatures[2].PersonalMemories.Count(m=>m.IncidentId==eventId)==1&&
        state.Creatures[2].Interaction.Emotion.Alarm==alarm,"reload repeated the strike or removed its continuous consequence");
});

Check("a solid box can hide the aggressor while the injured creature remains in sight",()=>
{
    var sim=Fixture();var observer=sim.State.Creatures[1];observer.X=observer.TargetX=0;observer.Z=observer.TargetZ=4.2;
    sim.State.Creatures.Add(new(){Name="盒内幼体",IsYoung=true,X=-.7,Z=1.6,TargetX=-.7,TargetZ=1.6,
        Fullness=90,Energy=90,Body=new(){SpeciesId="reimu"},
        Interaction=new(){Enabled=true,Hydration=90,Restraint=RestraintKind.Box,NextAutonomyAt=10000},
        Family=new(){NextSocialAt=10000,NextCareAt=10000}});
    sim.Advance(.25);var incident=Attack(sim);var knowledge=observer.Knowledge.Single(k=>k.IncidentId==incident.Id);
    Require(knowledge.Source==KnowledgeSource.Witness&&!knowledge.KnowsActor&&observer.Fear>0&&
        (observer.Family.Peers.GetValueOrDefault(sim.State.Creatures[0].Id)?.Fear??0)==0,
        "a solid occluder failed to preserve unknown aggressor identity");
});
Check("a first fatal strike records its cause before death cleanup and affects only the actual witnesses",()=>
{
    var sim=Fixture();var actor=sim.State.Creatures[0];var observer=sim.State.Creatures[1];var victim=sim.State.Creatures[2];
    victim.Condition=PhysicalCondition.Critical;victim.Health=0;victim.CriticalRemaining=1;
    victim.Interaction.Injuries.CriticalEpisode=true;victim.Interaction.Injuries.CriticalCareBudget=1;
    double humanTrust=observer.Trust,lastHumanHarm=victim.Interaction.HumanBond.LastHarmAt;
    sim.Advance(.25);var attack=Attack(sim);
    var death=sim.State.Incidents.Single(e=>e.Kind==IncidentKind.Death&&e.TargetId==victim.Id);
    Require(victim.Condition==PhysicalCondition.Dead&&sim.State.Incidents.IndexOf(attack)<sim.State.Incidents.IndexOf(death),
        "fatal cleanup preceded the attack fact or failed to produce one death");
    Require(victim.PersonalMemories.Count(m=>m.IncidentId==attack.Id)==1&&victim.Knowledge.Any(k=>k.IncidentId==attack.Id&&k.KnowsActor),
        "the fatal victim lost the strike's identity before death cleanup");
    Require(observer.PersonalMemories.Any(m=>m.IncidentId==attack.Id)&&observer.Family.KnownLosses[victim.Id].Source==KnowledgeSource.Witness&&
        observer.Trust==humanTrust&&victim.Interaction.HumanBond.LastHarmAt==lastHumanHarm,
        "surviving witness missed the fatal strike, or blamed the human instead");
});

Console.WriteLine($"Peer threat continuity: {failures} failure(s)");
return failures==0?0:1;

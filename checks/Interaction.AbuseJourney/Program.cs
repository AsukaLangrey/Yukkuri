using System.Text.Json;
using Yukkuri.Simulation;

// Full real-input journey. No injected wounds, health, fear, death or clock.
// Source workstation: never execute. Separately authorised remote host only.
try
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261031);
    var victim=sim.State.Creatures[0];var witness=sim.State.Creatures[1];
    Require(sim.State.Settings.LethalInjuries&&victim.Condition==PhysicalCondition.Active&&victim.Health==100&&
        victim.Interaction.FootIntegrity==100&&victim.Interaction.Injuries.Sites.Count==0,"default healthy starting state changed");

    // Keep a surviving observer in actual sight through the same fixture tool
    // available to the player. Record its baseline after this separate action.
    Act(sim,witness,InteractionAction.Pin);
    double witnessTrust=witness.Trust,witnessFear=witness.Fear,witnessAlarm=witness.Interaction.Emotion.Alarm;
    Act(sim,victim,InteractionAction.Pin);
    Act(sim,victim,InteractionAction.Punch,BodyPart.Side,0);
    Require(victim.Health<100&&victim.Pain>0&&victim.Interaction.Injuries.At(BodyPart.Side)?.Damage>0&&
        victim.Interaction.Emotion.Alarm>0,"actual punch omitted pain, local damage or its emotional aftermath");
    Trace(sim,victim,"pain after real punch");

    var burn=new InteractionInput(InteractionAction.BurnFeet,BodyPart.Bottom,1,.1,X:victim.X,Z:victim.Z);
    Require(sim.BeginInteractionGesture(victim.Id,burn).Success,"real foot contact could not begin");
    int samples=0;
    while(victim.Interaction.Injuries.PermanentFootLoss<75&&samples++<120)
    {
        Require(sim.ContinueInteractionGesture(burn,true).Success,"held foot contact stopped before its actual disability milestone");
        Frame(sim);
    }
    sim.EndInteractionGesture();
    Require(victim.Condition==PhysicalCondition.Active&&victim.Interaction.Injuries.PermanentFootLoss>=75&&
        victim.Interaction.FootIntegrity<=25,"real continuous contact failed to reach surviving permanent disability");
    Act(sim,victim,InteractionAction.Release);
    Require(victim.Interaction.Restraint==RestraintKind.None&&!WorldSimulation.CanSelfMove(victim),
        "mobility loss was only the removable frame, or release cured actual disability");
    double permanent=victim.Interaction.Injuries.PermanentFootLoss;
    Trace(sim,victim,"persistent disability after actual release");

    // A real care gesture can stabilise wounds; it cannot undo permanent loss.
    var care=new InteractionInput(InteractionAction.Care,BodyPart.Bottom,Duration:.1);
    Require(sim.BeginInteractionGesture(victim.Id,care).Success,"actual care could not begin");
    for(int n=0;n<18;n++){sim.ContinueInteractionGesture(care,true);Frame(sim);}
    sim.EndInteractionGesture();
    Require(victim.Interaction.Injuries.At(BodyPart.Bottom)!.CareRemaining>0&&
        victim.Interaction.Injuries.PermanentFootLoss==permanent&&!WorldSimulation.CanSelfMove(victim),
        "care skipped its real contact or restored permanent mobility immediately");

    int strikes=0;
    while(victim.Condition==PhysicalCondition.Active&&strikes++<16)
    {
        Act(sim,victim,InteractionAction.Punch,BodyPart.Side,1);
        Frame(sim);
    }
    Require(victim.Condition==PhysicalCondition.Critical&&victim.CriticalRemaining>0,
        "continued real strikes did not produce an actual rescue window");
    double criticalAt=sim.State.WorldSeconds,remaining=victim.CriticalRemaining;
    Trace(sim,victim,"critical rescue window");
    // No rescue is chosen here. Only ordinary time consumes the real window.
    double deadline=sim.State.WorldSeconds+remaining+5;
    while(victim.Condition!=PhysicalCondition.Dead&&sim.State.WorldSeconds<deadline)Frame(sim);
    Require(victim.Condition==PhysicalCondition.Dead&&sim.State.WorldSeconds>criticalAt&&
        victim.Interaction.Injuries.DeathCause.Length>0,"unattended critical state did not become irreversible death through time");
    Require(witness.Condition!=PhysicalCondition.Dead&&witness.Fear>witnessFear&&witness.Trust<witnessTrust&&
        witness.Interaction.Emotion.Alarm>witnessAlarm&&witness.Family.KnownLosses.TryGetValue(victim.Id,out var loss)&&
        loss.Source==KnowledgeSource.Witness,"surviving eyewitness did not retain intimidation and actual death knowledge");
    Require(witness.PersonalMemories.Any(m=>sim.State.Incidents.Any(e=>e.Id==m.IncidentId&&
        e.Kind==IncidentKind.Attack&&e.ActorId=="player"&&e.TargetId==victim.Id)),"no surviving personal memory identifies the real player harm");
    int medicine=sim.State.Medicine;
    Require(!sim.BeginInteractionGesture(victim.Id,care).Success&&sim.State.Medicine==medicine,
        "care revived the dead or consumed supplies on an invalid corpse interaction");
    SaveStore.Validate(sim.State);
    var copyState=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    var copy=new WorldSimulation(copyState);Frame(copy);
    Require(copyState.Creatures[0].Condition==PhysicalCondition.Dead&&copyState.Creatures[0].Interaction.Injuries.PermanentFootLoss==permanent&&
        copyState.Incidents.Count(e=>e.Kind==IncidentKind.Death&&e.TargetId==victim.Id)==1,
        "reload revived the body, erased disability, or repeated death settlement");
    Trace(sim,victim,"death with surviving eyewitness consequences");
    Console.WriteLine("PASS healthy -> actual pain -> permanent disability -> critical rescue window -> death with surviving witnesses");
    return 0;
}
catch(Exception e){Console.WriteLine("FAIL abuse journey: "+e);return 1;}

static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
static void Frame(WorldSimulation sim){sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);}
static void Act(WorldSimulation sim,Creature c,InteractionAction action,BodyPart part=BodyPart.Head,double force=.3)
{
    var result=sim.ExecuteInteraction(c.Id,new(action,part,force));
    Require(result.Success,$"{action} refused: {result.Message}");
}
static void Trace(WorldSimulation sim,Creature c,string stage)=>Console.WriteLine($"{stage}: t={sim.State.WorldSeconds:F2}, "+
    $"health={c.Health:F2}, pain={c.Pain:F2}, fear={c.Fear:F2}, alarm={c.Interaction.Emotion.Alarm:F2}, "+
    $"foot={c.Interaction.FootIntegrity:F2}, permanent={c.Interaction.Injuries.PermanentFootLoss:F2}, condition={c.Condition}");

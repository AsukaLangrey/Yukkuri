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
    LighterJourney();
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

static void LighterJourney()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261031);
    var victim=sim.State.Creatures[0];var witness=sim.State.Creatures[1];
    Require(sim.State.Mode!=PlayMode.CloseUp&&!sim.State.Settings.Pause&&victim.Health==100&&
        victim.Interaction.Injuries.Sites.Count==0,"lighter journey did not start healthy in ordinary world time");
    Act(sim,witness,InteractionAction.Pin);
    Act(sim,victim,InteractionAction.Pin);
    Require(victim.Interaction.Supine&&victim.Interaction.Restraint==RestraintKind.Frame,
        "actual fixing tool did not expose and hold the underside");
    // Input-path evidence, not a claim of mouse acceptance: Pin turns the skin
    // -105 degrees; its central underside faces forwards/upwards between the
    // frame's two side rails. RayContact classifies the actual nearest skin
    // triangle, and Room.Input passes that Part to Burn. A standing underside
    // cannot be selected through the face. Screen pixels remain a remote check.
    var burn=new InteractionInput(InteractionAction.Burn,BodyPart.Bottom,.4,.1,X:victim.X,Z:victim.Z);
    double started=sim.State.WorldSeconds,health=victim.Health;
    double alarm=victim.Interaction.Emotion.Alarm,witnessAlarm=witness.Interaction.Emotion.Alarm;
    double witnessFear=witness.Fear,witnessTrust=witness.Trust;
    double severity=Threats().Sum(e=>e.Severity);
    Require(sim.BeginInteractionGesture(victim.Id,burn).Success,"current lighter could not start on the exposed underside");
    for(int n=0;n<60;n++)
    {
        Require(sim.ContinueInteractionGesture(burn,true).Success,"actual initial lighter contact stopped");
        Frame(sim);
        if(n is 29 or 59)
        {
            Require(victim.Condition==PhysicalCondition.Active&&victim.Health<health&&
                victim.Interaction.Emotion.Alarm>alarm&&witness.Interaction.Emotion.Alarm>witnessAlarm&&
                witness.Fear>witnessFear&&witness.Trust<witnessTrust,
                "continued real contact lost its bodily or perceived consequences across ordinary event windows");
            health=victim.Health;alarm=victim.Interaction.Emotion.Alarm;
            witnessAlarm=witness.Interaction.Emotion.Alarm;witnessFear=witness.Fear;witnessTrust=witness.Trust;
        }
    }
    sim.EndInteractionGesture();
    const double firstDamage=6*(1.2+.4*5);
    Require(Math.Abs(sim.State.WorldSeconds-started-6)<.000001&&
        Math.Abs(victim.Interaction.Injuries.At(BodyPart.Bottom)!.Damage-firstDamage)<.000001&&
        Math.Abs(Threats().Sum(e=>e.Severity)-severity-firstDamage*1.8)<.000001,
        "ordinary-time lighter contact duplicated or omitted actual local harm or its threat severity");
    foreach(var incident in Threats())
        Require(witness.PersonalMemories.Count(m=>m.IncidentId==incident.Id)==1,
            "an actual visible threat window was forgotten or remembered repeatedly per sample");
    Trace(sim,victim,"current lighter after six ordinary seconds");

    // A short interruption includes a real accepted and swallowed sweet, not
    // a filled hunger field. The care budget was exhausted by actual harm.
    double lastHarm=victim.Interaction.HumanBond.LastHarmAt;
    double lastThreat=victim.Interaction.Emotion.LastThreatAt,oldAlarm=victim.Interaction.Emotion.Alarm;
    double wound=victim.Interaction.Injuries.At(BodyPart.Bottom)!.Damage,trust=victim.Trust;
    double fullness=victim.Fullness;int stock=sim.State.Food;
    long intake=victim.Interaction.IntakeSequence;
    var sweet=new InteractionInput(InteractionAction.FeedSweet,BodyPart.Mouth,Duration:.1);
    Require(sim.BeginInteractionGesture(victim.Id,sweet).Success,"real post-harm sweet offer could not begin");
    for(int n=0;n<8;n++)
    {Require(sim.ContinueInteractionGesture(sweet,true).Success,"post-harm sweet was not actually accepted");Frame(sim);}
    sim.EndInteractionGesture();
    Require(sim.State.Food==stock-1&&victim.Fullness>fullness&&victim.Interaction.IntakeSequence==intake+1,
        "post-harm feeding lacked one actual stock debit and one mouth entry");
    double foodDeadline=sim.State.WorldSeconds+10;
    while(victim.Interaction.EatingRemaining>0&&sim.State.WorldSeconds<foodDeadline)Frame(sim);
    Require(victim.Interaction.EatingRemaining==0&&sim.State.WorldSeconds-lastHarm<15&&
        victim.Interaction.HumanBond.LastHarmAt==lastHarm&&victim.Interaction.Emotion.LastThreatAt==lastThreat&&
        victim.Interaction.Emotion.Alarm>=oldAlarm&&victim.Trust<=trust&&
        victim.Interaction.Injuries.At(BodyPart.Bottom)!.Damage==wound&&
        victim.Fear>=EmotionalReadiness.FearFloor(victim)&&!EmotionalReadiness.Settled(victim)&&
        victim.Expression is not (Expression.Content or Expression.Delighted),
        "one swallowed sweet or a short pause erased the actual wound, distrust or unsettled aftermath");
    Require(witness.Interaction.Emotion.Alarm>=witnessAlarm&&witness.Trust<=witnessTrust,
        "the nearby observer forgot the preceding contact during the feeding interruption");

    burn=burn with {Force=1};
    Require(sim.BeginInteractionGesture(victim.Id,burn).Success,"current lighter could not resume after the real meal");
    // No preliminary punch here: current Burn deals equal local and systemic
    // damage. The >=75 permanent-loss boundary needs >99 local damage, so all
    // health, ordinary recovery and untreated burden must remain real.
    int samples=0;
    while(victim.Interaction.Injuries.PermanentFootLoss<75&&victim.Condition==PhysicalCondition.Active&&samples++<200)
    {
        Require(sim.ContinueInteractionGesture(burn,true).Success,"resumed actual lighter contact stopped");
        Frame(sim);
    }
    sim.EndInteractionGesture();
    Require(victim.Condition==PhysicalCondition.Active&&victim.Health>0&&
        victim.Interaction.Injuries.PermanentFootLoss>=75&&victim.Interaction.FootIntegrity<=25&&
        victim.Interaction.HumanBond.LastHarmAt>lastHarm&&victim.Interaction.Emotion.LastThreatAt>lastThreat&&
        victim.Interaction.Emotion.Alarm>oldAlarm&&witness.Interaction.Emotion.Alarm>witnessAlarm&&
        witness.Trust<witnessTrust,"resumed current lighter failed the living disability or renewed-memory milestone");
    double permanent=victim.Interaction.Injuries.PermanentFootLoss;
    Act(sim,victim,InteractionAction.Release);
    Require(!victim.Interaction.Supine&&victim.Interaction.Restraint==RestraintKind.None&&!WorldSimulation.CanSelfMove(victim),
        "current lighter disability disappeared when the actual frame was released");
    // Re-expose the underside for real targeted care; the disability assertion
    // above was made while no fixture could account for the inability to walk.
    Act(sim,victim,InteractionAction.Pin);
    var care=new InteractionInput(InteractionAction.Care,BodyPart.Bottom,Duration:.1);
    int medicine=sim.State.Medicine;
    Require(sim.BeginInteractionGesture(victim.Id,care).Success,"actual lighter wound care could not begin");
    for(int n=0;n<18;n++)
    {
        Require(sim.ContinueInteractionGesture(care,true).Success,"actual wound care failed");Frame(sim);
        Require(victim.Condition==PhysicalCondition.Active&&victim.Health>0,
            "untreated burden consumed the living disability milestone before ordinary wound care finished");
    }
    sim.EndInteractionGesture();
    Require(sim.State.Medicine==medicine-1&&victim.Interaction.Injuries.At(BodyPart.Bottom)!.CareRemaining>0&&
        victim.Interaction.Injuries.PermanentFootLoss==permanent,"care failed to use stock once or erased permanent loss");
    Act(sim,victim,InteractionAction.Release);
    Require(!WorldSimulation.CanSelfMove(victim),"real care and release cured permanent disability");
    Trace(sim,victim,"current lighter permanent disability after actual care and release");
    int strikes=0;
    while(victim.Condition==PhysicalCondition.Active&&strikes++<16)
    {Act(sim,victim,InteractionAction.Punch,BodyPart.Side,1);Frame(sim);}
    Require(victim.Condition==PhysicalCondition.Critical&&victim.CriticalRemaining>0,
        "post-lighter real strike did not retain the actual rescue window");
    double criticalAt=sim.State.WorldSeconds,deathDeadline=criticalAt+victim.CriticalRemaining+5;
    while(victim.Condition!=PhysicalCondition.Dead&&sim.State.WorldSeconds<deathDeadline)Frame(sim);
    Require(victim.Condition==PhysicalCondition.Dead&&sim.State.WorldSeconds>criticalAt&&
        victim.Interaction.Injuries.DeathCause.Length>0&&witness.Condition!=PhysicalCondition.Dead&&
        witness.Interaction.Emotion.Alarm>0&&witness.Fear>0&&witness.Trust<witnessTrust&&
        witness.Family.KnownLosses.TryGetValue(victim.Id,out var loss)&&loss.Source==KnowledgeSource.Witness&&
        witness.PersonalMemories.Any(m=>Threats().Any(e=>e.Id==m.IncidentId)),
        "current-tool death lacked surviving intimidation, personal memory or actual death knowledge");
    SaveStore.Validate(sim.State);
    var copied=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    var copy=new WorldSimulation(copied);Frame(copy);
    Require(copied.Creatures[0].Condition==PhysicalCondition.Dead&&
        copied.Creatures[0].Interaction.Injuries.PermanentFootLoss==permanent&&
        copied.Incidents.Count(e=>e.Kind==IncidentKind.Death&&e.TargetId==victim.Id)==1&&
        copied.Creatures[1].Family.KnownLosses.ContainsKey(victim.Id)&&copied.Creatures[1].Interaction.Emotion.Alarm>0,
        "reload lost the current-tool consequences or repeated death");
    Trace(sim,victim,"current lighter journey death with surviving witness");
    Console.WriteLine("PASS current lighter -> real feeding interruption -> renewed harm -> permanent disability -> critical -> death");
    IEnumerable<Incident> Threats()=>sim.State.Incidents.Where(e=>e.Kind==IncidentKind.Attack&&e.ActorId=="player"&&e.TargetId==victim.Id);
}

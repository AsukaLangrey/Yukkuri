using System.Text.Json;
using Yukkuri.Simulation;

int passed=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);passed++;Console.WriteLine("PASS: "+message);}
void Invalid(Action action,string message){try{action();}catch(InvalidDataException){Check(true,message);return;}throw new Exception("Not rejected: "+message);}
string Json(WorldState state)=>JsonSerializer.Serialize(state,SaveStore.JsonOptions);
WorldSimulation New()
{
    var s=WorldSimulation.CreateMilestone();s.State.Settings.Invasions=false;
    foreach(var r in s.State.Regions)r.StaffEnabled=false;
    foreach(var c in s.State.Creatures){c.BehaviorCooldown=9999;c.DecisionIn=9999;c.GuardDuty=false;}
    return s;
}
void Place(Creature c,double x,double z){c.X=x;c.Z=z;c.TargetX=x;c.TargetZ=z;c.DecisionIn=9999;}
void WoundToCritical(WorldSimulation world,Creature creature)
{while(creature.Health>0)world.Hurt(creature,10,"cumulative injury",null);}
var sim=New();var c=sim.Selected!;
SaveStore.Validate(sim.State);Check(sim.State.Creatures.Count==11&&c.Name=="小铃","authored M01 scenario has valid identities and distinct starting individuals");
var stroke=sim.Contact(c.Id,new(ContactAction.Stroke,ContactArea.Forehead));
var hug=sim.Contact(c.Id,new(ContactAction.Hug,ContactArea.Body));
var nuzzle=sim.Contact(c.Id,new(ContactAction.Nuzzle,ContactArea.Cheek));
Check(stroke.Success&&hug.Success&&nuzzle.Success&&new[]{stroke.Reaction,hug.Reaction,nuzzle.Reaction}.Distinct().Count()==3,"stroke hug and nuzzle produce distinct responses");
Check(c.Trust>30&&c.Attachment>20&&c.PersonalMemories.Count==3,"care changes trust and attachment and preserves personal memories");
sim.Travel("wild");var candidate=sim.Selected!;
Check(!sim.Contact(candidate.Id,new(ContactAction.Hug,ContactArea.Body)).Success,"unfamiliar wild creature can refuse intimate contact");
Check(!sim.InviteToSanctuary().Success,"unknown wild creature is not silently teleported into sanctuary");
for(int i=0;i<3;i++){sim.Feed(candidate.Id);sim.Contact(candidate.Id,new(ContactAction.Stroke,Duration:2));sim.Advance(2);}
string candidateId=candidate.Id;
Check(sim.InviteToSanctuary().Success&&candidate.RegionId=="home"&&candidate.Quarantine&&candidate.Id==candidateId,"earned trust permits invitation and preserves identity");
sim.Travel("home");sim.Select(candidateId);
Check(sim.AdmitSelected()&&!candidate.Quarantine&&candidate.Belonging>=45,"player admission builds belonging but does not award a certificate");
var gentle=New();var hard=New();
gentle.Contact(gentle.Selected!.Id,new(ContactAction.Discipline,ContactArea.Body,ContactTool.SoftGlove,.2,.5));
hard.Contact(hard.Selected!.Id,new(ContactAction.Discipline,ContactArea.Cheek,ContactTool.TrainingPaddle,.8,3));
Check(hard.Selected!.Health<gentle.Selected!.Health&&hard.Selected.Pain>gentle.Selected.Pain,"position tool force and duration produce different actual injury");
var protectedCare=New();double before=protectedCare.Selected!.Health;
protectedCare.Contact(protectedCare.Selected.Id,new(ContactAction.Hug,ContactArea.Body,Force:1,Duration:6));
Check(protectedCare.Selected.Health==before,"care-tool protection prevents excessive pressure injury");
foreach(var action in new[]{ContactAction.Stroke,ContactAction.Hug,ContactAction.Nuzzle})
foreach(var area in Enum.GetValues<ContactArea>())
foreach(var tool in Enum.GetValues<ContactTool>())
{
    var careCase=WorldSimulation.CreateMilestone();
    var cared=careCase.Selected!;
    double startingHealth=cared.Health;
    var response=careCase.Contact(cared.Id,new(action,area,tool,1,12));
    if(cared.Health!=startingHealth)
        throw new Exception($"Protected care unexpectedly injured: {action}/{area}/{tool}, damage={startingHealth-cared.Health}");
}
Check(true,"care protection covers all affection actions body areas and tools at maximum force and duration");
protectedCare.State.Settings.ProtectCareTools=false;
protectedCare.Contact(protectedCare.Selected.Id,new(ContactAction.Hug,ContactArea.Body,Force:1,Duration:6));
Check(protectedCare.Selected.Health<before,"disabling care protection changes real rules, not just visuals");
sim=New();c=sim.Selected!;
double sharing=c.SharingSkill,guard=c.GuardSkill;
for(int i=0;i<4;i++)sim.Contact(c.Id,new(ContactAction.Train,Duration:3));
Check(c.SharingSkill>sharing&&c.GuardSkill>guard&&sim.State.Progress.Completed.Contains("train"),"training affects learned behavior and guard ability");
double fatigue=c.TrainingFatigue;sim.Advance(20);
Check(c.TrainingFatigue<fatigue,"time spent resting allows training fatigue to recover");
sim=New();var witness=sim.State.Creatures[0];var victim=sim.State.Creatures[1];var far=sim.State.Creatures[4];
Place(witness,0,0);Place(victim,1,0);Place(far,8,-8);
foreach(var other in sim.State.Creatures.Where(x=>x.RegionId=="home"&&x!=witness&&x!=victim&&x!=far))Place(other,7,7);
double fear=witness.Fear,farFear=far.Fear;
var bad=sim.Contact(victim.Id,new(ContactAction.Discipline,Force:.5,Duration:2));
Check(bad.Success&&!bad.Justified&&witness.Fear>fear&&far.Fear==farFear,"unjustified harm frightens witnesses but not an uninformed distant creature");
Check(!sim.Knows(far,bad.IncidentId),"distant creature has no magical knowledge of the incident");
Place(far,.3,.3);sim.Advance(12);
Check(far.Knowledge.Any(k=>k.IncidentId==bad.IncidentId&&k.Source==KnowledgeSource.Told),"nearby conversation can carry previously unknown incident information");
sim=New();sim.State.Settings.Invasions=true;sim.State.Threat.NextAt=0;
witness=sim.State.Creatures[0];far=sim.State.Creatures[4];
Place(witness,-7,3);Place(far,8,-8);
sim.Advance(11);
var invader=sim.State.Creatures.Single(x=>x.Id==sim.State.Threat.IntruderId);
Check(invader.IsIntruder&&invader.RegionId=="home"&&sim.Knows(witness,invader.CurrentOffenseId),"nearby creature witnesses the actual intrusion");
double trust=witness.Trust;farFear=far.Fear;
var justified=sim.Contact(invader.Id,new(ContactAction.Discipline,ContactArea.Body,Force:.25,Duration:1));
Check(justified.Justified&&witness.Trust>trust&&far.Fear==farFear,"proportionate correction of a known intruder is approved by an informed sanctuary creature");
sim.Advance(12);
Check(invader.RegionId=="wild"&&sim.State.Progress.Completed.Contains("defense"),"intervention resolves encounter and returns the same intruder to wilderness");
sim=New();sim.State.Settings.Invasions=true;sim.State.Threat.NextAt=0;
var defender=sim.State.Creatures[1];defender.GuardDuty=true;defender.GuardSkill=0;defender.Health=70;defender.Energy=40;
sim.Advance(35);
Check(sim.State.Threat.HelpRequested&&sim.State.HelpTargetId!=null,"outmatched sanctuary defender calls the protagonist for help");
sim.FocusHelp();var rescue=sim.ProtectSelected();
Check(rescue.Success&&sim.State.Threat.PlayerResponded,"player can respond to the call and protect the defender");
sim.Advance(10);Check(sim.State.Threat.Phase is ThreatPhase.Resolved or ThreatPhase.Quiet,"help resolves the threat instead of leaving a permanent attack state");
sim=New();c=sim.Selected!;
WoundToCritical(sim,c);
Check(c.Health==0&&c.Condition==PhysicalCondition.Critical,"M01 injury is not a hidden nonlethal health floor");
int meds=sim.State.Medicine;
var treatment=sim.Contact(c.Id,new(ContactAction.Treat,Duration:3));
Check(treatment.Success&&sim.State.Medicine==meds-1&&c.Condition==PhysicalCondition.Recovering,"emergency treatment spends medicine and enters timed recovery");
sim.Advance(60);
Check(c.Condition==PhysicalCondition.Active&&c.Health>=25,"stabilized individual recovers after actual world time");
sim=New();c=sim.Selected!;WoundToCritical(sim,c);sim.Advance(100);
Check(c.Condition==PhysicalCondition.Dead&&!sim.Contact(c.Id,new(ContactAction.Treat)).Success,"untreated critical injury can become irreversible");
sim=New();sim.State.Settings.LethalInjuries=false;c=sim.Selected!;WoundToCritical(sim,c);sim.Advance(100);
Check(c.Condition!=PhysicalCondition.Dead,"independent lethal-injury toggle preserves injury but prevents death");
sim=New();c=sim.Selected!;sim.EnterCloseUp();
var frozen=Json(sim.State);sim.Advance(1000);
Check(Json(sim.State)==frozen,"M01 close-up freezes every passive state and random generator");
sim.Contact(c.Id,new(ContactAction.Stroke,Duration:3));
Check(sim.State.WorldSeconds==0&&c.Trust>30,"direct affection still works without target aging during close-up");
sim.ExitCloseUp();sim.Advance(.25);Check(sim.State.WorldSeconds==.25,"M01 close-up exits without catch-up");
sim.State.Settings.Pause=true;frozen=Json(sim.State);sim.Advance(100);
Check(frozen==Json(sim.State),"manual pause freezes passive simulation");
sim.State.Settings.Pause=false;
string directory=Path.Combine(Path.GetTempPath(),"yukkuri-m01-checks",Guid.NewGuid().ToString("N"));
var saves=new SaveStore(directory);saves.Save(sim.State,1);var loaded=saves.Load(1);
Check(Json(loaded.State)==Json(sim.State),"M01 feelings knowledge events progress and settings survive save roundtrip");
var restored=new WorldSimulation(loaded.State);
sim.Advance(20);restored.Advance(20);
Check(Json(restored.State)==Json(sim.State),"M01 loaded tuning and random state continue deterministically");
var invalid=New().State;invalid.Creatures[0].Fear=double.NaN;
Invalid(()=>SaveStore.Validate(invalid),"nonfinite psychological values are rejected before UI");
invalid=New().State;invalid.Settings=null!;
Invalid(()=>SaveStore.Validate(invalid),"null M01 settings produce controlled invalid-data error");
invalid=New().State;invalid.Creatures[0].ParentId=invalid.Creatures[0].Id;
Invalid(()=>SaveStore.Validate(invalid),"self-parent identity is rejected");
var completion=New();
foreach(var flag in new[]{"feed","affection","rescue","protect","warn","treat"})completion.State.Progress.Completed.Add(flag);
completion.Advance(.25);
Check(!completion.State.ScenarioComplete,"six unrelated flags cannot substitute for the six actual chapter objectives");
foreach(var flag in new[]{"train","admit","defense","contract"})completion.State.Progress.Completed.Add(flag);
completion.Advance(.25);Check(completion.State.ScenarioComplete,"all named objectives unlock the chapter completion state");

WorldSimulation SharingScene(ulong seed,bool trained,bool parentNearby,int sessions=0)
{
    var w=New();w.State.RandomState=unchecked(seed*0x9E3779B97F4A7C15UL);
    foreach(var oil in w.State.Creatures.Where(x=>x.RegionId=="home"))Place(oil,8,-8);
    var child=w.State.Creatures[2];var parent=w.State.Creatures[0];var peer=w.State.Creatures[4];
    Place(child,0,0);Place(peer,1,0);Place(parent,parentNearby?0:8,parentNearby?1:-8);
    var bowl=w.State.Regions[0].Facilities.First(x=>x.Kind==FacilityKind.Feeder);bowl.X=0;bowl.Z=0;bowl.Stock=20;
    child.Fullness=65;child.BehaviorCooldown=0;child.SharingSkill=trained?40:5;
    for(int i=0;i<sessions;i++)w.Contact(child.Id,new(ContactAction.Train,Duration:3));
    w.Advance(.25);return w;
}
int corrected=0,uncorrected=0;
for(ulong seed=1;seed<=40;seed++)
{
    var w=SharingScene(seed,false,true);var child=w.State.Creatures[2];var parent=w.State.Creatures[0];
    if(w.State.Incidents.Any(e=>e.Kind==IncidentKind.Correction&&e.ActorId==parent.Id&&e.TargetId==child.Id))corrected++;else uncorrected++;
}
Check(corrected>0&&uncorrected>0,"parent correction is a conditional probability, not unconditional scripted punishment");
var unseen=SharingScene(8,false,false);
Check(!unseen.State.Incidents.Any(e=>e.Kind==IncidentKind.Correction),"distant uninformed parent and elders do not correct unseen food theft");
var learned=SharingScene(8,true,true);
Check(!learned.State.Incidents.Any(e=>e.Kind==IncidentKind.Theft),"learned sharing changes actual food competition behavior");
int baselineThefts=0,practicedThefts=0;
for(ulong seed=1;seed<=120;seed++)
{
    if(SharingScene(seed,false,false).State.Incidents.Any(e=>e.Kind==IncidentKind.Theft))baselineThefts++;
    if(SharingScene(seed,false,false,6).State.Incidents.Any(e=>e.Kind==IncidentKind.Theft))practicedThefts++;
}
Check(practicedThefts<baselineThefts*.75&&practicedThefts>0,"six real training sessions reduce actual theft across matched seeds without guaranteeing instant perfection");
Console.WriteLine($"TRAINING_BEHAVIOR_COMPARISON: controls={baselineThefts}/120; six_sessions={practicedThefts}/120");
var pruning=New();pruning.State.Settings.Invasions=true;pruning.State.Threat.NextAt=0;pruning.Advance(11);
var offender=pruning.State.Creatures.Single(x=>x.Id==pruning.State.Threat.IntruderId);string offense=offender.CurrentOffenseId!;
for(int i=0;i<510;i++)pruning.Contact(pruning.Selected!.Id,new(ContactAction.Stroke));
Check(pruning.State.Incidents.Any(e=>e.Id==offense)&&pruning.State.PlayerKnownIncidents.Contains(offense),"long interaction sessions preserve active offense evidence during history pruning");
pruning.Hurt(offender,200,"test severe injury",null);pruning.Advance(.25);
Check(!offender.IsIntruder&&offender.CurrentOffenseId==null&&pruning.State.Threat.Phase==ThreatPhase.Resolved,"incapacitated intruder does not leave a permanent hostile encounter");
var terminal=New();var terminalOil=terminal.Selected!;terminalOil.Health=1;terminalOil.FillingIntegrity=1;
var terminalContact=terminal.Contact(terminalOil.Id,new(ContactAction.Discipline,ContactArea.Cheek,ContactTool.TrainingPaddle,1,6));
Console.WriteLine("TERMINAL_CONTACT: "+JsonSerializer.Serialize(new{terminalOil.Condition,terminalOil.Speech,terminalOil.LastReaction,terminalContact.Message}));
Check(terminalOil.Condition==PhysicalCondition.Dead&&terminalOil.Speech==""&&terminalOil.LastReaction=="再也没有回应。","fatal contact preserves terminal state instead of restoring a living response");
var underfed=New();var recovering=underfed.Selected!;recovering.Fullness=10;WoundToCritical(underfed,recovering);
underfed.Contact(recovering.Id,new(ContactAction.Treat));int foodBeforeRecovery=underfed.State.Food;
Check(underfed.Feed(recovering.Id)&&recovering.Fullness>10&&underfed.State.Food==foodBeforeRecovery-1&&recovering.Condition==PhysicalCondition.Recovering,"a stabilized recovering individual can receive food without regaining mobility early");
var criticalDefense=New();criticalDefense.State.Settings.Invasions=true;criticalDefense.State.Threat.NextAt=0;criticalDefense.Advance(11);
var hurtGuardian=criticalDefense.State.Creatures[1];Place(hurtGuardian,-7.5,3.8);WoundToCritical(criticalDefense,hurtGuardian);criticalDefense.Select(hurtGuardian.Id);
Check(criticalDefense.ProtectSelected().Success&&hurtGuardian.Condition==PhysicalCondition.Critical,"protagonist can protect an incapacitated victim without requiring it to move");
var parallelHelp=New();parallelHelp.State.Settings.Invasions=true;parallelHelp.State.Threat.NextAt=0;parallelHelp.Advance(11);
var emergency=parallelHelp.State.Creatures[0];var protectedPeer=parallelHelp.State.Creatures[1];Place(protectedPeer,-7.5,3.8);WoundToCritical(parallelHelp,emergency);
parallelHelp.Select(protectedPeer.Id);Check(parallelHelp.ProtectSelected().Success&&parallelHelp.State.HelpTargetId==emergency.Id,"protecting another individual does not erase an existing critical rescue request");
parallelHelp.Advance(10);Check(parallelHelp.State.HelpTargetId==emergency.Id,"ending an intrusion preserves the unrelated unresolved medical request");
parallelHelp.Contact(emergency.Id,new(ContactAction.Treat));Check(parallelHelp.State.HelpTargetId==null,"stabilizing the last emergency clears its resolved medical request");
var remainingHelp=New();var firstEmergency=remainingHelp.State.Creatures[0];var secondEmergency=remainingHelp.State.Creatures[1];
WoundToCritical(remainingHelp,firstEmergency);WoundToCritical(remainingHelp,secondEmergency);remainingHelp.Hurt(secondEmergency,500,"terminal fixture",null);
Check(remainingHelp.State.HelpTargetId==firstEmergency.Id,"losing one critical individual does not hide another unresolved emergency");
var medicalPriority=New();var urgentHome=medicalPriority.Selected!;WoundToCritical(medicalPriority,urgentHome);
medicalPriority.Travel("wild");
var wildBully=medicalPriority.State.Creatures.First(c=>c.RegionId=="wild"&&c.Aggression>.7);
var wildVictim=medicalPriority.State.Creatures.First(c=>c.RegionId=="wild"&&c.Aggression<.35);
Place(wildBully,wildVictim.X+.2,wildVictim.Z);wildBully.BehaviorCooldown=0;medicalPriority.Advance(.25);
Check(wildBully.CurrentOffenseId!=null&&medicalPriority.State.HelpTargetId==urgentHome.Id,"new wilderness bullying does not replace an unresolved critical medical request");
WorldSimulation BullyingScene()
{
    var world=New();world.Travel("wild");
    var bully=world.State.Creatures[7];var target=world.State.Creatures[5];
    foreach(var other in world.State.Creatures.Where(c=>c.RegionId=="wild"))Place(other,8,-8);
    Place(target,0,0);Place(bully,.2,0);bully.BehaviorCooldown=0;world.Advance(.25);
    return world;
}
var wrongProtection=BullyingScene();var innocent=wrongProtection.State.Creatures[6];Place(innocent,1,0);wrongProtection.Select(innocent.Id);
bool wrongProtectionRejected=!wrongProtection.ProtectSelected().Success;
var repeatedProtection=BullyingScene();var protectedWild=repeatedProtection.State.Creatures[5];repeatedProtection.Select(protectedWild.Id);
bool initialProtection=repeatedProtection.ProtectSelected().Success;double earnedTrust=protectedWild.Trust;
bool repeatProtectionRejected=!repeatedProtection.ProtectSelected().Success&&protectedWild.Trust==earnedTrust;
var staleHelp=BullyingScene();Place(staleHelp.State.Creatures[7],8,-8);staleHelp.Advance(.25);
bool staleHelpCleared=staleHelp.State.HelpTargetId==null&&!staleHelp.FocusHelp();
Console.WriteLine("PROTECTION_REGRESSION: "+JsonSerializer.Serialize(new{wrongProtectionRejected,initialProtection,repeatProtectionRejected,staleHelpCleared}));
Check(wrongProtectionRejected,"protection does not award rescue credit for an unrelated nearby offense");
Check(initialProtection&&repeatProtectionRejected,"one resolved wild threat cannot be used for repeated trust and protection rewards");
Check(staleHelpCleared,"help no longer directs the player to a threat that has moved out of reach");
var staffDefense=New();staffDefense.State.Settings.Invasions=true;staffDefense.State.Threat.NextAt=0;staffDefense.Advance(11);
var hostile=staffDefense.State.Creatures.Single(c=>c.IsIntruder);var residentToCare=staffDefense.State.Creatures[0];
foreach(var member in staffDefense.State.Creatures.Where(c=>c.RegionId=="home")){member.Fullness=90;member.Health=100;}
hostile.Fullness=0;hostile.Health=10;residentToCare.Fullness=10;residentToCare.Health=50;
var homeRegion=staffDefense.State.Regions.Single(r=>r.Id=="home");homeRegion.StaffEnabled=true;homeRegion.StaffCooldown=0;staffDefense.Advance(.25);
Console.WriteLine("STAFF_DEFENSE_REGRESSION: "+JsonSerializer.Serialize(new{hostileFullness=hostile.Fullness,hostileHealth=hostile.Health,residentFullness=residentToCare.Fullness,residentHealth=residentToCare.Health}));
Check(hostile.Fullness<1&&hostile.Health<=10&&residentToCare.Fullness>10&&residentToCare.Health>50,"default park staff care for their residents instead of provisioning the active intruder");
Check(staffDefense.Feed(hostile.Id),"player may still deliberately feed an intruder through direct interaction");
var movingSelection=WorldSimulation.CreateMilestone();movingSelection.Travel("wild");
var movingIntruder=movingSelection.State.Creatures[7];movingSelection.Select(movingIntruder.Id);
movingSelection.State.Threat.NextAt=0;movingSelection.Advance(11);SaveStore.Validate(movingSelection.State);
Check(movingIntruder.RegionId=="home"&&movingSelection.Selected?.RegionId=="wild","a selected wild individual entering the park leaves a valid selection in the current region");
movingSelection.Travel("home");movingSelection.Select(movingIntruder.Id);
movingSelection.State.Threat.Phase=ThreatPhase.Retreating;movingSelection.State.Threat.PhaseTime=8;
movingSelection.Advance(.25);SaveStore.Validate(movingSelection.State);
Check(movingIntruder.RegionId=="wild"&&movingSelection.Selected?.RegionId=="home","selected retreating intruder leaves the park without making the world unsaveable");
var ongoingExpression=New();ongoingExpression.State.Settings.Invasions=true;ongoingExpression.State.Threat.NextAt=0;
ongoingExpression.Advance(18);
var expressiveIntruder=ongoingExpression.State.Creatures.Single(c=>c.IsIntruder);
Console.WriteLine("ONGOING_INTRUSION_EXPRESSION: "+JsonSerializer.Serialize(new{expressiveIntruder.Expression,expressiveIntruder.ReactionRemaining,ongoingExpression.State.Threat.Phase}));
Check(ongoingExpression.State.Threat.Phase==ThreatPhase.Intruding&&expressiveIntruder.Expression==Expression.Defiant,"ongoing intruder remains visibly defiant after its initial speech expires");
expressiveIntruder.Pain=35;expressiveIntruder.ReactionRemaining=0;ongoingExpression.Advance(.25);
Check(expressiveIntruder.Expression==Expression.Hurt,"ongoing hostile behavior does not conceal significant pain");
expressiveIntruder.Pain=0;expressiveIntruder.Fear=75;ongoingExpression.Advance(.25);
Check(expressiveIntruder.Expression==Expression.Frightened,"fear overrides an intruder's defiant resting expression");
expressiveIntruder.Fear=0;ongoingExpression.State.Threat.Phase=ThreatPhase.Retreating;ongoingExpression.Advance(.25);
Check(expressiveIntruder.Expression!=Expression.Defiant,"retreating individual does not retain the ongoing intrusion expression");
Console.WriteLine($"RESULT: {passed} M01 checks passed. Saves retained: {directory}");

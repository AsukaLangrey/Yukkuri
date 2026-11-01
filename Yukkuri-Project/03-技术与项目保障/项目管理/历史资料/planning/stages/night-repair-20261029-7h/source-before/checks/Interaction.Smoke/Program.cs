using System.Text.Json;
using Yukkuri.Simulation;

var results = new List<object>();
void Check(string name, Action action)
{
    try { action(); results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
    catch (Exception e) { results.Add(new { name, passed = false, error = e.ToString() }); Console.WriteLine("FAIL " + name + ": " + e.Message); }
}
void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
void Step(WorldSimulation sim, double seconds)
{
    for (int n = 0; n < Math.Ceiling(seconds * 10); n++) { sim.AdvanceInteractionPresentation(.1); sim.Advance(.1); }
}
InteractionFeedback Gesture(WorldSimulation sim, Creature c, InteractionAction action, double seconds = 2,
    double motion = .05, BodyPart part = BodyPart.Head, double force = .3, bool cancelled = false)
{
    var request = new InteractionInput(action, part, force, .1, motion, c.X, c.Z);
    var result = sim.BeginInteractionGesture(c.Id, request);
    for (int n = 0; n < Math.Ceiling(seconds * 10) && sim.HasInteractionGesture; n++)
        result = sim.ContinueInteractionGesture(request, true);
    sim.EndInteractionGesture(cancelled);
    return result;
}
WorldSimulation Fresh() => WorldSimulation.CreateInteractionPlaytest();

Check("cheek invitation receives an answer before movement earns affection", () =>
{
    var sim=Fresh();var c=sim.Selected!;double trust=c.Trust;
    var input=new InteractionInput(InteractionAction.Nuzzle,BodyPart.Cheek,.25,.1,0);
    sim.BeginInteractionGesture(c.Id,input);InteractionFeedback reply=new(true,"","","idle");
    for(int n=0;n<5;n++)reply=sim.ContinueInteractionGesture(input,true);
    Require(reply.Pose=="nuzzle-ready"&&reply.Speech.Length>0,"oil never answered the stationary invitation");
    Require(c.Trust==trust&&c.Interaction.NuzzleFamiliarity==0,"waiting alone farmed affection");
    reply=sim.ContinueInteractionGesture(input with {Motion=.04},true);
    Require(reply.Pose=="nuzzle"&&c.Trust>trust,"accepted gentle motion did not become mutual nuzzling");
});
Check("leaving the cheek requires a fresh invitation", () =>
{
    var sim=Fresh();var c=sim.Selected!;var input=new InteractionInput(InteractionAction.Nuzzle,BodyPart.Cheek,.25,.1,.04);
    sim.BeginInteractionGesture(c.Id,input);for(int n=0;n<7;n++)sim.ContinueInteractionGesture(input,true);
    double trust=c.Trust;sim.ContinueInteractionGesture(input,false);
    var reply=sim.ContinueInteractionGesture(input,true);
    Require(reply.Pose=="invite"&&c.Trust==trust,"lost contact reused the earlier permission");
    for(int n=0;n<4;n++)reply=sim.ContinueInteractionGesture(input,true);
    Require(reply.Pose=="nuzzle-ready","oil did not answer the renewed invitation");
});
Check("forceful nuzzling is rejected without a happy nuzzle memory", () =>
{
    var sim=Fresh();var c=sim.Selected!;double trust=c.Trust,fear=c.Fear;
    var reply=Gesture(sim,c,InteractionAction.Nuzzle,part:BodyPart.Cheek,force:.9);
    Require(reply.Refused&&reply.Pose=="withdraw","high force was treated as affection");
    Require(c.Trust<=trust&&c.Fear>fear&&c.Interaction.NuzzleFamiliarity==0,"rough touch earned affection");
    Require(c.Interaction.Memories.ContainsKey("rough-touch")&&!c.Interaction.Memories.ContainsKey("nuzzle"),"rough touch recorded as happy nuzzling");
});
Check("fast rubbing is rejected even with a gentle force setting", () =>
{
    var sim=Fresh();var c=sim.Selected!;double trust=c.Trust;
    var reply=Gesture(sim,c,InteractionAction.Stroke,motion:.25,force:.2);
    Require(reply.Refused&&c.Trust<=trust,"fast scrubbing still rewarded as a gentle stroke");
});
Check("nuzzle guidance names the wrong body part before inviting", () =>
{
    var sim=Fresh();var c=sim.Selected!;
    var input=new InteractionInput(InteractionAction.Nuzzle,BodyPart.Head,.25,.1,.04);
    sim.BeginInteractionGesture(c.Id,input);var reply=sim.ContinueInteractionGesture(input,true);
    Require(reply.Message.Contains("脸侧")&&!reply.Message.Contains("等它回应"),"off-cheek hit pretended an invitation was underway");
});
Check("first batch contains twenty groups and unique reachable actions", () =>
{
    Require(InteractionCatalog.Groups.Length == 20, "group lost");
    var choices = InteractionCatalog.Groups.SelectMany(g => g.Choices).ToArray();
    Require(choices.Select(c => c.Action).Distinct().Count() == Enum.GetValues<InteractionAction>().Length, "missing action definition");
    Require(choices.Length == choices.Select(c => c.Action).Distinct().Count(), "duplicate action");
});
Check("pressing down and squeezing sideways return distinct body feedback", () =>
{
    var first=Fresh();var second=Fresh();
    var press=Gesture(first,first.Selected!,InteractionAction.Press,seconds:.3,part:BodyPart.Head,force:.5);
    var squeeze=Gesture(second,second.Selected!,InteractionAction.Squeeze,seconds:.3,part:BodyPart.Side,force:.5);
    Require(press.Pose=="pressure"&&squeeze.Pose=="squeeze","both tools only flatten the body downward");
    Require(first.Selected!.Fear>0&&second.Selected!.Fear>0,"body reaction lost its continuing distress");
});
Check("fresh world saves without loss of identity", () => SaveStore.Validate(Fresh().State));
Check("motion differentiates a stroke from a stationary hold", () =>
{
    var sim = Fresh(); var c = sim.Selected!; double trust = c.Trust;
    Gesture(sim, c, InteractionAction.Stroke, motion: 0);
    Require(c.Trust == trust, "stationary hold counted as strokes");
    Gesture(sim, c, InteractionAction.Stroke);
    Require(c.Trust > trust, "moving touch never changed response");
});
Check("leaving contact never continues hidden effects", () =>
{
    var sim = Fresh(); var c = sim.Selected!;
    var input = new InteractionInput(InteractionAction.BurnFeet, BodyPart.Bottom, .6, .1, .1);
    sim.BeginInteractionGesture(c.Id, input);
    for (int n = 0; n < 100; n++) sim.ContinueInteractionGesture(input, false);
    Require(c.Interaction.FootIntegrity == 100, "out of contact still hurts");
});
Check("gesture target stays locked when a different selection appears", () =>
{
    var sim = Fresh(); var first = sim.Selected!; var second = sim.State.Creatures[1];
    var input = new InteractionInput(InteractionAction.Stroke, Motion: .1);
    sim.BeginInteractionGesture(first.Id, input); sim.State.SelectedId = second.Id;
    sim.ContinueInteractionGesture(input, true);
    Require(second.Trust == 48 && first.Trust > 38, "gesture transferred to another oil");
});
Check("nuzzle requires the cheek and respects rejection", () =>
{
    var sim = Fresh(); var c = sim.Selected!; double trust = c.Trust;
    Gesture(sim, c, InteractionAction.Nuzzle, part: BodyPart.Head);
    Require(c.Trust == trust, "head falsely counted as mutual cheek contact");
    Gesture(sim, c, InteractionAction.Nuzzle, part: BodyPart.Cheek);
    Require(c.Interaction.NuzzleFamiliarity > 0 && c.Trust > trust, "no cheek response");
    c.Fear = 90; trust = c.Trust;
    Require(Gesture(sim, c, InteractionAction.Nuzzle, part: BodyPart.Cheek).Refused && c.Trust == trust, "fear ignored");
});
Check("a feeding gesture consumes exactly one portion", () =>
{
    var sim = Fresh(); var c = sim.Selected!; sim.State.UnlimitedResources = false;
    int before = sim.State.Food; double fullness = c.Fullness;
    Gesture(sim, c, InteractionAction.FeedMeal, seconds: 8, part: BodyPart.Mouth);
    Require(sim.State.Food == before - 1 && c.Fullness == fullness + 19, "duplicate food consumption");
    Require(!sim.BeginInteractionGesture(c.Id, new(InteractionAction.FeedMeal, BodyPart.Mouth)).Success, "can refeed before swallowing");
    Step(sim, 2);
    Require(sim.BeginInteractionGesture(c.Id, new(InteractionAction.FeedMeal, BodyPart.Mouth)).Success, "swallow never ends");
});
Check("cancel before food entry does not consume it", () =>
{
    var sim = Fresh(); var c = sim.Selected!; sim.State.UnlimitedResources = false; int before = sim.State.Food;
    Gesture(sim, c, InteractionAction.FeedMeal, seconds: .3, part: BodyPart.Mouth, cancelled: true);
    Require(sim.State.Food == before && c.Fullness == 55, "unaccepted food consumed");
});
Check("fullness is a valid refusal, not disobedience", () =>
{
    var sim = Fresh(); var c = sim.Selected!; c.Fullness = 100;
    Require(Gesture(sim, c, InteractionAction.FeedMeal, part: BodyPart.Mouth).Refused, "full creature forced to eat");
});
Check("taking and returning an accessory cannot farm trust", () =>
{
    var sim = Fresh(); var c = sim.Selected!; double before = c.Trust; string identity = c.Id, accessory = c.Interaction.Accessory.Id;
    for (int n = 0; n < 5; n++)
    { sim.ExecuteInteraction(c.Id, new(InteractionAction.TakeAccessory,BodyPart.Accessory)); sim.ExecuteInteraction(c.Id, new(InteractionAction.ReturnAccessory)); }
    Require(c.Trust < before && c.Id == identity && c.Interaction.Accessory.Id == accessory, "return erased theft or identity");
    Require(c.Interaction.Accessory.Losses == 5, "missing loss history");
});
Check("repair respects partial work and destroyed items", () =>
{
    var sim = Fresh(); var c = sim.Selected!;
    sim.ExecuteInteraction(c.Id, new(InteractionAction.TakeAccessoryCare,BodyPart.Accessory));
    sim.ExecuteInteraction(c.Id, new(InteractionAction.DamageAccessory));
    double damaged = c.Interaction.Accessory.Integrity;
    Gesture(sim, c, InteractionAction.RepairAccessory, seconds: 1, part: BodyPart.Accessory, cancelled: true);
    Require(c.Interaction.Accessory.Integrity > damaged && c.Interaction.Accessory.Integrity < 100, "partial repair lost");
    c.Interaction.Accessory.Integrity = 0;
    Require(!sim.BeginInteractionGesture(c.Id, new(InteractionAction.RepairAccessory)).Success, "destroyed item silently repaired");
});
Check("hand hold releases but fixed restraints require an explicit release", () =>
{
    var sim = Fresh(); var c = sim.Selected!;
    Gesture(sim, c, InteractionAction.Hold);
    Require(c.Interaction.Restraint == RestraintKind.None, "hand restraint stuck");
    sim.ExecuteInteraction(c.Id, new(InteractionAction.Pin)); sim.EndInteractionGesture(true);
    Require(c.Interaction.Restraint == RestraintKind.Frame, "cancel erased physical fixture");
    Require(sim.ExecuteInteraction(c.Id, new(InteractionAction.Release)).Success, "cannot remove fixture");
});
Check("cancelled carry stays supported, deliberate drop has a trajectory", () =>
{
    var sim = Fresh(); var c = sim.Selected!;
    sim.BeginInteractionGesture(c.Id, new(InteractionAction.Lift, Height: 2)); sim.EndInteractionGesture(true);
    Require(c.Interaction.Carried && !c.Interaction.Falling, "cancel dropped a supported creature");
    sim.ExecuteInteraction(c.Id, new(InteractionAction.Drop));
    Require(c.Interaction.Falling, "explicit drop not initiated");
    Step(sim, 2);
    Require(!c.Interaction.Falling && c.Interaction.Height == 0 && c.Health < 100, "drop has no persistent result");
});
Check("foot injury persists through feeding and removal of restraints", () =>
{
    var sim = Fresh(); var c = sim.Selected!;
    Gesture(sim, c, InteractionAction.BurnFeet, 8, part: BodyPart.Bottom, force: .8);
    Require(c.Interaction.FootIntegrity <= 25 && !WorldSimulation.CanSelfMove(c), "foot injury does not limit motion");
    double feet = c.Interaction.FootIntegrity;
    sim.ExecuteInteraction(c.Id, new(InteractionAction.Pin)); sim.ExecuteInteraction(c.Id, new(InteractionAction.Release));
    Gesture(sim, c, InteractionAction.FeedMeal, part: BodyPart.Mouth);
    Require(c.Interaction.FootIntegrity == feet && !WorldSimulation.CanSelfMove(c), "food or release healed feet");
});
Check("injured creature can eat nearby food but cannot teleport to a distant plate", () =>
{
    var sim = Fresh(); var c = sim.Selected!; c.Interaction.FootIntegrity = 10; c.Fullness = 30;
    sim.ExecuteInteraction(c.Id, new(InteractionAction.PlaceMeal, X: c.X + 3, Z: c.Z));
    double x = c.X, z = c.Z; Step(sim, 6);
    Require(c.X == x && c.Z == z && c.Fullness < 30, "disabled creature reached distant food");
    sim.ExecuteInteraction(c.Id, new(InteractionAction.PlaceMeal, X: c.X + .5, Z: c.Z));
    Step(sim, 1);
    Require(c.Fullness > 40 && c.Interaction.FootIntegrity == 10, "nearby food inaccessible or heals injury");
});
Check("care begins a real recovery period, never instant foot repair", () =>
{
    var sim = Fresh(); var c = sim.Selected!; c.Interaction.FootIntegrity = 20; c.Pain = 30;
    Gesture(sim, c, InteractionAction.Care, part: BodyPart.Bottom);
    Require(c.Interaction.FootTreated && c.Interaction.FootIntegrity == 20, "care instantly repaired function");
    Step(sim, 90);
    Require(c.Interaction.FootIntegrity > 25 && WorldSimulation.CanSelfMove(c), "recovery never regains movement");
});
Check("close-up freezes passive healing and time without a catch-up", () =>
{
    var sim = Fresh(); var c = sim.Selected!;
    c.Interaction.FootIntegrity = 20; c.Interaction.FootTreated = true; c.Interaction.FootRecovery = 5;
    sim.State.Mode = PlayMode.CloseUp; double age = c.InnerAgeSeconds;
    Step(sim, 20);
    Require(sim.State.WorldSeconds == 0 && c.InnerAgeSeconds == age && c.Interaction.FootRecovery == 5, "close-up advances passive time");
    sim.State.Mode = PlayMode.Management; Step(sim, .5);
    Require(sim.State.WorldSeconds <= .51 && c.Interaction.FootIntegrity == 20, "exit replayed missed time");
});
Check("bottom injury does not automatically disable flight or a different body", () =>
{
    var sim = Fresh(); var c = sim.Selected!; c.Body.SpeciesId = "remilia"; c.Interaction.FootIntegrity = 0;
    Require(WorldSimulation.CanSelfMove(c), "flying species uses ground foot gate");
    c.Body.SpeciesId = "reimu"; c.Body.UpgradeRoute = "body";
    Require(WorldSimulation.CanSelfMove(c), "body route uses unrelated foot gate");
});
Check("training only learns the named skill after real action", () =>
{
    var sim = Fresh(); var c = sim.Selected!; double guard = c.GuardSkill, sharing = c.SharingSkill;
    sim.ExecuteInteraction(c.Id, new(InteractionAction.TeachWait));
    Require(c.Interaction.Learning[LearnedCue.Wait].Familiarity == 0, "click itself counts as learning");
    Step(sim, 4);
    Require(c.GuardSkill == guard && c.SharingSkill == sharing && !c.Interaction.Learning.ContainsKey(LearnedCue.Follow), "unrelated skills increased");
});
Check("stationary follow does not pretend the learner followed", () =>
{
    var sim = Fresh(); var c = sim.Selected!; sim.State.PlayerX = c.X; sim.State.PlayerZ = c.Z;
    sim.ExecuteInteraction(c.Id, new(InteractionAction.TeachFollow));
    c.Interaction.CueUnderstood = true; Step(sim, 21);
    Require(c.Interaction.Learning[LearnedCue.Follow].Successes == 0, "no movement still counted as successful follow");
});
Check("dialogue expresses needs without leaking hidden counters", () =>
{
    var c = Fresh().Selected!;
    Require(OilDialogue.Say(c, "cannot-walk").Contains("脚脚"), "missing embodied speech");
    foreach (string key in new[] { "stroke", "nuzzle", "hurt", "accessory-taken", "recovery" })
    {
        string line = OilDialogue.Say(c, key);
        Require(!line.Contains("信任值") && !line.Contains("等级") && !line.Contains("+"), "speech leaks simulation stats");
    }
});
Check("save-load preserves injury accessory memory and does not replay gestures", () =>
{
    var sim = Fresh(); var c = sim.Selected!;
    Gesture(sim, c, InteractionAction.BurnFeet, 8, part: BodyPart.Bottom, force: .8);
    sim.ExecuteInteraction(c.Id, new(InteractionAction.TakeAccessory,BodyPart.Accessory));
    sim.ExecuteInteraction(c.Id, new(InteractionAction.Box));
    var directory = Path.Combine(Path.GetTempPath(), "Yukkuri-Interaction-" + Guid.NewGuid().ToString("N"));
    var store = new SaveStore(directory); store.Save(sim.State, 1);
    var loaded = new WorldSimulation(store.Load(1).State); var restored = loaded.Selected!;
    Require(restored.Interaction.FootIntegrity == c.Interaction.FootIntegrity &&
        restored.Interaction.Restraint == RestraintKind.Box && restored.Interaction.Accessory.Place == AccessoryPlace.Hand &&
        restored.Interaction.Memories.ContainsKey("fire") && !loaded.HasInteractionGesture, "persistent state missing or active gesture replayed");
});
Check("corrupt functional state rejected at the real save entry", () =>
{
    var sim = Fresh(); sim.Selected!.Interaction.FootIntegrity = double.NaN;
    bool rejected = false; try { SaveStore.Validate(sim.State); } catch (InvalidDataException) { rejected = true; }
    Require(rejected, "NaN state accepted");
});

Check("time beside the mouth cannot pre-charge the next bite", () =>
{
    var sim=Fresh();var c=sim.Selected!;double before=c.Fullness;
    var away=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Cheek,Duration:.1);
    sim.BeginInteractionGesture(c.Id,away);
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(away,true);
    sim.ContinueInteractionGesture(away with { Part=BodyPart.Mouth },true);
    Require(c.Fullness==before,"food consumed on first mouth frame after wrong-part hold");
});
Check("leaving the mouth interrupts readiness to take food", () =>
{
    var sim=Fresh();var c=sim.Selected!;double before=c.Fullness;
    var mouth=new InteractionInput(InteractionAction.FeedMeal,BodyPart.Mouth,Duration:.1);
    sim.BeginInteractionGesture(c.Id,mouth);
    for(int n=0;n<5;n++)sim.ContinueInteractionGesture(mouth,true);
    sim.ContinueInteractionGesture(mouth,false);
    for(int n=0;n<2;n++)sim.ContinueInteractionGesture(mouth,true);
    Require(c.Fullness==before,"disconnected contacts incorrectly accumulated a bite");
});
Check("repair touches the removed accessory rather than any body point", () =>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.Accessory.Place=AccessoryPlace.Hand;c.Interaction.Accessory.Integrity=40;
    Gesture(sim,c,InteractionAction.RepairAccessory,part:BodyPart.Cheek);
    Require(c.Interaction.Accessory.Integrity==40,"repairing cheek repaired remote accessory");
});
Check("a stop can interrupt an unfinished follow without awarding completion", () =>
{
    var sim=Fresh();var c=sim.Selected!;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.TeachFollow));
    var response=sim.ExecuteInteraction(c.Id,new(InteractionAction.Stop));
    Require(response.Success&&c.Interaction.ActiveCue==LearnedCue.Stop&&!c.Interaction.FollowPlayer,"stop could not cancel follow");
    Require(c.Interaction.Learning[LearnedCue.Follow].Successes==0,"interrupted follow received success");
});
Check("closed box does not let an oil eat through its back wall", () =>
{
    var sim=Fresh();var c=sim.Selected!;c.Fullness=30;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Box));
    sim.ExecuteInteraction(c.Id,new(InteractionAction.PlaceMeal,X:c.X,Z:c.Z-.8));
    Step(sim,.5);Require(c.Fullness<30,"ate food outside the closed rear wall");
});
Check("repeated greetings leave a real response window", () =>
{
    var sim=Fresh();var c=sim.Selected!;sim.ExecuteInteraction(c.Id,new(InteractionAction.Greet));double after=c.Trust;
    for(int n=0;n<30;n++)sim.ExecuteInteraction(c.Id,new(InteractionAction.Greet));
    Require(c.Trust==after,"button spam increased trust while response was still playing");
});

Check("putting down cannot intersect another oil or silently discard support",()=>
{
    var sim=Fresh();var c=sim.Selected!;var other=sim.State.Creatures[1];
    sim.BeginInteractionGesture(c.Id,new(InteractionAction.Lift));sim.EndInteractionGesture();
    var result=sim.ExecuteInteraction(c.Id,new(InteractionAction.PutDown,X:other.X,Z:other.Z));
    Require(!result.Success&&c.Interaction.Carried,"occupied landing accepted or support lost");
    Require(!sim.CanPlaceInteractionAt(c.Id,other.X,other.Z),"landing preview disagrees with the placement rule");
});
Check("food seeking does not merge multiple bodies",()=>
{
    var sim=Fresh();foreach(var c in sim.State.Creatures){c.Fullness=10;c.Interaction.NextAutonomyAt=10000;}
    sim.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Meal,X=0,Z=0,Portions=100});
    double nearest=double.MaxValue;
    for(int n=0;n<200;n++)
    {
        Step(sim,.1);
        for(int a=0;a<sim.State.Creatures.Count;a++)for(int b=a+1;b<sim.State.Creatures.Count;b++)
        {
            var first=sim.State.Creatures[a];var second=sim.State.Creatures[b];
            nearest=Math.Min(nearest,Math.Sqrt(Math.Pow(first.X-second.X,2)+Math.Pow(first.Z-second.Z,2)));
        }
    }
    Require(nearest>.92,"food seeking merged bodies; minimum distance="+nearest);
});
Check("pushing into a fixed box does not pass through the occupant",()=>
{
    var sim=Fresh();var c=sim.Selected!;var boxed=sim.State.Creatures[1];
    c.X=-2;c.Z=0;boxed.X=0;boxed.Z=0;sim.ExecuteInteraction(boxed.Id,new(InteractionAction.Box));
    double bx=boxed.X,bz=boxed.Z;
    c.Interaction.VelocityX=12;
    for(int n=0;n<10;n++)sim.AdvanceInteractionPresentation(.1);
    Require(c.X<-.9&&boxed.X==bx&&boxed.Z==bz,"push penetrated or moved the fixed box");
});

Check("wiping a cheek leaves dirt elsewhere and preserves local progress",()=>
{
    var sim=Fresh();var c=sim.Selected!;double before=c.Interaction.DirtAt(BodyPart.Bottom);
    Gesture(sim,c,InteractionAction.Clean,part:BodyPart.Cheek);
    Require(c.Interaction.DirtAt(BodyPart.Cheek)<18&&c.Interaction.DirtAt(BodyPart.Bottom)==before,"wipe affected the whole body");
    var loaded=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    Require(loaded.Creatures[0].Interaction.DirtAt(BodyPart.Cheek)==c.Interaction.DirtAt(BodyPart.Cheek),"local wipe lost in save");
});
Check("hanging adjustment persists after cancellation without becoming a drop",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Hang));
    var input=new InteractionInput(InteractionAction.AdjustHanging,BodyPart.Side,.3,.1,.1,c.X-.5,c.Z,1.1);
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"adjustment not available");
    sim.ContinueInteractionGesture(input,true);sim.EndInteractionGesture(true);
    Require(c.Interaction.Restraint==RestraintKind.Hanging&&!c.Interaction.Falling&&c.Interaction.Height==1.1,"cancel discarded the supporting fixture");
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.Lower)).Success&&c.Interaction.Carried&&!c.Interaction.Falling,"lower does not transfer support to the hand");
    for(int n=0;n<20;n++)sim.AdvanceInteractionPresentation(.1);
    Require(c.Interaction.Height==0&&!c.Interaction.Carried,"supported lower did not finish on the ground");
});
Check("idle standing cannot be sold as a learned stop",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    Require(!sim.ExecuteInteraction(c.Id,new(InteractionAction.TeachStop)).Success,"idle stop lesson accepted");
    Require(!c.Interaction.Learning.ContainsKey(LearnedCue.Stop),"idle lesson gained evidence");
});
Check("active teasing permits an actual chase in normal time",()=>
{
    var sim=Fresh();var c=sim.Selected!;double start=c.X;
    var input=new InteractionInput(InteractionAction.Tease,BodyPart.Mouth,.3,.1,.02,c.X-1.5,c.Z);
    sim.BeginInteractionGesture(c.Id,input);
    for(int n=0;n<20;n++){sim.ContinueInteractionGesture(input,true);Step(sim,.1);}
    Require(c.X<start-.3,"teasing gesture froze the oil instead of allowing pursuit");
});
Check("voices differ by individual and never promise impossible movement",()=>
{
    var sim=Fresh();var shy=sim.State.Creatures[0];var proud=sim.State.Creatures[1];var young=sim.State.Creatures[2];
    Require(new[]{OilDialogue.Say(shy,"learn"),OilDialogue.Say(proud,"learn"),OilDialogue.Say(young,"learn")}.Distinct().Count()==3,"individual voices are identical");
    shy.Interaction.FootIntegrity=10;
    for(int n=0;n<6;n++)Require(!OilDialogue.Say(shy,"accessory-high").Contains("跳"),"immobile creature speaks a jumping attempt");
    for(int n=0;n<6;n++)Require(!OilDialogue.Say(shy,"unwrap").Contains("可以动"),"unwrapping claims to restore movement");
});

Check("instant contact targets the named anatomical part",()=>
{
    foreach(var action in new[]{InteractionAction.Flick,InteractionAction.Slap,InteractionAction.Poke,InteractionAction.TakeAccessory})
    {
        var sim=Fresh();var c=sim.Selected!;double health=c.Health,trust=c.Trust;
        var result=sim.ExecuteInteraction(c.Id,new(action,BodyPart.Bottom));
        Require(!result.Success&&c.Health==health&&c.Trust==trust&&c.Interaction.HasAccessory,"wrong body part executed "+action);
    }
});
Check("head care cannot secretly treat a damaged foot",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.FootIntegrity=20;c.Interaction.FootTreated=false;
    sim.State.UnlimitedResources=false;int medicine=sim.State.Medicine;
    Gesture(sim,c,InteractionAction.Care,part:BodyPart.Head);
    Require(!c.Interaction.FootTreated&&sim.State.Medicine==medicine,"head contact treated the foot or consumed a foot dose");
    c.Pain=30;
    var head=new InteractionInput(InteractionAction.Care,BodyPart.Head,Duration:.1);
    sim.BeginInteractionGesture(c.Id,head);
    for(int n=0;n<15;n++)sim.ContinueInteractionGesture(head,true);
    sim.ContinueInteractionGesture(head with{Part=BodyPart.Bottom},true);
    Require(!c.Interaction.FootTreated&&sim.State.Medicine==medicine,"time at a different wound pre-charged foot care");
    for(int n=0;n<16;n++)sim.ContinueInteractionGesture(head with{Part=BodyPart.Bottom},true);
    Require(c.Interaction.FootTreated&&sim.State.Medicine==medicine-1,"local foot care never completed");
    sim.EndInteractionGesture();
});
Check("hands remain committed to one supported oil until it is set down",()=>
{
    var sim=Fresh();var first=sim.Selected!;var second=sim.State.Creatures[1];
    sim.BeginInteractionGesture(first.Id,new(InteractionAction.Lift));sim.EndInteractionGesture(true);
    var result=sim.BeginInteractionGesture(second.Id,new(InteractionAction.Lift));
    Require(!result.Success&&first.Interaction.Carried&&!second.Interaction.Carried,"another oil was lifted while both hands still supported the first");
});
Check("a missing treasure remains visible in the expression after speech ends",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.TakeAccessory,BodyPart.Accessory));
    for(int n=0;n<100;n++)sim.AdvanceInteractionPresentation(.1);
    Require(c.Expression.ToString()=="Worried","missing accessory reverted to the ordinary smiling face");
    Require(sim.State.WorldSeconds==0,"presentation advanced passive time");
});
Check("permitted accessory care waits differently from theft",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Trust=70;c.Fear=0;
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.TakeAccessoryCare,BodyPart.Accessory)).Success,"care loan refused");
    c.Interaction.NextAutonomyAt=0;Step(sim,1);
    Require(c.Interaction.NextAutonomyAt>0,"autonomy observation did not advance to its next tick");
    Require(c.Expression==Expression.Curious && (c.Speech.Contains("慢慢整理")||c.Speech.Contains("等")||c.Speech.Contains("轻轻")||c.Speech.Contains("整理好了")),"a permitted loan was treated as a theft: "+c.Expression+" / "+c.Speech);
    sim.ExecuteInteraction(c.Id,new(InteractionAction.DamageAccessory,BodyPart.Accessory));
    for(int n=0;n<100;n++)sim.AdvanceInteractionPresentation(.1);
    Require(c.Expression.ToString()=="Worried","damage to a borrowed item did not change the lasting response");
    var loaded=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    Require(loaded.Creatures[0].Interaction.Accessory.WasStolen,"betraying a care loan lost the history on save");
});
int failures = results.Count(r => !(bool)r.GetType().GetProperty("passed")!.GetValue(r)!);
var report = new { at = DateTimeOffset.Now, scope = "Simulation-level first-batch interaction checks; not GUI or user acceptance", success = failures == 0, failures, results };
if (args.Length > 0) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!); File.WriteAllText(args[0], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }
Console.WriteLine($"Interaction checks: {results.Count - failures}/{results.Count} passed");
return failures == 0 ? 0 : 1;

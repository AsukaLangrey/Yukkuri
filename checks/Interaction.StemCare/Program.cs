using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation sim)=>JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(Snapshot(sim),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void Frame(WorldSimulation sim,double dt=.1)
{sim.AdvanceInteractionPresentation(dt);sim.Advance(dt);}
WorldSimulation Fresh(ulong seed=20261031)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(seed);
    // Component fixture: clear scenery, keep all three real identities and roles.
    sim.State.RoomObjects.Clear();
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.X=-2+n*2;c.Z=0;c.TargetX=c.X;c.TargetZ=c.Z;
        c.Fullness=90;c.Energy=90;c.Interaction.Hydration=90;
        c.Family.NextCareAt=10000;c.Family.NextSocialAt=10000;c.Interaction.NextAutonomyAt=10000;
    }
    return sim;
}
WorldSimulation Brood(double elapsed=0)
{
    // Explicit existing-pregnancy/legacy-save fixture, not an affection journey.
    // Real entry points are checked separately below without fabricating their IDs.
    var sim=Fresh();var c=sim.Selected!;
    c.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,
        FatherId=sim.State.Creatures[1].Id,EventId="stem-fixture-1",PlannedChildren=2,Elapsed=elapsed};
    return Reload(sim);
}
InteractionInput Input(WorldSimulation sim,InteractionAction action,double duration=.1,double force=0)=>
    new(action,BodyPart.Head,Force:force,Duration:duration,ObjectId:sim.Selected!.Family.Pregnancy.EventId,TargetKind:InteractionTargetKind.Stem);
void Support(WorldSimulation sim)
{
    var input=Input(sim,InteractionAction.SupportStem);
    Require(sim.BeginInteractionGesture(sim.Selected!.Id,input).Success,"support did not begin");
    for(int n=0;n<16;n++)Require(sim.ContinueInteractionGesture(input,true).Success,"support did not continue");
}

Check("default target stays body; every unsupported stem tool refuses without body fallback",()=>
{
    var sim=Brood();var c=sim.Selected!;
    Require(new InteractionInput(InteractionAction.Punch).TargetKind==InteractionTargetKind.Body,"legacy input target changed");
    foreach(var action in Enum.GetValues<InteractionAction>().Where(a=>a is not
        (InteractionAction.SupportStem or InteractionAction.Punch or InteractionAction.Burn or InteractionAction.CutStem)))
    {
        var before=Snapshot(sim);Require(!sim.ExecuteInteraction(c.Id,Input(sim,action)).Success,"unsupported tool accepted: "+action);
        Require(Snapshot(sim)==before,"unsupported stem tool mutated body: "+action);
    }
    Require(!sim.BeginInteractionGesture(c.Id,new(InteractionAction.SupportStem)).Success,"body accepted stem support");
});

Check("support requires uninterrupted contact; pause cannot complete it",()=>
{
    var sim=Brood();var c=sim.Selected!;var input=Input(sim,InteractionAction.SupportStem);int stock=sim.State.Medicine;
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"begin failed");
    for(int n=0;n<15;n++)sim.ContinueInteractionGesture(input,true);
    Require(sim.State.Medicine==stock,"charged before 1.6 seconds");
    sim.ContinueInteractionGesture(input,false);
    Require(sim.InteractionGestureTarget==null&&!sim.InteractionGestureOwnsPose,"lost contact kept mother reserved");
    for(int n=0;n<15;n++)sim.ContinueInteractionGesture(input,true);
    Require(sim.State.Medicine==stock,"gap failed to reset contact");
    sim.State.Settings.Pause=true;var before=Snapshot(sim);
    Require(!sim.ContinueInteractionGesture(input,true).Success&&!sim.HasInteractionGesture,"paused gesture remained active");
    Require(Snapshot(sim)==before,"pause changed stored state");
    sim.State.Settings.Pause=false;Support(sim);
    Require(sim.State.Medicine==stock-1,"fresh complete support did not charge once");
});

Check("gesture identity cannot change to body, another brood or an expired event",()=>
{
    foreach(bool body in new[]{false,true})
    {
        var sim=Brood();var c=sim.Selected!;var input=Input(sim,InteractionAction.Burn);
        sim.BeginInteractionGesture(c.Id,input);var before=Snapshot(sim);
        var changed=body?input with{TargetKind=InteractionTargetKind.Body}:input with{ObjectId="another-brood"};
        Require(!sim.ContinueInteractionGesture(changed,true).Success&&!sim.HasInteractionGesture,"changed identity accepted");
        Require(Snapshot(sim)==before,"changed identity inflicted damage");
    }
    var old=Brood();var id=Input(old,InteractionAction.SupportStem);
    old.Selected!.Family.Pregnancy.Phase=PregnancyPhase.Recovering;
    Require(!old.BeginInteractionGesture(old.Selected.Id,id).Success,"expired brood accepted");
});

Check("unlimited money does not create medicine; one completed hold never charges twice or locks mother",()=>
{
    var sim=Brood();sim.State.UnlimitedResources=true;sim.State.Medicine=0;
    Require(!sim.StemCareStatus(sim.Selected!.Id)!.CanSupport&&!sim.BeginInteractionGesture(sim.Selected.Id,Input(sim,InteractionAction.SupportStem)).Success,
        "unlimited resources bypassed real medicine");
    sim.State.Medicine=2;Support(sim);var stem=sim.Selected.Family.Pregnancy.Stem!;
    Require(sim.State.Medicine==1&&stem.SupportRemaining==60&&sim.HasInteractionGesture&&
        !sim.InteractionGestureOwnsPose&&sim.InteractionGestureTarget==null,"support did not release active ownership");
    for(int n=0;n<100;n++)sim.ContinueInteractionGesture(Input(sim,InteractionAction.SupportStem),true);
    Require(sim.State.Medicine==1&&stem.SupportRemaining==60,"held support repeated transaction");
    sim.EndInteractionGesture();Require(!sim.BeginInteractionGesture(sim.Selected.Id,Input(sim,InteractionAction.SupportStem)).Success,"cooldown bypassed by a new gesture");
});

Check("stem punch and burn damage brood without head damage or maternal Hits; equal contact time is equal damage",()=>
{
    var sim=Brood();var c=sim.Selected!;double health=c.Health,pain=c.Pain,trust=c.Trust;
    string injuries=JsonSerializer.Serialize(c.Interaction.Injuries,SaveStore.JsonOptions);
    string caution=JsonSerializer.Serialize(c.Interaction.HumanBond.Caution,SaveStore.JsonOptions);int hits=c.Interaction.HitCount;
    Require(sim.ExecuteInteraction(c.Id,Input(sim,InteractionAction.Punch,force:1)).Success,"punch refused");
    Require(c.Family.Pregnancy.Stem!.Integrity==80&&c.Health==health&&c.Pain==pain&&c.Interaction.HitCount==hits&&
        JsonSerializer.Serialize(c.Interaction.Injuries,SaveStore.JsonOptions)==injuries&&
        JsonSerializer.Serialize(c.Interaction.HumanBond.Caution,SaveStore.JsonOptions)==caution,"stem hit was treated as maternal body injury");
    Require(c.Trust<trust&&c.Interaction.Emotion.Alarm>0&&c.Interaction.HumanBond.LastHarmAt==sim.State.WorldSeconds,"real human threat had no continuity");
    double BurnAt(double dt,int samples)
    {
        var s=Brood();var input=Input(s,InteractionAction.Burn,dt,1);s.BeginInteractionGesture(s.Selected!.Id,input);
        for(int n=0;n<samples;n++)s.ContinueInteractionGesture(input,true);
        Require(s.Selected.Health==100&&s.Selected.Interaction.HitCount==0,"burn damaged maternal body");
        return s.Selected.Family.Pregnancy.Stem!.Integrity;
    }
    Require(Math.Abs(BurnAt(.1,20)-BurnAt(.02,100))<1e-8,"burn damage depends on sample count");
});

Check("finite support cushions transmitted body force but does not erase its real cause",()=>
{
    var plain=Brood();var supported=Brood();Support(supported);supported.EndInteractionGesture();
    plain.Hurt(plain.Selected!,10,"测试外力","player",BodyPart.Side);
    supported.Hurt(supported.Selected!,10,"测试外力","player",BodyPart.Side);
    var a=plain.Selected!;var b=supported.Selected!;
    Require(Math.Abs(a.Family.Pregnancy.Stem!.Integrity-96.5)<1e-8&&Math.Abs(b.Family.Pregnancy.Stem!.Integrity-98.6)<1e-8,
        "support failed to attenuate only transmitted damage");
    Require(a.Health==90&&b.Health==90&&b.Family.Pregnancy.Stem.LastDamageCause.Contains("测试外力")&&
        b.Family.Pregnancy.Stem.LastDamageActorId=="player","maternal injury or stem cause lost");
});

Check("only ordinary biological time decays support and adverse conditions continuously damage brood",()=>
{
    var sim=Brood();Support(sim);sim.EndInteractionGesture();var c=sim.Selected!;var stem=c.Family.Pregnancy.Stem!;
    double elapsed=c.Family.Pregnancy.Elapsed;sim.State.Settings.Pause=true;
    for(int n=0;n<20;n++)Frame(sim);
    Require(stem.SupportRemaining==60&&c.Family.Pregnancy.Elapsed==elapsed,"pause advanced stem");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;
    for(int n=0;n<20;n++)Frame(sim);
    Require(stem.SupportRemaining==60&&c.Family.Pregnancy.Elapsed==elapsed,"close-up advanced pregnancy");
    sim.State.Mode=PlayMode.Management;c.Fullness=0;c.Interaction.Hydration=0;
    for(int n=0;n<10;n++)Frame(sim);
    Require(stem.SupportRemaining<60&&stem.Hydration<100&&stem.Integrity<100&&stem.LastDamageCause.Contains("失养"),"lack of maternal intake did not affect brood");
});

Check("completed held support permits original mature delivery exactly once",()=>
{
    var sim=Brood(238);var c=sim.Selected!;string id=c.Family.Pregnancy.EventId!;int planned=c.Family.Pregnancy.PlannedChildren;
    Support(sim);int stock=sim.State.Medicine;
    for(int n=0;n<100&&!sim.State.CompletedBirths.Contains(id);n++)Frame(sim);
    Require(sim.State.CompletedBirths.Contains(id)&&sim.State.Creatures.Count(o=>o.Family.BirthEventId==id)==planned,
        "completed hold prevented original birth or changed planned litter");
    Require(c.Family.Pregnancy.Phase==PregnancyPhase.Recovering,"birth skipped maternal recovery");
    var input=new InteractionInput(InteractionAction.SupportStem,ObjectId:id,TargetKind:InteractionTargetKind.Stem);
    Require(!sim.ContinueInteractionGesture(input,true).Success&&sim.State.Medicine==stock,"old hold charged after birth");
    var copy=Reload(sim);for(int n=0;n<20;n++)Frame(copy);
    Require(copy.State.Creatures.Count(o=>o.Family.BirthEventId==id)==planned&&copy.State.CompletedBirths.Count==1,"reload repeated birth");
});

Check("immersion damages supported stems and cold storage uses the existing biological clock",()=>
{
    var wet=Brood();Support(wet);wet.EndInteractionGesture();var c=wet.Selected!;
    wet.State.RoomObjects.Add(new(){Kind=RoomObjectKind.RiverWater,RegionId=c.RegionId,X=7,Z=0});
    // A declared already-submerged, restrained boundary fixture. Actual water
    // damage still comes only from real environmental checks and ordinary time.
    c.X=7.1;c.Z=0;c.TargetX=c.X;c.TargetZ=c.Z;c.Interaction.Restraint=RestraintKind.Frame;c.Interaction.Supine=true;
    Require(WaterEnvironment.IsInWater(wet.State,c),"water fixture is not physically immersed");
    for(int n=0;n<10;n++)Frame(wet);
    Require(Math.Abs(c.Family.Pregnancy.Stem!.Integrity-98.8)<1e-7&&c.Family.Pregnancy.Stem.LastDamageCause.Contains("浸水"),
        "support incorrectly shielded immersion or water damage used sample count");
    Require(!wet.StemCareStatus(c.Id)!.CanSupport,"underwater stem offered ordinary support");
    double damage=c.Family.Pregnancy.Stem.Integrity;
    Require(wet.ExecuteInteraction(c.Id,new(InteractionAction.Release)).Success&&wet.RescueFromWater(c.Id).Success,
        "actual release and rescue could not recover mother");
    Require(!WaterEnvironment.IsInWater(wet.State,c)&&c.Family.Pregnancy.Stem.Integrity==damage,
        "rescue erased existing brood damage");
    var cold=Brood();Support(cold);cold.EndInteractionGesture();var m=cold.Selected!;
    cold.SetColdStorage(m.Id,true);Require(m.Family.ColdStorage.Stored,"cold storage refused fixture");
    double before=m.Family.Pregnancy.Elapsed;for(int n=0;n<10;n++)Frame(cold);
    Require(Math.Abs(m.Family.Pregnancy.Elapsed-before-.1)<1e-7&&Math.Abs(m.Family.Pregnancy.Stem!.SupportRemaining-59.9)<1e-7,
        "stem diverged from slowed biological time");
    Require(!cold.StemCareStatus(m.Id)!.CanSupport,"cold mother remained touchable");
});

Check("brood failure records cause and recovery without premature children; death ends visible brood",()=>
{
    var sim=Brood();var c=sim.Selected!;string id=c.Family.Pregnancy.EventId!;
    for(int n=0;n<5;n++)sim.ExecuteInteraction(c.Id,Input(sim,InteractionAction.Punch,force:1));
    Require(c.Family.Pregnancy.Phase==PregnancyPhase.Recovering&&c.Family.Pregnancy.RecoveryRemaining>0&&
        c.Family.Pregnancy.Outcome.Contains("击打外茎")&&sim.StemCareStatus(c.Id)==null&&
        !sim.State.CompletedBirths.Contains(id)&&!sim.State.Creatures.Any(o=>o.Family.BirthEventId==id),"failed brood became children or lost cause");
    var copy=Reload(sim);Require(copy.Selected!.Family.Pregnancy.Outcome==c.Family.Pregnancy.Outcome,"reload lost failure cause");
    var dying=Brood();dying.Hurt(dying.Selected!,300,"测试致死外力",null,BodyPart.Side);
    // Actual lethal outcome can start at critical; the existing lethal follow-up consumes its window.
    if(dying.Selected!.Condition!=PhysicalCondition.Dead)dying.Hurt(dying.Selected,300,"测试致死外力",null,BodyPart.Side);
    Require(dying.Selected.Condition==PhysicalCondition.Dead&&dying.StemCareStatus(dying.Selected.Id)==null&&
        dying.Selected.Family.Pregnancy.Outcome.Length>0,"dead mother retained an active visible brood");
});

Check("legacy normalization preserves identity and clock; read-only status and reload preserve damaged state",()=>
{
    var sim=Brood(37);var c=sim.Selected!;var p=c.Family.Pregnancy;
    Require(p.EventId=="stem-fixture-1"&&p.Elapsed==37&&p.PlannedChildren==2&&p.Stem!.EventId==p.EventId,"legacy initialization reset existing pregnancy");
    sim.ExecuteInteraction(c.Id,Input(sim,InteractionAction.Punch));
    string before=Snapshot(sim);for(int n=0;n<20;n++)sim.StemCareStatus(c.Id);
    Require(Snapshot(sim)==before,"read-only status mutated persistent state");
    var copy=Reload(sim);Require(copy.Selected!.Family.Pregnancy.Stem!.Integrity==p.Stem.Integrity&&
        copy.Selected.Family.Pregnancy.Stem.LastDamageCause==p.Stem.LastDamageCause,"reload reset brood damage");
    copy.Selected.Family.Pregnancy.Stem.Hydration=double.NaN;
    bool rejected=false;try{SaveStore.Validate(copy.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"invalid numeric stem state accepted");
    var wrong=Brood();wrong.Selected!.Family.Pregnancy.Stem!.EventId="another-pregnancy";
    rejected=false;try{SaveStore.Validate(wrong.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected&&wrong.StemCareStatus(wrong.Selected.Id)==null,"cross-pregnancy stem identity accepted");
    var exhausted=Brood();exhausted.Selected!.Family.Pregnancy.Stem!.Integrity=0;
    rejected=false;try{SaveStore.Validate(exhausted.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected&&exhausted.StemCareStatus(exhausted.Selected.Id)==null,"zero-integrity active brood was accepted or visible");
    Frame(exhausted);
    Require(exhausted.Selected.Family.Pregnancy.Phase==PregnancyPhase.Recovering&&
        exhausted.Selected.Family.Pregnancy.Stem.Integrity==0&&!exhausted.State.CompletedBirths.Any(),"zero brood regrew or delivered");
});

Check("ordinary and forced real preparation both initialize the chosen brood without invented event IDs",()=>
{
    var ordinary=Fresh();var mother=ordinary.Selected!;var father=ordinary.State.Creatures[1];
    // Established willing partners are a declared entry fixture; preparation,
    // identity generation, count choice and the six-second contact are real.
    mother.Family.Peers[father.Id]=new(){Familiarity=80,Affection=80};
    father.Family.Peers[mother.Id]=new(){Familiarity=80,Affection=80};
    ordinary.RequestPregnancy(mother.Id,father.Id,BirthMethod.Stem);
    Require(mother.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"ordinary preparation did not start");
    for(int n=0;n<600&&mother.Family.Pregnancy.Phase==PregnancyPhase.Preparing;n++)Frame(ordinary);
    Require(mother.Family.Pregnancy.Phase==PregnancyPhase.Gestating&&mother.Family.Pregnancy.Stem?.EventId==mother.Family.Pregnancy.EventId&&
        mother.Family.Pregnancy.PlannedChildren is 2 or 3,"ordinary entry failed to initialize brood");
    bool found=false;
    for(ulong seed=1;seed<=64&&!found;seed++)
    {
        var forced=Fresh(seed);var m=forced.Selected!;var f=forced.State.Creatures[1];
        forced.CollectBreedingMaterial(f.Id);forced.RequestForcedPregnancy(m.Id,f.Id,BirthMethod.Stem);
        Require(FamilyRules.UnderIntervention(m),"forced preparation did not start");
        for(int n=0;n<45&&FamilyRules.UnderIntervention(m);n++)Frame(forced);
        if(m.Family.Pregnancy.Phase!=PregnancyPhase.Gestating)continue;
        Require(m.Family.Pregnancy.Stem?.EventId==m.Family.Pregnancy.EventId&&m.Family.Pregnancy.Stem!.Integrity==100,
            "forced entry failed to initialize brood after real maternal intervention damage");
        SaveStore.Validate(forced.State);found=true;
    }
    Require(found,"fixed seed set did not reach any real forced gestation branch");SaveStore.Validate(ordinary.State);
});

return failures==0?0:1;

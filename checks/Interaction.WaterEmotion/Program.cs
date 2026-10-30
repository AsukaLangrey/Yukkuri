using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Check(string name,Action test)
{
    try{test();Console.WriteLine("PASS "+name);}
    catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}
}
void Require(bool value,string why){if(!value)throw new Exception(why);}
WorldSimulation Fresh()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    sim.State.Creatures.RemoveAll(o=>o.Id!=c.Id);
    sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
    c.X=0;c.Z=0;c.TargetX=0;c.TargetZ=0;c.Fullness=90;c.Energy=90;
    c.Interaction.Hydration=90;c.Interaction.NextAutonomyAt=10000;
    return sim;
}
void Seconds(WorldSimulation sim,double duration)
{
    for(int n=0;n<(int)Math.Ceiling(duration/.25);n++)
    {sim.AdvanceInteractionPresentation(.1);sim.AdvanceInteractionPresentation(.1);sim.AdvanceInteractionPresentation(.05);sim.Advance(.25);}
}
void Immerse(WorldSimulation sim)
{
    var c=sim.Selected!;c.X=7.1;c.Z=0;c.TargetX=c.X;c.TargetZ=c.Z;
    sim.Advance(.25);
}
WorldSimulation Clone(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new WorldSimulation(state);
}
Creature AddNeighbor(WorldSimulation sim,string name,double x,double z,double kindness=.8)
{
    var c=new Creature{Name=name,X=x,Z=z,TargetX=x,TargetZ=z,Fullness=90,Energy=90,
        Body=new(){SpeciesId="reimu"},Interaction=new(){Enabled=true,Hydration=90,NextAutonomyAt=10000},
        Personality=new(){Initialized=true,Kindness=kindness,Pride=1-kindness,Carelessness=.2}};
    sim.State.Creatures.Add(c);return c;
}

Check("actual release can enter water without snapping to shore",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    c.Interaction.Carried=true;c.Interaction.Height=1;
    Require(sim.CanPlaceInteractionAt(c.Id,7.1,0),"water rejected as an invalid destination");
    var result=sim.ExecuteInteraction(c.Id,new(InteractionAction.PutDown,X:7.1,Z:0));
    Require(result.Success&&c.X==7.1&&c.Interaction.Water.InWater,"release did not leave the creature in water");
    Require(!WorldSimulation.CanSelfMove(c)&&WaterEnvironment.CanStruggle(c),"swimming was confused with normal ground movement");
    c.Interaction.FootIntegrity=0;
    double health=c.Health;Seconds(sim,12);
    Require(c.Interaction.Water.ExposureSeconds>8&&c.Health<health,"soaking had no actual health consequence");
});

Check("injured submerged creature eventually dies and cannot be revived by rescue",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.FootIntegrity=0;
    Immerse(sim);Seconds(sim,240);
    Require(c.Condition==PhysicalCondition.Dead&&c.Interaction.Injuries.DeathCause.Contains("泡水"),"prolonged immersion did not produce irreversible water death");
    int tools=sim.State.WaterRescueTools;
    Require(!sim.RescueFromWater(c.Id).Success&&sim.State.WaterRescueTools==tools,"rescue revived death or consumed stock on refusal");
    SaveStore.Validate(sim.State);
});

Check("rescue requires purchased stock and preserves injury and fear",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.FootIntegrity=0;
    sim.State.UnlimitedResources=false;sim.State.WaterRescueTools=0;sim.State.Money=17;
    Immerse(sim);Seconds(sim,10);double health=c.Health,fear=c.Fear;
    Require(!sim.ExecuteInteraction(c.Id,new(InteractionAction.Lift,BodyPart.Side)).Success,"ordinary hand bypassed rescue equipment");
    sim.SetColdStorage(c.Id,true);Require(!c.Family.ColdStorage.Stored,"cold storage bypassed water rescue");
    Require(!sim.BuyWaterRescueTool()&&!sim.RescueFromWater(c.Id).Success,"missing money or tool was bypassed");
    sim.State.Money=18;Require(sim.BuyWaterRescueTool(),"affordable purchase failed");
    Require(sim.State.Money==0&&sim.State.WaterRescueTools==1,"transaction accounting wrong");
    Require(sim.RescueFromWater(c.Id).Success,"valid rescue failed");
    Require(!WaterEnvironment.IsInWater(sim.State,c)&&!c.Interaction.Water.InWater&&sim.State.WaterRescueTools==0,"rescue destination or inventory wrong");
    Require(c.Health==health&&c.Fear>=fear&&c.Interaction.FootIntegrity==0&&c.Interaction.Water.RecoverySeconds>0,"rescue erased prior bodily/emotional consequences");
    SaveStore.Validate(sim.State);
});

Check("paused shop can buy but pause stops rescue and exposure",()=>
{
    var sim=Fresh();var c=sim.Selected!;Immerse(sim);
    sim.State.Settings.Pause=true;double exposure=c.Interaction.Water.ExposureSeconds,time=sim.State.WorldSeconds;
    int stock=sim.State.WaterRescueTools;
    Require(sim.BuyWaterRescueTool()&&sim.State.WaterRescueTools==stock+1,"paused modal shop cannot purchase");
    Require(!sim.RescueFromWater(c.Id).Success,"paused rescue mutated the world");
    Seconds(sim,20);sim.AdvanceInteractionPresentation(.1);
    Require(sim.State.WorldSeconds==time&&c.Interaction.Water.ExposureSeconds==exposure,"pause advanced danger");
});

Check("water exposure and fear survive a save without replaying entry",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Interaction.FootIntegrity=0;Immerse(sim);Seconds(sim,10);
    var copy=Clone(sim);double exposure=c.Interaction.Water.ExposureSeconds,alarm=c.Interaction.Emotion.Alarm;
    int memories=copy.Selected!.Memories.Count;
    Require(copy.Selected.Interaction.Water.ExposureSeconds==exposure&&copy.Selected.Interaction.Emotion.Alarm==alarm,"save lost ongoing danger or fear");
    copy.Advance(.25);
    Require(copy.Selected.Interaction.Water.ExposureSeconds>exposure&&copy.Selected.Memories.Count==memories,"loading restarted immersion or duplicated entry memory");
});

Check("put in danger then rescued does not farm trust",()=>
{
    var sim=Fresh();var c=sim.Selected!;double trust=c.Trust,safety=c.Interaction.HumanBond.Safety;
    c.Interaction.Carried=true;
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.PutDown,X:7.1,Z:0)).Success,"release failed");
    Require(sim.RescueFromWater(c.Id).Success,"rescue failed");
    Require(c.Trust<trust&&c.Interaction.HumanBond.Safety<=safety,"manufactured rescue farmed affection");
    Require(c.Speech.Contains("别再"),"rescue dialogue forgot the recent culprit");
});

Check("recent hurt remains visible after the action and survives food attraction",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.Punch,BodyPart.Side,.5)).Success,"generic body punch rejected");
    double alarm=c.Interaction.Emotion.Alarm;Require(alarm>0,"hit left no emotional continuity");
    c.Fullness=30;
    var food=new RoomObject{Kind=RoomObjectKind.Sweet,X=c.X+.7,Z=c.Z,Portions=5};sim.State.RoomObjects.Add(food);
    Seconds(sim,8);sim.AdvanceInteractionPresentation(.1);
    Require(c.Interaction.Emotion.Alarm>=alarm*.9&&!EmotionalReadiness.Settled(c),"food instantly erased recent threat");
    Require(c.Expression is not (Expression.Content or Expression.Delighted),"post-hit default expression became carefree");
    Clone(sim);
});

Check("punch combo uses actual consecutive hits and expires with time",()=>
{
    var sim=Fresh();var c=sim.Selected!;
    for(int n=0;n<3;n++)Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.Punch,BodyPart.Side,.1)).Success,"punch refused");
    Require(c.Interaction.HitCount==3,"combo count mismatch");Seconds(sim,2);
    Require(c.Interaction.HitCount==0,"combo remained forever");
    int before=c.Interaction.HitCount;
    Require(!sim.ExecuteInteraction(c.Id,new(InteractionAction.Punch,BodyPart.Accessory)).Success&&c.Interaction.HitCount==before,"accessory click counted as a body hit");
});

Check("new nested state rejects invalid save values",()=>
{
    var sim=Fresh();sim.Selected!.Interaction.Water.ExposureSeconds=double.NaN;
    bool rejected=false;try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"invalid water state accepted");
    sim.Selected.Interaction.Water.ExposureSeconds=0;sim.Selected.Personality.Kindness=2;
    rejected=false;try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"invalid personality accepted");
});

Check("visible witness remembers human harm while another region does not",()=>
{
    var sim=Fresh();var target=sim.Selected!;
    var witness=AddNeighbor(sim,"近处目击者",0,2);
    sim.State.Regions.Add(new(){Id="elsewhere",Name="别处",Kind=RegionKind.Wilderness});
    var distant=AddNeighbor(sim,"异区个体",0,2);distant.RegionId="elsewhere";
    double trust=witness.Trust;
    sim.ExecuteInteraction(target.Id,new(InteractionAction.Punch,BodyPart.Side,.5));
    Require(witness.Fear>0&&witness.Interaction.Emotion.Alarm>0&&witness.Trust<trust,"nearby witness ignored intimidation");
    Require(distant.Fear==0&&distant.Interaction.Emotion.Alarm==0,"threat crossed region without observation");
});

Check("human rescue gratitude requires actual nearby observation and cannot repeat",()=>
{
    var sim=Fresh();var target=sim.Selected!;target.Interaction.FootIntegrity=0;
    var witness=AddNeighbor(sim,"岸边目击者",5,1);
    sim.State.Regions.Add(new(){Id="elsewhere",Name="别处",Kind=RegionKind.Wilderness});
    var distant=AddNeighbor(sim,"异区个体",5,1);distant.RegionId="elsewhere";
    Immerse(sim);double near=witness.Trust,far=distant.Trust;
    Require(sim.RescueFromWater(target.Id).Success,"legitimate rescue failed");
    Require(witness.Trust>near&&distant.Trust==far,"gratitude did not follow observation bounds");
    double after=witness.Trust;
    Require(!sim.RescueFromWater(target.Id).Success&&witness.Trust==after,"repeated rescue command farmed gratitude");
});

Check("hunger attack can be stopped by feeding and preserves victim memory",()=>
{
    var sim=Fresh();var attacker=sim.Selected!;attacker.Fullness=1;
    attacker.Personality.Kindness=.05;attacker.Personality.Initialized=true;
    var victim=AddNeighbor(sim,"受害者",1.2,0);
    victim.X=WorldSimulation.InteractionFootprint(attacker)+WorldSimulation.InteractionFootprint(victim)+.1;victim.TargetX=victim.X;
    attacker.Personality.HungerTargetId=victim.Id;attacker.Personality.NextHungerStrikeAt=0;
    sim.Advance(.25);
    Require(victim.Health<100&&victim.Family.Peers[attacker.Id].Fear>0,"hunger pursuit produced no actual wound or relationship memory");
    double health=victim.Health;attacker.Fullness=40;Seconds(sim,4);
    Require(attacker.Personality.HungerTargetId==null&&victim.Health>=health,"food did not interrupt hunger aggression");
});

Check("close-up presentation expires combo without advancing ordinary time",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.State.Mode=PlayMode.CloseUp;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Punch,BodyPart.Side,.1));double time=sim.State.WorldSeconds;
    for(int n=0;n<20;n++)sim.AdvanceInteractionPresentation(.1);
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Punch,BodyPart.Side,.1));
    Require(c.Interaction.HitCount==1&&sim.State.WorldSeconds==time,"close-up reused an expired combo or advanced world time");
});

Check("lighter causes actual local damage and escape while fixed bodies tense",()=>
{
    foreach(bool fixedBody in new[]{false,true})
    {
        var sim=Fresh();var c=sim.Selected!;if(fixedBody)c.Interaction.Restraint=RestraintKind.Frame;
        double x=c.X,z=c.Z;
        var input=new InteractionInput(InteractionAction.Burn,BodyPart.Head,.5,.1,X:c.X,Z:c.Z);
        Require(sim.BeginInteractionGesture(c.Id,input).Success,"lighter could not start on a non-foot body part");
        for(int n=0;n<8;n++)sim.ContinueInteractionGesture(input,true);
        Require(c.Health<100&&c.Interaction.Injuries.At(BodyPart.Head)?.Damage>0&&c.Interaction.HeatTension>0,"lighter lacked actual local damage or tension");
        Require(fixedBody?c.X==x&&c.Z==z:c.X!=x||c.Z!=z,"restraint did not distinguish escape from tension");
        double health=c.Health,tension=c.Interaction.HeatTension;sim.EndInteractionGesture(true);
        for(int n=0;n<10;n++)sim.AdvanceInteractionPresentation(.1);
        Require(c.Health==health&&c.Interaction.HeatTension<tension,"release undid injury or failed to ease tension");
        Clone(sim);
    }
});

Check("legacy continuous harm shares witnesses without per-frame public incident spam",()=>
{
    var sim=Fresh();var c=sim.Selected!;var witness=AddNeighbor(sim,"目击者",0,2);
    var input=new InteractionInput(InteractionAction.BurnFeet,BodyPart.Bottom,.2,.1);
    sim.BeginInteractionGesture(c.Id,input);
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);
    sim.EndInteractionGesture();
    Require(witness.Fear>0&&witness.Interaction.Emotion.Alarm>0,"old burn action bypassed witness fear");
    Require(sim.State.Incidents.Count(e=>e.Kind==IncidentKind.Attack&&e.ActorId=="player")<=1,"same-world-time contact samples flooded public incidents");
});

Check("support migration settles water immediately even in close-up",()=>
{
    var sim=Fresh();var c=sim.Selected!;Immerse(sim);sim.State.Mode=PlayMode.CloseUp;
    double exposure=c.Interaction.Water.ExposureSeconds,time=sim.State.WorldSeconds;
    sim.MoveCreature(c.Id,0,0);
    Require(!c.Interaction.Water.InWater&&c.Interaction.Water.RecoverySeconds>0,"dry relocation kept the submerged pose cache");
    Require(c.Interaction.Water.ExposureSeconds==exposure&&sim.State.WorldSeconds==time,"support sync advanced time");
    Clone(sim);
});

Check("human interruption stops hunger pursuit without feeding or healing",()=>
{
    var sim=Fresh();var attacker=sim.Selected!;var victim=AddNeighbor(sim,"受害者",1,0);
    attacker.Personality.Initialized=true;attacker.Personality.Kindness=.05;attacker.Fullness=5;
    attacker.Personality.HungerTargetId=victim.Id;attacker.Personality.NextHungerStrikeAt=0;
    double health=victim.Health;
    sim.State.Settings.Pause=true;sim.InterruptFamilyConflict(attacker.Id);
    Require(attacker.Personality.HungerTargetId==victim.Id,"paused interruption mutated intent");
    sim.State.Settings.Pause=false;sim.InterruptFamilyConflict(attacker.Id);
    Require(attacker.Personality.HungerTargetId==null&&attacker.Personality.NextHungerConflictAt>=30,"spoken intervention failed to leave time to feed or separate");
    Require(attacker.Fullness==5&&victim.Health==health,"interruption invented food or healing");
    Seconds(sim,2);Require(victim.Health>=health,"interrupted attacker resumed before grace period");
});

Check("close-up support change cancels hunger pursuit without ordinary time",()=>
{
    var sim=Fresh();var attacker=sim.Selected!;var victim=AddNeighbor(sim,"受害者",1,0);
    attacker.Personality.Initialized=true;attacker.Personality.Kindness=.05;attacker.Fullness=5;
    attacker.Personality.HungerTargetId=victim.Id;attacker.Interaction.Carried=true;attacker.Interaction.Height=1;
    sim.State.Mode=PlayMode.CloseUp;double time=sim.State.WorldSeconds;
    sim.AdvanceInteractionPresentation(.1);
    Require(attacker.Personality.HungerTargetId==null&&sim.State.WorldSeconds==time,"carried attacker retained active pursuit while world time was frozen");
});

Check("nearby capable kind companion blocks starvation assault but distant one cannot",()=>
{
    foreach(bool near in new[]{true,false})
    {
        var sim=Fresh();var attacker=sim.Selected!;var victim=AddNeighbor(sim,"受害者",.8,0);
        var protector=AddNeighbor(sim,"保护者",near?1.4:-5,near?.4:5);
        protector.IsYoung=false;protector.Courage=.9;protector.Pain=protector.Fear=0;
        attacker.Personality.Initialized=true;attacker.Personality.Kindness=.05;attacker.Fullness=5;
        attacker.Personality.HungerTargetId=victim.Id;attacker.Personality.NextHungerStrikeAt=0;
        double health=victim.Health;sim.Advance(.25);
        Require(near?attacker.Personality.HungerTargetId==null&&victim.Health>=health:victim.Health<health,
            "protector ignored physical reach or failed to protect at contact");
        if(!near)Require(victim.Family.FlightRemaining>0&&victim.Interaction.Emotion.Alarm>0,"victim failed to flee or remember danger");
    }
});

Check("held harm depends on contact duration rather than input sampling rate",()=>
{
    foreach(var action in new[]{InteractionAction.Burn,InteractionAction.BurnFeet,InteractionAction.Press})
    {
        (double Health,double Pain,double Alarm,double Comfort,double Trust,double WitnessFear,double WitnessAlarm,double WitnessTrust,double HeardFear,double HeardAlarm)? expected=null;
        foreach(double sample in new[]{.1,.05,.02,.01})
        {
            var sim=Fresh();var c=sim.Selected!;sim.State.Mode=PlayMode.CloseUp;
            var witness=AddNeighbor(sim,"近处目击者",0,2);
            var listener=AddNeighbor(sim,"远处只听见的油",0,6);
            c.Interaction.Restraint=RestraintKind.Frame;
            var input=new InteractionInput(action,action==InteractionAction.BurnFeet?BodyPart.Bottom:BodyPart.Side,.4,sample);
            Require(sim.BeginInteractionGesture(c.Id,input).Success,"could not begin held contact");
            for(int n=0;n<(int)Math.Round(2/sample);n++)sim.ContinueInteractionGesture(input,true);
            sim.EndInteractionGesture();
            var actual=(Health:c.Health,Pain:c.Pain,Alarm:c.Interaction.Emotion.Alarm,
                Comfort:c.Interaction.Emotion.Manmanlai,Trust:c.Trust,
                WitnessFear:witness.Fear,WitnessAlarm:witness.Interaction.Emotion.Alarm,WitnessTrust:witness.Trust,
                HeardFear:listener.Fear,HeardAlarm:listener.Interaction.Emotion.Alarm);
            Require(actual.Health<100&&actual.Pain>0&&actual.Alarm>0,"contact lost real damage or emotional consequence");
            if(expected is { } reference)
                Require(Math.Abs(actual.Health-reference.Health)<.000001&&Math.Abs(actual.Pain-reference.Pain)<.000001&&
                    Math.Abs(actual.Alarm-reference.Alarm)<.000001&&Math.Abs(actual.Comfort-reference.Comfort)<.000001&&
                    Math.Abs(actual.Trust-reference.Trust)<.000001&&Math.Abs(actual.WitnessFear-reference.WitnessFear)<.000001&&
                    Math.Abs(actual.WitnessAlarm-reference.WitnessAlarm)<.000001&&Math.Abs(actual.WitnessTrust-reference.WitnessTrust)<.000001,
                    "same contact duration changed victim or witness consequences with sample count");
            if(expected is { } heardReference)
                Require(Math.Abs(actual.HeardFear-heardReference.HeardFear)<.000001&&
                    Math.Abs(actual.HeardAlarm-heardReference.HeardAlarm)<.000001,
                    "audibility threshold crossed differently with a different contact sample size");
            var incidents=sim.State.Incidents.Where(e=>e.Kind==IncidentKind.Attack&&e.ActorId=="player").ToArray();
            Require(incidents.Length==1&&witness.PersonalMemories.Count(m=>m.IncidentId==incidents[0].Id)==1,
                "continuous perception duplicated the public incident or witness memory");
            expected=actual;
        }
    }
});

Check("water concern presentation query rejects stale hidden or inaccessible targets without mutation",()=>
{
    var sim=Fresh();var target=sim.Selected!;Immerse(sim);
    var helper=AddNeighbor(sim,"援助者",5,0);helper.Personality.WaterConcernId=target.Id;
    Require(sim.WaterConcernTarget(helper)==target,"visible actual crisis missing");
    double time=sim.State.WorldSeconds,health=target.Health;
    helper.Quarantine=true;Require(sim.WaterConcernTarget(helper)==null,"quarantine boundary ignored");helper.Quarantine=false;
    helper.Interaction.Restraint=RestraintKind.Box;Require(sim.WaterConcernTarget(helper)==null,"boxed observer showed active rescue");
    helper.Interaction.Restraint=RestraintKind.None;
    target.Condition=PhysicalCondition.Dead;Require(sim.WaterConcernTarget(helper)==null,"old concern resurrected victim");
    target.Condition=PhysicalCondition.Active;target.X=0;
    Require(sim.WaterConcernTarget(helper)==null&&helper.Personality.WaterConcernId==target.Id,"query trusted wet cache or mutated saved concern");
    Require(sim.State.WorldSeconds==time&&target.Health==health,"read-only presentation query advanced state");
});

Check("continuous witness event survives a save without restarting or remembering each sample",()=>
{
    var sim=Fresh();var target=sim.Selected!;target.Interaction.Restraint=RestraintKind.Frame;
    var witness=AddNeighbor(sim,"目击者",0,2);
    var input=new InteractionInput(InteractionAction.Burn,BodyPart.Side,.4,.05);
    sim.BeginInteractionGesture(target.Id,input);
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);
    sim.EndInteractionGesture();
    var incident=sim.State.Incidents.Single(e=>e.Kind==IncidentKind.Attack&&e.ActorId=="player");
    string id=incident.Id;double previous=incident.Severity,fear=witness.Fear;
    var copy=Clone(sim);var saved=copy.State.Creatures.Single(c=>c.Id==witness.Id);
    copy.BeginInteractionGesture(target.Id,input);
    for(int n=0;n<20;n++)copy.ContinueInteractionGesture(input,true);
    copy.EndInteractionGesture();
    Require(copy.State.Incidents.Count(e=>e.Kind==IncidentKind.Attack&&e.ActorId=="player")==1&&
        copy.State.Incidents.Single(e=>e.Id==id).Severity>previous&&saved.Fear>fear,
        "save discarded the coalesced incident or its continued witnessed intensity");
    Require(saved.PersonalMemories.Count(m=>m.IncidentId==id)==1,"save replayed witness memory");
    saved.X=-8;saved.Z=0;double distantFear=saved.Fear,distantAlarm=saved.Interaction.Emotion.Alarm;
    copy.BeginInteractionGesture(target.Id,input);
    for(int n=0;n<20;n++)copy.ContinueInteractionGesture(input,true);
    copy.EndInteractionGesture();
    Require(saved.Fear==distantFear&&saved.Interaction.Emotion.Alarm==distantAlarm,
        "past knowledge caused new emotional increments beyond sight and hearing range");
});

Check("hearing then seeing the actor adds one first-sight response without replaying each contact",()=>
{
    var sim=Fresh();var target=sim.Selected!;target.Interaction.Restraint=RestraintKind.Frame;
    var listener=AddNeighbor(sim,"先听后看的油",0,6);
    var input=new InteractionInput(InteractionAction.Burn,BodyPart.Side,.4,.1);
    sim.BeginInteractionGesture(target.Id,input);
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(input,true);
    var incident=sim.State.Incidents.Single(e=>e.Kind==IncidentKind.Attack&&e.ActorId=="player");
    Require(listener.Knowledge.Single(k=>k.IncidentId==incident.Id).Source==KnowledgeSource.Heard&&
        !listener.Knowledge.Single(k=>k.IncidentId==incident.Id).KnowsActor,"unseen actor was identified by hearing");
    int before=listener.ReactionSerial;listener.Z=2;
    sim.ContinueInteractionGesture(input,true);
    Require(listener.ReactionSerial==before+1&&listener.Knowledge.Single(k=>k.IncidentId==incident.Id).Source==KnowledgeSource.Witness,
        "first direct sight was suppressed by an existing heard memory");
    int seen=listener.ReactionSerial;
    for(int n=0;n<5;n++)sim.ContinueInteractionGesture(input,true);
    sim.EndInteractionGesture();
    Require(listener.ReactionSerial==seen&&listener.PersonalMemories.Count(m=>m.IncidentId==incident.Id)==1,
        "same visible event replayed reaction or duplicated personal memory");
});

Console.WriteLine($"Water/emotion checks: {failures} failure(s).");
return failures==0?0:1;

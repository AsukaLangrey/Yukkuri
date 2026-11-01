using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void BuyFixtureProduct(WorldSimulation sim,string id,int packs=1)
{
    var product=CourtyardShop.Products.Single(p=>p.Id==id);
    int money=sim.State.Money,stock=sim.ShopQuantity(id);
    Require(sim.BuyShopProduct(id,packs)&&sim.State.Money==money-product.Price*packs&&
        sim.ShopQuantity(id)==stock+product.Pack*packs,"fixture purchase failed exact debit/stock: "+id);
}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation sim)=>JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);var state=JsonSerializer.Deserialize<WorldState>(Snapshot(sim),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void Frame(WorldSimulation sim,double dt=.1){sim.AdvanceInteractionPresentation(dt);sim.Advance(dt);}
WorldSimulation Attached(double elapsed=0)
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261031);sim.State.RoomObjects.Clear();
    // Cutting requires one reusable purchased tool in the v54 courtyard.
    BuyFixtureProduct(sim,"scissors");
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.X=-2+n*2;c.Z=0;c.TargetX=c.X;c.TargetZ=0;
        c.Fullness=90;c.Energy=90;c.Interaction.Hydration=90;c.Family.NextCareAt=10000;
        c.Family.NextSocialAt=10000;c.Interaction.NextAutonomyAt=10000;
    }
    // Existing gestation is an explicit component fixture. All detachment,
    // independent care, loss and birth transitions below use actual commands/time.
    sim.Selected!.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,
        FatherId=sim.State.Creatures[1].Id,EventId="detached-fixture-1",PlannedChildren=2,Elapsed=elapsed};
    return Reload(sim);
}
DetachedStemBrood Brood(WorldSimulation sim)=>sim.State.DetachedStemBroods.Single();
InteractionInput Input(WorldSimulation sim,InteractionAction action,string? supply=null,double dt=.1,double force=0)=>
    new(action,BodyPart.Side,Force:force,Duration:dt,ObjectId:Brood(sim).EventId,TargetKind:InteractionTargetKind.DetachedStem,SupplyObjectId:supply);
void Hold(WorldSimulation sim,InteractionInput input,int samples=16)
{
    Require(sim.BeginDetachedStemGesture(Brood(sim).EventId,input).Success,"independent gesture did not begin");
    for(int n=0;n<samples;n++)Require(sim.ContinueInteractionGesture(input,true).Success,"independent contact failed");
}
WorldSimulation Loose(double elapsed=0)
{
    var sim=Attached(elapsed);var c=sim.Selected!;
    var input=new InteractionInput(InteractionAction.CutStem,ObjectId:c.Family.Pregnancy.EventId,TargetKind:InteractionTargetKind.Stem);
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"cut did not begin");
    for(int n=0;n<16;n++)Require(sim.ContinueInteractionGesture(input,true).Success,"cut contact failed");
    sim.EndInteractionGesture();Require(sim.State.DetachedStemBroods.Count==1,"cut did not create one independent brood");return sim;
}
void Bottle(WorldSimulation sim)
{Require(sim.BuyStemVessel(),"vessel purchase failed");Hold(sim,Input(sim,InteractionAction.BottleStem));sim.EndInteractionGesture();}
void Supply(WorldSimulation sim,InteractionAction action)
{
    sim.EndInteractionGesture();var s=Brood(sim);
    // An actual finite physical source, not an inventory flag or injected brood value.
    var source=new RoomObject{Kind=action==InteractionAction.Drink?RoomObjectKind.Water:RoomObjectKind.Meal,
        RegionId=s.RegionId,X=-5,Z=-5,Portions=1};sim.State.RoomObjects.Add(source);
    Require(sim.HoldSupply(source.Id),"actual supply not held");
    Hold(sim,Input(sim,action,source.Id));Require(source.Portions==0,"actual source portion not consumed");
    sim.EndInteractionGesture();sim.ReleaseSupply(source.Id);
}

Check("real cut transfers original identity, progress, count and parent sources; mother recovers once",()=>
{
    var sim=Attached(37);var mother=sim.Selected!;var p=mother.Family.Pregnancy;double health=mother.Health,trust=mother.Trust;
    string injuries=JsonSerializer.Serialize(mother.Interaction.Injuries,SaveStore.JsonOptions);
    var input=new InteractionInput(InteractionAction.CutStem,ObjectId:p.EventId,TargetKind:InteractionTargetKind.Stem);
    sim.BeginInteractionGesture(mother.Id,input);for(int n=0;n<15;n++)sim.ContinueInteractionGesture(input,true);
    Require(sim.State.DetachedStemBroods.Count==0&&mother.Health==health,"cut settled before continuous threshold");
    sim.ContinueInteractionGesture(input,true);var s=Brood(sim);
    Require(s.EventId==p.EventId&&s.Elapsed==37&&s.PlannedChildren==2&&s.Mother.Id==mother.Id&&
        s.Mother.BodyId==mother.Body.Id&&s.Father.Id==p.FatherId,"detachment rerolled or lost origin");
    Require(p.Phase==PregnancyPhase.Recovering&&p.Stem==null&&mother.Health==health-2&&mother.Trust<trust&&
        sim.StemCareStatus(mother.Id)==null&&s.Integrity==97,"mother and brood ownership/recovery incorrect");
    Require(JsonSerializer.Serialize(mother.Interaction.Injuries,SaveStore.JsonOptions)==injuries,"cut invented a Head wound");
    Require(!sim.ContinueInteractionGesture(input,true).Success&&sim.State.DetachedStemBroods.Count==1,"old attached hold duplicated detachment");
    SaveStore.Validate(sim.State);
});

Check("empty bottle is bought and consumed only after one uninterrupted actual contact",()=>
{
    var sim=Loose();sim.State.UnlimitedResources=false;sim.State.Money=WorldSimulation.StemVesselPrice-1;
    Require(!sim.BuyStemVessel()&&sim.State.StemVessels==0,"unaffordable vessel purchased");
    sim.State.Money++;Require(sim.BuyStemVessel()&&sim.State.Money==0,"real vessel price not charged");
    var input=Input(sim,InteractionAction.BottleStem);Hold(sim,input,15);sim.ContinueInteractionGesture(input,false);
    for(int n=0;n<15;n++)sim.ContinueInteractionGesture(input,true);
    Require(sim.State.StemVessels==1&&Brood(sim).Phase==DetachedStemPhase.Loose,"interrupted bottle contact charged");
    sim.ContinueInteractionGesture(input,true);
    for(int n=0;n<100;n++)sim.ContinueInteractionGesture(input,true);
    Require(sim.State.StemVessels==0&&Brood(sim).Phase==DetachedStemPhase.Bottled&&Brood(sim).VesselIntegrity==100&&
        sim.InteractionGestureTarget==null&&!sim.InteractionGestureOwnsPose,"bottle hold charged twice or occupied mother");
    sim.EndInteractionGesture();sim.State.UnlimitedResources=true;
    var loose=Loose();Require(!loose.BeginDetachedStemGesture(Brood(loose).EventId,Input(loose,InteractionAction.BottleStem)).Success,
        "unlimited money invented a real bottle");
});

Check("real damaged bottle can release the same living brood once, then rehouse and deliver it",()=>
{
    var sim=Loose(37);Bottle(sim);var s=Brood(sim);string eventId=s.EventId;
    for(int n=0;n<2;n++)Require(sim.ExecuteInteraction(eventId,Input(sim,InteractionAction.Punch,force:1)).Success,"real bottle damage refused");
    Require(s.Phase==DetachedStemPhase.Bottled&&s.VesselIntegrity<=25&&s.Integrity>0,"actual damage did not leave a rescueable broken bottle");
    // Compare every existing persistent brood field except the three intentional changes.
    string Life()
    {
        var node=JsonSerializer.SerializeToNode(s,SaveStore.JsonOptions)!.AsObject();
        foreach(string key in new[]{"Phase","VesselIntegrity","LastEvent"})node.Remove(key);
        return node.ToJsonString();
    }
    string life=Life(),mother=JsonSerializer.Serialize(sim.Selected,SaveStore.JsonOptions);
    int medicine=sim.State.Medicine,vessels=sim.State.StemVessels,objects=sim.State.RoomObjects.Count;
    Require(sim.DetachedStemStatus(eventId)!.CanUnbottle,"broken live bottle not offered for rescue");
    var input=Input(sim,InteractionAction.UnbottleStem);Hold(sim,input,15);
    Require(sim.State.Medicine==medicine&&s.Phase==DetachedStemPhase.Bottled&&sim.InteractionCompletionProgress<1,"withdrawal completed early");
    Require(sim.ContinueInteractionGesture(input,true).Success,"withdrawal did not complete");
    Require(s.Phase==DetachedStemPhase.Loose&&s.VesselIntegrity==0&&Life()==life,"withdrawal repaired, rewound or replaced the brood");
    Require(sim.State.Medicine==medicine-1&&sim.State.StemVessels==vessels&&sim.State.RoomObjects.Count==objects&&
        s.LastEvent.Contains("旧瓶不退库存")&&JsonSerializer.Serialize(sim.Selected,SaveStore.JsonOptions)==mother,"withdrawal refunded bottle, invented object or changed mother");
    Require(!sim.DetachedStemStatus(eventId)!.CanUnbottle&&sim.InteractionCompletionProgress==1&&sim.InteractionGestureTarget==null,"completion retained ownership or repeat eligibility");
    string completed=Snapshot(sim);
    for(int n=0;n<25;n++)Require(sim.ContinueInteractionGesture(input,true).Success,"inert completed hold unexpectedly failed");
    Require(Snapshot(sim)==completed,"completed hold spent again or altered life");sim.EndInteractionGesture();
    double integrity=s.Integrity,hydration=s.Hydration;Frame(sim,sim.Config.TickSeconds);
    Require(s.Integrity<integrity&&s.Hydration<hydration,"withdrawal bypassed real loose-stem deterioration");
    sim=Reload(sim);s=Brood(sim);Require(s.Phase==DetachedStemPhase.Loose&&s.EventId==eventId&&sim.State.Medicine==medicine-1,"load repeated or reversed withdrawal");
    Bottle(sim);Require(sim.State.StemVessels==vessels&&s.Integrity<integrity,"rehousing repaired the brood or reused the discarded bottle");
    Require(sim.BuyFood(),"real food restock failed");
    void ActualSupply(InteractionAction action)
    {
        var place=action==InteractionAction.Drink?InteractionAction.PlaceWater:InteractionAction.PlaceMeal;
        var beforeIds=sim.State.RoomObjects.Select(o=>o.Id).ToHashSet();int food=sim.State.Food;
        Require(sim.PlaceInteractionSupply(place,-5,-5).Success,"real ground supply refused");
        var source=sim.State.RoomObjects.Single(o=>!beforeIds.Contains(o.Id));
        Require(sim.State.Food==food-(place==InteractionAction.PlaceMeal?1:0)&&sim.HoldSupply(source.Id),"source inventory or hold incorrect");
        Hold(sim,Input(sim,action,source.Id));Require(source.Portions==0,"bottle did not consume actual source");
        sim.EndInteractionGesture();sim.ReleaseSupply(source.Id);
    }
    for(int n=0;n<3000&&s.Phase==DetachedStemPhase.Bottled;n++)
    {if(s.Nutrition<65)ActualSupply(InteractionAction.FeedMeal);if(s.Hydration<65)ActualSupply(InteractionAction.Drink);Frame(sim);}
    Require(s.Phase==DetachedStemPhase.Born&&s.EventId==eventId&&sim.State.CompletedBirths.Count(id=>id==eventId)==1&&
        sim.State.Creatures.Count(c=>c.Family.BirthEventId==eventId)==s.PlannedChildren&&
        sim.State.Creatures.Where(c=>c.Family.BirthEventId==eventId).All(c=>c.Family.MotherId==s.Mother.Id&&c.Family.FatherId==s.Father.Id),"withdrawal/rehousing lost original litter or parent identities");
    var copy=Reload(sim);Frame(copy,copy.Config.TickSeconds);Require(copy.State.Creatures.Count(c=>c.Family.BirthEventId==eventId)==s.PlannedChildren,"load duplicated birth after rehousing");
    string terminal=Snapshot(sim);Require(!sim.BeginDetachedStemGesture(eventId,Input(sim,InteractionAction.UnbottleStem)).Success&&Snapshot(sim)==terminal,"born brood withdrawn again");
});

Check("withdrawal contact resets and cancellation, pause or changed identity never spend medicine",()=>
{
    foreach(string boundary in new[]{"cancel","pause","tool","identity","part"})
    {
        var sim=Loose();Bottle(sim);var input=Input(sim,InteractionAction.UnbottleStem);Hold(sim,input,15);
        if(boundary=="pause")sim.State.Settings.Pause=true;
        string before=Snapshot(sim);
        if(boundary=="cancel")sim.EndInteractionGesture(true);
        else Require(!sim.ContinueInteractionGesture(boundary switch{
            "tool"=>input with{Action=InteractionAction.Burn},"identity"=>input with{ObjectId="old-other-event"},
            "part"=>input with{Part=BodyPart.Mouth},_=>input},true).Success,"invalid continuation accepted");
        Require(!sim.HasInteractionGesture&&Snapshot(sim)==before,"interrupted withdrawal spent or changed life");
        sim.State.Settings.Pause=false;sim=Reload(sim);Require(Brood(sim).Phase==DetachedStemPhase.Bottled,"load completed unfinished withdrawal");
    }
    var reset=Loose();Bottle(reset);int stock=reset.State.Medicine;var request=Input(reset,InteractionAction.UnbottleStem);
    Hold(reset,request,15);reset.ContinueInteractionGesture(request,false);
    Require(reset.InteractionCompletionProgress==0,"lost contact retained completion timer");
    for(int n=0;n<15;n++)reset.ContinueInteractionGesture(request,true);
    Require(reset.State.Medicine==stock&&Brood(reset).Phase==DetachedStemPhase.Bottled,"resumed contact inherited earlier timer");
    reset.ContinueInteractionGesture(request,true);Require(reset.State.Medicine==stock-1&&Brood(reset).Phase==DetachedStemPhase.Loose,"renewed full contact did not settle once");
});

Check("withdrawal rejects wrong physical target, missing stock and newly occupied placement",()=>
{
    var loose=Loose();string before=Snapshot(loose);
    Require(!loose.BeginDetachedStemGesture(Brood(loose).EventId,Input(loose,InteractionAction.UnbottleStem)).Success&&Snapshot(loose)==before,"loose brood accepted withdrawal");
    var attached=Attached();var mother=attached.Selected!;
    foreach(var kind in new[]{InteractionTargetKind.Body,InteractionTargetKind.Stem})
    {before=Snapshot(attached);Require(!attached.BeginInteractionGesture(mother.Id,new(InteractionAction.UnbottleStem,BodyPart.Side,ObjectId:mother.Family.Pregnancy.EventId,TargetKind:kind)).Success&&Snapshot(attached)==before,"body or attached stem accepted withdrawal");}
    var sim=Loose();Bottle(sim);var s=Brood(sim);var input=Input(sim,InteractionAction.UnbottleStem);
    foreach(var invalid in new[]{input with{Part=BodyPart.Mouth},input with{SupplyObjectId="not-a-care-tool"}})
    {before=Snapshot(sim);Require(!sim.BeginDetachedStemGesture(s.EventId,invalid).Success&&Snapshot(sim)==before,"virtual aperture or supply identity accepted");}
    sim.State.Medicine=0;before=Snapshot(sim);
    Require(!sim.DetachedStemStatus(s.EventId)!.CanUnbottle&&!sim.BeginDetachedStemGesture(s.EventId,input).Success&&Snapshot(sim)==before,"unlimited money invented medicine");
    Require(sim.BuyMedicine(),"real nursing stock purchase failed");
    Require(sim.BeginDetachedStemGesture(s.EventId,Input(sim,InteractionAction.Lift)).Success,"actual bottle lift failed");
    before=Snapshot(sim);Require(!sim.DetachedStemStatus(s.EventId)!.CanUnbottle&&!sim.ExecuteInteraction(s.EventId,input).Success&&Snapshot(sim)==before,"held bottle accepted withdrawal");
    Require(s.HeldByHand&&sim.HasInteractionGesture&&sim.CurrentInteractionAction==InteractionAction.Lift&&
        sim.InteractionGestureObject==s.EventId,"rejected withdrawal released the transient hold or replaced its gesture");
    sim.EndInteractionGesture(true);
    Hold(sim,input,15);var blocker=new RoomObject{Kind=RoomObjectKind.Cushion,RegionId=s.RegionId,X=s.X,Z=s.Z};sim.State.RoomObjects.Add(blocker);
    before=Snapshot(sim);Require(!sim.DetachedStemStatus(s.EventId)!.CanUnbottle&&!sim.ContinueInteractionGesture(input,true).Success&&Snapshot(sim)==before,"completion ignored newly blocked original placement");
    sim.State.RoomObjects.Remove(blocker);
    for(int n=0;n<10&&s.Phase!=DetachedStemPhase.Failed;n++)sim.ExecuteInteraction(s.EventId,Input(sim,InteractionAction.Punch,force:1));
    Require(s.Phase==DetachedStemPhase.Failed,"actual hits did not create failed brood");before=Snapshot(sim);
    Require(!sim.BeginDetachedStemGesture(s.EventId,input).Success&&Snapshot(sim)==before,"failed brood revived by withdrawal");
    Hold(sim,Input(sim,InteractionAction.ClearStem));sim.EndInteractionGesture();before=Snapshot(sim);
    Require(!sim.BeginDetachedStemGesture(s.EventId,input).Success&&Snapshot(sim)==before,"cleared brood reappeared");
});

Check("actual held food and water are identity-bound; repeated hold never consumes a second portion",()=>
{
    var sim=Loose();Bottle(sim);var s=Brood(sim);
    Require(!sim.BeginDetachedStemGesture(s.EventId,Input(sim,InteractionAction.FeedMeal,"missing")).Success,"missing real source accepted");
    var source=new RoomObject{Kind=RoomObjectKind.Meal,RegionId=s.RegionId,X=-5,Z=-5,Portions=2};sim.State.RoomObjects.Add(source);sim.HoldSupply(source.Id);
    var input=Input(sim,InteractionAction.FeedMeal,source.Id);Hold(sim,input,15);var before=Snapshot(sim);
    Require(!sim.ContinueInteractionGesture(input with{SupplyObjectId="different-food"},true).Success&&Snapshot(sim)==before,
        "source swap consumed a portion or changed life");
    Hold(sim,input);for(int n=0;n<100;n++)sim.ContinueInteractionGesture(input,true);
    Require(source.Portions==1&&s.Nutrition==90&&!sim.InteractionIntakeAccepted,"independent nutrition consumed twice or became maternal intake");
    sim.EndInteractionGesture();sim.ReleaseSupply(source.Id);
    for(int n=0;n<300;n++)Frame(sim);
    double water=s.Hydration;Supply(sim,InteractionAction.Drink);
    Require(s.Hydration>water,"actual water failed to replenish vessel");SaveStore.Validate(sim.State);
});

Check("mother death does not erase independent life; real bottle care matures exactly the original litter",()=>
{
    var sim=Loose();Bottle(sim);var s=Brood(sim);var mother=sim.Selected!;string eventId=s.EventId;
    sim.Hurt(mother,300,"离体后母体的独立致死事件",null,BodyPart.Side);
    if(mother.Condition!=PhysicalCondition.Dead)sim.Hurt(mother,300,"离体后母体的独立致死事件",null,BodyPart.Side);
    Require(mother.Condition==PhysicalCondition.Dead&&s.Phase==DetachedStemPhase.Bottled&&s.Integrity==97,"mother death killed independent brood");
    for(int n=0;n<3000&&s.Phase==DetachedStemPhase.Bottled;n++)
    {
        if(s.Nutrition<65)Supply(sim,InteractionAction.FeedMeal);
        if(s.Hydration<65)Supply(sim,InteractionAction.Drink);
        Frame(sim);
    }
    Require(s.Phase==DetachedStemPhase.Born&&sim.State.CompletedBirths.Contains(eventId)&&
        sim.State.Creatures.Count(c=>c.Family.BirthEventId==eventId)==s.PlannedChildren,"real life and care did not deliver original litter");
    Require(sim.State.Creatures.Where(c=>c.Family.BirthEventId==eventId).All(c=>c.Family.MotherId==mother.Id&&c.Family.FatherId==s.Father.Id),
        "dead biological parent lost from actual genealogy");
    var copy=Reload(sim);for(int n=0;n<20;n++)Frame(copy);
    Require(copy.State.Creatures.Count(c=>c.Family.BirthEventId==eventId)==s.PlannedChildren,"reload duplicated detached birth");
});

Check("loose brood has a rescue window but eventually fails through actual lack of water",()=>
{
    var sim=Loose();var s=Brood(sim);Frame(sim);
    Require(s.Phase==DetachedStemPhase.Loose&&s.Integrity>0,"loose brood instantly failed");
    for(int n=0;n<4000&&s.Phase==DetachedStemPhase.Loose;n++)Frame(sim);
    Require(s.Phase==DetachedStemPhase.Failed&&s.Integrity==0&&s.LastDamageCause.Length>0&&
        !sim.State.CompletedBirths.Contains(s.EventId),"lack of care did not cause persistent loss");
    Require(!sim.BeginDetachedStemGesture(s.EventId,Input(sim,InteractionAction.BottleStem)).Success,"failed brood revived by empty bottle");
    SaveStore.Validate(sim.State);
});

Check("breaking a vessel causes continuous loss and replacing it preserves old stem damage",()=>
{
    var sim=Loose();Bottle(sim);var s=Brood(sim);
    for(int n=0;n<8;n++)Require(sim.ExecuteInteraction(s.EventId,Input(sim,InteractionAction.Punch)).Success,"actual vessel strike failed");
    Require(s.VesselIntegrity==20&&s.Integrity==57,"vessel damage or brood damage counted incorrectly");
    double water=s.Hydration;for(int n=0;n<10;n++)Frame(sim);
    Require(Math.Abs(water-s.Hydration-.65)<1e-7&&!sim.DetachedStemStatus(s.EventId)!.CanWater,"broken bottle retained normal water behavior");
    double integrity=s.Integrity;Bottle(sim);
    Require(s.VesselIntegrity==100&&s.Integrity==integrity,"replacement silently repaired the living stem");
});

Check("burn sampling and visible/unknown witnesses use the independent object's actual location",()=>
{
    (double Integrity,double Trust,double Alarm) Burn(double dt,int samples)
    {
        var sim=Loose();Bottle(sim);BuyFixtureProduct(sim,"lighter");var s=Brood(sim);var observer=sim.State.Creatures[1];
        observer.X=s.X+2;observer.Z=s.Z;observer.TargetX=observer.X;observer.TargetZ=observer.Z;
        double trust=observer.Trust,alarm=observer.Interaction.Emotion.Alarm;
        var input=Input(sim,InteractionAction.Burn,dt:dt,force:1);Hold(sim,input,0);
        for(int n=0;n<samples;n++)sim.ContinueInteractionGesture(input,true);
        Require(observer.Knowledge.Any(k=>k.IncidentId==s.ThreatIncidentId&&k.Source==KnowledgeSource.Witness&&k.KnowsActor),"actual object witness lacks identity");
        return(s.Integrity,observer.Trust-trust,observer.Interaction.Emotion.Alarm-alarm);
    }
    var a=Burn(.1,20);var b=Burn(.02,100);
    Require(Math.Abs(a.Integrity-b.Integrity)<1e-7&&Math.Abs(a.Trust-b.Trust)<1e-7&&Math.Abs(a.Alarm-b.Alarm)<1e-7,
        "detached harm or witness response depends on sample count");
    var heard=Loose();Bottle(heard);var brood=Brood(heard);var listener=heard.State.Creatures[1];var distant=heard.State.Creatures[2];
    listener.X=brood.X+6.5;listener.Z=brood.Z;listener.TargetX=listener.X;listener.TargetZ=listener.Z;
    distant.X=-7;distant.Z=-7;distant.TargetX=distant.X;distant.TargetZ=distant.Z;
    double trust=listener.Trust,alarm=listener.Interaction.Emotion.Alarm,farAlarm=distant.Interaction.Emotion.Alarm;
    heard.ExecuteInteraction(brood.EventId,Input(heard,InteractionAction.Punch,force:1));
    Require(listener.Knowledge.Any(k=>k.IncidentId==brood.ThreatIncidentId&&k.Source==KnowledgeSource.Heard&&!k.KnowsActor)&&
        listener.Trust==trust&&listener.Interaction.Emotion.Alarm>alarm,"hearing invented a visible human attacker or missed real sound");
    Require(!distant.Knowledge.Any(k=>k.IncidentId==brood.ThreatIncidentId)&&distant.Interaction.Emotion.Alarm==farAlarm,
        "independent harm informed a distant observer");
});

Check("drag is bounded by actual space, single hand, cancellation and persistent ground anchor",()=>
{
    var sim=Loose();Bottle(sim);var s=Brood(sim);double x=s.X,z=s.Z;
    var input=Input(sim,InteractionAction.Lift) with{X=x,Z=z};Hold(sim,input,0);
    Require(s.HeldByHand&&sim.InteractionGestureTarget==null,"independent carry took mother's identity");
    var food=new RoomObject{Kind=RoomObjectKind.Meal,X=-5,Z=-5};sim.State.RoomObjects.Add(food);
    Require(!sim.HoldSupply(food.Id),"one hand held bottle and food together");
    sim.ContinueInteractionGesture(input with{X=x,Z=z+2},false);
    var view=sim.DetachedStemStatus(s.EventId)!;
    Require(Math.Abs(view.Z-z)<=.600001&&s.X==view.X&&s.Z==view.Z,"one input teleported bottle or did not persist real position");
    for(int n=0;n<4;n++)sim.ContinueInteractionGesture(input with{X=x,Z=z+2},false);
    double actualX=s.X,actualZ=s.Z;
    Require(actualZ-z>1.9,"clear drag path never moved the bottle");
    var younger=sim.State.Creatures[2];
    Require(sim.CanPlaceInteractionAt(younger.Id,x,z),"invisible original bottle position remained reserved");
    // A dynamic occupancy boundary: another oil now occupies the vacated spot.
    younger.X=x;younger.Z=z;younger.TargetX=x;younger.TargetZ=z;
    Frame(sim);
    Require(!sim.CanPlaceInteractionAt(sim.Selected!.Id,actualX,actualZ),"held bottle did not occupy its visible location");
    var copy=Reload(sim);Require(!Brood(copy).HeldByHand&&Brood(copy).X==actualX&&Brood(copy).Z==actualZ,"load retained phantom hand or rewound real position");
    sim.EndInteractionGesture(true);Require(!s.HeldByHand&&s.X==actualX&&s.Z==actualZ,"cancel teleported bottle back to old anchor");
    Require(!sim.CanPlaceDetachedStemAt(s.EventId,sim.Selected!.X,sim.Selected.Z)&&!sim.CanPlaceDetachedStemAt(s.EventId,8,8),
        "body overlap or out-of-bounds landing allowed");
});

Check("pause, close-up, cross-region and wrong object reject without spending or advancing life",()=>
{
    foreach(string boundary in new[]{"pause","close","region","identity"})
    {
        var sim=Loose();sim.BuyStemVessel();var s=Brood(sim);var input=Input(sim,InteractionAction.BottleStem);Hold(sim,input,15);
        if(boundary=="pause")sim.State.Settings.Pause=true;
        if(boundary=="close")sim.State.Mode=PlayMode.CloseUp;
        if(boundary=="region")sim.State.ActiveRegionId="elsewhere";
        var before=Snapshot(sim);
        Require(!sim.ContinueInteractionGesture(boundary=="identity"?input with{ObjectId="other"}:input,true).Success&&
            !sim.HasInteractionGesture&&Snapshot(sim)==before,"boundary accepted or mutated: "+boundary);
    }
    var frozen=Loose();var b=Brood(frozen);double elapsed=b.Elapsed;frozen.State.Settings.Pause=true;
    for(int n=0;n<20;n++)Frame(frozen);Require(b.Elapsed==elapsed,"paused independent life advanced");
});

Check("mother cold storage and new pregnancy cannot take ownership of detached life",()=>
{
    var sim=Loose();Bottle(sim);var s=Brood(sim);var mother=sim.Selected!;
    BuyFixtureProduct(sim,"freezer");
    sim.SetColdStorage(mother.Id,true);Require(mother.Family.ColdStorage.Stored,"mother cold storage fixture failed");
    double elapsed=s.Elapsed;for(int n=0;n<10;n++)Frame(sim);
    Require(Math.Abs(s.Elapsed-elapsed-1)<1e-7,"mother's cold clock slowed independent brood");
    // A later pregnancy is a separate declared ownership boundary, never used
    // to fake the detached lifecycle's own elapsed time or birth results.
    mother.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,
        FatherId=s.Father.Id,EventId="later-independent-pregnancy",PlannedChildren=3};
    var copy=Reload(sim);
    Require(Brood(copy).EventId==s.EventId&&Brood(copy).PlannedChildren==2&&
        copy.Selected!.Family.Pregnancy.EventId!=s.EventId,"later pregnancy overwrote old brood");
});

Check("invalid duplicate ownership, lineage, numbers and completed ledger are rejected; status is read-only",()=>
{
    var sim=Loose();string before=Snapshot(sim);
    for(int n=0;n<20;n++){sim.DetachedStemStatus(Brood(sim).EventId);sim.DetachedStems(sim.State.ActiveRegionId);}
    Require(Snapshot(sim)==before,"query mutated independent life");
    foreach(string bad in new[]{"number","source","duplicate","ledger","zero"})
    {
        var copy=Reload(sim);var s=Brood(copy);
        if(bad=="number")s.Hydration=double.NaN;
        if(bad=="source")s.Mother.Id="missing-parent";
        if(bad=="duplicate"){copy.Selected!.Family.Pregnancy.Phase=PregnancyPhase.Gestating;copy.Selected.Family.Pregnancy.Stem=null;}
        if(bad=="ledger")copy.State.CompletedBirths.Add(s.EventId);
        if(bad=="zero")s.Integrity=0;
        bool rejected=false;try{SaveStore.Validate(copy.State);}catch(InvalidDataException){rejected=true;}
        Require(rejected,"invalid detached state accepted: "+bad);
    }
});

Check("real cleanup removes only terminal physical remnants and preserves all history without refunds",()=>
{
    var active=Loose();var before=Snapshot(active);
    Require(!active.BeginDetachedStemGesture(Brood(active).EventId,Input(active,InteractionAction.ClearStem)).Success&&Snapshot(active)==before,
        "living brood could be silently cleared");
    foreach(bool born in new[]{false,true})
    {
        var sim=Loose(born?238:0);var s=Brood(sim);
        if(born)
        {
            Bottle(sim);for(int n=0;n<40&&s.Phase!=DetachedStemPhase.Born;n++)Frame(sim);
            Require(s.Phase==DetachedStemPhase.Born,"terminal birth fixture failed");
        }
        else
            for(int n=0;n<5;n++)sim.ExecuteInteraction(s.EventId,Input(sim,InteractionAction.Punch,force:1));
        var phase=s.Phase;string cause=s.LastDamageCause;int stock=sim.State.StemVessels;int children=sim.State.Creatures.Count;
        Require(sim.DetachedStemStatus(s.EventId)!.CanClear,"terminal remnant could not be cleaned");
        var input=Input(sim,InteractionAction.ClearStem);Hold(sim,input,15);
        Require(!s.Cleared,"cleanup happened before real contact completed");
        sim.ContinueInteractionGesture(input,false);for(int n=0;n<16;n++)sim.ContinueInteractionGesture(input,true);
        Require(s.Cleared&&s.Phase==phase&&s.LastDamageCause==cause&&sim.State.StemVessels==stock&&sim.State.Creatures.Count==children,
            "cleanup erased cause, refunded bottle or changed life outcome");
        Require(!sim.ContinueInteractionGesture(input,true).Success&&!sim.DetachedStemStatus(s.EventId)!.CanClear,"same hold recleaned hidden object");
        var copy=Reload(sim);Require(Brood(copy).Cleared&&Brood(copy).EventId==s.EventId&&Brood(copy).LastDamageCause==cause,
            "reload deleted independent identity or forgot physical cleanup");
    }
});

Check("independent bottle receives bought ground supplies after every adult has died, without a selected living body",()=>
{
    var sim=Loose();Bottle(sim);var s=Brood(sim);var mother=sim.Selected!;
    // Retain the original gestation component start. Death, separation, bottle,
    // placement, pickup, sustained mouth contact and birth use actual commands.
    var parents=sim.State.Creatures.ToArray();
    foreach(var c in parents)
    {
        sim.Hurt(c,300,"独立瓶养中的亲代致死事件",null,BodyPart.Side);
        if(c.Condition!=PhysicalCondition.Dead)sim.Hurt(c,300,"独立瓶养中的亲代致死事件",null,BodyPart.Side);
    }
    Require(parents.All(c=>c.Condition==PhysicalCondition.Dead)&&s.Phase==DetachedStemPhase.Bottled,
        "journey still had an available living body or lost the independent brood");
    string before=Snapshot(sim);
    Require(!sim.ExecuteInteraction(mother.Id,new(InteractionAction.PlaceWater,X:-5,Z:-5)).Success&&Snapshot(sim)==before,
        "legacy body path bypassed its original death qualification");
    int stock=sim.State.Food,money=sim.State.Money;
    Require(sim.BuyFood()&&sim.State.Food==stock+WorldSimulation.FoodBundleCount&&sim.State.Money==money-WorldSimulation.FoodBundlePrice,
        "real paid purchase failed exact debit or finite food stock");
    int meals=0,waters=0;
    void Give(InteractionAction action)
    {
        var kind=action==InteractionAction.Drink?RoomObjectKind.Water:RoomObjectKind.Meal;
        var placement=kind==RoomObjectKind.Water?InteractionAction.PlaceWater:InteractionAction.PlaceMeal;
        int food=sim.State.Food;var ids=sim.State.RoomObjects.Select(o=>o.Id).ToHashSet();
        string bodies=JsonSerializer.Serialize(sim.State.Creatures,SaveStore.JsonOptions);
        Require(sim.PlaceInteractionSupply(placement,-5,-5).Success,"independent ground supply could not be placed");
        var source=sim.State.RoomObjects.Single(o=>!ids.Contains(o.Id));
        Require(source.Kind==kind&&source.RegionId==s.RegionId&&source.Portions==1&&!source.HeldByHand&&
            source.Height==RoomSupplySupport.Height(sim.State,s.RegionId,-5,-5,kind)&&
            sim.State.Food==food-(kind==RoomObjectKind.Meal?1:0)&&
            JsonSerializer.Serialize(sim.State.Creatures,SaveStore.JsonOptions)==bodies,
            "ground placement changed a corpse or lost its actual portion, stock cost or support");
        Require(sim.HoldSupply(source.Id),"new physical supply could not be picked up");
        var input=Input(sim,action,source.Id) with {Part=BodyPart.Mouth};
        Require(sim.BeginDetachedStemGesture(s.EventId,input).Success,"actual bottle-mouth offer did not begin");
        double value=kind==RoomObjectKind.Water?s.Hydration:s.Nutrition;
        for(int n=0;n<15;n++)
        {Require(sim.ContinueInteractionGesture(input,true).Success,"bottle contact failed");Frame(sim);}
        Require(source.Portions==1,"source consumed before the original 1.6 second contact");
        Require(sim.ContinueInteractionGesture(input,true).Success&&source.Portions==0&&
            (kind==RoomObjectKind.Water?s.Hydration:s.Nutrition)>value,
            "bottle did not consume its held portion and gain actual nourishment");
        double accepted=kind==RoomObjectKind.Water?s.Hydration:s.Nutrition;
        for(int n=0;n<8;n++)Require(sim.ContinueInteractionGesture(input,true).Success,"completed hold failed");
        Require(source.Portions==0&&(kind==RoomObjectKind.Water?s.Hydration:s.Nutrition)==accepted&&
            sim.State.Food==food-(kind==RoomObjectKind.Meal?1:0),"continued hold duplicated nourishment or charged inventory twice");
        sim.EndInteractionGesture();sim.ReleaseSupply(source.Id);
        if(kind==RoomObjectKind.Water)waters++;else meals++;
    }
    double started=sim.State.WorldSeconds;
    for(int n=0;n<3000&&s.Phase==DetachedStemPhase.Bottled;n++)
    {
        Require(parents.All(c=>c.Condition==PhysicalCondition.Dead),"bottle care revived a dead body");
        if(s.Nutrition<65)Give(InteractionAction.FeedMeal);
        // After the first actual meal, also cover no selection. The same public
        // world command must work without borrowing any dead or living identity.
        if(meals>0)sim.State.SelectedId=null;
        if(s.Hydration<65)Give(InteractionAction.Drink);
        Frame(sim);
    }
    Require(meals>0&&waters>0&&s.Phase==DetachedStemPhase.Born&&
        sim.State.WorldSeconds-started>=FamilyRules.GestationSeconds(BirthMethod.Stem)-.001&&
        sim.State.CompletedBirths.Contains(s.EventId)&&
        sim.State.Creatures.Count(c=>c.Family.BirthEventId==s.EventId)==s.PlannedChildren&&
        sim.State.Creatures.Where(c=>c.Family.BirthEventId==s.EventId).All(c=>c.Family.MotherId==s.Mother.Id&&c.Family.FatherId==s.Father.Id),
        "actual independent supplies did not preserve the original clock, litter and genealogy");
    var copy=Reload(sim);for(int n=0;n<10;n++)Frame(copy);
    Require(copy.State.Creatures.Count(c=>c.Family.BirthEventId==s.EventId)==s.PlannedChildren,
        "loading repeated the independently supplied birth");
});

Check("independent ground placement retains world gates and failed transactions leave stock and objects unchanged",()=>
{
    var sim=Loose();var s=Brood(sim);
    void Reject(InteractionAction action,double x=-5,double z=-5)
    {string before=Snapshot(sim);Require(!sim.PlaceInteractionSupply(action,x,z).Success&&Snapshot(sim)==before,"invalid ground placement mutated world state");}
    sim.State.Settings.Pause=true;Reject(InteractionAction.PlaceWater);sim.State.Settings.Pause=false;
    sim.State.Mode=PlayMode.CloseUp;Reject(InteractionAction.PlaceWater);sim.State.Mode=PlayMode.Management;
    sim.State.InteractionPlaytest=false;Reject(InteractionAction.PlaceMeal);sim.State.InteractionPlaytest=true;
    string region=sim.State.ActiveRegionId;
    foreach(string invalid in new[]{"","missing-region"}){sim.State.ActiveRegionId=invalid;Reject(InteractionAction.PlaceWater);}
    sim.State.ActiveRegionId=region;
    Reject(InteractionAction.FeedMeal);Reject((InteractionAction)int.MaxValue);
    Reject(InteractionAction.PlaceWater,double.NaN);Reject(InteractionAction.PlaceWater,z:double.PositiveInfinity);
    Reject(InteractionAction.PlaceWater,8.01);Reject(InteractionAction.PlaceWater,z:-8.01);
    Reject(InteractionAction.PlaceMeal,s.X,s.Z);
    int food=sim.State.Food;sim.State.Food=0;Reject(InteractionAction.PlaceMeal);sim.State.Food=food;
    NaturalResources.AddWoodland(sim.State,region);Reject(InteractionAction.PlaceWater,7.1,0);
    // Isolate the object-cap boundary from the separate finite-water budget.
    BuyFixtureProduct(sim,"water");int water=sim.State.StationWaterPortions;
    Require(water>24,"capacity fixture needs stock left after all 24 placements");
    for(int n=0;n<24;n++)Require(sim.PlaceInteractionSupply(InteractionAction.PlaceWater,-5,-5).Success,"existing ground-object capacity changed");
    Reject(InteractionAction.PlaceWater);Reject(InteractionAction.PlaceMeal);
    Require(sim.State.RoomObjects.Count(o=>o.Kind==RoomObjectKind.Water)==24&&sim.State.Food==food&&
        sim.State.StationWaterPortions==water-24,
        "water debit changed or the 24 object cap was bypassed");
});

return failures==0?0:1;

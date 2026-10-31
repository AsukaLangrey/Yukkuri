using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

// Component journeys for the separately authorized remote machine; never run locally.
// Initial needs, ages, positions and family relation isolate the physical source chain.
// Purchases, placement, refill, taking portions and all later progress use real commands/time.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body){try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation s)
{SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(Snapshot(s),SaveStore.JsonOptions)!;SaveStore.Validate(state);return new(state);}
void Frame(WorldSimulation s,double dt=.05){s.AdvanceInteractionPresentation(dt);s.Advance(dt);}
void Seconds(WorldSimulation s,double seconds){for(int n=0;n<(int)Math.Round(seconds/.05);n++)Frame(s);}
WorldSimulation Fresh()
{
    var s=WorldSimulation.CreateInteractionPlaytest(20261108);s.State.RoomObjects.Clear();
    s.State.PlayerX=0;s.State.PlayerZ=7;
    for(int n=0;n<s.State.Creatures.Count;n++)
    {
        var c=s.State.Creatures[n];c.X=c.TargetX=-5+n*4;c.Z=c.TargetZ=-5;
        c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Health=c.Interaction.Hydration=100;c.Pain=c.Fear=0;
        c.Interaction.NextAutonomyAt=c.DecisionIn=c.Family.NextCareAt=c.Family.NextSocialAt=10000;
    }
    s.SetAutonomousBreeding(false);return s;
}
SupplyStationState Station(WorldSimulation s)=>s.State.SupplyStations.Single();
InteractionInput Input(SupplyStationState s,InteractionAction action=InteractionAction.RefillSupplyStation,double dt=.1)=>
    new(action,BodyPart.Side,Duration:dt,X:s.X,Z:s.Z,ObjectId:s.Id,TargetKind:InteractionTargetKind.SupplyStation);
void Refill(WorldSimulation s)
{
    var station=Station(s);var input=Input(station);
    Require(s.BeginSupplyStationGesture(station.Id,input).Success,"refill did not begin");
    for(int n=0;n<16;n++)Require(s.ContinueInteractionGesture(input,true).Success,"refill contact failed");
    Require(s.InteractionCompletionProgress==1,"real timed refill did not complete");
    string settled=Snapshot(s);for(int n=0;n<4;n++)s.ContinueInteractionGesture(input,true);
    Require(Snapshot(s)==settled,"completed held refill transferred twice");s.EndInteractionGesture();
}
WorldSimulation Stocked(SupplyStationKind kind,bool output=true)
{
    var s=Fresh();Require(s.BuySupplyStation(kind),"empty device purchase failed");
    Require(s.PlaceSupplyStation(kind,0,0).Success,"device placement failed");
    if(kind==SupplyStationKind.Water)Require(s.BuyStationWater(),"water purchase failed");
    Refill(s);if(output)Seconds(s,.25);return s;
}
RoomObject Outlet(WorldSimulation s)=>s.State.RoomObjects.Single(o=>o.Id==Station(s).OutletObjectId);
int Pending(WorldSimulation s,string id)=>s.State.Creatures.Count(c=>c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeSourceId==id);
int Physical(WorldSimulation s,RoomObjectKind kind)=>Station(s).StoredPortions+s.State.RoomObjects.Where(o=>o.Kind==kind&&!o.Stored).Sum(o=>o.Portions)+
    s.State.Creatures.Count(c=>c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeKind==kind);

Check("finite purchases, contact cancellation and one-time refill use actual capacity",()=>
{
    foreach(var kind in Enum.GetValues<SupplyStationKind>())
    {
        var s=Fresh();s.State.UnlimitedResources=false;s.State.Money=0;
        string before=Snapshot(s);Require(!s.BuySupplyStation(kind)&&Snapshot(s)==before,"failed purchase mutated world");
        s.State.Money=100;int price=kind==SupplyStationKind.Food?WorldSimulation.FoodStationPrice:WorldSimulation.WaterStationPrice;
        Require(s.BuySupplyStation(kind)&&s.State.Money==100-price,"device not charged once");
        before=Snapshot(s);Require(!s.PlaceSupplyStation(kind,double.NaN,0).Success&&Snapshot(s)==before,"invalid placement consumed inventory");
        Require(s.PlaceSupplyStation(kind,0,0).Success,"legal placement failed");
        if(kind==SupplyStationKind.Water)Require(s.BuyStationWater(),"batch water purchase failed");
        var station=Station(s);var input=Input(station);int stock=kind==SupplyStationKind.Food?s.State.Food:s.State.StationWaterPortions;
        Require(s.BeginSupplyStationGesture(station.Id,input).Success,"start failed");
        for(int n=0;n<8;n++)s.ContinueInteractionGesture(input,true);
        Require(station.StoredPortions==0&&s.InteractionCompletionProgress is >.49 and <.51,"refilled early");
        s.ContinueInteractionGesture(input,false);Require(s.InteractionCompletionProgress==0,"lost contact did not reset");
        for(int n=0;n<15;n++)s.ContinueInteractionGesture(input,true);
        Require(station.StoredPortions==0,"resumed contact inherited prior progress");
        s.EndInteractionGesture(true);Require((kind==SupplyStationKind.Food?s.State.Food:s.State.StationWaterPortions)==stock,"cancel consumed inventory");
        s.BeginSupplyStationGesture(station.Id,input);s.State.Settings.Pause=true;
        Require(!s.ContinueInteractionGesture(input,true).Success&&!s.HasInteractionGesture&&station.StoredPortions==0,"pause settled refill");
        s.State.Settings.Pause=false;Refill(s);int capacity=kind==SupplyStationKind.Food?WorldSimulation.FoodStationCapacity:WorldSimulation.WaterStationCapacity;
        Require(station.StoredPortions==capacity&&(kind==SupplyStationKind.Food?s.State.Food:s.State.StationWaterPortions)==stock-capacity,"refill violated finite capacity");
        before=Snapshot(s);Require(!s.BeginSupplyStationGesture(station.Id,input).Success&&Snapshot(s)==before,"full refill changed state");
        s.State.Mode=PlayMode.CloseUp;int storedBefore=station.StoredPortions;double time=s.State.WorldSeconds;
        Seconds(s,2);Require(s.State.WorldSeconds==time&&station.StoredPortions==storedBefore&&station.OutletObjectId==null,"close-up generated output");
        s.State.Mode=PlayMode.Management;Seconds(s,.25);Require(station.StoredPortions==capacity-1&&Outlet(s).Portions==1,"not exactly one real portion");
    }
    var limit=Fresh();limit.State.FoodStationStock=99;limit.State.StationWaterPortions=int.MaxValue-5;
    string unchanged=Snapshot(limit);Require(!limit.BuySupplyStation(SupplyStationKind.Food)&&!limit.BuyStationWater()&&Snapshot(limit)==unchanged,"overflow purchase was not transactional");
});

Check("save continuation keeps deterministic output identity and old saves grant no stock",()=>
{
    var s=Stocked(SupplyStationKind.Food,output:false);var copy=Reload(s);
    Seconds(s,.25);Seconds(copy,.25);
    Require(Outlet(s).Id==Outlet(copy).Id&&Station(s).OutputSequence==Station(copy).OutputSequence&&
        Station(s).StoredPortions==Station(copy).StoredPortions,"same save produced different output identities");
    Require(Outlet(s).Id.StartsWith(Station(s).Id+"-out-",StringComparison.Ordinal),"automatic output used random identity");
    var json=JsonNode.Parse(Snapshot(Fresh()))!.AsObject();
    foreach(string name in new[]{"SupplyStations","SupplyStationSequence","FoodStationStock","WaterStationStock","StationWaterPortions"})
        json.Remove(json.Select(p=>p.Key).FirstOrDefault(k=>k.Equals(name,StringComparison.OrdinalIgnoreCase))??name);
    var legacy=JsonSerializer.Deserialize<WorldState>(json.ToJsonString(),SaveStore.JsonOptions)!;SaveStore.Validate(legacy);
    Require(legacy.SupplyStations.Count==0&&legacy.FoodStationStock==0&&legacy.WaterStationStock==0&&legacy.StationWaterPortions==0,"legacy save minted inventory");
    Station(s).StoredPortions=WorldSimulation.FoodStationCapacity;bool rejected=false;
    try{SaveStore.Validate(s.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"warehouse plus live outlet overflow accepted");
    var exhausted=Stocked(SupplyStationKind.Water,false);Station(exhausted).OutputSequence=long.MaxValue;Seconds(exhausted,.25);
    Require(Station(exhausted).StoredPortions==WorldSimulation.WaterStationCapacity&&Station(exhausted).OutletObjectId==null,"sequence exhaustion lost or duplicated water");
    foreach(Action<WorldState> corrupt in new Action<WorldState>[]
    {
        state=>state.Creatures[0].Interaction=null!,
        state=>state.Creatures[^1].Interaction=null!,
        state=>state.DetachedStemBroods=null!,
        state=>state.DetachedStemBroods.Add(null!),
        state=>state.SupplyStations=null!,
        state=>state.SupplyStations.Add(null!)
    })
    {
        var bad=Stocked(SupplyStationKind.Food);corrupt(bad.State);bool invalid=false;
        try{SaveStore.Validate(bad.State);}catch(InvalidDataException){invalid=true;}
        Require(invalid,"explicit null reference was not rejected as invalid save data");
    }
});

Check("rear approach reaches real front meal and water for young, adult, elder and hurt bodies",()=>
{
    foreach(var kind in Enum.GetValues<SupplyStationKind>())foreach(string posture in new[]{"young","adult","elder","hurt"})
    {
        var s=Stocked(kind);var c=s.State.Creatures[0];var outlet=Outlet(s);int total=Physical(s,outlet.Kind);
        // Initial approach fixture: behind and to one side, with a genuine line
        // of sight past the bin. No positions or needs are changed after this.
        c.X=c.TargetX=-1.8;c.Z=c.TargetZ=-1.4;c.IsYoung=posture=="young";c.IsElder=posture=="elder";
        c.Body.AgeSeconds=c.InnerAgeSeconds=c.IsYoung?0:c.IsElder?50000:2000;
        c.Fullness=kind==SupplyStationKind.Food?45:95;c.Interaction.Hydration=kind==SupplyStationKind.Water?45:95;
        if(posture=="hurt")s.Hurt(c,3,"供给器组件的既有侧伤",null,BodyPart.Side);
        bool pending=false,accepted=false;double before=kind==SupplyStationKind.Food?c.Fullness:c.Interaction.Hydration;
        for(int n=0;n<1600&&!accepted;n++)
        {
            Frame(s);pending|=c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeSourceId==outlet.Id;
            accepted=c.Interaction.GroundIntakeSourceId==outlet.Id&&c.Interaction.GroundIntake&&!c.Interaction.NaturalIntakePending&&c.Interaction.EatingRemaining>0;
            Require(WorldSimulation.Distance(c.X,c.Z,0,0)>=WorldSimulation.SupplyStationRadius+WorldSimulation.InteractionFootprint(c)-.0001,"approach passed through warehouse");
        }
        Require(pending&&accepted,$"{kind}/{posture} did not reach actual mouth entry: {c.Activity}");
        Require(c.Z>=outlet.Z-.001&&(kind==SupplyStationKind.Food?c.Fullness:c.Interaction.Hydration)>before&&Physical(s,outlet.Kind)==total-1,"entry did not consume exactly one finite portion from the front");
        Require(Station(s).StoredPortions==total-1&&outlet.Portions==0,"new output appeared while original mouth was busy");
        SaveStore.Validate(s.State);
    }
});

Check("pending mouth reservation counts capacity and taking it returns the same portion once",()=>
{
    var s=Stocked(SupplyStationKind.Food);var c=s.State.Creatures[0];c.X=c.TargetX=0;c.Z=c.TargetZ=1.64;c.Fullness=45;
    var outlet=Outlet(s);int total=Physical(s,outlet.Kind);bool saw=false;
    for(int n=0;n<200&&!saw;n++){Frame(s);saw=c.Interaction.NaturalIntakePending;}
    Require(saw&&outlet.Portions==0&&Pending(s,outlet.Id)==1,"no actual reservation");
    Require(s.SupplyStationStatus(Station(s).Id)!.OutletPortions==1&&!s.SupplyStationStatus(Station(s).Id)!.CanRefill,"pending portion disappeared from capacity");
    Require(!s.BeginSupplyStationGesture(Station(s).Id,Input(Station(s),InteractionAction.Lift)).Success,"moved an occupied outlet");
    var pendingCopy=Reload(s);Require(Physical(pendingCopy,outlet.Kind)==total,"reload reset pending finite source");
    Require(s.HoldSupply(outlet.Id)&&outlet.HeldByHand&&outlet.Portions==1&&!c.Interaction.NaturalIntakePending&&Station(s).OutletObjectId==null,"taking pending source failed one-time return/unbind");
    Require(!s.HoldSupply(outlet.Id)&&Physical(s,outlet.Kind)==total,"second take copied portion");
    Require(s.ReleaseSupply(outlet.Id,-3,3),"actual portion could not be put down");
    Seconds(s,.25);Require(Physical(s,outlet.Kind)==total&&Station(s).OutletObjectId!=outlet.Id,"new tray refunded previous portion");
    SaveStore.Validate(s.State);
});

Check("the same empty outlet refills after swallowing and supplies a second real mouthful in place",()=>
{
    foreach(var kind in Enum.GetValues<SupplyStationKind>())
    {
        var s=Stocked(kind);var c=s.State.Creatures[0];var outlet=Outlet(s);var station=Station(s);
        c.X=c.TargetX=0;c.Z=c.TargetZ=1.64;
        c.Fullness=kind==SupplyStationKind.Food?10:95;c.Interaction.Hydration=kind==SupplyStationKind.Water?10:95;
        int total=Physical(s,outlet.Kind),exposures=c.Interaction.Digestion.Exposures;bool first=false,finished=false,refilled=false,second=false;
        double firstX=0,firstZ=0;
        for(int n=0;n<1200&&!second;n++)
        {
            Frame(s);
            if(!first&&c.Interaction.Digestion.Exposures==exposures+1)
            {first=true;firstX=c.X;firstZ=c.Z;Require(outlet.Portions==0&&station.StoredPortions==total-1,"first accepted mouthful did not retain its empty plate");}
            if(first)
            {
                Require(WorldSimulation.Distance(c.X,c.Z,firstX,firstZ)<.05,"consumer had to leave its actual feeding stance to get a second portion");
                finished|=c.Interaction.EatingRemaining==0;
                refilled|=finished&&station.StoredPortions==total-2&&outlet.Portions+Pending(s,outlet.Id)==1;
                second=c.Interaction.Digestion.Exposures==exposures+2;
            }
            Require(station.OutletObjectId==outlet.Id&&s.State.RoomObjects.Count(o=>o.Id==outlet.Id)==1,"empty tray was replaced or duplicated");
        }
        Require(first&&finished&&refilled&&second&&Physical(s,outlet.Kind)==total-2,"same-tray second real intake did not conserve finite supply");
        SaveStore.Validate(s.State);var copy=Reload(s);
        Require(Station(copy).OutletObjectId==outlet.Id&&Physical(copy,outlet.Kind)==total-2,"reload refunded a swallowed source");
    }
});

Check("physical drag keeps occupied last legal ground and cannot overlap supply or independent objects",()=>
{
    var s=Stocked(SupplyStationKind.Water);var station=Station(s);var outlet=Outlet(s);var lift=Input(station,InteractionAction.Lift);
    Require(s.BeginSupplyStationGesture(station.Id,lift).Success&&station.HeldByHand,"lift failed");
    Require(!s.HoldSupply(outlet.Id),"held both device and water");
    s.ContinueInteractionGesture(lift with {X=1.2,Z=0},true);double x=station.X,z=station.Z;
    Require(x>0&&outlet.X==x&&outlet.Z==z+WorldSimulation.SupplyStationOutletOffset,"outlet did not travel with actual device");
    Require(!s.CanPlaceInteractionAt(s.Selected!.Id,x,z)&&!s.PlaceInteractionSupply(InteractionAction.PlaceWater,x,z).Success,"warehouse footprint not shared");
    s.EndInteractionGesture(true);Require(!station.HeldByHand&&station.X==x&&station.Z==z,"cancel teleported device");
    var loaded=Reload(s);Require(Station(loaded).X==x&&Outlet(loaded).X==x&&!Station(loaded).HeldByHand,"save restored stale hand or anchor");
    lift=Input(station,InteractionAction.Lift);s.BeginSupplyStationGesture(station.Id,lift);
    s.State.Regions.Add(new(){Id="other",Name="other",Kind=RegionKind.Sanctuary});s.State.ActiveRegionId="other";s.State.SelectedId=null;
    Require(!s.ContinueInteractionGesture(lift,true).Success&&!station.HeldByHand&&!s.HasInteractionGesture,"region change retained held device");
});

Check("family collection delivers the actual dispenser portion to a dependent child",()=>
{
    foreach(var kind in Enum.GetValues<SupplyStationKind>())
    {
        var s=Stocked(kind);var giver=s.State.Creatures[0];var child=s.State.Creatures[2];var outlet=Outlet(s);
        giver.X=giver.TargetX=0;giver.Z=giver.TargetZ=2.3;giver.Fullness=giver.Interaction.Hydration=95;
        child.X=child.TargetX=4;child.Z=child.TargetZ=2;child.IsYoung=true;child.Body.AgeSeconds=child.InnerAgeSeconds=0;
        child.Family.MotherId=child.ParentId=giver.Id;child.Interaction.FootIntegrity=20;child.Family.Warmth=90;
        child.Fullness=kind==SupplyStationKind.Food?20:95;child.Interaction.Hydration=kind==SupplyStationKind.Water?20:95;
        int initial=Physical(s,outlet.Kind);s.ArrangeCare(giver.Id,child.Id);Require(giver.Family.CareTargetId==child.Id,"care did not begin");
        string? parcelId=null;bool carried=false,placed=false,accepted=false;
        for(int n=0;n<2400&&!accepted;n++)
        {
            Frame(s);
            if(giver.Family.CareParcelId is { } id){parcelId=id;carried|=s.State.RoomObjects.Any(o=>o.Id==id&&o.CarrierId==giver.Id);}
            if(parcelId!=null)
            {
                placed|=s.State.RoomObjects.Any(o=>o.Id==parcelId&&o.CarrierId==null&&WorldSimulation.Distance(o.X,o.Z,child.X,child.Z)<=NaturalResources.Reach(child));
                accepted=child.Interaction.GroundIntakeSourceId==parcelId&&!child.Interaction.NaturalIntakePending&&child.Interaction.EatingRemaining>0;
            }
        }
        Require(carried&&placed&&accepted,"dispenser portion did not complete real family transport and mouth entry");
        Require(Physical(s,outlet.Kind)==initial-1,"family transport created or refunded a portion");SaveStore.Validate(s.State);
    }
});

Check("empty feedback, blocked output and no creature target keep warehouse stock intact",()=>
{
    var s=Stocked(SupplyStationKind.Food,false);var station=Station(s);int stored=station.StoredPortions;
    Require(s.ExecuteInteraction(s.Selected!.Id,new(InteractionAction.GiveBall,X:0,Z:.85)).Success,"blocking real ball placement failed");
    Seconds(s,.25);Require(station.StoredPortions==stored&&station.OutletObjectId==null&&station.LastEvent.Contains("占用"),"blocked outlet lost a portion");
    var ball=s.State.RoomObjects.Single();Require(s.PutAwayRoomObject(ball.Id),"ball removal failed");Seconds(s,.25);
    Require(Outlet(s).Portions==1&&station.StoredPortions==stored-1,"cleared outlet did not resume");
    s.State.SelectedId=null;Require(s.BuySupplyStation(SupplyStationKind.Water)&&s.PlaceSupplyStation(SupplyStationKind.Water,-3,2).Success,"empty selection blocked device placement");
    var empty=s.State.SupplyStations.Single(t=>t.Kind==SupplyStationKind.Water);Require(empty.StoredPortions==0&&empty.OutletObjectId==null&&empty.LastEvent.Contains("空仓"),"empty device fabricated water");
    var input=Input(empty);Require(!s.BeginSupplyStationGesture(empty.Id,input).Success,"refill without real bulk water succeeded");
    s.State.Settings.Pause=true;Require(s.BuyStationWater(),"paused supply menu could not purchase");string before=Snapshot(s);
    Require(!s.BeginSupplyStationGesture(empty.Id,input).Success&&Snapshot(s)==before,"paused actual refill changed stock");
});

Environment.ExitCode=failures==0?0:1;

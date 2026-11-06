using System.Text.Json;
using Yukkuri.Simulation;

// Real purchase/place/refill/dispense and drag APIs. Initial creature positions
// isolate the outlet; no Godot/native input or personal saves are used.
int failures=0,passed=0;
string evidence=args.Length>0?Path.GetFullPath(args[0]):Path.Combine(AppContext.BaseDirectory,"evidence");
Directory.CreateDirectory(evidence);
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{
    try{body();passed++;Console.WriteLine(JsonSerializer.Serialize(new{name,result="PASS"}));}
    catch(Exception ex){failures++;Console.WriteLine(JsonSerializer.Serialize(new{name,result="FAIL",error=ex.Message}));}
}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
string? Invalid(WorldSimulation s){try{SaveStore.Validate(s.State);return null;}catch(InvalidDataException ex){return ex.Message;}}
void Seconds(WorldSimulation s,double seconds){for(int n=0;n<(int)Math.Round(seconds/.05);n++){s.AdvanceInteractionPresentation(.05);s.Advance(.05);}}
WorldSimulation Stocked(SupplyStationKind kind)
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
    s.SetAutonomousBreeding(false);
    Require(s.BuySupplyStation(kind),"purchase");Require(s.PlaceSupplyStation(kind,0,0).Success,"place");
    if(kind==SupplyStationKind.Water)Require(s.BuyStationWater(),"buy water");
    var t=s.State.SupplyStations.Single();
    var input=new InteractionInput(InteractionAction.RefillSupplyStation,BodyPart.Side,Duration:.1,X:t.X,Z:t.Z,ObjectId:t.Id,TargetKind:InteractionTargetKind.SupplyStation);
    Require(s.BeginSupplyStationGesture(t.Id,input).Success,"begin refill");
    for(int n=0;n<16;n++)Require(s.ContinueInteractionGesture(input,true).Success,"refill contact");
    Require(s.InteractionCompletionProgress==1,"refill incomplete");s.EndInteractionGesture();
    Seconds(s,.25);Require(t.OutletObjectId!=null,"no outlet");SaveStore.Validate(s.State);return s;
}
SupplyStationState Station(WorldSimulation s)=>s.State.SupplyStations.Single();
RoomObject Outlet(WorldSimulation s)=>s.State.RoomObjects.Single(o=>o.Id==Station(s).OutletObjectId);
int Physical(WorldSimulation s,RoomObjectKind kind)=>Station(s).StoredPortions+s.State.RoomObjects.Where(o=>o.Kind==kind&&!o.Stored).Sum(o=>o.Portions);
void Roundtrip(WorldSimulation s,string name)
{
    var store=new SaveStore(Path.Combine(evidence,name));store.Save(s.State,0);
    var loaded=store.Load(0);Require(!loaded.RecoveredBackup,"unexpected backup");SaveStore.Validate(loaded.State);
    Require(JsonSerializer.Serialize(loaded.State,SaveStore.JsonOptions)==Snapshot(s),"roundtrip changed physical state");
}
foreach(var kind in Enum.GetValues<SupplyStationKind>())
{
    Check(kind+" full move immediately validates, saves and conserves paid supply",()=>{
        var s=Stocked(kind);var t=Station(s);var o=Outlet(s);int physical=Physical(s,o.Kind),stock=t.StoredPortions,money=s.State.Money;long sequence=t.OutputSequence;
        Require(s.MoveLooseObject(o.Id,2,o.Z)&&o.X==2,"full move failed");
        string? invalid=Invalid(s);bool saveFailed=false;string? saveError=null;
        try{new SaveStore(Path.Combine(evidence,kind+"-immediate")).Save(s.State,0);}catch(InvalidDataException ex){saveFailed=true;saveError=ex.Message;}
        s.State.Settings.Pause=true;double now=s.State.WorldSeconds;Seconds(s,1);
        string? pausedInvalid=Invalid(s);Require(s.State.WorldSeconds==now,"paused time changed");
        s.State.Settings.Pause=false;s.State.Mode=PlayMode.CloseUp;Seconds(s,1);string? closeInvalid=Invalid(s);
        s.State.Mode=PlayMode.Management;
        Console.WriteLine(JsonSerializer.Serialize(new{diagnostic=kind.ToString(),invalid,saveFailed,saveError,pausedInvalid,closeInvalid,staleId=t.OutletObjectId,actualX=o.X,actualZ=o.Z}));
        Require(Physical(s,o.Kind)==physical&&t.StoredPortions==stock&&s.State.Money==money&&t.OutputSequence==sequence&&o.Portions==1,"drag changed paid supply");
        Seconds(s,.25);SaveStore.Validate(s.State);
        Require(t.OutletObjectId!=o.Id&&t.StoredPortions==stock-1&&Physical(s,o.Kind)==physical&&s.State.RoomObjects.Count(x=>x.Kind==o.Kind)==2&&o.Portions==1,"next tick lost/duplicated displaced portion");
        Console.WriteLine(JsonSerializer.Serialize(new{diagnostic=kind+" next normal tick",valid=Invalid(s)==null,total=Physical(s,o.Kind)}));
        Require(invalid==null&&!saveFailed&&pausedInvalid==null&&closeInvalid==null,"displaced outlet invalid before next tick");
        Roundtrip(s,kind+"-next-tick");
    });
    Check(kind+" immediate paused save/load continues deterministically",()=>{
        var s=Stocked(kind);var o=Outlet(s);Require(s.MoveLooseObject(o.Id,2,o.Z),"move");
        s.State.Settings.Pause=true;SaveStore.Validate(s.State);Roundtrip(s,kind+"-paused");
        var store=new SaveStore(Path.Combine(evidence,kind+"-paused"));
        var copy=new WorldSimulation(store.Load(0).State);s.State.Settings.Pause=copy.State.Settings.Pause=false;
        Seconds(s,.25);Seconds(copy,.25);Require(Snapshot(s)==Snapshot(copy),"save continuation diverged");
    });
    Check(kind+" partial obstacle movement releases only after actual displacement",()=>{
        var s=Stocked(kind);var o=Outlet(s);var t=Station(s);double z=o.Z;int total=Physical(s,o.Kind);
        s.State.RoomObjects.Add(new(){Id="drag-blocker",Kind=RoomObjectKind.Ball,RegionId=o.RegionId,X=1,Z=z});
        Require(s.MoveLooseObject(o.Id,2,z)&&o.X>0&&o.X<1,"expected partial movement");
        Require(t.OutletObjectId==null,"partial displacement retained outlet");SaveStore.Validate(s.State);
        Require(Physical(s,o.Kind)==total,"partial move changed supply");
    });
    Check(kind+" first-step obstruction retains outlet despite true return",()=>{
        var s=Stocked(kind);var o=Outlet(s);var t=Station(s);double x=o.X,z=o.Z;
        // Synthetic collision fixture tests loop semantics, not legal placement.
        s.State.RoomObjects.Add(new(){Id="first-step-blocker",Kind=RoomObjectKind.Ball,RegionId=o.RegionId,X=.5,Z=z});
        Require(s.MoveLooseObject(o.Id,2,z)&&o.X==x&&o.Z==z,"expected blocked first step");
        Require(t.OutletObjectId==o.Id,"unmoved outlet released");SaveStore.Validate(s.State);
    });
    foreach(var offset in new[]{(Name:"noop",X:0d,Z:0d,Keep:true),(Name:"inside",X:.0005,Z:0d,Keep:true),
        (Name:"outside",X:.0011,Z:0d,Keep:false),(Name:"diagonal-inside",X:.0006,Z:.0006,Keep:true),
        (Name:"diagonal-outside",X:.0008,Z:.0008,Keep:false),(Name:"exact-axis-threshold",X:.001,Z:0d,Keep:false)})
    Check(kind+" threshold "+offset.Name,()=>{
        var s=Stocked(kind);var o=Outlet(s);var t=Station(s);int total=Physical(s,o.Kind);
        Require(s.MoveLooseObject(o.Id,o.X+offset.X,o.Z+offset.Z),"threshold move rejected");
        Require((t.OutletObjectId==o.Id)==offset.Keep,"ownership disagrees with actual outlet distance");
        Require(Physical(s,o.Kind)==total,"threshold changed portions");SaveStore.Validate(s.State);
    });
    Check(kind+" rejected moves preserve complete snapshot",()=>{
        var s=Stocked(kind);var o=Outlet(s);
        foreach(var target in new[]{(X:double.NaN,Z:0d),(X:double.PositiveInfinity,Z:0d),(X:8d,Z:0d),(X:0d,Z:0d)})
        {string before=Snapshot(s);Require(!s.MoveLooseObject(o.Id,target.X,target.Z)&&Snapshot(s)==before,"invalid move mutated");}
        string unchanged=Snapshot(s);Require(!s.MoveLooseObject("missing",2,2)&&Snapshot(s)==unchanged,"unknown id mutated");
        s.State.Settings.Pause=true;unchanged=Snapshot(s);Require(!s.MoveLooseObject(o.Id,2,2)&&Snapshot(s)==unchanged,"paused move mutated");
        s.State.Settings.Pause=false;s.State.Regions.Add(new(){Id="other",Name="other",Kind=RegionKind.Sanctuary});s.State.ActiveRegionId="other";s.State.SelectedId=null;
        unchanged=Snapshot(s);Require(!s.MoveLooseObject(o.Id,2,2)&&Snapshot(s)==unchanged,"cross-region move mutated");
        s.State.ActiveRegionId=o.RegionId;o.Stored=true;unchanged=Snapshot(s);
        Require(!s.MoveLooseObject(o.Id,2,2)&&Snapshot(s)==unchanged,"stored object moved");
    });
    Check(kind+" ordinary non-outlet and held-then-released supply keep existing semantics",()=>{
        var s=Stocked(kind);var o=Outlet(s);var t=Station(s);
        s.State.RoomObjects.Add(new(){Id="ordinary",Kind=RoomObjectKind.Ball,RegionId=o.RegionId,X=3,Z=3});
        Require(s.MoveLooseObject("ordinary",4,3)&&t.OutletObjectId==o.Id,"ordinary drag changed owner");
        int total=Physical(s,o.Kind);Require(s.HoldSupply(o.Id)&&t.OutletObjectId==null,"hold did not detach");
        Require(s.ReleaseSupply(o.Id,2,2)&&s.MoveLooseObject(o.Id,3,2),"released supply drag");
        Require(t.OutletObjectId==null&&Physical(s,o.Kind)==total,"released independent supply rebound");SaveStore.Validate(s.State);
    });
}
Console.WriteLine(JsonSerializer.Serialize(new{summary=true,passed,failures}));
Environment.ExitCode=failures==0?0:1;
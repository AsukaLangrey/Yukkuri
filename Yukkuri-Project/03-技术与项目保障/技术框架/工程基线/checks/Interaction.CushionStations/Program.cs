using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;

// Component fixtures isolate station-body occupancy. Station purchase/placement,
// carry, release and landing use production commands. Initial spatial arrangements
// and legacy-overlap snapshots are explicit fixtures, not gameplay journey claims.
int passed=0,failed=0;
const double Epsilon=.00001;
double halfX=InteractionFixtureGeometry.CushionWidth*.5;
double halfZ=InteractionFixtureGeometry.CushionDepth*.5;
double limitX=halfX+WorldSimulation.SupplyStationRadius;
double limitZ=halfZ+WorldSimulation.SupplyStationRadius;
void Require(bool value,string why){if(!value)throw new Exception(why);}
void Check(string name,Action test)
{
    try{test();passed++;Console.WriteLine("PASS "+name);}
    catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+ex.Message);}
}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation s)
{
    SaveStore.Validate(s.State);
    var state=JsonSerializer.Deserialize<WorldState>(Snapshot(s),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
RoomObject Pad(WorldSimulation s)=>s.State.RoomObjects.Single(o=>o.Kind==RoomObjectKind.Cushion);
WorldSimulation Fresh(double x=-3.8,double z=-3.4)
{
    var s=WorldSimulation.CreateInteractionPlaytest(20261106);
    var pad=s.State.RoomObjects.First(o=>o.Kind==RoomObjectKind.Cushion);
    s.State.RoomObjects.RemoveAll(o=>o!=pad);pad.X=x;pad.Z=z;
    for(int n=0;n<s.State.Creatures.Count;n++)
    {
        var c=s.State.Creatures[n];c.X=c.TargetX=-5+n*4;c.Z=c.TargetZ=5;
        c.Interaction.NextAutonomyAt=c.DecisionIn=c.Family.NextCareAt=c.Family.NextSocialAt=10000;
    }
    return s;
}
void Place(WorldSimulation s,SupplyStationKind kind,double x=0,double z=-3)
{
    Require(s.BuySupplyStation(kind),"purchase failed");
    Require(s.PlaceSupplyStation(kind,x,z).Success,"legal station placement failed");
}
bool Overlaps(RoomObject pad,SupplyStationState station)=>
    Math.Abs(pad.X-station.X)<limitX&&Math.Abs(pad.Z-station.Z)<limitZ;
void Land(WorldSimulation s,RoomObject pad,Vector2 velocity=default)
{
    s.ReleasePortableCarry(pad.Id,velocity);
    for(int n=0;n<80&&s.PortableStatus(pad.Id)!=null;n++)s.AdvanceInteractionPresentation(.05);
    Require(s.PortableStatus(pad.Id)==null&&!pad.HeldByHand,"real release did not land");
    SaveStore.Validate(s.State);
}
foreach(var kind in Enum.GetValues<SupplyStationKind>())
{
    Check($"{kind}: original default-pad overlap blocked through real release",()=>
    {
        var s=WorldSimulation.CreateInteractionPlaytest(20261106);
        var pad=s.State.RoomObjects.First(o=>o.Kind==RoomObjectKind.Cushion&&o.X<0);
        Place(s,kind,-1.8,-3.4);var station=s.State.SupplyStations.Single();
        Require(s.BeginPortableCarry(pad.Id),"carry failed");
        Require(!s.MovePortableCarry(pad.Id,-2.6,-3.4,.15),"overlapping endpoint accepted");
        Require(pad.X==-3.8&&pad.Z==-3.4,"blocked endpoint displaced pad");
        Land(s,pad);Require(!Overlaps(pad,station),"overlap survived real landing");
    });
    foreach(var side in new[]{(-1,0),(1,0),(0,-1),(0,1)})
    {
        double dx=side.Item1,dz=side.Item2;
        Check($"{kind}: side {side} inside rejected, contact and outside accepted",()=>
        {
            var s=Fresh(dx*2.5,-3+dz*2.5);var pad=Pad(s);Place(s,kind);
            double tx=dx*limitX,tz=-3+dz*limitZ;
            Require(!s.MoveLooseObject(pad.Id,tx-dx*Epsilon,tz-dz*Epsilon),"inside accepted");
            Require(s.MoveLooseObject(pad.Id,tx+dx*Epsilon,tz+dz*Epsilon),"outside rejected");
            Require(Math.Abs(pad.X-(tx+dx*Epsilon))<1e-9&&Math.Abs(pad.Z-(tz+dz*Epsilon))<1e-9,"outside not reached");
            Require(s.MoveLooseObject(pad.Id,tx,tz),"exact contact rejected");
            Require(Math.Abs(pad.X-tx)<1e-9&&Math.Abs(pad.Z-tz)<1e-9,"exact contact not reached");
            Require(s.MoveLooseObject(pad.Id,dx*2.5,-3+dz*2.5),"cannot leave blocked boundary");
            Require(!Overlaps(pad,s.State.SupplyStations.Single()),"boundary escape overlaps");
            SaveStore.Validate(s.State);
        });
    }
    foreach(var corner in new[]{(-1,-1),(-1,1),(1,-1),(1,1)})
    {
        double dx=corner.Item1,dz=corner.Item2;
        Check($"{kind}: corner {corner} preserves existing expanded-rectangle rule",()=>
        {
            var s=Fresh(dx*2.5,-3+dz*2.5);var pad=Pad(s);Place(s,kind);
            // This corner lies outside the physical rounded corner but inside the
            // existing placement envelope: do not silently replace its geometry.
            Require(!s.MoveLooseObject(pad.Id,dx*(limitX-Epsilon),-3+dz*(limitZ-Epsilon)),"inside corner accepted");
            Require(s.MoveLooseObject(pad.Id,dx*(limitX+Epsilon),-3+dz*(limitZ-Epsilon)),"outside corner rejected");
            Require(!Overlaps(pad,s.State.SupplyStations.Single()),"outside corner overlaps");
            SaveStore.Validate(s.State);
        });
    }
    Check($"{kind}: legal endpoint across station stops at first blocked sample",()=>
    {
        var s=Fresh(-2.5,-3);var pad=Pad(s);Place(s,kind);
        Require(s.BeginPortableCarry(pad.Id),"carry failed");
        Require(s.MovePortableCarry(pad.Id,2.5,-3,.15),"partial-path command rejected");
        Require(pad.X>-2.5&&pad.X<=-limitX&&pad.X>-limitX-.081,"did not stop at near boundary");
        Require(!Overlaps(pad,s.State.SupplyStations.Single()),"partial path entered station");
        Require(s.MovePortableCarry(pad.Id,-2.5,-3,.15),"cannot retreat after partial path");
        Land(s,pad);Require(Math.Abs(pad.X+2.5)<1e-6,"retreat failed");
    });
    Check($"{kind}: thrown cushion cannot pass through station",()=>
    {
        var s=Fresh(-2.5,-3);var pad=Pad(s);Place(s,kind);
        Require(s.BeginPortableCarry(pad.Id),"carry failed");
        Land(s,pad,new Vector2(10,0));
        Require(pad.X>-2.5&&pad.X<=-limitX&&!Overlaps(pad,s.State.SupplyStations.Single()),"throw crossed station");
    });
    Check($"{kind}: legacy overlap reload permits strictly shallower small steps only",()=>
    {
        var setup=Fresh(-2.5,-3);Place(setup,kind);Pad(setup).X=-.8;
        var s=Reload(setup);var pad=Pad(s);var station=s.State.SupplyStations.Single();
        Require(Overlaps(pad,station),"legacy overlap fixture lost");
        Require(s.BeginPortableCarry(pad.Id),"legacy carry failed");
        Require(!s.MovePortableCarry(pad.Id,-.79,-3,.15)&&pad.X==-.8,"deeper move accepted");
        Require(!s.MovePortableCarry(pad.Id,-.8,-2.99,.15)&&pad.Z==-3,"equal-depth lateral move accepted");
        for(int n=1;n<=12;n++)Require(s.MovePortableCarry(pad.Id,-.8-n*.05,-3,.15),"small-step withdrawal blocked");
        Require(Math.Abs(pad.X+1.4)<1e-9&&!Overlaps(pad,station),"did not escape");
        Require(!s.MovePortableCarry(pad.Id,-1.29,-3,.15),"reentry accepted after escape");
        Land(s,pad);Require(!Overlaps(pad,station),"escaped pad overlaps after release");
        var reload=Reload(s);Require(!Overlaps(Pad(reload),reload.State.SupplyStations.Single()),"save round-trip restored overlap");
    });
    Check($"{kind}: legacy overlap cannot cross center to opposite legal endpoint",()=>
    {
        var setup=Fresh(-2.5,-3);Place(setup,kind);Pad(setup).X=-.8;
        var s=Reload(setup);var pad=Pad(s);
        s.MoveLooseObject(pad.Id,2.5,-3);
        Require(pad.X==-.8&&pad.Z==-3,"crossed deeper through existing overlap");
    });
    Check($"{kind}: legacy escape cannot enter another station",()=>
    {
        var s=Fresh(-5,-3);Place(s,kind,0,-3);Place(s,kind,-2,-3);var pad=Pad(s);pad.X=-.8;
        s=Reload(s);pad=Pad(s);double before=pad.X;
        Require(!s.MoveLooseObject(pad.Id,-.85,-3)&&pad.X==before,"escape deepened overlap with second station");
    });
    Check($"{kind}: first blocked path sample preserves position and return convention",()=>
    {
        var s=Fresh(-2.5,-3);Place(s,kind);var pad=Pad(s);
        Require(s.MoveLooseObject(pad.Id,-limitX-Epsilon,-3),"boundary approach failed");
        double before=pad.X;
        Require(s.MoveLooseObject(pad.Id,2.5,-3),"partial command return convention changed");
        Require(pad.X==before&&pad.Z==-3,"first blocked sample moved pad");
        SaveStore.Validate(s.State);
    });
    Check($"{kind}: legacy escape never enters a previously clear second station",()=>
    {
        var setup=Fresh(-5,-5);Place(setup,kind,0,-3);Place(setup,kind,-2.5,-3);Pad(setup).X=-.8;Pad(setup).Z=-3;
        var s=Reload(setup);var pad=Pad(s);var second=s.State.SupplyStations.Last();
        Require(!Overlaps(pad,second),"second station starts overlapping");
        Require(s.BeginPortableCarry(pad.Id),"legacy carry failed");
        for(int n=1;n<=7;n++)Require(s.MovePortableCarry(pad.Id,-.8-n*.05,-3,.15),"clear small step rejected");
        double before=pad.X;
        Require(!s.MovePortableCarry(pad.Id,-1.25,-3,.15)&&pad.X==before&&!Overlaps(pad,second),"entered new station while escaping old one");
        Land(s,pad);Require(!Overlaps(pad,second),"release entered second station");
    });    Check($"{kind}: reverse station placement still rejects same pad overlap",()=>
    {
        var s=Fresh(-2.6,-3.4);Require(s.BuySupplyStation(kind),"purchase failed");
        string before=Snapshot(s);
        Require(!s.CanPlaceSupplyStationAt(kind,-1.8,-3.4)&&!s.PlaceSupplyStation(kind,-1.8,-3.4).Success,"reverse placement accepted");
        Require(Snapshot(s)==before,"rejected reverse placement changed state");
    });
}
Check("no-op, pause, invalid and missing commands preserve world state",()=>
{
    var s=Fresh(-2.5,-3);var pad=Pad(s);Place(s,SupplyStationKind.Food);
    string before=Snapshot(s);Require(s.MoveLooseObject(pad.Id,pad.X,pad.Z)&&Snapshot(s)==before,"valid no-op changed state");
    Require(!s.MoveLooseObject(pad.Id,double.NaN,-3)&&!s.MoveLooseObject(pad.Id,0,double.PositiveInfinity)&&
        !s.MoveLooseObject("missing",0,-3)&&Snapshot(s)==before,"invalid command changed state");
    s.State.Settings.Pause=true;before=Snapshot(s);
    Require(!s.MoveLooseObject(pad.Id,-2.4,-3)&&Snapshot(s)==before,"pause mutated world");
});
Check("other-region station does not block cushion",()=>
{
    var s=Fresh(-2.5,-3);Place(s,SupplyStationKind.Food);s.State.Regions.Add(new(){Id="other-region",Name="Isolated fixture",Kind=RegionKind.Sanctuary});s.State.SupplyStations.Single().RegionId="other-region";
    var pad=Pad(s);Require(s.MoveLooseObject(pad.Id,-.8,-3)&&pad.X==-.8,"different region blocked pad");SaveStore.Validate(s.State);
});
Check("ordinary small items keep their existing station clearance",()=>
{
    foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Water,RoomObjectKind.Sweet,RoomObjectKind.Ball,RoomObjectKind.Treasure})
    {
        var s=Fresh(-5,-5);Place(s,SupplyStationKind.Food);
        var item=new RoomObject{Kind=kind,X=-2.5,Z=-3,RegionId=s.State.ActiveRegionId,Portions=1};s.State.RoomObjects.Add(item);
        Require(s.MoveLooseObject(item.Id,-.8,-3)&&item.X==-.8,"small item behavior changed: "+kind);
        SaveStore.Validate(s.State);
    }
});
Check("unrestrained body can still walk onto soft pad",()=>
{
    var s=WorldSimulation.CreateInteractionPlaytest(20261106);var pad=s.State.RoomObjects.First(o=>o.Kind==RoomObjectKind.Cushion&&o.X<0);
    var c=s.State.Creatures[0];Require(s.SlideAlongGround(c.Id,pad.X,pad.Z),"body could not walk onto pad");
    Require(Math.Abs(c.X-pad.X)<1e-6&&Math.Abs(c.Z-pad.Z)<1e-6,"body stopped outside pad");
    SaveStore.Validate(s.State);
});
Console.WriteLine(JsonSerializer.Serialize(new{check="Interaction.CushionStations",passed,failed}));
return failed==0?0:1;

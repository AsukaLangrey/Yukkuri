using System.Reflection;
using System.Text.Json;
using Yukkuri.Simulation;

// Finite rule checks, not a UI or complete economy progression test.
int failures=0,passed=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body){try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
var room=typeof(WorldSimulation).GetMethod("RoomMove",Private)!;
var occupancy=typeof(WorldSimulation).GetMethod("CanOccupyRoom",Private)!;
var recovery=typeof(WorldSimulation).GetMethod("TryRecoverShelterExteriorStep",Private);
var geometry=typeof(ShelterGeometry).GetMethod("ExteriorSideRecoveryClear");
bool Recover(WorldSimulation s,Creature c,double step=.05)=>(bool)(recovery?.Invoke(s,[c,step])??false);
bool Geometry(double ax,double az,double bx,double bz,double radius=.78,bool open=true)=>(bool)(geometry?.Invoke(null,[ax,az,bx,bz,radius,open])??false);
void Walk(WorldSimulation s,Creature c,double x,double z,double dt=.25,bool slide=true)=>room.Invoke(s,[c,x,z,dt,slide]);
void Tick(WorldSimulation s,double sec){for(int n=0;n<(int)Math.Round(sec/.05);n++){s.AdvanceInteractionPresentation(.05);s.Advance(.05);}}
WorldSimulation Reload(WorldSimulation s){SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(s.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;SaveStore.Validate(state);return new(state);}
(WorldSimulation,Creature) Journey(int side=1,bool control=false)
{
    var s=WorldSimulation.CreateCourtyardEconomy();var c=s.State.Creatures.Single(c=>c.IsYoung);
    // Only this economic prerequisite is synthetic; no claim of a fresh default unlock.
    s.State.CreatureSalesRevenue=500;
    // Matching synthetic ledger keeps this declared economic initial state save-valid.
    s.State.CreatureSales.Add(new(new Creature{Id="prior-sale",Name="Economic prerequisite",
        Body=new(){Id="prior-sale-body",SpeciesId="reimu"},Registration=new(){IdentityId="prior-sale-identity"}},500,0));
    Require(s.BuyShelter()&&s.PlaceShelter(0,3.5).Success,"paid public shelter placement");
    void Move(double x,double z){Require(s.SlideAlongGround(c.Id,x,z),"public movement rejected");Require(Math.Abs(c.X-x)<1e-7&&Math.Abs(c.Z-z)<1e-7,"public movement did not reach legal endpoint");}
    if(side<0){Move(3,6.7);Move(-3,6.7);Move(-3,3.5);}else Move(3,3.5);
    Move(side*(1.25+WorldSimulation.ShelterPassageRadius(c)+.01+(control?.5:0)),3.5);
    Require(s.CanPlaceInteractionAt(c.Id,c.X,c.Z),"young position not legal");
    // Accelerate age only, then let ordinary family ticking change the actual age class.
    c.Body.AgeSeconds=c.InnerAgeSeconds=FamilyRules.YoungSeconds-.01;
    Tick(s,.25);Require(!c.IsYoung,"ordinary growth not completed");return (s,c);
}
(WorldSimulation,Creature) Boundary()
{
    var (s,c)=Journey();
    // Synthetic exact boundary arrangement: keep separate from public growth journeys.
    c.X=c.TargetX=1.8;c.Z=c.TargetZ=3.5;c.Interaction.Height=0;
    c.Interaction.VelocityX=c.Interaction.VelocityZ=c.Interaction.VerticalVelocity=0;
    return (s,c);
}
foreach(int side in new[]{1,-1})foreach(bool water in new[]{false,true})foreach(bool reload in new[]{false,true})
Check($"public growth autonomous {(water?"water":"food")} side={side} reload={reload}",()=>
{
    var (s,c)=Journey(side);string id=c.Id;
    if(reload){s=Reload(s);c=s.State.Creatures.Single(x=>x.Id==id);}
    // Synthetic needs isolate actual autonomous consumption, without replacing its steps.
    c.Fullness=water?90:20;c.Interaction.Hydration=water?20:90;
    Require(s.PlaceInteractionSupply(water?InteractionAction.PlaceWater:InteractionAction.PlaceMeal,side*4.3,3.5).Success,"supply placement");
    double ox=c.X,oz=c.Z;Tick(s,60);
    Require(double.Hypot(c.X-ox,c.Z-oz)>.25,"growth left autonomous body stuck");
    Require((water?c.Interaction.Hydration:c.Fullness)>40,"autonomous need remained unmet");
    Require(s.State.Shelters.All(h=>ShelterGeometry.FitsLocalCircle(c.X-h.X,c.Z-h.Z,WorldSimulation.ShelterPassageRadius(c),h.DoorOpen)),"finished overlapping wall");
});
foreach(bool water in new[]{false,true})Check("unobstructed public growth control "+water,()=>
{
    var(s,c)=Journey(control:true);c.Fullness=water?90:20;c.Interaction.Hydration=water?20:90;
    Require(s.PlaceInteractionSupply(water?InteractionAction.PlaceWater:InteractionAction.PlaceMeal,4.3,3.5).Success,"control supply");Tick(s,60);
    Require((water?c.Interaction.Hydration:c.Fullness)>40,"unobstructed control failed");
});
Check("six argument strict occupancy contract retained",()=>{Require(occupancy.GetParameters().Length==6,"arity changed");var(s,c)=Boundary();Require(!(bool)occupancy.Invoke(s,[c,c.X,c.Z,null,null,false])!,"strict placement relaxed");});
Check("both exterior normal directions recover",()=>{Require(Geometry(1.8,0,1.85,0)&&Geometry(-1.8,0,-1.85,0),"outward normals rejected");});
Check("inside wall centre tangent inward and tunnel rejected",()=>
{
    foreach(var p in new[]{(.8,0.0,.85,0.0),(1.2,0.0,1.3,0.0),(1.8,0.0,1.8,.1),(1.8,0.0,1.7,0.0),(1.8,0.0,-2.5,0.0)})
        Require(!Geometry(p.Item1,p.Item2,p.Item3,p.Item4),"illegal geometry admitted");
});
Check("front rear corners and rear wall remain strict",()=>
{
    foreach(double z in new[]{1.25,-1.25,1.4,-1.4})Require(!Geometry(1.8,z,1.9,z),"corner second-wall sweep bypassed");
    Require(!Geometry(0,-1.8,.1,-1.8),"rear wall recovery invented");
});
Check("closed door cannot be crossed or escaped",()=>{Require(!Geometry(0,1.6,0,1.8,.78,false),"closed-door bypass");Require(!ShelterGeometry.SweptCircleClear(0,0,0,3,.78,false),"strict closed-door sweep changed");});
Check("closed-door shelter still allows exterior side normal",()=>{var(s,c)=Boundary();s.State.Shelters.Single().DoorOpen=false;Require(Recover(s,c),"irrelevant closed door blocked exterior recovery");});
Check("ordinary inward goal gets only bounded outward recovery",()=>
{
    var(s,c)=Boundary();double x=c.X,z=c.Z;Walk(s,c,0,3.5);Require(c.X>x&&Math.Abs(c.Z-z)<1e-9,"not outward");Require(c.X-x<=s.Config.MoveSpeed*.25+1e-9,"double frame budget");
});
Check("tiny frame budget is not enlarged",()=>{var(s,c)=Boundary();double x=c.X;Walk(s,c,0,3.5,.001);Require(c.X>x&&c.X-x<=s.Config.MoveSpeed*.001+1e-9,"tiny frame enlarged");});
Check("door traversal slide false receives no recovery",()=>{var(s,c)=Boundary();double x=c.X,z=c.Z;Walk(s,c,4,3.5,.25,false);Require(c.X==x&&c.Z==z,"door traversal recovered");});
Check("manual dragging and placement remain strict",()=>{var(s,c)=Boundary();double x=c.X,z=c.Z;s.SlideAlongGround(c.Id,4,3.5);Require(c.X==x&&c.Z==z&&!s.CanPlaceInteractionAt(c.Id,4,3.5),"manual path relaxed");});
Check("second shelter blocks outward recovery",()=>{var(s,c)=Boundary();s.State.Shelters.Add(new(){Id="boundary-second",RegionId=c.RegionId,X=3.8,Z=3.5});double x=c.X;Require(!Recover(s,c)&&c.X==x,"opposite shelter wall bypassed");});
Check("neighbor body blocks recovery",()=>{var(s,c)=Boundary();var other=s.State.Creatures.First(x=>x!=c);other.X=c.X+WorldSimulation.InteractionFootprint(c)+WorldSimulation.InteractionFootprint(other)+.01;other.Z=c.Z;double x=c.X;Require(!Recover(s,c)&&c.X==x,"other body bypassed");});
Check("station blocks recovery",()=>{var(s,c)=Boundary();s.State.SupplyStations.Add(new(){Id="boundary-station",RegionId=c.RegionId,X=c.X+WorldSimulation.InteractionFootprint(c)+WorldSimulation.SupplyStationRadius+.01,Z=c.Z});double x=c.X;Require(!Recover(s,c)&&c.X==x,"station bypassed");});
Check("reservations and interior occupancy stay strict",()=>{var(s,c)=Boundary();var h=s.State.Shelters.Single();var guest=s.State.Creatures.First(x=>x!=c);guest.X=0;guest.Z=3.5;h.GuestId=guest.Id;h.Phase=ShelterVisitPhase.Inside;Require(!(bool)occupancy.Invoke(s,[c,0.0,3.5,null,null,false])!,"reservation bypassed");Require(Recover(s,c)&&h.GuestId==guest.Id,"exterior recovery changed reservation");});
foreach(string guard in new[]{"height","velocityX","velocityZ","verticalVelocity","carried","falling","supine","restraint","rolling"})Check("non-walking guard "+guard,()=>
{
    var(s,c)=Boundary();switch(guard){case "height":c.Interaction.Height=.1;break;case "velocityX":c.Interaction.VelocityX=.1;break;case "velocityZ":c.Interaction.VelocityZ=.1;break;case "verticalVelocity":c.Interaction.VerticalVelocity=.1;break;case "carried":c.Interaction.Carried=true;break;case "falling":c.Interaction.Falling=true;break;case "supine":c.Interaction.Supine=true;break;case "restraint":c.Interaction.Restraint=RestraintKind.Box;break;case "rolling":typeof(InteractionLife).GetMethod("BeginRoll",Private)!.Invoke(c.Interaction,null);break;}
    double x=c.X,z=c.Z;Require(!Recover(s,c)&&c.X==x&&c.Z==z,"nonwalking recovered");
});
Check("pause and close-up freeze growth recovery",()=>{foreach(bool close in new[]{false,true}){var(s,c)=Boundary();double x=c.X,z=c.Z;if(close)s.State.Mode=PlayMode.CloseUp;else s.State.Settings.Pause=true;Tick(s,2);Require(c.X==x&&c.Z==z,"frozen world moved");}});
foreach(int target in new[]{0,1,2})Check("persistent target never reenters wall "+target,()=>
{
    var(s,c)=Boundary();for(int n=0;n<200;n++){double x=c.X,z=c.Z;Walk(s,c,target==0?0:target==1?c.X:4,target==1?6:3.5);double travel=double.Hypot(c.X-x,c.Z-z);Require(travel<=s.Config.MoveSpeed*.25+1e-8,"frame budget");Require(c.X>=x-1e-8||ShelterGeometry.FitsLocalCircle(c.X,c.Z-3.5,WorldSimulation.ShelterPassageRadius(c),true),"reentered overlap");}Require(ShelterGeometry.FitsLocalCircle(c.X,c.Z-3.5,WorldSimulation.ShelterPassageRadius(c),true),"not recovered");
});
Console.WriteLine(JsonSerializer.Serialize(new{passed,failed=failures}));Environment.ExitCode=failures==0?0:1;

using System.Reflection;
using System.Text.Json;
using Yukkuri.Simulation;
int passed=0,failed=0;
void Require(bool yes,string why){if(!yes)throw new Exception(why);}
void Check(string name,Action test){try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
bool Call(WorldSimulation s,string name,params object?[] args)=>(bool)typeof(WorldSimulation).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(s,args)!;
(WorldSimulation s,Creature c) Natural(bool grow=true){var s=WorldSimulation.CreateCourtyardEconomy();var c=s.State.Creatures.Single(c=>c.IsYoung);
foreach(var (x,z) in new[]{(5.76,.4),(5.76,0.0)}){Require(s.SlideAlongGround(c.Id,x,z),"public placement rejected");Require(Math.Abs(c.X-x)<1e-8&&Math.Abs(c.Z-z)<1e-8,"waypoint blocked");}
Require(WoodlandLayout.GroundFits(s.State,c.RegionId,c.X,c.Z,WorldSimulation.InteractionFootprint(c)),"young invalid");
if(grow)c.Body.AgeSeconds=FamilyRules.YoungSeconds-.25;
s.SetGroundHandTarget(c.Id);s.Advance(.25);s.SetGroundHandTarget(null);Require(c.IsYoung!=grow,"age transition");return(s,c);}
Check("original strict six-parameter method remains unique",()=>{var a=typeof(WorldSimulation).GetMethods(BindingFlags.Instance|BindingFlags.NonPublic).Where(m=>m.Name=="CanOccupyRoom").ToArray();Require(a.Length==1&&a[0].GetParameters().Length==6,"strict contract changed");});
foreach(bool grow in new[]{false,true})foreach(bool reload in new[]{false,true})Check($"natural age / public autonomous food-water grow={grow} reload={reload}",()=>{
var(s,c)=Natural(grow);if(reload){SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(s.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;s=new(state);c=s.State.Creatures.Single(x=>x.Id==c.Id);}
double ox=c.X,oz=c.Z,f=c.Fullness,w=c.Interaction.Hydration;bool ate=false,drank=false,moved=false;
for(int n=0;n<24000;n++){s.AdvanceInteractionPresentation(.05);s.Advance(.05);ate|=c.Fullness>f+.1;drank|=c.Interaction.Hydration>w+.1;moved|=Math.Abs(c.X-ox)+Math.Abs(c.Z-oz)>.5;f=c.Fullness;w=c.Interaction.Hydration;}
Require(ate&&drank&&moved,"no actual autonomous food/water increase or movement");Require(c.Health>0,"died");SaveStore.Validate(s.State);});
Check("strict placement rejects deficit while recovery step allows progress",()=>{var(s,c)=Natural();Require(!Call(s,"CanOccupyRoom",c,c.X-.01,c.Z,null,null,false),"strict accepts deficit");Require(Call(s,"CanTakeGroundStep",c,c.X-.01,c.Z,true,false),"recovery denied");Require(!Call(s,"CanTakeGroundStep",c,c.X-.01,c.Z,false,false),"non-slide recovery");});
foreach(double dz in new[]{-.06,-.001,0,.001,.06})Check($"landward recovery dz={dz}",()=>{var(s,c)=Natural();Require(Call(s,"CanRecoverRiverbankStep",c,c.X-.02,c.Z+dz),"landward rejected");});
foreach(double dz in new[]{-.06,.06})Check($"endpoint improvement hides interior regression dz={dz}",()=>{var(s,c)=Natural();double mid=dz>0?Math.PI/.65:0;c.Z=mid-dz/2;c.X=WoodlandLayout.RiverEdge(c.Z)-.4;double dx=-.104*Math.Abs(dz)+1e-7;Require(c.X+dx-WoodlandLayout.RiverEdge(c.Z+dz)<c.X-WoodlandLayout.RiverEdge(c.Z),"bad adversarial fixture");Require(!Call(s,"CanRecoverRiverbankStep",c,c.X+dx,c.Z+dz),"interior regression accepted");});
foreach(double z in new[]{-7.0,0,7.0})Check($"long segment endpoints z={z}",()=>{var(s,c)=Natural();c.Z=z;c.X=WoodlandLayout.RiverEdge(z)-.4;Require(Call(s,"CanRecoverRiverbankStep",c,c.X-2,-z),"large monotonically safe segment rejected");});
Check("zero and tangent steps rejected",()=>{var(s,c)=Natural();Require(!Call(s,"CanRecoverRiverbankStep",c,c.X,c.Z),"zero accepted");Require(!Call(s,"CanRecoverRiverbankStep",c,c.X+.104*.01,c.Z+.01),"outward tangent accepted");Require(Call(s,"CanRecoverRiverbankStep",c,c.X-1e-10,c.Z),"short landward denied");});
foreach(double offset in new[]{0.0,1e-10})Check($"center at bank versus in water {offset}",()=>{var(s,c)=Natural();c.X=WoodlandLayout.RiverEdge(c.Z)+offset;Require(Call(s,"CanRecoverRiverbankStep",c,c.X-.01,c.Z)==(offset==0),"center gate");});
Check("no recovery entitlement on valid footprint or after exit",()=>{var(s,c)=Natural();c.X=WoodlandLayout.RiverEdge(c.Z)-WorldSimulation.InteractionFootprint(c)*Math.Sqrt(1+.104*.104);Require(!Call(s,"CanRecoverRiverbankStep",c,c.X-.01,c.Z),"valid origin entitled");Require(!Call(s,"CanTakeGroundStep",c,c.X+.01,c.Z,true,false),"new deficit allowed");});
Check("no river no entitlement",()=>{var(s,c)=Natural();s.State.RoomObjects.RemoveAll(o=>o.Kind==RoomObjectKind.RiverWater);Require(!Call(s,"CanRecoverRiverbankStep",c,c.X-.01,c.Z),"no-river recovery");});
Check("body and station blocking retained",()=>{var(s,c)=Natural();var other=s.State.Creatures.First(o=>o!=c);other.X=c.X-.01;other.Z=c.Z;Require(!Call(s,"CanTakeGroundStep",c,c.X-.01,c.Z,true,false),"peer bypass");s.State.Creatures.Remove(other);s.State.SupplyStations.Add(new(){Id="synthetic",RegionId=c.RegionId,X=c.X-.01,Z=c.Z});Require(!Call(s,"CanTakeGroundStep",c,c.X-.01,c.Z,true,false),"station bypass");});
Check("room wall blocking retained",()=>{var(s,c)=Natural();c.Z=8-WorldSimulation.InteractionFootprint(c);Require(!Call(s,"CanTakeGroundStep",c,c.X-.02,c.Z+.01,true,false),"wall bypass");});
Check("tree blocking retained",()=>{var(s,c)=Natural();var t=WoodlandLayout.Trees[0];c.X=t.X+.1;c.Z=t.Z;Require(!Call(s,"CanTakeGroundStep",c,t.X,t.Z,true,false),"tree bypass");});
Check("allowWater behavior unchanged",()=>{var(s,c)=Natural();Require(Call(s,"CanTakeGroundStep",c,c.X+.01,c.Z,true,true)==Call(s,"CanOccupyRoom",c,c.X+.01,c.Z,null,null,true),"allowWater changed");});
foreach(double dz in new[]{-.06,.06})Check($"final clear endpoint still requires curve proof dz={dz}",()=>{var(s,c)=Natural();double mid=dz>0?Math.PI/.65:0;c.Z=mid-dz/2;double dx=-.104*Math.Abs(dz)+1e-7;double change=dx-WoodlandLayout.RiverEdge(c.Z+dz)+WoodlandLayout.RiverEdge(c.Z);Require(change<0,"fixture does not improve");c.X=WoodlandLayout.RiverEdge(c.Z)-WorldSimulation.InteractionFootprint(c)*Math.Sqrt(1+.104*.104)-change/2;Require(Call(s,"CanOccupyRoom",c,c.X+dx,c.Z+dz,null,null,false),"fixture endpoint not clear");Require(!Call(s,"CanTakeGroundStep",c,c.X+dx,c.Z+dz,true,false),"clear endpoint bypassed curve proof");});
Console.WriteLine($"RESULT passed={passed} failed={failed}");return failed==0?0:1;

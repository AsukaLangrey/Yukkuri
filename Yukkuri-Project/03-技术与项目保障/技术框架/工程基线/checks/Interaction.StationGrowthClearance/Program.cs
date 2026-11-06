using System.Reflection;
using System.Text.Json;
using Yukkuri.Simulation;
// Finite synthetic regression cases; run only on an independently authorized test host.
int failures=0;var results=new List<object>();
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action body){try{body();results.Add(new{name,passed=true,error=(string?)null});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.Message});Console.WriteLine("FAIL "+name+": "+e.Message);}}
var ground=typeof(WorldSimulation).GetMethod("GroundMove",BindingFlags.Instance|BindingFlags.NonPublic)!;
bool Move(WorldSimulation s,double x,double z,bool slide=false)=>(bool)ground.Invoke(s,new object[]{s.Selected!,x,z,slide,false})!;
WorldSimulation Fixture(bool grow=true,double sx=0,double sz=0,double dx=1,double dz=0,bool river=false,SupplyStationKind kind=SupplyStationKind.Food)
{
    var s=WorldSimulation.CreateInteractionPlaytest(20261108);var c=s.State.Creatures.Single(c=>c.IsYoung);
    s.State.Creatures.RemoveAll(x=>x.Id!=c.Id);s.State.SelectedId=c.Id;
    if(!river)s.State.RoomObjects.Clear();else s.State.RoomObjects.RemoveAll(x=>x.Kind!=RoomObjectKind.RiverWater);
    s.SetAutonomousBreeding(false);c.X=c.TargetX=sx+dx*2;c.Z=c.TargetZ=sz+dz*2;
    c.Body.AgeSeconds=c.InnerAgeSeconds=FamilyRules.YoungSeconds-.5;
    c.Fullness=c.Energy=c.Health=c.Interaction.Hydration=100;c.Family.Warmth=90;
    c.DecisionIn=c.Interaction.NextAutonomyAt=c.Family.NextCareAt=c.Family.NextSocialAt=10000;
    Require(s.BuySupplyStation(kind)&&s.PlaceSupplyStation(kind,sx,sz).Success,"public station placement");
    s.State.SupplyStations.Single().RefillIntervalSeconds=0;
    Require(s.CanPlaceInteractionAt(c.Id,sx+dx,sz+dz),"young target not legal");
    // Place the initial young body only after the public occupancy gate accepted it.
    s.MoveCreature(c.Id,sx+dx,sz+dz);
    Require(s.CanPlaceInteractionAt(c.Id,c.X,c.Z),"young start not legal");SaveStore.Validate(s.State);
    if(grow)Grow(s);return s;
}
void Grow(WorldSimulation s)
{
    var c=s.Selected!;double x=c.X,z=c.Z;
    for(int n=0;n<3;n++){s.AdvanceInteractionPresentation(.25);s.Advance(.25);}
    Require(!c.IsYoung&&Math.Abs(c.X-x)<1e-8&&Math.Abs(c.Z-z)<1e-8,"natural stationary growth failed");SaveStore.Validate(s.State);
}
Check("natural growth permits bounded outward steps but not static overlap placement",()=>
{
    var s=Fixture();var c=s.Selected!;
    Require(!s.CanPlaceInteractionAt(c.Id,1.05,0),"static placement relaxed");
    for(int n=0;n<5;n++){double x=c.X;Require(Move(s,x+.05,0)&&Math.Abs(c.X-x-.05)<1e-8,"outward step blocked or teleported");}
    Require(s.CanPlaceInteractionAt(c.Id,c.X,c.Z),"failed to fully clear station");
    Require(!Move(s,1,0)&&c.X>=WorldSimulation.InteractionFootprint(c)+WorldSimulation.SupplyStationRadius,"reentry crossed station");
});
Check("inward and centre-crossing recovery are blocked with slide disabled",()=>
{
    var s=Fixture();Require(!Move(s,.95,0)&&s.Selected!.X==1,"inward accepted");
    Require(!Move(s,-2,0)&&s.Selected!.X==1,"crossed centre");
});
Check("tangent outward progress is bounded with sliding enabled",()=>
{
    var s=Fixture();var c=s.Selected!;Require(Move(s,1,.05,true)&&c.Z==.05&&c.X==1,"tangent progress failed");
});
Check("every overlapping station must improve and new station overlap stays blocked",()=>
{
    var s=Fixture(false);Require(s.BuySupplyStation(SupplyStationKind.Water)&&s.PlaceSupplyStation(SupplyStationKind.Water,2,0).Success,"second legal station");Grow(s);
    Require(!Move(s,1.05,0)&&s.Selected!.X==1,"recovery deepened other station");
    Require(Move(s,1,.05)&&s.Selected!.Z==.05,"outward from both failed");
    var t=Fixture();Require(t.BuySupplyStation(SupplyStationKind.Water)&&t.PlaceSupplyStation(SupplyStationKind.Water,2.2,0).Success,"nonoverlapping station");
    Require(!Move(t,1.05,0)&&t.Selected!.X==1,"entered new overlap");
});
Check("room wall remains strict while leaving growth overlap",()=>
{
    var s=Fixture(sx:6.2);Require(!Move(s,7.25,0)&&s.Selected!.X==7.2,"wall relaxed");
});
Check("river remains strict while leaving growth overlap",()=>
{
    double x=WoodlandLayout.RiverEdge(0)-WorldSimulation.InteractionFootprint(new Creature())*Math.Sqrt(1+.104*.104)-.01;
    var s=Fixture(sx:x-1,river:true);double before=s.Selected!.X;
    Require(!Move(s,before+.05,0)&&s.Selected!.X==before,"river relaxed");
});
Check("tree remains strict while leaving growth overlap",()=>
{
    var tree=WoodlandLayout.Trees[0];var s=Fixture(sx:tree.X+2,sz:tree.Z,dx:-1,river:true);double before=s.Selected!.X;
    Require(!Move(s,before-.05,s.Selected.Z)&&s.Selected.X==before,"tree relaxed");
});
Check("other body remains strict during station recovery",()=>
{
    var s=Fixture();var peer=WorldSimulation.CreateInteractionPlaytest().State.Creatures.First(c=>!c.IsYoung);
    peer.Id="clearance-peer";peer.X=2.58;peer.Z=0;peer.RegionId=s.Selected!.RegionId;s.State.Creatures.Add(peer);
    Require(!Move(s,1.05,0)&&s.Selected.X==1,"body collision relaxed");
});
Check("six argument occupancy reflection contract is preserved",()=>
{
    var method=typeof(WorldSimulation).GetMethod("CanOccupyRoom",BindingFlags.Instance|BindingFlags.NonPublic);
    Require(method!=null&&method.GetParameters().Length==6,"reflection contract changed");
});
Check("valid synthetic JSON reload has identical bounded recovery",()=>
{
    var s=Fixture();string json=JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
    var state=JsonSerializer.Deserialize<WorldState>(json,SaveStore.JsonOptions)!;SaveStore.Validate(state);var copy=new WorldSimulation(state);
    for(int n=0;n<5;n++){Move(s,s.Selected!.X+.05,0);Move(copy,copy.Selected!.X+.05,0);}
    SaveStore.Validate(s.State);SaveStore.Validate(copy.State);
    Require(JsonSerializer.Serialize(s.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(copy.State,SaveStore.JsonOptions),"reload diverged");
});
Check("swept short chords cannot enter a previously clear station",()=>
{
    foreach(double half in new[]{.02,.0002})
    {
        var s=Fixture();var c=s.Selected!;double gap=WorldSimulation.InteractionFootprint(c)+WorldSimulation.SupplyStationRadius;
        double x=Math.Sqrt(gap*gap-half*half*.5);
        // A legal adult start, endpoints outside, interior of this short chord inside.
        Require(s.CanPlaceInteractionAt(c.Id,x,-half),"chord start invalid");s.MoveCreature(c.Id,x,-half);
        Require(s.CanPlaceInteractionAt(c.Id,x,half),"chord end invalid");
        Require(!Move(s,x,half)&&c.Z==-half,"short chord tunnelled");
    }
});
Check("shelter wall remains strict during station recovery",()=>
{
    var s=Fixture();Require(s.BuyShelter()&&s.PlaceShelter(3.04,0).Success,"legal shelter placement");
    Require(!Move(s,1.05,0)&&s.Selected!.X==1,"shelter wall relaxed");
});
Check("fresh disk synthetic slot preserves natural growth recovery",()=>
{
    string root=Path.Combine(Environment.CurrentDirectory,".local","station-clearance","synthetic-slots",Guid.NewGuid().ToString("N"));
    var store=new SaveStore(root);var s=Fixture(false);store.Save(s.State,1);
    var copy=new WorldSimulation(store.Load(1).State);Grow(s);Grow(copy);
    Require(Move(s,1.05,0)&&Move(copy,1.05,0),"disk growth recovery failed");
    store.Save(copy.State,2);var reloaded=new WorldSimulation(store.Load(2).State);
    Require(JsonSerializer.Serialize(s.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(reloaded.State,SaveStore.JsonOptions),"disk state differs");
});
Check("slide-enabled inward goal can recover without an inward step",()=>
{
    var s=Fixture();var c=s.Selected!;
    for(int n=0;n<200&&WorldSimulation.Distance(c.X,c.Z,0,0)<WorldSimulation.InteractionFootprint(c)+WorldSimulation.SupplyStationRadius;n++)
    {
        double x=c.X,z=c.Z,before=WorldSimulation.Distance(x,z,0,0);
        Move(s,x-.05,z,true);
        Require(WorldSimulation.Distance(c.X,c.Z,0,0)>before,"slide recovery stuck");
        Require(WorldSimulation.Distance(x,z,c.X,c.Z)<=.05000001,"fallback teleported");
        Require(x*(c.X-x)+z*(c.Z-z)>=0,"fallback initially moved inward");
    }
    Require(s.CanPlaceInteractionAt(c.Id,c.X,c.Z),"fallback did not fully escape");
});
Check("zero and sub-threshold moves do not fabricate clearance",()=>
{
    var s=Fixture();Move(s,1,0,true);Move(s,1.000000001,0,true);
    Require(s.Selected!.X==1&&s.Selected.Z==0,"near-zero move changed body");
});
Check("radial inward goals recover from sixteen natural-growth approach angles",()=>
{
    for(int k=0;k<16;k++)
    {
        double a=k*Math.Tau/16,dx=Math.Cos(a),dz=Math.Sin(a);var s=Fixture(dx:dx,dz:dz);var c=s.Selected!;
        for(int n=0;n<200&&WorldSimulation.Distance(c.X,c.Z,0,0)<WorldSimulation.InteractionFootprint(c)+WorldSimulation.SupplyStationRadius;n++)
        {
            double x=c.X,z=c.Z,before=WorldSimulation.Distance(x,z,0,0);
            Move(s,x-.05*x/before,z-.05*z/before,true);
            Require(WorldSimulation.Distance(c.X,c.Z,0,0)>before,"angle "+k+" stuck");
            Require(WorldSimulation.Distance(x,z,c.X,c.Z)<=.05000001&&x*(c.X-x)+z*(c.Z-z)>=0,"unsafe recovery");
        }
        Require(s.CanPlaceInteractionAt(c.Id,c.X,c.Z),"angle "+k+" did not escape");
    }
});
Check("natural food and water seeking recover on both station sides",()=>
{
    foreach(var kind in Enum.GetValues<SupplyStationKind>())foreach(double side in new[]{-1d,1d})
    {
        var s=Fixture(dx:side,kind:kind);var c=s.Selected!;var station=s.State.SupplyStations.Single();
        if(kind==SupplyStationKind.Water)Require(s.BuyStationWater(),"water purchase failed");
        var input=new InteractionInput(InteractionAction.RefillSupplyStation,BodyPart.Side,Duration:.1,X:station.X,Z:station.Z,ObjectId:station.Id,TargetKind:InteractionTargetKind.SupplyStation);
        Require(s.BeginSupplyStationGesture(station.Id,input).Success,"refill begin failed");
        for(int n=0;n<16;n++)Require(s.ContinueInteractionGesture(input,true).Success,"refill failed");s.EndInteractionGesture();
        c.Fullness=kind==SupplyStationKind.Food?45:95;c.Interaction.Hydration=kind==SupplyStationKind.Water?45:95;
        bool escaped=false,accepted=false;int stock=station.StoredPortions;
        for(int n=0;n<1600&&!accepted;n++)
        {
            double x=c.X,z=c.Z,before=WorldSimulation.Distance(x,z,0,0),gap=WorldSimulation.InteractionFootprint(c)+WorldSimulation.SupplyStationRadius;
            s.AdvanceInteractionPresentation(.05);s.Advance(.05);
            double after=WorldSimulation.Distance(c.X,c.Z,0,0);
            Require(WorldSimulation.Distance(x,z,c.X,c.Z)<=s.Config.MoveSpeed*s.Config.TickSeconds+1e-8,"autonomous movement exceeded one simulation tick budget");
            if(before<gap&&WorldSimulation.Distance(x,z,c.X,c.Z)>1e-8)Require(after>before,"autonomy worsened growth overlap");
            if(escaped)Require(after>=gap-.00001,"autonomy reentered station");escaped|=after>=gap;
            accepted=c.Interaction.GroundIntake&&!c.Interaction.NaturalIntakePending&&c.Interaction.EatingRemaining>0;
        }
        Require(escaped&&accepted,$"{kind}/{side} did not escape and consume: {c.Activity}");
        Require(station.StoredPortions==stock-1,"finite station stock changed incorrectly");SaveStore.Validate(s.State);
    }
});
if(args.Length>0){string path=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{checks=results.Count,failures,results},new JsonSerializerOptions{WriteIndented=true}));}
Environment.ExitCode=failures==0?0:1;

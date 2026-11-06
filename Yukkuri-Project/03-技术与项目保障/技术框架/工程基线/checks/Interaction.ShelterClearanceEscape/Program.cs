using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Yukkuri.Simulation;

int failures=0,passed=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Validate(WorldSimulation s){SaveStore.Validate(s.State);SaveStore.Validate(JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(s.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!);}
(WorldSimulation s,Creature c,RoomObject ball) Fixture(bool shelter=true,bool drop=false)
{
    var s=WorldSimulation.CreateInteractionPlaytest(20261031);s.State.RoomObjects.Clear();s.State.PlayerX=s.State.PlayerZ=6;s.SetAutonomousBreeding(false);
    // Synthetic background placement and calm/healthy needs isolate the spatial rule.
    for(int n=0;n<s.State.Creatures.Count;n++){
        var c=s.State.Creatures[n];c.X=c.TargetX=-5+n*4;c.Z=c.TargetZ=-5;
        c.Fullness=c.Health=c.Energy=c.Interaction.Hydration=100;c.Fear=c.Pain=0;
        c.Interaction.Emotion.Alarm=0;c.Interaction.Emotion.SafeSeconds=100;
        c.Interaction.NextAutonomyAt=c.DecisionIn=c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    }
    if(shelter)Require(s.BuyShelter()&&s.PlaceShelter(0,0).Success,"public shelter setup");
    var body=s.State.Creatures[0];body.X=body.TargetX=0;body.Z=body.TargetZ=3;
    Require(s.SlideAlongGround(body.Id,0,2.02)&&Math.Abs(body.Z-2.02)<1e-9,"public approach");
    Require(s.BuyShopProduct("ball")&&s.ExecuteInteraction(s.State.Creatures[1].Id,new(InteractionAction.GiveBall,BodyPart.Side,X:0,Z:0)).Success,"public give ball");
    var ball=s.State.RoomObjects.Single(o=>o.Kind==RoomObjectKind.Ball);
    if(drop){Require(s.BeginPortableCarry(ball.Id)&&s.MovePortableCarry(ball.Id,0,1,.65),"public carry");s.ReleasePortableCarry(ball.Id,Vector2.Zero);}
    else Require(s.MoveLooseObject(ball.Id,0,1),"public ball drag");
    Require(s.State.Shelters.All(h=>h.GuestId==null),"unexpected guest");Validate(s);return(s,body,ball);
}
bool Fits(WorldSimulation s,Creature c,double x,double z)=>(bool)typeof(WorldSimulation).GetMethod("ShelterBodyFits",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(s,[c,x,z,0d])!;
bool Escape(Creature c,ShelterState h,RoomObject b,double x,double z,double? radius=null)=>(bool)typeof(WorldSimulation).GetMethod("ShelterFrontBallClearanceEscape",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[c,h,b,x,z,radius??WorldSimulation.ShelterPassageRadius(c)])!;
foreach(bool shelter in new[]{false,true})foreach(double delta in new[]{.01,.05,.08,.1})Check($"small outward step shelter={shelter} delta={delta}",()=>{
    var(s,c,b)=Fixture(shelter);double old=c.Z;Require(s.SlideAlongGround(c.Id,0,old+delta)&&Math.Abs(c.Z-old-delta)<1e-9,"step blocked");Validate(s);
});
foreach(bool drop in new[]{false,true})foreach(bool water in new[]{false,true})Check($"autonomous actual {(water?"drinking":"eating")} after {(drop?"drop":"drag")}",()=>{
    var(s,c,b)=Fixture(drop:drop);Require(s.PlaceInteractionSupply(water?InteractionAction.PlaceWater:InteractionAction.PlaceMeal,0,4).Success,"public supply");
    if(water)c.Interaction.Hydration=10;else c.Fullness=10;double max=10,maxStep=0;bool moved=false;
    for(int n=0;n<600;n++){
        double x=c.X,z=c.Z;s.AdvanceInteractionPresentation(.05);s.Advance(.05);
        double step=Math.Sqrt(Math.Pow(c.X-x,2)+Math.Pow(c.Z-z,2));maxStep=Math.Max(maxStep,step);moved|=step>0;
        max=Math.Max(max,water?c.Interaction.Hydration:c.Fullness);Require(s.State.Shelters.Single().GuestId==null,"reserved shelter");
    }
    Require(moved&&max>15&&maxStep<.25,$"need not completed or teleport: max={max}, maxStep={maxStep}");Validate(s);
    Console.WriteLine(JsonSerializer.Serialize(new{drop,water,max,maxStep,guest=s.State.Shelters.Single().GuestId}));
});
Check("no-op inward and crossing public slides stay blocked",()=>{
    foreach(double z in new[]{2.02,2.01,1.0,-.2}){
        var(s,c,b)=Fixture();if(z>=1)Require(!s.CanPlaceInteractionAt(c.Id,0,z),"placement bypassed blocker");s.SlideAlongGround(c.Id,0,z);Require(c.Z==2.02,"invalid move escaped inward/crossed ball");Validate(s);
    }
});
Check("strict derivative rejects equal-distance and cross-through despite farther endpoint",()=>{
    var(s,c,b)=Fixture();var h=s.State.Shelters.Single();
    Require(!Escape(c,h,b,c.X,c.Z),"no-op allowed");Require(!Escape(c,h,b,0,2.01),"inward allowed");
    b.X=.9;b.Z=c.Z+.1;double dx=c.X-b.X,dz=c.Z-b.Z,angle=.1;
    double x=b.X+dx*Math.Cos(angle)-dz*Math.Sin(angle),z=b.Z+dx*Math.Sin(angle)+dz*Math.Cos(angle);
    // Reflect the small rotation when needed so the endpoint moves outward.
    if(z<=c.Z){angle=-angle;x=b.X+dx*Math.Cos(angle)-dz*Math.Sin(angle);z=b.Z+dx*Math.Sin(angle)+dz*Math.Cos(angle);}
    Require(z>c.Z&&!Escape(c,h,b,x,z),"equal distance accepted");
    x=b.X-dx*1.1;z=b.Z-dz*1.1;Require(z>c.Z&&!Escape(c,h,b,x,z),"crossing accepted based on endpoint only");Validate(s);
});
Check("front band boundaries and physical overlap stay narrow",()=>{
    var(s,c,b)=Fixture();var h=s.State.Shelters.Single();double r=WorldSimulation.ShelterPassageRadius(c),edge=ShelterGeometry.InteriorDepth*.5+r;
    foreach(double z in new[]{edge-.000001,0,-2.02,ShelterGeometry.OuterDepth*.5+r}){c.Z=z;b.Z=z-1.02;Require(!Escape(c,h,b,0,z+.01),"out-of-band exception");}
    c.Z=edge;b.Z=c.Z-1.02;Require(Escape(c,h,b,0,c.Z+.01),"exact outside boundary blocked");
    b.Z=c.Z-.7;Require(!Escape(c,h,b,0,c.Z+.01),"physical overlap exception");
    b.Z=c.Z-1.2;Require(!Escape(c,h,b,0,c.Z+.01),"no existing conservative deficit");
    c.X=ShelterGeometry.InteriorWidth*.5;b.X=c.X;b.Z=c.Z-1.02;Require(!Escape(c,h,b,c.X,c.Z+.01),"side band exception");Validate(s);
});
Check("each ball and other objects retain blocking",()=>{
    var(s,c,b)=Fixture();Require(Fits(s,c,0,2.03),"first ball recovery blocked");
    var second=JsonSerializer.Deserialize<RoomObject>(JsonSerializer.Serialize(b,SaveStore.JsonOptions),SaveStore.JsonOptions)!;second.Id="clearance-second-ball";second.X=.9;second.Z=2.2;s.State.RoomObjects.Add(second);
    Require(!Fits(s,c,0,2.03),"second approached ball bypassed");second.Z=1.5;Require(Fits(s,c,0,2.03),"two receding balls blocked");
    second.Kind=RoomObjectKind.Water;second.X=.5;Require(!Fits(s,c,0,2.03),"non-ball bypassed");Validate(s);
});
Check("walls closed door ceiling other bodies and reservation stay effective",()=>{
    var(s,c,b)=Fixture();var h=s.State.Shelters.Single();Require(!s.CanPlaceInteractionAt(c.Id,1.5,0),"wall bypass");
    h.DoorOpen=false;Require(!s.CanPlaceInteractionAt(c.Id,0,2.03),"closed door bypass");h.DoorOpen=true;
    c.Interaction.Height=2;Require(!Fits(s,c,0,2.03),"ceiling bypass");c.Interaction.Height=0;
    var other=s.State.Creatures[1];other.X=0;other.Z=2.8;Require(!s.CanPlaceInteractionAt(c.Id,0,2.03),"other body bypass");
    other.Z=0;h.GuestId=other.Id;Require(!Fits(s,c,0,2.03),"guest bypass");h.GuestId=null;other.X=-1;other.Z=-5;Validate(s);
});
Check("additional shelter wall remains effective",()=>{
    var(s,c,b)=Fixture();Require(s.BuyShelter()&&s.PlaceShelter(4,0).Success,"public second house");
    Require(s.CanPlaceInteractionAt(c.Id,0,2.03),"distant house blocks valid recovery");
    Require(!Fits(s,c,2.75,0),"second house wall bypass");Validate(s);
});
Check("posture radii are evaluated live and restraints excluded",()=>{
    foreach(bool supine in new[]{false,true}){
        var(s,c,b)=Fixture();c.Interaction.Supine=supine;var h=s.State.Shelters.Single();double r=WorldSimulation.ShelterPassageRadius(c);
        c.Z=ShelterGeometry.InteriorDepth*.5+r+.04;b.Z=c.Z-(r+.28);Require(Escape(c,h,b,0,c.Z+.01),"live posture recovery failed");
        c.Interaction.Restraint=RestraintKind.Box;Require(!Escape(c,h,b,0,c.Z+.01),"box generalized as ball circle");c.Interaction.Restraint=RestraintKind.None;Validate(s);
    }
});
Check("new conservative overlap and inside escape not authorized",()=>{
    var(s,c,b)=Fixture();c.Z=2.07;b.Z=.95;Require(!Fits(s,c,0,2.02),"new overlap permitted");
    c.Z=1.9;b.Z=.9;Require(!Fits(s,c,0,1.91),"inside recovery widened");Validate(s);
});
Console.WriteLine(JsonSerializer.Serialize(new{passed,failures}));return failures==0?0:1;

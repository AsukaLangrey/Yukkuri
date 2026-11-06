using System.Text.Json;
using Yukkuri.Simulation;

// Synthetic fixtures only. Never reads user saves. Invoke with an isolated evidence directory.
string evidence = args.Length == 1 ? args[0] : throw new ArgumentException("Provide an isolated synthetic-save directory.");
Directory.CreateDirectory(evidence);
int passed=0,failed=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
WorldSimulation Fresh(RoomObjectKind kind,int count=64)
{
    var s=WorldSimulation.CreateInteractionPlaytest(20261106);
    s.State.RoomObjects.Clear();
    foreach(var x in s.State.Creatures){x.Personality.Kindness=0;x.Interaction.NextCleaningAt=100000;x.Interaction.NextAutonomyAt=100000;x.Family.AutonomousBreedingPermitted=false;}
    var c=s.State.Creatures[0];
    for(int n=0;n<count;n++)s.State.RoomObjects.Add(new(){Id=$"pile-{n}",RegionId=c.RegionId,Kind=kind,X=n==0?c.X:6,Z=n==0?c.Z:5,Portions=n==0?1000:1,OwnerId=c.Id,Contamination=1});
    return s;
}
RoomObject Pile(WorldSimulation s,int n)=>s.State.RoomObjects.Single(o=>o.Id==$"pile-{n}");
int Total(WorldSimulation s)=>s.State.RoomObjects.Sum(o=>o.Portions);
int Completed(Creature c)=>c.Interaction.Physiology.Urinations+c.Interaction.Physiology.Defecations;
void Begin(WorldSimulation s,RoomObjectKind kind)
{
    var p=s.State.Creatures[0].Interaction.Physiology;
    p.Bladder=kind==RoomObjectKind.Urine?85:10;p.Bowel=kind==RoomObjectKind.Feces?85:10;
    SaveStore.Validate(s.State);
    for(int n=0;n<10&&!p.Active;n++)s.Advance(.1);
    Require(p.Active,"real public Advance did not start excretion");
}
void Finish(WorldSimulation s,RoomObjectKind kind)
{
    var c=s.State.Creatures[0];var p=c.Interaction.Physiology;int before=Completed(c);
    Require(p.Active,"action must already be active");
    for(int n=0;n<60&&Completed(c)==before;n++)s.Advance(.1);
    SaveStore.Validate(s.State);
    Require(Completed(c)==before+1&&!p.Active,"did not complete exactly once");
    Require((kind==RoomObjectKind.Urine?p.Bladder:p.Bowel)==10,"need was not settled");
    int total=Total(s);s.Advance(.1);s.Advance(.1);
    Require(Completed(c)==before+1&&Total(s)==total,"completion duplicated");
}
void Produce(WorldSimulation s,RoomObjectKind kind){Begin(s,kind);Finish(s,kind);}
foreach(var kind in new[]{RoomObjectKind.Urine,RoomObjectKind.Feces})
{
    Check($"{kind}: nearest full uses spare capacity",()=>{var s=Fresh(kind);int total=Total(s);Produce(s,kind);Require(Total(s)==total+1&&Pile(s,0).Portions==1000&&Pile(s,1).Portions==2&&s.State.RoomObjects.Count==64,"lost portion or changed cap/order");});
    Check($"{kind}: 63 piles still create",()=>{var s=Fresh(kind,63);int total=Total(s);Produce(s,kind);Require(Total(s)==total+1&&s.State.RoomObjects.Count==64&&Pile(s,1).Portions==1,"creation boundary changed");});
    Check($"{kind}: nearest nonfull unchanged",()=>{var s=Fresh(kind);Pile(s,0).Portions=1;Produce(s,kind);Require(Pile(s,0).Portions==2&&Pile(s,1).Portions==1&&s.State.RoomObjects.Count==64,"nearest eligible not chosen");});
    Check($"{kind}: distance beats list order and 999 moves to next",()=>{var s=Fresh(kind);var c=s.State.Creatures[0];Pile(s,2).X=c.X;Pile(s,2).Z=c.Z;Pile(s,2).Portions=999;int total=Total(s);Produce(s,kind);Require(Pile(s,2).Portions==1000&&Pile(s,1).Portions==1&&Total(s)==total+1,"nearest eligible not filled to 1000");Produce(s,kind);Require(Pile(s,2).Portions==1000&&Pile(s,1).Portions==2&&Total(s)==total+2,"did not advance to next eligible pile");});
    Check($"{kind}: same-position ties retain list order",()=>{var s=Fresh(kind);var c=s.State.Creatures[0];foreach(var o in s.State.RoomObjects){o.X=c.X;o.Z=c.Z;}Produce(s,kind);Require(Pile(s,1).Portions==2&&Pile(s,2).Portions==1,"tie order changed");});
    Check($"{kind}: region type stored filters",()=>{var s=Fresh(kind);var c=s.State.Creatures[0];s.State.Regions.Add(new(){Id="waste-test-region",Name="Synthetic region",Kind=RegionKind.Sanctuary});s.State.RoomObjects.InsertRange(0,new[]{new RoomObject{Id="other-region",RegionId="waste-test-region",Kind=kind,X=c.X,Z=c.Z},new RoomObject{Id="stored",RegionId=c.RegionId,Kind=kind,Stored=true,X=c.X,Z=c.Z},new RoomObject{Id="other-kind",RegionId=c.RegionId,Kind=kind==RoomObjectKind.Urine?RoomObjectKind.Feces:RoomObjectKind.Urine,X=c.X,Z=c.Z}});int total=Total(s);Produce(s,kind);Require(Total(s)==total+1&&Pile(s,1).Portions==2&&s.State.RoomObjects.Take(3).All(o=>o.Portions==1),"ineligible pile selected");});
    Check($"{kind}: excluded objects do not count toward 64",()=>{var s=Fresh(kind,63);var c=s.State.Creatures[0];s.State.RoomObjects.Add(new(){Id="stored",RegionId=c.RegionId,Kind=kind,Stored=true});s.State.RoomObjects.Add(new(){Id="other-kind",RegionId=c.RegionId,Kind=kind==RoomObjectKind.Urine?RoomObjectKind.Feces:RoomObjectKind.Urine});Produce(s,kind);Require(s.State.RoomObjects.Count(o=>o.Kind==kind&&!o.Stored)==64&&Pile(s,1).Portions==1,"filtered count prevented new pile");});
    Check($"{kind}: accepted 65-pile input remains bounded",()=>{var s=Fresh(kind,65);int total=Total(s);Produce(s,kind);Require(s.State.RoomObjects.Count==65&&Total(s)==total+1,"legacy count lost portion or added object");});
    Check($"{kind}: all full retains existing limitation",()=>{var s=Fresh(kind);foreach(var o in s.State.RoomObjects)o.Portions=1000;Produce(s,kind);Require(Total(s)==64000&&s.State.RoomObjects.Count==64,"invented overflow rule");});
    Check($"{kind}: active synthetic save resumes exactly once",()=>{var s=Fresh(kind);Begin(s,kind);var store=new SaveStore(Path.Combine(evidence,kind.ToString()));store.Save(s.State,0);var copy=new WorldSimulation(store.Load(0).State);int total=Total(copy);Finish(copy,kind);Require(Total(copy)==total+1&&Pile(copy,1).Portions==2,"resume lost portion");store.Save(copy.State,1);var loaded=store.Load(1).State;Require(JsonSerializer.Serialize(loaded,SaveStore.JsonOptions)==JsonSerializer.Serialize(copy.State,SaveStore.JsonOptions),"roundtrip changed state");});
    Check($"{kind}: interruption preserves need and later produces once",()=>{var s=Fresh(kind);Begin(s,kind);var c=s.State.Creatures[0];var p=c.Interaction.Physiology;int total=Total(s);c.Interaction.Supine=true;s.AdvanceInteractionPresentation(.1);SaveStore.Validate(s.State);Require(!p.Active&&Completed(c)==0&&Total(s)==total&&(kind==RoomObjectKind.Urine?p.Bladder:p.Bowel)>=85,"interrupted action settled need or waste");c.Interaction.Supine=false;Produce(s,kind);Require(Total(s)==total+1,"resumed action lost or duplicated portion");});
}
Console.WriteLine(JsonSerializer.Serialize(new{passed,failed,total=passed+failed}));
return failed==0?0:1;

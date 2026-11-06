using System.Text.Json;
using Yukkuri.Simulation;

// Links the production partial directly; no copied formatter and no Godot/player save access.
Console.OutputEncoding=new System.Text.UTF8Encoding(false);
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Tick(WorldSimulation s,double seconds){for(int i=0;i<(int)Math.Round(seconds*20);i++){s.AdvanceInteractionPresentation(.05);s.Advance(.05);}}
string Json(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation s){SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(Json(s),SaveStore.JsonOptions)!;SaveStore.Validate(state);return new(state);}
WorldSimulation Birth()
{
    // Same real pregnancy journey as Courtyard.Economy, not a fabricated sale archive.
    var s=WorldSimulation.CreateCourtyardEconomy(42);s.State.RoomObjects.Clear();s.State.Creatures.RemoveAt(2);s.State.AutonomousBreeding=false;
    for(int n=0;n<2;n++)
    {
        var c=s.State.Creatures[n];c.X=c.TargetX=n*1.65;c.Z=c.TargetZ=0;
        c.Fullness=c.Interaction.Hydration=c.Health=c.Energy=100;c.Pain=c.Fear=0;c.Trust=90;
        c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    }
    var m=s.State.Creatures[0];var f=s.State.Creatures[1];
    string message=s.RequestPregnancy(m.Id,f.Id,BirthMethod.Stem);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing,message);Tick(s,280);
    Require(s.State.Creatures.Count(c=>c.Family.BirthEventId==m.Family.Pregnancy.EventId)>=2,"real birth missing");
    Tick(s,180);Require(m.Family.Pregnancy.Phase==PregnancyPhase.None,"recovery not finished");
    return Reload(s);
}
void Sell(WorldSimulation s,Creature c)
{
    int price=CourtyardEconomy.Quote(c).Price,money=s.State.Money;
    string message=s.SellCreatureToShop(c.Id,price);
    Require(!s.State.Creatures.Contains(c)&&s.State.Money==money+price,message);
}
void Texts(WorldSimulation s,Creature child,string mother,string father,string label)
{
    var room=new InteractionRoom(s);string before=Json(s);
    string property=room.PropertyText(child),summary=room.SummaryText(child);
    Console.WriteLine(JsonSerializer.Serialize(new{label,property,summary},new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
    Require(property.StartsWith($"家庭：母亲 {mother} · 父亲 {father}\n",StringComparison.Ordinal),"property parent names lost");
    Require(summary.Contains($"母亲：{mother}；父亲：{father}",StringComparison.Ordinal),"family summary parent names lost");
    Require(before==Json(s),"production text mutated world state");
}
foreach(string scenario in new[]{"mother","father","both"})Check("real birth, sold "+scenario+", reload and inactive archive",()=>
{
    var s=Birth();var m=s.State.Creatures[0];var f=s.State.Creatures[1];var child=s.State.Creatures.First(c=>c.Family.MotherId==m.Id);
    Texts(s,child,m.Name,f.Name,scenario+" live");
    if(scenario!="father")Sell(s,m);
    if(scenario!="mother")Sell(s,f);
    Texts(s,child,m.Name,f.Name,scenario+" sold");
    s=Reload(s);child=s.State.Creatures.Single(c=>c.Id==child.Id);
    Texts(s,child,m.Name,f.Name,scenario+" reloaded");
    foreach(var sale in s.State.CreatureSales)
    {
        string frozen=JsonSerializer.Serialize(sale.Creature,SaveStore.JsonOptions);
        Require(!s.Select(sale.Creature.Id),"sold parent selectable");
        Require(s.FamilyCareReason(child.Id,sale.Creature.Id)!=null&&s.FamilyCareReason(sale.Creature.Id,child.Id)!=null,"sold parent usable as care target or carer");
        Tick(s,1);Require(frozen==JsonSerializer.Serialize(sale.Creature,SaveStore.JsonOptions),"sold parent still simulated");
    }
    SaveStore.Validate(s.State);
});
Check("cold and memorialized identities retain names",()=>
{
    var s=Birth();var m=s.State.Creatures[0];var f=s.State.Creatures[1];var child=s.State.Creatures.First(c=>c.Family.MotherId==m.Id);
    // Paid tool fixture, with progression disabled only for the established cold-storage boundary.
    s.State.EconomyProgression=false;s.State.Money=10000;
    Require(s.BuyShopProduct("freezer"),"freezer purchase failed");s.SetColdStorage(m.Id,true);
    Require(m.Family.ColdStorage.Stored,"cold storage failed");Texts(s,child,m.Name,f.Name,"cold mother");
    var cold=Reload(s);Texts(cold,cold.State.Creatures.Single(c=>c.Id==child.Id),m.Name,f.Name,"cold mother reloaded");
    s.SetColdStorage(m.Id,false);s.Hurt(f,200,"identity regression",null);s.LayToRest(f.Id);
    Require(f.Condition==PhysicalCondition.Dead&&f.Family.Memorialized,"memorial setup failed");
    Texts(s,child,m.Name,f.Name,"memorial father");s=Reload(s);
    Texts(s,s.State.Creatures.Single(c=>c.Id==child.Id),m.Name,f.Name,"cold/memorial reloaded");
});
Check("null and truly missing parent ids keep their existing fallbacks",()=>
{
    var s=WorldSimulation.CreateCourtyardEconomy();var c=s.State.Creatures[2];
    // Presentation-only malformed-reference boundary, never passed off as a valid save fixture.
    c.Family.MotherId="missing-mother";c.Family.FatherId=null;
    var room=new InteractionRoom(s);string before=Json(s);
    Require(room.PropertyText(c).StartsWith("家庭：母亲 未记录 · 父亲 未记录\n"),"property fallback changed");
    Require(room.SummaryText(c).Contains("母亲：不在这里的家人；父亲：未记录"),"summary fallback changed");
    c.Family.MotherId=null;c.Family.FatherId="missing-father";
    Require(room.SummaryText(c).Contains("母亲：未记录；父亲：不在这里的家人"),"father fallback changed");
    c.Family.MotherId="missing-mother";c.Family.FatherId=null;
    Require(before==Json(s),"fallback text mutated state");
});
Console.WriteLine($"FAILURES {failures}");return failures==0?0:1;

public partial class InteractionRoom
{
    private WorldSimulation Sim { get; }
    public InteractionRoom(WorldSimulation sim)=>Sim=sim;
    public string PropertyText(Creature c)=>ObjectFamilyDetails(c);
    public string SummaryText(Creature c)=>FamilySummary(c);
}

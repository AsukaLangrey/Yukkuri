using System.Text.Json;
using Yukkuri.Simulation;

// Source only until the user approves compilation and execution of this isolated copy.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Near(double a,double b,string why)=>Require(Math.Abs(a-b)<1e-7,why);
void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);var copy=new WorldSimulation(state);SaveStore.Validate(copy.State);return copy;
}
WorldSimulation Fixture(int count=3)
{
    var sim=WorldSimulation.CreateCourtyardEconomy(77);sim.State.AutonomousBreeding=false;
    sim.State.RoomObjects.Clear();sim.State.Shelters.Clear();
    while(sim.State.Creatures.Count<count)
    {
        int n=sim.State.Creatures.Count;var c=JsonSerializer.Deserialize<Creature>(JsonSerializer.Serialize(sim.State.Creatures[2],SaveStore.JsonOptions),SaveStore.JsonOptions)!;
        c.Id=$"cold-fixture-{n}";c.Name=$"存储{n}";c.Body.Id=c.Id+"-body";c.Registration.IdentityId=c.Id+"-identity";
        c.Interaction.Accessory.Id=c.Id+"-accessory";c.Family=new();sim.State.Creatures.Add(c);
    }
    foreach(var c in sim.State.Creatures){c.Interaction.NextAutonomyAt=100000;c.Family.NextSocialAt=c.Family.NextCareAt=100000;}
    return sim;
}
Check("1/2/3/5/10 clocks, 600 simulated seconds per day, pause/closeup and save speed",()=>
{
    foreach(int speed in WorldSimulation.CourtyardSpeeds)
    {
        var sim=Fixture();Require(sim.State.Settings.Speed==1,"new yard not 1x");sim.SetCourtyardSpeed(speed);
        for(int n=0;n<20;n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}
        Near(sim.State.WorldSeconds,speed,"speed applied zero/twice");
        sim.State.Settings.Pause=true;sim.Advance(1);Near(sim.State.WorldSeconds,speed,"pause advanced");
        sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;sim.Advance(1);Near(sim.State.WorldSeconds,speed,"closeup advanced");
        sim.State.Mode=PlayMode.Management;var copy=Reload(sim);Near(copy.CourtyardSpeed,speed,"speed lost on load");
        Require(CourtyardCalendar.At(600).DayOfSeason==2,"600-second day changed");
    }
});
Check("12 real slots survive load; 13th refuses without moving, charging or duplicating",()=>
{
    var sim=Fixture(13);var ids=sim.State.Creatures.Select(c=>c.Id).ToArray();int money=sim.State.Money;
    for(int n=0;n<12;n++){sim.RecycleCourtyardObject(ids[n],out bool committed);Require(committed,"commit flag missing");Require(sim.State.Creatures[n].Family.ColdStorage.Stored,"store failed "+n);}
    var last=sim.State.Creatures[12];double x=last.X,z=last.Z;string region=last.RegionId;
    Require(sim.CourtyardRecycleReason(last.Id)!=null,"full hover accepted");sim.RecycleCourtyardObject(last.Id,out bool rejectedCommit);Require(!rejectedCommit,"full refusal reported success");
    Require(!last.Family.ColdStorage.Stored&&last.X==x&&last.Z==z&&last.RegionId==region,"full moved oil");
    var copy=Reload(sim);Require(copy.State.Creatures.Count==13&&copy.State.Creatures.Count(c=>c.Family.ColdStorage.Stored)==12,"load lost capacity/identity");
    Require(copy.State.Money==money,"storage charged money");
    copy.SetColdStorage(ids[0],false);Require(!copy.State.Creatures[0].Family.ColdStorage.Stored,"return failed");
    copy.RecycleCourtyardObject(ids[12]);Require(copy.State.Creatures[12].Family.ColdStorage.Stored,"slot not released");Reload(copy);
});
Check("stored physiology and injury rescue clock freeze beyond old deadline, no auto return",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];c.Health=1;sim.Hurt(c,2,"fixture injury",null);
    sim.RecycleCourtyardObject(c.Id);Require(c.Family.ColdStorage.Stored,"injured body not stored");
    var before=(c.Body.AgeSeconds,c.InnerAgeSeconds,c.Fullness,c.Energy,c.Interaction.Hydration,c.CriticalRemaining,c.Interaction.Physiology.Bladder);
    c.Family.ColdStorage.Elapsed=599;for(int n=0;n<80;n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}
    Require(before==(c.Body.AgeSeconds,c.InnerAgeSeconds,c.Fullness,c.Energy,c.Interaction.Hydration,c.CriticalRemaining,c.Interaction.Physiology.Bladder),"biology changed");
    Require(c.Family.ColdStorage.Stored,"deadline forced exit");var copy=Reload(sim);copy.SetColdStorage(c.Id,false);
    Require(!copy.State.Creatures[0].Family.ColdStorage.Stored&&copy.State.Creatures[0].CriticalRemaining==c.CriticalRemaining,"return reset injury");Reload(copy);
});
Check("sell/release/destroy use one departure archive and settle only once after load",()=>
{
    foreach(var kind in new[]{CreatureDepartureKind.Sale,CreatureDepartureKind.Release,CreatureDepartureKind.Destroy})
    {
        var sim=Fixture();string id=sim.State.Creatures[0].Id;sim.RecycleCourtyardObject(id);sim=Reload(sim);
        var c=sim.State.Creatures.Single(c=>c.Id==id);int food=sim.State.Food,money=sim.State.Money,price=CourtyardEconomy.Quote(c).Price;
        long revenue=sim.State.CreatureSalesRevenue;
        void Submit(){if(kind==CreatureDepartureKind.Sale)sim.SellCreatureToShop(id,price,true);else sim.DepartColdCreature(id,kind==CreatureDepartureKind.Destroy);}
        Submit();Submit();
        Require(!sim.State.Creatures.Any(c=>c.Id==id)&&sim.State.KnownCreatures.Count(c=>c.Id==id)==1,"identity duplicated/lost");
        Require(sim.State.CreatureSales.Single(s=>s.Creature.Id==id).Kind==kind,"departure kind lost");
        Require(sim.State.Creatures.All(o=>!o.Family.PartnerIds.Contains(id)&&!o.Family.CaregiverIds.Contains(id)),"live partner/caregiver reference retained");
        Require(sim.State.Money==money+(kind==CreatureDepartureKind.Sale?price:0),"money settlement");
        Require(sim.State.CreatureSalesRevenue==revenue+(kind==CreatureDepartureKind.Sale?price:0),"release unlocked shop");
        Require(sim.State.Food==food+(kind==CreatureDepartureKind.Destroy?1:0),"food awarded twice");Reload(sim);
    }
});
Check("stale sale price and paused operations preserve stored identity/inventory",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];sim.RecycleCourtyardObject(c.Id);int food=sim.State.Food,money=sim.State.Money;
    sim.SellCreatureToShop(c.Id,CourtyardEconomy.Quote(c).Price+1,true);Require(c.Family.ColdStorage.Stored,"stale quote sold");
    sim.State.Settings.Pause=true;sim.SetColdStorage(c.Id,false);sim.DepartColdCreature(c.Id,true);sim.SellCreatureToShop(c.Id,CourtyardEconomy.Quote(c).Price,true);
    Require(c.Family.ColdStorage.Stored&&sim.State.Money==money&&sim.State.Food==food,"pause mutated storage");Reload(sim);
});
Check("legacy container migrates once into unified slots and returns same body",()=>
{
    var sim=Fixture();var c=sim.State.Creatures[0];string body=c.Body.Id;
    sim.State.Regions.Add(new(){Id="courtyard-container",Name="旧箱",Kind=RegionKind.Sanctuary});
    c.RegionId="courtyard-container";sim.State.SelectedId=null;sim=new WorldSimulation(sim.State);
    Require(c.Family.ColdStorage.Stored,"legacy not migrated");sim=Reload(sim);sim.SetColdStorage(c.Id,false);
    Require(sim.State.Creatures.Single(x=>x.Id==c.Id).Body.Id==body&&!sim.State.Creatures.Single(x=>x.Id==c.Id).Family.ColdStorage.Stored,"legacy identity/return");Reload(sim);
});
Check("save validator rejects forged 13th freezer occupant",()=>
{
    var sim=Fixture(13);foreach(var c in sim.State.Creatures)c.Family.ColdStorage.Stored=true;
    bool rejected=false;try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"capacity enforced only in UI");
});
Environment.ExitCode=failures==0?0:1;

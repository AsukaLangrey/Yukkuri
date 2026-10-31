using System.Text.Json;
using Yukkuri.Simulation;

// Source only on the original development host; run on the authorized remote host.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
WorldSimulation Empty()
{
    var s=WorldSimulation.CreateInteractionPlaytest();
    s.State.Creatures.Clear();s.State.SelectedId=null;
    return s;
}
void RejectUnchanged(WorldSimulation s,CreatureArrival p,double x=0,double z=2)
{
    string before=Snapshot(s);
    Require(s.NewCreaturePlacementReason(p,x,z)!=null,"invalid preview was accepted");
    Require(!s.PlaceNewCreature(p,x,z).Success,"invalid placement succeeded");
    Require(Snapshot(s)==before,"rejection changed money, RNG, selection or existing world");
}

Check("each species age and role creates one paid independent identity and survives save",()=>
{
    foreach(string species in new[]{"reimu","marisa"})foreach(bool young in new[]{false,true})
    foreach(var role in Enum.GetValues<ReproductionRole>())
    {
        var s=Empty();s.State.WorldSeconds=500;
        var p=new CreatureArrival(species,young,role," 小团子 ");
        int money=s.State.Money;string before=Snapshot(s);
        for(int n=0;n<20;n++)Require(s.NewCreaturePlacementReason(p,0,2)==null,"clear ground rejected");
        Require(Snapshot(s)==before,"preview changed world");
        Require(s.PlaceNewCreature(p,0,2).Success&&s.State.Creatures.Count==1,"did not add exactly one creature");
        var c=s.Selected!;
        Require(s.State.Money==money-WorldSimulation.NewCreaturePrice&&c.Name=="小团子"&&c.RegionId==s.State.ActiveRegionId,
            "purchase or selected identity wrong");
        Require(c.Body.SpeciesId==species&&c.IsYoung==young&&c.Body.ReproductionRole==role&&
            c.Body.AgeSeconds==(young?0:FamilyRules.YoungSeconds)&&c.X==0&&c.Z==2&&c.TargetX==0&&c.TargetZ==2,
            "profile or actual placement wrong");
        Require(c.Interaction.Enabled&&c.Personality.Initialized&&c.Trust==20&&c.Health==100&&
            c.Family.MotherId==null&&c.Family.FatherId==null&&c.Family.BirthEventId==null&&s.State.CompletedBirths.Count==0,
            "arrival invented family, birth, trust or injury");
        SaveStore.Validate(s.State);
        var restored=JsonSerializer.Deserialize<WorldState>(Snapshot(s),SaveStore.JsonOptions)!;
        SaveStore.Validate(restored);var loaded=new WorldSimulation(restored);var same=loaded.Selected!;
        Require(same.Id==c.Id&&same.Body.Id==c.Body.Id&&same.Registration.IdentityId==c.Registration.IdentityId&&
            same.Interaction.Accessory.Id==c.Interaction.Accessory.Id&&Snapshot(loaded)==Snapshot(s),"reload changed arrival or charged twice");
        RejectUnchanged(s,p,0,2);
        Require(s.PlaceNewCreature(p,-3,2).Success,"second clear landing rejected");
        Require(s.State.Creatures.Select(o=>o.Id).Distinct().Count()==2&&s.State.Creatures.Select(o=>o.Body.Id).Distinct().Count()==2&&
            s.State.Creatures.Select(o=>o.Registration.IdentityId).Distinct().Count()==2&&
            s.State.Creatures.Select(o=>o.Interaction.Accessory.Id).Distinct().Count()==2,"duplicate identities");
        Require(c.Trust==20&&c.Family.Peers.Count==0,"arrival changed an existing creature relationship");
    }
});
Check("invalid surfaces profiles money pause and closeup reject without side effects",()=>
{
    var p=new CreatureArrival("reimu");
    foreach(var point in new[]{(double.NaN,2d),(0d,double.PositiveInfinity),(8d,0d),(7d,0d),(-3.8,-3.4)})
        RejectUnchanged(Empty(),p,point.Item1,point.Item2);
    RejectUnchanged(Empty(),p with{SpeciesId="unknown"});
    RejectUnchanged(Empty(),p with{Role=(ReproductionRole)99});
    RejectUnchanged(Empty(),p with{Name=new string('a',25)});
    RejectUnchanged(Empty(),p with{Name="line\nbreak"});
    var paused=Empty();paused.State.Settings.Pause=true;RejectUnchanged(paused,p);
    var close=WorldSimulation.CreateInteractionPlaytest();close.State.Mode=PlayMode.CloseUp;RejectUnchanged(close,p);
    var poor=Empty();poor.State.UnlimitedResources=true;poor.State.Money=WorldSimulation.NewCreaturePrice-1;RejectUnchanged(poor,p);
    poor.State.Money=WorldSimulation.NewCreaturePrice;Require(poor.PlaceNewCreature(p,0,2).Success&&poor.State.Money==0,"exact balance failed");
});
Check("empty current region receives creature without selecting another region",()=>
{
    var s=WorldSimulation.CreateInteractionPlaytest();var originals=s.State.Creatures.Select(c=>JsonSerializer.Serialize(c)).ToArray();
    s.State.Regions.Add(new(){Id="annex",Name="新小院",Kind=RegionKind.Sanctuary});s.State.ActiveRegionId="annex";s.State.SelectedId=null;
    Require(s.PlaceNewCreature(new("marisa"),0,2).Success&&s.Selected!.RegionId=="annex","ignored active region");
    Require(originals.SequenceEqual(s.State.Creatures.Take(3).Select(c=>JsonSerializer.Serialize(c))),"existing residents changed");
    SaveStore.Validate(s.State);
});
Check("props and shelter interiors cannot be used as arrival points",()=>
{
    var p=new CreatureArrival("reimu");var s=Empty();
    s.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Ball,X=0,Z=2});RejectUnchanged(s,p);
    s.State.RoomObjects.RemoveAt(s.State.RoomObjects.Count-1);
    Require(s.BuyShelter()&&s.PlaceShelter(0,2).Success,"shelter fixture failed");
    RejectUnchanged(s,p);RejectUnchanged(s,p,1.2,2);
});
Check("population limit rejects but never removes existing identities",()=>
{
    var s=Empty();for(int n=0;n<WorldSimulation.ArrivalPopulationLimit;n++)s.State.Creatures.Add(new(){X=-5,Z=-5});
    RejectUnchanged(s,new("reimu"));
});
Check("new arrival participates in normal interaction and world time",()=>
{
    var s=Empty();Require(s.PlaceNewCreature(new("reimu"),0,2).Success,"arrival failed");
    var c=s.Selected!;
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.Greet)).Success,"new arrival cannot be greeted");
    double fullness=c.Fullness,age=c.Body.AgeSeconds;
    s.Advance(s.Config.TickSeconds);
    Require(c.Fullness<fullness&&c.Body.AgeSeconds>age&&s.State.Creatures.Contains(c),"arrival is only a display object");
    SaveStore.Validate(s.State);
});

Console.WriteLine($"Creature arrival checks: {failures} failures");
return failures==0?0:1;

using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

// Source-only here; execute on the separately authorised remote machine.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
// Fixture equipment is acquired through the current paid shop, not injected stock.
void BuyTool(WorldSimulation sim,string id)
{
    var product=CourtyardShop.Products.Single(p=>p.Id==id);
    int money=sim.State.Money,stock=sim.ShopQuantity(id);
    Require(sim.BuyShopProduct(id)&&sim.State.Money==money-product.Price&&sim.ShopQuantity(id)==stock+product.Pack,
        "paid fixture purchase failed: "+id);
}
// Close-up may finish the visible reaction, but not advance saved world facts.
string WorldFacts(WorldSimulation s)
{
    var root=JsonNode.Parse(Snapshot(s))!;
    foreach(var creature in root["Creatures"]!.AsArray())creature!.AsObject().Remove("ReactionRemaining");
    return root.ToJsonString();
}
WorldSimulation Fresh(ulong seed=20261031)
{
    var s=WorldSimulation.CreateInteractionPlaytest(seed);var c=s.Selected!;
    s.State.Creatures.RemoveAll(o=>o!=c);s.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
    c.X=c.Z=c.TargetX=c.TargetZ=0;c.Fullness=c.Energy=c.Interaction.Hydration=90;
    c.Family.NextSocialAt=c.Family.NextCareAt=c.Interaction.NextAutonomyAt=10000;
    return s;
}
void Frame(WorldSimulation s){s.AdvanceInteractionPresentation(.1);s.Advance(.1);}
void Immerse(WorldSimulation s,double x=7.1)
{
    var c=s.Selected!;var lift=new InteractionInput(InteractionAction.Lift,BodyPart.Side);
    Require(s.BeginInteractionGesture(c.Id,lift).Success,"actual lift failed");s.EndInteractionGesture();
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.PutDown,X:x,Z:0)).Success,"actual water release failed");
    Require(c.Interaction.Water.InWater&&WaterEnvironment.IsInWater(s.State,c),"release snapped onto dry land");
}
void FirstAttempt(WorldSimulation s)
{
    for(int n=0;n<40&&s.Selected!.Interaction.Water.AttemptSequence==0;n++)Frame(s);
    Require(s.Selected!.Interaction.Water.AttemptSequence>0,"eligible water contact never produced a real attempt");
}
WorldSimulation Reload(WorldSimulation s)
{
    SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(Snapshot(s),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}

Check("only a real attempt records progress and its first frame is immediately saveable",()=>
{
    var s=Fresh();Immerse(s);var c=s.Selected!;var w=c.Interaction.Water;double x=c.X;
    Require(w.AttemptSequence==0&&w.AttemptAt==-100&&s.WaterAttemptStatus(c)==null,"entry invented an attempted jump");
    for(int n=0;n<5;n++)Frame(s);
    Require(w.AttemptSequence==0&&c.X==x,"schedule waiting moved or recorded an attempt");
    FirstAttempt(s);var view=s.WaterAttemptStatus(c);
    Require(view is {Outcome:WaterAttemptOutcome.Advanced,Sequence:1}&&view.EventId==w.EventId&&view.At==s.State.WorldSeconds&&
        c.X<x&&c.X>=x-.18-.000001&&w.InWater,"actual first progress or its causal identity was wrong");
    SaveStore.Validate(s.State);var copy=Reload(s);
    Require(copy.Selected!.Interaction.Water.AttemptAt==w.AttemptAt&&copy.Selected.Interaction.Water.AttemptSequence==1,"reload invented or removed an attempt");
});
Check("real occupancy records a blocked attempt without moving the body or inventing progress",()=>
{
    var s=Fresh();Immerse(s);var c=s.Selected!;
    var blocker=new Creature{Name="岸边失能阻挡夹具",RegionId=c.RegionId,Fullness=90,Energy=90,
        Interaction=new(){Enabled=true,Hydration=90,FootIntegrity=0,NextAutonomyAt=10000},
        Personality=new(){Initialized=true,Kindness=.05},Family=new(){NextCareAt=10000,NextSocialAt=10000}};
    blocker.X=blocker.TargetX=c.X-WorldSimulation.InteractionFootprint(c)-WorldSimulation.InteractionFootprint(blocker)-.02;
    blocker.Z=blocker.TargetZ=0;s.State.Creatures.Add(blocker);
    double x=c.X;FirstAttempt(s);
    Require(c.X==x&&s.WaterAttemptStatus(c)?.Outcome==WaterAttemptOutcome.Blocked,"occupied next step was labelled as movement");
    s.MoveCreature(blocker.Id,0,-5);long old=c.Interaction.Water.AttemptSequence;
    for(int n=0;n<40&&c.Interaction.Water.AttemptSequence==old;n++)Frame(s);
    Require(c.X<x&&s.WaterAttemptStatus(c)?.Outcome==WaterAttemptOutcome.Advanced,"removing the actual blocker did not permit later progress");
});
Check("scheduled checks do not fabricate attempts while fixed disabled exhausted or overturned",()=>
{
    foreach(int kind in Enumerable.Range(0,4))
    {
        var s=Fresh();Immerse(s);var c=s.Selected!;var w=c.Interaction.Water;
        if(kind==0)c.Interaction.Restraint=RestraintKind.Frame;
        else if(kind==1)c.Interaction.FootIntegrity=0;
        else if(kind==2)c.Energy=10;
        else {c.Interaction.Supine=true;c.Interaction.SupineRemaining=10;}
        double next=w.NextAttemptAt,x=c.X;for(int n=0;n<45;n++)Frame(s);
        Require(w.NextAttemptAt>next&&w.AttemptSequence==0&&w.AttemptOutcome==null&&c.X==x&&s.WaterAttemptStatus(c)==null,
            "timer rollover impersonated an actual bodily attempt");
        SaveStore.Validate(s.State);
    }
});
Check("a successful real bank attempt keeps the original escape and recovery settlement",()=>
{
    bool escaped=false;
    // Exercise the real probability and real dry landing search; no injected success.
    for(ulong seed=1;seed<=32&&!escaped;seed++)
    {
        var s=Fresh(seed);BuyTool(s,"freezer");Immerse(s,WoodlandLayout.RiverEdge(0)+.12);var c=s.Selected!;
        FirstAttempt(s);var view=s.WaterAttemptStatus(c);
        if(view?.Outcome!=WaterAttemptOutcome.Escaped)continue;
        escaped=true;Require(!c.Interaction.Water.InWater&&!WaterEnvironment.IsInWater(s.State,c)&&
            c.Interaction.Water.RecoverySeconds>0&&c.Interaction.Water.LastRescueAt==-100&&s.State.WaterRescueTools==1,
            "self escape used rescue inventory or bypassed ordinary recovery");
        double x=c.X,z=c.Z;string before=Snapshot(s);for(int n=0;n<100;n++)_ = s.WaterAttemptStatus(c);
        Require(c.X==x&&c.Z==z&&Snapshot(s)==before,"observing escape moved or settled the body again");SaveStore.Validate(s.State);
        s.SetColdStorage(c.Id,true);
        Require(c.Family.ColdStorage.Stored&&s.WaterAttemptStatus(c)==null,"cold storage exposed a previous live attempt");
        Reload(s);
    }
    Require(escaped,"no actual successful escape was observed across the fixed ordinary seed set");
});
Check("queries are pure pause preserves facts and close-up hides rather than advances them",()=>
{
    var s=Fresh();Immerse(s);FirstAttempt(s);var c=s.Selected!;var beforeView=s.WaterAttemptStatus(c);
    string before=Snapshot(s);for(int n=0;n<100;n++)_ = s.WaterAttemptStatus(c);
    Require(Snapshot(s)==before,"read-only fact query changed random time or position");
    s.State.Settings.Pause=true;before=Snapshot(s);for(int n=0;n<30;n++)Frame(s);
    Require(Snapshot(s)==before&&s.WaterAttemptStatus(c)==beforeView,"pause advanced attempts or erased the recorded fact");
    s.State.Settings.Pause=false;Require(s.EnterCloseUp(),"close-up failed");before=WorldFacts(s);double reaction=c.ReactionRemaining;
    for(int n=0;n<30;n++)Frame(s);
    Require(s.WaterAttemptStatus(c)==null&&WorldFacts(s)==before,"close-up generated a new attempt or advanced saved world facts");
    Require(Math.Abs(c.ReactionRemaining-Math.Max(0,reaction-3))<.000001,"close-up did not advance only its allowed visual reaction timer");
    s.ExitCloseUp();for(int n=0;n<12;n++)Frame(s);
    double elapsed=s.State.WorldSeconds-c.Interaction.Water.AttemptAt;
    Console.WriteLine($"WATER_PULSE_CLOCK after12Frames={elapsed:R}, visible={WorldSimulation.WaterAttemptVisibleSeconds:R}");
    Require((s.WaterAttemptStatus(c)!=null)==(elapsed<=WorldSimulation.WaterAttemptVisibleSeconds),"pulse lifetime disagreed with the actual world clock");
    for(int n=0;n<10&&s.State.WorldSeconds-c.Interaction.Water.AttemptAt<=WorldSimulation.WaterAttemptVisibleSeconds;n++)Frame(s);
    Require(s.State.WorldSeconds<c.Interaction.Water.NextAttemptAt&&c.Interaction.Water.AttemptSequence==1&&s.WaterAttemptStatus(c)==null,
        "expired fact remained a current pulse or expiry invented another attempt");
});
Check("rescue and a real second immersion do not reuse the previous attempt identity",()=>
{
    var s=Fresh();Immerse(s);FirstAttempt(s);var c=s.Selected!;string old=c.Interaction.Water.EventId!;
    Require(s.RescueFromWater(c.Id).Success&&s.WaterAttemptStatus(c)==null,"human rescue was misreported as another autonomous attempt");
    Immerse(s);var w=c.Interaction.Water;
    Require(w.EventId!=old&&w.AttemptSequence==0&&w.AttemptGeneration==null&&w.AttemptOutcome==null&&w.AttemptAt==-100,
        "a new immersion replayed the previous body's attempt");
    FirstAttempt(s);Require(s.WaterAttemptStatus(c)?.EventId==w.EventId&&w.AttemptSequence==1,"new episode did not start a new attempt chain");
    SaveStore.Validate(s.State);
});
Check("attempt exhaustion changes generation without replacing the immersion or colliding with its first sequence",()=>
{
    var s=Fresh();Immerse(s);FirstAttempt(s);var c=s.Selected!;var w=c.Interaction.Water;string eventId=w.EventId!;
    // Explicit counter-exhaustion fixture, after a real eligible attempt.
    w.AttemptSequence=long.MaxValue;SaveStore.Validate(s.State);
    for(int n=0;n<40&&w.AttemptGeneration==null;n++)Frame(s);
    Require(w.EventId==eventId&&w.AttemptSequence==1&&Guid.TryParseExact(w.AttemptGeneration,"N",out _)&&
        s.WaterAttemptStatus(c)?.Generation==w.AttemptGeneration,"attempt counter overflowed or reused an old compound identity");
    Reload(s);
});
Check("death and another viewed region cannot expose an old live attempt",()=>
{
    var s=Fresh();Immerse(s);FirstAttempt(s);var c=s.Selected!;long sequence=c.Interaction.Water.AttemptSequence;
    s.State.Regions.Add(new(){Id="other",Name="别处"});s.State.ActiveRegionId="other";
    Require(s.WaterAttemptStatus(c)==null,"other courtyard showed an old attempt");s.State.ActiveRegionId=c.RegionId;
    s.Hurt(c,1000,"实际致死伤势",null,BodyPart.Side);
    Require(c.Condition==PhysicalCondition.Dead&&s.WaterAttemptStatus(c)==null&&c.Interaction.Water.AttemptSequence==sequence,
        "death replayed or manufactured a live attempt");
    var copy=Reload(s);Require(copy.WaterAttemptStatus(copy.Selected!)==null,"dead saved attempt restarted after load");
});
Check("old saves without attempt fields stay empty and invalid causal data is rejected",()=>
{
    var s=Fresh();Immerse(s);FirstAttempt(s);
    var json=JsonNode.Parse(Snapshot(s))!;var water=json["Creatures"]![0]!["Interaction"]!["Water"]!.AsObject();
    foreach(string key in new[]{"AttemptSequence","AttemptGeneration","AttemptAt","AttemptOutcome"})water.Remove(key);
    var old=json.Deserialize<WorldState>(SaveStore.JsonOptions)!;SaveStore.Validate(old);var legacy=new WorldSimulation(old);
    Require(legacy.Selected!.Interaction.Water.AttemptSequence==0&&legacy.WaterAttemptStatus(legacy.Selected)==null,"old schedule became invented attempt history");
    foreach(int variant in Enumerable.Range(0,6))
    {
        var copy=Reload(s);var w=copy.Selected!.Interaction.Water;
        switch(variant)
        {
            case 0:w.AttemptAt=double.NaN;break;
            case 1:w.AttemptAt=copy.State.WorldSeconds+1;break;
            case 2:w.AttemptSequence=-1;break;
            case 3:w.AttemptGeneration="invalid";break;
            case 4:w.AttemptOutcome=(WaterAttemptOutcome)999;break;
            case 5:w.AttemptSequence=0;break;
        }
        bool rejected=false;try{SaveStore.Validate(copy.State);}catch(InvalidDataException){rejected=true;}
        Require(rejected,"bad attempt identity or time was accepted");
    }
});

return failures==0?0:1;

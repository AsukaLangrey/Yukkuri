using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Require(bool condition,string why){if(!condition)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation sim)=>JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
WorldSimulation Brood()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();sim.State.RoomObjects.Clear();
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.X=-2+n*2;c.Z=0;c.TargetX=c.X;c.TargetZ=0;
        c.Fullness=90;c.Energy=90;c.Interaction.Hydration=90;c.Family.NextSocialAt=10000;
        c.Family.NextCareAt=10000;c.Interaction.NextAutonomyAt=10000;
    }
    // Existing-pregnancy component fixture; all later contact is through the API.
    sim.Selected!.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,
        EventId="completion-fixture",FatherId=sim.State.Creatures[1].Id,PlannedChildren=2};
    return new(JsonSerializer.Deserialize<WorldState>(Snapshot(sim),SaveStore.JsonOptions)!);
}
InteractionInput Stem(InteractionAction action)=>new(action,BodyPart.Side,Duration:.1,ObjectId:"completion-fixture",TargetKind:InteractionTargetKind.Stem);
InteractionInput Detached(InteractionAction action,string? supply=null)=>new(action,BodyPart.Side,Duration:.1,ObjectId:"completion-fixture",
    TargetKind:InteractionTargetKind.DetachedStem,SupplyObjectId:supply);
void Complete(WorldSimulation sim,InteractionInput input)
{for(int n=0;n<16;n++)Require(sim.ContinueInteractionGesture(input,true).Success,"continuous action refused");}
WorldSimulation Loose()
{
    var sim=Brood();var input=Stem(InteractionAction.CutStem);
    Require(sim.BeginInteractionGesture(sim.Selected!.Id,input).Success,"cut begin refused");Complete(sim,input);
    Require(sim.InteractionCompletionProgress==1,"cut did not expose completed contact");sim.EndInteractionGesture();return sim;
}

Check("support progress is actual uninterrupted contact and observation never mutates time or inventory",()=>
{
    var sim=Brood();sim.State.Medicine=1;var input=Stem(InteractionAction.SupportStem);
    Require(sim.InteractionCompletionProgress==null,"idle exposed progress");
    Require(sim.BeginInteractionGesture(sim.Selected!.Id,input).Success&&sim.InteractionCompletionProgress==0,"initial progress wrong");
    for(int n=0;n<8;n++)sim.ContinueInteractionGesture(input,true);
    Require(Math.Abs(sim.InteractionCompletionProgress!.Value-.5)<.000001,"progress used total hold instead of contact");
    string before=Snapshot(sim);for(int n=0;n<100;n++)_ = sim.InteractionCompletionProgress;
    Require(Snapshot(sim)==before,"progress query mutated state");
    sim.ContinueInteractionGesture(input,false);Require(sim.InteractionCompletionProgress==0,"leaving contact did not reset progress");
    Complete(sim,input);Require(sim.InteractionCompletionProgress==1&&sim.State.Medicine==0,"successful support did not finish once");
    sim.ContinueInteractionGesture(input,false);Require(sim.InteractionCompletionProgress==1,"completed hold lost one-shot completion");
    sim.EndInteractionGesture();Require(sim.InteractionCompletionProgress==null,"released gesture retained a progress bar");
});

Check("cut bottle and clearing share one timed channel while burn and carry expose no completion percentage",()=>
{
    var sim=Loose();Require(sim.BuyStemVessel(),"bottle purchase failed");var input=Detached(InteractionAction.BottleStem);
    Require(sim.BeginDetachedStemGesture("completion-fixture",input).Success,"bottle begin refused");
    sim.ContinueInteractionGesture(input,true);Require(sim.InteractionCompletionProgress>0&&sim.InteractionCompletionProgress<1,"bottle contact not represented");
    sim.ContinueInteractionGesture(input,false);Require(sim.InteractionCompletionProgress==0,"bottle gap not represented");
    Complete(sim,input);Require(sim.InteractionCompletionProgress==1,"bottle completion missing");sim.EndInteractionGesture();
    input=Detached(InteractionAction.Lift);Require(sim.BeginDetachedStemGesture("completion-fixture",input).Success,"lift refused");
    Require(sim.InteractionCompletionProgress==null,"carry invented completion");sim.EndInteractionGesture();
    input=Detached(InteractionAction.Burn);Require(sim.BeginDetachedStemGesture("completion-fixture",input).Success,"burn refused");
    sim.ContinueInteractionGesture(input,true);Require(sim.InteractionCompletionProgress==null,"burn invented completion");sim.EndInteractionGesture();
    for(int n=0;n<6&&sim.State.DetachedStemBroods[0].Phase!=DetachedStemPhase.Failed;n++)
        Require(sim.ExecuteInteraction("completion-fixture",Detached(InteractionAction.Punch) with {Force=1}).Success,"terminal damage refused");
    Require(sim.State.DetachedStemBroods[0].Phase==DetachedStemPhase.Failed,"actual impacts did not reach failed state");
    input=Detached(InteractionAction.ClearStem);Require(sim.BeginDetachedStemGesture("completion-fixture",input).Success,"clear begin refused");
    Complete(sim,input);Require(sim.InteractionCompletionProgress==1&&sim.State.DetachedStemBroods[0].Cleared,"clear completion missing");
});

Check("real held meal and water expose timed contact but normal body actions do not",()=>
{
    foreach(var action in new[]{InteractionAction.Drink,InteractionAction.FeedMeal})
    {
        var sim=Loose();Require(sim.BuyStemVessel(),"purchase failed");var bottle=Detached(InteractionAction.BottleStem);
        Require(sim.BeginDetachedStemGesture("completion-fixture",bottle).Success,"bottle refused");Complete(sim,bottle);sim.EndInteractionGesture();
        // Existing resource-level fixture, not an automatic refill or hidden source.
        var brood=sim.State.DetachedStemBroods[0];brood.Hydration=50;brood.Nutrition=50;
        var source=new RoomObject{Kind=action==InteractionAction.Drink?RoomObjectKind.Water:RoomObjectKind.Meal,
            RegionId=brood.RegionId,X=-5,Z=-5,Portions=1};sim.State.RoomObjects.Add(source);
        Require(sim.HoldSupply(source.Id),"real source pickup failed");
        var input=Detached(action,source.Id);Require(sim.BeginDetachedStemGesture("completion-fixture",input).Success,"refill begin refused");
        for(int n=0;n<8;n++)sim.ContinueInteractionGesture(input,true);
        Require(Math.Abs(sim.InteractionCompletionProgress!.Value-.5)<.000001&&source.Portions==1,"refill spent early or lacked timed contact");
        for(int n=0;n<8;n++)sim.ContinueInteractionGesture(input,true);
        Require(sim.InteractionCompletionProgress==1&&source.Portions==0,"refill did not settle once");sim.EndInteractionGesture();
    }
    var body=Brood();Require(body.BeginInteractionGesture(body.Selected!.Id,new(InteractionAction.Burn,BodyPart.Side,Duration:.1)).Success,"body burn refused");
    Require(body.InteractionCompletionProgress==null,"ordinary body action got a stem completion bar");
});

Console.WriteLine($"Failures: {failures}");return failures==0?0:1;

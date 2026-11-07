using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation s,double seconds){for(int i=0;i<(int)Math.Round(seconds*20);i++){s.AdvanceInteractionPresentation(.05);s.Advance(.05);}}
string Json(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation s){SaveStore.Validate(s.State);var state=JsonSerializer.Deserialize<WorldState>(Json(s),SaveStore.JsonOptions)!;SaveStore.Validate(state);return new(state);}
WorldSimulation Pair(ulong seed=42)
{
    var s=WorldSimulation.CreateCourtyardEconomy(seed);s.State.RoomObjects.Clear();s.State.Creatures.RemoveAt(2);s.State.AutonomousBreeding=false;
    for(int n=0;n<2;n++)
    {
        var c=s.State.Creatures[n];c.X=c.TargetX=n*1.65;c.Z=c.TargetZ=0;
        c.Fullness=c.Interaction.Hydration=c.Health=c.Energy=100;c.Pain=c.Fear=0;c.Trust=90;
        c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;c.Family.NextSocialAt=c.Family.NextCareAt=10000;
    }
    return s;
}
Creature[] Birth(WorldSimulation s,bool atBirth=false)
{
    var m=s.State.Creatures[0];var f=s.State.Creatures[1];
    string message=s.RequestPregnancy(m.Id,f.Id,BirthMethod.Stem);
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing,message);
    if(atBirth)
    {
        // Inheritance must be checked at actual birth, before subsequent lived
        // experience can legitimately award an ability completion.
        for(int n=0;n<5600;n++)
        {
            Tick(s,.05);
            if(s.State.Creatures.Any(c=>c.Family.MotherId==m.Id&&c.Family.BirthEventId!=null))break;
        }
    }
    else Tick(s,280);
    var babies=s.State.Creatures.Where(c=>c.Family.MotherId==m.Id&&c.Family.FatherId==f.Id&&c.Family.BirthEventId!=null).ToArray();
    Require(babies.Length>=2,"actual gestation did not produce brood: "+m.Family.LastEvent);
    return babies;
}
void Sell(WorldSimulation s,Creature c)
{
    int money=s.State.Money;int price=CourtyardEconomy.Quote(c).Price;
    string result=s.SellCreatureToShop(c.Id,price);
    Require(!s.State.Creatures.Contains(c)&&s.State.Money==money+price,result);
}

Check("normal starting yard reaches its first autonomous birth",()=>
{
    var s=WorldSimulation.CreateCourtyardEconomy();SaveStore.Validate(s.State);Tick(s,400);
    Require(s.State.CompletedBirths.Count>0,"starting pair did not breed: "+s.State.Creatures[0].Family.LastEvent);
    Reload(s);
});
Check("price priorities, safe needs plateau, cosmetic modifiers",()=>
{
    var s=Pair();var c=s.Selected!;
    c.IsYoung=true;c.Body.AgeSeconds=0;
    int young=CourtyardEconomy.Quote(c).Price;c.Body.AgeSeconds=1700;
    Require(CourtyardEconomy.Quote(c).Price<young,"older infant more valuable");c.IsYoung=false;
    int adult=CourtyardEconomy.Quote(c).Price;Require(adult<young,"adult age premium reversed");
    foreach(string rank in new[]{"铜牌","银牌","金牌"}){c.Registration.Grade=rank;int next=CourtyardEconomy.Quote(c).Price;Require(next>adult,"rank ordering");adult=next;}
    c.Registration.Grade="未评级";double core=CourtyardEconomy.Quote(c).CoreValue;
    c.Interaction.Development.Skills[LifeSkill.Cleaning]=new(){Completed=10};Require(CourtyardEconomy.Quote(c).CoreValue>core,"actual ability no value");
    c.Fullness=c.Interaction.Hydration=25;int safe=CourtyardEconomy.Quote(c).Price;
    c.Fullness=c.Interaction.Hydration=100;Require(CourtyardEconomy.Quote(c).Price==safe,"feeding within safe zone raises price");
    c.Fullness=24;Require(CourtyardEconomy.Quote(c).Price<safe,"hunger no penalty");c.Fullness=100;c.Interaction.Hydration=10;
    Require(CourtyardEconomy.Quote(c).Price<safe,"thirst no penalty");c.Interaction.Hydration=100;
    c.Interaction.Dirt=100;int dirty=CourtyardEconomy.Quote(c).Price;c.Interaction.Dirt=0;Require(CourtyardEconomy.Quote(c).Price>dirty,"cleaning no value");
    core=CourtyardEconomy.Quote(c).CoreValue;c.Health=50;Require(CourtyardEconomy.Quote(c).CoreValue==core&&CourtyardEconomy.Quote(c).Health==.75,"health changes core");
});
Check("read-only quotes, stale confirmation, duplicate sale, reload and legacy save",()=>
{
    var s=Pair();var c=s.Selected!;string before=Json(s);var quote=CourtyardEconomy.Quote(c);s.CreatureSaleReason(c.Id);
    Require(before==Json(s),"preview mutated state");s.SellCreatureToShop(c.Id,quote.Price+1);Require(before==Json(s),"stale quote mutates state");
    Sell(s,c);int money=s.State.Money;s.SellCreatureToShop(c.Id,quote.Price);Require(s.State.Money==money,"duplicate payment");
    var copy=Reload(s);Require(copy.State.CreatureSalesRevenue==quote.Price&&copy.State.CreatureSales.Count==1,"sale lost on reload");
    Tick(copy,2);SaveStore.Validate(copy.State);
    var legacy=JsonNode.Parse(Json(Pair()))!.AsObject();legacy.Remove("CreatureSales");legacy.Remove("CreatureSalesRevenue");legacy.Remove("EconomyProgression");
    var old=JsonSerializer.Deserialize<WorldState>(legacy.ToJsonString(),SaveStore.JsonOptions)!;SaveStore.Validate(old);
    Require(old.CreatureSalesRevenue==0&&!old.EconomyProgression,"invented old progress");
});
Check("real birth, infant sale and parent departure retain ancestry",()=>
{
    var s=Pair();var babies=Birth(s);var m=s.State.Creatures[0];
    Require(s.CreatureSaleReason(m.Id)!=null,"recovering mother sale allowed");
    Sell(s,babies[0]);s=Reload(s);Tick(s,180);m=s.State.Creatures[0];
    Require(m.Family.Pregnancy.Phase==PregnancyPhase.None,"recovery did not finish");Sell(s,m);s=Reload(s);
    Require(s.State.Creatures.Any(c=>c.Family.MotherId==m.Id),"ancestry erased");
    Tick(s,5);SaveStore.Validate(s.State);
});
Check("income unlocks tools permanently; no immediate arrival arbitrage",()=>
{
    var s=WorldSimulation.CreateCourtyardEconomy();s.State.AutonomousBreeding=false;
    int money=s.State.Money;Require(!s.BuyShopProduct("food-station")&&s.State.Money==money,"locked item bought");
    foreach(var c in s.State.Creatures.ToArray())Sell(s,c);
    Require(s.State.CreatureSalesRevenue>=200&&s.BuyShopProduct("food-station"),"threshold did not unlock");
    long revenue=s.State.CreatureSalesRevenue;Require(s.BuyShopProduct("food")&&s.BuyShopProduct("water"),"cannot reinvest");
    Require(s.State.CreatureSalesRevenue==revenue&&Reload(s).ShopUnlockReason("food-station")==null,"spending relocked tool");
    var result=s.PlaceNewCreature(new("reimu",true),0,0);Require(result.Success,result.Message);
    var c2=s.Selected!;Require(CourtyardEconomy.Quote(c2).Price<WorldSimulation.NewCreaturePrice,"instant resale profit");Sell(s,c2);Reload(s);
});
Check("sale interrupts care without destroying parcels or fabricating inventory",()=>
{
    var s=WorldSimulation.CreateCourtyardEconomy();var adult=s.State.Creatures[0];var child=s.State.Creatures[2];
    var parcel=new RoomObject{Kind=RoomObjectKind.Meal,Portions=1,CarrierId=adult.Id,X=adult.X,Z=adult.Z};
    s.State.RoomObjects.Add(parcel);adult.Family.CareTargetId=child.Id;adult.Family.CareParcelId=parcel.Id;
    adult.Interaction.Development.Duty=LifeDuty.Care;adult.Interaction.Development.RecipientId=child.Id;
    SaveStore.Validate(s.State);int food=s.State.Food;Sell(s,child);
    Require(parcel.CarrierId==null&&parcel.Portions==1&&s.State.Food==food&&adult.Family.CareTargetId==null&&
        adult.Interaction.Development.Duty==LifeDuty.None,"care parcel or duty not settled");Reload(s);
    s.State.Money=int.MaxValue;string before=Json(s);s.SellCreatureToShop(adult.Id,CourtyardEconomy.Quote(adult).Price);
    Require(before==Json(s),"overflow rejection changed state");
    s.State.CreatureSalesRevenue++;bool rejected=false;try{SaveStore.Validate(s.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"tampered ledger accepted");
});
Check("high quality parents statistically produce better offspring without skill or rank inheritance",()=>
{
    var low=new List<double>();var high=new List<double>();
    for(ulong seed=1;seed<=20;seed++)foreach(bool good in new[]{false,true})
    {
        var s=Pair(seed);
        foreach(var c in s.State.Creatures){c.Personality.Kindness=good?.9:.1;c.Personality.Carelessness=good?.1:.9;c.Aggression=good?.1:.9;c.Patience=good?.9:.1;
            c.Registration.Grade="金牌";c.Interaction.Development.Skills[LifeSkill.Cleaning]=new(){Completed=10};}
        var babies=Birth(s,atBirth:true);
        foreach(var b in babies){(good?high:low).Add(CourtyardEconomy.Quality(b));Require(b.Registration.Grade=="未评级"&&CourtyardEconomy.Ability(b)==0,
            $"rank or ability inherited: seed={seed}, good={good}, age={b.Body.AgeSeconds}, grade={b.Registration.Grade}, skills={JsonSerializer.Serialize(b.Interaction.Development,SaveStore.JsonOptions)}");}
    }
    Require(high.Average()>low.Average()+40&&high.Distinct().Count()>10,"inheritance lacks quality bias or variation");
    Console.WriteLine($"inheritance: high n={high.Count} mean={high.Average():F2}; low n={low.Count} mean={low.Average():F2}");
});
Check("paid supplies sustain real growth and repeated profitable breeding",()=>
{
    var s=Pair();int openingMoney=s.State.Money;int openingFood=s.State.Food,openingWater=s.State.StationWaterPortions;
    foreach(var c in s.State.Creatures)c.Family.NextSocialAt=0;
    var babies=Birth(s);var keep=babies[0];
    foreach(var b in babies.Skip(1))Sell(s,b);
    Require(s.BuyShopProduct("wrap"),"cannot buy basic warmth");
    Require(s.ExecuteInteraction(keep.Id,new(InteractionAction.Wrap,BodyPart.Side)).Success,"wrap failed");
    void CareTick()
    {
        foreach(var c in s.State.Creatures.ToArray())
        {
            Require(c.Condition!=PhysicalCondition.Dead,"care journey death: "+c.Name);
            if(s.State.Creatures.Any(m=>m.Family.Pregnancy.Phase==PregnancyPhase.Preparing))continue;
            if(c.Interaction.EatingRemaining<=0&&!c.Interaction.Physiology.Active)
            {
                InteractionAction? action=Math.Min(c.Interaction.Hydration,c.Fullness)>=65?null:c.Interaction.Hydration<c.Fullness?InteractionAction.Drink:InteractionAction.FeedMeal;
                if(action is { } act)
                {
                    if(s.State.Food==0)Require(s.BuyShopProduct("food"),"food insolvency");
                    if(s.State.StationWaterPortions==0)Require(s.BuyShopProduct("water"),"water insolvency");
                    if(c.Trust<15)
                    {
                        var kind=act==InteractionAction.Drink?RoomObjectKind.Water:RoomObjectKind.Meal;
                        if(!s.State.RoomObjects.Any(o=>o.Kind==kind&&o.Portions>0&&WorldSimulation.Distance(o.X,o.Z,c.X,c.Z)<1))
                            s.ExecuteInteraction(c.Id,new(act==InteractionAction.Drink?InteractionAction.PlaceWater:InteractionAction.PlaceMeal,BodyPart.Side,X:c.X+.4,Z:c.Z));
                    }
                    var input=new InteractionInput(act,BodyPart.Mouth,Force:.15,Duration:.1);
                    if(s.BeginInteractionGesture(c.Id,input).Success)
                    {
                        for(int k=0;k<12&&!s.InteractionIntakeAccepted;k++){s.ContinueInteractionGesture(input,true);Tick(s,.1);}
                        s.EndInteractionGesture();
                    }
                }
            }
            if(c.Energy<60&&c.Interaction.RestRemaining<=0)s.ExecuteInteraction(c.Id,new(InteractionAction.Rest));
        }
        Tick(s,1);
    }
    double until=s.State.WorldSeconds+2000;
    while(keep.IsYoung&&s.State.WorldSeconds<until)CareTick();
    Require(!keep.IsYoung&&keep.Body.AgeSeconds>=1800,"real nursery did not grow: "+keep.Fullness+"/"+keep.Health+"/"+keep.Family.Warmth);
    Require(s.ExecuteInteraction(keep.Id,new(InteractionAction.Unwrap,BodyPart.Side)).Success,"unwrap failed");
    while(s.CreatureSaleReason(keep.Id)!=null&&s.State.WorldSeconds<until+30)CareTick();
    Sell(s,keep);s=Reload(s);
    for(int round=0;round<3;round++)
    {
        var m=s.State.Creatures[0];var f=s.State.Creatures[1];
        double deadline=s.State.WorldSeconds+900;
        while(m.Family.Pregnancy.Phase!=PregnancyPhase.Preparing&&s.State.WorldSeconds<deadline)
        {s.RequestPregnancy(m.Id,f.Id,BirthMethod.Stem);if(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing)break;CareTick();}
        int births=s.State.CompletedBirths.Count;
        while(s.State.CompletedBirths.Count==births&&s.State.WorldSeconds<deadline){if(m.Family.Pregnancy.Phase==PregnancyPhase.None)s.RequestPregnancy(m.Id,f.Id,BirthMethod.Stem);CareTick();}
        Require(s.State.CompletedBirths.Count>births,"next round birth failed: "+m.Family.LastEvent);
        foreach(var b in s.State.Creatures.Where(c=>c.IsYoung).ToArray())Sell(s,b);
        while(m.Family.Pregnancy.Phase!=PregnancyPhase.None&&s.State.WorldSeconds<deadline)CareTick();
        s=Reload(s);
    }
    Require(s.State.Money>openingMoney,"breeding did not cover care costs");
    Require(s.State.Food<openingFood||s.State.StationWaterPortions<openingWater,"care did not consume real stock");
    Require(s.BuyShopProduct("food")&&s.BuyShopProduct("water")&&s.BuyShopProduct("food-station"),"reinvestment failed");
    Require(s.State.CreatureSalesRevenue>=1000&&s.BuyShopProduct("shelter")&&s.BuyShopProduct("inducer"),"later tool tiers did not unlock");
    Console.WriteLine($"journey: {s.State.WorldSeconds:F1} simulated seconds, births={s.State.CompletedBirths.Count}, revenue={s.State.CreatureSalesRevenue}, balance={s.State.Money}, food={s.State.Food}, water={s.State.StationWaterPortions}");
});
Console.WriteLine($"FAILURES {failures}");return failures==0?0:1;

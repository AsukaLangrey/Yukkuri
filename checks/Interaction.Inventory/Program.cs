using System.Text.Json;
using Yukkuri.Simulation;

// Check source only on the development workstation; execution belongs to the authorised remote host.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
WorldSimulation Fresh()
{
    var s=WorldSimulation.CreateInteractionPlaytest();var c=s.Selected!;
    s.State.Creatures.RemoveAll(o=>o!=c);s.State.RoomObjects.Clear();
    c.X=c.Z=c.TargetX=c.TargetZ=0;c.Fullness=55;c.Energy=90;c.Trust=80;c.Interaction.Hydration=90;
    c.Family.NextCareAt=c.Family.NextSocialAt=c.Interaction.NextAutonomyAt=10000;
    return s;
}
InteractionInput Meal(string? id=null)=>new(InteractionAction.FeedMeal,BodyPart.Mouth,Duration:.1,ObjectId:id);
void Hold(WorldSimulation s,InteractionInput input,int frames)
{for(int n=0;n<frames;n++)s.ContinueInteractionGesture(input,true);}
void Offer(WorldSimulation s,string? id=null)
{
    var input=Meal(id);Require(s.BeginInteractionGesture(s.Selected!.Id,input).Success,"actual offered meal could not begin");
    Hold(s,input,9);Require(s.InteractionIntakeAccepted,"meal did not actually enter");
}
RoomObject Reserve(WorldSimulation s)
{
    var c=s.Selected!;var obj=new RoomObject{Id="reserved-food",RegionId=c.RegionId,Kind=RoomObjectKind.Meal,X=.5,Z=0,Portions=1};
    s.State.RoomObjects.Add(obj);s.Advance(.25);
    Require(c.Interaction.NaturalIntakePending&&obj.Portions==0,"actual natural intake failed to reserve its portion");
    return obj;
}

Check("playtest free purchases replenish actual inventory and a held offer consumes once",()=>
{
    var s=Fresh();var c=s.Selected!;s.State.Food=0;s.State.Money=0;
    Require(s.UsesConsumableStock&&s.State.UnlimitedResources&&!s.BeginInteractionGesture(c.Id,Meal()).Success,"free money granted invisible food");
    Require(s.BuyFood()&&s.State.Money==0&&s.State.Food==WorldSimulation.FoodBundleCount,"free purchase skipped real inventory");
    Offer(s);int stock=s.State.Food;double fullness=c.Fullness;
    Hold(s,Meal(),30);s.EndInteractionGesture(cancelled:true);
    Require(stock==WorldSimulation.FoodBundleCount-1&&s.State.Food==stock&&c.Fullness==fullness,"hold or cancel duplicated or refunded accepted food");
    SaveStore.Validate(s.State);
});
Check("unaccepted offers pause and missing portions do not consume food",()=>
{
    var s=Fresh();var c=s.Selected!;s.State.Food=1;double fullness=c.Fullness;var input=Meal();
    Require(s.BeginInteractionGesture(c.Id,input).Success,"offer refused");Hold(s,input,3);s.EndInteractionGesture(cancelled:true);
    Require(s.State.Food==1&&c.Fullness==fullness&&c.Interaction.EatingRemaining==0,"cancelled preparation consumed");
    Require(s.BeginInteractionGesture(c.Id,input).Success,"second offer refused");Hold(s,input,3);
    s.State.Settings.Pause=true;string before=Snapshot(s);Hold(s,input,20);s.EndInteractionGesture(cancelled:true);
    Require(Snapshot(s)==before,"pause changed food or the body");s.State.Settings.Pause=false;
    s.State.Food=0;Require(!s.BeginInteractionGesture(c.Id,input).Success&&c.Fullness==fullness,"empty stock produced a meal");
});
Check("placing then hand-feeding a real supply consumes its portion without charging stock twice",()=>
{
    var s=Fresh();var c=s.Selected!;s.State.Food=1;
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.PlaceMeal,X:1.2,Z:0)).Success&&s.State.Food==0,"ground portion was not charged");
    var obj=s.State.RoomObjects.Single();Require(s.HoldSupply(obj.Id),"actual food could not be picked up");
    Offer(s,obj.Id);Require(obj.Portions==0&&s.State.Food==0,"held entry charged warehouse or retained its consumed portion");
    s.EndInteractionGesture(cancelled:true);Require(s.ReleaseSupply(obj.Id,1.2,0),"empty held object failed to release");
    Require(!s.PutAwayRoomObject(obj.Id)&&s.State.Food==0,"consumed physical portion refunded after release");
});
Check("natural reserved food returns once before entry and never after entry",()=>
{
    foreach(bool accepted in new[]{false,true})
    {
        var s=Fresh();s.State.Food=0;var obj=Reserve(s);var c=s.Selected!;
        if(accepted)
            for(int n=0;n<50&&c.Interaction.NaturalIntakePending;n++)s.AdvanceInteractionPresentation(.05);
        Require(!accepted||!c.Interaction.NaturalIntakePending&&c.Interaction.Digestion.Exposures==1,"natural mouthful never entered");
        // The fully consumed source can already be pruned; either way it cannot refund.
        bool removed=s.PutAwayRoomObject(obj.Id);
        Require(accepted?s.State.Food==0:removed&&s.State.Food==1&&!c.Interaction.NaturalIntakePending,
            "return confused a reservation with food already accepted");
        Require(!s.PutAwayRoomObject(obj.Id)&&s.State.Food==(accepted?0:1),"repeated return cloned a portion");
        SaveStore.Validate(s.State);
    }
});
Check("full inventory refuses reserved-food return before cancelling any intake",()=>
{
    var s=Fresh();var obj=Reserve(s);s.State.Food=int.MaxValue;
    string before=Snapshot(s);Require(!s.PutAwayRoomObject(obj.Id)&&Snapshot(s)==before,"full return cancelled the reserved intake or overflowed stock");
    s.State.Food=int.MaxValue-1;
    Require(s.PutAwayRoomObject(obj.Id)&&s.State.Food==int.MaxValue&&!s.Selected!.Interaction.NaturalIntakePending,"one exact remaining slot did not accept one returned reservation");
    SaveStore.Validate(s.State);
});
Check("ordinary care requires real medicine and cancelled or completed contact cannot duplicate a dose",()=>
{
    var s=Fresh();var c=s.Selected!;s.Hurt(c,12,"检查护理用实际伤害",null,BodyPart.Side);s.State.Medicine=0;
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.1);double health=c.Health;
    Require(!s.BeginInteractionGesture(c.Id,input).Success&&c.Health==health,"empty test-mode stock supplied treatment");
    Require(s.BuyMedicine()&&s.State.Money==0&&s.State.Medicine==WorldSimulation.MedicineBundleCount,"free medicine purchase failed");
    Require(s.BeginInteractionGesture(c.Id,input).Success,"care begin refused");Hold(s,input,7);s.EndInteractionGesture(cancelled:true);
    Require(s.State.Medicine==3&&c.Health==health&&c.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining==0,"partial care granted a dose");
    Require(s.BeginInteractionGesture(c.Id,input).Success,"care restart refused");Hold(s,input,20);
    Require(s.State.Medicine==2&&c.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining>0,"real completed care did not use one dose");
    Hold(s,input,30);s.EndInteractionGesture(cancelled:true);Require(s.State.Medicine==2,"completed hold consumed repeatedly or refunded");
});
Check("legacy feeding and treatment share actual playtest inventory",()=>
{
    var s=Fresh();var c=s.Selected!;s.State.Food=0;s.State.Medicine=0;
    Require(!s.Feed(c.Id),"legacy feed bypassed empty stock");s.BuyFood();Require(s.Feed(c.Id)&&s.State.Food==19,"legacy feed failed to debit");
    s.Hurt(c,12,"实际护理伤势",null,BodyPart.Side);double health=c.Health;
    Require(!s.Contact(c.Id,new(ContactAction.Treat)).Success&&c.Health==health,"legacy care bypassed empty stock");
    s.BuyMedicine();Require(s.Contact(c.Id,new(ContactAction.Treat)).Success&&s.State.Medicine==2,"legacy care omitted actual dose");
    Require(!s.Contact(c.Id,new(ContactAction.Treat)).Success&&s.State.Medicine==2,"repeat legacy treatment consumed a failed dose");
});
Check("wait training and teasing require an existing portion and cancellation does not manufacture it",()=>
{
    var s=Fresh();var c=s.Selected!;s.State.Food=0;ulong random=s.State.RandomState;
    Require(!s.ExecuteInteraction(c.Id,new(InteractionAction.TeachWait)).Success&&c.Interaction.Learning.Count==0&&s.State.RandomState==random&&
        s.State.RoomObjects.Count==0,"empty wait training left a paid attempt or fabricated bait");
    var tease=new InteractionInput(InteractionAction.Tease,BodyPart.Mouth,Duration:.1,X:.5,Z:0);
    Require(!s.BeginInteractionGesture(c.Id,tease).Success,"empty stock supplied invisible bait");
    s.State.Food=1;Require(s.BeginInteractionGesture(c.Id,tease).Success,"real available bait refused");Hold(s,tease,8);s.EndInteractionGesture(cancelled:true);
    Require(s.State.Food==1&&c.Interaction.EatingRemaining==0,"non-feeding tease consumed or duplicated food");
    Require(s.ExecuteInteraction(c.Id,new(InteractionAction.TeachWait)).Success&&s.State.Food==0&&s.State.RoomObjects.Count==1,"training did not reserve its real ground portion");
    string id=s.State.RoomObjects[0].Id;s.ExecuteInteraction(c.Id,new(InteractionAction.Stop));
    Require(s.State.Food==0&&s.PutAwayRoomObject(id)&&s.State.Food==1&&!s.PutAwayRoomObject(id),"cancelled cue refunded separately from its actual object");
});
Check("free and paid purchasing reject full bundles atomically without changing old legal stocks",()=>
{
    foreach(bool free in new[]{false,true})
    {
        var s=Fresh();s.State.UnlimitedResources=free;s.State.Money=100;
        s.State.Food=int.MaxValue-WorldSimulation.FoodBundleCount+1;s.State.Medicine=int.MaxValue-WorldSimulation.MedicineBundleCount+1;
        SaveStore.Validate(s.State);string before=Snapshot(s);
        Require(!s.BuyFood()&&!s.BuyMedicine()&&Snapshot(s)==before,"full-stock purchase lost money or truncated a bundle");
        s.State.Food=int.MaxValue-WorldSimulation.FoodBundleCount;s.State.Medicine=int.MaxValue-WorldSimulation.MedicineBundleCount;
        Require(s.BuyFood()&&s.BuyMedicine()&&s.State.Food==int.MaxValue&&s.State.Medicine==int.MaxValue&&
            s.State.Money==(free?100:75),"exact-capacity purchase did not preserve original price and bundle");
        SaveStore.Validate(s.State);
    }
    var poor=Fresh();poor.State.UnlimitedResources=false;poor.State.Money=0;string snapshot=Snapshot(poor);
    Require(!poor.BuyFood()&&!poor.BuyMedicine()&&Snapshot(poor)==snapshot,"insufficient money partially committed a purchase");
});
Check("feeder refilling respects actual stock and refuses overflowing storage before debiting",()=>
{
    var s=Fresh();var feeder=new Facility{Kind=FacilityKind.Feeder,Stock=0};s.ActiveRegion.Facilities.Add(feeder);s.State.Food=3;
    Require(s.Refill()&&feeder.Stock==3&&s.State.Food==0&&!s.Refill(),"test-mode feeder invented a refill");
    s.State.Food=10;feeder.Stock=int.MaxValue-9;string before=Snapshot(s);
    Require(!s.Refill()&&Snapshot(s)==before,"overflowing feeder lost warehouse food");
});
Check("non-playtest unlimited sandbox keeps its legacy unlimited materials",()=>
{
    var s=WorldSimulation.CreateMilestone(sandbox:true);var c=s.Selected!;s.State.Food=s.State.Medicine=0;
    Require(!s.UsesConsumableStock&&s.Feed(c.Id)&&s.State.Food==0,"legacy sandbox feeding lost its unlimited mode");
    s.Hurt(c,12,"旧沙盒护理",null,BodyPart.Side);
    Require(s.Contact(c.Id,new(ContactAction.Treat)).Success&&s.State.Medicine==0,"legacy sandbox treatment lost its unlimited mode");
    int stock=s.ActiveRegion.Facilities.Single(f=>f.Kind==FacilityKind.Feeder).Stock;
    Require(s.Refill()&&s.ActiveRegion.Facilities.Single(f=>f.Kind==FacilityKind.Feeder).Stock==stock+10&&s.State.Food==0,"legacy sandbox refill began charging inventory");
});
Check("staff care consumes only actual successful doses in a finite-resource world",()=>
{
    var s=WorldSimulation.CreateMilestone();var c=s.Selected!;s.State.Medicine=0;s.State.Settings.Accidents=false;s.State.Settings.Invasions=false;
    s.Hurt(c,25,"雇员护理测试实际伤害",null,BodyPart.Side);var wound=c.Interaction.Injuries.At(BodyPart.Side)!;
    foreach(var r in s.State.Regions)r.StaffEnabled=false;s.ActiveRegion.StaffEnabled=true;s.ActiveRegion.StaffCooldown=0;
    s.Advance(.25);Require(wound.CareRemaining==0&&s.State.Medicine==0,"staff treated without a dose");
    Require(s.BuyMedicine(),"staff supplies could not be bought");s.ActiveRegion.StaffCooldown=0;s.Advance(.25);
    Require(wound.CareRemaining>0&&s.State.Medicine==2,"successful staff treatment did not consume one dose");
    s.ActiveRegion.StaffCooldown=0;s.Advance(.25);Require(s.State.Medicine==2,"staff repeat with no applicable treatment consumed again");
});

return failures==0?0:1;

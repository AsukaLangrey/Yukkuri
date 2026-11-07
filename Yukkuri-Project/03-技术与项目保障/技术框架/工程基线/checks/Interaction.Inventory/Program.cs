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

Check("playtest paid purchases replenish actual inventory and a held offer consumes once",()=>
{
    var s=Fresh();var c=s.Selected!;s.State.Food=0;s.State.Money=0;
    Require(s.UsesConsumableStock&&!s.State.UnlimitedResources&&!s.BeginInteractionGesture(c.Id,Meal()).Success,"new courtyard bypassed finite inventory");
    s.State.UnlimitedResources=true;
    Require(!s.BeginInteractionGesture(c.Id,Meal()).Success,"unlimited flag granted invisible food");
    string before=Snapshot(s);Require(!s.BuyFood()&&Snapshot(s)==before,"courtyard purchase bypassed payment");
    s.State.Money=WorldSimulation.FoodBundlePrice;
    Require(s.BuyFood()&&s.State.Money==0&&s.State.Food==WorldSimulation.FoodBundleCount,"paid purchase skipped real inventory");
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
Check("recycling waits for intake and physical cancellation returns only unaccepted food",()=>
{
    foreach(bool accepted in new[]{false,true})
    {
        var s=Fresh();s.State.Food=0;var obj=Reserve(s);var c=s.Selected!;
        if(accepted)
            for(int n=0;n<50&&c.Interaction.NaturalIntakePending;n++)s.AdvanceInteractionPresentation(.05);
        Require(!accepted||!c.Interaction.NaturalIntakePending&&c.Interaction.Digestion.Exposures==1,"natural mouthful never entered");
        string before=Snapshot(s);
        Require(!s.PutAwayRoomObject(obj.Id)&&Snapshot(s)==before,"recycling interrupted an active mouthful");
        // Actual pickup, rather than recycling, interrupts the contact. A pending
        // portion returns to its source; an accepted portion must never return.
        Require(s.HoldSupply(obj.Id),"physical pickup did not interrupt intake");
        Require(!c.Interaction.NaturalIntakePending&&obj.Portions==(accepted?0:1)&&s.State.Food==0,
            "pickup confused a reservation with food already accepted");
        Require(s.ReleaseSupply(obj.Id,2,0),"picked-up source could not be placed clear of the body");
        Require(s.PutAwayRoomObject(obj.Id)==!accepted&&s.State.Food==(accepted?0:1),
            "idle source returned the wrong number of portions");
        Require(!s.PutAwayRoomObject(obj.Id)&&s.State.Food==(accepted?0:1),"repeated return cloned a portion");
        SaveStore.Validate(s.State);
    }
});
Check("recycling preserves active intake and full inventory refuses an idle portion atomically",()=>
{
    var s=Fresh();var obj=Reserve(s);s.State.Food=int.MaxValue;
    string before=Snapshot(s);Require(!s.PutAwayRoomObject(obj.Id)&&Snapshot(s)==before,"full return cancelled the reserved intake or overflowed stock");
    s.State.Food=int.MaxValue-1;
    before=Snapshot(s);Require(!s.PutAwayRoomObject(obj.Id)&&Snapshot(s)==before,"available stock space interrupted active intake");
    Require(s.HoldSupply(obj.Id)&&s.ReleaseSupply(obj.Id,2,0)&&obj.Portions==1&&!s.Selected!.Interaction.NaturalIntakePending,
        "physical pickup did not restore the unaccepted portion");
    s.State.Food=int.MaxValue;before=Snapshot(s);
    Require(!s.PutAwayRoomObject(obj.Id)&&Snapshot(s)==before,"full return lost the idle portion or overflowed stock");
    s.State.Food=int.MaxValue-1;
    Require(s.PutAwayRoomObject(obj.Id)&&s.State.Food==int.MaxValue,"one exact remaining slot did not accept one idle portion");
    SaveStore.Validate(s.State);
});
Check("ordinary care requires real medicine and cancelled or completed contact cannot duplicate a dose",()=>
{
    var s=Fresh();var c=s.Selected!;s.Hurt(c,12,"检查护理用实际伤害",null,BodyPart.Side);s.State.Medicine=0;
    var input=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.1);double health=c.Health;
    Require(!s.BeginInteractionGesture(c.Id,input).Success&&c.Health==health,"empty test-mode stock supplied treatment");
    int money=s.State.Money;
    Require(s.BuyMedicine()&&s.State.Money==money-WorldSimulation.MedicineBundlePrice&&s.State.Medicine==WorldSimulation.MedicineBundleCount,"paid medicine purchase failed");
    Require(s.BeginInteractionGesture(c.Id,input).Success,"care begin refused");Hold(s,input,7);s.EndInteractionGesture(cancelled:true);
    Require(s.State.Medicine==3&&c.Health==health&&c.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining==0,"partial care granted a dose");
    Require(s.BeginInteractionGesture(c.Id,input).Success,"care restart refused");Hold(s,input,20);
    Require(s.State.Medicine==2&&c.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining>0,"real completed care did not use one dose");
    Hold(s,input,30);s.EndInteractionGesture(cancelled:true);Require(s.State.Medicine==2,"completed hold consumed repeatedly or refunded");
});
// Controlled ordinary Side injury through the production Hurt API, not a claim
// that autonomous play generated the injury. No passive time is advanced here.
WorldSimulation CareFixture()
{
    var s=Fresh();var c=s.Selected!;s.Hurt(c,12,"护理中断反馈受控轻伤",null,BodyPart.Side);
    Require(c.Condition is not (PhysicalCondition.Critical or PhysicalCondition.Dead)&&
        c.Interaction.Injuries.At(BodyPart.Side) is {Damage:>0,CareRemaining:0}&&s.State.Medicine>0,
        "controlled ordinary care target was not available");
    return s;
}
string CareFacts(WorldSimulation s)
{
    var c=s.Selected!;var i=c.Interaction;
    return JsonSerializer.Serialize(new{s.State.Medicine,s.State.Food,s.State.Money,c.Health,c.Pain,c.Condition,
        c.CriticalRemaining,c.FillingIntegrity,i.Injuries,i.Digestion,i.CareContinuity,i.FootTreated,i.FootRecovery,
        i.HeadSensitivity,i.Dressings,i.DressingPart,i.DressingSide,i.DressingRemaining,s.State.Progress.CareActions},SaveStore.JsonOptions);
}
foreach(bool cancelled in new[]{false,true})
{
    foreach(bool leaveContact in new[]{false,true})
    Check($"unfinished care reports stopped on {(cancelled?"cancel":"release")}, leave contact={leaveContact}",()=>
    {
        var s=CareFixture();var c=s.Selected!;var input=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.1);
        string facts=CareFacts(s);Require(s.BeginInteractionGesture(c.Id,input).Success,"ordinary care could not begin");
        Hold(s,input,7);var pending=s.ContinueInteractionGesture(input,true);
        Require(pending.Message.Contains("正在处理伤处")&&s.InteractionGestureOwnsPose&&CareFacts(s)==facts,
            "partial care was not active or changed treatment facts");
        if(leaveContact)
        {
            var off=s.ContinueInteractionGesture(input,false);
            Require(off.Message.Contains("接触停止")&&!s.InteractionGestureOwnsPose&&CareFacts(s)==facts,
                "leaving contact did not stop without treatment");
        }
        var ended=s.EndInteractionGesture(cancelled);
        Require(!s.HasInteractionGesture&&!s.InteractionGestureOwnsPose&&CareFacts(s)==facts,
            "end retained gesture ownership or changed medicine, injury, health or care flags");
        Require(ended.Success&&!ended.Refused&&ended.Message.Contains("护理已停止")&&ended.Message.Contains("尚未使用护理包")&&
            !ended.Message.Contains("正在处理伤处")&&!ended.Message.Contains("松开"),
            "ended care still advertises an active hold: "+ended.Message);
        Require(string.IsNullOrEmpty(s.EndInteractionGesture(cancelled).Message)&&CareFacts(s)==facts,"repeated end replayed feedback or treatment");
        SaveStore.Validate(s.State);
    });
    Check($"completed care retains its result after leaving contact and {(cancelled?"cancel":"release")}",()=>
    {
        var s=CareFixture();var c=s.Selected!;var input=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.1);
        int stock=s.State.Medicine,careActions=s.State.Progress.CareActions;
        Require(s.BeginInteractionGesture(c.Id,input).Success,"ordinary completed care could not begin");Hold(s,input,20);
        var complete=s.ContinueInteractionGesture(input,true);string facts=CareFacts(s);
        Require(complete.Message.Contains("护理完成")&&s.State.Medicine==stock-1&&s.State.Progress.CareActions==careActions+1&&
            c.Interaction.Injuries.At(BodyPart.Side)!.CareRemaining>0&&c.Interaction.HasDressing(BodyPart.Side),"care did not complete exactly once");
        Hold(s,input,30);s.ContinueInteractionGesture(input,false);var ended=s.EndInteractionGesture(cancelled);
        Require(ended.Message==complete.Message&&!s.HasInteractionGesture&&CareFacts(s)==facts,
            "completed result was replaced or completed treatment was repeated/refunded");
        s.EndInteractionGesture(cancelled);Require(CareFacts(s)==facts,"repeated end changed completed care");SaveStore.Validate(s.State);
    });
}
Check("care end preserves latest wrong-part feedback and exhausted-stock refusal",()=>
{
    foreach(bool exhausted in new[]{false,true})
    {
        var s=CareFixture();var c=s.Selected!;var input=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.1);
        Require(s.BeginInteractionGesture(c.Id,input).Success,"refusal fixture could not begin");Hold(s,input,7);
        if(exhausted){s.State.Medicine=0;Hold(s,input,20);}else input=input with {Part=BodyPart.Accessory};
        var last=s.ContinueInteractionGesture(input,true);string facts=CareFacts(s);
        Require(exhausted?last.Refused&&last.Message.Contains("护理用品不足"):last.Message.Contains("不是需要处理的伤处"),"expected latest care feedback absent");
        var ended=s.EndInteractionGesture(cancelled:true);
        Require(ended.Message==last.Message&&ended.Success==last.Success&&ended.Refused==last.Refused&&CareFacts(s)==facts,
            "care end masked the latest invalid-part/refusal result or changed treatment");
    }
});
Check("care entry refusals remain non-mutating and do not leave a gesture",()=>
{
    foreach(string reason in new[]{"no injury","no medicine","invalid target"})
    {
        var s=reason=="no injury"?Fresh():CareFixture();var c=s.Selected!;
        if(reason=="no medicine")s.State.Medicine=0;
        string before=Snapshot(s);var feedback=s.BeginInteractionGesture(reason=="invalid target"?"missing-patient":c.Id,
            new(InteractionAction.Care,BodyPart.Side,Duration:.1));
        Require(!feedback.Success&&!s.HasInteractionGesture&&Snapshot(s)==before,"care entry changed a refused state: "+reason);
        Require(string.IsNullOrEmpty(s.EndInteractionGesture().Message)&&Snapshot(s)==before,"ending a refused entry created an interruption: "+reason);
    }
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
Check("courtyard purchasing always charges and rejects full bundles atomically",()=>
{
    foreach(bool unlimited in new[]{false,true})
    {
        var s=Fresh();s.State.UnlimitedResources=unlimited;s.State.Money=100;
        s.State.Food=int.MaxValue-WorldSimulation.FoodBundleCount+1;s.State.Medicine=int.MaxValue-WorldSimulation.MedicineBundleCount+1;
        SaveStore.Validate(s.State);string before=Snapshot(s);
        Require(!s.BuyFood()&&!s.BuyMedicine()&&Snapshot(s)==before,"full-stock purchase lost money or truncated a bundle");
        s.State.Food=int.MaxValue-WorldSimulation.FoodBundleCount;s.State.Medicine=int.MaxValue-WorldSimulation.MedicineBundleCount;
        Require(s.BuyFood()&&s.BuyMedicine()&&s.State.Food==int.MaxValue&&s.State.Medicine==int.MaxValue&&
            s.State.Money==100-WorldSimulation.FoodBundlePrice-WorldSimulation.MedicineBundlePrice,"exact-capacity purchase did not preserve paid price and bundle");
        SaveStore.Validate(s.State);
    }
    foreach(bool unlimited in new[]{false,true})
    {
        var poor=Fresh();poor.State.UnlimitedResources=unlimited;poor.State.Money=0;string snapshot=Snapshot(poor);
        Require(!poor.BuyFood()&&!poor.BuyMedicine()&&Snapshot(poor)==snapshot,"insufficient money partially committed a purchase");
    }
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
    s.State.Money=0;
    Require(s.BuyFood()&&s.BuyMedicine()&&s.State.Money==0&&s.State.Food==WorldSimulation.FoodBundleCount&&
        s.State.Medicine==WorldSimulation.MedicineBundleCount,"non-courtyard sandbox lost its free purchases");
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

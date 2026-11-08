using System.Text.Json;
using Yukkuri.Simulation;

// Remote-only source. Never run on the original development workstation.
int failures=0;
void BuyTool(WorldSimulation sim,string id)
{
    var product=CourtyardShop.Products.Single(p=>p.Id==id);
    int money=sim.State.Money,stock=sim.ShopQuantity(id);
    Require(sim.BuyShopProduct(id)&&sim.State.Money==money-product.Price&&
        sim.ShopQuantity(id)==stock+product.Pack,"required paid fixture purchase failed: "+id);
}

void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
void PermanentFoot(Creature c)
{
    // Explicit starting fixture: the wound has healed with lasting disability.
    c.Interaction.FootIntegrity=20;
    c.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Bottom,PermanentLoss=80,Cause="旧底部伤残"});
}
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261031);sim.State.RoomObjects.Clear();sim.State.UnlimitedResources=false;
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=c.Health=100;c.Fear=c.Pain=0;c.Trust=70;
        c.TrueDisposition="gentle";c.Aggression=.08;c.Patience=.8;c.Courage=.2;
        c.DecisionIn=10000;c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.NextSocialAt=10000;c.Family.NextCareAt=10000;
        c.X=c.TargetX=n==0?0:n==1?4:-5;c.Z=c.TargetZ=n==2?-5:0;
    }
    var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];PermanentFoot(b);
    a.Family.Peers[b.Id]=new(){Affection=60,Familiarity=60};b.Family.Peers[a.Id]=new(){Affection=60,Familiarity=60};
    return sim;
}
(WorldSimulation Sim,Creature Giver,Creature Recipient,RoomObject Source) Delivery(bool water=false,bool arrange=true)
{
    var sim=Fixture();var giver=sim.State.Creatures[0];var recipient=sim.State.Creatures[1];
    recipient.Fullness=water?100:20;recipient.Interaction.Hydration=water?20:100;
    var source=new RoomObject{Id="assisted-source",Kind=water?RoomObjectKind.Water:RoomObjectKind.Meal,X=0,Z=.95,Portions=3};
    sim.State.RoomObjects.Add(source);
    if(arrange){Require(sim.FamilyCareReason(giver.Id,recipient.Id)==null,"合法成人照护被拒绝");sim.ArrangeCare(giver.Id,recipient.Id);
        Require(giver.Family.CareTargetId==recipient.Id,"实际安排没有建立任务");}
    return(sim,giver,recipient,source);
}
RoomObject WaitForParcel(WorldSimulation sim,Creature giver)
{
    for(int n=0;n<120;n++)
    {Tick(sim,.25);if(giver.Family.CareParcelId is { } id&&sim.State.RoomObjects.Find(o=>o.Id==id&&o.CarrierId==giver.Id) is { } parcel)return parcel;}
    throw new Exception("没有从真实来源取到运输份额："+giver.Family.LastEvent);
}
void FinishDelivery(WorldSimulation sim,Creature giver,Creature recipient,RoomObject parcel)
{
    bool pending=false,accepted=false,placed=false;double initialNeed=NaturalResources.IsWater(parcel.Kind)?recipient.Interaction.Hydration:recipient.Fullness;
    int exposures=recipient.Interaction.Digestion.Exposures;
    for(int n=0;n<600;n++)
    {
        Tick(sim,.05);
        if(parcel.CarrierId==null&&WorldSimulation.Distance(recipient.X,recipient.Z,parcel.X,parcel.Z)<=NaturalResources.Reach(recipient))placed=true;
        if(recipient.Interaction.GroundIntakeSourceId==parcel.Id&&recipient.Interaction.EatingRemaining>0)
        {pending|=recipient.Interaction.NaturalIntakePending;accepted|=!recipient.Interaction.NaturalIntakePending;}
        if(accepted&&recipient.Interaction.EatingRemaining==0)break;
    }
    Require(placed&&pending&&accepted&&recipient.Interaction.EatingRemaining==0,"没有真实放下→预留→入口→吞咽："+giver.Family.LastEvent);
    Require(recipient.Interaction.Digestion.Exposures==exposures+1&&parcel.Portions==0,"送来的实际一份未恰好结算一次");
    Require((NaturalResources.IsWater(parcel.Kind)?recipient.Interaction.Hydration:recipient.Fullness)>initialNeed,"实际入口没有补充相应需求");
}
int Portions(WorldSimulation sim,RoomObjectKind kind)=>sim.State.RoomObjects.Where(o=>o.Kind==kind&&!o.Stored).Sum(o=>o.Portions);
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void Reject(WorldSimulation sim,Creature giver,Creature recipient,string fragment)
{
    string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    string? reason=sim.FamilyCareReason(giver.Id,recipient.Id);
    Require(reason!=null&&reason.Contains(fragment),"拒绝原因不具体："+reason);
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==before,"只读原因查询改变了状态");
    sim.ArrangeCare(giver.Id,recipient.Id);Require(giver.Family.CareTargetId==null,"被拒绝的安排仍建立照护任务");
}

Check("成年永久脚伤对象实际收到一份主食，不重建脚底或被收养",()=>
{
    var (sim,a,b,source)=Delivery();a.Family.Peers[b.Id].CareSeconds=59;b.Family.Peers[a.Id].CareSeconds=59;
    double trust=b.Trust,foot=b.Interaction.FootIntegrity,loss=b.Interaction.Injuries.PermanentFootLoss,age=b.InnerAgeSeconds;
    string body=b.Body.Id;var role=b.Body.ReproductionRole;
    var parcel=WaitForParcel(sim,a);Require(source.Portions==2&&Portions(sim,RoomObjectKind.Meal)==3,"取份额破坏守恒");
    FinishDelivery(sim,a,b,parcel);
    Require(Portions(sim,RoomObjectKind.Meal)==2&&b.Interaction.FootIntegrity==foot&&b.Interaction.Injuries.PermanentFootLoss==loss,
        "送食重复消费或恢复了永久损伤");
    Require(a.Family.Peers[b.Id].CareSeconds>=64&&b.Family.CaregiverIds.Count==0&&!b.Family.LastEvent.Contains("养亲")&&
        b.ParentId==null&&b.Family.MotherId==null&&b.Family.FatherId==null,"成年照护错误形成收养或血缘");
    Require(b.Trust==trust&&b.Body.Id==body&&b.Body.ReproductionRole==role&&b.InnerAgeSeconds>age,"照护改写身份、角色或对人信任");
    SaveStore.Validate(sim.State);
});
Check("成年伤残对象收到实际饮水，运输途中不先增加水分",()=>
{
    var (sim,a,b,source)=Delivery(water:true);double water=b.Interaction.Hydration;
    var parcel=WaitForParcel(sim,a);Require(b.Interaction.Hydration<=water&&source.Portions==2,"刚取到水就隔空补水");
    FinishDelivery(sim,a,b,parcel);Require(Portions(sim,RoomObjectKind.Water)==2,"饮水份额重复结算");
});
Check("双方亲近者会自主照护，正常成年陌生对象不会被自动投喂",()=>
{
    var (sim,a,b,source)=Delivery(arrange:false);a.Family.NextCareAt=0;
    var parcel=WaitForParcel(sim,a);FinishDelivery(sim,a,b,parcel);
    foreach(bool healthy in new[]{false,true})
    {
        var (s,g,r,food)=Delivery(arrange:false);g.Family.NextCareAt=0;
        if(healthy){r.Interaction.FootIntegrity=100;r.Interaction.Injuries.Sites.Clear();}
        else{g.Family.Peers.Clear();r.Family.Peers.Clear();}
        Require(s.FamilyCareReason(g.Id,r.Id)!=null,"健康成人或陌生人获得协助喂食资格");
        Tick(s,4);Require(g.Family.CareTargetId==null&&g.Family.CareParcelId==null&&!s.State.RoomObjects.Any(o=>o.OwnerId==g.Id),"自动照护绕过身体/关系门槛");
    }
});
Check("恢复期健康脚底也可得到送食，恢复和步行能力不被照护重置",()=>
{
    var (sim,a,b,source)=Delivery(arrange:false);b.Interaction.Injuries.Sites.Clear();b.Interaction.FootIntegrity=100;
    b.Condition=PhysicalCondition.Recovering;b.Health=8;b.RecoveryRemaining=55;b.Stabilized=true;
    Require(sim.FamilyCareReason(a.Id,b.Id)==null,"恢复期对象被正常脚底排除");sim.ArrangeCare(a.Id,b.Id);
    var parcel=WaitForParcel(sim,a);FinishDelivery(sim,a,b,parcel);
    Require(b.Condition==PhysicalCondition.Recovering&&b.RecoveryRemaining>0&&b.RecoveryRemaining<55&&!WorldSimulation.CanSelfMove(b),"送食直接完成恢复或赋予步行");
});
Check("地面包裹可实际接下食水，箱子悬挂和不稳支撑不能冒充可接食",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var (sim,a,b,source)=Delivery(water,arrange:false);
        BuyTool(sim,"wrap");
        Require(sim.ExecuteInteraction(b.Id,new(InteractionAction.Wrap)).Success,"实际包裹入口失败");
        Require(sim.FamilyCareReason(a.Id,b.Id)==null,"地面包裹被当作密闭限制");sim.ArrangeCare(a.Id,b.Id);
        var parcel=WaitForParcel(sim,a);FinishDelivery(sim,a,b,parcel);
        Require(b.Interaction.Restraint==RestraintKind.Wrap,"送食暗中解除包裹");
    }
    foreach(var restraint in new[]{RestraintKind.Box,RestraintKind.Hanging,RestraintKind.Frame})
    {var (sim,a,b,source)=Delivery(arrange:false);b.Interaction.Restraint=restraint;Reject(sim,a,b,"限制");}
});
Check("暂停面板查询允许实际候选但无副作用，命令仍服从暂停",()=>
{
    var (sim,a,b,source)=Delivery(arrange:false);sim.State.Settings.Pause=true;
    string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    Require(sim.FamilyCareReason(a.Id,b.Id)==null,"暂停模态无法读取实际资格");
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==before,"资格查询修改状态");
    sim.ArrangeCare(a.Id,b.Id);Require(a.Family.CareTargetId==null&&source.Portions==3,"暂停命令仍接单");
    sim.State.Settings.Pause=false;sim.ArrangeCare(a.Id,b.Id);Require(a.Family.CareTargetId==b.Id,"恢复普通时间仍不能安排");
});
Check("本人需要和对象支撑各自给明确原因，不靠UI猜测",()=>
{
    var edits=new (Action<Creature,Creature> Apply,string Reason)[]
    {
        ((a,b)=>a.Fullness=20,"饿"),((a,b)=>a.Interaction.Hydration=20,"渴"),
        ((a,b)=>a.Interaction.RestRemaining=5,"休息"),((a,b)=>a.Interaction.BeginIntake(IntakeKind.Meal),"咽下"),
        ((a,b)=>a.Interaction.ActiveCue=LearnedCue.Wait,"指令"),
        ((a,b)=>b.Interaction.Carried=true,"托"),((a,b)=>b.Interaction.Falling=true,"下落"),
        ((a,b)=>b.Interaction.Height=.8,"高处")
    };
    foreach(var edit in edits){var (sim,a,b,source)=Delivery(arrange:false);edit.Apply(a,b);Reject(sim,a,b,edit.Reason);}
    {
        var (sim,a,b,source)=Delivery(arrange:false);
        // Use both real actions. The product owns Lowering and keeps the hand
        // supporting the creature until the lowering process has completed.
        BuyTool(sim,"hanger");
        Require(sim.ExecuteInteraction(b.Id,new(InteractionAction.Hang)).Success,"照护悬挂前置入口失败");
        Require(sim.ExecuteInteraction(b.Id,new(InteractionAction.Lower)).Success,"照护放下前置入口失败");
        Require(b.Interaction.Lowering&&b.Interaction.Carried&&!b.Interaction.Falling&&
            b.Interaction.Restraint==RestraintKind.None&&b.Interaction.Height>0,"没有实际进入托住放下过程");
        Reject(sim,a,b,"放下");
    }
});
Check("已有家人也尊重双方恐惧怨恨，单向喜欢不足以照护陌生成人",()=>
{
    foreach(bool reverse in new[]{false,true})foreach(bool resentment in new[]{false,true})
    {
        var (sim,a,b,source)=Delivery(arrange:false);b.Family.MotherId=a.Id;
        var bond=reverse?b.Family.Peers[a.Id]:a.Family.Peers[b.Id];if(resentment)bond.Resentment=35;else bond.Fear=40;
        Reject(sim,a,b,"害怕或怨恨");
    }
    var (s,g,r,food)=Delivery(arrange:false);r.Family.Peers[g.Id].Affection=0;Reject(s,g,r,"不够亲近");
});
Check("脚伤亲代只在真实近旁保暖，不搬运或隔空补食",()=>
{
    var sim=Fixture();var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];PermanentFoot(a);
    b.IsYoung=true;b.Body.AgeSeconds=b.InnerAgeSeconds=0;b.Family.MotherId=a.Id;b.X=b.TargetX=1.1;
    b.Fullness=20;b.Family.Warmth=20;a.Family.NextCareAt=0;
    var source=new RoomObject{Id="near-parent-food",Kind=RoomObjectKind.Meal,X=-.6,Z=-.5,Portions=3};sim.State.RoomObjects.Add(source);
    Require(sim.FamilyCareReason(a.Id,b.Id)==null,"近旁脚伤亲代不能陪护");sim.ArrangeCare(a.Id,b.Id);
    double x=a.X,z=a.Z,warm=b.Family.Warmth,full=b.Fullness;Tick(sim,5);
    Require(a.X==x&&a.Z==z&&b.Family.Warmth>warm&&a.Family.Peers[b.Id].CareSeconds>0,"近旁保暖未发生或亲代被假移动");
    Require(source.Portions==3&&a.Family.CareParcelId==null&&b.Fullness<=full&&a.Interaction.FootIntegrity==20,
        "保暖隔空搬运、补食或修好了脚底");
});
Check("脚伤亲代与幼体分开时不远程保暖，读档也不创造接触",()=>
{
    var sim=Fixture();var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];PermanentFoot(a);
    b.IsYoung=true;b.Body.AgeSeconds=b.InnerAgeSeconds=0;b.Family.MotherId=a.Id;b.Family.Warmth=20;a.Family.NextCareAt=0;
    Reject(sim,a,b,"走不开");double warm=b.Family.Warmth;var copy=Reload(sim);Tick(copy,5);
    var g=copy.State.Creatures[0];var child=copy.State.Creatures[1];
    Require(child.Family.Warmth<warm&&g.Family.CareTargetId==null&&g.Family.Peers[child.Id].CareSeconds==0,"分开仍获得保暖或照护关系");
});
Check("缺少或拿在玩家手里的物资不会生成食物，并说明已停止递送",()=>
{
    foreach(string unavailable in new[]{"missing","held","stored"})
    {
        var (sim,a,b,source)=Delivery(arrange:false);
        if(unavailable=="missing")sim.State.RoomObjects.Remove(source);
        else if(unavailable=="held")Require(sim.HoldSupply(source.Id),"不能拿起夹具物资");
        else source.Stored=true;
        double full=b.Fullness;sim.ArrangeCare(a.Id,b.Id);Tick(sim,1);
        Require(a.Family.CareTargetId==null&&a.Family.CareParcelId==null&&b.Fullness<=full&&source.Portions==3,"无可用物资仍送食或重复取份额");
        Require(a.Family.LastEvent.Contains("没有可取用")&&b.Family.LastEvent.Contains("中断"),"任务结束仍无双方可读原因");
    }
});
Check("搬运者新受伤或自身饥渴立即释放真实份额，不重复归还",()=>
{
    foreach(bool injury in new[]{false,true})
    {
        var (sim,a,b,source)=Delivery();var parcel=WaitForParcel(sim,a);double full=b.Fullness;
        if(injury)sim.Hurt(a,80,"搬运途中底部受伤",null,BodyPart.Bottom);else a.Fullness=60;
        sim.AdvanceInteractionPresentation(.05);
        Require(a.Family.CareTargetId==null&&a.Family.CareParcelId==null&&parcel.CarrierId==null&&parcel.Portions==1&&Portions(sim,RoomObjectKind.Meal)==3,
            "中断未释放份额或复制/吞掉份额");
        Require(b.Fullness==full,"中断时隔空送达");
        if(!injury)Require(a.Family.LastEvent.Contains("饿")&&b.Family.LastEvent.Contains("中断"),"未留下实际饥饿取消原因");
        sim.AdvanceInteractionPresentation(.05);Require(Portions(sim,RoomObjectKind.Meal)==3,"重复清理再次退款");
        SaveStore.Validate(sim.State);
    }
});
Check("收件人濒危死亡或冷藏中断搬运，不直接灌食",()=>
{
    foreach(string change in new[]{"critical","dead","cold"})
    {
        var (sim,a,b,source)=Delivery(water:true);var parcel=WaitForParcel(sim,a);double water=b.Interaction.Hydration;
        if(change=="cold"){BuyTool(sim,"freezer");sim.SetColdStorage(b.Id,true);Require(b.Family.ColdStorage.Stored,"未实际进入冷藏");}
        else sim.Hurt(b,change=="dead"?200:100,"收件人突发重伤",null);
        sim.AdvanceInteractionPresentation(.05);
        Require(a.Family.CareTargetId==null&&a.Family.CareParcelId==null&&parcel.CarrierId==null&&Portions(sim,RoomObjectKind.Water)==3,
            "失能收件人仍占用搬运或丢失份额");
        Require(b.Interaction.Hydration==water&&b.Interaction.EatingRemaining==0,"濒危/死亡/冷藏被强行灌食");
        if(change=="dead")Require(b.Interaction.Injuries.DeathCause=="收件人突发重伤","取消照护覆盖死因");
        SaveStore.Validate(sim.State);
    }
});
Check("实际运输份额存读续接，不重取物资或成人收养",()=>
{
    var (sim,a,b,source)=Delivery();var parcel=WaitForParcel(sim,a);string id=parcel.Id;
    sim.ArrangeCare(a.Id,b.Id);Require(a.Family.CareParcelId==id&&source.Portions==2,"重复安排丢掉已取份额");
    var copy=Reload(sim);var giver=copy.State.Creatures[0];var recipient=copy.State.Creatures[1];
    var transported=copy.State.RoomObjects.Single(o=>o.Id==id);
    Require(giver.Family.CareTargetId==recipient.Id&&giver.Family.CareParcelId==id&&transported.CarrierId==giver.Id&&Portions(copy,RoomObjectKind.Meal)==3,
        "读档丢掉任务或复制运输份额");
    FinishDelivery(copy,giver,recipient,transported);Require(recipient.Family.CaregiverIds.Count==0,"读档让成年照护变成收养");
});
Check("运输暂停与近看冻结实际旅程，恢复普通时间再送达",()=>
{
    foreach(bool closeUp in new[]{false,true})
    {
        var (sim,a,b,source)=Delivery();var parcel=WaitForParcel(sim,a);double x=a.X,z=a.Z,time=sim.State.WorldSeconds,age=a.Body.AgeSeconds;
        if(closeUp)Require(sim.EnterCloseUp(),"未进入近看");else sim.State.Settings.Pause=true;
        Tick(sim,5);
        Require(a.X==x&&a.Z==z&&sim.State.WorldSeconds==time&&a.Body.AgeSeconds==age&&a.Family.CareParcelId==parcel.Id&&source.Portions==2,
            "冻结期间仍搬运、结算时间或重取份额");
        if(closeUp)sim.ExitCloseUp();else sim.State.Settings.Pause=false;
        FinishDelivery(sim,a,b,parcel);
    }
});
Check("非法运输存档被拒绝，实际原档仍可保存",()=>
{
    var (sim,a,b,source)=Delivery();var parcel=WaitForParcel(sim,a);
    foreach(bool wrongOwner in new[]{false,true})
    {
        var copy=Reload(sim);var transported=copy.State.RoomObjects.Single(o=>o.Id==parcel.Id);
        if(wrongOwner)transported.CarrierId=b.Id;else transported.Portions=2;
        bool rejected=false;try{SaveStore.Validate(copy.State);}catch(InvalidDataException){rejected=true;}
        Require(rejected,"非法运输归属或份额被保存");
    }
    SaveStore.Validate(sim.State);
});

Console.WriteLine($"Family assisted care: {failures} failure(s)");
return failures==0?0:1;

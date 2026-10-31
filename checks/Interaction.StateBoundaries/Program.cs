using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void BuyTool(WorldSimulation sim,string id)
{
    var product=CourtyardShop.Products.Single(p=>p.Id==id);
    int money=sim.State.Money,stock=sim.ShopQuantity(id);
    Require(sim.BuyShopProduct(id)&&sim.State.Money==money-product.Price&&
        sim.ShopQuantity(id)==stock+product.Pack,"required paid fixture purchase failed: "+id);
}

void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action check)
{try{check();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
WorldSimulation Create()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();sim.State.UnlimitedResources=false;
    return sim;
}
string Snapshot(WorldSimulation sim,Creature c)=>JsonSerializer.Serialize(new
{Creature=c,sim.State.Food,sim.State.Medicine,sim.State.RandomState},SaveStore.JsonOptions);
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
void RejectLegacyActions(WorldSimulation sim,Creature c)
{
    string before=Snapshot(sim,c);
    foreach(var action in Enum.GetValues<ContactAction>())
        Require(!sim.Contact(c.Id,new(action)).Success,"旧Contact仍接受"+action);
    Require(!sim.Feed(c.Id),"旧Feed仍接受玩家喂食");
    Require(!sim.Feed(c.Id,staff:true),"旧Feed仍接受员工喂食");
    Require(Snapshot(sim,c)==before,"被拒动作仍改变身体、关系、学习、库存或随机数");
}
void PrepareThrow(WorldSimulation sim,Creature c)
{
    Require(sim.BeginInteractionGesture(c.Id,new(InteractionAction.Lift,Height:.8,X:c.X,Z:c.Z)).Success,"夹具未托起");
    var input=new InteractionInput(InteractionAction.Throw,Force:.6,Duration:.1,Motion:.1,DirectionX:1,X:c.X,Z:c.Z);
    Require(sim.BeginInteractionGesture(c.Id,input).Success,"夹具未进入投掷");
    Require(sim.ContinueInteractionGesture(input,true).Success,"夹具未积累投掷方向与动作");
    Require(c.Interaction.Carried&&!c.Interaction.Falling,"投掷在松手前执行了");
}
void RequireSupported(Creature c)
{
    var i=c.Interaction;
    Require(i.Carried&&!i.Falling&&i.VerticalVelocity==0&&i.VelocityX==0&&i.VelocityZ==0,
        "被禁止的结束动作施加冲量或丢失原有手托支撑");
}
void StartIntervention(WorldSimulation sim,Creature mother,bool pendingGreeting=false)
{
    var father=sim.State.Creatures[1];
    sim.CollectBreedingMaterial(father.Id);
    Require(sim.State.BreedingMaterials.Count==1,"夹具没有取得有效材料");
    if(pendingGreeting)
    {
        mother.Interaction.Resting=true;mother.Interaction.RestRemaining=8;
        Require(sim.ExecuteInteraction(mother.Id,new(InteractionAction.Greet)).Success&&mother.Interaction.PendingConversation.HasValue,
            "夹具没有产生休息中的待回应");
    }
    sim.RequestForcedPregnancy(mother.Id,father.Id,BirthMethod.Live);
    Require(FamilyRules.UnderIntervention(mother)&&mother.Family.Pregnancy.InterventionRemaining>0,"夹具未开始强制准备");
    Require(sim.State.BreedingMaterials.Count==0,"开始时没有结算材料");
}

Check("冷藏拒绝旧接触和喂食入口且读档后仍拒绝",()=>
{
    var sim=Create();var c=sim.Selected!;c.Health=60;c.Pain=15;
    BuyTool(sim,"freezer");sim.SetColdStorage(c.Id,true);Require(c.Family.ColdStorage.Stored,"夹具未进入冷藏");
    RejectLegacyActions(sim,c);
    var copy=Reload(sim);Require(copy.Selected!.Family.ColdStorage.Stored,"读档丢失冷藏状态");
    RejectLegacyActions(copy,copy.Selected!);
});
Check("暂停拒绝旧接触和喂食入口且不消耗资源",()=>
{
    var sim=Create();var c=sim.Selected!;c.Health=60;c.Pain=15;sim.State.Settings.Pause=true;
    RejectLegacyActions(sim,c);
});
Check("已积累投掷在暂停后松手只取消不发射",()=>
{
    var sim=Create();var c=sim.Selected!;PrepareThrow(sim,c);
    sim.State.Settings.Pause=true;double health=c.Health;int food=sim.State.Food,medicine=sim.State.Medicine;
    sim.EndInteractionGesture();RequireSupported(c);
    Require(c.Health==health&&sim.State.Food==food&&sim.State.Medicine==medicine,"暂停松手改变身体或库存");
    Require(!sim.HasInteractionGesture,"取消后仍保留投掷手势");
});
Check("已积累投掷在濒危后松手保留托举支撑",()=>
{
    var sim=Create();var c=sim.Selected!;PrepareThrow(sim,c);
    c.Health=1;sim.Hurt(c,2,"突发身体恶化",null);
    Require(c.Condition==PhysicalCondition.Critical,"夹具未进入濒危");
    double budget=c.CriticalRemaining;sim.EndInteractionGesture();RequireSupported(c);
    Require(c.Condition==PhysicalCondition.Critical&&c.CriticalRemaining==budget,"松手改变了濒危窗口");
});
Check("正常投掷仍在松手结算而不是无条件吞掉",()=>
{
    var sim=Create();var c=sim.Selected!;PrepareThrow(sim,c);sim.EndInteractionGesture();
    Require(!c.Interaction.Carried&&c.Interaction.Falling&&c.Interaction.VerticalVelocity>0&&c.Interaction.VelocityX>0,
        "正常动作也被边界保护屏蔽");
});
Check("濒危允许托举放稳和实际护理",()=>
{
    var sim=Create();var c=sim.Selected!;c.Health=1;sim.Hurt(c,2,"重伤待救",null);
    double x=c.X,z=c.Z;
    Require(sim.BeginInteractionGesture(c.Id,new(InteractionAction.Lift,Height:.8,X:x,Z:z)).Success,"濒危无法托举");
    sim.EndInteractionGesture(true);RequireSupported(c);
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.PutDown,X:x,Z:z)).Success,"濒危无法放稳");
    Require(!c.Interaction.Carried&&!c.Interaction.Falling&&c.Interaction.Height==0,"放稳后支撑状态错误");
    int medicine=sim.State.Medicine;
    var care=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.1);
    Require(sim.BeginInteractionGesture(c.Id,care).Success,"濒危无法护理");
    for(int n=0;n<20;n++)sim.ContinueInteractionGesture(care,true);
    sim.EndInteractionGesture();
    Require(c.Condition==PhysicalCondition.Recovering&&sim.State.Medicine==medicine-1,"实际护理没有稳定伤势或重复扣药");
    Require(Reload(sim).Selected!.Condition==PhysicalCondition.Recovering,"稳定后的合法状态不能续接");
});
Check("强制准备挡住普通新旧动作且近看不能绕过",()=>
{
    var sim=Create();var c=sim.Selected!;StartIntervention(sim,c);sim.EnterCloseUp();
    string before=Snapshot(sim,c);
    foreach(var action in new[]{InteractionAction.Greet,InteractionAction.Sing,InteractionAction.Stretch,InteractionAction.TeachWait,InteractionAction.Lift})
        Require(!sim.ExecuteInteraction(c.Id,new(action)).Success,"准备中接受离散动作"+action);
    foreach(var action in new[]{InteractionAction.Stroke,InteractionAction.FeedMeal,InteractionAction.Drink})
        Require(!sim.BeginInteractionGesture(c.Id,new(action)).Success,"准备中接受连续动作"+action);
    foreach(var action in Enum.GetValues<ContactAction>().Where(a=>a!=ContactAction.Treat))
        Require(!sim.Contact(c.Id,new(action)).Success,"准备中接受旧接触"+action);
    Require(!sim.Feed(c.Id)&&!sim.Feed(c.Id,staff:true),"准备中接受旧喂食");
    Require(Snapshot(sim,c)==before&&sim.State.BreedingMaterials.Count==0,"被拒动作改变状态或返还材料");
});
Check("强制准备清旧待回应且读档不会重扣材料或关系",()=>
{
    var sim=Create();var c=sim.Selected!;StartIntervention(sim,c,pendingGreeting:true);
    Require(!c.Interaction.PendingConversation.HasValue&&c.Interaction.PendingPraise==null,"强制准备保留了此前待回应或待赞扬");
    double trust=c.Trust,attachment=c.Attachment,remaining=c.Family.Pregnancy.InterventionRemaining;
    sim.EnterCloseUp();
    for(int n=0;n<30;n++){sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);}
    Require(c.Trust==trust&&c.Attachment==attachment,"旧待回应在准备期间增长关系");
    Require(c.Family.Pregnancy.InterventionRemaining==remaining,"近看偷走干预计时");
    var copy=Reload(sim);var loaded=copy.Selected!;
    Require(FamilyRules.UnderIntervention(loaded)&&loaded.Family.Pregnancy.InterventionRemaining==remaining,
        "读档丢失正在进行的准备或重置计时");
    Require(copy.State.BreedingMaterials.Count==0&&loaded.Trust==trust&&loaded.Attachment==attachment,
        "读档重扣关系或重发材料");
});
Check("必要护理明确中断强制准备并保留已发生代价",()=>
{
    foreach(bool legacy in new[]{false,true})
    {
        var sim=Create();var c=sim.Selected!;c.Health=60;c.Pain=15;StartIntervention(sim,c);
        int medicine=sim.State.Medicine;double health=c.Health;
        if(legacy)Require(sim.Contact(c.Id,new(ContactAction.Treat)).Success,"旧护理不能中断准备");
        else
        {
            var care=new InteractionInput(InteractionAction.Care,BodyPart.Side,Duration:.1);
            Require(sim.BeginInteractionGesture(c.Id,care).Success,"持续护理不能中断准备");
            Require(!FamilyRules.UnderIntervention(c),"护理开始后准备仍继续占用身体");
            for(int n=0;n<20;n++)sim.ContinueInteractionGesture(care,true);
            sim.EndInteractionGesture();
        }
        Require(!FamilyRules.UnderIntervention(c)&&c.Family.Pregnancy.InterventionRemaining==0,"护理未清干预计时");
        Require(c.Health>health&&sim.State.Medicine==medicine-1,"护理未产生效果或扣药次数错误");
        Require(sim.State.BreedingMaterials.Count==0&&c.Family.NextForcedAttemptAt>sim.State.WorldSeconds,
            "中断退回材料或取消重试等待");
        Require(!FamilyRules.UnderIntervention(Reload(sim).Selected!),"读档重启了已经中断的准备");
    }
});
Check("收起原坐标旁的宝物不会唤起冷藏个体的反应和怨恨",()=>
{
    var sim=Create();var c=sim.Selected!;
    BuyTool(sim,"treasure");
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.GiveTreasure)).Success,"夹具未放置宝物");
    var treasure=sim.State.RoomObjects.Single(o=>o.Kind==RoomObjectKind.Treasure&&o.OwnerId==c.Id);
    BuyTool(sim,"freezer");sim.SetColdStorage(c.Id,true);Require(c.Family.ColdStorage.Stored,"夹具未冷藏");
    string before=Snapshot(sim,c);
    Require(sim.PutAwayRoomObject(treasure.Id)&&treasure.Stored,"地上的宝物没有正常收起");
    Require(Snapshot(sim,c)==before,"冷藏者仍看到收物、增长怨恨/记忆或回应");
    var copy=Reload(sim);var loaded=copy.Selected!;
    Require(loaded.Family.ColdStorage.Stored&&loaded.Resentment==c.Resentment&&loaded.ReactionSerial==c.ReactionSerial,
        "读档伪造冷藏期间的目击反应");
});
Environment.ExitCode=failures==0?0:1;

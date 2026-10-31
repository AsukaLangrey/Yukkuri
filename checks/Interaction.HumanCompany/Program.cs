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

void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
WorldSimulation Create()
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    // Isolate the hand relationship from peers, food searches and toy invitations.
    sim.State.Creatures.RemoveAll(x=>x.Id!=c.Id);sim.State.RoomObjects.Clear();
    sim.State.UnlimitedResources=false;c.X=0;c.Z=0;c.TargetX=0;c.TargetZ=0;
    c.Fullness=90;c.Energy=90;c.Interaction.Hydration=90;c.Trust=75;c.Attachment=55;
    c.Pain=0;c.Fear=0;c.ReactionRemaining=0;c.Interaction.NextAutonomyAt=10000;
    c.Interaction.HumanBond.Familiarity=15;c.Interaction.HumanBond.Safety=15;
    c.Interaction.HumanBond.LastHarmAt=-100;
    return sim;
}
void Step(WorldSimulation sim,int frames,double handX=.6,double handZ=0,bool showHand=true)
{
    for(int n=0;n<frames;n++)
    {
        sim.SetInteractionHand(handX,handZ,showHand);
        sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);
    }
}
void Start(WorldSimulation sim,double handX=.6)
{
    Step(sim,5,handX);Require(sim.Selected!.Interaction.HumanCompany.Until>0,"夹具没有开始主动陪伴请求");
}
double Memory(Creature c)=>c.Interaction.Memories.GetValueOrDefault("human-company");
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
string Company(Creature c)=>JsonSerializer.Serialize(c.Interaction.HumanCompany,SaveStore.JsonOptions);
void RejectedSave(WorldSimulation sim)
{
    bool rejected=false;try{SaveStore.Validate(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"非法主动陪伴记录通过了整档验证");
}

Check("普通时间实际靠近后才积累陪伴，满六秒一段只记一次",()=>
{
    var sim=Create();var c=sim.Selected!;Start(sim,3);
    double x=c.X;Require(c.Interaction.HumanCompany.ContactSeconds==0&&Memory(c)==0,"隔空记了陪伴");
    Step(sim,10,3);Require(c.X>x,"提出请求后没有实际靠近手");
    Require(c.Interaction.HumanCompany.ContactSeconds==0&&Memory(c)==0,"尚未靠近就获得持续陪伴记录");
    Step(sim,130,3);
    Require(Math.Abs(c.X-3)<=1.16&&c.Interaction.HumanCompany.ContactSeconds==6,"没有在真实近处完成陪伴");
    Require(c.Interaction.HumanCompany.Remembered&&Memory(c)==1,"六秒完成没有只记一次");
    Step(sim,20,3);Require(Memory(c)==1,"同一次陪伴按帧重复记忆");
});
Check("不到六秒没有完成记忆，真实近处相处才改善关系",()=>
{
    var sim=Create();var c=sim.Selected!;double trust=c.Trust;Start(sim);Step(sim,40);
    Require(c.Interaction.HumanCompany.ContactSeconds>0&&c.Interaction.HumanCompany.ContactSeconds<6,"夹具不处于未满六秒阶段");
    Require(Memory(c)==0&&!c.Interaction.HumanCompany.Remembered,"过早记完成");
    Require(c.Trust>trust,"真实陪伴没有关系反馈");Step(sim,25);Require(Memory(c)==1,"累计足够相处仍未记忆");
});
Check("信任、依恋、安全、熟悉度及近期伤害各自限制主动请求",()=>
{
    Action<Creature>[] blocks=[c=>c.Trust=45,c=>c.Attachment=20,c=>c.Interaction.HumanBond.Safety=0,
        c=>c.Interaction.HumanBond.Familiarity=0,c=>c.Interaction.HumanBond.LastHarmAt=0,c=>c.Resentment=50];
    foreach(var block in blocks)
    {
        var sim=Create();var c=sim.Selected!;block(c);double trust=c.Trust;Step(sim,30);
        Require(c.Interaction.HumanCompany.Until==0&&Memory(c)==0,"亲密条件不足仍主动亲近");
        Require(c.Trust<=trust,"被拒请求仍自动涨信任");
    }
});
Check("疼痛、饥渴、疲劳和恐惧优先，不能用陪伴掩盖身体需要",()=>
{
    Action<Creature>[] blocks=[c=>c.Pain=45,c=>c.Fullness=30,c=>c.Interaction.Hydration=30,c=>c.Energy=30,c=>c.Fear=75];
    foreach(var block in blocks)
    {
        var sim=Create();var c=sim.Selected!;block(c);Step(sim,30);
        Require(c.Interaction.HumanCompany.Until==0&&Memory(c)==0,"身体或生活需要被主动陪伴抢占");
    }
});
Check("不安时可以主动找安慰并留下真实相处记忆",()=>
{
    var sim=Create();var c=sim.Selected!;c.Fear=30;c.Health=80;Start(sim);
    Require(c.Interaction.HumanCompany.SeekingComfort,"不安请求被当作普通玩耍");
    double fear=c.Fear;Step(sim,65);
    Require(Memory(c)==1&&c.Fear<fear,"真实陪伴没有形成安慰经历");
    Require(c.Interaction.Injuries.Sites.Count==0,"安慰凭空产生伤势记录");
});
Check("暂停和近看不会增长陪伴时间、记忆或关系",()=>
{
    foreach(bool closeUp in new[]{false,true})
    {
        var sim=Create();var c=sim.Selected!;Start(sim);Step(sim,10);
        if(closeUp)Require(sim.EnterCloseUp(),"无法进入近看");else sim.State.Settings.Pause=true;
        string state=Company(c);double trust=c.Trust,attachment=c.Attachment,now=sim.State.WorldSeconds;Step(sim,80);
        Require(Company(c)==state&&c.Trust==trust&&c.Attachment==attachment&&sim.State.WorldSeconds==now&&Memory(c)==0,
            "暂停或近看偷偷完成陪伴");
        if(closeUp)sim.ExitCloseUp();else sim.State.Settings.Pause=false;
        Step(sim,60);Require(Memory(c)==1,"普通时间恢复后不能继续同次陪伴");
    }
});
Check("冷藏立即结束主动陪伴，冷藏内无隔空关系增长",()=>
{
    var sim=Create();var c=sim.Selected!;Start(sim);Step(sim,10);BuyTool(sim,"freezer");sim.SetColdStorage(c.Id,true);
    Require(c.Family.ColdStorage.Stored&&c.Interaction.HumanCompany.Until==0,"冷藏保留陪伴请求");
    double trust=c.Trust,attachment=c.Attachment;Step(sim,80);
    Require(c.Interaction.HumanCompany.ContactSeconds==0&&Memory(c)==0&&c.Trust==trust&&c.Attachment==attachment,
        "冷藏期间继续亲近手");Reload(sim);
});
Check("收手立即停止收益，旧位置宽限和换区不能补齐相处",()=>
{
    foreach(bool otherRegion in new[]{false,true})
    {
        var sim=Create();var c=sim.Selected!;Start(sim);Step(sim,5);
        if(otherRegion)
        {
            sim.State.Regions.Add(new(){Id="elsewhere",Name="另一小院",Kind=RegionKind.Sanctuary});
            Require(sim.Travel("elsewhere"),"无法换区");
        }
        sim.SetInteractionHand(.6,0,false);
        double trustBefore=c.Trust;Step(sim,3,showHand:false);
        Require(c.Interaction.HumanCompany.Until==0&&c.Interaction.HumanCompany.ContactSeconds==0&&Memory(c)==0,
            "实际收手或跨区后仍把旧位置算为陪伴");
        Require(c.Trust==trustBefore,"收手后仍获得一帧关系收益");
        if(!otherRegion)Require(sim.State.InteractionHandPresent&&sim.State.WorldSeconds<sim.State.InteractionHandUntil,
            "夹具没有保留尚未过期的旧位置记忆，无法检验它与真实手分离");
        double trust=c.Trust;Step(sim,20,showHand:false);Require(c.Trust==trust,"手离开后仍增长信任");
    }
});
Check("未完成时离开接触半径须重积连续六秒",()=>
{
    var sim=Create();var c=sim.Selected!;Start(sim);Step(sim,40);
    Require(c.Interaction.HumanCompany.ContactSeconds>3&&Memory(c)==0,"离开前进度夹具错误");
    Step(sim,3,3);Require(c.Interaction.HumanCompany.Until>0&&c.Interaction.HumanCompany.ContactSeconds==0,
        "手仍可见但不在近处时没有重置未完成的连续相处");
    double closeX=c.X+.6;Step(sim,30,closeX);
    Require(c.Interaction.HumanCompany.ContactSeconds>0&&c.Interaction.HumanCompany.ContactSeconds<6&&Memory(c)==0,
        "重新靠近借用了离开前的累计时间");
    Step(sim,35,closeX);Require(Memory(c)==1,"重新连续相处足够时间仍未形成记忆");
    Step(sim,3,3);Step(sim,10,c.X+.6);Require(Memory(c)==1,"已经完成的相处因短暂离开又重复记忆");
});
Check("永久脚底失能可在手边相处，但不能靠陪伴恢复走路",()=>
{
    var sim=Create();var c=sim.Selected!;c.Interaction.FootIntegrity=20;
    c.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Bottom,Damage=0,PermanentLoss=80,Cause="既往底部伤残"});
    double x=c.X,z=c.Z;Start(sim,3);Step(sim,20,3);
    Require(c.X==x&&c.Z==z&&c.Interaction.HumanCompany.ContactSeconds==0,"不能走的个体隔空靠近了手");
    Step(sim,70,.6);
    Require(Memory(c)==1,"手靠近后失能个体仍无法获得陪伴");
    Require(c.Interaction.FootIntegrity==20&&c.Interaction.Injuries.At(BodyPart.Bottom)!.PermanentLoss==80&&!WorldSimulation.CanSelfMove(c),
        "陪伴恢复了永久脚底能力");Reload(sim);
});
Check("真实高伤害立即打断请求，保留伤势与救治优先",()=>
{
    var sim=Create();var c=sim.Selected!;Start(sim);Step(sim,20);
    sim.Hurt(c,55,"身体遭到强烈撞击","player",BodyPart.Head);
    Require(c.Health==45&&c.Pain>25&&c.Interaction.Injuries.At(BodyPart.Head)!.Damage==55,"夹具没有产生真实高伤害");
    Require(c.Interaction.HumanCompany.Until==0&&c.Interaction.HumanCompany.ContactSeconds==0,"伤害后仍保留待完成相处");
    double trust=c.Trust;Step(sim,50);Require(Memory(c)==0&&c.Trust<=trust,"受伤后旧陪伴继续发奖励");
});
Check("同档续接保留真实累计时间且完成后重读不重复记忆",()=>
{
    var original=Create();Start(original);Step(original,25);var c=original.Selected!;
    double elapsed=c.Interaction.HumanCompany.ContactSeconds;Require(elapsed>0&&elapsed<6&&Memory(c)==0,"读档夹具阶段错误");
    var copy=Reload(original);Require(copy.Selected!.Interaction.HumanCompany.ContactSeconds==elapsed,"读档丢失陪伴进度");
    Step(original,40);Step(copy,40);
    Require(Memory(original.Selected!)==1&&Memory(copy.Selected!)==1,"续接未正确只结算一次");
    Require(Company(original.Selected!)==Company(copy.Selected!),"同一存档的同输入续接产生不同陪伴状态");
    var finished=Reload(copy);Step(finished,20);Require(Memory(finished.Selected!)==1,"完成后读档再次结算同段记忆");
});
Check("读档不会把保存的手位置当成新的实际输入",()=>
{
    var sim=Create();Start(sim);Step(sim,40);var copy=Reload(sim);var c=copy.Selected!;double trust=c.Trust;
    // Deliberately supply no SetInteractionHand call after constructing the loaded simulation.
    copy.Advance(.5);
    Require(c.Interaction.HumanCompany.Until==0&&Memory(c)==0&&c.Trust==trust,"存档中的旧手位置继续获得陪伴收益");
});
Check("非法完成标志、超现实接触时间和失效残留均拒绝整档保存",()=>
{
    Action<HumanCompanyState>[] corruptions=[s=>s.Remembered=true,s=>s.ContactSeconds=6,
        s=>s.Until=0,s=>s.StartedAt=double.NaN,s=>s.Until+=5,s=>{s.StartedAt+=5;s.Until+=5;}];
    foreach(var corrupt in corruptions)
    {var sim=Create();Start(sim);SaveStore.Validate(sim.State);corrupt(sim.Selected!.Interaction.HumanCompany);RejectedSave(sim);}
});
Check("冷藏濒危死亡与强制准备整档不能夹带已清理的活动陪伴",()=>
{
    foreach(string transition in new[]{"cold","critical","dead","intervention"})
    {
        var sim=Create();var c=sim.Selected!;Start(sim);Step(sim,10);
        var old=JsonSerializer.Deserialize<HumanCompanyState>(Company(c),SaveStore.JsonOptions)!;
        switch(transition)
        {
            case "cold":BuyTool(sim,"freezer");sim.SetColdStorage(c.Id,true);Require(c.Family.ColdStorage.Stored,"冷藏夹具失败");break;
            case "critical":
            case "dead":
                sim.Hurt(c,100,"严重外伤","player",BodyPart.Head);
                Require(c.Condition==PhysicalCondition.Critical,"濒危夹具失败");
                if(transition=="dead"){sim.Hurt(c,2,"伤势继续恶化",null);Require(c.Condition==PhysicalCondition.Dead,"死亡夹具失败");}
                break;
            case "intervention":
                var father=WorldSimulation.CreateInteractionPlaytest().State.Creatures[1];sim.State.Creatures.Add(father);
                sim.CollectBreedingMaterial(father.Id);Require(sim.State.BreedingMaterials.Count==1,"材料夹具失败");
                sim.RequestForcedPregnancy(c.Id,father.Id,BirthMethod.Live);
                Require(FamilyRules.UnderIntervention(c),"强制准备夹具失败");break;
        }
        Require(c.Interaction.HumanCompany.Until==0,"真实高优先级流程未清理陪伴");
        SaveStore.Validate(sim.State); // Establish that the real transition itself is saveable before corruption.
        c.Interaction.HumanCompany=old;RejectedSave(sim);
    }
});
Check("明确头部Hurt记录头部戒备，身体恢复和读档不把它变为侧腹记忆",()=>
{
    var sim=Create();var c=sim.Selected!;sim.Hurt(c,20,"头部直接受伤","player",BodyPart.Head);
    Require(c.Interaction.HumanBond.Caution.GetValueOrDefault(BodyPart.Head)==60&&
        c.Interaction.HumanBond.Caution.GetValueOrDefault(BodyPart.Side)==0,"显式伤害部位被错误记到侧腹");
    // Isolate learned avoidance from current pain/wound refusal; this is a fixture, not a claimed healing path.
    c.Health=100;c.Pain=0;c.Interaction.HeadSensitivity=0;c.Interaction.Injuries.At(BodyPart.Head)!.Damage=0;
    Require(!InteractionReadiness.WillAcceptAffection(c,BodyPart.Head,SurfaceSide.Whole)&&
        InteractionReadiness.WillAcceptAffection(c,BodyPart.Side,SurfaceSide.Whole),"长期头部戒备未独立影响接受度");
    var copy=Reload(sim);Step(copy,50,showHand:false);
    Require(copy.Selected!.Interaction.HumanBond.Caution.GetValueOrDefault(BodyPart.Head)==60,"读档或离线等待抹掉局部戒备");
});
Check("离散弹额真实伤害归于头部且长期记忆不需要连续手势",()=>
{
    var sim=Create();var c=sim.Selected!;
    Require(!sim.HasInteractionGesture,"夹具不应有连续手势");
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.Flick,BodyPart.Head,Force:1)).Success,"实际弹额被拒绝");
    double caution=c.Interaction.HumanBond.Caution.GetValueOrDefault(BodyPart.Head);
    Require(c.Health<100&&c.Interaction.Injuries.At(BodyPart.Head) is {Damage:>0}&&caution>0&&
        c.Interaction.HumanBond.Caution.GetValueOrDefault(BodyPart.Side)==0,"离散弹额没有保留正确部位伤势和戒备");
    var copy=Reload(sim);Step(copy,50,showHand:false);
    Require(copy.Selected!.Interaction.HumanBond.Caution.GetValueOrDefault(BodyPart.Head)==caution,"弹额记忆没有持久保存");
});

Check("实时陪伴查询复用资格且切近看不等下一次输入或tick",()=>
{
    var sim=Create();var c=sim.Selected!;Start(sim);
    Require(sim.HasLiveHumanCompany(c),"已经开始的有效陪伴没有可读状态");
    string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    Require(sim.HasLiveHumanCompany(c)&&JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==before,"只读查询改变了状态");
    sim.State.Mode=PlayMode.CloseUp;
    double until=c.Interaction.HumanCompany.Until,time=sim.State.WorldSeconds;
    Require(!sim.HasLiveHumanCompany(c)&&c.Interaction.HumanCompany.Until==until&&sim.State.WorldSeconds==time,
        "切近看后旧手位置仍产生有效陪伴，或查询取消了真实记录");
    sim.State.Mode=PlayMode.Management;sim.State.Settings.Pause=true;
    Require(sim.HasLiveHumanCompany(c),"暂停不应删除既有有效陪伴状态");
    sim.SetInteractionHand(0,0,false);
    Require(!sim.HasLiveHumanCompany(c)&&c.Interaction.HumanCompany.Until==until,"旧三秒手位置记忆冒充实时陪伴");
});

Check("实际入水和刚出现的危险立即使陪伴表现失效而非靠旧Until",()=>
{
    var sim=Create();var c=sim.Selected!;Start(sim);
    var attacker=new Creature{Name="饥饿逼近者",X=1.8,Z=0,TargetX=1.8,Fullness=5,Energy=90,
        Body=new(){SpeciesId="marisa"},Interaction=new(){Enabled=true,Hydration=90},
        Personality=new(){Initialized=true,Kindness=.05,HungerTargetId=c.Id}};
    sim.State.Creatures.Add(attacker);
    double until=c.Interaction.HumanCompany.Until,time=sim.State.WorldSeconds;
    Require(!sim.HasLiveHumanCompany(c)&&c.Interaction.HumanCompany.Until==until&&sim.State.WorldSeconds==time,
        "逼近中的有效危险没有及时压过安心陪伴，或只读查询改变了世界");
    sim.InterruptInteractionThreat(attacker.Id);
    Require(sim.HasLiveHumanCompany(c),"已解除且尚未造成伤害的威胁仍被旧目标字段当作正在发生");
    sim.State.RoomObjects.Add(new(){Kind=RoomObjectKind.RiverWater,RegionId=c.RegionId,X=7,Z=0});
    c.X=7.1;sim.SetInteractionHand(7.5,0,true);
    Require(!c.Interaction.Water.InWater&&!sim.HasLiveHumanCompany(c),"陪伴查询只依赖未更新的入水缓存");
});

Check("轻伤短反应结束和接下一口甜食之后仍保留警惕与距离",()=>
{
    var sim=Create();var c=sim.Selected!;
    Require(c.Interaction.Emotion.SafeSeconds==0&&EmotionalReadiness.Settled(c),"未受惊的新个体被默认安全计时拦住");
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.Punch,BodyPart.Side,0)).Success,"轻击夹具未形成真实伤害");
    Require(c.Trust>35&&c.Interaction.Emotion.Alarm is >0 and <12&&!EmotionalReadiness.Settled(c),
        "低于旧门槛的真实惊惧仍被当作已经安定");
    double alarm=c.Interaction.Emotion.Alarm,fullness=c.Fullness;
    var offer=new InteractionInput(InteractionAction.FeedSweet,BodyPart.Mouth,Duration:.1);
    Require(sim.BeginInteractionGesture(c.Id,offer).Success,"受伤后连正常递食入口都被取消");
    for(int n=0;n<8;n++)sim.ContinueInteractionGesture(offer,true);
    sim.EndInteractionGesture();
    Require(c.Fullness>fullness&&c.Interaction.Emotion.Alarm==alarm,"食物没有吸引它，或吃一口清空了惊惧");
    Step(sim,80,.2);
    double distance=Math.Sqrt((c.X-.2)*(c.X-.2)+c.Z*c.Z);
    Require(distance>1.3&&!EmotionalReadiness.Settled(c)&&c.Interaction.HumanCompany.Until==0,
        "高信任者在轻伤短反应结束后立刻贴手或恢复无忧状态");
    Require(c.Expression is not (Expression.Content or Expression.Delighted),"刚咽下食物就覆盖了近期伤害表情");
});
Check("真实安全时间逐渐缩短距离，无威胁的默认个体不需先等待",()=>
{
    var sim=Create();var c=sim.Selected!;
    Require(sim.PreferredHumanDistance(c)==1.15,"默认未受惊者凭空产生退让距离");
    var fearful=new Creature{Fear=65,Trust=20};
    Require(sim.PreferredHumanDistance(fearful)==2.2,"旧档或其他来源的恐惧使用了另一套靠近距离");
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Punch,BodyPart.Side,0));
    double distance=sim.PreferredHumanDistance(c),alarm=c.Interaction.Emotion.Alarm;
    Step(sim,400,showHand:false);
    Require(c.Interaction.Emotion.SafeSeconds>=15&&c.Interaction.Emotion.Alarm>0&&c.Interaction.Emotion.Alarm<alarm&&
        EmotionalReadiness.Settled(c)&&sim.PreferredHumanDistance(c)<distance,
        "连续安全生活没有逐渐恢复，或凭空删掉了全部惊惧");
});
Check("受伤后仍可信的手可在保留距离时陪伴，不能把重新接近等同于贴脸",()=>
{
    var sim=Create();var c=sim.Selected!;
    sim.ExecuteInteraction(c.Id,new(InteractionAction.Punch,BodyPart.Side,0));
    Step(sim,610,showHand:false);
    double handX=c.X+(c.X>=0?-3:3),handZ=c.Z;
    Step(sim,160,handX,handZ);
    double distance=Math.Sqrt((c.X-handX)*(c.X-handX)+(c.Z-handZ)*(c.Z-handZ));
    Require(Memory(c)==1&&c.Interaction.HumanCompany.Remembered,"真实恢复后没有完成谨慎陪伴");
    Require(sim.PreferredHumanDistance(c)>1.15&&distance>=sim.PreferredHumanDistance(c)-.1&&
        c.Interaction.Emotion.Alarm>0&&c.Health<100,"陪伴消除了安全距离、惊惧或身体后果");
});

Check("跟随的大步终点留在舒适距离外，不在靠近和退避之间来回",()=>
{
    var sim=Create();var c=sim.Selected!;
    Require(sim.ExecuteInteraction(c.Id,new(InteractionAction.Punch,BodyPart.Side,0)).Success,"真实受击前置失败");
    double harmAt=c.Interaction.HumanBond.LastHarmAt;
    bool Moving()=>c.Interaction.Falling||c.Interaction.Carried||c.Interaction.Lowering||c.Interaction.Height>.05||
        Math.Abs(c.Interaction.VelocityX)+Math.Abs(c.Interaction.VelocityZ)>.005;
    // Let the real impact settle through the normal clock before measuring follow.
    // Do not erase the impulse, damage or emotional response to manufacture a quiet body.
    for(int n=0;n<200&&Moving();n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}
    Require(!Moving()&&c.Health<100&&c.Interaction.Emotion.Alarm>0&&c.Interaction.HumanBond.LastHarmAt==harmAt,
        "受击未实际停稳，或前置抹除了伤势和惊惧");
    Console.WriteLine($"TRACE follow settled world={sim.State.WorldSeconds:R}, vx={c.Interaction.VelocityX:R}, health={c.Health:R}, alarm={c.Interaction.Emotion.Alarm:R}");
    // Existing completed follow intent and a legal initial position are fixtures;
    // the injury and changing distance still come from the real interaction above.
    c.Interaction.FollowPlayer=true;c.X=c.TargetX=sim.PreferredHumanDistance(c)+.04;c.Z=c.TargetZ=0;
    double previous=c.X;
    for(int n=0;n<20;n++)
    {
        sim.SetInteractionHand(0,0,true);sim.AdvanceInteractionPresentation(.1);sim.Advance(.25);
        Require(c.X>=sim.PreferredHumanDistance(c)-.001&&c.X<=previous+.001,
            "跟随朝手中心跨过了安全圈，或下一步又向外退开"+
            $" (step={n}, x={c.X:R}, z={c.Z:R}, preferred={sim.PreferredHumanDistance(c):R}, previous={previous:R}, activity={c.Activity}, falling={c.Interaction.Falling}, height={c.Interaction.Height:R}, vx={c.Interaction.VelocityX:R}, vz={c.Interaction.VelocityZ:R}, vy={c.Interaction.VerticalVelocity:R}, cue={c.Interaction.ActiveCue})");
        previous=c.X;
    }
});

Console.WriteLine($"Human company: {failures} failure(s)");
return failures==0?0:1;

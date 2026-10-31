using System.Text.Json;
using Yukkuri.Simulation;

// Written for the independently authorized remote test machine. Do not run on the source workstation.
int failures=0;
void BuyTool(WorldSimulation sim,string id)
{
    var product=CourtyardShop.Products.Single(p=>p.Id==id);
    int money=sim.State.Money,stock=sim.ShopQuantity(id);
    Require(sim.BuyShopProduct(id)&&sim.State.Money==money-product.Price&&
        sim.ShopQuantity(id)==stock+product.Pack,"required paid fixture purchase failed: "+id);
}

void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception error){failures++;Console.WriteLine("FAIL "+name+": "+error);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261031);sim.State.RoomObjects.Clear();
    sim.State.PlayerX=0;sim.State.PlayerZ=7;
    for(int n=0;n<sim.State.Creatures.Count;n++)
    {
        var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=c.Health=100;c.Fear=c.Pain=0;
        c.TrueDisposition="gentle";c.Aggression=.08;c.Patience=.8;c.Courage=.2;
        c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.NextSocialAt=10000;c.Family.NextCareAt=10000;
        c.X=c.TargetX=n==0?0:n==1?-5:4;c.Z=c.TargetZ=n==1?-5:0;
    }
    var adult=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    child.IsYoung=true;child.Body.AgeSeconds=child.InnerAgeSeconds=0;child.Interaction.FootIntegrity=20;
    child.ParentId=child.Family.MotherId=sim.State.Creatures[1].Id;child.Family.CaregiverIds.Clear();
    adult.Family.Peers[child.Id]=new(){Affection=25,Familiarity=25};
    child.Family.Peers[adult.Id]=new(){Affection=8,Familiarity=10};
    return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
(WorldSimulation Sim,Creature Adult,Creature Child,RoomObject Source) Delivery(bool water=false)
{
    var sim=Fixture();var adult=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    child.Fullness=water?100:20;child.Interaction.Hydration=water?20:100;
    var source=new RoomObject{Id="care-learning-source",Kind=water?RoomObjectKind.Water:RoomObjectKind.Meal,
        RegionId=adult.RegionId,X=0,Z=.95,Portions=3};
    sim.State.RoomObjects.Add(source);sim.ArrangeCare(adult.Id,child.Id);
    Require(adult.Family.CareTargetId==child.Id,"安全熟悉的非亲代没有接纳照护");
    return(sim,adult,child,source);
}
RoomObject WaitForParcel(WorldSimulation sim,Creature adult)
{
    for(int n=0;n<120;n++)
    {Tick(sim,.25);if(adult.Family.CareParcelId is { } id&&sim.State.RoomObjects.Find(o=>o.Id==id&&o.CarrierId==adult.Id) is { } parcel)return parcel;}
    throw new Exception("没有取得实际运输份额："+adult.Activity);
}
int Portions(WorldSimulation sim,RoomObjectKind kind)=>sim.State.RoomObjects.Where(o=>o.Kind==kind&&!o.Stored).Sum(o=>o.Portions);

Check("非亲代自主照护逐步形成养亲，血缘与对人信任不被重写",()=>
{
    var sim=Fixture();var adult=sim.State.Creatures[0];var child=sim.State.Creatures[2];
    child.X=child.TargetX=1.5;child.Family.Warmth=20;adult.Family.NextCareAt=0;
    string? mother=child.Family.MotherId,parent=child.ParentId;double trust=child.Trust;
    Tick(sim,10);
    Require(adult.Family.Peers[child.Id].CareSeconds>0&&!child.Family.CaregiverIds.Contains(adult.Id),"没有实际照护或短暂接触立刻成为养亲");
    Tick(sim,170);
    Require(child.Family.CaregiverIds.Contains(adult.Id)&&adult.Family.Peers[child.Id].CareSeconds>=60,"持续实际照护没有形成养亲");
    Require(child.Family.MotherId==mother&&child.ParentId==parent&&child.Family.FatherId==null,"养亲覆盖了血缘");
    Require(child.Trust==trust&&child.Family.Peers[adult.Id].Affection>8,"油油关系复制或改变了对人信任");
});
Check("只有MotherId或FatherId的亲代也会自主照护",()=>
{
    foreach(bool mother in new[]{true,false})
    {
        var sim=Fixture();var adult=sim.State.Creatures[0];var child=sim.State.Creatures[2];
        adult.Family.Peers.Clear();child.Family.Peers.Clear();child.ParentId=null;
        child.Family.MotherId=mother?adult.Id:null;child.Family.FatherId=mother?null:adult.Id;
        child.X=child.TargetX=1.5;child.Family.Warmth=20;adult.Family.NextCareAt=0;
        Tick(sim,4);Require(adult.Family.Peers.TryGetValue(child.Id,out var relation)&&relation.CareSeconds>0,"血缘亲代被旧ParentId条件漏掉");
    }
});
Check("邀请本身不形成养亲，重复安排保留正在搬运的唯一份额",()=>
{
    var (sim,adult,child,source)=Delivery();
    Require(!child.Family.CaregiverIds.Contains(adult.Id)&&adult.Family.Peers[child.Id].CareSeconds==0,"点击安排就产生照护经验");
    var parcel=WaitForParcel(sim,adult);string id=parcel.Id;long sequence=sim.State.FamilySequence;
    for(int n=0;n<30;n++)sim.ArrangeCare(adult.Id,child.Id);
    Require(adult.Family.CareParcelId==id&&parcel.CarrierId==adult.Id&&sim.State.FamilySequence==sequence&&Portions(sim,source.Kind)==3,"重复安排中断运输或重新分份");
});
Check("食水先取出搬运，只有幼体实际入口才增加需求并消费份额",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var (sim,adult,child,source)=Delivery(water);double before=water?child.Interaction.Hydration:child.Fullness;
        var parcel=WaitForParcel(sim,adult);
        Require(source.Portions==2&&parcel.Portions==1&&Portions(sim,source.Kind)==3,"分装复制或丢失份额");
        Require((water?child.Interaction.Hydration:child.Fullness)<=before,"搬运时隔空喂食");
        for(int n=0;n<240&&(water?child.Interaction.Hydration:child.Fullness)<=before;n++)Tick(sim,.25);
        Require((water?child.Interaction.Hydration:child.Fullness)>before,"送达后没有实际吃喝");
        Require(parcel.CarrierId==null&&Portions(sim,source.Kind)<=2&&child.Interaction.Digestion.Exposures>0,"没有走真实入口与有限消耗");
    }
});
Check("目标被抱走、入箱、冷藏、长大或逃离立即解除搬运",()=>
{
    foreach(string mode in new[]{"carried","box","cold","grown","flight","retreat","region","hidden"})
    {
        var (sim,adult,child,source)=Delivery();var parcel=WaitForParcel(sim,adult);double x=adult.X,z=adult.Z;
        if(mode=="carried"){child.Interaction.Carried=true;child.Interaction.Height=2;}
        else if(mode=="box")child.Interaction.Restraint=RestraintKind.Box;
        else if(mode=="cold"){BuyTool(sim,"freezer");sim.SetColdStorage(child.Id,true);Require(child.Family.ColdStorage.Stored,"中断夹具未进入冷藏");}
        else if(mode=="grown")child.IsYoung=false;
        else if(mode=="flight"){child.Family.FlightRemaining=8;child.Family.FlightX=5;child.Family.FlightZ=3;}
        else if(mode=="retreat")child.RetreatRemaining=8;
        else if(mode=="region"){sim.State.Regions.Add(new(){Id="care-elsewhere",Name="另一处",Kind=RegionKind.Wilderness});child.RegionId="care-elsewhere";}
        else {child.X=child.TargetX=7;child.Z=child.TargetZ=6;}
        sim.AdvanceInteractionPresentation(.05);
        Require(adult.Family.CareTargetId==null&&adult.Family.CareParcelId==null&&parcel.CarrierId==null,"目标中断没有释放："+mode);
        Require(Portions(sim,source.Kind)==3&&WorldSimulation.Distance(parcel.X,parcel.Z,x,z)<.001,"中断丢份额或瞬移到幼体："+mode);
    }
});
Check("照护者逃离、休息或急需吃喝时卸下份额并释放自身动作",()=>
{
    foreach(string mode in new[]{"flight","rest","food","water","follow"})
    {
        var (sim,adult,child,source)=Delivery();var parcel=WaitForParcel(sim,adult);
        if(mode=="flight")adult.Family.FlightRemaining=8;
        else if(mode=="rest")adult.Interaction.Resting=true;
        else if(mode=="food")adult.Fullness=10;
        else if(mode=="water")adult.Interaction.Hydration=10;
        else adult.Interaction.FollowPlayer=true;
        sim.AdvanceInteractionPresentation(.05);
        Require(parcel.CarrierId==null&&adult.Family.CareTargetId==null&&Portions(sim,source.Kind)==3,"自身优先需求未中断搬运："+mode);
    }
});
Check("隔着箱体、第三只箱体或悬空不能保暖或刷照护",()=>
{
    foreach(string mode in new[]{"child-box","adult-high","third-box","far"})
    {
        var sim=Fixture();var adult=sim.State.Creatures[0];var child=sim.State.Creatures[2];
        child.X=child.TargetX=1.5;child.Family.Warmth=20;child.Family.CaregiverIds.Add(adult.Id);adult.Family.NextCareAt=0;
        if(mode=="child-box")child.Interaction.Restraint=RestraintKind.Box;
        else if(mode=="adult-high"){adult.Interaction.Carried=true;adult.Interaction.Height=2;}
        else if(mode=="far")child.X=child.TargetX=5;
        else
        {
            var blocker=sim.State.Creatures[1];blocker.X=blocker.TargetX=.75;blocker.Z=blocker.TargetZ=0;
            blocker.Interaction.Restraint=RestraintKind.Box;
        }
        // One ordinary tick tests contact before approaching could legitimately close a gap.
        Tick(sim,.25);
        Require(child.Family.Warmth<=20&&adult.Family.Peers[child.Id].CareSeconds==0,"隔空保暖或照护："+mode);
    }
});
Check("幼体害怕或怨恨照护者时不强行靠近",()=>
{
    foreach(bool fear in new[]{true,false})
    {
        var sim=Fixture();var adult=sim.State.Creatures[0];var child=sim.State.Creatures[2];
        if(fear)child.Family.Peers[adult.Id].Fear=70;else child.Family.Peers[adult.Id].Resentment=70;
        child.Family.Warmth=20;adult.Family.NextCareAt=0;sim.ArrangeCare(adult.Id,child.Id);Tick(sim,2);
        Require(adult.Family.CareTargetId==null&&adult.Family.Peers[child.Id].CareSeconds==0,"关系拒绝被亲代安排绕过");
    }
});
Check("用户拿走运输物后照护者不能夺回或复制",()=>
{
    var (sim,adult,child,source)=Delivery();var parcel=WaitForParcel(sim,adult);
    Require(sim.HoldSupply(parcel.Id),"无法拿起运输份额");
    Tick(sim,1);
    Require(parcel.HeldByHand&&parcel.CarrierId==null&&adult.Family.CareParcelId==null&&Portions(sim,source.Kind)==3,"仍占用手中物或复制份额");
});
Check("没有资源或资源已经收起时不凭空造食水",()=>
{
    foreach(bool missing in new[]{true,false})
    {
        var (sim,adult,child,source)=Delivery();
        if(missing)sim.State.RoomObjects.Clear();
        else {source.Stored=true;source.Portions=3;}
        double fullness=child.Fullness;Tick(sim,15);
        Require(adult.Family.CareParcelId==null&&child.Fullness<=fullness&&
            sim.State.RoomObjects.All(o=>o.Id==source.Id),"不可获得的来源被复制为运输份额");
    }
});
Check("分装保留污染但不读取隐藏污染来修改生活认知",()=>
{
    var (sim,adult,child,source)=Delivery();source.Contamination=.7;
    adult.Family.LifeKnowledge["safe:Meal"]=true;child.Family.LifeKnowledge["safe:Meal"]=false;
    var parcel=WaitForParcel(sim,adult);
    Require(parcel.Contamination==source.Contamination,"分装洗掉了真实污染");
    Require(adult.Family.LifeKnowledge["safe:Meal"]&&!child.Family.LifeKnowledge["safe:Meal"]&&child.Interaction.Digestion.Exposures==0,
        "搬运或看见食物泄露隐藏污染或提前算进食经验");
});
Check("看见与搬运不纠正旧认知，实际干净吃喝后才改变",()=>
{
    foreach(bool water in new[]{false,true})
    {
        var (sim,adult,child,source)=Delivery(water);string key="safe:"+source.Kind;
        child.Family.LifeKnowledge[key]=false;adult.Family.LifeKnowledge[key]=false;
        WaitForParcel(sim,adult);
        Require(!child.Family.LifeKnowledge[key]&&!adult.Family.LifeKnowledge[key],"未入口就凭搬运改变知识");
        for(int n=0;n<240&&child.Interaction.Digestion.Exposures==0;n++)Tick(sim,.25);
        Require(child.Interaction.Digestion.Exposures>0&&child.Family.LifeKnowledge[key],"实际安全吃喝不能纠正旧认知");
        Require(!adult.Family.LifeKnowledge[key],"幼体的个人进食经验自动复制给照护者");
    }
});
Check("冷藏、暂停和近看不凭空增加照护经验",()=>
{
    foreach(string mode in new[]{"cold","pause","close"})
    {
        var sim=Fixture();var adult=sim.State.Creatures[0];var child=sim.State.Creatures[2];
        child.X=child.TargetX=1.5;child.Family.Warmth=20;adult.Family.NextCareAt=0;
        if(mode=="cold"){BuyTool(sim,"freezer");sim.SetColdStorage(child.Id,true);Require(child.Family.ColdStorage.Stored,"经验夹具未进入冷藏");}
        else if(mode=="pause")sim.State.Settings.Pause=true;
        else sim.State.Mode=PlayMode.CloseUp;
        Tick(sim,30);Require(adult.Family.Peers[child.Id].CareSeconds==0&&!child.Family.CaregiverIds.Contains(adult.Id),"无实际照护却形成经验："+mode);
    }
});
Check("搬运中读档继续一致，照护历史与养亲引用独立保存",()=>
{
    var (sim,adult,child,source)=Delivery();WaitForParcel(sim,adult);var restored=Reload(sim);
    Tick(sim,5);Tick(restored,5);
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(restored.State,SaveStore.JsonOptions),"存档后重复分份或照护推进不一致");
    Require(sim.State.Creatures[2].Family.MotherId==sim.State.Creatures[1].Id,"存档照护改写亲缘");
});

Environment.ExitCode=failures==0?0:1;

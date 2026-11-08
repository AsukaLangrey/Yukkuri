using System.Text.Json;
using Yukkuri.Simulation;

// SOURCE ONLY in the isolated delivery. Run only after the user's build/test confirmation.
int failed=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action check){try{check();Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e);}}
void Tick(WorldSimulation sim,double seconds){for(double t=0;t<seconds-1e-9;t+=.25)sim.Advance(.25);}
WorldSimulation Yard(int count=2)
{
    var s=new WorldState{InteractionPlaytest=true,MilestoneScenario=true,EconomyProgression=true,CourtyardLifeVersion=1,
        GrassNutritionCredit=0,Money=240,Food=50,StationWaterPortions=20,
        Regions=[new(){Id="home",Name="检查小院",Kind=RegionKind.Sanctuary}],AutonomousBreeding=false};
    for(int n=0;n<count;n++)
    {
        var c=new Creature{Id="check-oil-"+n,Name="同名",X=-4+n%5*2,Z=2+n/5*2,TargetX=-4+n%5*2,TargetZ=2+n/5*2,
            Fullness=100,Energy=90,Health=100,Trust=80,Patience=.7,Aggression=0,Courage=.7,
            Body=new(){Id="check-body-"+n,SpeciesId="reimu",ReproductionRole=n==1?ReproductionRole.Paternal:ReproductionRole.Maternal},
            Registration=new(){IdentityId="check-person-"+n},
            Interaction=new(){Enabled=true,Hydration=100,NextAutonomyAt=100000,ToyInterest=0,Accessory=new(){Id="check-accessory-"+n}},
            Family=new(){NextCareAt=100000,NextSocialAt=100000,NextBreedingDecisionAt=100000}};
        GrowthRules.Initialize(c,false);s.Creatures.Add(c);
    }
    s.SelectedId=s.Creatures.FirstOrDefault()?.Id;return new(s);
}
WorldSimulation Clone(WorldSimulation sim)=>new(JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!);
void Pregnant(WorldSimulation sim,BirthMethod method,int count)
{
    var mother=sim.State.Creatures[0];var father=sim.State.Creatures[1];
    mother.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=method,FatherId=father.Id,EventId="check-birth",PlannedChildren=count};
}
Check("四阶段是7/14/14天，边界体型连续且仅成油可繁殖",()=>{
    var sim=Yard();var c=sim.Selected!;GrowthRules.Initialize(c,true);
    foreach(var (age,stage) in new[]{(0d,GrowthStage.Infant),(4200d,GrowthStage.Child),(12600d,GrowthStage.Youth),(21000d,GrowthStage.Adult)})
    {
        c.Body.AgeSeconds=age;Require(GrowthRules.Stage(c)==stage,"stage at "+age);
        if(age>0){double right=GrowthRules.Scale(c);c.Body.AgeSeconds=age-.0001;Require(Math.Abs(right-GrowthRules.Scale(c))<.0001,"scale jump");c.Body.AgeSeconds=age;}
    }
    Require(GrowthRules.AdultAge/60==350,"adult duration");
    c.Body.AgeSeconds=12600;c.IsYoung=true;Require(sim.RequestPregnancy(c.Id,sim.State.Creatures[1].Id,BirthMethod.Stem).Contains("成熟"),"youth allowed breeding");
    Require(GrowthRules.NeedsFactor(c)<1,"youth adult needs");
});
Check("旧成油不返幼，旧幼油按首7天比例迁移，孕期保留比例且迁移幂等",()=>{
    var sim=Yard();var s=sim.State;var adult=s.Creatures[0];var child=s.Creatures[1];
    adult.GrowthVersion=child.GrowthVersion=0;adult.Body.AgeSeconds=50;adult.IsYoung=false;child.IsYoung=true;child.Body.AgeSeconds=900;
    s.CourtyardLifeVersion=0;adult.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Live,Elapsed=240,PlannedChildren=1};
    GrowthRules.MigrateWorld(s);Require(adult.Body.AgeSeconds==21000&&child.Body.AgeSeconds==2100,"age migration");
    Require(adult.Family.Pregnancy.Elapsed==4200,"pregnancy fraction");
    string before=JsonSerializer.Serialize(s,SaveStore.JsonOptions);GrowthRules.MigrateWorld(s);
    Require(before==JsonSerializer.Serialize(s,SaveStore.JsonOptions),"migration not idempotent");
});
Check("旧档损坏数据仍拒绝，迁移不吞掉负年龄或空记录",()=>{
    var sim=Yard();sim.Selected!.GrowthVersion=0;sim.Selected.Body.AgeSeconds=-1;
    bool rejected=false;try{GrowthRules.MigrateWorld(sim.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"negative age silently repaired");
    var invalid=Yard();invalid.State.CourtyardLifeVersion=0;invalid.State.DetachedStemBroods.Add(null!);
    rejected=false;try{GrowthRules.MigrateWorld(invalid.State);}catch(InvalidDataException){rejected=true;}
    Require(rejected,"null stem broke backup-compatible validation");
});
Check("正常成油每天60饱食60水分，幼龄需求较低，手动与自动水同为60",()=>{
    var sim=Yard(1);var c=sim.Selected!;c.Interaction.Restraint=RestraintKind.Box;
    Tick(sim,1);Require(Math.Abs(c.Fullness-99.9)<1e-8&&Math.Abs(c.Interaction.Hydration-99.9)<1e-8,"adult baseline");
    Require(CourtyardSurvival.ManualWaterGain==NaturalResources.WaterValue(RoomObjectKind.Water),"water gain mismatch");
    var young=Yard(1);GrowthRules.Initialize(young.Selected!,true);young.Selected!.Interaction.Restraint=RestraintKind.Box;Tick(young,1);
    Require(young.Selected.Fullness>c.Fullness&&young.Selected.Interaction.Hydration>c.Interaction.Hydration,"infant needs");
});
Check("暂停近看和冷藏都不推进年龄、孕期与困意，屋内照常计时",()=>{
    var sim=Yard();Pregnant(sim,BirthMethod.Live,2);var c=sim.Selected!;
    foreach(var mode in new[]{"pause","close","cold"})
    {
        sim.State.Settings.Pause=mode=="pause";sim.State.Mode=mode=="close"?PlayMode.CloseUp:PlayMode.Management;c.Family.ColdStorage.Stored=mode=="cold";
        double age=c.Body.AgeSeconds,preg=c.Family.Pregnancy.Elapsed,sleep=c.Interaction.Sleepiness,water=c.Interaction.Hydration;
        Tick(sim,1);Require(c.Body.AgeSeconds==age&&c.Family.Pregnancy.Elapsed==preg&&c.Interaction.Sleepiness==sleep&&c.Interaction.Hydration==water,"clock "+mode);
    }
    c.Family.ColdStorage.Stored=false;sim.State.Mode=PlayMode.Management;sim.State.Settings.Pause=false;Tick(sim,1);
    Require(c.Family.Pregnancy.Elapsed>0,"resumed pregnancy");
});
Check("7/14天孕期，保存的每胎数量不因读档重抽，五芽均可存档",()=>{
    Require(FamilyRules.GestationSeconds(BirthMethod.Stem)==4200&&FamilyRules.GestationSeconds(BirthMethod.Live)==8400,"gestation duration");
    foreach(var method in new[]{BirthMethod.Stem,BirthMethod.Live})foreach(int count in method==BirthMethod.Live?new[]{1,2}:new[]{2,3,4,5})
    {
        var sim=Yard();Pregnant(sim,method,count);Tick(sim,.25);SaveStore.Validate(sim.State);
        ulong rng=sim.State.RandomState;var loaded=Clone(sim);
        Require(loaded.Selected!.Family.Pregnancy.PlannedChildren==count&&loaded.State.RandomState==rng,"count reroll");
        loaded.Selected.Family.Pregnancy.Elapsed=FamilyRules.GestationSeconds(method)-.25;
        loaded.Selected.Fullness=loaded.Selected.Interaction.Hydration=100;Tick(loaded,.5);
        Require(loaded.State.CompletedBirths.Contains("check-birth")&&loaded.State.Creatures.Count==2+count,"delivery count");
        Require(loaded.State.Creatures.Skip(2).All(c=>c.GrowthVersion==1&&c.Body.AgeSeconds<1&&c.Family.MotherId==sim.Selected!.Id&&c.Robustness is >=0 and <=1),"birth init");
        var roundTrip=Clone(loaded);Tick(roundTrip,1);Require(roundTrip.State.Creatures.Count==loaded.State.Creatures.Count,"duplicate delivery");
    }
});
Check("困意与体力分离，70点食水足够8小时，屋内基本保暖但仍有拥挤惩罚",()=>{
    var sim=Yard(1);var c=sim.Selected!;sim.State.ShelterStock=1;Require(sim.PlaceShelter(0,-5).Success,"house placement");
    var h=sim.State.Shelters[0];c.X=c.TargetX=h.X-1.08;c.Z=c.TargetZ=h.Z-1.08;
    c.Fullness=c.Interaction.Hydration=70;c.Interaction.Sleepiness=80;
    h.Residents.Add(new(){CreatureId=c.Id,Slot=0,Phase=ShelterVisitPhase.Inside,VisualHidden=true});
    sim.State.WorldSeconds=350;Tick(sim,199.5);
    Require(c.Interaction.Resting&&sim.IsShelteredHidden(c),"small hunger interrupted sleep "+JsonSerializer.Serialize(new{c.X,c.Z,c.Activity,c.Fullness,c.Interaction.Hydration,c.Interaction.Resting,c.Interaction.Sleepiness,c.Family.Warmth,Radius=WorldSimulation.ShelterPassageRadius(c),h.Residents}));
    Require(c.Fullness>49&&c.Interaction.Hydration>39,"night consumption");
    Require(c.Interaction.Sleepiness<5&&c.Body.AgeSeconds>GrowthRules.AdultAge+199,"house clock or sleep");
    Require(WorldSimulation.ShelterComfort(5)=="极度拥挤","crowding");
});
Check("真实门帘进度、受阻不关门、完全进屋隐藏和出口恢复可见",()=>{
    var sim=Yard(1);sim.State.ShelterStock=1;Require(sim.PlaceShelter(0,-5).Success,"house placement");
    var h=sim.State.Shelters[0];var c=sim.Selected!;sim.State.WorldSeconds=350;c.Interaction.Sleepiness=80;
    bool partial=false,hidden=false;double previousX=c.X,previousZ=c.Z;
    for(int n=0;n<480;n++)
    {
        Tick(sim,.25);Require(Math.Sqrt(Math.Pow(c.X-previousX,2)+Math.Pow(c.Z-previousZ,2))<.4,"teleport");previousX=c.X;previousZ=c.Z;
        partial|=h.DoorProgress>0&&h.DoorProgress<1;hidden|=sim.IsShelteredHidden(c);if(hidden)break;
    }
    Require(hidden,"did not enter");var loaded=Clone(sim);Require(loaded.IsShelteredHidden(loaded.Selected!),"loaded hidden state");
    c.Interaction.Hydration=20;Tick(sim,.25);Require(!sim.IsShelteredHidden(c)||!h.DoorPassable,"exit visibility before door open");
    for(int n=0;n<400&&h.Residents.Count>0;n++)Tick(sim,.25);
    Require(!sim.IsShelteredHidden(c)&&h.Residents.Count==0,"exit stuck");
    c.X=h.X;c.Z=h.Z+1.25*ShelterGeometry.SharedScale;h.DoorOpen=false;Tick(sim,2);
    Require(h.DoorProgress>.99,"gate closed on body");
    Require(partial||h.DoorProgress==1,"animation never progressed");
});
Check("五床容量包含预约，茎生母油通过门洞占一床，户外住户不被隐藏",()=>{
    var sim=Yard(6);sim.State.ShelterStock=1;Require(sim.PlaceShelter(0,-5).Success,"house placement");
    sim.State.WorldSeconds=350;foreach(var c in sim.State.Creatures)c.Interaction.Sleepiness=80;
    Tick(sim,100);var home=sim.State.Shelters[0];
    Require(home.Residents.Count<=5&&sim.ShelterStatus(home.Id)!.OccupantIds.Length<=5,"capacity exceeded");
    Require(sim.State.Creatures.Count(c=>sim.IsShelteredHidden(c))==5,"five arrivals or outdoor visibility "+JsonSerializer.Serialize(sim.State.Creatures.Select(c=>new{c.Id,c.X,c.Z,c.Activity,Radius=WorldSimulation.ShelterPassageRadius(c),Hidden=sim.IsShelteredHidden(c),Resident=home.Residents.Find(v=>v.CreatureId==c.Id)})));
    foreach(var resident in sim.State.Creatures)resident.Interaction.Hydration=20;Tick(sim,120);
    Require(home.Residents.Count==0&&sim.State.Creatures.All(c=>!sim.IsShelteredHidden(c)),"five residents could not exit in sequence");
    var pregnant=Yard();pregnant.State.ShelterStock=1;Require(pregnant.PlaceShelter(0,-5).Success,"pregnancy house placement");
    Pregnant(pregnant,BirthMethod.Stem,5);pregnant.State.WorldSeconds=350;
    Require(pregnant.ShelterEntryReason(pregnant.Selected!,pregnant.State.Shelters[0].Id)==null,"attached stem denied shared house");
    Tick(pregnant,100);var mother=pregnant.Selected!;
    Require(pregnant.IsShelteredHidden(mother)&&mother.Interaction.StemFold>.99,"mother did not physically enter with folded stem");
    Require(pregnant.State.Shelters[0].Residents.Count(v=>v.CreatureId==mother.Id)==1&&pregnant.State.Creatures.Count==2,"unborn buds took beds or created early children");
});
Check("野草营养预算不随面积增长、污染按摄入剂量而非帧数、健壮度并非免疫",()=>{
    Require(WorldSimulation.GrassNutritionPerDay==300&&WorldSimulation.GrassPortionCap*7<=300,"grass budget");
    var sim=Yard(0);sim.State.GrassNutritionCredit=300;Tick(sim,30);
    Require(sim.State.RoomObjects.Where(o=>o.Kind==RoomObjectKind.Leaves).Sum(o=>o.Portions)<=42,"stock cap");
    var c=new Creature();GrowthRules.Initialize(c,false);c.Robustness=1;double robust=CourtyardSurvival.InfectionChance(c,RoomObjectKind.Leaves,1);
    c.Robustness=0;double weak=CourtyardSurvival.InfectionChance(c,RoomObjectKind.Leaves,1);
    Require(robust>0&&robust<weak,"constitution effect");
    c.Robustness=.5;double bite=CourtyardSurvival.InfectionChance(c,RoomObjectKind.RiverWater,1);
    Require(Math.Abs(1-Math.Pow(1-bite,60.0/60)-.22)<1e-8,"daily exposure hazard");
    SaveStore.Validate(sim.State);
});
Check("同名对象事件严格按ID、旧分工取消但能力和牌照估值保留",()=>{
    var sim=Yard();var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];
    sim.State.ObjectEvents.Add(new(){ObjectId=a.Id,Text="仅甲",At=0});sim.State.Journal.Add("同名的全局消息");
    Require(sim.EventsFor(a.Id).Contains("仅甲")&&!sim.EventsFor(b.Id).Any(),"name/global leakage");
    a.Registration.Grade="金牌";a.Interaction.Development.Duty=LifeDuty.Care;a.Interaction.Development.RecipientId=b.Id;
    int price=CourtyardEconomy.Quote(a).Price;var loaded=Clone(sim);
    Require(loaded.Selected!.Interaction.Development.Duty==LifeDuty.None&&loaded.Selected.Registration.Grade=="金牌"&&CourtyardEconomy.Quote(loaded.Selected).Price==price,"directive migration lost training value");
    Require(sim.SetLifeDuty(a.Id,LifeDuty.Cleaning).Contains("暂未开放"),"manual duty still exposed");
});
Check("扩场边界与存档一致，跳跃暂停保持姿态",()=>{
    Require(Math.Abs(Math.Pow(CourtyardGround.HalfSize/8,2)-2)<1e-10,"area is not doubled");
    var sim=Yard(1);var c=sim.Selected!;sim.MoveCreature(c.Id,-10,8.5);SaveStore.Validate(sim.State);
    Require(c.X==-10&&c.Z==8.5,"old clamp survived");
    var hop=new ActorHopMotion();hop.Reset(System.Numerics.Vector2.Zero);hop.Advance(new(0,.5f),.1f,true,false);
    var frame=hop.Frame;var position=hop.Position;hop.Advance(new(0,.5f),.1f,ActorHopMotion.AllowsView(true,true,true),true);
    Require(hop.Frame==frame&&hop.Position==position,"pause discarded hop");
});

Check("季节昼夜温度连续，全年龄变冷会寻找同伴且暂停冷藏不失温",()=>{
    double winter=25*600,summer=11*600;
    Require(CourtyardClimate.Temperature(summer)>CourtyardClimate.Temperature(winter)+15,"season order");
    Require(Math.Abs(CourtyardClimate.Temperature(7*600-200+.001)-CourtyardClimate.Temperature(7*600-200-.001))<.01,"season discontinuity");
    var sim=Yard(2);sim.State.WorldSeconds=winter;var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];
    a.Family.Warmth=b.Family.Warmth=30;double before=Math.Abs(a.X-b.X);Tick(sim,8);
    Require(a.Family.SeekingWarmth&&Math.Abs(a.X-b.X)<before,"adult did not approach warmth");
    double warmth=a.Family.Warmth;sim.State.Settings.Pause=true;Tick(sim,20);Require(a.Family.Warmth==warmth,"pause cooled body");
    sim.State.Settings.Pause=false;a.Family.ColdStorage.Stored=true;Tick(sim,20);Require(a.Family.Warmth==warmth,"storage cooled body");
    var warm=Yard(1);warm.State.WorldSeconds=summer;warm.Selected!.Family.Warmth=40;Tick(warm,8);Require(warm.Selected.Family.Warmth>40,"warm day did not restore warmth");
});
Check("落地重叠分步解开，不瞬移，不穿小屋墙，最终释放碰撞",()=>{
    var sim=Yard(2);var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];a.X=b.X;a.Z=b.Z;
    a.Interaction.Height=.1;a.Interaction.Falling=true;double traveled=0;
    for(int n=0;n<80;n++){double x=a.X,z=a.Z;for(int f=0;f<5;f++)sim.AdvanceInteractionPresentation(.05);Tick(sim,.25);double d=Math.Sqrt(Math.Pow(a.X-x,2)+Math.Pow(a.Z-z,2));Require(d<.25,"landing teleport");traveled+=d;}
    Require(traveled>.5&&!a.Interaction.LandingSeparationPending,"landing did not separate "+JsonSerializer.Serialize(new{traveled,a.X,a.Z,a.Interaction.LandingSeparationPending,a.Interaction.Falling,a.Activity,OtherX=b.X,OtherZ=b.Z}));
});
if(args.Contains("--budget-14-days"))BudgetCampaign();
Environment.ExitCode=failed==0?0:1;

void BudgetCampaign()
{
    // Measurement scenarios, not acceptance claims. Purchases use real packs and cash; no top-ups, no sales.
    foreach(var scenario in new[]{"current-start-shop","five-adults-shop","current-start-pregnant-shop","current-start-wild-unavailable","current-start-polluted-wild"})
    {
        var sim=WorldSimulation.CreateCourtyardEconomy(20261102);var s=sim.State;s.AutonomousBreeding=false;
        if(scenario=="five-adults-shop")
        {
            foreach(var c in s.Creatures)GrowthRules.Initialize(c,false);
            for(int n=3;n<5;n++){var c=new Creature{Id="budget-extra-"+n,Name="额外成油",X=-6+n,Z=4,Body=new(){SpeciesId="reimu"},Interaction=new(){Enabled=true}};GrowthRules.Initialize(c,false);s.Creatures.Add(c);}
        }
        if(scenario.Contains("pregnant")){s.Creatures[0].Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,FatherId=s.Creatures[1].Id,EventId="budget-birth",PlannedChildren=5};sim=new(s);}
        sim.Config.WildGrassEnabled=!(scenario.Contains("shop")||scenario.Contains("unavailable"));
        int spent=0,firstShortageDay=0;double minFood=100,minWater=100;int deaths=0;int startCount=s.Creatures.Count;
        var daily=new List<object>();
        for(int step=0;step<14*600*4;step++)
        {
            bool shop=scenario.Contains("shop"),unavailable=scenario.Contains("unavailable");
            if(shop||unavailable)s.RoomObjects.RemoveAll(o=>NaturalResources.IsWild(o.Kind)&&NaturalResources.Consumer(s,o)==null&&!s.Creatures.Any(c=>c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeSourceId==o.Id));
            foreach(var c in s.Creatures.Where(c=>c.Condition==PhysicalCondition.Active).ToArray())
            {
                // An explicit supply policy places paid stock beside a consumer. The creature must still take it physically.
                bool water=c.Interaction.Hydration<70&&!s.RoomObjects.Any(o=>o.Kind==RoomObjectKind.Water&&o.Portions>0&&Math.Abs(o.X-c.X)<1&&Math.Abs(o.Z-c.Z)<1);
                bool food=c.Fullness<(shop||unavailable?70:30)&&!s.RoomObjects.Any(o=>o.Kind==RoomObjectKind.Meal&&o.Portions>0&&Math.Abs(o.X-c.X)<1&&Math.Abs(o.Z-c.Z)<1);
                if(water&&s.StationWaterPortions==0){int before=s.Money;if(sim.BuyShopProduct("water"))spent+=before-s.Money;else if(firstShortageDay==0)firstShortageDay=1+step/2400;}
                if(food&&s.Food==0){int before=s.Money;if(sim.BuyShopProduct("food"))spent+=before-s.Money;else if(firstShortageDay==0)firstShortageDay=1+step/2400;}
                if(water||food)
                {
                    var action=water?InteractionAction.PlaceWater:InteractionAction.PlaceMeal;
                    // Never place supplies in the hut; emergency need makes the resident exit first.
                    if(!sim.IsShelteredHidden(c))sim.PlaceInteractionSupply(action,c.X+.2,c.Z+.35);
                }
            }
            sim.Advance(.25);
            minFood=Math.Min(minFood,s.Creatures.Min(c=>c.Fullness));minWater=Math.Min(minWater,s.Creatures.Min(c=>c.Interaction.Hydration));
            if((step+1)%2400==0)daily.Add(new{Day=(step+1)/2400,s.Money,Spent=spent,Living=s.Creatures.Count(c=>c.Condition!=PhysicalCondition.Dead),Births=s.Creatures.Count-startCount,MinFood=minFood,MinWater=minWater,ShortageDay=firstShortageDay});
        }
        deaths=s.Creatures.Count(c=>c.Condition==PhysicalCondition.Dead);
        Console.WriteLine(JsonSerializer.Serialize(new{Scenario=scenario,Daily=daily,Deaths=deaths,Episodes=s.Creatures.Sum(c=>c.Interaction.Digestion.Episodes),Exposures=s.Creatures.Sum(c=>c.Interaction.Digestion.Exposures),RuleVersion=s.CourtyardLifeVersion}));
    }
}

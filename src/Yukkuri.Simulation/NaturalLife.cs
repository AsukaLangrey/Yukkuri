using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public sealed class ResourceMemory
{
    public string RegionId { get; set; } = "home";
    public RoomObjectKind Kind { get; set; }
    public double X { get; set; }
    public double Z { get; set; }
    public double SeenAt { get; set; }
    public bool Available { get; set; } = true;
    public double AvoidUntil { get; set; }
    // Learned suspicion, not the resource's hidden contamination value.
    public double Suspicion { get; set; }
}

public sealed class DigestiveHealth
{
    public int Exposures { get; set; }
    public int Episodes { get; set; }
    public double IncubationRemaining { get; set; }
    public double SymptomsRemaining { get; set; }
    public double Severity { get; set; }
    public double CareRemaining { get; set; }
    public string? SuspectedSourceId { get; set; }
    public double NextFoodAt { get; set; }
    public List<RecentIntake> RecentIntakes { get; set; } = new();
    public bool Symptomatic => SymptomsRemaining > 0;
}

public sealed class RecentIntake
{
    public string? SourceId { get; set; }
    public RoomObjectKind? Kind { get; set; }
    public double At { get; set; }
}

public sealed partial class InteractionLife
{
    public Dictionary<string,ResourceMemory> Resources { get; set; } = new();
    public DigestiveHealth Digestion { get; set; } = new();
    public string? ResourceTargetId { get; set; }
    public double ResourceBestDistance { get; set; } = 100;
    public double ResourceStuckSeconds { get; set; }
    public double NextResourceScanAt { get; set; }
    public double ExploreUntil { get; set; }
    public double LastHelpDecisionAt { get; set; } = -100;
    public double HelpWaitingUntil { get; set; }
}

public sealed partial class WorldState
{
    [JsonIgnore] public bool InteractionHandPresent { get; set; }
    [JsonIgnore] public double InteractionHandUntil { get; set; }
    [JsonIgnore] public string? InteractionHandRegionId { get; set; }
}

public static class NaturalResources
{
    public const double SenseRange = 3.5;
    public const double BiteContactSeconds=.45;
    public static Creature? Consumer(WorldState state,RoomObject source)=>state.Creatures.Find(c=>
        c.RegionId==source.RegionId&&c.Condition!=PhysicalCondition.Dead&&c.Interaction.GroundIntake&&
        c.Interaction.GroundIntakeSourceId==source.Id&&c.Interaction.EatingRemaining>0&&
        !c.Interaction.Carried&&!c.Interaction.Falling&&!c.Interaction.Supine&&c.Interaction.Height<=.25);
    // The portion is already claimed. Keep it visible until the taking motion reaches
    // the source, without making that same portion available to a second creature.
    public static int VisibleFoodPortions(WorldState state,RoomObject source)
    {
        var consumer=Consumer(state,source);
        bool taking=consumer?.Interaction.NaturalIntakePending==true;
        return source.Portions+(taking?1:0);
    }
    public static double VisibleWaterFill(WorldState state,RoomObject source)
    {
        if(source.Portions>0)return 1;
        var consumer=Consumer(state,source);
        return consumer==null?0:Math.Clamp((consumer.Interaction.EatingRemaining-.25)/
            (InteractionLife.IntakeDuration(IntakeKind.Water)-.25-BiteContactSeconds),0,1);
    }
    // Centre-to-source reach follows the same body scale as the standing envelope.
    // Equipment does not extend the mouth's reach.
    public static double Reach(Creature c) => .8*(c.IsYoung?.73:c.IsElder?1.03:1);
    public static (double X,double Z) Approach(Creature c,ResourceMemory source)
    {
        double reach=Reach(c)*.99;
        // River access faces the dry west bank. Walk to a standing point, not into
        // the source and rely on collision rejection to happen within mouth reach.
        if(source.Kind==RoomObjectKind.RiverWater)return (source.X-reach,source.Z);
        double dx=source.X-c.X,dz=source.Z-c.Z,distance=Math.Sqrt(dx*dx+dz*dz);
        if(distance<=reach)return (c.X,c.Z);
        return (source.X-dx/distance*reach,source.Z-dz/distance*reach);
    }
    public static bool IsFood(RoomObjectKind k) => k is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Leaves;
    public static bool IsWater(RoomObjectKind k) => k is RoomObjectKind.Water or RoomObjectKind.RiverWater;
    public static bool IsResource(RoomObjectKind k) => IsFood(k)||IsWater(k);
    public static bool IsWild(RoomObjectKind k) => k is RoomObjectKind.Leaves or RoomObjectKind.RiverWater;
    public static bool WantsFood(Creature c)=>c.Fullness<(c.Interaction.Digestion.Symptomatic?55:70);
    public static bool WantsWater(Creature c)=>c.Interaction.Hydration<70;
    // All values are editable game balance, not claimed to be the reference game's numbers.
    public static double FoodValue(RoomObjectKind k) => k switch {RoomObjectKind.Leaves=>7,RoomObjectKind.Sweet=>9,RoomObjectKind.Meal=>19,_=>0};
    public static double WaterValue(RoomObjectKind k) => IsWater(k)?24:0;
    public static double IllnessChance(double contamination,double vulnerability) =>
        Math.Clamp(contamination*(.32+.18*vulnerability),0,.8);

    public static void AddWoodland(WorldState state,string regionId)
    {
        // Sources sit at dry approach points. River geometry is beyond the eastern bank.
        state.RoomObjects.Add(new(){Id=$"{regionId}-leaves-west",RegionId=regionId,Kind=RoomObjectKind.Leaves,X=-5.5,Z=-1.5,Portions=12,Contamination=.75});
        state.RoomObjects.Add(new(){Id=$"{regionId}-leaves-north",RegionId=regionId,Kind=RoomObjectKind.Leaves,X=.8,Z=-5.6,Portions=10,Contamination=.65});
        state.RoomObjects.Add(new(){Id=$"{regionId}-river-bank",RegionId=regionId,Kind=RoomObjectKind.RiverWater,X=WoodlandLayout.DrinkX,Z=WoodlandLayout.BankZ,Portions=1,Contamination=.95});
    }
}

public sealed partial class WorldSimulation
{
    public void SetInteractionHand(double x,double z,bool engaged)
    {
        _interactionHandLive=false;
        if(!State.InteractionPlaytest)return;
        if(engaged&&double.IsFinite(x)&&double.IsFinite(z))
        {
            _interactionHandLive=true;
            State.PlayerX=Math.Clamp(x,-7.7,7.7);State.PlayerZ=Math.Clamp(z,-7.7,7.7);
            State.InteractionHandPresent=true;State.InteractionHandUntil=State.WorldSeconds+3;
            State.InteractionHandRegionId=State.ActiveRegionId;
        }
        else if(State.WorldSeconds>=State.InteractionHandUntil||State.InteractionHandRegionId!=State.ActiveRegionId)State.InteractionHandPresent=false;
    }
    private bool SeesHelpingHand(Creature c) => State.InteractionHandPresent&&State.WorldSeconds<State.InteractionHandUntil&&
        c.RegionId==State.ActiveRegionId&&c.RegionId==State.InteractionHandRegionId&&
        Distance(c.X,c.Z,State.PlayerX,State.PlayerZ)<NaturalResources.SenseRange&&HasGroundSightLine(c,State.PlayerX,State.PlayerZ);

    private void ObserveNaturalResources(Creature c)
    {
        var life=c.Interaction;
        if(State.WorldSeconds<life.NextResourceScanAt)return;
        life.NextResourceScanAt=State.WorldSeconds+.8;
        foreach(var obj in State.RoomObjects)
        {
            if(obj.RegionId!=c.RegionId||!NaturalResources.IsResource(obj.Kind)||obj.Stored||obj.HeldByHand||obj.CarrierId!=null||
                Distance(c.X,c.Z,obj.X,obj.Z)>NaturalResources.SenseRange||!CanSeeResource(c,obj))continue;
            if(!life.Resources.TryGetValue(obj.Id,out var memory))
            {
                if(life.Resources.Count>=128)life.Resources.Remove(life.Resources.MinBy(p=>p.Value.SeenAt).Key);
                life.Resources[obj.Id]=memory=new();
            }
            if(memory.RegionId!=obj.RegionId||memory.X!=obj.X||memory.Z!=obj.Z)
            {
                if(life.ResourceTargetId==obj.Id)ResetNaturalResourceSearch(c);
                memory.AvoidUntil=0;
            }
            memory.RegionId=obj.RegionId;memory.Kind=obj.Kind;memory.X=obj.X;memory.Z=obj.Z;
            memory.Available=obj.Portions>0;memory.SeenAt=State.WorldSeconds;
        }
        // Only forget a missing/moved object when the remembered position is actually inspected.
        foreach(var (id,memory) in life.Resources)
            if(memory.RegionId==c.RegionId&&Distance(c.X,c.Z,memory.X,memory.Z)<1&&HasGroundSightLine(c,memory.X,memory.Z)&&
                !State.RoomObjects.Any(o=>o.Id==id&&!o.Stored&&!o.HeldByHand&&o.CarrierId==null&&o.RegionId==c.RegionId&&o.Portions>0&&Distance(o.X,o.Z,memory.X,memory.Z)<.5))
                memory.Available=false;
    }

    private bool CanSeeResource(Creature c,RoomObject obj)
    {
        return c.Condition!=PhysicalCondition.Dead&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&
            HasGroundSightLine(c,obj.X,obj.Z);
    }
    private bool HasGroundSightLine(Creature c,double x,double z)
    {
        double dx=x-c.X,dz=z-c.Z,len=dx*dx+dz*dz;
        if(c.Interaction.Restraint==RestraintKind.Box)
        {
            double size=c.IsYoung?.73:c.IsElder?1.03:1;
            double innerX=(InteractionFixtureGeometry.BoxSideX-InteractionFixtureGeometry.BoxWallThickness*.5)*size;
            double back=(InteractionFixtureGeometry.BoxBackZ+InteractionFixtureGeometry.BoxBackThickness*.5)*size;
            double front=InteractionFixtureGeometry.BoxFrontZ*size;
            bool inside=Math.Abs(dx)<innerX&&dz>back&&dz<front;
            // The barred front allows observation farther than mouth reach. Rear
            // and side panels still hide locations beyond their solid surfaces.
            if(!inside&&(dz<=front||Math.Abs(dx*front/dz)>=innerX))return false;
        }
        bool Blocks(double centerX,double centerZ,double radius)
        {
            double t=len<.00001?0:Math.Clamp(((centerX-c.X)*dx+(centerZ-c.Z)*dz)/len,0,1);
            return t>.05&&t<.95&&Distance(c.X+t*dx,c.Z+t*dz,centerX,centerZ)<radius;
        }
        foreach(var other in State.Creatures)
        {
            if(other.Id==c.Id||other.RegionId!=c.RegionId||other.Interaction.Restraint!=RestraintKind.Box)continue;
            double size=other.IsYoung?.73:other.IsElder?1.03:1;
            if(Math.Abs(x-other.X)<InteractionFixtureGeometry.BoxSideX*size&&
                Math.Abs(z-other.Z)<InteractionFixtureGeometry.BoxDepth*.5*size)return false;
            if(Blocks(other.X,other.Z,InteractionFootprint(other)))return false;
        }
        // A conservative ground-level view, using the same leaning trunk envelope
        // as movement. Foliage overhead is not an opaque ground obstacle.
        if(WoodlandLayout.HasRiver(State,c.RegionId))
            foreach(var tree in WoodlandLayout.Trees)
                if(Blocks(tree.X,tree.Z,tree.TrunkRadius))return false;
        return true;
    }

    private bool TickNaturalNeeds(Creature c,double dt)
    {
        var life=c.Interaction;
        bool thirsty=NaturalResources.WantsWater(c),hungry=NaturalResources.WantsFood(c);
        if(life.EatingRemaining>0){c.Activity=life.EatingKind==IntakeKind.Water?"小口喝水":"慢慢嚼这一口";return true;}
        if(!thirsty&&!hungry){ResetNaturalResourceSearch(c);life.HelpWaitingUntil=0;return false;}
        if(life.NaturalBitePause>0){c.Activity="停一小会儿，再看看想吃喝什么";return true;}
        if(!thirsty&&hungry&&DigestiveFoodPause(c))
        {c.Activity="肚子不舒服，歇一会儿再吃";return true;}
        ObserveNaturalResources(c);
        bool hand=SeesHelpingHand(c);
        if(!hand)
        {
            life.HelpWaitingUntil=0;
            if(life.ReactionPose=="help-look")life.ReactionPoseRemaining=0;
        }
        double urgent=Math.Min(c.Fullness,life.Hydration);
        if(hand&&State.WorldSeconds-life.LastHelpDecisionAt>=24&&c.ReactionRemaining<=0)
        {
            life.LastHelpDecisionAt=State.WorldSeconds;
            double probability=Math.Clamp(.04+c.Trust*.008-c.Fear*.002+(urgent<12?.25:0)+(!CanSelfMove(c)?.25:0),.02,.95);
            if(Next()<probability)
            {
                life.LastHelpAt=State.WorldSeconds;life.HelpWaitingUntil=State.WorldSeconds+1.5;
                if(thirsty)life.LastThirstRequestAt=State.WorldSeconds;
                double dx=State.PlayerX-c.X,dz=State.PlayerZ-c.Z;
                if(dx*dx+dz*dz<.0001){dx=Math.Sin(life.Facing);dz=Math.Cos(life.Facing);}
                life.SetReactionDirection(dx,dz);
                Feedback(c,thirsty?"thirsty":"hungry",Expression.Calling,
                    "它看向正在附近活动的手，请求一点吃喝；等一会儿也会继续自己找。","help-look");
                // The brief appeal ends with its wait; a later walk must regain its gait.
                life.ReactionPoseRemaining=urgent<12?0:1.5;
            }
        }
        if(State.WorldSeconds<life.HelpWaitingUntil&&urgent>=12){c.Activity="看向手，等一小会儿";return true;}
        ResourceMemory? target=null;double best=double.NegativeInfinity;string? targetId=null;bool targetUrgent=false;
        foreach(var (id,m) in life.Resources)
        {
            if(m.RegionId!=c.RegionId||!m.Available||m.AvoidUntil>State.WorldSeconds)continue;
            bool water=NaturalResources.IsWater(m.Kind);if(water?!thirsty:!hungry||DigestiveFoodPause(c))continue;
            double need=water?100-life.Hydration:100-c.Fullness;
            bool urgentNeed=need>85;
            double distance=Distance(c.X,c.Z,m.X,m.Z);
            double score=need*.10-distance*.45-m.Suspicion*.025;
            // A remembered belief affects caution, never the source's real safety
            // or location. New experience can correct inherited misconceptions.
            if(c.Family.LifeKnowledge.TryGetValue("safe:"+m.Kind,out bool believedSafe))score+=believedSafe?.2:-.4;
            if(!NaturalResources.IsWild(m.Kind)&&hand&&c.Trust<40&&Distance(m.X,m.Z,State.PlayerX,State.PlayerZ)<1.8)
                score-=(40-c.Trust)*.15;
            if(id==life.ResourceTargetId)score+=.4;
            // A less urgent nearby snack must not displace available water (or
            // food) needed to interrupt rest. Keep normal scoring within a tier.
            if(target==null||urgentNeed&&!targetUrgent||urgentNeed==targetUrgent&&score>best)
            {best=score;target=m;targetId=id;targetUrgent=urgentNeed;}
        }
        if(target!=null&&targetId!=null)
        {
            bool changedTarget=life.ResourceTargetId!=targetId;
            if(changedTarget)
            {life.ResourceTargetId=targetId;life.ResourceBestDistance=Distance(c.X,c.Z,target.X,target.Z);life.ResourceStuckSeconds=0;c.TargetX=c.X;c.TargetZ=c.Z;}
            // The remembered location can guide a search, but looking up its ID
            // must not reveal a moved source outside current sight or this region.
            var obj=State.RoomObjects.Find(o=>o.Id==targetId&&o.RegionId==c.RegionId&&!o.Stored&&!o.HeldByHand&&o.CarrierId==null&&
                Distance(c.X,c.Z,o.X,o.Z)<=NaturalResources.SenseRange&&CanSeeResource(c,o));
            double distance=Distance(c.X,c.Z,target.X,target.Z);
            bool distrust=hand&&c.Trust<25&&urgent>=12&&!NaturalResources.IsWild(target.Kind)&&
                Distance(target.X,target.Z,State.PlayerX,State.PlayerZ)<1.8;
            if(distrust&&changedTarget)
            {
                // Keep the chosen goal during a brief cautious look. Repeatedly
                // discarding it would reject the only supply until near starvation.
                life.HelpWaitingUntil=State.WorldSeconds+1.5;
                c.Activity="先看一会儿手旁的吃喝";return true;
            }
            if(distance<=NaturalResources.Reach(c)&&obj!=null&&obj.RegionId==c.RegionId&&!obj.Stored&&!obj.HeldByHand&&obj.CarrierId==null&&obj.Portions>0&&
                Distance(c.X,c.Z,obj.X,obj.Z)<=NaturalResources.Reach(c)&&CanReachRoomFood(c,obj)&&NaturalResources.Consumer(State,obj)==null&&
                !life.Carried&&!life.Falling&&!life.Supine&&life.Height<=.25)
            {
                if(TryWaterDrinkingAccident(c,obj))return true;
                BeginNaturalIntake(c,obj);ResetNaturalResourceSearch(c);return true;
            }
            if(CanSelfMove(c))
            {
                var approach=NaturalResources.Approach(c,target);
                bool adjusting=false;
                if(obj!=null&&!obj.Stored&&!obj.HeldByHand&&obj.CarrierId==null&&distance<=NaturalResources.Reach(c)&&NaturalResources.Consumer(State,obj)==null)
                {
                    // Keep a chosen nearby standing point while approaching it. This
                    // uses remembered world targets, without teleporting or extra save state.
                    if(Distance(c.TargetX,c.TargetZ,obj.X,obj.Z)<=NaturalResources.Reach(c)&&
                        Distance(c.X,c.Z,c.TargetX,c.TargetZ)>.02&&CanOccupyRoom(c,c.TargetX,c.TargetZ)&&CanReachRoomFood(c,obj,c.TargetX,c.TargetZ))
                    {approach=(c.TargetX,c.TargetZ);adjusting=true;}
                    else if(TryResourceStandingPoint(c,obj,out var alternative))
                    {approach=alternative;c.TargetX=approach.X;c.TargetZ=approach.Z;adjusting=true;}
                }
                double beforeX=c.X,beforeZ=c.Z;
                RoomMove(c,approach.X,approach.Z,dt);
                double remaining=Distance(c.X,c.Z,target.X,target.Z);
                bool approachedStandingPoint=adjusting&&Distance(c.X,c.Z,approach.X,approach.Z)<Distance(beforeX,beforeZ,approach.X,approach.Z)-.001;
                if(remaining<life.ResourceBestDistance-.04||approachedStandingPoint){life.ResourceBestDistance=remaining;life.ResourceStuckSeconds=0;}
                else life.ResourceStuckSeconds+=dt;
                c.Activity=adjusting?"换个站位，试着够到嘴边":NaturalResources.IsWater(target.Kind)?"去找水水":"去找能吃的东西";
                if(life.ResourceStuckSeconds>=3)
                {target.AvoidUntil=State.WorldSeconds+30;ResetNaturalResourceSearch(c);c.Activity="这边过不去，换个地方找";}
                return true;
            }
        }
        if(life.ResourceTargetId!=null)ResetNaturalResourceSearch(c);
        if(!CanSelfMove(c)){c.Activity="想找吃喝，但身体走不开";return false;}
        if(State.WorldSeconds>=life.ExploreUntil||Distance(c.X,c.Z,c.TargetX,c.TargetZ)<.3)
        {
            life.ExploreUntil=State.WorldSeconds+5;
            double angle=Next()*Math.Tau,edge=8-InteractionFootprint(c);
            c.TargetX=Math.Clamp(c.X+Math.Cos(angle)*3.2,-edge,edge);
            c.TargetZ=Math.Clamp(c.Z+Math.Sin(angle)*3.2,-edge,edge);
        }
        RoomMove(c,c.TargetX,c.TargetZ,dt);c.Activity=thirsty?"四处找水水":"四处找能吃的叶子";return true;
    }

    private static void ResetNaturalResourceSearch(Creature c)
    {
        var life=c.Interaction;
        life.ResourceTargetId=null;life.ResourceBestDistance=100;life.ResourceStuckSeconds=0;
        life.ExploreUntil=0;
    }

    private void ResetInterruptedAutonomySearch(Creature c)
    {
        var life=c.Interaction;
        if(!life.Carried&&!life.Falling&&!life.Supine&&life.Restraint==RestraintKind.None&&
            c.Condition is not (PhysicalCondition.Critical or PhysicalCondition.Dead)&&life.ActiveCue==null&&
            !(InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy))return;
        // Also used by presentation: carrying in close-up can relocate the body
        // without advancing world time. Neither resource nor cushion approach
        // progress may survive a body or lesson interruption.
        ResetNaturalResourceSearch(c);
        ResetInteractionRestSearch(c);
        life.HelpWaitingUntil=0;
        if(life.ReactionPose=="help-look")life.ReactionPoseRemaining=0;
    }

    private bool TryResourceStandingPoint(Creature c,RoomObject source,out (double X,double Z) point)
    {
        point=default;
        if(source.Kind==RoomObjectKind.RiverWater||!NaturalIntakeGeometry.ClearsBodySupport(c,source))return false;
        double best=double.PositiveInfinity,reach=NaturalResources.Reach(c);
        for(int ring=0;ring<3;ring++)for(int angle=0;angle<12;angle++)
        {
            double radius=reach*(ring==0?.99:ring==1?.65:.4);
            double x=source.X+Math.Sin(angle*Math.Tau/12)*radius,z=source.Z+Math.Cos(angle*Math.Tau/12)*radius;
            double travel=Distance(c.X,c.Z,x,z);
            if(travel<.02||travel>=best||!CanOccupyRoom(c,x,z)||!CanReachRoomFood(c,source,x,z))continue;
            best=travel;point=(x,z);
        }
        return double.IsFinite(best);
    }

    private void BeginNaturalIntake(Creature c,RoomObject obj)
    {
        var life=c.Interaction;bool water=NaturalResources.IsWater(obj.Kind);
        if(obj.Kind!=RoomObjectKind.RiverWater)obj.Portions--;
        life.BeginIntake(water?IntakeKind.Water:obj.Kind==RoomObjectKind.Sweet?IntakeKind.Sweet:IntakeKind.Meal);
        life.NaturalIntakePending=true;
        life.GroundIntake=true;life.IntakeFacing=Math.Atan2(obj.X-c.X,obj.Z-c.Z);
        life.GroundIntakeKind=obj.Kind;life.GroundIntakeSourceId=obj.Id;life.GroundIntakeDistance=Distance(c.X,c.Z,obj.X,obj.Z);life.GroundIntakeSourceHeight=obj.Height;
        c.Activity=water?"低头准备喝一口":"低头准备取一口";
        Feedback(c,water?"prepare-natural-water":"prepare-natural-food",Expression.Curious,
            water?"它靠近水，低下头准备喝一口。":"它靠近食物，低下头准备取一口。",water?"sip":"eat");
    }
    private void CompleteNaturalIntake(Creature c)
    {
        var life=c.Interaction;
        var obj=State.RoomObjects.Find(o=>o.Id==life.GroundIntakeSourceId&&!o.Stored&&!o.HeldByHand&&o.CarrierId==null&&o.RegionId==c.RegionId);
        if(obj==null||obj.Kind!=life.GroundIntakeKind||!CanReachRoomFood(c,obj)||
            Distance(c.X,c.Z,obj.X,obj.Z)>NaturalResources.Reach(c)||Math.Abs(obj.Height-life.GroundIntakeSourceHeight)>.005)
        {DetachNaturalIntake(c);return;}
        life.NaturalIntakePending=false;
        bool water=NaturalResources.IsWater(obj.Kind);
        c.Fullness=Clamp100(c.Fullness+NaturalResources.FoodValue(obj.Kind));
        life.Hydration=Clamp100(life.Hydration+NaturalResources.WaterValue(obj.Kind));
        if(!life.Digestion.Symptomatic)LearnFoodExperience(c,obj.Kind,true);
        if(!water&&life.Digestion.Symptomatic)life.Digestion.NextFoodAt=State.WorldSeconds+6;
        c.Activity=water?obj.Kind==RoomObjectKind.RiverWater?"在岸边小口喝水":"低头喝杯里的水":obj.Kind==RoomObjectKind.Leaves?"慢慢嚼叶子":"慢慢吃饭";
        Feedback(c,water?"drink":obj.Kind==RoomObjectKind.Leaves?"eat-leaves":obj.Kind==RoomObjectKind.Sweet?"sweet":"eat",
            Expression.Content,water?"它自己找到了水，小口喝着。":obj.Kind==RoomObjectKind.Leaves?
                "它找到一簇能吃的叶子，慢慢嚼起来。":"食物在嘴边，它自己吃到了这一口。",water?"sip":"eat");
        RegisterNaturalExposure(c,obj);
    }
    private void DetachNaturalIntake(Creature c)
    {
        var life=c.Interaction;
        if(life.NaturalIntakePending)
        {
            var source=State.RoomObjects.Find(o=>o.Id==life.GroundIntakeSourceId);
            if(source!=null&&source.Kind!=RoomObjectKind.RiverWater)source.Portions++;
            life.NaturalIntakePending=false;life.EatingRemaining=0;life.GroundIntake=false;
            if(life.ReactionSituation is "prepare-natural-food" or "prepare-natural-water")
            {life.ReactionPose="idle";life.ReactionPoseRemaining=0;c.Speech="";c.ReactionRemaining=0;}
        }
        life.GroundIntakeSourceId=null;
    }
    private void PruneConsumedSupplies()=>State.RoomObjects.RemoveAll(o=>o.Portions<=0&&!o.HeldByHand&&
        !NaturalResources.IsWild(o.Kind)&&NaturalResources.Consumer(State,o)==null&&
        !State.Creatures.Any(c=>c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeSourceId==o.Id));

    private void RegisterNaturalExposure(Creature c,RoomObject obj)
    {
        var health=c.Interaction.Digestion;health.Exposures++;
        RememberRecentIntake(c,obj.Id,obj.Kind);
        double vulnerability=(100-c.Health)/100+(c.IsYoung?.3:0);
        if(obj.Contamination<=0||Next()>=NaturalResources.IllnessChance(obj.Contamination,vulnerability))return;
        double severity=20+obj.Contamination*30;
        if(health.Severity<=0)
        {
            health.IncubationRemaining=18+Next()*15;health.Severity=severity;
            health.Episodes++;
        }
        else health.Severity=Math.Min(85,health.Severity+severity*.15);
        // Repeated exposures increase burden but cannot restart an already running incubation.
    }

    private bool DigestiveFoodPause(Creature c)=>c.Interaction.Digestion.Symptomatic&&c.Fullness>=12&&
        State.WorldSeconds<c.Interaction.Digestion.NextFoodAt;

    private void RememberRecentIntake(Creature c,string? sourceId,RoomObjectKind? kind=null)
    {
        var recent=c.Interaction.Digestion.RecentIntakes;
        recent.RemoveAll(intake=>State.WorldSeconds-intake.At>60);
        recent.Add(new(){SourceId=sourceId,Kind=kind,At=State.WorldSeconds});
        if(recent.Count>12)recent.RemoveRange(0,recent.Count-12);
    }

    private void SuspectRecentIntakes(Creature c)
    {
        var health=c.Interaction.Digestion;
        var recent=health.RecentIntakes.Where(intake=>State.WorldSeconds-intake.At is >=0 and <=60).ToArray();
        health.SuspectedSourceId=null;
        if(recent.Length==0)return;
        // Temporal association only: all recently eaten sources share suspicion.
        // Hidden contamination or the successful infection roll is never read here.
        // A hand-fed meal also shares uncertainty, without inventing a world source.
        foreach(var group in recent.Where(intake=>intake.SourceId!=null).GroupBy(intake=>intake.SourceId!))
            if(c.Interaction.Resources.TryGetValue(group.Key,out var memory))
            {
                memory.Suspicion=Math.Min(100,memory.Suspicion+25.0*group.Count()/recent.Length);
                LearnFoodExperience(c,memory.Kind,false);
            }
        // Preserve the eaten kind even after the last portion or its map memory
        // disappears. The link is still only a suspicion based on recent meals.
        foreach(var kind in recent.Where(intake=>intake.Kind.HasValue).Select(intake=>intake.Kind!.Value).Distinct())
            LearnFoodExperience(c,kind,false);
        if(recent[0].SourceId is { } onlySource&&recent.All(intake=>intake.SourceId==onlySource))
            health.SuspectedSourceId=onlySource;
    }

    private void TickDigestiveHealth(Creature c,double dt)
    {
        var health=c.Interaction.Digestion;
        if(health.Severity<=0)return;
        if(health.IncubationRemaining>0)
        {
            health.IncubationRemaining=Math.Max(0,health.IncubationRemaining-dt);
            if(health.IncubationRemaining<=0)
            {
                health.SymptomsRemaining=100+health.Severity*2;
                SuspectRecentIntakes(c);
                Feedback(c,"stomach-unwell",Expression.Worried,"它停下来缩了缩身体，吃过东西后肚子不太舒服。","stomach");
                RecordInteraction(c,"stomach-unwell","吃喝后过了一阵，肚子不舒服；还不能确定具体原因。",1);
            }
            return;
        }
        if(health.SymptomsRemaining<=0)return;
        double support=c.Fullness>25&&c.Interaction.Hydration>25?1:.25;
        double recovery=support*(c.Interaction.Resting?1.4:1)*(health.CareRemaining>0?1.5:1);
        health.SymptomsRemaining=Math.Max(0,health.SymptomsRemaining-dt*recovery);
        health.CareRemaining=Math.Max(0,health.CareRemaining-dt);
        c.Energy=Math.Max(0,c.Energy-dt*health.Severity*.0015);
        c.Interaction.Hydration=Math.Max(0,c.Interaction.Hydration-dt*health.Severity*.0005);
        if(c.Health>20)Hurt(c,Math.Min(c.Health-20,dt*health.Severity*.0008),"吃喝后的身体不适",null);
        if(health.SymptomsRemaining<=0)
        {
            health.Severity=0;health.CareRemaining=0;health.SuspectedSourceId=null;health.NextFoodAt=0;
            Feedback(c,"stomach-ease",Expression.Content,"这阵不适慢慢缓下来了，体力还需要恢复。","rest");
        }
    }
}

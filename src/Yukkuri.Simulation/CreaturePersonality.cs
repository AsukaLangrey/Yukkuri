namespace Yukkuri.Simulation;

public sealed class PersonalityTraits
{
    public bool Initialized { get; set; }
    public double Kindness { get; set; } = .5;
    public double Carelessness { get; set; } = .25;
    public double Pride { get; set; } = .3;
    public double NextHungerConflictAt { get; set; }
    public string? HungerTargetId { get; set; }
    public double NextHungerStrikeAt { get; set; }
    public double RemainsNutrition { get; set; } = 24;
    public string? WaterConcernId { get; set; }
    public double NextWaterResponseAt { get; set; }
    public double NextWaterAccidentAt { get; set; }
    public double LastRescueWitnessAt { get; set; } = -100;
}

public sealed partial class Creature
{
    public PersonalityTraits Personality { get; set; } = new();
}

public sealed partial class WorldSimulation
{
    private static void InitializePersonality(Creature c)
    {
        var p=c.Personality;
        if(p.Initialized)return;
        p.Kindness=c.TrueDisposition switch {"shy"=>.8,"greedy"=>.22,"cruel"=>.05,"proud"=>.22,_=>.6};
        p.Carelessness=c.TrueDisposition switch {"shy"=>.12,"greedy"=>.58,"careless"=>.85,"proud"=>.5,_=>.32};
        p.Pride=c.TrueDisposition switch {"greedy"=>.8,"proud"=>.85,"cruel"=>.95,"shy"=>.15,_=>.4};
        p.Initialized=true;
    }

    private bool CanWitnessWater(Creature observer,Creature target) => observer!=target&&
        observer.Condition==PhysicalCondition.Active&&!observer.Family.ColdStorage.Stored&&
        !observer.Family.Memorialized&&observer.RegionId==target.RegionId&&
        !WaterEnvironment.IsInWater(State,observer)&&FamilyVisible(observer,target,5);

    private static bool CanRespondToWater(Creature observer) => CanSelfMove(observer)&&
        observer.Interaction.EatingRemaining<=0&&!FamilyRules.UnderIntervention(observer);

    // Presentation reads the same visibility and bodily eligibility as the
    // behaviour. A saved concern id alone is not evidence of a current crisis.
    public Creature? WaterConcernTarget(Creature observer)
    {
        if(!State.Creatures.Contains(observer)||!CanRespondToWater(observer))return null;
        var target=State.Creatures.Find(c=>c.Id==observer.Personality.WaterConcernId);
        return target!=null&&target.Condition!=PhysicalCondition.Dead&&
            WaterEnvironment.IsInWater(State,target)&&CanWitnessWater(observer,target)?target:null;
    }

    private bool TryWaterDrinkingAccident(Creature c,RoomObject source)
    {
        var p=c.Personality;
        if(source.Kind!=RoomObjectKind.RiverWater||!State.Settings.Accidents||
            State.WorldSeconds<p.NextWaterAccidentAt||!CanSelfMove(c))return false;
        p.NextWaterAccidentAt=State.WorldSeconds+45;
        if(Next()>=p.Carelessness*.12)return false;
        double x=WoodlandLayout.RiverEdge(c.Z)+.12;
        if(!CanOccupyRoom(c,x,c.Z,allowWater:true))return false;
        c.X=x;c.TargetX=x;c.TargetZ=c.Z;
        EnterWater(c);c.LastReaction="喝水时探得太远，脚下一滑落进了水里。";
        return true;
    }

    private bool TryRespondToWater(Creature c,double dt)
    {
        var p=c.Personality;
        if(!CanRespondToWater(c)){p.WaterConcernId=null;return false;}
        var target=State.Creatures.Where(o=>o.Condition!=PhysicalCondition.Dead&&
            WaterEnvironment.IsInWater(State,o)&&CanWitnessWater(c,o)).OrderBy(o=>Distance(c.X,c.Z,o.X,o.Z)).FirstOrDefault();
        if(target==null){p.WaterConcernId=null;return false;}
        p.WaterConcernId=target.Id;
        if(p.Kindness>=.5)
        {
            double helperZ=Math.Clamp(target.Z+(c.Z>=target.Z?1:-1),-6.7,6.7);
            double bankX=WoodlandLayout.RiverEdge(helperZ)-InteractionFootprint(c)-.12;
            if(c.Fear<75&&c.Energy>20)RoomMove(c,bankX,helperZ,dt);
            c.Activity=$"着急地看着{target.Name}，寻找救援办法";
            State.HelpTargetId=target.Id;
            if(State.WorldSeconds<p.NextWaterResponseAt)return true;
            p.NextWaterResponseAt=State.WorldSeconds+8;
            c.Fear=Clamp100(c.Fear+2);RememberThreat(c,2);
            bool close=Distance(c.X,c.Z,target.X,target.Z)<1.5;
            if(close&&target.Interaction.Restraint==RestraintKind.None&&WaterEnvironment.CanStruggle(target)&&
                target.Energy>12&&target.X-WoodlandLayout.RiverEdge(target.Z)<.28&&
                TryWaterBankSpot(target,out double x,out double z,.7)&&Next()<.35)
            {
                target.X=x;target.Z=z;target.TargetX=x;target.TargetZ=z;LeaveWater(target,false);
                var bond=FamilyBond(target,c);bond.Affection=Clamp100(bond.Affection+8);
                bond.Familiarity=Clamp100(bond.Familiarity+4);bond.LastEvent="在岸边帮助自己脱离了水域";
                RecordInteraction(target,"peer-water-rescue",$"在{c.Name}的岸边接应下挣扎脱险。",5);
                Feedback(c,"recovery",Expression.Worried,$"它在岸边接应了{target.Name}，还在查看对方情况。","water-help");
            }
            else
                React(c,Expression.Calling,"人类！快帮帮它！",close?"在岸边伸展身体试着接应，但不能保证够到。":"急着靠近岸边，并向人类呼救。",6);
            return true;
        }
        if(p.Kindness<.25&&State.WorldSeconds>=p.NextWaterResponseAt)
        {
            p.NextWaterResponseAt=State.WorldSeconds+12;
            c.Activity="在岸边嘲弄落水的同伴";
            React(c,Expression.Defiant,"笨蛋！自己跳回来呀！","留在安全的岸边嘲弄，没有伸出援手。",5);
            var bond=FamilyBond(target,c);bond.Resentment=Clamp100(bond.Resentment+4);
            bond.LastEvent="落水时被对方嘲弄而没有得到帮助";
            target.Fear=Clamp100(target.Fear+2);RememberThreat(target,2);
            return true;
        }
        return false;
    }

    private void AcknowledgeHumanWaterRescue(Creature rescued)
    {
        InitializePersonality(rescued);
        // Pulling it out of danger you just caused cannot repeatedly farm trust.
        bool causedDanger=rescued.Interaction.Water.LastPlayerSubmersionAt>=0&&
            State.WorldSeconds-rescued.Interaction.Water.LastPlayerSubmersionAt<120;
        bool grateful=rescued.Personality.Kindness>=.25||rescued.Personality.Pride<.75;
        if(!causedDanger)
        {
            rescued.Trust=Clamp100(rescued.Trust+(grateful?5:1));
            rescued.Attachment=Clamp100(rescued.Attachment+(grateful?3:.5));
        }
        rescued.Family.LastEvent=causedDanger?"被人类救上岸，但仍记得刚才也是人类把自己弄进水里":
            grateful?"记得人类把自己从水里救回来":"认为人类救助自己理所当然";
        foreach(var observer in State.Creatures.Where(o=>CanWitnessWater(o,rescued)))
        {
            InitializePersonality(observer);
            if(observer.Personality.Kindness<.25||causedDanger||
                State.WorldSeconds-observer.Personality.LastRescueWitnessAt<30)continue;
            observer.Personality.LastRescueWitnessAt=State.WorldSeconds;
            observer.Trust=Clamp100(observer.Trust+2);
            observer.Interaction.HumanBond.Safety=Clamp100(observer.Interaction.HumanBond.Safety+2);
            observer.Family.LastEvent=$"看见人类救回{rescued.Name}，更愿意向人类求助";
            RecordInteraction(observer,"witness-water-rescue",observer.Family.LastEvent,2);
            React(observer,Expression.Worried,"谢谢你救它！","看见同伴得救，仍担心它的身体。",5);
        }
    }

    private void StopHungerAggression(Creature c,string reason)
    {
        c.Personality.HungerTargetId=null;
        c.Personality.NextHungerConflictAt=Math.Max(c.Personality.NextHungerConflictAt,State.WorldSeconds+30);
        c.TargetX=c.X;c.TargetZ=c.Z;c.Family.LastEvent=reason;
    }

    // Contact changes during close-up must cancel the intent even while world time is frozen.
    private bool CanHungerAggress(Creature c) => c.Fullness<12&&c.Personality.Kindness<.25&&CanSelfMove(c)&&
        !WaterEnvironment.IsInWater(State,c)&&c.Energy>=10&&c.Interaction.EatingRemaining<=0&&c.Fear<65&&c.Pain<40&&
        c.Family.FlightRemaining<=0&&!c.Interaction.Physiology.Active&&!FamilyRules.UnderIntervention(c)&&
        InteractionGestureTarget!=c.Id&&c.Family.Conflict.TargetId==null&&c.Family.ChildThreat.TargetId==null;

    private bool CanPursueHungerTarget(Creature c,Creature target) => c!=target&&CanHungerAggress(c)&&
        !target.Family.ColdStorage.Stored&&!target.Family.Memorialized&&!target.Interaction.Carried&&
        (target.Condition!=PhysicalCondition.Dead||target.Personality.RemainsNutrition>0)&&
        FamilyVisible(c,target,4)&&!WaterEnvironment.IsInWater(State,target);

    private void SynchronizeHungerThreat(Creature c)
    {
        if(c.Personality.HungerTargetId==null)return;
        var target=State.Creatures.Find(o=>o.Id==c.Personality.HungerTargetId);
        if(target==null||!CanPursueHungerTarget(c,target))
            StopHungerAggression(c,"饥饿袭击被隔开或身体状态打断，已有伤势与记忆仍保留");
    }

    private bool TryHungerAggression(Creature c,double dt)
    {
        var p=c.Personality;
        if(!CanHungerAggress(c))
        {p.HungerTargetId=null;return false;}
        if(p.HungerTargetId==null)
        {
            if(State.WorldSeconds<p.NextHungerConflictAt)return false;
            p.NextHungerConflictAt=State.WorldSeconds+10;
            // Edible, reachable ordinary food wins; malice does not invent starvation.
            if(State.RoomObjects.Any(o=>!o.Stored&&o.RegionId==c.RegionId&&o.Portions>0&&
                NaturalResources.IsFood(o.Kind)&&Distance(c.X,c.Z,o.X,o.Z)<3))return false;
            if(Next()>=.12+c.Aggression*.3)return false;
            var victim=State.Creatures.Where(o=>o!=c&&o.RegionId==c.RegionId&&!o.Family.ColdStorage.Stored&&
                !o.Family.Memorialized&&!o.Interaction.Carried&&!WaterEnvironment.IsInWater(State,o)&&
                o.Personality.RemainsNutrition>0&&FamilyVisible(c,o,3.5)).OrderBy(o=>o.Health).FirstOrDefault();
            if(victim==null)return false;
            p.HungerTargetId=victim.Id;p.NextHungerStrikeAt=State.WorldSeconds+2;
            React(c,Expression.Angry,"饿……吃的……",$"饥饿中的恶劣性格让它盯上了{victim.Name}；现在仍可隔开或喂食。",4);
        }
        var target=State.Creatures.Find(o=>o.Id==p.HungerTargetId);
        if(target==null||!CanPursueHungerTarget(c,target))
        {p.HungerTargetId=null;return false;}
        var protector=target.Condition==PhysicalCondition.Dead?null:State.Creatures
            .Where(o=>o!=c&&o!=target&&o.Personality.Kindness>=.65&&o.Courage>=.55&&
                CanProtectFamily(o)&&FamilyInContact(o,target)&&FamilyVisible(o,c,2.5))
            .OrderBy(o=>o.Id,StringComparer.Ordinal).FirstOrDefault();
        if(protector!=null)
        {
            StopHungerAggression(c,$"{protector.Name}护住了{target.Name}，饥饿袭击被隔开");
            var safety=FamilyBond(target,protector);
            safety.Affection=Clamp100(safety.Affection+4);safety.LastEvent="在危险靠近时护住了自己";
            protector.Family.LastEvent=$"护住{target.Name}，仍需要人类处理饥饿与安全距离";
            React(protector,Expression.Angry,"不许伤害它！","守在同伴身边，阻止了这次靠近。",5);
            return true;
        }
        c.Activity=$"饥饿地逼近{target.Name}";
        if(!FamilyInContact(c,target)){RoomMove(c,target.X,target.Z,dt);return true;}
        if(State.WorldSeconds<p.NextHungerStrikeAt)return true;
        p.NextHungerStrikeAt=State.WorldSeconds+3;
        if(target.Condition==PhysicalCondition.Dead)
        {
            double taken=Math.Min(6,target.Personality.RemainsNutrition);
            if(taken<=0){p.HungerTargetId=null;return false;}
            target.Personality.RemainsNutrition-=taken;c.Fullness=Clamp100(c.Fullness+taken);
            c.Activity="极饿时啃食同伴的残躯";
            RecordInteraction(c,"starvation-cannibalism","极度饥饿时啃食同伴，周围个体会记住这件事。",6);
            Emit(IncidentKind.Attack,target,c.Id,$"{c.Name}啃食了同伴的残躯",8);
            return true;
        }
        InflictPeerAttack(c,target,4+c.Aggression*5,8,"饥饿袭击",
            $"{c.Name}因极度饥饿袭击了{target.Name}","遭到饥饿中的同伴袭击，需要离开危险。",12);
        var bond=FamilyBond(target,c);bond.Fear=Clamp100(bond.Fear+15);bond.Resentment=Clamp100(bond.Resentment+15);
        bond.LastHarmAt=State.WorldSeconds;bond.LastEvent="遭到饥饿中的同伴袭击";
        State.HelpTargetId=target.Id;
        if(target.Condition!=PhysicalCondition.Dead)
        {
            React(target,Expression.Calling,"不要！救救我！","遭到饥饿同伴袭击，试图退开并寻求人类保护。",5);
            if(CanSelfMove(target))
            {
                double dx=target.X-c.X,dz=target.Z-c.Z;
                if(Math.Abs(dx)+Math.Abs(dz)<.001)dx=1;
                double distance=Math.Sqrt(dx*dx+dz*dz);
                target.Family.FlightX=Math.Clamp(target.X+dx/distance*3,-7,7);
                target.Family.FlightZ=Math.Clamp(target.Z+dz/distance*3,-7,7);
                target.Family.FlightRemaining=5;
            }
        }
        return true;
    }
}

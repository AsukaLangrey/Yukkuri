namespace Yukkuri.Simulation;

public sealed class WaterExposure
{
    public string? EventId { get; set; }
    public double EnteredAt { get; set; } = -100;
    public bool PlayerCaused { get; set; }
    public bool HumanRescueCredited { get; set; }
    public bool InWater { get; set; }
    public double ExposureSeconds { get; set; }
    public double Struggle { get; set; }
    public double RecoverySeconds { get; set; }
    public double NextAttemptAt { get; set; }
    public double LastRescueAt { get; set; } = -100;
    public double LastPlayerSubmersionAt { get; set; } = -100;
}

public sealed partial class InteractionLife
{
    public WaterExposure Water { get; set; } = new();
}

public sealed partial class WorldState
{
    public int WaterRescueTools { get; set; } = 1;
    public int WaterRescuePurchases { get; set; }
}

public static class WaterEnvironment
{
    public const int RescueToolPrice = 18;
    public static bool IsInWater(WorldState state,Creature c) =>
        !c.Family.ColdStorage.Stored&&!c.Interaction.Carried&&c.Interaction.Height<=.1&&
        WoodlandLayout.HasRiver(state,c.RegionId)&&c.X>WoodlandLayout.RiverEdge(c.Z);
    public static bool CanStruggle(Creature c) => c.Condition==PhysicalCondition.Active&&
        c.Interaction.Restraint==RestraintKind.None&&!c.Interaction.Supine&&!c.Interaction.Carried&&
        (!WorldSimulation.UsesGroundFoot(c)||c.Interaction.FootIntegrity>25)&&c.Energy>12;
}

public sealed partial class WorldSimulation
{
    public bool BuyWaterRescueTool()
    {
        if(!State.InteractionPlaytest||State.WaterRescueTools>=99||
            !State.UnlimitedResources&&State.Money<WaterEnvironment.RescueToolPrice)return false;
        if(!State.UnlimitedResources)State.Money-=WaterEnvironment.RescueToolPrice;
        State.WaterRescueTools++;State.WaterRescuePurchases++;
        State.LastActionMessage="购入一份水域救援用品，可救起一只落水的油。";
        return true;
    }

    public InteractionFeedback RescueFromWater(string id)
    {
        var c=InteractionTarget(id);
        if(State.Settings.Pause)return Unavailable("暂停中，救助等待继续。");
        if(c==null||c.Condition==PhysicalCondition.Dead)return Unavailable("当前没有可以救助的落水油。");
        if(!WaterEnvironment.IsInWater(State,c))return Unavailable("它现在没有落水。");
        if(State.WaterRescueTools<=0)return Unavailable("需要先购买水域救援用品。");
        if(c.Interaction.Restraint!=RestraintKind.None)return Unavailable("先解除固定装置，再用救援用品将它带回岸上。");
        if(!TryWaterBankSpot(c,out double x,out double z))return Unavailable("附近岸边被占着，需要先腾出安全落点。");
        // Capture actual witnesses before changing the target's location.
        // Seeing a wet survivor later does not reveal who rescued it.
        EnterWater(c);
        var witnesses=State.Creatures.Where(o=>CanWitnessWater(o,c)).ToArray();
        State.WaterRescueTools--;
        c.X=x;c.Z=z;c.TargetX=x;c.TargetZ=z;
        c.Interaction.VelocityX=0;c.Interaction.VelocityZ=0;
        LeaveWater(c,true);
        AcknowledgeHumanWaterRescue(c,witnesses);
        bool causedDanger=c.Interaction.Water.PlayerCaused||RecentlyHarmedByHuman(c);
        RecordInteraction(c,"water-rescue","用救援用品被带回岸上，仍然湿透、惊魂未定。",5);
        var result=Feedback(c,"recovery",Expression.Worried,"已经救回岸上；它还在发抖，需要擦干并检查伤势。","water-recover");
        if(c.Condition!=PhysicalCondition.Critical)
        {
            c.Speech=MouthFunction.FormatSpeech(c,causedDanger?"别再把我丢进去了……":c.Personality.Kindness<.25&&c.Personality.Pride>=.75?
                "救本大爷是你的荣幸！":"谢、谢谢……还以为回不来了……");
            result=result with {Speech=c.Speech};
        }
        return result;
    }

    private bool TryWaterBankSpot(Creature c,out double x,out double z,double maxOffset=2.1)
    {
        double own=InteractionFootprint(c);
        foreach(double offset in new[]{0.0,.7,-.7,1.4,-1.4,2.1,-2.1})
        {
            if(Math.Abs(offset)>maxOffset)continue;
            z=Math.Clamp(c.Z+offset,-7+own,7-own);
            x=WoodlandLayout.RiverEdge(z)-own-.18;
            if(CanOccupyRoom(c,x,z))return true;
        }
        x=c.X;z=c.Z;return false;
    }

    private void EnterWater(Creature c,bool causedByPlayer=false)
    {
        var i=c.Interaction;var w=i.Water;
        if(w.InWater)return;
        w.InWater=true;w.ExposureSeconds=0;w.NextAttemptAt=State.WorldSeconds+1;
        w.EventId=NewFamilyId("water");w.EnteredAt=State.WorldSeconds;
        w.PlayerCaused=causedByPlayer;w.HumanRescueCredited=false;
        ClearWaterAftercare(c);
        foreach(var observer in State.Creatures)
        {
            if(observer.Personality.WaterConcernId==c.Id)
            {observer.Personality.WaterConcernId=null;observer.Personality.WaterConcernEventId=null;}
            if(observer.Personality.WaterAftercare.TargetId==c.Id)ClearWaterAftercare(observer);
        }
        w.Struggle=Math.Max(5,c.Energy);w.RecoverySeconds=0;
        i.Falling=false;i.Height=0;i.VerticalVelocity=0;i.SoakSurface();
        CancelWildSelfCare(c,"落水打断了自行护伤，需要先脱离水域。");
        DetachNaturalIntake(c);CancelFamilyCare(c,reason:"落水中断了照护，先救援。");
        if(c.Family.CompanionTargetId!=null)ClearCompanionship(c);
        if(FamilyRules.UnderIntervention(c))CancelIntervention(c,"落水中断了繁育干预；已用材料与后果保留。");
        c.Personality.HungerTargetId=null;c.Personality.WaterConcernId=null;c.Personality.WaterConcernEventId=null;
        if(!c.Personality.HungerFeeding.Committed)c.Personality.HungerFeeding=new();
        EndHumanCompany(c);i.RestRemaining=0;i.Resting=false;i.ActiveCue=null;i.FollowPlayer=false;
        RememberThreat(c,30);c.Fear=Clamp100(c.Fear+30);
        if(causedByPlayer)
        {
            w.LastPlayerSubmersionAt=State.WorldSeconds;
            Distress(c,12,cause:"落水");
        }
        State.HelpTargetId=c.Id;
        RecordInteraction(c,"water-entry","落进水里，挣扎着寻找岸边。",8);
        Feedback(c,"hurt",Expression.Frightened,"它落进水里，正在挣扎并尝试跳回岸上。","water-struggle");
    }

    private void LeaveWater(Creature c,bool rescued)
    {
        var w=c.Interaction.Water;
        w.InWater=false;w.Struggle=0;
        w.RecoverySeconds=Math.Max(w.RecoverySeconds,30+Math.Min(90,w.ExposureSeconds));
        if(rescued)w.LastRescueAt=State.WorldSeconds;
        BeginWaterAftercare(c);
        c.Interaction.NextAutonomyAt=Math.Max(c.Interaction.NextAutonomyAt,State.WorldSeconds+8);
        RefreshHelpAfterResolution(c);
    }

    // Support changes can happen in close-up while ordinary time is frozen.
    // Leaving water settles once; it never advances exposure or grants rescue credit.
    private void SynchronizeWaterSupport(Creature c)
    {
        if(c.Interaction.Water.InWater&&!WaterEnvironment.IsInWater(State,c))LeaveWater(c,false);
    }

    // Called on the simulation clock, never on render frames: pause/save cannot skip exposure.
    private bool TickWaterExposure(Creature c,double dt)
    {
        var i=c.Interaction;var w=i.Water;
        bool immersed=WaterEnvironment.IsInWater(State,c);
        if(!immersed)
        {
            if(w.InWater)LeaveWater(c,false);
            if(!c.Family.ColdStorage.Stored)w.RecoverySeconds=Math.Max(0,w.RecoverySeconds-dt);
            return false;
        }
        EnterWater(c);
        w.ExposureSeconds=Math.Min(86400,w.ExposureSeconds+dt);
        c.Energy=Math.Max(0,c.Energy-dt*(c.Condition==PhysicalCondition.Active?.65:.2));
        w.Struggle=c.Condition==PhysicalCondition.Active?Math.Clamp(c.Energy*(1-c.Pain/150),0,100):0;
        i.SoakSurface();
        c.Fear=Clamp100(c.Fear+dt*.45);
        c.Activity=w.Struggle>12?"在水中挣扎，试着跳向岸边":"泡在水里，已经无力挣扎";
        c.Expression=w.Struggle>12?Expression.Frightened:Expression.Exhausted;
        i.ReactionPose="water-struggle";i.ReactionPoseRemaining=Math.Max(.4,i.ReactionPoseRemaining);
        // Soaking progressively harms the body; it uses the existing irreversible injury/death route.
        if(w.ExposureSeconds>8)Hurt(c,dt*(.25+Math.Min(2,(w.ExposureSeconds-8)*.035)),"长时间泡水",null);
        if(c.Condition==PhysicalCondition.Dead)return true;
        if(State.WorldSeconds>=w.NextAttemptAt)
        {
            w.NextAttemptAt=State.WorldSeconds+2.5;
            if(WaterEnvironment.CanStruggle(c)&&w.Struggle>12)
            {
                double depth=c.X-WoodlandLayout.RiverEdge(c.Z);
                if(depth<.5&&TryWaterBankSpot(c,out double x,out double z,.7)&&Next()<.35+c.Courage*.3)
                {
                    c.X=x;c.Z=z;c.TargetX=x;c.TargetZ=z;LeaveWater(c,false);
                    Feedback(c,"recovery",Expression.Worried,"它挣扎着跳回岸上，仍然湿透并受了惊。","water-recover");
                    return false;
                }
                // Each attempt can make real progress toward the bank, but exhaustion/disability matters.
                double xNext=c.X-Math.Min(.18,.04+c.Energy*.0014);
                if(CanOccupyRoom(c,xNext,c.Z,allowWater:true))c.X=xNext;
                c.TargetX=c.X;c.TargetZ=c.Z;
            }
        }
        return true;
    }
}

namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    public double CleaningLearning { get; set; }
    public double LastCleaningLessonAt { get; set; } = -100;
    public double NextCleaningAt { get; set; }
    public double RuleLearning { get; set; }
    public string? CleaningTargetId { get; set; }
    public double CleaningSeconds { get; set; }
    public double LastExaminationAt { get; set; } = -1;
    public string Examination { get; set; } = "";
    public bool PersistentPressure { get; set; }
}
public sealed partial class FamilyLife
{
    // Separate from the old global simulation switch and old arranged-breeding permission.
    public bool AutonomousBreedingPermitted { get; set; } = true;
    public bool HasGravestone { get; set; }
}
public sealed partial class WorldSimulation
{
    private string? _groundHandTarget;
    public void SetGroundHandTarget(string? id)
    {
        _groundHandTarget=id;
        if(id!=null&&InteractionTarget(id) is { } c){ClearCompanionship(c);CancelWildSelfCare(c,"被手贴地移动，暂停自行护理。");}
    }
    public string TeachCleaning(string id)
    {
        var c=InteractionTarget(id);var error=FamilyCommandError(c);if(error!=null)return error;
        if(!FamilyActionReady(c!)||c!.Fear>50||c.Fullness<30)return "它现在无法专心，先照顾身体并让它安稳下来。";
        if(State.WorldSeconds-c.Interaction.LastCleaningLessonAt<12)return "先让它消化刚才的示范。";
        var waste=State.RoomObjects.FirstOrDefault(o=>o.RegionId==c.RegionId&&!o.Stored&&IsWaste(o.Kind)&&Distance(c.X,c.Z,o.X,o.Z)<2);
        if(waste==null)return "先带它到需要清理的脏处，再示范实际打扫。";
        if(!CleanRoomWaste(waste.Id))return "这处已经清理过了。";
        c.Interaction.LastCleaningLessonAt=State.WorldSeconds;
        c.Interaction.CleaningLearning=Math.Min(100,c.Interaction.CleaningLearning+25);
        RecordInteraction(c,"cleaning-lesson","看见并参与了清理身旁脏处的示范。",1);
        return c.Interaction.CleaningLearning>=75?"已经学会自己打扫，会在身体安稳时主动清理。":"完成一次实际清扫示范，还需要练习。";
    }
    private void TickCourtyardLearning(double dt)
    {
        foreach(var c in State.Creatures)
        {
            var i=c.Interaction;
            if(!CanSelfMove(c)||i.CleaningLearning<75||c.Fullness<35||i.Hydration<30||c.Fear>50||c.Energy<25||
                InteractionGestureTarget==c.Id||c.Family.CompanionTargetId!=null||c.Family.CareTargetId!=null)
            {i.CleaningTargetId=null;i.CleaningSeconds=0;continue;}
            var waste=State.RoomObjects.Find(o=>o.Id==i.CleaningTargetId&&!o.Stored&&IsWaste(o.Kind)&&o.RegionId==c.RegionId);
            if(waste==null)
            {
                i.CleaningTargetId=null;i.CleaningSeconds=0;
                if(State.WorldSeconds<i.NextCleaningAt)continue;
                i.NextCleaningAt=State.WorldSeconds+10;
                waste=State.RoomObjects.Where(o=>!o.Stored&&IsWaste(o.Kind)&&o.RegionId==c.RegionId&&Distance(c.X,c.Z,o.X,o.Z)<4)
                    .OrderBy(o=>Distance(c.X,c.Z,o.X,o.Z)).FirstOrDefault();
                if(waste==null)continue;i.CleaningTargetId=waste.Id;
            }
            c.Activity="自己打扫卫生";
            double distance=Distance(c.X,c.Z,waste.X,waste.Z);
            if(distance>.75)
            {
                i.CleaningSeconds=0;double step=Math.Min(distance,dt*.65);
                SlideAlongGround(c.Id,c.X+(waste.X-c.X)/distance*step,c.Z+(waste.Z-c.Z)/distance*step,false);
                continue;
            }
            c.TargetX=c.X;c.TargetZ=c.Z;i.CleaningSeconds+=dt;
            if(i.CleaningSeconds<2)continue;
            if(CleanRoomWaste(waste.Id)){c.Energy=Math.Max(0,c.Energy-1);i.PendingPraise="自己打扫卫生";i.PraiseRemaining=12;RecordInteraction(c,"self-clean","自己清理了一处脏污。",.5);}
            i.CleaningTargetId=null;i.CleaningSeconds=0;i.NextCleaningAt=State.WorldSeconds+12;
        }
    }
    public bool SlideAlongGround(string id,double x,double z,bool manual=true)
    {
        var c=InteractionTarget(id);
        if(c==null||State.Settings.Pause||!double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)>8||Math.Abs(z)>8||
            (manual?c.Family.ColdStorage.Stored||c.Family.Memorialized||c.Interaction.Carried||c.Interaction.Falling||c.Interaction.Restraint!=RestraintKind.None:!CanSelfMove(c)))return false;
        double distance=Distance(c.X,c.Z,x,z);int steps=Math.Max(1,(int)Math.Ceiling(distance/.08));
        double startX=c.X,startZ=c.Z;
        for(int n=1;n<=steps;n++)
        {
            double nx=startX+(x-startX)*n/steps,nz=startZ+(z-startZ)*n/steps;
            if(!ShelterMovePathClear(c,nx,nz)||!CanOccupyRoom(c,nx,nz))break;c.X=nx;c.Z=nz;
        }
        c.TargetX=c.X;c.TargetZ=c.Z;c.Interaction.VelocityX=c.Interaction.VelocityZ=0;return true;
    }
    public string ExamineBody(string id)
    {
        var c=InteractionTarget(id);var error=FamilyCommandError(c);if(error!=null)return error;
        var i=c!.Interaction;
        bool secured=i.Restraint is RestraintKind.Hand or RestraintKind.Frame or RestraintKind.Wrap;
        if(!secured&&(i.Carried||i.Falling||c.Fear>40||c.Trust<30||Distance(c.X,c.Z,c.TargetX,c.TargetZ)>.15))
            return "它还不能配合站稳，先按住或固定，再检查身体。";
        i.LastExaminationAt=State.WorldSeconds;
        i.Examination=$"检查记录：生命 {c.Health:0.0}，疼痛 {c.Pain:0.0}，足部完整 {i.FootIntegrity:0.0}，水分 {i.Hydration:0.0}。\n"+DescribeInteractionCondition(c);
        i.LastObservedCondition=i.Examination;return i.Examination;
    }
    public string UseRescueJuice(string id)
    {
        var c=InteractionTarget(id);var error=FamilyCommandError(c);if(error!=null)return error;
        if(c!.Condition!=PhysicalCondition.Critical)return "救护橙汁只用于仍有救治时间的濒危个体。";
        if(c.CriticalRemaining<=0||WaterEnvironment.IsInWater(State,c))return "先救离水域，并确认仍有救治时间。";
        if(ShopQuantity("juice")<=0)return "仓库没有救护橙汁。";
        ChangeShopQuantity("juice",-1);StabilizeBody(c,55);
        c.Interaction.Hydration=Clamp100(c.Interaction.Hydration+15);
        RecordInteraction(c,"rescue-juice","接受救护橙汁后稳定了濒危状态，仍需要恢复和伤处护理。",3);
        return "已稳定生命状态，原有伤势保留，需要休养。";
    }
    public string SuppressBreedingInterest(string id)
    {
        var c=InteractionTarget(id);var error=FamilyCommandError(c);if(error!=null)return error;
        if(c!.IsYoung||c.Family.ColdStorage.Stored||c.Family.Pregnancy.Phase!=PregnancyPhase.None||
            (c.Family.InducedInterestRemaining<=0&&c.Family.BreedingInterest<40))return "它当前没有适用的发情状态。";
        if(ShopQuantity("suppressant")<=0)return "仓库没有抑制发情喷剂。";
        ChangeShopQuantity("suppressant",-1);c.Family.InducedInterestRemaining=0;c.Family.BreedingInterest=0;
        React(c,Expression.Curious,"刚才想做什么来着……","发情状态解除，愣了一会儿。",4);
        RecordInteraction(c,"breeding-suppressed","喷剂解除发情状态，伴侣关系和经历未改变。",1);return "发情已解除，它有些懵圈。";
    }
    public string SetIndividualBreedingPermission(string id,bool allowed)
    {
        var c=InteractionTarget(id);var error=FamilyCommandError(c);if(error!=null)return error;
        c!.Family.AutonomousBreedingPermitted=allowed;return allowed?"已许可自主繁育。":"已告诉它不要自主繁育；是否遵守取决于教育与性格，已有孕育继续。";
    }
    private static bool RespectsBreedingRequest(Creature c)=>c.Interaction.RuleLearning>=50||c.Interaction.CleaningLearning>=75||c.Patience>=.5&&c.Aggression<.4;
    private static bool MayChooseBreeding(Creature c)=>c.Family.AutonomousBreedingPermitted||!RespectsBreedingRequest(c);
    public string KeepHandPressure(string id,bool hold)
    {
        var c=InteractionTarget(id);var error=FamilyCommandError(c);if(error!=null)return error;
        var i=c!.Interaction;
        if(!hold){if(i.Restraint==RestraintKind.Hand)i.Restraint=RestraintKind.None;i.PersistentPressure=false;return "已经解除手的按压。";}
        if(!i.Supine||i.Carried||i.Falling||i.Restraint!=RestraintKind.None)return "先推翻并放稳，再保持按压。";
        if(State.Creatures.Any(o=>o!=c&&o.Interaction.PersistentPressure&&o.Interaction.Restraint==RestraintKind.Hand))return "先解除另一只油身上的持续按压。";
        i.PersistentPressure=true;i.Restraint=RestraintKind.Hand;ClearCompanionship(c);c.TargetX=c.X;c.TargetZ=c.Z;return "手正在持续按住；可在它身旁解除。";
    }
    public string BuryCreature(string id)
    {
        var c=InteractionTarget(id);if(c==null)return "先选择遗体。";
        bool already=c.Family.Memorialized;string result=LayToRest(id);
        if(!already&&c.Family.Memorialized)
        {
            c.Family.HasGravestone=true;
            foreach(var relative in State.Creatures.Where(o=>o.Condition!=PhysicalCondition.Dead&&FamilyRules.IsCloseFamily(o,c)))
            {relative.Interaction.Emotion.Manmanlai=Clamp100(relative.Interaction.Emotion.Manmanlai+5);relative.Family.GriefRemaining=Math.Max(0,relative.Family.GriefRemaining-15);if(relative.Family.GriefRemaining==0)relative.Family.GrievingForId=null;}
        }
        return result;
    }
}

namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public string? CleaningLessonReason(string id)
    {
        var c=InteractionTarget(id);var error=FamilyCommandError(c);if(error!=null)return error;
        if(!FamilyActionReady(c!)||c!.Fear>50||c.Fullness<30)return "现在无法专心，先照顾身体并让它安稳下来。";
        if(State.WorldSeconds-c.Interaction.LastCleaningLessonAt<12)return "先消化刚才的示范，稍后再练习。";
        if(!State.RoomObjects.Any(o=>o.RegionId==c.RegionId&&!o.Stored&&IsWaste(o.Kind)&&Distance(c.X,c.Z,o.X,o.Z)<2&&HasGroundSightLine(c,o.X,o.Z)))
            return "需要身旁看得见的脏处，不能隔着障碍示范清洁。";
        return null;
    }
    private string? CleaningBodyReason(Creature c)
    {
        var i=c.Interaction;
        if(c.RegionId!=State.ActiveRegionId)return "当前未在这一地区观察，清洁安排保留。";
        if(!CanSelfMove(c)||c.Condition!=PhysicalCondition.Active||c.Family.ColdStorage.Stored||WaterEnvironment.IsInWater(State,c))return "身体还不能安全地走过去，先照顾自身。";
        if(c.Fullness<35||i.Hydration<30||c.Energy<25)return "先满足吃喝和休息，再继续清洁。";
        if(c.Fear>50||c.Pain>=25||i.Digestion.Symptomatic)return "现在害怕、疼痛或不舒服，暂缓清洁。";
        if(InteractionGestureTarget==c.Id||_groundHandTarget==c.Id)return "正在接受玩家操作，清洁先暂停。";
        if(i.ActiveCue!=null||i.FollowPlayer||i.WaitRemaining>0||i.EatingRemaining>0||i.Resting||i.RestRemaining>0||i.Physiology.Active||i.Injuries.SelfCare.Active)
            return "先完成正在进行的吃喝、休息、练习或自身照护。";
        if(!FamilyActionReady(c,allowCleaning:true)||c.Family.CompanionTargetId!=null||c.Family.ChildThreat.TargetId!=null||HasLiveHumanCompany(c))
            return "正在处理相处、照护或危险，清洁稍后继续。";
        return null;
    }
    private string? CleaningWorkReason(Creature c)=>CleaningBodyReason(c)??
        (!(SelfMotivatedCleaner(c)&&WantsCleanerEnvironment(c))&&!HasCleaningInstruction(c)&&c.Interaction.CleaningTargetId==null?"没有主动清洁的意愿；需要明确命令，或继续培养规则教育。":null);
    private void StopCleaningTask(Creature c,string reason,SkillEvidenceKind kind=SkillEvidenceKind.Interrupted)
    {
        var i=c.Interaction;
        if(i.CleaningTargetId!=null)RecordAbility(c,LifeSkill.Cleaning,kind,reason);
        i.CleaningTargetId=null;i.CleaningSeconds=0;i.Development.CleaningStalledSeconds=0;
        i.NextCleaningAt=Math.Max(i.NextCleaningAt,State.WorldSeconds+5);
    }
    private RoomObject? FindCleaningWaste(Creature c)=>State.RoomObjects
        .Where(o=>!o.Stored&&IsWaste(o.Kind)&&o.RegionId==c.RegionId&&Distance(c.X,c.Z,o.X,o.Z)<4&&HasGroundSightLine(c,o.X,o.Z)&&
            !State.Creatures.Any(other=>other!=c&&other.Interaction.CleaningTargetId==o.Id))
        .OrderBy(o=>HasPeerCleaningOrder(c)&&o.Id==c.Interaction.Development.OrderedWasteId?0:1)
        .ThenBy(o=>Distance(c.X,c.Z,o.X,o.Z)).ThenBy(o=>o.Id,StringComparer.Ordinal).FirstOrDefault();
    private void BeginCleaningTask(Creature c,RoomObject waste)
    {
        c.Interaction.CleaningTargetId=waste.Id;c.Interaction.CleaningSeconds=0;c.Interaction.Development.CleaningStalledSeconds=0;
        RecordAbility(c,LifeSkill.Cleaning,SkillEvidenceKind.Attempt,HasPeerCleaningOrder(c)?"收到其他油的要求，开始实际尝试清理。":c.Interaction.Development.Duty==LifeDuty.Cleaning?"收到玩家安排，开始实际尝试清理。":"主动选择一处脏污，开始实际尝试清理。");
    }
    public string SetLifeDuty(string id,LifeDuty duty,string? recipientId=null)=>"手动分工暂未开放；个体仍会自主照料与清洁。";
    public string DutySummary(string id)
    {
        var c=State.Creatures.Find(c=>c.Id==id);if(c==null)return "对象已不在这里。";
        var d=c.Interaction.Development;
        if(d.Duty==LifeDuty.None)return "分工：自由生活\n"+CleaningDisposition(c)+(d.LastChoreMessage.Length>0?"\n最近："+d.LastChoreMessage:"");
        if(d.Duty==LifeDuty.Cleaning)
        {
            string reason=CleaningWorkReason(c)??(c.Interaction.CleaningTargetId!=null?"正在走近或清理脏处。":State.WorldSeconds<c.Interaction.NextCleaningAt?"本次尝试后先歇一会儿。":FindCleaningWaste(c)==null?"附近没有可见且未被他人处理的脏处。":"有余力时开始清洁。");
            return "分工：清洁附近\n"+reason;
        }
        var child=State.Creatures.Find(other=>other.Id==d.RecipientId);
        if(child==null)return "分工：照护对象已离开，需重新安排。";
        string status=c.Family.CareTargetId==child.Id?c.Family.CareLastEvent:
            FamilyCareReasonCore(c,child)??(!child.IsYoung?"对象已经长大，请调整分工。":!ChildNeedsDutyCare(child)?"对象目前不缺食水与保暖，先自由活动。":State.WorldSeconds<c.Family.NextCareAt?"照护结束或中断后先稍作休息。":"等待有余力时继续照护。");
        return $"分工：照看{child.Name}\n{status}";
    }
    private static bool ChildNeedsDutyCare(Creature child)=>child.Fullness<55||child.Interaction.Hydration<55||child.Family.Warmth<60;
    private void TickLifeDuties()
    {
        foreach(var c in State.Creatures)
        {
            SynchronizeAbilityAttempts(c);
            var i=c.Interaction;var d=i.Development;
            if(d.Duty!=LifeDuty.None)
            {
                if(d.Duty==LifeDuty.Care&&c.Family.CareTargetId==d.RecipientId)CancelFamilyCare(c,reason:"旧手动分工已取消，恢复自主生活。");
                if(d.Duty==LifeDuty.Cleaning&&i.CleaningTargetId!=null)StopCleaningTask(c,"旧手动分工已取消，恢复自主生活。");
                d.Duty=LifeDuty.None;d.RecipientId=null;
            }
            if(d.CleaningOrderedById!=null&&!HasPeerCleaningOrder(c))ClearPeerCleaningOrder(c);
            // Release stale reservations before the ordinary autonomy loop, so needs and danger can take over this tick.
            if(i.CleaningTargetId!=null&&CleaningWorkReason(c) is { } reason)StopCleaningTask(c,reason);
            if(State.WorldSeconds<d.NextDutyCheck)continue;
            d.NextDutyCheck=State.WorldSeconds+2;
            if(i.CleaningTargetId==null&&TryDelegateCleaning(c))continue;

        }
    }
}

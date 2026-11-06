namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    // Initial tuning. Rank is a motivation/readiness clue, never a substitute for actual work evidence.
    public static int CleaningLicenseLevel(Creature c)=>c.Registration.Grade switch
    {"铜" or "铜牌" or "Bronze"=>1,"银" or "银牌" or "Silver"=>2,"金" or "金牌" or "Gold"=>3,"白金" or "白金牌" or "Platinum"=>4,_=>0};
    private static bool SelfMotivatedCleaner(Creature c)=>c.Personality.Kindness>=.65||c.Interaction.RuleLearning>=50||CleaningLicenseLevel(c)>0;
    private static bool PushesChoresOntoPeers(Creature c)=>c.Personality.Kindness<.15&&c.Personality.Pride>=.75;
    private bool HasPeerCleaningOrder(Creature c)
    {
        var d=c.Interaction.Development;
        return d.CleaningOrderedById!=null&&d.CleaningOrderUntil>State.WorldSeconds&&
            State.Creatures.Any(other=>other.Id==d.CleaningOrderedById&&other.RegionId==c.RegionId&&other.Condition!=PhysicalCondition.Dead)&&
            State.RoomObjects.Any(o=>o.Id==d.OrderedWasteId&&EnvironmentHygiene.IsGroundWaste(State,o)&&o.RegionId==c.RegionId);
    }
    private bool HasCleaningInstruction(Creature c)=>c.Interaction.Development.Duty==LifeDuty.Cleaning||HasPeerCleaningOrder(c);
    public string CleaningDisposition(Creature c)
    {
        string initiative=SelfMotivatedCleaner(c)?"有主动清洁倾向":c.Personality.Kindness<.25?"通常不愿主动清洁，需要明确命令":"通常需要提醒才会清洁";
        string basis=c.Personality.Kindness>=.65?"性格友善":c.Interaction.RuleLearning>=50?"已有较好的规则教育":CleaningLicenseLevel(c)>0?$"已有{c.Registration.Grade}水平":"性格与教育尚不足以形成主动习惯";
        string order=HasPeerCleaningOrder(c)?"；正在响应其他油的清洁要求":c.Interaction.Development.Duty==LifeDuty.Cleaning?"；已收到玩家清洁安排":"";
        return $"清洁意愿：{initiative}（{basis}）{order}。"+(PushesChoresOntoPeers(c)?"特别恶劣且自负时可能把活推给其他油；对方可以拒绝。":"")+
            $"\n规则教育 {c.Interaction.RuleLearning:0}/100 · 牌照 {c.Registration.Grade} · 清洁熟悉度 {c.Interaction.CleaningLearning:0}/100";
    }
    private static double CleaningRetryDelay(Creature c)=>CleaningLicenseLevel(c) switch
    {>=3=>6,2=>8,1=>10,_=>c.Interaction.RuleLearning>=50||c.Personality.Kindness>=.65?10:14};
    private static void ClearPeerCleaningOrder(Creature c)
    {
        var d=c.Interaction.Development;d.CleaningOrderedById=null;d.OrderedWasteId=null;d.CleaningOrderUntil=0;
    }
    private static void SetChoreMessage(Creature c,string message)=>c.Interaction.Development.LastChoreMessage=message.Length>240?message[..240]:message;
    private bool TryDelegateCleaning(Creature issuer)
    {
        var d=issuer.Interaction.Development;
        if(!PushesChoresOntoPeers(issuer)||d.Duty==LifeDuty.Care||HasPeerCleaningOrder(issuer)||State.WorldSeconds<d.NextDelegationAt||CleaningBodyReason(issuer)!=null)return false;
        var waste=FindCleaningWaste(issuer);if(waste==null)return false;
        d.NextDelegationAt=State.WorldSeconds+25;
        var recipient=State.Creatures.Where(other=>other!=issuer&&other.RegionId==issuer.RegionId&&other.Condition==PhysicalCondition.Active&&
            other.Interaction.Development.Duty==LifeDuty.None&&!HasPeerCleaningOrder(other)&&other.Interaction.CleaningTargetId==null&&
            FamilyVisible(issuer,other,4)&&HasGroundSightLine(issuer,other.X,other.Z)&&HasGroundSightLine(other,waste.X,waste.Z)&&
            Distance(other.X,other.Z,waste.X,waste.Z)<4&&CleaningBodyReason(other)==null)
            .OrderBy(other=>Distance(issuer.X,issuer.Z,other.X,other.Z)).ThenBy(other=>other.Id,StringComparer.Ordinal).FirstOrDefault();
        if(recipient==null)return false;
        recipient.Family.Peers.TryGetValue(issuer.Id,out var relationship);
        bool willing=SelfMotivatedCleaner(recipient)||relationship is {Affection:>=25,Resentment:<35,Fear:<40}||
            relationship is {Fear:>=15 and <40,Resentment:<35}&&recipient.Courage<.4;
        if(!willing)
        {
            SetChoreMessage(issuer,$"想让{recipient.Name}清理，但对方没有答应。");
            SetChoreMessage(recipient,$"没有答应{issuer.Name}的清洁要求。");
            RecordInteraction(issuer,"chore-refused",d.LastChoreMessage,.2);return false;
        }
        var task=recipient.Interaction.Development;
        task.CleaningOrderedById=issuer.Id;task.OrderedWasteId=waste.Id;task.CleaningOrderUntil=State.WorldSeconds+45;
        SetChoreMessage(recipient,$"收到{issuer.Name}的清洁要求，尝试处理指定脏处。");
        SetChoreMessage(issuer,$"把一处清洁推给了{recipient.Name}，自己没有完成这项劳动。");
        BeginCleaningTask(recipient,waste);
        issuer.Interaction.NextCleaningAt=Math.Max(issuer.Interaction.NextCleaningAt,State.WorldSeconds+12);
        RecordInteraction(issuer,"chore-delegated",d.LastChoreMessage,.2);
        return true;
    }
}

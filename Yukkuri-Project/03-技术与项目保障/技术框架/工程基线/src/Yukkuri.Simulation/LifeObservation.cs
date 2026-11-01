namespace Yukkuri.Simulation;

public sealed record LifeActivityView(string Action,string Goal,string Reason);
public sealed partial class WorldSimulation
{
    // Read-only projections: inspecting UI must not consume supplies, advance random state or create relationships.
    public LifeActivityView ExplainLifeActivity(string id)
    {
        var c=State.Creatures.Find(c=>c.Id==id);
        if(c==null)return new("对象已离开","重新选择对象","");
        var i=c.Interaction;
        if(c.Condition==PhysicalCondition.Dead)return new("生命已结束","查看保留的经历","不会执行分工或继续学习。");
        if(c.Family.ColdStorage.Stored)return new("冷藏中","等待取出与复苏","分工保留，当前无法执行。");
        if(WaterEnvironment.IsInWater(State,c))return new("水域危险中","先回到安全地面","先救援，其他安排暂停。");
        if(c.Condition==PhysicalCondition.Critical)return new("身体危急","先稳定伤势","普通学习和分工暂停。");
        if(i.Carried||i.Falling||InteractionGestureTarget==c.Id||_groundHandTarget==c.Id)return new("正在接受操作或等待落地","结束接触后恢复生活","当前操作优先，未完成的劳动不会记为成功。");
        if(i.CleaningTargetId is { } wasteId)
        {
            var waste=State.RoomObjects.Find(o=>o.Id==wasteId&&!o.Stored);
            return new(i.CleaningSeconds>0?"正在清洁":"走向脏处","清理已选中的脏污",CleaningWorkReason(c)??(waste==null?"目标已经改变，准备重新寻找。":i.Development.CleaningStalledSeconds>1?"前进受阻；持续无法接近会停止重试。":HasPeerCleaningOrder(c)?"收到其他油的要求，正在实际尝试。":i.Development.Duty==LifeDuty.Cleaning?"按玩家安排尝试，仍需实际完成。":"基于性格、教育或牌照，主动尝试清洁。"));
        }
        if(c.Family.CareTargetId is { } childId)
        {
            var child=State.Creatures.Find(o=>o.Id==childId);
            return new(c.Activity,$"照看{child?.Name??"原对象"}",c.Family.CareLastEvent);
        }
        if(i.ActiveCue is { } cue)return new("正在练习"+LifeDevelopmentRules.SkillName(LifeDevelopmentRules.CueSkill(cue)),"实际完成这次动作",i.CueDemonstration?"跟随示范中，尚未计为完成。":"尝试执行指令中，尚未计为完成。");
        if(i.EatingRemaining>0)return new(c.Activity,"吃喝完当前一口","先完成吞咽，再评估其他需要。");
        if(i.Resting||i.RestRemaining>0)return new("正在休息","恢复体力","有余力后重新评估分工。");
        if(HasPeerCleaningOrder(c))return new(c.Activity,"处理同伴要求的清洁",CleaningWorkReason(c)??i.Development.LastChoreMessage);
        if(i.Development.Duty!=LifeDuty.None)return new(c.Activity,"恢复条件后继续分工",DutySummary(id));
        return new(c.Activity,"按自己的需要生活",c.Fullness<35||i.Hydration<30?"当前需要食水；位置、通路与判断会影响是否取用。":c.Energy<25?"当前需要休息。":
            i.Development.LastChoreMessage.Length>0?"最近清洁互动："+i.Development.LastChoreMessage:"未安排固定职责；具体行动以眼前行为为准。");
    }
    public string PreviewBodyInteraction(string id,InteractionInput input)
    {
        var c=InteractionTarget(id);
        var error=ValidateInteraction(c,input);
        if(error!=null)return error;
        if(input.Action==InteractionAction.Care&&UsesConsumableStock&&State.Medicine<=0)return "护理用品不足，先到商店补充。";
        if(input.Action is InteractionAction.FeedMeal or InteractionAction.FeedSweet&&UsesConsumableStock&&State.Food<=0)return "食物库存不足，先准备食物。";
        // The detailed gesture still decides acceptance and completion. This is not a success promise.
        return "可尝试："+InteractionCatalog.Choice(input.Action).Label+"；实际回应以接触后为准。";
    }
}

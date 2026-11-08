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
        if(FamilyRules.UnderIntervention(c))return new("繁育干预进行中","先结束身体干预","身体正在被占用，不能同时睡眠恢复或执行普通生活动作。");
        if(i.Falling)return new("正在下落","先安全落地","尚未获得地面支撑，无法自行走动或休息。");
        if(i.Carried)return new("正在被托着","等待放稳或接受照护","手正在支撑身体，不能自行走到食水或休息处。");
        if(InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy||_groundHandTarget==c.Id)return new("正在接受操作","结束接触后恢复生活","当前操作占用身体，未完成的劳动不会记为成功。");
        if(i.Physiology.Active)return new(i.Physiology.Excreting==ExcretionKind.Urine?"正在撒尿":"正在排便","先完成排泄","当前停步是为排泄；完成后重新评估吃喝与休息。");
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
        if(i.EatingRemaining>0)return new(i.NaturalIntakePending?"准备取这一口":i.EatingKind==IntakeKind.Water?"小口喝水":"慢慢嚼这一口","吃喝完当前一口","先完成吞咽，再评估其他需要。");
        if(i.Resting)return new("正在休息","恢复体力与困意","有余力后重新评估生活需要。");
        if(i.Restraint!=RestraintKind.None)return new(c.Activity,"活动受到限制",i.Restraint switch
        {
            RestraintKind.Frame=>"固定具限制移动，无法自行走到远处的食水。",
            RestraintKind.Box=>"箱壁限制移动，只能尝试够得到的食水。",
            RestraintKind.Wrap=>"软布包裹中，可以接受照护，暂时不能自行走动。",
            RestraintKind.Hanging=>"悬挂装置限制身体，需要放稳后才能自行生活。",
            _=>"被手按住，解除按压后才能恢复自主移动。"
        });
        if(i.RestRemaining>0)return new(c.Activity,"先处理妨碍休息的需要",
            CourtyardSurvival.Urgent(c,sleeping:true)?"食水不足，先寻找吃喝，再继续休息。":
            c.Fear>=60?"恐惧较高，暂时不能放松入睡。":c.Pain>=65?"疼痛妨碍入睡，先处理伤势。":
            i.RestTargetId!=null?"正在找休息位置；通路受阻时会改在地面休息。":"身体或环境还不允许入睡，尚未获得休息恢复。");
        if(NaturalResources.WantsFood(c)||NaturalResources.WantsWater(c))return ExplainResourceNeed(c);
        if(!CanSelfMove(c))return new(c.Activity,"等待身体恢复活动能力",
            c.Condition==PhysicalCondition.Recovering?"仍在伤后恢复，暂时不能自主行走；可照护或把食水送到身边。":
            i.Supine?"身体翻倒，尚未翻回正常姿势。":"移动部位受伤，无法走到远处；可照护或提供够得到的食水。");
        if(c.Family.SeekingWarmth)return new(c.Activity,"恢复保暖","身体发冷，正在尝试寻找同伴或可进入的小屋。");
        if(i.Sleepiness>=CourtyardSurvival.DaytimeSleepiness||c.Energy<CourtyardSurvival.ExhaustedEnergy)
            return new(c.Activity,"寻找休息机会",i.Sleepiness>=CourtyardSurvival.DaytimeSleepiness?"困意已经很高，需要睡眠；尚未真正入睡。":"体力已经耗尽，需要休息；尚未获得休息恢复。");
        if(!EmotionalReadiness.Settled(c))return new(c.Activity,"慢慢恢复安全感",
            c.Pain>=20?"疼痛仍影响放松与亲近，需要伤势护理。":c.Fear>=35?"仍然害怕，需要留出距离和持续安全。":"惊惧余波或离水后的不适还在，需要持续安全与恢复时间。");
        if(HasPeerCleaningOrder(c))return new(c.Activity,"处理同伴要求的清洁",CleaningWorkReason(c)??i.Development.LastChoreMessage);
        if(i.Development.Duty!=LifeDuty.None)return new(c.Activity,"恢复条件后继续分工",DutySummary(id));
        return new(c.Activity,"按自己的需要生活",i.Development.LastChoreMessage.Length>0?"最近清洁互动："+i.Development.LastChoreMessage:"当前没有迫切需要，按自己的节奏活动。");
    }
    private LifeActivityView ExplainResourceNeed(Creature c)
    {
        var i=c.Interaction;
        string goal=NaturalResources.WantsFood(c)?NaturalResources.WantsWater(c)?"补充食物与水分":"补充食物":"补充水分";
        if(!CanSelfMove(c))return new(c.Activity,goal,"当前身体无法自行移动，只能尝试够得到的食水，或接受递送照护。");
        if(i.HelpWaitingUntil>State.WorldSeconds)return new(c.Activity,goal,"正在短暂等待或观察眼前的手，之后会继续自行寻找。");
        if(i.ResourceTargetId is { } id&&i.Resources.TryGetValue(id,out var resource))
            return new(c.Activity,goal,i.ResourceStuckSeconds>1?"接近食水时受阻，持续走不到会换目标。":
                NaturalResources.IsWater(resource.Kind)?"已选中记忆中的水源，正在尝试接近并喝到水。":"已选中记忆中的食物，正在尝试接近并取食。");
        if(i.Digestion.Symptomatic&&i.Digestion.NextFoodAt>State.WorldSeconds&&!NaturalResources.WantsWater(c))
            return new(c.Activity,goal,"肚子不适，暂缓进食，缓一会儿再尝试。");
        return new(c.Activity,goal,"尚未确定可取用的目标，会继续观察与寻找；有库存不代表身边有能吃喝到的资源。");
    }
    public string PreviewBodyInteraction(string id,InteractionInput input)
    {
        var c=InteractionTarget(id);
        var error=ValidateInteraction(c,input);
        if(error!=null)return error;
        if(input.Action==InteractionAction.Care&&UsesConsumableStock&&State.Medicine<=0)return "护理用品不足，先到商店补充。";
        if(input.Action is InteractionAction.FeedMeal or InteractionAction.FeedSweet&&UsesConsumableStock&&State.Food<=0)return "食物库存不足，先准备食物。";
        // The detailed gesture still decides acceptance and completion. This is not a success promise.
        return InteractionCatalog.Choice(input.Action).Label;
    }
}

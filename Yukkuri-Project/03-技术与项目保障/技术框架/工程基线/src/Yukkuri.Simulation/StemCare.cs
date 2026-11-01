namespace Yukkuri.Simulation;

// One attached brood shares the original pregnancy identity and birth ledger.
// These values never represent separately born or detached children.
public sealed class StemBroodState
{
    public string EventId { get; set; } = "";
    public double Integrity { get; set; } = 100;
    public double Hydration { get; set; } = 100;
    public double SupportRemaining { get; set; }
    public double LastCareAt { get; set; } = -100;
    public double LastDamageAt { get; set; } = -100;
    public string LastDamageCause { get; set; } = "";
    public string? LastDamageActorId { get; set; }
}

public sealed record StemCareView(string EventId,int PlannedChildren,double Progress,
    double Integrity,double Hydration,double SupportRemaining,bool CanSupport,
    string? RefusalReason,string LastDamageCause);

public sealed partial class WorldSimulation
{
    private static void InitializeStemBrood(PregnancyState p)=>p.Stem=
        p.Method==BirthMethod.Stem&&p.EventId!=null?new(){EventId=p.EventId}:null;

    private static StemBroodState? AttachedStem(Creature c)
    {
        var p=c.Family.Pregnancy;
        return c.Condition!=PhysicalCondition.Dead&&p.Phase==PregnancyPhase.Gestating&&
            p.Method==BirthMethod.Stem&&p.Stem is { } stem&&stem.EventId==p.EventId?stem:null;
    }

    public StemCareView? StemCareStatus(string motherId)
    {
        var c=State.Creatures.Find(o=>o.Id==motherId);
        if(c==null||AttachedStem(c) is not { } s||s.Integrity<=0)return null;
        var p=c.Family.Pregnancy;var reason=StemSupportError(c,s);
        return new(s.EventId,p.PlannedChildren,p.Elapsed/FamilyRules.GestationSeconds(BirthMethod.Stem),
            s.Integrity,s.Hydration,s.SupportRemaining,reason==null,reason,s.LastDamageCause);
    }

    private string? StemContactError(Creature c)
    {
        var error=FamilyCommandError(c);
        if(error!=null)return error;
        if(c.Family.ColdStorage.Stored)return "先取出冷藏中的母体，再接触外茎。";
        if(FamilyRules.UnderIntervention(c))return "繁育干预仍在进行，先停止干预。";
        if(State.Mode==PlayMode.CloseUp&&State.SelectedId!=c.Id)return "先退出特写，再选择母体。";
        if(c.Condition==PhysicalCondition.Critical)return "母体仍然危急，先稳定母体伤势。";
        if(WaterEnvironment.IsInWater(State,c))return "先将母体救出水面，再照护外茎。";
        return null;
    }

    private string? StemSupportError(Creature c,StemBroodState s)
    {
        var error=StemContactError(c);if(error!=null)return error;
        if(State.Medicine<=0)return "没有护理包，先补充护理材料。";
        if(State.WorldSeconds-s.LastCareAt<20)return "刚做过支护，需要时间观察，不能重复包扎。";
        if(s.SupportRemaining>10&&s.Integrity>=90&&s.Hydration>=75)return "这窝外茎目前支护充足。";
        return null;
    }

    private string? ValidateStemInteraction(Creature c,InteractionInput input)
    {
        var error=StemContactError(c);if(error!=null)return error;
        if(AttachedStem(c) is not { } stem||input.ObjectId!=stem.EventId)
            return "这窝外茎已经改变或结束，先重新对准当前外茎。";
        if(stem.Integrity<=0)return "这窝外茎已经无法继续发育。";
        if(input.Action==InteractionAction.CutStem&&(c.Interaction.Carried||c.Interaction.Falling||c.Interaction.Height>.05))
            return "先把母体放稳，再剪离外茎。";
        if(input.Action==InteractionAction.CutStem&&(State.Creatures.Any(o=>o.Interaction.Carried)||
            State.RoomObjects.Any(o=>o.HeldByHand)||State.DetachedStemBroods.Any(o=>o.HeldByHand)))
            return "手里还有其他东西，先放下再使用剪具。";
        return input.Action is InteractionAction.SupportStem or InteractionAction.Punch or InteractionAction.Burn or InteractionAction.CutStem
            ?null:"这个工具还不能用于外茎；请移开后对准身体，或使用茎株支护。";
    }

    private InteractionFeedback BeginStemGesture(Creature c,InteractionInput input)
    {
        if(input.Action==InteractionAction.Punch)return StrikeStem(c,input);
        var stem=AttachedStem(c)!;
        if(input.Action==InteractionAction.SupportStem&&StemSupportError(c,stem) is { } reason)return Unavailable(reason);
        var g=new ContactGesture{TargetId=c.Id,Input=input,ObjectId=input.ObjectId};
        _interactionGesture=g;
        g.Last=new(true,input.Action==InteractionAction.SupportStem?"对准这一窝外茎保持接触，完成后才使用护理包。":
            input.Action==InteractionAction.CutStem?"剪具已对准这窝外茎，保持接触完成剪离；移开会停止。":"火焰正在接近外茎，移开立即停止。",
            c.Speech,input.Action==InteractionAction.SupportStem?"stem-care":input.Action==InteractionAction.CutStem?"stem-cut":"stem-burn");
        return g.Last;
    }

    private InteractionFeedback ContinueStemGesture(ContactGesture g,Creature c,InteractionInput input,bool inContact)
    {
        // Completed support remains an inert held gesture, so the same mouse
        // hold cannot start another purchase or hold the mother in a care pose.
        if(g.Applied&&input.Action==InteractionAction.SupportStem)return g.Last with {Changed=false};
        double dt=Math.Min(.1,input.Duration);if(dt<=0)return g.Last;
        g.Input=input;g.Elapsed+=dt;g.ContactActive=inContact;
        if(!inContact)
        {
            g.ContactElapsed=0;
            if(c.ReactionSerial==g.ReactionSerial){c.Interaction.ReactionPose="idle";c.Interaction.ReactionPoseRemaining=0;}
            return g.Last with {Message="已离开外茎，连续接触计时重新开始。",Changed=false,Pose="idle"};
        }
        g.ContactElapsed+=dt;
        if(input.Action==InteractionAction.CutStem)
        {
            if(g.ContactElapsed+1e-9<1.6)return g.Last;
            g.Last=CutAttachedStem(c);g.Applied=g.Last.Success;g.CompletionReleased=true;g.ReactionSerial=c.ReactionSerial;
            if(!g.Last.Success)EndInteractionGesture(true);
            return g.Last;
        }
        if(input.Action==InteractionAction.SupportStem)
        {
            var stem=AttachedStem(c)!;
            if(StemSupportError(c,stem) is { } error)
            {EndInteractionGesture(true);return Unavailable(error);}
            if(g.ContactElapsed+1e-9<1.6)return g.Last;
            State.Medicine--;stem.LastCareAt=State.WorldSeconds;
            stem.Integrity=Clamp100(stem.Integrity+12);stem.Hydration=Clamp100(stem.Hydration+25);
            stem.SupportRemaining=60;g.Applied=true;g.CompletionReleased=true;
            c.Family.LastEvent="这一窝外茎已补水并支护，仍需母体安稳吃喝；支护会逐渐失效。";
            RecordInteraction(c,"stem-support",c.Family.LastEvent,2);
            g.Last=Feedback(c,"stem-support",Expression.Worried,c.Family.LastEvent,"stem-supported");
            g.ReactionSerial=c.ReactionSerial;return g.Last;
        }
        double amount=dt*(2+input.Force*7);
        DamageAttachedStem(c,amount,"人类灼烧外茎","player",direct:true);
        if(CanSelfMove(c))
        {
            double x=c.X-input.X,z=c.Z-input.Z;
            if(Math.Abs(x)+Math.Abs(z)<.01){x=c.X-State.PlayerX;z=c.Z-State.PlayerZ;}
            if(Math.Abs(x)+Math.Abs(z)<.01)z=-1;
            RoomMove(c,c.X+x,c.Z+z,dt*1.7);c.Activity="护住外茎，躲开火焰";
        }
        g.Applied=true;g.Effect+=amount;
        bool lost=AttachedStem(c)==null;
        if(g.Elapsed-g.FeedbackAt>=1.2||lost)
        {
            g.FeedbackAt=g.Elapsed;
            g.Last=Feedback(c,"stem-burn",Expression.Frightened,lost?c.Family.Pregnancy.Outcome:"外茎正在灼伤，这窝幼体的完整度持续下降。",lost?"stem-loss":"stem-burn");
            g.ReactionSerial=c.ReactionSerial;
        }
        return g.Last with {Changed=true,Effect=amount};
    }

    private InteractionFeedback StrikeStem(Creature c,InteractionInput input)
    {
        double amount=5+input.Force*15;
        DamageAttachedStem(c,amount,"人类击打外茎","player",direct:true);
        bool lost=AttachedStem(c)==null;
        return Feedback(c,"stem-hit",Expression.Frightened,lost?c.Family.Pregnancy.Outcome:
            "外茎受到击打，这窝幼体受损；母体身体伤势单独计算。",lost?"stem-loss":"stem-hit");
    }

    private void DamageAttachedStem(Creature mother,double amount,string cause,string? actor,bool direct=false,bool transmitted=false)
    {
        if(amount<=0||AttachedStem(mother) is not { } s)return;
        if(s.SupportRemaining>0&&transmitted)amount*=.4;
        amount=Math.Min(amount,s.Integrity);
        s.Integrity=Math.Max(0,s.Integrity-amount);
        s.LastDamageAt=State.WorldSeconds;s.LastDamageCause=cause;s.LastDamageActorId=actor;
        if(direct)
        {
            // This is a witnessed threat to the brood, not a Head injury or a
            // second maternal Hurt call. Body caution and body Hits stay intact.
            mother.Fear=Clamp100(mother.Fear+amount*.65);
            mother.Trust=Math.Max(0,mother.Trust-amount*.4);
            var bond=mother.Interaction.HumanBond;
            bond.LastHarmAt=State.WorldSeconds;bond.Safety=Math.Max(0,bond.Safety-amount);
            bond.CareBudget=0;RememberThreat(mother,amount*2);EndHumanCompany(mother);
            CancelWildSelfCare(mother,"外茎正在受到伤害，停止自行护伤并保护这一窝。");
            DetachNaturalIntake(mother);
            CancelFamilyCare(mother,reason:"外茎正在受到伤害，递送已中断；已取份额留在身旁。");
            if(mother.Family.CompanionTargetId!=null)ClearCompanionship(mother);
            RecordPlayerThreat(mother,$"人类的外茎伤害威胁到{mother.Name}正在孕育的幼体",amount*1.8);
            mother.Interaction.Resting=false;mother.Interaction.RestRemaining=0;
            mother.Interaction.ActiveCue=null;mother.Interaction.FollowPlayer=false;
        }
        if(s.Integrity<=0)
            EndPregnancy(mother,$"外茎完整度耗尽，这窝孕育未能继续。最后原因：{cause}。母体进入恢复期。");
    }

    private bool AdvanceStemBrood(Creature mother,double dt)
    {
        if(AttachedStem(mother) is not { } s)return true;
        if(s.Integrity<=0)
        {
            EndPregnancy(mother,"外茎完整度耗尽，孕育未能继续。最后原因："+
                (s.LastDamageCause.Length>0?s.LastDamageCause:"此前的外茎损伤")+"。母体进入恢复期。");
            return false;
        }
        s.SupportRemaining=Math.Max(0,s.SupportRemaining-dt);
        bool submerged=WaterEnvironment.IsInWater(State,mother);
        bool nourished=mother.Fullness>=25&&mother.Interaction.Hydration>=25&&mother.Condition!=PhysicalCondition.Critical;
        s.Hydration=Math.Clamp(s.Hydration+dt*(submerged?-.8:nourished?.08:-.4),0,100);
        if(submerged)DamageAttachedStem(mother,dt*1.2,"母体浸水使外茎泡损",null,direct:false);
        else if(!nourished||s.Hydration<20)
            DamageAttachedStem(mother,dt*(s.Hydration<20?.45:.12),"母体失养使外茎缺水枯损",null,direct:false);
        else if(s.Hydration>=40)s.Integrity=Clamp100(s.Integrity+dt*.015);
        return mother.Family.Pregnancy.Phase==PregnancyPhase.Gestating;
    }
}

namespace Yukkuri.Simulation;

public sealed partial class FamilyLife
{
    // Physiological interest is neither consent nor affection toward a particular partner.
    public double BreedingInterest { get; set; }
    public double InducedInterestRemaining { get; set; }
    public double NextInductionAt { get; set; }
    public double LastInterestEventAt { get; set; } = -100;
}

public sealed partial class WorldState
{
    public int CourtshipTools { get; set; } = 1;
    public int CourtshipToolPurchases { get; set; }
}

public sealed partial class WorldSimulation
{
    public const int CourtshipToolPrice=24;

    public bool BuyCourtshipTool()
    {
        if(State.InteractionPlaytest)return BuyShopProduct("inducer");
        if(!State.InteractionPlaytest||State.CourtshipTools>=99||
            !State.UnlimitedResources&&State.Money<CourtshipToolPrice)return false;
        if(!State.UnlimitedResources)State.Money-=CourtshipToolPrice;
        State.CourtshipTools++;State.CourtshipToolPurchases=Math.Min(int.MaxValue-1,State.CourtshipToolPurchases)+1;
        State.LastActionMessage="购入一份繁育诱导用品；只能有限提高兴趣，不会替它选择伴侣。";
        return true;
    }

    // Read-only eligibility for paused panels; performing the action has its own pause gate.
    public string? BreedingInductionReason(string id)
    {
        var c=InteractionTarget(id);
        if(c==null)return "先选择当前小院里的个体。";
        if(c.Condition==PhysicalCondition.Dead)return "它已经离世，不能再接受诱导。";
        if(c.IsYoung||!FamilyRules.SupportedSpecies(c.Body.SpeciesId))
            return "只适用于已成熟、已制作繁育规则的个体。";
        if(c.Family.ColdStorage.Stored)return "先从冷藏取出。";
        if(State.WorldSeconds<c.Family.NextInductionAt)return "刚进行过诱导，需要留出恢复间隔。";
        if(State.CourtshipTools<=0)return "需要先购买繁育诱导用品。";
        return null;
    }

    public InteractionFeedback InduceBreedingInterest(string id)
    {
        if(State.Settings.Pause)return Unavailable("暂停中，诱导等待继续。");
        var error=BreedingInductionReason(id);
        if(error!=null)return Unavailable(error);
        var c=InteractionTarget(id)!;
        var f=c.Family;
        State.CourtshipTools--;
        bool suitable=c.Condition==PhysicalCondition.Active&&InterventionBodyAvailable(c)&&
            f.Pregnancy.Phase==PregnancyPhase.None&&c.Pain<25&&c.Fear<55&&c.Fullness>=50&&c.Interaction.Hydration>=50&&c.Energy>=50;
        if(!suitable)
        {
            f.NextInductionAt=State.WorldSeconds+30;
            Hurt(c,12+Math.Max(0,50-c.Health)*.35+Math.Max(0,30-c.Energy)*.2,"身体不适合时使用繁育诱导喷剂","player");
            c.Energy=Math.Max(0,c.Energy-12);c.Interaction.Hydration=Math.Max(0,c.Interaction.Hydration-10);
            RecordInteraction(c,"unsafe-induction","身体未能承受诱导，出现真实身体恶化。",8);
            return new(true,"身体不适合，诱导造成恶化；严重时可能进入濒危或死亡。",c.Speech,"guard",Changed:true);
        }
        f.InducedInterestRemaining=90;f.NextInductionAt=State.WorldSeconds+180;
        f.BreedingInterest=Clamp100(Math.Max(40,f.BreedingInterest+20));
        f.NextBreedingDecisionAt=State.WorldSeconds;
        c.Fullness=Math.Max(0,c.Fullness-3);c.Energy=Math.Max(0,c.Energy-3);
        RecordInteraction(c,"breeding-induction","人类使用普通诱导用品，使身体暂时更容易产生繁育兴趣；对伴侣的意愿仍由自己决定。",2);
        return Feedback(c,"courtship-interest",Expression.Curious,
            "诱导已生效，会在一段时间内缓慢增加兴趣；仍需安全环境、适合的成熟伴侣和双方接受。","courtship-interest");
    }

    private bool SecureForBreeding(Creature c) => c.Condition==PhysicalCondition.Active&&
        !c.Family.ColdStorage.Stored&&!WaterEnvironment.IsInWater(State,c)&&
        !c.Interaction.Water.InWater&&c.Interaction.Water.RecoverySeconds<=0&&
        c.Interaction.Emotion.Alarm<12&&c.Interaction.Emotion.SafeSeconds>=45&&
        c.Interaction.Emotion.Manmanlai>=60&&c.Fear<30&&c.Pain<20&&
        !State.Creatures.Any(other=>other!=c&&other.Condition!=PhysicalCondition.Dead&&FamilyVisible(c,other,5)&&
            (other.IsIntruder||other.Family.Conflict.TargetId!=null||other.Family.ChildThreat.TargetId!=null||
             other.Personality.HungerTargetId!=null||WaterEnvironment.IsInWater(State,other)));

    private static bool DeepPairBond(Creature c,Creature other) =>
        c.Family.PartnerIds.Contains(other.Id)&&other.Family.PartnerIds.Contains(c.Id)&&
        c.Family.Peers.TryGetValue(other.Id,out var bond)&&bond.Familiarity>=70&&bond.Affection>=70&&
        bond.Fear<15&&bond.Resentment<15;

    private void AdvanceBreedingInterest(Creature c,double dt)
    {
        var f=c.Family;
        if(c.Condition==PhysicalCondition.Dead){f.InducedInterestRemaining=0;return;}
        if(dt<=0)return;
        bool induced=f.InducedInterestRemaining>0;
        f.InducedInterestRemaining=Math.Max(0,f.InducedInterestRemaining-dt);
        if(c.IsYoung||f.Pregnancy.Phase!=PregnancyPhase.None||c.Family.ColdStorage.Stored||
            c.Condition!=PhysicalCondition.Active||c.Pain>=25||c.Fear>=55||WaterEnvironment.IsInWater(State,c))
        {f.BreedingInterest=Math.Max(0,f.BreedingInterest-dt*.35);return;}
        bool comfortable=c.Fullness>=60&&c.Interaction.Hydration>=60&&c.Energy>=50&&SecureForBreeding(c);
        if(induced&&comfortable&&MayChooseBreeding(c)&&f.CompanionTargetId==null&&State.WorldSeconds-f.LastInterestEventAt>=12)
        {
            var interested=State.Creatures.Where(other=>other!=c&&!other.IsYoung&&other.Body.ReproductionRole!=c.Body.ReproductionRole&&
                other.RegionId==c.RegionId&&MayChooseBreeding(other)&&SecureForBreeding(other)&&FamilyRules.WillPair(c,other)&&FamilyRules.WillPair(other,c))
                .OrderBy(other=>Distance(c.X,c.Z,other.X,other.Z)).FirstOrDefault();
            if(interested!=null){f.LastInterestEventAt=State.WorldSeconds;InviteCompanion(c.Id,interested.Id);}
        }
        var partner=comfortable?State.Creatures.FirstOrDefault(other=>other!=c&&
            !other.IsYoung&&other.Body.ReproductionRole!=c.Body.ReproductionRole&&
            FamilyVisible(c,other,4.5)&&DeepPairBond(c,other)&&DeepPairBond(other,c)&&SecureForBreeding(other)):null;
        double gain=partner!=null?.07:0;
        // Ordinary induction supplies do not create emotional safety or rewrite a relationship.
        if(induced&&c.Fullness>=50&&c.Interaction.Hydration>=50&&c.Energy>=50)gain+=.12;
        f.BreedingInterest=Clamp100(f.BreedingInterest+dt*(gain>0?gain:-.025));
        if(partner!=null&&f.BreedingInterest>=40&&State.WorldSeconds-f.LastInterestEventAt>=120&&
            c.ReactionRemaining<=0&&FamilyActionReady(c))
        {
            f.LastInterestEventAt=State.WorldSeconds;
            React(c,Expression.Shy,"想和你一直慢慢来……","在安全的环境里，对熟悉的伴侣表达了繁育兴趣。",5);
            f.LastEvent=$"对{partner.Name}表达了繁育兴趣，仍需对方接受";
        }
    }

    private void SettleBreedingInterest(Creature mother,Creature? father)
    {
        mother.Family.BreedingInterest=0;mother.Family.InducedInterestRemaining=0;
        if(father!=null)father.Family.BreedingInterest=Math.Max(0,father.Family.BreedingInterest-30);
    }
}

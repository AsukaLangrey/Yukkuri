namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private string? FamilyCommandError(Creature? c,bool allowDead=false)
    {
        if(!State.InteractionPlaytest||c==null||c.RegionId!=State.ActiveRegionId)return "先选择当前小院的个体。";
        if(State.Settings.Pause)return "先恢复时间，再安排。";
        if(!allowDead&&c.Condition==PhysicalCondition.Dead)return "它已经离世，不能再参与。";
        return null;
    }

    public string SetBreedingPermission(string id,bool allowed)
    {
        var c=State.Creatures.Find(x=>x.Id==id);var error=FamilyCommandError(c);
        if(error!=null)return error;
        c!.Family.AllowsBreeding=allowed;
        // This changes a management permission; it does not fabricate a lesson
        // learned or cancel a pregnancy which has already begun.
        return State.LastActionMessage=allowed?"已允许安排繁育，仍需双方愿意并符合身体条件。":"已暂停新的繁育安排；已经开始的孕育继续，需要照护。";
    }

    public string SetAutonomousBreeding(bool allowed)
    {
        if(!State.InteractionPlaytest)return "需要在相处小院安排。";
        if(State.Settings.Pause)return "先恢复时间，再调整。";
        State.AutonomousBreeding=allowed;
        return State.LastActionMessage=allowed?"允许成熟伴侣自主考虑繁育；双方仍可拒绝。":"暂停新的自主繁育，已开始的孕育继续。";
    }

    private void TickAutonomousBreeding()
    {
        if(!State.AutonomousBreeding)return;
        foreach(var mother in State.Creatures)
        {
            var f=mother.Family;
            if(mother.Body.ReproductionRole!=ReproductionRole.Maternal||mother.IsYoung||mother.RegionId!=State.ActiveRegionId||
                f.Pregnancy.Phase!=PregnancyPhase.None||State.WorldSeconds<Math.Max(f.NextBreedingDecisionAt,f.Pregnancy.RetryAt))continue;
            f.NextBreedingDecisionAt=State.WorldSeconds+120;
            // Existing dependent children and the parents' own needs take precedence.
            if(!FamilyActionReady(mother)||!SecureForBreeding(mother)||f.BreedingInterest<40||mother.Fullness<80||mother.Interaction.Hydration<80||mother.Energy<65||
                State.Creatures.Any(c=>c.IsYoung&&c.Condition!=PhysicalCondition.Dead&&c.Family.CaregiverIds.Contains(mother.Id)))continue;
            var father=State.Creatures.Where(c=>f.PartnerIds.Contains(c.Id)&&c.Body.ReproductionRole==ReproductionRole.Paternal&&
                FamilyActionReady(c)&&FamilyVisible(mother,c)&&SecureForBreeding(c)&&c.Family.BreedingInterest>=40&&
                DeepPairBond(mother,c)&&DeepPairBond(c,mother)&&c.Fullness>70&&c.Interaction.Hydration>70)
                .OrderBy(c=>c.Id,StringComparer.Ordinal).FirstOrDefault();
            if(father==null)continue;
            bool secure=!State.Creatures.Any(c=>c.RegionId==mother.RegionId&&c.Condition==PhysicalCondition.Active&&
                (c.IsIntruder||c.Family.Conflict.TargetId!=null||c.Family.ChildThreat.TargetId!=null)&&FamilyVisible(mother,c,5));
            if(!secure)continue;
            BirthMethod method=secure&&mother.Patience>=.5&&father.Patience>=.35&&mother.Health>=90&&mother.Energy>=80&&mother.Fullness>=90
                ?BirthMethod.Live:BirthMethod.Stem;
            if(PregnancyEligibility(mother,father,method)!=null)continue;
            string previous=State.LastActionMessage;
            RequestPregnancy(mother.Id,father.Id,method);
            State.LastActionMessage=previous;
        }
    }

    private string? PregnancyEligibility(Creature mother,Creature father,BirthMethod method)
    {
        if(!mother.Family.AllowsBreeding||!father.Family.AllowsBreeding)return "有一方的新繁育许可已关闭，仍可以相处和结伴。";
        if(mother.Id==father.Id||mother.Body.ReproductionRole!=ReproductionRole.Maternal||father.Body.ReproductionRole!=ReproductionRole.Paternal)
            return "需要出生角色分别为母体与父体的两只成熟油。";
        if(mother.RegionId!=father.RegionId)return "它们不在同一个地方。";
        if(mother.IsYoung||father.IsYoung)return "幼体还没有成熟，不能安排繁育。";
        if(WaterEnvironment.IsInWater(State,mother)||WaterEnvironment.IsInWater(State,father))
            return "先将落水的个体救回安全岸边，再考虑繁育安排。";
        if(!FamilyRules.SupportedSpecies(mother.Body.SpeciesId)||!FamilyRules.SupportedSpecies(father.Body.SpeciesId))
            return "这个物种组合还没有制作繁育规则。";
        if(!FamilyRules.WillPair(mother,father)||!FamilyRules.WillPair(father,mother))
            return "双方还没有都愿意；先照顾需要，让它们有安稳相处的机会。";
        double nutrition=method==BirthMethod.Live?65:45;
        if(mother.Fullness<nutrition||mother.Interaction.Hydration<nutrition||mother.Health<(method==BirthMethod.Live?80:65)||mother.Energy<(method==BirthMethod.Live?60:40))
            return method==BirthMethod.Live?"胎生需要母体有更充足的营养、体力和较好的身体状态。":"母体当前吃喝、体力或身体条件还不适合孕育。";
        return null;
    }

    public string RequestPregnancy(string motherId,string fatherId,BirthMethod method)
    {
        var mother=State.Creatures.Find(c=>c.Id==motherId);var father=State.Creatures.Find(c=>c.Id==fatherId);
        var error=FamilyCommandError(mother)??FamilyCommandError(father);
        if(error!=null)return error;
        if(!Enum.IsDefined(method))return "没有这种孕育方式。";
        var p=mother!.Family.Pregnancy;
        if(p.Phase!=PregnancyPhase.None)return "这次孕育或恢复尚未结束，普通安排需要先等身体准备好。";
        if(State.WorldSeconds<p.RetryAt)return "刚刚尝试过，先休息和相处，不要连续催促。";
        error=PregnancyEligibility(mother,father!,method);
        if(error!=null)return error;
        if(!FamilyActionReady(mother)||!FamilyActionReady(father!)||
            mother.Family.CompanionTargetId is { } mt&&mt!=fatherId||father!.Family.CompanionTargetId is { } ft&&ft!=motherId)
            return "先等双方结束眼前的照护、休息或其他相处，再提出孕育安排。";
        p.Phase=PregnancyPhase.Preparing;p.Method=method;p.FatherId=fatherId;
        p.Elapsed=0;p.WaitingSeconds=0;p.Burden=0;p.PlannedChildren=0;p.EventId=null;p.Outcome="";
        p.Forced=false;p.InterventionRemaining=0;p.RecoveryDebt=0;p.Decision=PregnancyDecision.Undecided;
        p.PrenatalAttachment=Clamp100(mother.Patience*60+(mother.Family.Peers.GetValueOrDefault(fatherId)?.Affection??0)*.4);
        p.NextDecisionAt=State.WorldSeconds+30;
        BeginCompanionship(mother,father!);
        return State.LastActionMessage="已向双方提出安排。它们需要实际靠近并接受，之后才会开始孕育。";
    }

    private string NewFamilyId(string kind)
    {
        if(State.FamilySequence==long.MaxValue)throw new InvalidOperationException("家庭事件编号已耗尽，不能再生成新事件。");
        string id;
        do
        {
            id=$"family-{kind}-{++State.FamilySequence}";
            if(State.FamilySequence==long.MaxValue)throw new InvalidOperationException("家庭事件编号已耗尽。");
        }while(State.CompletedBirths.Contains(id)||State.FamilyRelationshipEvents.Any(e=>e.Id==id)||
            State.Creatures.Any(c=>c.Id==id||c.Family.Pregnancy.EventId==id||kind=="birth"&&c.Id.StartsWith(id+"-",StringComparison.Ordinal))||State.RoomObjects.Any(o=>o.Id==id));
        return id;
    }

    private void EndPregnancy(Creature mother,string outcome)
    {
        var p=mother.Family.Pregnancy;
        if(mother.Condition==PhysicalCondition.Dead)
        {
            p.Phase=PregnancyPhase.None;p.RecoveryRemaining=0;
            p.Outcome="宝宝已出生，母体在生产后离世。";mother.Family.LastEvent=p.Outcome;
            ClearCompanionship(mother);RecordInteraction(mother,"pregnancy-outcome",p.Outcome,5);return;
        }
        p.Phase=PregnancyPhase.Recovering;p.InterventionRemaining=0;p.RecoveryRemaining=Math.Min(900,FamilyRules.RecoverySeconds(p.Method)+p.RecoveryDebt);
        p.RetryAt=State.WorldSeconds+p.RecoveryRemaining;p.Outcome=outcome;
        ClearCompanionship(mother);mother.Family.LastEvent=outcome;
        RecordInteraction(mother,"pregnancy-outcome",outcome,3);
    }

    private void TickFamilies(double dt)
    {
        // Snapshot: births append complete individuals only after a parent update.
        foreach(var mother in State.Creatures.ToArray())
        {
            var f=mother.Family;var p=f.Pregnancy;double bodyDt=BiologicalDelta(mother,dt);
            TickYoungLife(mother,bodyDt);
            AdvanceBreedingInterest(mother,bodyDt);
            if(p.Phase==PregnancyPhase.None)continue;
            if(mother.Condition==PhysicalCondition.Dead)
            {
                if(p.Phase is PregnancyPhase.Preparing or PregnancyPhase.Gestating)
                {p.Phase=PregnancyPhase.None;p.Outcome="母体离世，孕育中止。";f.LastEvent=p.Outcome;ClearCompanionship(mother);RecordInteraction(mother,"pregnancy-loss",p.Outcome,5);}
                p.Phase=PregnancyPhase.None;p.RecoveryRemaining=0;p.InterventionRemaining=0;
                continue;
            }
            if(p.Phase==PregnancyPhase.Recovering)
            {
                if(mother.Fullness>35&&mother.Interaction.Hydration>35&&mother.Pain<40)
                    p.RecoveryRemaining=Math.Max(0,p.RecoveryRemaining-bodyDt);
                if(p.RecoveryRemaining<=0){p.Phase=PregnancyPhase.None;p.Burden=0;f.LastEvent="孕育后的身体负担已经缓解。";}
                continue;
            }
            var father=State.Creatures.Find(c=>c.Id==p.FatherId);
            if(FamilyRules.UnderIntervention(mother)){AdvanceForcedIntervention(mother,father,bodyDt);continue;}
            if(p.Phase==PregnancyPhase.Preparing)
            {
                p.WaitingSeconds=Math.Min(90,p.WaitingSeconds+bodyDt);
                if(father==null||father.Condition==PhysicalCondition.Dead||!f.AllowsBreeding||!father.Family.AllowsBreeding||p.WaitingSeconds>=90)
                {p.Phase=PregnancyPhase.None;p.Elapsed=0;p.RetryAt=State.WorldSeconds+30;ClearCompanionship(mother);f.LastEvent="这次安排没有开始，双方先回到各自生活。";continue;}
                // Renew an expired invitation only when both have free time;
                // hunger, rest and a different companion keep their priority.
                if(FamilyActionReady(mother)&&FamilyActionReady(father)&&f.CompanionTargetId==null&&father.Family.CompanionTargetId==null&&
                    State.WorldSeconds>=f.NextSocialAt&&State.WorldSeconds>=father.Family.NextSocialAt&&
                    f.CareTargetId==null&&father.Family.CareTargetId==null)
                    BeginCompanionship(mother,father);
                if(f.ColdStorage.Stored||!FamilyRules.WillPair(mother,father)||!FamilyRules.WillPair(father,mother)||
                    !FamilyActionReady(mother)||!FamilyActionReady(father)||f.CareTargetId!=null||father.Family.CareTargetId!=null||
                    f.CompanionTargetId!=father.Id||father.Family.CompanionTargetId!=mother.Id||
                    mother.Interaction.Lowering||father.Interaction.Lowering||
                    Math.Abs(mother.Interaction.VelocityX)+Math.Abs(mother.Interaction.VelocityZ)>.05||
                    Math.Abs(father.Interaction.VelocityX)+Math.Abs(father.Interaction.VelocityZ)>.05||
                    !FamilyInContact(mother,father)||f.LastCompanionActivityAt!=State.WorldSeconds||
                    father.Family.LastCompanionActivityAt!=State.WorldSeconds||
                    InteractionGestureTarget==mother.Id||InteractionGestureTarget==father.Id)
                {p.Elapsed=0;continue;}
                // Waiting near each other is not preparation: both must have
                // actually spent this tick together after their own needs.
                p.Elapsed+=bodyDt;
                if(p.Elapsed<6)continue;
                string? eligibility=PregnancyEligibility(mother,father,p.Method);
                if(eligibility!=null){p.Elapsed=0;f.LastEvent=eligibility;continue;}
                if(!FormPartnership(mother,father)){p.Elapsed=0;continue;}
                // Cross-ecology remains possible in either direction but rare;
                // failed attempts are spaced out and cannot be rerolled by loading.
                if(FamilyRules.CrossEcology(mother,father)&&Next()>=.02)
                {p.Phase=PregnancyPhase.None;p.Elapsed=0;p.RetryAt=State.WorldSeconds+300;ClearCompanionship(mother);f.LastEvent="它们接受了彼此，但这次没有开始孕育。";continue;}
                p.Phase=PregnancyPhase.Gestating;p.Elapsed=0;p.WaitingSeconds=0;p.EventId=NewFamilyId("birth");
                SettleBreedingInterest(mother,father);
                p.PlannedChildren=p.Method==BirthMethod.Live?1:2+(Next()<.25?1:0);
                ClearCompanionship(mother);f.LastEvent=p.Method==BirthMethod.Live?"胎生孕育开始了。":"茎生孕育开始了。";
                RecordInteraction(mother,"pregnancy",f.LastEvent,3);
                continue;
            }
            bool distressed=mother.Fullness<25||mother.Interaction.Hydration<25||mother.Pain>=40||mother.Interaction.Digestion.Symptomatic||mother.Condition==PhysicalCondition.Critical||
                mother.Interaction.Water.InWater||WaterEnvironment.IsInWater(State,mother);
            double burdenRate=p.Method==BirthMethod.Live?.22:.12;
            if(father!=null&&FamilyRules.CrossEcology(mother,father))burdenRate*=FamilyRules.Predator(mother.Body.SpeciesId)?1.15:1.5;
            p.Burden=Math.Clamp(p.Burden+bodyDt*(distressed?burdenRate:-.045),0,100);
            mother.Fullness=Math.Max(0,mother.Fullness-bodyDt*(p.Method==BirthMethod.Live?.022:.01));
            mother.Interaction.Hydration=Math.Max(0,mother.Interaction.Hydration-bodyDt*(p.Method==BirthMethod.Live?.018:.008));
            mother.Energy=Math.Max(0,mother.Energy-bodyDt*(p.Method==BirthMethod.Live?.02:.008));
            p.Elapsed=Math.Min(FamilyRules.GestationSeconds(p.Method),p.Elapsed+bodyDt*(distressed?(p.Decision==PregnancyDecision.ProtectYoung?.5:.25):1));
            if(ConsiderPregnancyRisk(mother,bodyDt))continue;
            if(p.Burden>=(p.Decision==PregnancyDecision.ProtectYoung?95:80))
            {EndPregnancy(mother,p.Method==BirthMethod.Live?"身体持续处于危险中，发生了生理性吸收；这次孕育未能继续。":"持续失养或伤病使茎生发育失败，需要照护母体。");continue;}
            if(p.Burden>=50)Hurt(mother,bodyDt*.035,"孕育负担",null);
            if(mother.Condition==PhysicalCondition.Dead)continue;
            if(p.Elapsed>=FamilyRules.GestationSeconds(p.Method)&&!f.ColdStorage.Stored)
                TryDeliverFamily(mother,father);
        }
    }

    private void TryDeliverFamily(Creature mother,Creature? father)
    {
        var p=mother.Family.Pregnancy;
        if(p.EventId==null||State.CompletedBirths.Contains(p.EventId))return;
        var i=mother.Interaction;
        if(mother.Condition!=PhysicalCondition.Active||i.Water.InWater||WaterEnvironment.IsInWater(State,mother)||i.Carried||i.Falling||i.Lowering||i.Height>.05||i.Restraint!=RestraintKind.None||i.Supine||
            Math.Abs(i.VelocityX)+Math.Abs(i.VelocityZ)>.05||
            i.EatingRemaining>0||i.Physiology.Active||InteractionGestureTarget==mother.Id||mother.RetreatRemaining>0||
            mother.Family.FlightRemaining>0||mother.Family.Conflict.TargetId!=null||mother.Family.ChildThreat.TargetId!=null)return;
        if(father==null){EndPregnancy(mother,"孕育记录缺少父体来源，停止结算并保留母体状态。");return;}
        var babies=new List<Creature>();
        // Reserve enough dry free space for the entire birth before settling any.
        for(int n=0;n<p.PlannedChildren;n++)
        {
            var probe=new Creature{Id=$"{p.EventId}-{n}",RegionId=mother.RegionId,IsYoung=true};
            bool found=false;
            for(double radius=1.4;radius<=4&&!found;radius+=.45)
                for(int angle=0;angle<24&&!found;angle++)
                {
                    double x=mother.X+Math.Cos(angle*Math.Tau/24)*radius,z=mother.Z+Math.Sin(angle*Math.Tau/24)*radius;
                    if(!CanOccupyRoom(probe,x,z)||babies.Any(b=>Distance(b.X,b.Z,x,z)<InteractionFootprint(b)+InteractionFootprint(probe)))continue;
                    probe.X=x;probe.Z=z;probe.TargetX=x;probe.TargetZ=z;found=true;
                }
            if(!found||State.Creatures.Count+babies.Count>=10000)
            {mother.Family.LastEvent="宝宝快出生了，需要母体旁边有干燥的空位置。";return;}
            babies.Add(probe);
        }
        // Only make ordinary activities yield after the complete birth has space.
        // Do not drop carried food or stop companionship merely while waiting.
        YieldActivitiesForDelivery(mother);
        foreach(var baby in babies)
        {
            baby.Name=$"{mother.Name}的宝宝{State.Creatures.Count(c=>c.Family.MotherId==mother.Id)+1}";
            baby.Body=new(){Id=baby.Id+"-body",SpeciesId=Next()<.5?mother.Body.SpeciesId:father.Body.SpeciesId,
                ReproductionRole=Next()<.5?ReproductionRole.Maternal:ReproductionRole.Paternal};
            baby.Registration=new(){IdentityId=baby.Id+"-identity"};
            double disposition=Next();
            baby.TrueDisposition=disposition<.03?new[]{"curious","shy","proud","greedy"}[(int)(Next()*4)]:disposition<.15?father.TrueDisposition:mother.TrueDisposition;
            baby.Courage=Math.Clamp(mother.Courage*.8+father.Courage*.2+(Next()-.5)*.08,0,1);
            baby.Patience=Math.Clamp(mother.Patience*.8+father.Patience*.2+(Next()-.5)*.08,0,1);
            baby.Aggression=Math.Clamp(mother.Aggression*.8+father.Aggression*.2+(Next()-.5)*.08,0,1);
            baby.Health=(p.Method==BirthMethod.Live?100:92)-p.Burden*.2;
            baby.Energy=p.Method==BirthMethod.Live?78:68;baby.Fullness=55;baby.Trust=10;baby.Attachment=5;
            baby.ParentId=mother.Id;
            baby.Interaction=new(){Enabled=true,Hydration=55,NextAutonomyAt=State.WorldSeconds+10,
                Accessory=new(){Id=baby.Id+"-accessory",X=baby.X,Z=baby.Z}};
            baby.Family=new(){MotherId=mother.Id,FatherId=father.Id,BirthEventId=p.EventId,BirthMethod=p.Method,
                CaregiverIds=[mother.Id,father.Id],LastEvent="刚刚出生，需要照护。"};
            baby.Family.Peers[mother.Id]=new(){Familiarity=20,Affection=25};
            baby.Family.Peers[father.Id]=new(){Familiarity=10,Affection=15};
            if(p.Method==BirthMethod.Live)
                foreach(var knowledge in mother.Family.LifeKnowledge.OrderBy(x=>x.Key,StringComparer.Ordinal).Take(2))
                {
                    baby.Family.LifeKnowledge[knowledge.Key]=knowledge.Value;
                    if(FamilyRules.KnowledgeName(knowledge.Key).Length>0)
                        baby.Family.LearningSources[knowledge.Key]=new(){Source=LifeKnowledgeSource.Inherited,FromId=mother.Id,At=State.WorldSeconds};
                }
            State.Creatures.Add(baby);
            mother.Family.Peers[baby.Id]=new(){Familiarity=20,Affection=30};
            father.Family.Peers[baby.Id]=new(){Familiarity=10,Affection=20};
        }
        State.CompletedBirths.Add(p.EventId);
        Hurt(mother,p.Method==BirthMethod.Live?7+p.Burden*.12:2+p.Burden*.05,"生产负担",null);
        EndPregnancy(mother,$"{babies.Count}只宝宝出生了，母体需要恢复，幼体需要吃喝与保暖。"+
            (p.Decision==PregnancyDecision.ProtectYoung?"这次曾在危险中主动坚持保胎。":""));
    }

    private void YieldActivitiesForDelivery(Creature mother)
    {
        CancelWildSelfCare(mother,"生产需要占用身体，自行护伤已中断；尚未获得护理效果。");
        var i=mother.Interaction;
        i.ActiveCue=null;i.CueElapsed=0;i.CueTravel=0;i.CueUnderstood=false;i.CueDemonstration=false;
        i.FollowPlayer=false;i.WaitRemaining=0;i.Resting=false;i.RestRemaining=0;
        EndHumanCompany(mother);CancelFamilyCare(mother);ClearCompanionship(mother);
        mother.Activity="生产后休息";
    }
}

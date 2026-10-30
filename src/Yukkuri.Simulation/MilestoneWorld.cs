namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public static SimulationConfig MilestoneConfig()=>new() {HungerPerSecond=.055,EnergyPerSecond=.035,MoveSpeed=.9};
    public static WorldSimulation CreateMilestone(bool sandbox=false, ulong seed=20261027)
    {
        var config=MilestoneConfig();
        var sim=Create(5,sandbox,config);var s=sim.State;
        s.MilestoneScenario=true;s.RandomState=seed==0?20261027:seed;s.Money=160;s.Food=35;
        s.Regions[0].Facilities=[new(){Kind=FacilityKind.Feeder,X=-3,Z=-1,Stock=16},new(){Kind=FacilityKind.Bed,X=3,Z=-3},new(){Kind=FacilityKind.Bed,X=5,Z=-3}];
        var names=new[]{"小铃","松果","豆豆","米糕","小麦","芽芽","荠菜","刺头","阿栗","小葵","灰帽"};
        for(int i=0;i<s.Creatures.Count;i++)
        {
            var c=s.Creatures[i];c.Name=names[i];c.Id=$"m01-oil-{i:00}";c.Body.Id=$"m01-body-{i:00}";c.Registration.IdentityId=$"m01-identity-{i:00}";
            c.Body.SpeciesId=i%2==0?"reimu":"marisa";c.TrueDisposition=new[]{"shy","brave","greedy","gentle","curious","shy","curious","proud","curious","gentle","proud"}[i];
            c.X=i<5?-3+i*1.8:-5+(i-5)*2;c.Z=i<5?2.2:1.5+(i%2)*2;
            c.TargetX=c.X;c.TargetZ=c.Z;c.Trust=i<5?30:5;c.Belonging=i<5?65:0;c.Attachment=i<5?20:0;
            c.Fullness=65+i%3*5;c.Energy=85;c.Courage=c.TrueDisposition switch{"brave"=>.8,"proud"=>.75,"shy"=>.22,_=>.5};
            c.Aggression=c.TrueDisposition=="proud"?.8:.12;c.Patience=c.TrueDisposition=="greedy"?.22:.6;
            c.DecisionIn=3+i*.7;c.BehaviorCooldown=45+i*7;c.Observation.Hypothesis="尚需通过行为了解它";
        }
        s.Creatures[0].IsElder=true;s.Creatures[1].GuardDuty=true;s.Creatures[1].GuardSkill=22;
        s.Creatures[2].IsYoung=true;s.Creatures[2].ParentId=s.Creatures[0].Id;s.Creatures[2].SharingSkill=5;
        s.Creatures[3].IsElder=true;s.Creatures[3].GuardSkill=10;
        s.Creatures[5].Health=72;s.Creatures[5].Pain=10;s.Creatures[5].Speech="这里……安全吗？";
        s.Creatures[7].GuardSkill=35;s.Creatures[7].Fullness=45;s.Creatures[7].Belonging=0;
        s.SelectedId=s.Creatures[0].Id;s.Journal.Clear();
        sim.Log("雇员小岚：先和油库里熟悉起来吧。小铃很谨慎，豆豆还在学习分享。");
        sim.Log("今天的安排：照护与训练、去林缘观察、把愿意跟随的油带回，照看乐园边界。");
        return sim;
    }
    private static double Clamp100(double v)=>Math.Clamp(v,0,100);
    private bool IsAvailable(Creature c)=>c.Condition==PhysicalCondition.Active&&c.Health>0;
    private void React(Creature c,Expression expression,string speech,string reaction,double seconds=4)
    {
        if(c.Condition==PhysicalCondition.Dead)return;
        c.Interaction.ReactionSituation="";c.Interaction.ReactionRefused=false;
        c.Interaction.ReactionPose="idle";c.Interaction.ReactionPoseRemaining=0;
        if(c.Condition==PhysicalCondition.Critical){expression=Expression.Exhausted;speech="……";}
        else if(c.Pain>=40&&expression is Expression.Content or Expression.Delighted){expression=Expression.Hurt;speech="还疼着……";}
        else if(c.Fear>=68&&expression is Expression.Content or Expression.Delighted){expression=Expression.Frightened;speech="先别靠近……";}
        c.Expression=expression;c.Speech=c.Condition==PhysicalCondition.Critical?speech:MouthFunction.FormatSpeech(c,speech);
        c.LastReaction=reaction;c.ReactionSerial++;c.ReactionRemaining=seconds;
    }
    private void Remember(Creature c,Incident incident,string text,double weight)
    {
        c.PersonalMemories.Add(new(){IncidentId=incident.Id,At=State.WorldSeconds,Text=text,EmotionalWeight=weight});
        if(c.PersonalMemories.Count>48)c.PersonalMemories.RemoveAt(0);
        c.Memories.Add(text);if(c.Memories.Count>30)c.Memories.RemoveAt(0);
    }
    private void Learn(Creature c,Incident incident,KnowledgeSource source,bool knowsActor,string? informant=null)
    {
        var previous=c.Knowledge.Find(k=>k.IncidentId==incident.Id);
        if(previous!=null)
        {
            if(knowsActor&&!previous.KnowsActor){previous.KnowsActor=true;previous.Source=source;previous.InformantId=informant;}
            return;
        }
        c.Knowledge.Add(new(){IncidentId=incident.Id,Source=source,KnowsActor=knowsActor,InformantId=informant,LearnedAt=State.WorldSeconds});
        if(c.Knowledge.Count>80)c.Knowledge.RemoveAt(0);
    }
    public bool Knows(Creature c,string? incidentId)=>incidentId!=null&&c.Knowledge.Any(k=>k.IncidentId==incidentId&&k.KnowsActor);
    private Incident Emit(IncidentKind kind,Creature target,string? actorId,string description,double severity=0,string? reason=null,Creature? visibleActor=null)
    {
        var incident=new Incident{Id=$"event-{++State.IncidentSequence:000000}",Kind=kind,TargetId=target.Id,ActorId=actorId,
            RegionId=target.RegionId,X=target.X,Z=target.Z,At=State.WorldSeconds,Description=description,Severity=severity,ReasonIncidentId=reason};
        State.Incidents.Add(incident);
        TrimIncidentHistory();
        foreach(var c in State.Creatures.Where(c=>c.RegionId==target.RegionId&&c.Condition!=PhysicalCondition.Dead))
        {
            if(State.InteractionPlaytest&&(c.Family.ColdStorage.Stored||target.Family.ColdStorage.Stored))continue;
            double distance=Distance(c.X,c.Z,target.X,target.Z);
            bool sees=(State.InteractionPlaytest?FamilyVisible(c,target):distance<=4.5)||c.Id==actorId||c.Id==target.Id;
            if(sees)Learn(c,incident,KnowledgeSource.Witness,
                visibleActor==null||c.Id==target.Id||c.Id==actorId||FamilyVisible(c,visibleActor));
            else if(severity>=4&&distance<=7)Learn(c,incident,KnowledgeSource.Heard,false);
        }
        if(target.RegionId==State.ActiveRegionId||actorId=="player")State.PlayerKnownIncidents.Add(incident.Id);
        return incident;
    }
    private void TrimIncidentHistory()
    {
        if(State.Incidents.Count>500)
        {
            // Keep facts still needed to judge an ongoing offense or encounter.
            var expired=State.Incidents.FirstOrDefault(e=>e.Id!=State.Threat.IncidentId&&!State.Creatures.Any(c=>c.CurrentOffenseId==e.Id));
            if(expired!=null){
            string oldest=expired.Id;State.Incidents.Remove(expired);
            State.PlayerKnownIncidents.Remove(oldest);
            foreach(var c in State.Creatures)c.Knowledge.RemoveAll(k=>k.IncidentId==oldest);
            }
        }
    }
    private void WitnessCorrection(Incident incident,Creature target,double severity)
    {
        foreach(var witness in State.Creatures.Where(c=>c.Id!=target.Id&&c.RegionId==target.RegionId&&IsAvailable(c)))
        {
            var knowledge=witness.Knowledge.Find(k=>k.IncidentId==incident.Id);
            if(knowledge==null)continue;
            if(!knowledge.KnowsActor)
            {
                witness.Fear=Clamp100(witness.Fear+severity*.18);
                Remember(witness,incident,"听到了不安的声音，还不知道发生了什么。",severity*.2);continue;
            }
            bool knowsReason=Knows(witness,incident.ReasonIncidentId);
            bool family=witness.Id==target.ParentId||witness.ParentId==target.Id;
            if(knowsReason&&witness.Belonging>=30&&severity<=15)
            {
                witness.Trust=Clamp100(witness.Trust+1.5);witness.Fear=Math.Max(0,witness.Fear-2);
                React(witness,Expression.Content,"这里是我们的家！","知道事情经过，认为这是保护乐园。",3);
                Remember(witness,incident,"知道它刚才的行为，认可主人制止它。",3);
            }
            else
            {
                double intensity=family?1.45:1;
                witness.Fear=Clamp100(witness.Fear+severity*.65*intensity);
                witness.Trust=Clamp100(witness.Trust-severity*.22*intensity);
                React(witness,Expression.Frightened,family?"不要再伤害它了！":"为什么……？","害怕地退开，注意着主人的动作。",5);
                Remember(witness,incident,knowsReason?"虽然知道起因，但这种伤害让自己害怕。":"不知道它做错了什么，却看见它受到伤害。",severity);
            }
        }
    }
    private void TickMilestoneWorld(double dt)
    {
        State.SocialTimer+=dt;
        if(State.SocialTimer>=12)
        {
            State.SocialTimer-=12;
            foreach(var speaker in State.Creatures.Where(IsAvailable))
            {
                // A mouth injury affects telling a fact, not hearing one. Finishing a
                // mouthful also takes precedence over a complete spoken account.
                if(!MouthFunction.CanSpeakClearly(speaker)||speaker.Interaction.EatingRemaining>0)continue;
                var fact=speaker.Knowledge.LastOrDefault(k=>k.KnowsActor);
                if(fact==null)continue;
                var incident=State.Incidents.Find(e=>e.Id==fact.IncidentId);if(incident==null)continue;
                var listener=State.Creatures.FirstOrDefault(c=>c.Id!=speaker.Id&&c.RegionId==speaker.RegionId&&IsAvailable(c)&&Distance(c.X,c.Z,speaker.X,speaker.Z)<2.5&&!Knows(c,incident.Id));
                if(listener==null)continue;
                if(incident.Kind==IncidentKind.Death)
                {
                    var deceased=State.Creatures.Find(c=>c.Id==incident.TargetId);
                    if(deceased!=null&&speaker.Family.KnownLosses.ContainsKey(deceased.Id))
                        TellFamilyLoss(speaker.Id,listener.Id,deceased.Id);
                    continue;
                }
                Learn(listener,incident,KnowledgeSource.Told,true,speaker.Id);
                Remember(listener,incident,$"听{speaker.Name}说：{incident.Description}",1);
            }
        }
        TickThreat(dt);
        RefreshCurrentHelp();
        State.ScenarioComplete=new[]{"feed","affection","train","admit","defense","contract"}.All(State.Progress.Completed.Contains);
    }
    private void OnMilestoneFeed(Creature c,bool staff)
    {
        if(c.Condition==PhysicalCondition.Recovering)
            React(c,Expression.Shy,"谢谢……慢一点。","躺着慢慢接受食物，继续休养。",4);
        else React(c,Expression.Content,c.TrueDisposition=="greedy"?"还想再吃一口！":"谢谢，慢慢吃。","张嘴咀嚼，脸颊鼓起来。",4);
        if(staff)return;
        c.Attachment=Clamp100(c.Attachment+1);c.Familiarity++;c.CareSessionCount++;
        State.Progress.CareActions++;State.Progress.Completed.Add("feed");
        var e=Emit(IncidentKind.Care,c,"player",$"给{c.Name}喂食");Remember(c,e,"主人带来了食物。",2);
    }
    public InteractionResult InviteToSanctuary()
    {
        var c=Selected;
        if(c==null||c.RegionId=="home"||State.Mode==PlayMode.CloseUp||!IsAvailable(c))return new(false,"先在野外选择一只可以移动的油库里。","",Expression.Neutral);
        if(c.IsIntruder||c.CurrentOffenseId!=null||c.Aggression>.65)return new(false,"它还在敌对或警戒，暂时不愿跟随。先观察并处理眼前的危险。","退后盯着你。",Expression.Defiant);
        if(c.Trust<15||c.Fear>60)
        {
            React(c,Expression.Shy,"还不知道能不能相信你……","向后缩了缩。",4);
            return new(false,"它还不够信任你。温和接触、喂食或保护它，再试着邀请。",c.LastReaction,c.Expression);
        }
        var e=Emit(IncidentKind.Rescue,c,"player",$"{c.Name}愿意跟随主角前往乐园观察区");Remember(c,e,"有人愿意带自己去安全的地方。",8);
        CancelWildSelfCare(c,"接受护送前往乐园，自行护伤已中断；这次机会已经用过。");
        c.RegionId="home";c.Quarantine=true;c.X=-6;c.Z=5;c.TargetX=c.X;c.TargetZ=c.Z;c.Belonging=20;c.Activity="在新环境中观察";
        React(c,Expression.Shy,"这里就是乐园吗？","好奇地张望，仍有一点紧张。",5);
        State.Progress.Rescued++;State.Progress.Completed.Add("rescue");
        State.SelectedId=State.Creatures.FirstOrDefault(x=>x.RegionId==State.ActiveRegionId)?.Id;
        Log($"{c.Name}愿意跟随你，已抵达观察区。返回后可决定接纳。");
        return new(true,"已护送到乐园观察区。返回乐园继续照护，并由你决定接纳。",c.LastReaction,c.Expression);
    }
    public bool AdmitSelected()
    {
        var c=Selected;if(c==null||!c.Quarantine||c.RegionId!="home"||State.Mode==PlayMode.CloseUp)return false;
        c.Quarantine=false;c.Belonging=Math.Max(45,c.Belonging);c.TargetX=-2;c.TargetZ=2;
        React(c,Expression.Delighted,"这里也有我的位置了！","开心地轻轻弹跳。",5);
        State.Progress.Completed.Add("admit");Log($"你接纳了{c.Name}。它开始把乐园当作家；牌照仍需另行申请。");return true;
    }
    public const int MedicineBundlePrice=15;
    public const int MedicineBundleCount=3;
    public bool BuyMedicine()
    {
        if(State.Medicine>int.MaxValue-MedicineBundleCount||!State.UnlimitedResources&&State.Money<MedicineBundlePrice)return false;
        if(!State.UnlimitedResources)State.Money-=MedicineBundlePrice;
        State.Medicine+=MedicineBundleCount;Log($"补给：{MedicineBundleCount}份护理包入库。");return true;
    }
    public InteractionResult ClaimCareContract()
    {
        if(State.WorldSeconds<State.Progress.NextContractAt)return new(false,$"下一次养护记录整理还需 {Math.Ceiling(State.Progress.NextContractAt-State.WorldSeconds)} 秒。","",Expression.Neutral);
        var home=State.Creatures.Where(c=>c.RegionId=="home"&&!c.Quarantine&&IsAvailable(c)).ToArray();
        if(home.Length==0||home.Count(c=>c.Health>=65&&c.Fullness>=55)<Math.Min(3,home.Length))return new(false,"至少照顾好3只园内油库里：身体≥65、饱腹≥55。不足3只时需全部达标。","",Expression.Neutral);
        if(State.Progress.CareActions<3)return new(false,"需要再完成3次主动照护，才能整理新一份养护记录。","",Expression.Neutral);
        State.Progress.CareActions-=3;State.Progress.CareContracts++;State.Progress.NextContractAt=State.WorldSeconds+90;
        int amount=30+Math.Min(10,State.Progress.Rescued*5);State.Money+=amount;
        State.Progress.Completed.Add("contract");Log($"完成第{State.Progress.CareContracts}次养护委托，获得{amount}资金。");
        return new(true,$"获得 {amount} 资金。照护、补给与扩建可以继续进行。","",Expression.Content);
    }
}

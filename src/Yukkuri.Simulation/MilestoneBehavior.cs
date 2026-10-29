namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private bool TickMilestoneCreature(Creature c,RegionState region,double dt)
    {
        AdvanceBodyInjuries(c,dt);
        if(c.Condition==PhysicalCondition.Dead)return true;
        c.ReactionRemaining=Math.Max(0,c.ReactionRemaining-dt);
        c.TrainingFatigue=Math.Max(0,c.TrainingFatigue-dt*.08);
        c.Strain=Math.Max(0,c.Strain-dt*.02);
        c.Pain=Math.Max(0,c.Pain-dt*(c.Fullness>40?.035:.01));
        c.BehaviorCooldown-=dt;c.SocialCooldown-=dt;
        if(c.ReactionRemaining==0)
        {
            c.Speech="";
            c.Expression=c.Pain>25?Expression.Hurt:c.Fear>60?Expression.Frightened:c.Energy<20?Expression.Exhausted:
                c.IsIntruder&&State.Threat.Phase==ThreatPhase.Intruding?Expression.Defiant:Expression.Neutral;
        }
        if(c.OffenseRemaining>0)
        {
            c.OffenseRemaining-=dt;if(c.OffenseRemaining<=0&&!c.IsIntruder)ResolveOffense(c);
        }
        if(c.Condition==PhysicalCondition.Critical)
        {
            c.CriticalRemaining=Math.Max(0,c.CriticalRemaining-dt);c.Activity=$"重伤待救 · {Math.Ceiling(c.CriticalRemaining)}秒";
            if(c.CriticalRemaining<=0)
            {
                if(State.Settings.LethalInjuries)Die(c,"错过救治时限");
                else {c.Condition=PhysicalCondition.Recovering;c.Health=1;c.RecoveryRemaining=90;c.Activity="保护模式：恢复中";}
            }
            return true;
        }
        if(c.Condition==PhysicalCondition.Recovering)
        {
            c.Activity="休养恢复";c.RecoveryRemaining=Math.Max(0,c.RecoveryRemaining-dt*(c.Fullness>35?1:.3));
            c.Energy=Math.Min(100,c.Energy+dt*.4);
            if(c.RecoveryRemaining<=0){c.Condition=PhysicalCondition.Active;c.Health=Math.Max(25,c.Health);React(c,Expression.Shy,"还能慢慢走……","恢复了活动能力，还需要继续照护。",5);}
            return true;
        }
        if(c.Interaction.EatingRemaining>0){c.Activity="慢慢咽下嘴里的这一口";return true;}
        if(c.IsIntruder)return true; // The shared encounter director owns these positions.
        if(State.Threat.Phase==ThreatPhase.Intruding&&State.Threat.DefenderId==c.Id)return true;
        if(c.RetreatRemaining>0)
        {
            c.RetreatRemaining=Math.Max(0,c.RetreatRemaining-dt);MoveToward(c,c.TargetX,c.TargetZ,dt,1.3);c.Activity="退到安全距离";return true;
        }
        if(c.Fear>70&&c.RegionId==State.ActiveRegionId&&Distance(c.X,c.Z,State.PlayerX,State.PlayerZ)<3)
        {
            double dx=c.X-State.PlayerX,dz=c.Z-State.PlayerZ;
            MoveToward(c,Math.Clamp(c.X+dx,-8,8),Math.Clamp(c.Z+dz,-8,8),dt,1.2);
            c.Activity="躲避主人";return true;
        }
        if(c.RegionId=="wild"&&c.Aggression>.7&&c.BehaviorCooldown<=0)
        {
            var victim=State.Creatures.Where(x=>x.Id!=c.Id&&x.RegionId=="wild"&&IsAvailable(x)&&x.Aggression<.35)
                .MinBy(x=>Distance(c.X,c.Z,x.X,x.Z));
            if(victim!=null&&Distance(c.X,c.Z,victim.X,victim.Z)<6)
            {
                c.Activity="靠近别人的食物";
                MoveToward(c,victim.X,victim.Z,dt,1.2);
                if(Distance(c.X,c.Z,victim.X,victim.Z)<1.2)
                {
                    var e=Emit(IncidentKind.Attack,victim,c.Id,$"{c.Name}推撞了{victim.Name}，试图抢占食物",6);
                    c.CurrentOffenseId=e.Id;c.OffenseRemaining=80;c.BehaviorCooldown=25;
                    Hurt(victim,4,"野外冲突",c.Id);victim.Fear=Clamp100(victim.Fear+8);
                    React(victim,Expression.Calling,"不要抢，帮帮我！","一边躲闪，一边向周围求助。",6);
                    victim.RetreatRemaining=6;victim.TargetX=-7;victim.TargetZ=6;
                    React(c,Expression.Defiant,"这里的都是我的！","扬起脸，挡住对方的去路。",5);
                    Remember(victim,e,$"被{c.Name}推撞，感到害怕。",6);
                    if(State.ActiveRegionId=="wild")
                    {
                        if(State.Creatures.Find(x=>x.Id==State.HelpTargetId)?.Condition!=PhysicalCondition.Critical)State.HelpTargetId=victim.Id;
                        Log($"{victim.Name}正在被欺负。可以选中它，选择保护。");
                    }
                }
                return true;
            }
        }
        if(c.RegionId=="home"&&!c.Quarantine&&c.TrueDisposition=="greedy"&&c.BehaviorCooldown<=0&&c.Fullness<75)
        {
            c.BehaviorCooldown=45;
            var feeder=region.Facilities.FirstOrDefault(f=>f.Kind==FacilityKind.Feeder&&f.Stock>=2&&Distance(c.X,c.Z,f.X,f.Z)<3);
            var companion=State.Creatures.FirstOrDefault(x=>x.Id!=c.Id&&x.RegionId==c.RegionId&&IsAvailable(x)&&Distance(c.X,c.Z,x.X,x.Z)<4);
            if(feeder!=null&&companion!=null)
            {
                if(c.SharingSkill>=35||Next()<c.SharingSkill/35)
                {
                    React(c,Expression.Content,"给大家留一份。","来到食盆前后，主动给同伴让出位置。",5);
                    c.Activity="等待同伴取食";
                    if(State.ActiveRegionId=="home")Log($"{c.Name}这次忍住了抢食，给同伴留出了位置。");
                    return false;
                }
                feeder.Stock-=2;c.Fullness=Clamp100(c.Fullness+12);BeginLegacyIntake(c);
                var e=Emit(IncidentKind.Theft,c,c.Id,$"{c.Name}抢了两份食物，没有给同伴留位置",3);
                c.CurrentOffenseId=e.Id;c.OffenseRemaining=40;
                React(c,Expression.Defiant,"先到的就是我的！","鼓着脸，挡在食盆前。",6);
                if(State.ActiveRegionId=="home")Log($"{c.Name}抢占了食物，附近油库里注意到了。");
                var elder=State.Creatures.Where(x=>x.Id!=c.Id&&x.RegionId=="home"&&IsAvailable(x)&&(x.IsElder||x.Id==c.ParentId)&&Knows(x,e.Id))
                    .OrderByDescending(x=>x.Id==c.ParentId).FirstOrDefault();
                if(elder!=null&&Next()<.65)
                {
                    c.SharingSkill=Clamp100(c.SharingSkill+3);c.Obedience=Clamp100(c.Obedience+2);c.Fear=Clamp100(c.Fear+1);
                    React(elder,Expression.Angry,"要给其他油留一份！","靠近并制止了抢食行为。",5);
                    React(c,Expression.Shy,"知道啦……","退开一些，把位置让出来。",5);
                    var correction=Emit(IncidentKind.Correction,c,elder.Id,$"{elder.Name}制止了{c.Name}抢食",2,e.Id);
                    Remember(c,correction,$"{elder.Name}教自己给同伴留一份。",3);
                    c.Relationships[elder.Id]=Math.Clamp(c.Relationships.GetValueOrDefault(elder.Id)+1,-100,100);
                    ResolveOffense(c);
                    Log($"{elder.Name}主动管教了{c.Name}，让它给同伴留出位置。");
                }
            }
        }
        if(c.Fullness>60&&c.Energy>50&&c.Pain<20&&c.Health<100)c.Health=Math.Min(100,c.Health+dt*.025);
        return false;
    }
    private void MoveToward(Creature c,double x,double z,double dt,double speed=1)
    {
        if(!CanSelfMove(c))return;
        if(UsesGroundFoot(c))speed*=Math.Clamp(c.Interaction.FootIntegrity/100,.3,1);
        c.TargetX=Math.Clamp(x,-8,8);c.TargetZ=Math.Clamp(z,-8,8);
        double distance=Distance(c.X,c.Z,c.TargetX,c.TargetZ);
        if(distance>.01){double step=Math.Min(distance,Config.MoveSpeed*speed*dt);c.X+=(c.TargetX-c.X)/distance*step;c.Z+=(c.TargetZ-c.Z)/distance*step;}
    }
    private void TickThreat(double dt)
    {
        var threat=State.Threat;threat.PhaseTime+=dt;
        if(!State.Settings.Invasions&&threat.Phase is ThreatPhase.Quiet or ThreatPhase.Resolved)return;
        if(threat.Phase==ThreatPhase.Quiet&&State.WorldSeconds>=threat.NextAt)
        {
            var candidate=State.Creatures.Where(c=>c.RegionId=="wild"&&IsAvailable(c)&&c.Aggression>.6&&c.RetreatRemaining<=0).OrderByDescending(c=>c.Aggression).FirstOrDefault();
            if(candidate==null){threat.NextAt=State.WorldSeconds+60;return;}
            threat.Phase=ThreatPhase.Approaching;threat.PhaseTime=0;threat.IntruderId=candidate.Id;
            threat.DefenderId=null;threat.HelpRequested=false;threat.PlayerResponded=false;threat.Sequence++;
            Log("雇员小岚：林缘有动静，留意乐园入口。");
        }
        var intruder=State.Creatures.Find(c=>c.Id==threat.IntruderId);
        if(intruder==null)return;
        if(threat.Phase==ThreatPhase.Approaching&&threat.PhaseTime>=10)
        {
            if(!IsAvailable(intruder)){threat.Phase=ThreatPhase.Quiet;threat.NextAt=State.WorldSeconds+60;return;}
            intruder.RegionId="home";intruder.X=-7.8;intruder.Z=3.8;intruder.IsIntruder=true;intruder.Quarantine=false;
            var e=Emit(IncidentKind.Intrusion,intruder,intruder.Id,$"{intruder.Name}闯入乐园并威吓油库里",5);
            intruder.CurrentOffenseId=e.Id;intruder.OffenseRemaining=120;threat.IncidentId=e.Id;
            threat.Phase=ThreatPhase.Intruding;threat.PhaseTime=0;threat.EncounterCooldown=4;
            React(intruder,Expression.Defiant,"这里的食物归我啦！","昂着脸，向食盆和园内油库里靠近。",6);
            State.PlayerKnownIncidents.Add(e.Id);Log($"{intruder.Name}闯入乐园。油库里正在守护自己的家。");
        }
        if(threat.Phase==ThreatPhase.Intruding)
        {
            if(!IsAvailable(intruder)){ResolveThreat(false);return;}
            var defender=State.Creatures.Find(c=>c.Id==threat.DefenderId);
            if(defender==null)
            {
                defender=State.Creatures.Where(c=>c.RegionId=="home"&&!c.Quarantine&&!c.IsIntruder&&IsAvailable(c)&&c.Belonging>40&&c.GuardDuty)
                    .OrderByDescending(c=>c.GuardSkill+c.Courage*20).FirstOrDefault();
                if(defender!=null)threat.DefenderId=defender.Id;
            }
            if(intruder.Interaction.EatingRemaining>0)
                intruder.Activity="慢慢咽下嘴里的这一口";
            else if(defender!=null&&IsAvailable(defender)&&defender.Health>35&&defender.Energy>15&&defender.Interaction.EatingRemaining<=0)
            {
                defender.Activity="守护乐园";intruder.Activity="试图闯过守卫";
                MoveToward(defender,intruder.X,intruder.Z,dt,1.3);
                MoveToward(intruder,defender.X,defender.Z,dt,.8);
                var intrusion=State.Incidents.Find(e=>e.Id==threat.IncidentId);
                if(intrusion!=null&&Distance(defender.X,defender.Z,intruder.X,intruder.Z)<=4.5)
                    Learn(defender,intrusion,KnowledgeSource.Witness,true);
                if(Distance(defender.X,defender.Z,intruder.X,intruder.Z)<1.4)
                {
                    threat.EncounterCooldown-=dt;
                    if(threat.EncounterCooldown<=0)
                    {
                        threat.EncounterCooldown=4;
                        double defense=defender.GuardSkill*.7+defender.Courage*25+defender.Energy*.1;
                        double attack=intruder.GuardSkill*.6+intruder.Aggression*25+intruder.Energy*.1;
                        defender.Energy=Math.Max(0,defender.Energy-5);intruder.Energy=Math.Max(0,intruder.Energy-4);
                        if(defense+Next()*10>=attack)
                        {
                            intruder.Fear=Clamp100(intruder.Fear+12);intruder.X-=.35;
                            React(defender,Expression.Angry,"不许欺负这里的油！","挡住去路，把入侵者推回去。",3);
                            if(intruder.Fear>=45||intruder.Energy<20){BeginRetreat(intruder,false);return;}
                        }
                        else
                        {
                            Hurt(defender,4+(attack-defense)*.08,"抵御入侵",intruder.Id);
                            var clash=Emit(IncidentKind.Attack,defender,intruder.Id,$"{defender.Name}在护园时被{intruder.Name}撞伤",6);
                            Remember(defender,clash,"为了保护乐园而受了伤。",5);
                            React(defender,Expression.Hurt,"这里……不能给你！","努力挡住入侵者，身体已经疲倦。",4);
                        }
                        if(defender.Health<80||defender.Energy<50)CallForHelp(defender);
                    }
                }
            }
            else
            {
                if(defender!=null&&defender.Interaction.EatingRemaining<=0)CallForHelp(defender);
                var victim=State.Creatures.FirstOrDefault(c=>c.RegionId=="home"&&!c.IsIntruder&&IsAvailable(c));
                if(victim!=null)
                {
                    MoveToward(intruder,victim.X,victim.Z,dt,.7);
                    if(Distance(intruder.X,intruder.Z,victim.X,victim.Z)<1.3)
                    {
                        threat.EncounterCooldown-=dt;
                        if(threat.EncounterCooldown<=0){threat.EncounterCooldown=7;Hurt(victim,5,"入侵冲突",intruder.Id);CallForHelp(victim);}
                    }
                }
            }
            if(threat.PhaseTime>100)BeginRetreat(intruder,false);
        }
        if(threat.Phase==ThreatPhase.Retreating)
        {
            MoveToward(intruder,-8,6,dt,1.7);intruder.Activity="离开乐园";
            if(threat.PhaseTime>=8||Distance(intruder.X,intruder.Z,-8,6)<.3)
            {
                intruder.RegionId="wild";intruder.X=6;intruder.Z=-5;intruder.IsIntruder=false;
                intruder.RetreatRemaining=25;intruder.TargetX=7;intruder.TargetZ=-7;ResolveOffense(intruder);ResolveThreat(threat.PlayerResponded);
            }
        }
        if(threat.Phase==ThreatPhase.Resolved&&threat.PhaseTime>20){threat.Phase=ThreatPhase.Quiet;threat.PhaseTime=0;}
    }
    private void CallForHelp(Creature defender)
    {
        if(defender.Condition==PhysicalCondition.Dead)return;
        var current=State.Creatures.Find(c=>c.Id==State.HelpTargetId);
        if(State.Threat.HelpRequested&&current!=null&&current.Condition!=PhysicalCondition.Dead)return;
        State.Threat.HelpRequested=true;
        if(State.Creatures.Find(c=>c.Id==State.HelpTargetId)?.Condition!=PhysicalCondition.Critical)State.HelpTargetId=defender.Id;
        React(defender,Expression.Calling,"主人，帮帮我们！",defender.Condition==PhysicalCondition.Active?"向主人呼喊，勉强守在入口。":"虚弱地呼救，等待主人帮助。",10);
        var e=Emit(IncidentKind.Help,defender,defender.Id,$"{defender.Name}抵御吃力，呼喊主人帮助",8);
        State.PlayerKnownIncidents.Add(e.Id);Log($"{defender.Name}：主人，帮帮我们！可以查看求助并介入。");
    }
    private void BeginRetreat(Creature intruder,bool player)
    {
        intruder.RetreatRemaining=20;intruder.TargetX=-8;intruder.TargetZ=6;
        if(intruder.IsIntruder&&State.Threat.IntruderId==intruder.Id)
        {State.Threat.Phase=ThreatPhase.Retreating;State.Threat.PhaseTime=0;State.Threat.PlayerResponded|=player;}
        else {intruder.TargetX=7;intruder.TargetZ=-7;intruder.BehaviorCooldown=45;}
        React(intruder,Expression.Frightened,"知道了，我走就是……","后退着离开，仍回头观察。",5);
    }
    private void ResolveThreat(bool player)
    {
        var intruder=State.Creatures.Find(c=>c.Id==State.Threat.IntruderId);
        if(intruder!=null){intruder.IsIntruder=false;ResolveOffense(intruder);}
        State.Threat.Phase=ThreatPhase.Resolved;State.Threat.PhaseTime=0;State.Threat.NextAt=State.WorldSeconds+180;
        if(State.HelpTargetId==State.Threat.DefenderId&&State.Creatures.Find(c=>c.Id==State.HelpTargetId)?.Condition!=PhysicalCondition.Critical)State.HelpTargetId=null;
        State.Progress.Defenses++;State.Progress.Completed.Add("defense");
        Log(player?"你与油库里一起守住了乐园。记得照护受伤的同伴。":"油库里守护结束，乐园暂时平静下来。");
    }
    public InteractionResult ProtectSelected()
    {
        var target=Selected;
        if(target==null||target.Condition==PhysicalCondition.Dead)return new(false,"先选择需要帮助的油库里。","",Expression.Neutral);
        if(target.RegionId!=State.ActiveRegionId)return new(false,"先前往它所在的地区。","",Expression.Neutral);
        var attacker=ThreateningAttacker(target);
        if(attacker==null)return new(false,"目前没有发现针对它的威胁，可以继续观察和照护。","",Expression.Neutral);
        string? reason=attacker.CurrentOffenseId;
        BeginRetreat(attacker,true);
        if(!attacker.IsIntruder)ResolveOffense(attacker);
        target.Trust=Clamp100(target.Trust+12);target.Fear=Math.Max(0,target.Fear-12);target.Attachment=Clamp100(target.Attachment+5);
        var e=Emit(IncidentKind.Protection,target,"player",$"主角挡在{target.Name}前面，制止了{attacker.Name}",0,reason);
        Remember(target,e,"有人挡在前面保护了自己。",12);
        if(target.Condition==PhysicalCondition.Active)React(target,Expression.Shy,
            target.RegionId=="home"?"你来了！一起守住我们的家！":"谢谢你……可以跟着你吗？",
            target.RegionId=="home"?"望向退去的入侵者，终于放松下来。":"靠近你的身边，慢慢放松。",6);
        else React(target,Expression.Exhausted,"……","仍躺在原地；威胁退去后，紧绷的身体稍稍放松。",6);
        State.Progress.Completed.Add("protect");
        RefreshCurrentHelp();
        Log($"你制止了{attacker.Name}，保护了{target.Name}。");
        return new(true,"威胁已被制止。观察伤势，必要时护理或邀请它回园。",target.LastReaction,target.Expression,IncidentId:e.Id);
    }
    public bool FocusHelp()
    {
        RefreshCurrentHelp();
        var c=State.Creatures.Find(c=>c.Id==State.HelpTargetId);
        if(c==null)return false;
        if(State.Mode==PlayMode.CloseUp)ExitCloseUp();
        if(State.ActiveRegionId!=c.RegionId)Travel(c.RegionId);
        State.SelectedId=c.Id;return true;
    }
    private Creature? ThreateningAttacker(Creature target)
    {
        return State.Creatures.Where(c=>c.Id!=target.Id&&c.RegionId==target.RegionId&&IsAvailable(c)&&c.RetreatRemaining<=0&&c.CurrentOffenseId!=null)
            .Where(c=>State.PlayerKnownIncidents.Contains(c.CurrentOffenseId!))
            .Where(c=>Distance(c.X,c.Z,target.X,target.Z)<=6)
            .Where(c=>State.Incidents.Any(e=>e.Id==c.CurrentOffenseId&&!e.Resolved&&
                (e.Kind==IncidentKind.Attack&&e.TargetId==target.Id||e.Kind==IncidentKind.Intrusion&&c.IsIntruder&&target.RegionId=="home"&&!target.IsIntruder)))
            .MinBy(c=>Distance(c.X,c.Z,target.X,target.Z));
    }
    private void RefreshCurrentHelp()
    {
        var urgent=State.Creatures.Where(c=>c.Condition==PhysicalCondition.Critical).OrderBy(c=>c.CriticalRemaining).FirstOrDefault();
        if(urgent!=null){State.HelpTargetId=urgent.Id;return;}
        bool NeedsHelp(Creature c)=>c.Condition!=PhysicalCondition.Dead&&ThreateningAttacker(c)!=null&&
            (c.RegionId=="wild"||State.Threat.HelpRequested&&c.Id==State.Threat.DefenderId);
        var current=State.Creatures.Find(c=>c.Id==State.HelpTargetId);
        if(current!=null&&NeedsHelp(current))return;
        State.HelpTargetId=State.Creatures.FirstOrDefault(NeedsHelp)?.Id;
    }
}

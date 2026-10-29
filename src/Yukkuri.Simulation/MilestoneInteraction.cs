namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public InteractionResult Contact(string id,ContactRequest request)
    {
        var c=State.Creatures.Find(c=>c.Id==id);
        if(!State.MilestoneScenario||c==null||c.RegionId!=State.ActiveRegionId)
            return new(false,"先选择当前地区的一只油库里。","",Expression.Neutral);
        if(!Enum.IsDefined(request.Action)||!Enum.IsDefined(request.Area)||!Enum.IsDefined(request.Tool)||
           !double.IsFinite(request.Force)||!double.IsFinite(request.Duration)||request.Force is <0 or >1||request.Duration is <.05 or >12)
            return new(false,"接触参数无效。","",Expression.Neutral);
        if(State.Mode==PlayMode.CloseUp&&State.SelectedId!=id)return new(false,"特写中只能接触当前对象。","",Expression.Neutral);
        if(c.Condition==PhysicalCondition.Dead)return new(false,"它已经无法回应了。","没有回应。",Expression.Exhausted);
        if(request.Action==ContactAction.Treat)return Treat(c,request);
        if(!IsAvailable(c))return new(false,"它现在需要救治，暂时无法参与互动。","虚弱地躺着。",Expression.Hurt);
        double oldTrust=c.Trust;
        double force=request.Force;
        bool affection=request.Action is ContactAction.Stroke or ContactAction.Hug or ContactAction.Nuzzle;
        if(affection&&State.Settings.ProtectCareTools)force=Math.Min(force,.45);
        double material=request.Tool switch{ContactTool.SoftGlove=>.7,ContactTool.TrainingPaddle=>1.4,_=>1};
        double area=request.Area switch{ContactArea.Forehead=>1.15,ContactArea.Cheek=>1.25,_=>.85};
        double duration=Math.Clamp(request.Duration,.1,6);
        double strain=force*material*area*(.7+duration*.24);
        // Protection applies to the combined pressure, not force alone: a long hold
        // on a sensitive area with a firmer tool must not bypass the care setting.
        if(affection&&State.Settings.ProtectCareTools)strain=Math.Min(strain,1.05);
        c.LastPlayerTouchAt=State.WorldSeconds;
        string actionName=request.Action.ToString();
        c.ActionCounts[actionName]=c.ActionCounts.GetValueOrDefault(actionName)+1;
        bool justified=c.CurrentOffenseId!=null&&c.OffenseRemaining>0&&State.PlayerKnownIncidents.Contains(c.CurrentOffenseId);
        if(request.Action==ContactAction.Warn)
        {
            if(justified)
            {
                c.Obedience=Clamp100(c.Obedience+5);c.Fear=Clamp100(c.Fear+2);
                c.RetreatRemaining=8;c.TargetX=-8;c.TargetZ=6;
                React(c,Expression.Defiant,c.Aggression>.6?"知道了……别过来！":"我会注意的。","停下动作，观察你的态度。");
                if(c.IsIntruder&&c.Aggression<.7)BeginRetreat(c,true);
            }
            else React(c,Expression.Curious,"是在叫我吗？","歪着头，不明白为什么被警告。");
            State.Progress.Completed.Add("warn");
            return new(true,justified?"你制止了眼前的不当行为。":"没有已知的不当行为，它不明白警告的原因。",c.LastReaction,c.Expression,Justified:justified);
        }
        if(request.Action==ContactAction.Train)
        {
            if(c.Fear>65||c.Energy<20||c.Fullness<25)
            {
                React(c,Expression.Exhausted,"现在……不想练。","避开指示，需要先休息或获得安全感。");
                return new(false,"它暂时无法专心，先处理饥饿、疲劳或恐惧。",c.LastReaction,c.Expression);
            }
            double cooperation=(.5+c.Trust/100+c.Patience*.5)/(1+c.TrainingFatigue*.035);
            double gain=cooperation*(1+duration*.6)*(force<=.45?1:.45);
            c.TrainingCount++;c.TrainingFatigue=Clamp100(c.TrainingFatigue+10);
            c.SharingSkill=Clamp100(c.SharingSkill+gain*1.4);c.GuardSkill=Clamp100(c.GuardSkill+gain);
            c.Obedience=Clamp100(c.Obedience+gain*.7);c.Aggression=Math.Max(0,c.Aggression-gain*.0015);
            c.Energy=Math.Max(0,c.Energy-3);c.Trust=Clamp100(c.Trust+(force<=.45?1:-1));
            React(c,Expression.Curious,c.TrainingCount<3?"这样做，对吗？":"这次我记住啦！","跟着指示练习，成功后望向你。",5);
            var e=Emit(IncidentKind.Care,c,"player",$"和{c.Name}一起练习分享与守护");
            Remember(c,e,"练习了等待、分享和呼叫主人。",3);
            c.Observation.Notes.Add($"训练记录：第{c.TrainingCount}次一起练习等待、分享与守护；需要在生活中继续观察效果。");
            if(c.Observation.Notes.Count>30)c.Observation.Notes.RemoveAt(0);
            State.Progress.TrainingSessions++;State.Progress.CareActions++;
            if(c.TrainingCount>=3)State.Progress.Completed.Add("train");
            Log($"{c.Name}完成一次训练。休息后学习效果会更好。");
            return new(true,"练习留下了进步；继续观察它对食物和领地的行为。",c.LastReaction,c.Expression,TrustChange:c.Trust-oldTrust,IncidentId:e.Id);
        }
        if(affection)
        {
            bool intimate=request.Action is ContactAction.Hug or ContactAction.Nuzzle;
            bool refuses=intimate&&(c.Trust<22||c.Fear>55)||c.Pain>65;
            if(refuses&&force<.6)
            {
                c.Fear=Clamp100(c.Fear+.5);
                React(c,Expression.Shy,c.Pain>65?"那里疼……":"先不要靠得这么近。","缩起身子，轻轻避开接触。",4);
                return new(false,"它还不愿接受这种接触。先轻轻抚摸、喂食，或照顾伤势。",c.LastReaction,c.Expression);
            }
            double comfort=Math.Max(.1,1-Math.Abs(force-.23)*1.8);
            double preferred=request.Action switch
            {
                ContactAction.Stroke=>request.Area==ContactArea.Forehead?1.3:.8,
                ContactAction.Hug=>request.Area==ContactArea.Body?1.3:.7,
                _=>request.Area==ContactArea.Cheek?1.4:.7
            };
            double familiarity=1/(1+Math.Max(0,c.CareSessionCount-5)*.06);
            double gain=(1+duration*.55)*comfort*preferred*familiarity;
            double damage=Math.Max(0,strain-1.05)*12;
            if(request.Tool==ContactTool.TrainingPaddle)gain*=.3;
            if(damage>0||refuses)
            {
                Hurt(c,Math.Max(damage,.5),"过重的接触","player",request.Area switch{ContactArea.Forehead=>BodyPart.Head,ContactArea.Cheek=>BodyPart.Cheek,_=>BodyPart.Side});
                c.Trust=Clamp100(c.Trust-3);c.Fear=Clamp100(c.Fear+4);
                React(c,Expression.Hurt,"太紧了，放开……","挣动着试图躲开，表情变得不安。",5);
                var e=Emit(IncidentKind.Correction,c,"player",$"过重的接触使{c.Name}不适",Math.Max(4,damage));
                Remember(c,e,"接触太重，让自己害怕。",5);WitnessCorrection(e,c,Math.Max(4,damage));
                return new(true,"力度或持续时间让它不适；注意它的拒绝和身体状态。",c.LastReaction,c.Expression,damage,c.Trust-oldTrust,false,e.Id);
            }
            c.Trust=Clamp100(c.Trust+gain);c.Attachment=Clamp100(c.Attachment+gain*.8);
            c.Fear=Math.Max(0,c.Fear-gain*.5);c.Resentment=Math.Max(0,c.Resentment-gain*.12);
            c.CareSessionCount++;c.Familiarity++;State.Progress.CareActions++;State.Progress.Completed.Add("affection");
            var result=request.Action switch
            {
                ContactAction.Stroke=>(Expression.Content,"慢慢来，好舒服……","闭起眼睛，轻轻压低身子迎向手掌。"),
                ContactAction.Hug=>(Expression.Delighted,"暖暖的……再抱一下！","依偎在怀里，身体随着呼吸轻轻起伏。"),
                _=>(Expression.Delighted,"痒痒的，嘿嘿！","脸颊泛红，左右蹭动着回应。")
            };
            React(c,result.Item1,result.Item2,result.Item3,4);
            var care=Emit(IncidentKind.Care,c,"player",$"温和地{ActionLabel(request.Action)}{c.Name}");
            Remember(c,care,$"主人{ActionLabel(request.Action)}了自己，觉得很安心。",gain);
            return new(true,result.Item3,c.LastReaction,c.Expression,TrustChange:c.Trust-oldTrust,IncidentId:care.Id);
        }
        if(request.Action==ContactAction.Discipline)
        {
            double damage=Math.Max(0,strain-.28)*16;
            double severity=3+strain*8;
            Hurt(c,damage,"惩戒接触","player",request.Area switch{ContactArea.Forehead=>BodyPart.Head,ContactArea.Cheek=>BodyPart.Cheek,_=>BodyPart.Side});
            c.Fear=Clamp100(c.Fear+severity*.55);c.Resentment=Clamp100(c.Resentment+severity*(justified?.14:.55));
            c.Trust=Clamp100(c.Trust-(justified?.6:severity*.3));
            c.Obedience=Clamp100(c.Obedience+severity*.35);
            if(justified){c.SharingSkill=Clamp100(c.SharingSkill+1.5);c.Aggression=Math.Max(0,c.Aggression-.025);}
            React(c,damage>10?Expression.Hurt:Expression.Defiant,damage>10?"好疼……":justified?"下次……不这样了。":"为什么要这样！",
                damage>10?"身体缩起，颤抖着避开接触。":"惊得缩了一下，带着不服气的表情看着你。",5);
            var e=Emit(IncidentKind.Correction,c,"player",$"对{c.Name}施加了惩戒",severity,justified?c.CurrentOffenseId:null);
            Remember(c,e,justified?"自己的行为被主人制止了。":"没有明白原因，却被主人伤害了。",severity);
            WitnessCorrection(e,c,severity);State.Progress.Completed.Add("correction");
            if(c.IsIntruder&&IsAvailable(c)&&(force>=.25||c.Fear>=30))BeginRetreat(c,true);
            if(justified&&!c.IsIntruder){ResolveOffense(c);c.BehaviorCooldown=40;}
            Log(justified?$"你制止了{c.Name}；知情者与不知情者会有不同反应。":$"{c.Name}没有已知的不当行为；目击者感到不安。");
            return new(true,c.Condition==PhysicalCondition.Dead?"伤势已经不可逆，它无法再回应。":c.Condition==PhysicalCondition.Critical?"它已重伤倒下，需要立即护理。":damage>10?"它受伤了。过重惩戒也会使知情的同伴恐惧。":justified?"制止了不当行为。观察它和知情同伴的反应。":"这次伤害没有已知理由，附近油库里会产生恐惧。",c.LastReaction,c.Expression,damage,c.Trust-oldTrust,justified,e.Id);
        }
        return new(false,"无法执行此互动。","",Expression.Neutral);
    }
    public static string ActionLabel(ContactAction action)=>action switch
    {
        ContactAction.Stroke=>"抚摸",ContactAction.Hug=>"抱抱",ContactAction.Nuzzle=>"蹭蹭",
        ContactAction.Train=>"训练",ContactAction.Warn=>"警告",ContactAction.Discipline=>"惩戒",ContactAction.Treat=>"护理",_=>"接触"
    };
    private void ResolveOffense(Creature c)
    {
        var e=State.Incidents.Find(e=>e.Id==c.CurrentOffenseId);if(e!=null)e.Resolved=true;
        c.CurrentOffenseId=null;c.OffenseRemaining=0;
    }
    public void Hurt(Creature c,double amount,string cause,string? actor,BodyPart? part=null,SurfaceSide side=SurfaceSide.Whole)
    {
        if(!double.IsFinite(amount)||amount<0)throw new ArgumentOutOfRangeException(nameof(amount));
        if(amount==0||c.Condition==PhysicalCondition.Dead)return;
        var injuries=c.Interaction.Injuries;
        // Systemic illness, starvation and childbirth have no invented contact site.
        if(part.HasValue)ApplyLocalInjury(c,part.Value,side,amount,cause);
        else if(actor=="player"&&cause!="底部受伤")
        {
            var gesture=_interactionGesture;
            ApplyLocalInjury(c,gesture?.TargetId==c.Id?gesture.Input.Part:BodyPart.Side,
                gesture?.TargetId==c.Id?gesture.Input.Side:SurfaceSide.Whole,amount,cause);
        }
        if(c.Condition is PhysicalCondition.Critical or PhysicalCondition.Recovering)
        {
            if(c.Condition==PhysicalCondition.Critical)
                injuries.CriticalCareBudget=Math.Min(injuries.CriticalCareBudget,c.CriticalRemaining);
            injuries.CriticalCareBudget=Math.Max(0,injuries.CriticalCareBudget-amount*.5);
            if(c.Condition==PhysicalCondition.Critical)c.CriticalRemaining=Math.Min(c.CriticalRemaining,injuries.CriticalCareBudget);
        }
        if(actor=="player"&&c.Interaction.Enabled)RememberPlayerHarm(c,amount);
        c.Health=Math.Max(0,c.Health-amount);c.Pain=Clamp100(c.Pain+amount*1.5);c.Strain=Clamp100(c.Strain+amount*.7);
        c.InjuryCause=cause;
        if(amount>18)c.FillingIntegrity=Math.Max(0,c.FillingIntegrity-(amount-18)*1.2);
        if(c.Condition==PhysicalCondition.Critical)c.FillingIntegrity=Math.Max(0,c.FillingIntegrity-amount);
        if(c.Health<=0&&c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering)
        {
            injuries.CriticalEpisode=true;
            c.Condition=PhysicalCondition.Critical;c.CriticalRemaining=injuries.CriticalCareBudget;c.Stabilized=false;c.Activity="重伤待救";
            StopIncapacitatedActions(c);
            React(c,Expression.Exhausted,"……","失去行动能力，需要立即护理。",10);
            State.HelpTargetId=c.Id;Log($"{c.Name}重伤倒下。尽快使用护理包稳定伤势。");
        }
        if((c.FillingIntegrity<=0||c.Condition==PhysicalCondition.Critical&&c.CriticalRemaining<=0)&&State.Settings.LethalInjuries)Die(c,cause);
    }
    private void Die(Creature c,string cause)
    {
        if(c.Condition==PhysicalCondition.Dead)return;
        if(cause=="错过救治时限"&&!string.IsNullOrWhiteSpace(c.InjuryCause))cause=c.InjuryCause+"，未能及时稳定";
        c.Interaction.Injuries.DeathCause=cause;
        c.Interaction.Injuries.DeathAt=State.WorldSeconds;
        c.Condition=PhysicalCondition.Dead;c.Health=0;c.Speech="";c.Expression=Expression.Exhausted;c.Activity="已死亡";
        c.CriticalRemaining=0;c.RecoveryRemaining=0;c.Stabilized=false;c.ReactionRemaining=0;
        StopIncapacitatedActions(c);
        if(InteractionGestureTarget==c.Id)_interactionGesture=null;
        NormalizeDeadInteractionSupport(c);
        RefreshHelpAfterResolution(c);
        c.LastReaction="再也没有回应。";c.ReactionSerial++;
        var e=Emit(IncidentKind.Death,c,null,$"{c.Name}因{cause}死亡",10);
        foreach(var witness in State.Creatures.Where(w=>w.Id!=c.Id&&Knows(w,e.Id)))
        {witness.Fear=Clamp100(witness.Fear+8);Remember(witness,e,$"目睹{c.Name}离去。",10);}
        Log($"{c.Name}已经无法恢复。你可以继续照顾其他油库里，或读取此前存档。");
    }
    private void NormalizeDeadInteractionSupport(Creature c)
    {
        var i=c.Interaction;
        if(c.Condition!=PhysicalCondition.Dead)return;
        bool releasedSupport=i.Carried||i.Lowering||i.Falling||i.Restraint==RestraintKind.Hand;
        StopIncapacitatedActions(c);
        // Death and loading share support cleanup, without replaying damage or
        // a death incident. Fixed devices remain separate from the player's hand.
        i.Carried=false;i.Lowering=false;i.Falling=false;
        if(i.Restraint!=RestraintKind.Hanging)i.Height=0;
        i.VerticalVelocity=0;i.VelocityX=0;i.VelocityZ=0;
        i.EndRoll();i.ReactionPose="dead";i.ReactionPoseRemaining=0;
        if(i.Restraint==RestraintKind.Hand)i.Restraint=RestraintKind.None;
        if(releasedSupport)ClearLandingOverlap(c);
        c.TargetX=c.X;c.TargetZ=c.Z;
    }
    private InteractionResult Treat(Creature c,ContactRequest request)
    {
        if(!State.UnlimitedResources&&State.Medicine<=0)return new(false,"护理包不足，可在补给处购买。","",Expression.Neutral);
        BodyPart carePart=request.Area switch{ContactArea.Forehead=>BodyPart.Head,ContactArea.Cheek=>BodyPart.Cheek,_=>BodyPart.Side};
        var careSite=ChooseGeneralCareSite(c,carePart);
        if(!NeedsInteractionCare(c))return new(false,c.Interaction.Injuries.CareCooldown>0?"刚做过护理，需要时间观察恢复；重复包扎不能加快愈合。":"目前没有需要护理的伤势。","",Expression.Neutral);
        if(!ApplyBodyCare(c,careSite.Part,careSite.Side))return new(false,"刚做过护理，需要时间观察恢复；重复包扎不能加快愈合。","",Expression.Neutral);
        if(!State.UnlimitedResources)State.Medicine--;
        c.Trust=Clamp100(c.Trust+3);c.Fear=Math.Max(0,c.Fear-2);
        React(c,Expression.Shy,"轻一点……谢谢。","包扎后慢慢放松，还需要休息。",5);
        var e=Emit(IncidentKind.Care,c,"player",$"为{c.Name}护理伤势");Remember(c,e,"主人照顾了自己的伤。",4);
        State.Progress.CareActions++;State.Progress.Completed.Add("treat");
        return new(true,c.Condition==PhysicalCondition.Recovering?"伤势已稳定，需要休息恢复。":"消耗1份护理包，伤势缓解。",c.LastReaction,c.Expression,IncidentId:e.Id);
    }
    private void StaffCare(Creature c)
    {
        var careSite=ChooseGeneralCareSite(c,BodyPart.Side);
        if(c.Condition==PhysicalCondition.Critical&&(State.Medicine>0||State.UnlimitedResources))
        {
            if(!State.UnlimitedResources)State.Medicine--;
            ApplyBodyCare(c,careSite.Part,careSite.Side);c.RecoveryRemaining=75;
            Log($"雇员小岚为{c.Name}做了紧急稳定处理，正在等待恢复。");
        }
        else if(c.Condition==PhysicalCondition.Active&&NeedsInteractionCare(c)&&(State.Medicine>0||State.UnlimitedResources))
        {
            if(!ApplyBodyCare(c,careSite.Part,careSite.Side))return;
            if(!State.UnlimitedResources)State.Medicine--;
            c.Activity="接受雇员护理";
        }
    }
    private void RefreshHelpAfterResolution(Creature treated)
    {
        if(State.HelpTargetId!=treated.Id)return;
        RefreshCurrentHelp();
    }
    public InteractionResult RegisterImpact(string id,double speed)
    {
        var c=State.Creatures.Find(c=>c.Id==id);
        if(c==null||c.Condition==PhysicalCondition.Dead||!State.MilestoneScenario||!State.Settings.Accidents||!double.IsFinite(speed)||speed<=4)return new(false,"","",Expression.Neutral);
        double damage=Math.Min(35,(speed-4)*4);
        Hurt(c,damage,"跌落碰撞","player",BodyPart.Bottom);c.Fear=Clamp100(c.Fear+damage*.3);
        React(c,Expression.Hurt,"哎哟！","落地后缩起身子，需要检查伤势。",4);
        var e=Emit(IncidentKind.Correction,c,"player",$"{c.Name}跌落受伤",damage);
        Remember(c,e,"被拿起后跌落，感到疼痛。",damage);WitnessCorrection(e,c,damage);
        return new(true,"跌落碰撞造成了伤势。",c.LastReaction,c.Expression,damage,IncidentId:e.Id);
    }
}

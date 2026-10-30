namespace Yukkuri.Simulation;

public sealed class HumanCompanyState
{
    public double Until { get; set; }
    public double StartedAt { get; set; }
    public double NextRequestAt { get; set; }
    public double ContactSeconds { get; set; }
    public bool SeekingComfort { get; set; }
    public bool Remembered { get; set; }

    public static void Validate(HumanCompanyState? state,double now)
    {
        static bool Number(double v)=>double.IsFinite(v)&&v>=0;
        if(state==null||!Number(state.Until)||!Number(state.StartedAt)||!Number(state.NextRequestAt)||
            !Number(state.ContactSeconds)||state.ContactSeconds>6||state.Until>now+20.001||
            state.Until==0&&(state.StartedAt!=0||state.ContactSeconds!=0||state.SeekingComfort||state.Remembered)||
            state.Until>0&&(state.StartedAt>now+.001||Math.Abs(state.Until-state.StartedAt-20)>.001||
                state.ContactSeconds>now-state.StartedAt+.001)||
            state.Remembered&&state.ContactSeconds<6)
            throw new InvalidDataException("主动陪伴状态无效。");
    }
}

public sealed partial class InteractionLife
{
    public HumanCompanyState HumanCompany { get; set; } = new();
}

public sealed partial class WorldSimulation
{
    // Live input is not saved. The older three-second position memory remains
    // useful for following/searching, but cannot count as present companionship.
    private bool _interactionHandLive;
    // A saved Until is not a live interaction: the hand, body and current
    // competing needs still have to be valid, including during close-up.
    public bool HasLiveHumanCompany(Creature c) => State.InteractionPlaytest&&State.Mode!=PlayMode.CloseUp&&
        c.Interaction.HumanCompany.Until>State.WorldSeconds&&State.Creatures.Contains(c)&&CanSeekHumanCompany(c);

    private bool CanSeekHumanCompany(Creature c)
    {
        var i=c.Interaction;
        return c.Condition==PhysicalCondition.Active&&!c.Quarantine&&!c.IsIntruder&&
            !c.Family.ColdStorage.Stored&&!FamilyRules.UnderIntervention(c)&&!i.Water.InWater&&!WaterEnvironment.IsInWater(State,c)&&
            c.Pain<25&&c.Fear<60&&c.Resentment<35&&c.Trust>=60&&c.Attachment>=40&&
            i.HumanBond.Familiarity>=8&&i.HumanBond.Safety>=8&&State.WorldSeconds-i.HumanBond.LastHarmAt>=60&&
            !NaturalResources.WantsFood(c)&&!NaturalResources.WantsWater(c)&&c.Energy>=40&&
            !i.Digestion.Symptomatic&&!i.Physiology.Active&&i.EatingRemaining<=0&&
            i.Restraint==RestraintKind.None&&!i.Carried&&!i.Falling&&!i.Lowering&&i.Height<=.25&&
            !i.Supine&&!i.Resting&&i.RestRemaining<=0&&i.ActiveCue==null&&!i.FollowPlayer&&i.WaitRemaining<=0&&
            InteractionGestureTarget!=c.Id&&c.RetreatRemaining<=0&&c.Family.FlightRemaining<=0&&
            c.Family.Conflict.TargetId==null&&c.Family.ChildThreat.TargetId==null&&
            c.Family.CompanionTargetId==null&&c.Family.CareTargetId==null&&c.Family.CareParcelId==null&&
            !State.Creatures.Any(other=>other!=c&&CurrentInteractionThreatTarget(other)==c)&&
            _interactionHandLive&&SeesHelpingHand(c);
    }

    private void EndHumanCompany(Creature c)
    {
        var request=c.Interaction.HumanCompany;
        if(request.Until<=0)return;
        request.Until=0;request.StartedAt=0;request.ContactSeconds=0;request.SeekingComfort=false;request.Remembered=false;
        request.NextRequestAt=Math.Max(request.NextRequestAt,State.WorldSeconds+30);
    }

    private bool TryHumanCompany(Creature c,double dt)
    {
        if(!CanSeekHumanCompany(c)){EndHumanCompany(c);return false;}
        var request=c.Interaction.HumanCompany;
        if(request.Until>0&&State.WorldSeconds>=request.Until)
        {EndHumanCompany(c);return false;}
        if(request.Until==0)
        {
            if(State.WorldSeconds<request.NextRequestAt||c.ReactionRemaining>0)return false;
            request.StartedAt=State.WorldSeconds;request.Until=State.WorldSeconds+20;request.NextRequestAt=State.WorldSeconds+60;
            request.SeekingComfort=c.Fear>=20||c.Family.GriefRemaining>0||!EmotionalReadiness.Settled(c);
            Feedback(c,request.SeekingComfort?"request-comfort":"request-company",
                request.SeekingComfort?Expression.Worried:Expression.Content,
                request.SeekingComfort?"它认出了熟悉的手，想有人安静陪一会儿。":"它认出了熟悉的手，想靠过来陪一会儿。",
                "invite",false);
            return true;
        }
        double distance=Distance(c.X,c.Z,State.PlayerX,State.PlayerZ);
        if(distance>PreferredHumanDistance(c)+.01)
        {
            if(!request.Remembered)request.ContactSeconds=0;
            if(CanSelfMove(c))
            {ApproachHumanAtDistance(c,dt,PreferredHumanDistance(c));c.Activity=request.SeekingComfort?"靠近熟悉的手寻求安慰":"主动靠近熟悉的手";}
            else c.Activity="想有人陪伴，等熟悉的手靠近";
            return true;
        }
        c.TargetX=c.X;c.TargetZ=c.Z;
        c.Activity=request.SeekingComfort?"和熟悉的手安静相处":"安心待在熟悉的手旁边";
        request.ContactSeconds=Math.Min(6,request.ContactSeconds+dt);
        // Benefits require real proximity and ordinary time, and share the same
        // finite affection budget as care. No body damage is healed by company.
        ImproveAffection(c,dt*.04);
        c.Fear=Math.Max(EmotionalReadiness.FearFloor(c),c.Fear-dt*.035);
        if(request.SeekingComfort&&c.Family.GriefRemaining>0)
        {
            c.Family.GriefRemaining=Math.Max(0,c.Family.GriefRemaining-dt*.08);
            if(c.Family.GriefRemaining==0)c.Family.GrievingForId=null;
        }
        if(request.ContactSeconds>=6&&!request.Remembered)
        {
            request.Remembered=true;
            RecordInteraction(c,"human-company",request.SeekingComfort?
                "心里难受时，熟悉的手在近处安静陪了一会儿。":"主动靠近熟悉的手，一起安静待了一会儿。",1);
        }
        return true;
    }
}

using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    // An invitation belongs to the current encounter, not to a saved future reward.
    [JsonIgnore] public InteractionAction? PendingConversation { get; internal set; }
    [JsonIgnore] internal string? ConversationPraise { get; set; }
    [JsonIgnore] internal int ConversationReactionSerial { get; set; }
    [JsonIgnore] internal double ConversationDelay { get; set; }
}

public sealed partial class WorldSimulation
{
    private InteractionFeedback BeginConversation(Creature c,InteractionAction action)
    {
        var i=c.Interaction;
        string? praise=action==InteractionAction.Praise&&i.PraiseRemaining>0?i.PendingPraise:null;
        if(i.EatingRemaining<=0&&!i.Resting)return RespondConversation(c,action,praise);
        i.PendingConversation=action;i.ConversationPraise=praise;
        i.ConversationReactionSerial=c.ReactionSerial;
        i.ConversationDelay=i.EatingRemaining>0?.65:.9;
        string message=i.EatingRemaining>0?"它还在慢慢咽这一口，等嘴里空了再回应；不必重复催它。":"它还在歇着，给它一点时间轻轻回应。";
        State.LastActionMessage=message;
        return new(true,message,c.Speech,InteractionPresentation.Pose(c),Changed:false);
    }

    private void ValidatePendingConversation(Creature c)
    {
        var i=c.Interaction;
        if(i.PendingConversation.HasValue&&(c.ReactionSerial!=i.ConversationReactionSerial||
            c.Condition is not (PhysicalCondition.Active or PhysicalCondition.Recovering)||c.RegionId!=State.ActiveRegionId))
        {i.PendingConversation=null;i.ConversationPraise=null;i.ConversationDelay=0;}
    }

    private void AdvancePendingConversation(Creature c,double dt)
    {
        var i=c.Interaction;
        if(i.PendingConversation is not { } action||i.EatingRemaining>0)return;
        i.ConversationDelay=Math.Max(0,i.ConversationDelay-dt);
        if(i.ConversationDelay>0)return;
        string? praise=i.ConversationPraise;
        i.PendingConversation=null;i.ConversationPraise=null;
        RespondConversation(c,action,praise);
    }

    private InteractionFeedback RespondConversation(Creature c,InteractionAction action,string? praise)
    {
        var i=c.Interaction;i.SocialResponseRemaining=2.4;
        switch(action)
        {
            case InteractionAction.Greet:
                ImproveAffection(c,.25);
                return Feedback(c,i.Resting?"greet-rest":c.Fear>45||c.Trust<25?"greet-shy":"greet",
                    i.Resting?Expression.Sleeping:Expression.Curious,
                    i.Resting?"它没有急着起身，歇着轻轻应了一声。":"抬起脸，听见你在叫它。",i.Resting?"rest":"greet");
            case InteractionAction.Praise:
                if(praise!=null)
                {
                    RecordInteraction(c,"praise",$"做好{praise}后得到了夸奖。",2);
                    if(i.PendingPraise==praise){i.PendingPraise=null;i.PraiseRemaining=0;}
                    ImproveAffection(c,1.5);
                }
                else ImproveAffection(c,.15);
                return Feedback(c,i.Resting?"praise-rest":praise!=null?"praise":"praise-empty",
                    i.Resting?Expression.Sleeping:Expression.Delighted,
                    praise!=null?$"它把夸奖和刚做好的{praise}联系起来。":i.Resting?"听见温和的话，它在休息中轻轻回应。":"它听见温和的话，朝你望过来。",i.Resting?"rest":"proud");
            default:
                if(c.Fear>75&&c.Trust<15)
                    return Feedback(c,"comfort-distance",Expression.Frightened,"它还不敢让你靠近，先留一点距离陪着。","withdraw",false,true);
                ImproveAffection(c,.5);c.Fear=Math.Max(0,c.Fear-2);
                return Feedback(c,i.Resting?"comfort-rest":"comfort",i.Resting?Expression.Sleeping:Expression.Shy,
                    i.Resting?"它听见你还在旁边，继续安静歇着。":"安慰让它稍微放松，先陪在旁边。",i.Resting?"rest":"comfort");
        }
    }
}

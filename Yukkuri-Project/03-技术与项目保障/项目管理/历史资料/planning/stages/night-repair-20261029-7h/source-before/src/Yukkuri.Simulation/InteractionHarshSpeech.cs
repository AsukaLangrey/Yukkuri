using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    [JsonIgnore] public double HarshResponseRemaining { get; internal set; }
}

public sealed partial class WorldSimulation
{
    private InteractionFeedback HarshConversation(Creature c,InteractionAction action)
    {
        var i=c.Interaction;
        if(i.HarshResponseRemaining>0)return Unavailable("它还在消化刚才的话，先看它的回应。 ");
        i.HarshResponseRemaining=2.2;
        if(action==InteractionAction.Scold)
        {
            bool reason=c.CurrentOffenseId!=null&&c.OffenseRemaining>0&&State.PlayerKnownIncidents.Contains(c.CurrentOffenseId)&&
                State.Incidents.Any(e=>e.Id==c.CurrentOffenseId&&e.ActorId==c.Id&&!e.Resolved&&e.RegionId==c.RegionId&&e.Kind is IncidentKind.Attack or IncidentKind.Theft or IncidentKind.Intrusion);
            bool immobile=!CanSelfMove(c);
            i.ActiveCue=null;i.FollowPlayer=false;i.WaitRemaining=1;c.TargetX=c.X;c.TargetZ=c.Z;
            Distress(c,reason?.4:1.5);
            RecordInteraction(c,reason?"scold-context":"scold-confused",reason?"眼前的事件被喝止，但停止不代表已经学会规则。":"没有明白缘由，就听见了严厉的喝止。",reason?.3:1);
            return Feedback(c,immobile?"scold-immobile":reason?"scold":"confused-scold",
                c.Fear>60?Expression.Frightened:c.Courage>.6?Expression.Defiant:Expression.Worried,
                immobile?"它本来就无法走动，听见喝止后缩了一下；这不代表学会了停下。":reason?"它暂时停住动作，事件和学习结果都没有被自动判定为完成。":"它没有明白缘由，暂时停住看着你。","startle");
        }
        bool belittle=action==InteractionAction.Belittle;
        bool request=c.ReactionRemaining>0&&i.ReactionSituation is "request-nuzzle" or "thirsty" or "cannot-walk" or "box" or "hang" or "restrained" or "treasure-missing" or "accessory-high" or "accessory-taken";
        Distress(c,belittle?4:1.5);
        RecordInteraction(c,belittle?"belittled":"cold",belittle?"听见了针对自己的贬斥。":request?"刚发出的请求得到了冷淡回应。":"说话时遇到了冷淡的语气。",1);
        bool defiant=belittle&&c.Courage>.6;
        return Feedback(c,belittle?defiant?"belittle-defiant":"belittle-hurt":request?"cold-request":"cold",
            defiant?Expression.Defiant:c.Fear>60?Expression.Frightened:Expression.Shy,
            belittle?defiant?"它鼓起脸反驳，仍记得你刚才的话。":"它缩了一下，避开你的视线。":request?"它收住刚才的请求，仍然需要被回应的事情并没有消失。":"它听见冷淡的语气，慢慢移开视线。",defiant?"defiant":"withdraw");
    }
}

namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    public BodyPart PulledPart { get; set; }
    public SurfaceSide PulledSide { get; set; }
    public double PullAmount { get; set; }
    public double PullRemaining { get; set; }
    public double ImpactCompression { get; set; }
    public double ImpactCompressionRemaining { get; set; }
}
public sealed partial class WorldSimulation
{
    public InteractionFeedback PullContactPart(string id,BodyPart part,SurfaceSide side,double amount,double seconds)
    {
        var c=InteractionTarget(id);
        var input=new InteractionInput(InteractionAction.Squeeze,part,Force:.25,Duration:seconds,Side:side);
        if(ValidateInteraction(c,input) is { } error)return Unavailable(error);
        if(!double.IsFinite(amount)||amount is <0 or >1||part==BodyPart.Accessory)return Unavailable("拉拽部位无效。");
        var i=c!.Interaction;i.PulledPart=part;i.PulledSide=side;i.PullAmount=amount;i.PullRemaining=.3;
        Distress(c,seconds*amount,seconds*amount*.3,"局部拉拽",part,side);
        return Feedback(c,"poke",Expression.Defiant,part==BodyPart.Head?"头发被拉长，它试着缩回去。":"脸侧被轻轻拉长，它想挣开。","poke");
    }
}

using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    // A one-shot reaction is presentation, not a held action to replay when a save is loaded.
    [JsonIgnore] public string ReactionPose { get; set; }="idle";
    [JsonIgnore] public double ReactionPoseRemaining { get; set; }
    [JsonIgnore] public string ReactionSituation { get; set; }="";
    [JsonIgnore] public bool ReactionRefused { get; set; }
    [JsonIgnore] public double BallContactCooldown { get; set; }
    [JsonIgnore] public double ReactionDirectionX { get; private set; }
    [JsonIgnore] public double ReactionDirectionZ { get; private set; }=-1;
    internal void SetReactionDirection(double x,double z)
    {
        double length=Math.Sqrt(x*x+z*z);
        ReactionDirectionX=length>.001?x/length:0;ReactionDirectionZ=length>.001?z/length:-1;
    }
}

public static class InteractionPresentation
{
    public static string VoiceCue(Creature c)
    {
        var i=c.Interaction;
        // Pain and requests for help can also reject an action. Keep their own voice;
        // an ordinary frightened refusal still uses the refusal cue.
        if(c.Expression==Expression.Hurt||i.ReactionSituation is "stomach-unwell" or "stomach-no-food" or "touch-sore"||
            i.ReactionSituation=="inspect"&&i.Digestion.Symptomatic)return "coo-hurt";
        if(c.Expression==Expression.Calling)return "coo-help";
        if(i.ReactionRefused)return "coo-refuse";
        if(c.Expression==Expression.Frightened)return "coo-hurt";
        if(i.ReactionSituation.Contains("nuzzle",StringComparison.Ordinal))return "coo-nuzzle";
        if(i.ReactionSituation is "eat" or "eat-leaves" or "sweet" or "drink" or "mouthful-done")return "feed";
        if(i.ReactionSituation is "lift" or "lift-low" or "rock")return "coo-hug";
        return "coo-stroke";
    }
    public static string Pose(Creature c)
    {
        var i=c.Interaction;
        if(c.Condition==PhysicalCondition.Dead)return "dead";
        if(i.Falling)return "fall";
        // Eating can finish without a swallow reaction (for example while in pain).
        // Its short reaction timer must not keep an empty mouth in an intake pose.
        if(i.ReactionPoseRemaining>0&&i.ReactionPose!="idle"&&
            (i.ReactionPose is not ("eat" or "sip")||i.EatingRemaining>0))return i.ReactionPose;
        if(i.EatingRemaining>0)return i.EatingKind==IntakeKind.Water?"sip":"eat";
        if(i.Resting)return "rest";
        if(i.Carried)return "carry";
        if(i.Digestion.Symptomatic)return "stomach";
        return i.HeadSensitivity>45||i.CheekSensitivity>45?"guard":"idle";
    }
}

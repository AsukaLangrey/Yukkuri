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
    public static string VoiceCue(Creature c)=>c.Interaction.ReactionRefused?"coo-refuse":
        c.Expression is Expression.Hurt or Expression.Frightened||c.Interaction.ReactionSituation=="stomach-unwell"?"coo-hurt":
        c.Interaction.ReactionSituation.Contains("nuzzle",StringComparison.Ordinal)?"coo-nuzzle":
        c.Expression==Expression.Calling?"coo-help":"coo-stroke";
    public static string Pose(Creature c)
    {
        var i=c.Interaction;
        if(c.Condition==PhysicalCondition.Dead)return "dead";
        if(i.Falling)return "fall";
        if(i.ReactionPoseRemaining>0&&i.ReactionPose!="idle")return i.ReactionPose;
        if(i.EatingRemaining>0)return i.EatingKind==IntakeKind.Water?"sip":"eat";
        if(i.Resting)return "rest";
        if(i.Carried)return "carry";
        if(i.Digestion.Symptomatic)return "stomach";
        return i.HeadSensitivity>45||i.CheekSensitivity>45?"guard":"idle";
    }
}

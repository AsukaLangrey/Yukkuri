namespace Yukkuri.Simulation;

// A limited functional consequence of the existing local injury. This does not
// model a missing organ, surgery or loss of understanding, identity or memory.
public static class MouthFunction
{
    public static double Impairment(Creature c)
    {
        var wound=c.Interaction.Injuries.At(BodyPart.Mouth);
        return wound==null?0:Math.Max(wound.Damage,wound.PermanentLoss);
    }

    public static double IntakeRate(Creature c,IntakeKind kind)
    {
        double affected=Math.Clamp((Impairment(c)-20)/80,0,1);
        double minimum=kind==IntakeKind.Water?.4:.25;
        return 1-affected*(1-minimum);
    }

    public static bool CanSpeakClearly(Creature c)=>Impairment(c)<60;

    public static string FormatSpeech(Creature c,string speech)=>
        string.IsNullOrWhiteSpace(speech)||CanSpeakClearly(c)?speech:"唔……嗯……";

    public static string Describe(Creature c)
    {
        double impairment=Impairment(c);
        if(impairment<=20)return "嘴边活动基本正常，仍可照常慢慢吃喝和说话。";
        bool lasting=c.Interaction.Injuries.At(BodyPart.Mouth) is {PermanentLoss:>20};
        string cause=lasting?"嘴边留有持续的功能受限，":"嘴边受伤，活动不太方便，";
        return cause+(CanSpeakClearly(c)?"接食和咽下需要更久，仍能清楚说话。":
            "接食和咽下需要更久，说话含混，可以用声音和动作表达需要。");
    }
}

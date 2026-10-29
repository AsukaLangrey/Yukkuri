namespace Yukkuri.Simulation;

public static class CreatureAppearance
{
    // Older prototype worlds alternate their two authored appearances by display
    // index. Keep that existing selection consistent with body support queries.
    public static bool UsesHat(string speciesId,int legacyIndex=0)=>
        speciesId=="marisa"||speciesId=="prototype-common"&&legacyIndex%2!=0;
}

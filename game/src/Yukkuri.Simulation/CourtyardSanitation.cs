namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    // Derived from actual ground waste; cleaning, removal and save loading cannot leave a stale score.
    public double EnvironmentScore(string regionId)=>Math.Clamp(100-State.RoomObjects
        .Where(o=>o.RegionId==regionId&&!o.Stored&&!o.HeldByHand&&o.CarrierId==null&&IsWaste(o.Kind))
        .Sum(o=>(o.Kind==RoomObjectKind.Feces?8.0:5.0)*Math.Max(0,o.Portions)),0,100);
    public static double MinimumEnvironmentScore(Creature c)
    {
        double kindness=Math.Clamp(c.Personality.Kindness,0,1);
        double personal=kindness<=.15?0:kindness<.65?(kindness-.15)/.5*92:92+(kindness-.65)/.35*7;
        double education=c.Interaction.RuleLearning<50?c.Interaction.RuleLearning*1.8:90+(c.Interaction.RuleLearning-50)*.18;
        return Math.Clamp(Math.Max(personal,Math.Max(education,CleaningLicenseLevel(c)*24)),0,99);
    }
    public double EnvironmentDiscomfort(Creature c)=>Math.Clamp((MinimumEnvironmentScore(c)-EnvironmentScore(c.RegionId))*.65,0,60);
    private bool WantsCleanerEnvironment(Creature c)=>EnvironmentDiscomfort(c)>0;
    public string EnvironmentSummary(Creature c)=>$"环境评分 {EnvironmentScore(c.RegionId):0}/100 · 卫生要求 {MinimumEnvironmentScore(c):0}/100\n"+
        (WantsCleanerEnvironment(c)?"脏污让它不适，降低馒馒来；有余力时会考虑清理。":"当前环境尚在它的容忍范围内。");
    private bool NeedsOutdoorToilet(Creature c)
    {double threshold=HousingNight?95:80;return c.Interaction.Physiology.Bladder>=threshold||c.Interaction.Physiology.Bowel>=threshold;}
    private bool IsInsideToiletBoundary(Creature c)=>State.Shelters.Any(s=>ShelterBodyPresent(c,s));
    private void RequestOutdoorToilet(Creature c)
    {
        if(!NeedsOutdoorToilet(c)||!IsInsideToiletBoundary(c))return;
        c.Interaction.Resting=false;c.Interaction.RestRemaining=0;
        foreach(var shelter in State.Shelters.Where(s=>ShelterBodyPresent(c,s)))
        {
            if(shelter.Capacity==5)
            {
                var visit=shelter.Residents.Find(v=>v.CreatureId==c.Id);
                if(visit!=null)visit.ExitRequested=true;
            }
            else if(shelter.GuestId==c.Id)shelter.ExitRequested=true;
        }
        c.Activity="需要排泄，先沿门口到屋外；屋内不排泄";
    }
}

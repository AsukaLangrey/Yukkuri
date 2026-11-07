namespace Yukkuri.Simulation;

// Read-only descriptions share the decision thresholds, including an unfinished meal.
// These are parallel needs, not mutually exclusive replacements for physical condition.
public static class CreatureNeedsObservation
{
    public static string Food(Creature c)=>c.Fullness<=0?"饥饿耗损":
        c.Fullness<CourtyardSurvival.UrgentFood?"急需进食":
        NaturalResources.WantsFood(c)?(c.Interaction.MealSeeking&&c.Fullness>=CourtyardSurvival.MealStart?"还想吃完这餐":"想吃东西"):
        c.Fullness>=CourtyardSurvival.MealSatisfied?"饱足":"尚可";
    public static string Water(Creature c)=>c.Interaction.Hydration<=0?"严重脱水":
        c.Interaction.Hydration<CourtyardSurvival.DehydrationWarning?"缺水":
        NaturalResources.WantsWater(c)?"口渴":"充足";
    public static string Sleep(Creature c)=>c.Interaction.Resting?"正在补觉":
        c.Interaction.Sleepiness>=CourtyardSurvival.DaytimeSleepiness?"十分困倦":
        c.Interaction.Sleepiness>CourtyardSurvival.NapSatisfied?"困意积累":"清醒";
    public static string Energy(Creature c)=>c.Energy<CourtyardSurvival.ExhaustedEnergy?"精疲力尽":c.Energy<35?"疲倦":"有体力";
    public static IEnumerable<string> Needs(Creature c)
    {
        if(c.Condition==PhysicalCondition.Dead||c.Family.ColdStorage.Stored)yield break;
        if(NaturalResources.WantsFood(c))yield return Food(c)+"，需要实际吃到食物。";
        if(NaturalResources.WantsWater(c))yield return Water(c)+"，需要实际喝到水。";
        if(c.Interaction.Sleepiness>=CourtyardSurvival.DaytimeSleepiness)yield return "十分困倦，需要睡眠；体力充足也不能代替补觉。";
        else if(c.Energy<CourtyardSurvival.ExhaustedEnergy)yield return "精疲力尽，需要休息恢复体力。";
        else if(c.Energy<35)yield return "体力不足，容易疲倦。";
        if(c.Family.SeekingWarmth)yield return "身体发冷，想靠近同伴或进屋保暖。";
        if(c.Interaction.Physiology.Bladder>=85||c.Interaction.Physiology.Bowel>=85)
            yield return c.Interaction.Physiology.Active?"正在排泄，结束后再处理其他需要。":"有排泄需要；睡眠时会暂时忍耐，迫切时需到屋外解决。";
    }
}

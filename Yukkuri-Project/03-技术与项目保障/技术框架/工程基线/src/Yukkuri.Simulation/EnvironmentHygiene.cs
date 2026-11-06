namespace Yukkuri.Simulation;

/// <summary>Live regional floor pollution. Never cached or serialized.</summary>
public static class EnvironmentHygiene
{
    public static int PenaltyPerPortion(RoomObjectKind kind)=>kind switch
    {RoomObjectKind.Feces=>8,RoomObjectKind.Urine=>5,_=>0};

    public static bool IsGroundWaste(WorldState state,RoomObject obj)=>
        PenaltyPerPortion(obj.Kind)>0&&obj.Portions>0&&!obj.Stored&&!obj.HeldByHand&&
        obj.CarrierId==null&&obj.Height==0&&
        !state.PortableMotions.Any(m=>m.Id==obj.Id&&(m.Held||m.Height>0||m.VerticalVelocity!=0));

    public static int Score(WorldState state,string regionId)
    {
        int score=100;
        foreach(var obj in state.RoomObjects)
        {
            if(obj.RegionId!=regionId||!IsGroundWaste(state,obj))continue;
            // Widen before multiplying; stop at zero even for very large stacks.
            long penalty=(long)PenaltyPerPortion(obj.Kind)*obj.Portions;
            if(penalty>=score)return 0;
            score-=(int)penalty;
        }
        return score;
    }

    // Approved 2026-11-02 curves; derived from current traits, never saved separately.
    public static double Requirement(Creature c)
    {
        double kindness=Math.Clamp(c.Personality.Kindness,0,1);
        double education=Math.Clamp(c.Interaction.RuleLearning,0,100);
        double byKindness=kindness<=.15?0:kindness<=.65?(kindness-.15)/.5*92:92+(kindness-.65)/.35*7;
        double byEducation=education<=50?education/50*90:90+(education-50)/50*9;
        return Math.Min(99,Math.Max(Math.Max(byKindness,byEducation),WorldSimulation.CleaningLicenseLevel(c)*24));
    }

    public static double Discomfort(WorldState state,Creature c)=>
        Math.Min(60,Math.Max(0,Requirement(c)-Score(state,c.RegionId))*.65);

    public static string DescribeIndividual(WorldState state,Creature c)=>
        $"个体卫生要求 {Requirement(c):0.##}/100 · 当前不适 {Discomfort(state,c):0.##}（馒馒来目标扣值）";

    public static string DescribeWaste(WorldState state,RoomObject obj)
    {
        string name=obj.Kind==RoomObjectKind.Urine?"尿液":"粪便";
        string impact=IsGroundWaste(state,obj)?$"每份扣 {PenaltyPerPortion(obj.Kind)} 分，区域最低 0 分":"当前不计地面评分";
        return $"{name} · {obj.Portions} 份 · {impact}";
    }
}

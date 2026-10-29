namespace Yukkuri.Simulation;

public static class RoomBallEnvironment
{
    public const double CurrentSpeed=.10;
    public static bool InWater(WorldState state,RoomObject ball)=>WoodlandLayout.HasRiver(state,ball.RegionId)&&ball.X>WoodlandLayout.RiverEdge(ball.Z);
    public static float CenterHeight(WorldState state,RoomObject ball)
    {
        float radius=InteractionFixtureGeometry.BallRadius;
        if(!InWater(state,ball))return radius;
        double overhang=(ball.X-WoodlandLayout.RiverEdge(ball.Z))/Math.Sqrt(1+.104*.104);
        // While part of the sphere still overlaps the bank, that dry edge supports it.
        double bankSupport=overhang<radius?Math.Sqrt(radius*radius-overhang*overhang):0;
        return (float)Math.Max(NaturalIntakeGeometry.WaterHeight+radius*.35,bankSupport);
    }
}

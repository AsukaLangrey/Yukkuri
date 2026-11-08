namespace Yukkuri.Simulation;

public readonly record struct WoodlandTree(double X,double Z,double Scale= .92)
{
    // The trunk leans slightly at body height; include that in its solid footprint.
    public double TrunkRadius => .22*Scale;
    public (double X,double Z,double Radius) TrunkAt(double height)
    {
        double u=Math.Clamp(height/(1.6*Scale),0,1);
        return (X+.13*u*u*Scale,Z,(.13-.065*u)*Scale);
    }
}

/// <summary>Shared by source art and movement. River mouths are approached from the dry west bank.</summary>
public static class WoodlandLayout
{
    public const double BankZ = 2.3*CourtyardGround.Expansion;
    public static IReadOnlyList<WoodlandTree> Trees { get; }=Array.AsReadOnly(new WoodlandTree[]
    {new(-6.7*CourtyardGround.Expansion,-4.8*CourtyardGround.Expansion),new(-5.7*CourtyardGround.Expansion,-6.7*CourtyardGround.Expansion),new(2.7*CourtyardGround.Expansion,-6.9*CourtyardGround.Expansion),new(-6.9*CourtyardGround.Expansion,4.6*CourtyardGround.Expansion)});
    public static double RiverEdge(double z) => CourtyardGround.Expansion*(6.35 + .16*Math.Sin(z/CourtyardGround.Expansion*.65));
    public static double DrinkX => RiverEdge(BankZ);
    public static bool HasRiver(WorldState state,string region) =>
        state.RoomObjects.Any(o=>o.RegionId==region&&o.Kind==RoomObjectKind.RiverWater);
    public static bool GroundFits(WorldState state,string region,double x,double z,double radius,bool allowWater=false)
    {
        if(!HasRiver(state,region))return true;
        // Account for the curved bank across the whole footprint, not only its centre.
        if(!allowWater&&x+radius*Math.Sqrt(1+.104*.104)>RiverEdge(z))return false;
        return Trees.All(tree=>Math.Pow(x-tree.X,2)+Math.Pow(z-tree.Z,2)>=Math.Pow(radius+tree.TrunkRadius,2));
    }
}

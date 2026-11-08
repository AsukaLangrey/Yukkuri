namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    // The courtyard uses paid stock for food and medicine. Preserve
    // the original unlimited-material sandbox outside the interaction courtyard.
    public bool UsesConsumableStock => State.InteractionPlaytest || !State.UnlimitedResources;
    public const int FoodBundlePrice=10;
    public const int FoodBundleCount=20;
}

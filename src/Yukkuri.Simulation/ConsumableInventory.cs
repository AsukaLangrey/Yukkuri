namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    // Playtest grants free purchases, not invisible food or medicine. Preserve
    // the original unlimited-material sandbox outside the interaction courtyard.
    public bool UsesConsumableStock => State.InteractionPlaytest || !State.UnlimitedResources;
    public const int FoodBundlePrice=10;
    public const int FoodBundleCount=20;
}

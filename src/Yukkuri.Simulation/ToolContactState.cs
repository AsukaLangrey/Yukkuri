namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    public int HitCount { get; set; }
    public double LastHitAt { get; set; } = -100;
    public double HitRemaining { get; set; }
    public double HeatTension { get; set; }
}

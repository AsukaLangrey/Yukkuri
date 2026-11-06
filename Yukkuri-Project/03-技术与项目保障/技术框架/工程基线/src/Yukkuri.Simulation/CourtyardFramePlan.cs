namespace Yukkuri.Simulation;

// No clock, backlog or saved state: each real frame admits at most 0.1 seconds.
// Stalls discard excess real time, as before. Integer speeds repeat that same
// accepted step, preserving the 1x observation order rather than enlarging dt.
public readonly record struct CourtyardFramePlan(int StepCount, double StepSeconds)
{
    public static bool IsSupportedSpeed(double speed, bool interactionPlaytest) =>
        double.IsFinite(speed) && (speed is >= .25 and <= 3 || interactionPlaytest && speed is 5 or 10);

    public static CourtyardFramePlan Create(double delta, double speed, bool paused, bool closeup)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (!IsSupportedSpeed(speed, true)) throw new ArgumentOutOfRangeException(nameof(speed));
        if (paused || delta == 0) return default;
        double accepted = Math.Min(.1, delta);
        if (closeup) return new(1, accepted);
        int steps = (int)Math.Ceiling(speed); // At most ten; never ceiling(float dt / .1).
        return new(steps, accepted * (speed / steps));
    }
}
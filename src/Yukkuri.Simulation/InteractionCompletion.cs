namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    // Only these tasks have a fixed uninterrupted contact duration. This is a
    // read-only view of their authoritative timer, never a presentation clock.
    public double? InteractionCompletionProgress
    {
        get
        {
            if(_interactionGesture is not { Refused:false } g)return null;
            bool timed=g.Input.TargetKind switch
            {
                InteractionTargetKind.Stem=>g.Input.Action is InteractionAction.SupportStem or InteractionAction.CutStem,
                InteractionTargetKind.DetachedStem=>g.Input.Action is InteractionAction.BottleStem or InteractionAction.ClearStem or
                    InteractionAction.Drink or InteractionAction.FeedMeal,
                _=>false
            };
            return timed?g.Applied?1:g.ContactActive?Math.Clamp(g.ContactElapsed/1.6,0,1):0:null;
        }
    }
}

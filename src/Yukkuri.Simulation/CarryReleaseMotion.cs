using System;
using System.Collections.Generic;
using System.Numerics;

namespace Yukkuri.Simulation;

// Average actual carried displacement over the last 80 ms, including time spent still.
public sealed class CarryReleaseMotion
{
    public const double WindowSeconds=.08;
    public const double ThrowSpeed=1.2;
    public const double MaximumSpeed=6;
    private readonly List<(Vector2 Travel,double Seconds)> _samples=new();
    private double _seconds;
    public Vector2 Velocity
    {
        get
        {
            if(_seconds<.008)return Vector2.Zero;
            Vector2 travel=Vector2.Zero;
            foreach(var sample in _samples)travel+=sample.Travel;
            var velocity=travel/(float)_seconds;
            float speed=velocity.Length();
            return speed>MaximumSpeed?velocity*(float)(MaximumSpeed/speed):velocity;
        }
    }
    public void Reset(){_samples.Clear();_seconds=0;}
    public void Sample(Vector2 travel,double seconds)
    {
        if(!float.IsFinite(travel.X)||!float.IsFinite(travel.Y)||!double.IsFinite(seconds)||seconds<=0||seconds>.15)
        {Reset();return;}
        _samples.Add((travel,seconds));_seconds+=seconds;
        while(_seconds>WindowSeconds&&_samples.Count>0)
        {
            var first=_samples[0];double remove=Math.Min(first.Seconds,_seconds-WindowSeconds);
            _seconds-=remove;
            if(remove>=first.Seconds)_samples.RemoveAt(0);
            else
            {
                _samples[0]=(first.Travel*(float)(1-remove/first.Seconds),first.Seconds-remove);
                break;
            }
        }
    }
}

public sealed partial class WorldSimulation
{
    public InteractionFeedback ReleaseDraggedCreature(string id,Vector2 velocity)
    {
        if(!float.IsFinite(velocity.X)||!float.IsFinite(velocity.Y))return Unavailable("松手速度无效，仍然托着它。");
        var c=InteractionTarget(id);
        double speed=velocity.Length();
        bool throwing=speed>=CarryReleaseMotion.ThrowSpeed;
        var invalid=ValidateInteraction(c,new(throwing?InteractionAction.Throw:InteractionAction.PutDown));
        if(invalid!=null)return Unavailable(invalid);
        if(!c!.Interaction.Carried)return Unavailable("现在没有托着它。");
        if(!throwing)return ExecuteInteraction(id,new(InteractionAction.PutDown,X:c.X,Z:c.Z));
        speed=Math.Min(speed,CarryReleaseMotion.MaximumSpeed);
        var i=c.Interaction;
        i.Lowering=false;i.Carried=false;i.Falling=true;
        i.VerticalVelocity=0;
        i.VelocityX=0;i.VelocityZ=0;
        Impulse(c,velocity.X,velocity.Y,speed);
        i.LastContact=InteractionAction.Throw.ToString();c.LastPlayerTouchAt=State.WorldSeconds;
        c.ActionCounts[nameof(InteractionAction.Throw)]=c.ActionCounts.GetValueOrDefault(nameof(InteractionAction.Throw))+1;
        RecordInteraction(c,"Throw","随着甩手的方向和速度被抛了出去。",Math.Max(.3,speed));
        return Feedback(c,"fall",Expression.Frightened,"它随着松手前的甩动方向和速度抛出。","fall");
    }
}

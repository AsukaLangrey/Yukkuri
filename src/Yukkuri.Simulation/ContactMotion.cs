using System.Numerics;

namespace Yukkuri.Simulation;

/// <summary>Pointer travel in contact space. Never uses movement of the character or camera.</summary>
public readonly record struct ContactMotionSample(double Distance, double Speed, double Force,
    double NormalSpeed, Vector3 Direction, double StopImpact = 0, double TangentFraction = 1,
    double ApproachSpeed = 0);

public sealed class ContactMotion
{
    // Tuning for the fictional soft body, not a physical mouse pressure measurement.
    public const double ResponseSeconds = .045;
    public const double MaximumSampleSeconds = .15;
    public const double StopConfirmationSeconds = .025;
    public const double MaximumBrakeSeconds = .09;
    public const double QuickSpeed = 2.5, StoppedSpeed = .8;
    public const double MinimumStrokeSeconds = .04, MinimumStrokeDistance = .18;
    public const double MinimumDeceleration = 25, ImpactCooldown = .25;
    public const double InwardSpeedWeight = 1.35, BaseForce = .12, ForcePerSpeed = .16;
    private string? _target;
    private double _speed, _forceSpeed;
    private double _previousSpeed, _quickSeconds, _quickDistance, _cooldown;
    private double _brakeSpeed, _brakeImpulseSpeed, _brakeSeconds, _stoppedSeconds;
    private Vector3 _travelDirection;

    public void Reset()
    { _target=null;_speed=0;_forceSpeed=0;_previousSpeed=0;_quickSeconds=0;_quickDistance=0;_cooldown=0;ClearBrake();_travelDirection=Vector3.Zero; }

    private void ClearBrake() { _brakeSpeed=0;_brakeImpulseSpeed=0;_brakeSeconds=0;_stoppedSeconds=0; }

    public ContactMotionSample Sample(string? target, Vector3 pointerTravel, Vector3 normal,
        double seconds, double bodyScale = 1, bool manipulating = true)
    {
        if (!manipulating || target == null || !double.IsFinite(seconds) || seconds <= 0 || seconds > MaximumSampleSeconds ||
            !double.IsFinite(bodyScale) || bodyScale <= 0 || !Finite(pointerTravel) || !Finite(normal))
        { Reset(); return default; }
        if(!float.IsFinite(pointerTravel.LengthSquared())||!float.IsFinite(normal.LengthSquared()))
        {Reset();return default;}
        if (target != _target)
        { Reset();_target = target; return default; }
        double distance = pointerTravel.Length() / bodyScale;
        double rawSpeed = distance / seconds;
        if(!double.IsFinite(distance)||!double.IsFinite(rawSpeed)){Reset();return default;}
        Vector3 unitNormal=normal.LengthSquared()>.000001f?Vector3.Normalize(normal):Vector3.Zero;
        double signedNormalDistance=Vector3.Dot(pointerTravel,unitNormal)/bodyScale;
        double normalSpeed=signedNormalDistance/seconds; // positive = away from the surface
        double approachSpeed=Math.Max(0,-normalSpeed);
        double tangentDistance=(pointerTravel-unitNormal*(float)(signedNormalDistance*bodyScale)).Length()/bodyScale;
        double tangentFraction=distance>.000001?Math.Clamp(tangentDistance/distance,0,1):0;
        double approachFraction=rawSpeed>0?Math.Clamp(approachSpeed/rawSpeed,0,1):0;
        double effectiveSpeed=rawSpeed*Math.Sqrt(tangentFraction*tangentFraction+Math.Pow(approachFraction*InwardSpeedWeight,2));
        if(!double.IsFinite(effectiveSpeed)){Reset();return default;}
        Vector3 direction=pointerTravel.LengthSquared()>.000001f?Vector3.Normalize(pointerTravel):Vector3.Zero;
        _cooldown=Math.Max(0,_cooldown-seconds);
        double impact=0;
        // Pulling away is not driving into or across the body. Cancel the stroke even
        // while the ray still intersects skin, so its later endpoint cannot become a hit.
        bool withdrawing=normalSpeed>StoppedSpeed&&normalSpeed>rawSpeed*tangentFraction;
        if(withdrawing){ClearBrake();_quickSeconds=0;_quickDistance=0;_previousSpeed=0;}
        // A brake is a gesture, not an inference about the user's character or a mouse pressure sensor.
        // It needs an engaged, uninterrupted contact stroke followed by a sharp stop. Release,
        // leaving the body and stalled frames reset the stroke instead of completing a hit.
        if(rawSpeed<QuickSpeed&&_brakeSpeed==0&&_cooldown<=0&&_previousSpeed>=QuickSpeed&&
            _quickSeconds>=MinimumStrokeSeconds&&_quickDistance>=MinimumStrokeDistance)
        {
            _brakeSpeed=_previousSpeed;
            // The last frame can straddle the endpoint. Use the coherent stroke's
            // time-weighted speed for strength, not that arbitrary partial frame.
            _brakeImpulseSpeed=_quickDistance/_quickSeconds;
        }
        if(_brakeSpeed>0)
        {
            _brakeSeconds+=seconds;
            _stoppedSeconds=rawSpeed<StoppedSpeed?_stoppedSeconds+seconds:0;
            // A missing pointer packet is not a stop. Confirm a short settled endpoint;
            // a resumed stroke cancels it, and a gentle multi-frame slowdown expires it.
            if(rawSpeed>=QuickSpeed||_brakeSeconds>MaximumBrakeSeconds)ClearBrake();
            else if(_stoppedSeconds>=StopConfirmationSeconds&&(_brakeSpeed-rawSpeed)/_brakeSeconds>MinimumDeceleration)
            {
                impact=Math.Clamp((_brakeImpulseSpeed-StoppedSpeed)/6,.15,1);
                direction=_travelDirection;_cooldown=ImpactCooldown;ClearBrake();
            }
        }
        if(rawSpeed>=QuickSpeed&&!withdrawing)
        {
            if(_travelDirection.LengthSquared()>.1f&&Vector3.Dot(direction,_travelDirection)<.4f)
            {_quickDistance=0;_quickSeconds=0;}
            _quickSeconds+=seconds;_quickDistance+=distance;_travelDirection=direction;
        }
        else {_quickSeconds=0;_quickDistance=0;}
        _previousSpeed=withdrawing?0:rawSpeed;
        // Integrate a first-order response exactly over the sample. This is much less sensitive
        // to frame rate than per-frame lerps, while an intentional fast sweep remains strong.
        double decay = Math.Exp(-seconds / ResponseSeconds);
        double x=seconds/ResponseSeconds;
        double oneMinusDecay=x<.0001?x*(1-x*.5+x*x/6):1-decay;
        double averageSpeed = rawSpeed + (_speed - rawSpeed) * ResponseSeconds * oneMinusDecay / seconds;
        _speed = rawSpeed + (_speed - rawSpeed) * decay;
        double averageForceSpeed=effectiveSpeed+(_forceSpeed-effectiveSpeed)*ResponseSeconds*oneMinusDecay/seconds;
        _forceSpeed=effectiveSpeed+(_forceSpeed-effectiveSpeed)*decay;
        double force = distance > .000001 ? Math.Clamp(BaseForce + averageForceSpeed * ForcePerSpeed, BaseForce, 1) : BaseForce;
        return new(distance, averageSpeed, force, normalSpeed,direction,impact,tangentFraction,approachSpeed);
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}

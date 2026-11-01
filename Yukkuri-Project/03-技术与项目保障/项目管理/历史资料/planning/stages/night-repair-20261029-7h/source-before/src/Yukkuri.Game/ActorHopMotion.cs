using System;
using System.Numerics;

public readonly record struct HopFrame(Vector3 Scale, float Lift, float Pitch, float Travel);

// Presentation only: the simulation still owns destinations, collisions and injury rules.
// Ground phases hold the rendered position; all travel happens during the ballistic arc.
public sealed class ActorHopMotion
{
    public const float Duration=.50f, Launch=.08f, Touchdown=.39f;
    private bool _initialized,_active;
    private float _age;
    private Vector2 _start,_end,_position;
    public bool Active=>_active;
    public float Heading {get;private set;}
    public bool Turning {get;private set;}
    public HopFrame Frame=>_active?Sample(_age):new(Vector3.One,0,0,1);
    public Vector2 Position=>_position;

    public void Reset(Vector2 target)
    { _initialized=true;_active=false;Turning=false;_age=0;_start=_end=_position=target; }
    public void Face(float heading) { if(!_active)Heading=heading; }

    public void Advance(Vector2 target,float delta,bool allowed,bool paused)
    {
        if(!_initialized||!allowed||Vector2.Distance(_position,target)>1.5f){Reset(target);return;}
        if(paused)return;
        delta=Math.Clamp(delta,0,.1f);
        Turning=false;
        if(!_active)
        {
            if(Vector2.DistanceSquared(_position,target)<.0001f){_position=target;return;}
            var direction=target-_position;
            float desired=MathF.Atan2(direction.X,direction.Y);
            float turn=MathF.IEEERemainder(desired-Heading,MathF.Tau);
            float step=MathF.Tau*1.5f*delta;
            Heading=MathF.IEEERemainder(Heading+Math.Clamp(turn,-step,step),MathF.Tau);
            // Turn on the ground before launching. Lock this heading and destination
            // through the entire hop, even if the simulation changes direction midair.
            if(MathF.Abs(turn)>step+.00001f){Turning=true;return;}
            Heading=desired;
            _active=true;_age=0;_start=_position;_end=target;
        }
        _age+=delta;
        _position=Vector2.Lerp(_start,_end,Frame.Travel);
        if(_age>=Duration){_position=_end;_active=false;_age=0;}
    }

    public static HopFrame Sample(float age)
    {
        age=Math.Clamp(age,0,Duration);
        float height=1,lift=0,pitch=0,travel=0;
        if(age<Launch)
        {
            // A short, planted anticipation, then a fast release into the air.
            float t=age/Launch;
            height=t<.65f?Lerp(1,.86f,Smooth(t/.65f)):Lerp(.86f,1.10f,Smooth((t-.65f)/.35f));
        }
        else if(age<Touchdown)
        {
            float t=(age-Launch)/(Touchdown-Launch);
            lift=.27f*4*t*(1-t);
            height=t<.45f?Lerp(1.10f,1,Smooth(t/.45f)):Lerp(1,1.06f,Smooth((t-.45f)/.55f));
            pitch=-6*MathF.Sin(t*MathF.PI);
            travel=t;
        }
        else
        {
            float t=(age-Touchdown)/(Duration-Touchdown);
            height=t<.25f?Lerp(1.06f,.82f,Smooth(t/.25f)):Lerp(.82f,1,Smooth((t-.25f)/.75f));
            travel=1;
        }
        // Preserve volume and the underside's contact point during compression.
        float width=1/MathF.Sqrt(height);
        return new(new(width,height,width),lift+.46f*(height-1),pitch,travel);
    }
    private static float Smooth(float t)=>t*t*(3-2*t);
    private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
}

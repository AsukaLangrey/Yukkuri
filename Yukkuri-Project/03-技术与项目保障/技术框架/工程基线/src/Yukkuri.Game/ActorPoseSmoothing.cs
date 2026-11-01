using System;
using System.Numerics;

// Shared pose transition used by the actor and frame-by-frame source previews.
public sealed class ActorPoseSmoothing
{
    private bool _initialized;
    public Vector3 Scale {get;private set;}
    public Vector3 Offset {get;private set;}
    public Quaternion Rotation {get;private set;}=Quaternion.Identity;
    public void BeginFrom(Vector3 scale,Vector3 offset,Quaternion rotation)
    {Scale=scale;Offset=offset;Rotation=rotation;_initialized=true;}
    public void Advance(Vector3 scale,Vector3 offset,Quaternion rotation,float delta,bool paused,bool groundIntake=false)
    {
        if(!_initialized){Scale=scale;Offset=offset;Rotation=rotation;_initialized=true;}
        if(paused||!float.IsFinite(delta)||delta<=0)return;
        // Ground intake already has an authored ease-in. A shorter finishing tail
        // lets that approach reach the source at the actual entry event.
        float blend=1-MathF.Exp(-delta*(groundIntake?18:14));
        Scale=Vector3.Lerp(Scale,scale,blend);
        Offset=Vector3.Lerp(Offset,offset,blend);
        Rotation=Quaternion.Normalize(Quaternion.Slerp(Rotation,rotation,blend));
    }
    public void Face(Quaternion rotation)=>Rotation=rotation;
}

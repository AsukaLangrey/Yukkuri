using System;
using System.Numerics;

public static class InteractionCameraOrbit
{
    public const float DefaultPitch=14.5f;
    public static Vector3 Offset(float yawDegrees,float pitchDegrees,float focusHeight)
    {
        float yaw=yawDegrees*MathF.PI/180,pitch=Math.Clamp(pitchDegrees,-25,65)*MathF.PI/180;
        const float radius=5.8f;
        return new(MathF.Sin(yaw)*MathF.Cos(pitch)*radius,
            MathF.Max(.15f-focusHeight,MathF.Sin(pitch)*radius),
            MathF.Cos(yaw)*MathF.Cos(pitch)*radius);
    }
    public static float Size(float baseSize,float zoom)=>baseSize*Math.Clamp(zoom,.65f,1.6f);
    public static bool TryPlaneHit(Vector3 origin,Vector3 direction,float height,out Vector3 hit)
    {
        hit=default;
        if(MathF.Abs(direction.Y)<.0001f)return false;
        float distance=(height-origin.Y)/direction.Y;
        if(!float.IsFinite(distance)||distance<0)return false;
        hit=origin+direction*distance;
        return float.IsFinite(hit.X)&&float.IsFinite(hit.Y)&&float.IsFinite(hit.Z);
    }
    public static Vector3 ViewMotion(Vector3 right,Vector3 up,Vector2 pixels,float size,float viewportHeight)=>
        (right*pixels.X-up*pixels.Y)*size/MathF.Max(1,viewportHeight);

    public static Vector2 ContactImpulse(Vector3 outwardNormal,Vector3 cameraForward)
    {
        var direction=new Vector2(-outwardNormal.X,-outwardNormal.Z);
        if(direction.LengthSquared()<.0025f)direction=new(cameraForward.X,cameraForward.Z);
        return direction.LengthSquared()<.00001f?new(0,-1):Vector2.Normalize(direction);
    }

    public static Vector3 GroundDrag(Vector3 right,Vector3 forward,Vector2 pixels,float size,float viewportHeight)
    {
        // Relative pointer travel keeps height changes and camera recentering from moving the oil.
        // Limit depth gain near the horizon; at normal downward angles this matches plane dragging.
        var across=new Vector3(right.X,0,right.Z);
        var ahead=new Vector3(forward.X,0,forward.Z);
        if(across.LengthSquared()<.0001f||ahead.LengthSquared()<.0001f)return Vector3.Zero;
        return (Vector3.Normalize(across)*pixels.X-Vector3.Normalize(ahead)*pixels.Y/MathF.Max(.25f,MathF.Abs(forward.Y)))*size/MathF.Max(1,viewportHeight);
    }
}

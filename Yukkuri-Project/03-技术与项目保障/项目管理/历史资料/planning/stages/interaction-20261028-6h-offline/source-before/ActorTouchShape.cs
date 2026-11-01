using System;
using System.Numerics;

public static class ActorTouchShape
{
    public static Vector3 Press(float force)
    {
        force=Math.Clamp(force,0,1);
        return new(1+force*.20f,1-force*.26f,1+force*.08f);
    }
    public static Vector3 Squeeze(float force)
    {
        force=Math.Clamp(force,0,1);
        return new(1-force*.22f,1+force*.13f,1+force*.08f);
    }
}

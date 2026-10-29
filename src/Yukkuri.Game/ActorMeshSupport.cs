using System;
using System.Numerics;

// The minimum of a triangle mesh under an affine transform occurs at a vertex.
// Use actual vertices when its enclosing box threatens to push the actor upward.
public static class ActorMeshSupport
{
    public static float LowestY(ReadOnlySpan<Vector3> vertices,Vector3 verticalRow,float originY)
    {
        float lowest=float.PositiveInfinity;
        foreach(var p in vertices)lowest=MathF.Min(lowest,Vector3.Dot(p,verticalRow)+originY);
        return lowest;
    }
}

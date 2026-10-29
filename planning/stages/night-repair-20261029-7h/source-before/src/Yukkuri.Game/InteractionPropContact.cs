using System;
using System.Numerics;
using Yukkuri.Simulation;

public static class InteractionPropContact
{
    public static bool RayHit(RoomObjectKind kind,Vector3 position,Vector3 origin,Vector3 direction,out float distance)
    {
        distance=0;
        var radii=kind switch {RoomObjectKind.Ball=>new Vector3(InteractionFixtureGeometry.BallRadius),RoomObjectKind.Meal=>new(.32f,.08f,.32f),RoomObjectKind.Sweet=>new(.185f,.05f,.185f),RoomObjectKind.Treasure=>new(.11f,.08f,.09f),_=>Vector3.Zero};
        if(radii.X==0)return false;
        var center=position+Vector3.UnitY*radii.Y;
        var start=(origin-center)/radii;var ray=direction/radii;
        float a=ray.LengthSquared(),b=2*Vector3.Dot(start,ray),c=start.LengthSquared()-1;
        float discriminant=b*b-4*a*c;
        if(a<.000001f||discriminant<0)return false;
        float root=MathF.Sqrt(discriminant),near=(-b-root)/(2*a),far=(-b+root)/(2*a);
        distance=near>=0?near:far;
        return float.IsFinite(distance)&&distance>=0;
    }
}

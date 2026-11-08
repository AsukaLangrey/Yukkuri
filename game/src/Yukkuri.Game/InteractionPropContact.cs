using System;
using System.Numerics;
using Yukkuri.Simulation;

public static class InteractionPropContact
{
    public static bool RayHit(RoomObjectKind kind,Vector3 position,Vector3 origin,Vector3 direction,out float distance,float? centerHeight=null)
    {
        // Match the visible puddle and both pellets, so invisible margins do not
        // intercept a ground click or hide another object behind the waste.
        if(kind==RoomObjectKind.Urine)
            return RayEllipsoid(position+new Vector3(0,.014f,0),new(.30f,.012f,.23f),origin,direction,out distance);
        if(kind==RoomObjectKind.Feces)
        {
            bool first=RayEllipsoid(position+new Vector3(0,.07f,0),new(.13f,.07f,.115f),origin,direction,out float firstDistance);
            bool second=RayEllipsoid(position+new Vector3(.08f,.05f,.09f),new(.085f,.05f,.08f),origin,direction,out float secondDistance);
            distance=first&&second?MathF.Min(firstDistance,secondDistance):first?firstDistance:secondDistance;
            return first||second;
        }
        distance=0;
        var radii=kind switch {RoomObjectKind.Ball=>new Vector3(InteractionFixtureGeometry.BallRadius),RoomObjectKind.Meal=>new(.32f,.08f,.32f),RoomObjectKind.Sweet=>new(.185f,.05f,.185f),RoomObjectKind.Water=>new(.18f,.08f,.18f),RoomObjectKind.Leaves=>new(.55f,.08f,.55f),RoomObjectKind.RiverWater=>new(.42f,.04f,.55f),RoomObjectKind.Treasure=>new(.11f,.08f,.09f),_=>Vector3.Zero};
        if(radii.X==0)return false;
        var center=position+Vector3.UnitY*(centerHeight??radii.Y);
        return RayEllipsoid(center,radii,origin,direction,out distance);
    }
    private static bool RayEllipsoid(Vector3 center,Vector3 radii,Vector3 origin,Vector3 direction,out float distance)
    {
        distance=0;
        var start=(origin-center)/radii;var ray=direction/radii;
        float a=ray.LengthSquared(),b=2*Vector3.Dot(start,ray),c=start.LengthSquared()-1;
        float discriminant=b*b-4*a*c;
        if(a<.000001f||discriminant<0)return false;
        float root=MathF.Sqrt(discriminant),near=(-b-root)/(2*a),far=(-b+root)/(2*a);
        distance=near>=0?near:far;
        return float.IsFinite(distance)&&distance>=0;
    }
}

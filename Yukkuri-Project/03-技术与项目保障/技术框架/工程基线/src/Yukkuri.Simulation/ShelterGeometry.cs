using System.Numerics;

namespace Yukkuri.Simulation;

public readonly record struct ShelterWallBox(Vector3 Center,Vector3 Size);
public static class ShelterGeometry
{
    public const double InteriorWidth=2.30,InteriorDepth=2.40,DoorWidth=2.10,DoorHeight=2.30,CeilingY=2.35,
        WallThickness=.10,OuterWidth=2.50,OuterDepth=2.60;
    private static readonly ShelterWallBox[] OpenWalls=
    [
        new(new(-1.20f,1.175f,0),new(.10f,2.35f,2.60f)),
        new(new(1.20f,1.175f,0),new(.10f,2.35f,2.60f)),
        new(new(0,1.175f,-1.25f),new(2.50f,2.35f,.10f)),
        new(new(-1.15f,1.175f,1.25f),new(.20f,2.35f,.10f)),
        new(new(1.15f,1.175f,1.25f),new(.20f,2.35f,.10f))
    ];
    private static readonly ShelterWallBox[] ClosedWalls=[..OpenWalls,new(new(0,1.15f,1.25f),new(2.10f,2.30f,.08f))];
    public static IReadOnlyList<ShelterWallBox> WallBoxes(bool doorOpen)=>doorOpen?OpenWalls:ClosedWalls;
    public static double OutsideZ(double passageRadius)=>OuterDepth*.5+passageRadius+.12;
    public static bool DoorControlHit(double localX,double height,double localZ)=>
        double.IsFinite(localX)&&double.IsFinite(height)&&double.IsFinite(localZ)&&
        Math.Abs(localX)<=1.15&&Math.Abs(localZ-1.25)<=.12&&height>=0&&height<=CeilingY;
    public static bool FullyInside(double x,double z,double radius)=>Math.Abs(x)+radius<=InteriorWidth*.5+.00001&&Math.Abs(z)+radius<=InteriorDepth*.5+.00001;
    public static bool OverlapsInterior(double x,double z,double radius)=>
        Math.Abs(x)<InteriorWidth*.5+radius&&Math.Abs(z)<InteriorDepth*.5+radius;
    public static bool CircleTouchesBox(double x,double z,double radius,ShelterWallBox box)
    {
        double dx=Math.Max(0,Math.Abs(x-box.Center.X)-box.Size.X*.5),dz=Math.Max(0,Math.Abs(z-box.Center.Z)-box.Size.Z*.5);
        return dx*dx+dz*dz<radius*radius-1e-9;
    }
    public static bool FitsLocalCircle(double x,double z,double radius,bool doorOpen)=>
        double.IsFinite(x)&&double.IsFinite(z)&&double.IsFinite(radius)&&radius>=0&&
        !WallBoxes(doorOpen).Any(w=>CircleTouchesBox(x,z,radius,w));
    public static bool SweptCircleClear(double fromX,double fromZ,double toX,double toZ,double radius,bool doorOpen)
    {
        if(!new[]{fromX,fromZ,toX,toZ,radius}.All(double.IsFinite)||radius<0)return false;
        // A swept circle is conservatively tested against each wall's expanded
        // rectangle. No frame or cursor jump can tunnel across its thickness.
        foreach(var wall in WallBoxes(doorOpen))
            if(SegmentBox(fromX,fromZ,toX,toZ,wall.Center.X-wall.Size.X*.5-radius,wall.Center.X+wall.Size.X*.5+radius,
                wall.Center.Z-wall.Size.Z*.5-radius,wall.Center.Z+wall.Size.Z*.5+radius))return false;
        return true;
    }
    private static bool SegmentBox(double ax,double az,double bx,double bz,double minX,double maxX,double minZ,double maxZ)
    {
        double start=0,end=1;
        bool Axis(double origin,double delta,double minimum,double maximum)
        {
            if(Math.Abs(delta)<1e-12)return origin>minimum+1e-8&&origin<maximum-1e-8;
            double a=(minimum-origin)/delta,b=(maximum-origin)/delta;if(a>b)(a,b)=(b,a);
            start=Math.Max(start,a);end=Math.Min(end,b);return start<end-1e-8;
        }
        return Axis(ax,bx-ax,minX,maxX)&&Axis(az,bz-az,minZ,maxZ)&&end>0&&start<1;
    }
}

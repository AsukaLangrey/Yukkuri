using System.Numerics;

namespace Yukkuri.Simulation;

public readonly record struct ShelterWallBox(Vector3 Center,Vector3 Size);
public static class ShelterGeometry
{
    public const double SharedScale=1.65;
    public static double Scale(ShelterState shelter)=>shelter.Capacity==5?SharedScale:1;
    public static double ApproachDepth(double scale)=>OuterDepth*.5*scale+1.25;
    public static bool FullyInside(double x,double z,double radius,double scale)=>FullyInside(x/scale,z/scale,radius/scale);
    public static bool OverlapsInterior(double x,double z,double radius,double scale)=>OverlapsInterior(x/scale,z/scale,radius/scale);
    public static bool FitsLocalCircle(double x,double z,double radius,bool open,double scale)=>FitsLocalCircle(x/scale,z/scale,radius/scale,open);
    public static bool SweptCircleClear(double ax,double az,double bx,double bz,double radius,bool open,double scale)=>
        SweptCircleClear(ax/scale,az/scale,bx/scale,bz/scale,radius/scale,open);
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
        foreach(var wall in WallBoxes(doorOpen))
        {
            double minX=wall.Center.X-wall.Size.X*.5,maxX=wall.Center.X+wall.Size.X*.5;
            double minZ=wall.Center.Z-wall.Size.Z*.5,maxZ=wall.Center.Z+wall.Size.Z*.5;
            double ax=fromX-Math.Clamp(fromX,minX,maxX),az=fromZ-Math.Clamp(fromZ,minZ,maxZ);
            double bx=toX-Math.Clamp(toX,minX,maxX),bz=toZ-Math.Clamp(toZ,minZ,maxZ);
            double dx=toX-fromX,dz=toZ-fromZ,length2=dx*dx+dz*dz,radius2=radius*radius;
            double fromDistance=ax*ax+az*az,toDistance=bx*bx+bz*bz;
            // Growth can enlarge a body already resting against a wall. Permit only
            // a short outward step that fully clears this existing overlap.
            if(fromDistance<radius2-1e-9&&fromDistance>1e-12&&toDistance>=radius2-1e-9&&
                length2<=.01&&ax*dx+az*dz>0)continue;
            if(SegmentBox(fromX,fromZ,toX,toZ,minX,maxX,minZ,maxZ))return false;
            double distance=Math.Min(fromDistance,toDistance);
            foreach(double x in new[]{minX,maxX})foreach(double z in new[]{minZ,maxZ})
            {
                double t=length2<1e-20?0:Math.Clamp(((x-fromX)*dx+(z-fromZ)*dz)/length2,0,1);
                double vx=x-fromX-t*dx,vz=z-fromZ-t*dz;distance=Math.Min(distance,vx*vx+vz*vz);
            }
            // Exact segment-to-box distance keeps rounded corner clearance; the old
            // expanded rectangle trapped growing bodies that were outside the wall.
            if(distance<radius2-1e-9)return false;
        }
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

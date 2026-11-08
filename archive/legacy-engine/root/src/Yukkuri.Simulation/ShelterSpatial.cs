namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public static double ShelterPassageRadius(Creature c)=>ShelterBodyEnvelope.Radius(c);
    private bool ShelterBodyPresent(Creature c,ShelterState s)=>c.RegionId==s.RegionId&&!c.Family.ColdStorage.Stored&&
        !c.Family.Memorialized&&ShelterGeometry.OverlapsInterior(c.X-s.X,c.Z-s.Z,ShelterPassageRadius(c));
    private bool ShelterFullyInside(Creature c,ShelterState s)=>ShelterGeometry.FullyInside(c.X-s.X,c.Z-s.Z,ShelterPassageRadius(c));
    private Creature? PhysicalShelterGuest(ShelterState s)=>State.Creatures.FirstOrDefault(c=>ShelterBodyPresent(c,s)&&
        Math.Abs(c.X-s.X)<ShelterGeometry.InteriorWidth*.5&&c.Z-s.Z<ShelterGeometry.OuterDepth*.5&&c.Z-s.Z>-ShelterGeometry.OuterDepth*.5);
    private bool ShelterGroundFits(string region,double x,double z,double radius,bool allowInterior=true)
    {
        foreach(var s in State.Shelters.Where(s=>s.RegionId==region))
            if(!ShelterGeometry.FitsLocalCircle(x-s.X,z-s.Z,radius,s.DoorOpen)||
                !allowInterior&&ShelterGeometry.OverlapsInterior(x-s.X,z-s.Z,radius))return false;
        return true;
    }
    private bool ShelterBodyFits(Creature c,double x,double z,double minimumRadius=0)
    {
        if(State.Shelters.Count==0)return true;
        double radius=Math.Max(minimumRadius,ShelterPassageRadius(c));
        foreach(var s in State.Shelters.Where(s=>s.RegionId==c.RegionId))
        {
            if(!ShelterGeometry.FitsLocalCircle(x-s.X,z-s.Z,radius,s.DoorOpen))return false;
            bool entering=Math.Abs(x-s.X)<ShelterGeometry.InteriorWidth*.5&&z-s.Z<ShelterGeometry.OuterDepth*.5+radius&&z-s.Z>-ShelterGeometry.OuterDepth*.5-radius;
            if(entering)
            {
                if(ShelterBodyEnvelope.Height(c)+c.Interaction.Height>ShelterGeometry.CeilingY-.015)return false;
                if(AttachedStem(c)!=null&&!ShelterBodyPresent(c,s))return false;
                if(s.GuestId!=null&&s.GuestId!=c.Id&&ValidShelterGuest(s)!=null)return false;
                if(State.Creatures.Any(other=>other!=c&&ShelterBodyPresent(other,s)&&
                    Math.Abs(other.X-s.X)<ShelterGeometry.InteriorWidth*.5&&other.Z-s.Z<ShelterGeometry.OuterDepth*.5))return false;
                if(State.RoomObjects.Any(o=>o.RegionId==c.RegionId&&!o.Stored&&!o.HeldByHand&&o.CarrierId==null&&
                    o.Kind is not (RoomObjectKind.RiverWater or RoomObjectKind.Urine or RoomObjectKind.Feces)&&
                    Distance(x,z,o.X,o.Z)<radius+(o.Kind==RoomObjectKind.Cushion?1.3:StationObjectRadius(o.Kind))))return false;
            }
        }
        return true;
    }
    private bool ShelterMovePathClear(Creature c,double x,double z,double? height=null)
    {
        if(State.Shelters.Count==0)return true;
        double radius=ShelterPassageRadius(c);
        foreach(var s in State.Shelters.Where(s=>s.RegionId==c.RegionId))
        {
            if(!ShelterGeometry.SweptCircleClear(c.X-s.X,c.Z-s.Z,x-s.X,z-s.Z,radius,s.DoorOpen))return false;
            if((ShelterGeometry.OverlapsInterior(c.X-s.X,c.Z-s.Z,radius)||ShelterGeometry.OverlapsInterior(x-s.X,z-s.Z,radius))&&
                ShelterBodyEnvelope.Height(c)+(height??c.Interaction.Height)>ShelterGeometry.CeilingY-.015)return false;
        }
        return true;
    }
    private bool ShelterSightClear(string region,double ax,double az,double bx,double bz)=>State.Shelters
        .Where(s=>s.RegionId==region).All(s=>ShelterGeometry.SweptCircleClear(ax-s.X,az-s.Z,bx-s.X,bz-s.Z,0,s.DoorOpen));
    private bool ShelterObjectPathClear(string region,double ax,double az,double bx,double bz,double radius)=>State.Shelters
        .Where(s=>s.RegionId==region).All(s=>ShelterGeometry.SweptCircleClear(ax-s.X,az-s.Z,bx-s.X,bz-s.Z,radius,s.DoorOpen));
    private double ShelterSafeHeight(Creature c,double requested)
    {
        double result=requested;
        foreach(var s in State.Shelters.Where(s=>s.RegionId==c.RegionId&&
            ShelterGeometry.OverlapsInterior(c.X-s.X,c.Z-s.Z,ShelterPassageRadius(c))))
            result=Math.Min(result,Math.Max(0,ShelterGeometry.CeilingY-ShelterBodyEnvelope.Height(c)-.03));
        return result;
    }
    private double ShelterPortableHeight(string id,double requested)
    {
        if(State.Creatures.Find(c=>c.Id==id) is { } body)return ShelterSafeHeight(body,requested);
        if(PortablePosition(id) is not { } point)return requested;
        string? region=State.RoomObjects.Find(o=>o.Id==id)?.RegionId??Station(id)?.RegionId??DetachedStem(id)?.RegionId;
        return State.Shelters.Any(s=>s.RegionId==region&&ShelterGeometry.OverlapsInterior(point.X-s.X,point.Y-s.Z,.4))?
            Math.Min(requested,.8):requested;
    }
    public bool CanPlaceShelterAt(double x,double z)=>ShelterWorldError()==null&&CanPlaceShelter(State.ActiveRegionId,x,z);
    private bool CanPlaceShelter(string region,double x,double z)
    {
        double halfX=ShelterGeometry.OuterWidth*.5,halfZ=ShelterGeometry.OuterDepth*.5;
        // Reserve the real building and front approach area, not a circular
        // collider that would make the interior inaccessible after placement.
        if(!double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)+halfX>7.8||z-halfZ< -7.8||z+2.55>7.8)return false;
        foreach(double dx in new[]{-halfX,0,halfX})foreach(double dz in new[]{-halfZ,0,halfZ,2.55})
            if(!WoodlandLayout.GroundFits(State,region,x+dx,z+dz,.08))return false;
        if(WoodlandLayout.HasRiver(State,region)&&WoodlandLayout.Trees.Any(t=>
            Math.Abs(t.X-x)<halfX+t.TrunkRadius&&t.Z-z> -halfZ-t.TrunkRadius&&t.Z-z<2.55+t.TrunkRadius))return false;
        bool Area(double px,double pz,double radius)=>Math.Abs(px-x)<halfX+radius&&pz-z> -halfZ-radius&&pz-z<2.55+radius;
        if(State.Creatures.Any(c=>c.RegionId==region&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&Area(c.X,c.Z,ShelterPassageRadius(c))))return false;
        if(State.RoomObjects.Any(o=>o.RegionId==region&&!o.Stored&&o.CarrierId==null&&o.Kind!=RoomObjectKind.RiverWater&&Area(o.X,o.Z,o.Kind==RoomObjectKind.Cushion?1.3:StationObjectRadius(o.Kind))))return false;
        if(State.DetachedStemBroods.Any(s=>s.RegionId==region&&!s.Cleared&&s.Phase!=DetachedStemPhase.Born&&Area(s.X,s.Z,StemVesselRadius)))return false;
        if(State.SupplyStations.Any(s=>s.RegionId==region&&(Area(s.X,s.Z,SupplyStationRadius)||Area(s.X,s.Z+SupplyStationOutletOffset,.32))))return false;
        return !State.Shelters.Any(s=>s.RegionId==region&&Math.Abs(s.X-x)<ShelterGeometry.OuterWidth+.1&&Math.Abs(s.Z-z)<3.9);
    }
    private bool ShelterDoorClear(ShelterState s)
    {
        var gate=new ShelterWallBox(new(0,1.15f,1.25f),new(2.30f,2.30f,.24f));
        if(State.Creatures.Any(c=>c.RegionId==s.RegionId&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&
            ShelterGeometry.CircleTouchesBox(c.X-s.X,c.Z-s.Z,ShelterPassageRadius(c),gate)))return false;
        if(State.RoomObjects.Any(o=>o.RegionId==s.RegionId&&!o.Stored&&o.CarrierId==null&&o.Kind!=RoomObjectKind.RiverWater&&
            ShelterGeometry.CircleTouchesBox(o.X-s.X,o.Z-s.Z,o.Kind==RoomObjectKind.Cushion?1.3:StationObjectRadius(o.Kind),gate)))return false;
        return !State.DetachedStemBroods.Any(b=>b.RegionId==s.RegionId&&!b.Cleared&&b.Phase!=DetachedStemPhase.Born&&
            ShelterGeometry.CircleTouchesBox(b.X-s.X,b.Z-s.Z,StemVesselRadius,gate))&&
            !State.SupplyStations.Any(b=>b.RegionId==s.RegionId&&ShelterGeometry.CircleTouchesBox(b.X-s.X,b.Z-s.Z,SupplyStationRadius,gate));
    }
}

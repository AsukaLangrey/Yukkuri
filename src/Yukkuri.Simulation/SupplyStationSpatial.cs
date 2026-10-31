namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private bool ClearOfStations(string region,double x,double z,double radius,string? ignore=null)=>
        !State.SupplyStations.Any(s=>s.Id!=ignore&&s.RegionId==region&&Distance(x,z,s.X,s.Z)<radius+SupplyStationRadius);
    public bool CanPlaceSupplyStationAt(SupplyStationKind kind,double x,double z)=>Enum.IsDefined(kind)&&
        StationWorldError()==null&&CanPlaceStation(State.ActiveRegionId,x,z);
    private bool CanStationOutletFit(string region,double x,double z,string? stationId,string? outletId,bool existingPlate=false)
    {
        const double radius=.32;
        if(!double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)>8-radius||Math.Abs(z)>8-radius||
            !WoodlandLayout.GroundFits(State,region,x,z,radius)||!ClearOfStations(region,x,z,radius,stationId))return false;
        if(State.DetachedStemBroods.Any(s=>s.RegionId==region&&!s.Cleared&&s.Phase!=DetachedStemPhase.Born&&Distance(x,z,s.X,s.Z)<radius+StemVesselRadius))return false;
        if(!existingPlate&&State.Creatures.Any(c=>c.RegionId==region&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&
            !(c.Interaction.Height>1&&c.Interaction.Restraint==RestraintKind.None)&&Distance(c.X,c.Z,x,z)<InteractionFootprint(c)+radius))return false;
        // A held supply keeps its cancel-return ground anchor reserved for new
        // output only. A family's carried parcel has a real moving drop position.
        return !State.RoomObjects.Any(o=>o.Id!=outletId&&!o.Stored&&o.CarrierId==null&&o.RegionId==region&&
            (o.Kind==RoomObjectKind.Cushion?Math.Abs(o.X-x)<InteractionFixtureGeometry.CushionWidth*.5+radius&&
                Math.Abs(o.Z-z)<InteractionFixtureGeometry.CushionDepth*.5+radius:
                o.Kind!=RoomObjectKind.RiverWater&&Distance(o.X,o.Z,x,z)<radius+StationObjectRadius(o.Kind)));
    }
    private static double StationObjectRadius(RoomObjectKind kind)=>kind switch
    {RoomObjectKind.Meal=>.32,RoomObjectKind.Water=>.18,RoomObjectKind.Ball=>.32,RoomObjectKind.Sweet=>.25,_=>.2};
    private bool CanPlaceStation(string region,double x,double z,SupplyStationState? moving=null)
    {
        const double radius=SupplyStationRadius;
        if(!double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)>8-radius||Math.Abs(z)>8-radius||
            !WoodlandLayout.GroundFits(State,region,x,z,radius)||!ClearOfStations(region,x,z,radius,moving?.Id))return false;
        if(State.DetachedStemBroods.Any(s=>s.RegionId==region&&!s.Cleared&&s.Phase!=DetachedStemPhase.Born&&Distance(x,z,s.X,s.Z)<radius+StemVesselRadius))return false;
        if(State.Creatures.Any(c=>c.RegionId==region&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&
            !(c.Interaction.Height>1&&c.Interaction.Restraint==RestraintKind.None)&&Distance(c.X,c.Z,x,z)<InteractionFootprint(c)+radius))return false;
        if(State.RoomObjects.Any(o=>o.Id!=moving?.OutletObjectId&&!o.Stored&&o.CarrierId==null&&o.RegionId==region&&
            (o.Kind==RoomObjectKind.Cushion?Math.Abs(o.X-x)<InteractionFixtureGeometry.CushionWidth*.5+radius&&
                Math.Abs(o.Z-z)<InteractionFixtureGeometry.CushionDepth*.5+radius:o.Kind!=RoomObjectKind.RiverWater&&Distance(o.X,o.Z,x,z)<radius+StationObjectRadius(o.Kind))))return false;
        return CanStationOutletFit(region,x,z+SupplyStationOutletOffset,moving?.Id,moving?.OutletObjectId);
    }
    private SupplyStationState? StationForSource(RoomObject source)=>State.SupplyStations.Find(s=>s.OutletObjectId==source.Id&&StationOutlet(s)==source);
    private bool StationSourceFront(Creature c,RoomObject source,double x,double z)=>StationForSource(source)==null||
        z>=source.Z-.001&&ClearOfStations(c.RegionId,x,z,InteractionFootprint(c));
    private static bool SegmentMeetsCircle(double ax,double az,double bx,double bz,double x,double z,double radius)
    {
        double dx=bx-ax,dz=bz-az,length=dx*dx+dz*dz;
        double t=length<.000001?0:Math.Clamp(((x-ax)*dx+(z-az)*dz)/length,0,1);
        double ex=ax+t*dx-x,ez=az+t*dz-z;return ex*ex+ez*ez<radius*radius;
    }
    // Both independent intake and family collection use the same front-side
    // standing candidates, then a visible ground waypoint around the rear bin.
    private bool TryStationApproach(Creature c,RoomObject source,out (double X,double Z) point)
    {
        point=default;var station=StationForSource(source);
        if(station==null||!TryResourceStandingPoint(c,source,out var standing))return false;
        point=standing;double radius=SupplyStationRadius+InteractionFootprint(c)+.12;
        if(!SegmentMeetsCircle(c.X,c.Z,standing.X,standing.Z,station.X,station.Z,radius-.08))return true;
        double sign=c.X>=station.X?1:-1;
        foreach(double side in new[]{sign,-sign})
        {
            // First pass the side of the warehouse; do not cut a chord through it.
            double x=station.X+side*(radius+.15);
            double z=c.Z<station.Z&&Math.Abs(c.X-station.X)<radius?station.Z-radius-.15:station.Z+radius+.15;
            if(CanOccupyRoom(c,x,z)&&!SegmentMeetsCircle(c.X,c.Z,x,z,station.X,station.Z,radius-.08))
            {point=(x,z);return true;}
        }
        point=(c.X,c.Z);return true;
    }
}

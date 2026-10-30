namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public bool CanPlaceInteractionAt(string id,double x,double z)
    {
        var c=InteractionTarget(id);
        return c!=null&&double.IsFinite(x)&&double.IsFinite(z)&&CanOccupyRoom(c,x,z,allowWater:true);
    }
    public static double InteractionFootprint(Creature c,RestraintKind? restraint=null,bool? supine=null)
    {
        double scale=c.IsYoung?.73:c.IsElder?1.03:1;
        var fixture=restraint??c.Interaction.Restraint;
        bool turned=supine??(restraint.HasValue?fixture==RestraintKind.Frame:c.Interaction.Supine||c.Interaction.Rolling);
        double body=turned?InteractionFixtureGeometry.SupineFootprint:InteractionFixtureGeometry.StandingFootprint;
        double equipment=fixture switch
        {RestraintKind.Box=>InteractionFixtureGeometry.BoxFootprint,RestraintKind.Frame=>InteractionFixtureGeometry.FrameFootprint,RestraintKind.Hanging=>InteractionFixtureGeometry.HangingFootprint,_=>0};
        return scale*Math.Max(body,equipment);
    }
    private bool CanOccupyRoom(Creature c,double x,double z,RestraintKind? restraint=null,bool? supine=null,bool allowWater=false)
    {
        double own=InteractionFootprint(c,restraint,supine);
        if(Math.Abs(x)>8-own||Math.Abs(z)>8-own)return false;
        if(!WoodlandLayout.GroundFits(State,c.RegionId,x,z,own,allowWater))return false;
        if(State.DetachedStemBroods.Any(s=>s.RegionId==c.RegionId&&!s.Cleared&&s.Phase!=DetachedStemPhase.Born&&
            Distance(x,z,s.X,s.Z)<own+StemVesselRadius))return false;
        if((restraint??c.Interaction.Restraint) is RestraintKind.Box or RestraintKind.Frame or RestraintKind.Hanging)
        {
            foreach(var cushion in State.RoomObjects)
            {
                if(cushion.Stored||cushion.Kind!=RoomObjectKind.Cushion||cushion.RegionId!=c.RegionId)continue;
                double dx=Math.Max(0,Math.Abs(cushion.X-x)-InteractionFixtureGeometry.CushionWidth*.5),dz=Math.Max(0,Math.Abs(cushion.Z-z)-InteractionFixtureGeometry.CushionDepth*.5);
                if(dx*dx+dz*dz<(own+.02)*(own+.02))return false;
            }
        }
        foreach(var other in State.Creatures)
        {
            if(other.Id==c.Id||other.Family.ColdStorage.Stored||other.Family.Memorialized||other.RegionId!=c.RegionId||other.Interaction.Height>1&&other.Interaction.Restraint==RestraintKind.None)continue;
            double gap=own+InteractionFootprint(other);
            if(Distance(x,z,other.X,other.Z)<gap-.00001)return false;
        }
        return true;
    }
    private bool GroundMove(Creature c,double x,double z,bool slide,bool allowWater=false)
    {
        double dx=x-c.X,dz=z-c.Z,length=Math.Sqrt(dx*dx+dz*dz);
        if(length<.00001)return true;
        int slices=Math.Max(1,(int)Math.Ceiling(length/.06));
        dx/=slices;dz/=slices;
        bool complete=true;
        for(int n=0;n<slices;n++)
        {
            if(CanOccupyRoom(c,c.X+dx,c.Z+dz,allowWater:allowWater)){c.X+=dx;c.Z+=dz;continue;}
            complete=false;
            if(!slide)break;
            // Try a small turn around the obstacle. Never displace a restrained or injured neighbor.
            bool moved=false;
            foreach(double angle in new[]{Math.PI/4,-Math.PI/4,Math.PI/2,-Math.PI/2})
            {
                double sx=dx*Math.Cos(angle)-dz*Math.Sin(angle),sz=dx*Math.Sin(angle)+dz*Math.Cos(angle);
                if(!CanOccupyRoom(c,c.X+sx,c.Z+sz,allowWater:allowWater))continue;
                c.X+=sx;c.Z+=sz;moved=true;break;
            }
            if(!moved)break;
        }
        return complete;
    }
    private void ClearLandingOverlap(Creature c)
    {
        if(CanOccupyRoom(c,c.X,c.Z,allowWater:true))return;
        double originX=c.X,originZ=c.Z;
        for(double radius=.08;radius<=3;radius+=.08)
            for(int angle=0;angle<32;angle++)
            {
                double x=originX+Math.Cos(angle*Math.Tau/32)*radius,z=originZ+Math.Sin(angle*Math.Tau/32)*radius;
                if(!CanOccupyRoom(c,x,z,allowWater:true))continue;
                c.X=x;c.Z=z;c.TargetX=x;c.TargetZ=z;return;
            }
    }
}

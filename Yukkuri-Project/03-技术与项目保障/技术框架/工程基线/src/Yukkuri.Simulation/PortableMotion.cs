using System.Numerics;
using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public sealed class PortableMotion
{
    public string Id { get; set; } = "";
    public double Height { get; set; }
    public double VelocityX { get; set; }
    public double VelocityZ { get; set; }
    public double VerticalVelocity { get; set; }
    [JsonIgnore] public bool Held { get; set; }
}
public sealed partial class WorldState
{
    public List<PortableMotion> PortableMotions { get; set; } = [];
}
public sealed partial class WorldSimulation
{
    public PortableMotion? PortableStatus(string id)=>State.PortableMotions.Find(m=>m.Id==id);
    public Vector2? PortablePosition(string id)
    {
        if(State.RoomObjects.Find(candidate=>candidate.Id==id) is { } o)return new((float)o.X,(float)o.Z);
        if(Station(id) is { } s)return new((float)s.X,(float)s.Z);
        if(DetachedStem(id) is { } stem)return new((float)stem.X,(float)stem.Z);
        if(State.Creatures.Find(candidate=>candidate.Id==id) is { } c)return new((float)c.X,(float)c.Z);
        return null;
    }
    public bool BeginPortableCarry(string id)
    {
        if(State.Settings.Pause||StationHandOccupied()||State.PortableMotions.Count>0)return false;
        bool valid=false;
        if(State.RoomObjects.Find(candidate=>candidate.Id==id&&candidate.RegionId==State.ActiveRegionId&&!candidate.Stored) is { } o)
            valid=o.Kind is RoomObjectKind.Ball or RoomObjectKind.Treasure or RoomObjectKind.Cushion;
        else if(Station(id) is { } s)valid=StationError(s)==null&&StationActionError(s,InteractionAction.Lift)==null;
        else if(DetachedStem(id) is { } stem)valid=stem.RegionId==State.ActiveRegionId&&!stem.Cleared&&stem.Phase==DetachedStemPhase.Bottled;
        else if(InteractionTarget(id) is { } c)
            valid=!c.Family.Memorialized&&!c.Family.ColdStorage.Stored&&(c.Condition==PhysicalCondition.Dead||c.Interaction.Restraint!=RestraintKind.None);
        if(!valid)return false;
        State.PortableMotions.Add(new(){Id=id,Height=ShelterPortableHeight(id,.65),Held=true});SetPortableOccupancy(id,true);return true;
    }
    private void SetPortableOccupancy(string id,bool held)
    {
        if(State.RoomObjects.Find(candidate=>candidate.Id==id) is { } o){o.HeldByHand=held;o.VelocityX=o.VelocityZ=0;}
        if(Station(id) is { } s)s.HeldByHand=held;
        if(DetachedStem(id) is { } stem)stem.HeldByHand=held;
    }
    public bool MovePortableCarry(string id,double x,double z,double height)
    {
        var m=PortableStatus(id);
        if(m==null||!m.Held||State.Settings.Pause||!double.IsFinite(height))return false;
        m.Height=ShelterPortableHeight(id,Math.Clamp(height,.15,3));
        bool moved=MovePortable(id,x,z);m.Height=ShelterPortableHeight(id,m.Height);return moved;
    }
    private bool MovePortable(string id,double x,double z)
    {
        if(!double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)>7.5||Math.Abs(z)>7.5)return false;
        if(Station(id) is { } station)
        {
            var origin=PortablePosition(id)!.Value;int steps=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(origin,new((float)x,(float)z))/.06));
            for(int n=1;n<=steps;n++)
            {
                double nx=origin.X+(x-origin.X)*n/steps,nz=origin.Y+(z-origin.Y)*n/steps;
                if(!CanPlaceStation(station.RegionId,nx,nz,station))break;
                var outlet=StationOutlet(station);station.X=nx;station.Z=nz;
                if(outlet!=null){outlet.X=nx;outlet.Z=nz+SupplyStationOutletOffset;}
            }
            return true;
        }
        if(DetachedStem(id) is { } stem)
        {
            double ox=stem.X,oz=stem.Z;int steps=Math.Max(1,(int)Math.Ceiling(Distance(ox,oz,x,z)/.06));
            for(int n=1;n<=steps;n++)
            {double nx=ox+(x-ox)*n/steps,nz=oz+(z-oz)*n/steps;if(!CanPlaceDetachedStem(stem.RegionId,nx,nz,id))break;stem.X=nx;stem.Z=nz;}
            return true;
        }
        if(State.RoomObjects.Any(o=>o.Id==id))return MoveLooseObject(id,x,z);
        return MoveSupportedBodyOrFixture(id,x,z);
    }
    public void ReleasePortableCarry(string id,Vector2 velocity,bool cancel=false)
    {
        if(PortableStatus(id) is not { } m)return;
        m.Held=false;
        if(cancel){SetPortableOccupancy(id,false);State.PortableMotions.Remove(m);return;}
        if(!float.IsFinite(velocity.X)||!float.IsFinite(velocity.Y))velocity=Vector2.Zero;
        if(velocity.Length()<CarryReleaseMotion.ThrowSpeed)velocity=Vector2.Zero;
        if(velocity.Length()>10)velocity=Vector2.Normalize(velocity)*10;
        m.VelocityX=velocity.X;m.VelocityZ=velocity.Y;m.VerticalVelocity=velocity.Length()>0?1.2:0;
    }
    public void LaunchReleasedSupply(string id,Vector2 velocity)
    {
        var obj=State.RoomObjects.Find(o=>o.Id==id&&!o.Stored&&!o.HeldByHand&&o.Portions>0&&o.RegionId==State.ActiveRegionId);
        if(obj==null||State.Settings.Pause||State.PortableMotions.Count>0)return;
        State.PortableMotions.Add(new(){Id=id,Height=.6,Held=true});SetPortableOccupancy(id,true);ReleasePortableCarry(id,velocity);
    }
    private void TickPortableMotion(double dt)
    {
        foreach(var m in State.PortableMotions.ToArray())
        {
            if(DetachedStem(m.Id) is {Phase:not DetachedStemPhase.Bottled})
            {SetPortableOccupancy(m.Id,false);State.PortableMotions.Remove(m);continue;}
            if(PortablePosition(m.Id) is not { } position){State.PortableMotions.Remove(m);continue;}
            SetPortableOccupancy(m.Id,true);
            if(m.Held)continue;
            MovePortable(m.Id,position.X+m.VelocityX*dt,position.Y+m.VelocityZ*dt);
            m.VerticalVelocity-=9.8*dt;m.Height=Math.Clamp(m.Height+m.VerticalVelocity*dt,0,3);
            double roofHeight=ShelterPortableHeight(m.Id,m.Height);
            if(m.Height>roofHeight){m.Height=roofHeight;m.VerticalVelocity=Math.Min(0,m.VerticalVelocity);}
            if(m.Height>0)continue;
            SetPortableOccupancy(m.Id,false);State.PortableMotions.Remove(m);
        }
    }
}

namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    // Ground supplies belong to the current yard, not to the selected body.
    // In particular, a dead parent cannot prevent caring for its living bottle.
    public InteractionFeedback PlaceInteractionSupply(InteractionAction action,double x,double z)
    {
        if(!State.InteractionPlaytest||string.IsNullOrWhiteSpace(State.ActiveRegionId)||
            !State.Regions.Any(region=>region.Id==State.ActiveRegionId))
            return Unavailable("先回到有效的小院区域，再放置食水。");
        if(State.Settings.Pause)return Unavailable("先恢复时间，再放置食水。");
        if(State.Mode==PlayMode.CloseUp)return Unavailable("先退出特写，再向小院地面放置食水。");
        if(action is not (InteractionAction.PlaceMeal or InteractionAction.PlaceSweet or InteractionAction.PlaceWater)||
            !double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)>8||Math.Abs(z)>8)
            return Unavailable("这个食水种类或放置位置无效。");
        return PlaceFood(State.ActiveRegionId,new(action,X:x,Z:z));
    }

    private void SettleRoomSupplies()
    {
        foreach(var obj in State.RoomObjects)
            if(!obj.Stored&&!obj.HeldByHand&&obj.CarrierId==null&&obj.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water)
                obj.Height=RoomSupplySupport.Height(State,obj.RegionId,obj.X,obj.Z,obj.Kind);
    }
    public bool HoldSupply(string id)
    {
        if(!State.InteractionPlaytest||State.Settings.Pause||State.RoomObjects.Any(o=>o.HeldByHand)||
            State.DetachedStemBroods.Any(s=>s.HeldByHand)||State.SupplyStations.Any(s=>s.HeldByHand))return false;
        var obj=State.RoomObjects.Find(o=>o.Id==id&&!o.Stored&&o.RegionId==State.ActiveRegionId);
        if(obj==null||obj.Kind is not (RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water))return false;
        if(obj.Portions<=0&&NaturalResources.Consumer(State,obj)==null)return false;
        // The same portion changes hands. Mark it held before canceling so the
        // drop helper cannot move it back under the former carrier's body.
        obj.HeldByHand=true;
        if(obj.CarrierId is { } carrierId&&State.Creatures.Find(c=>c.Id==carrierId&&c.Family.CareParcelId==obj.Id) is { } carrier)
            CancelFamilyCare(carrier,reason:"搬运中的这一份食水被手接走，递送已中断；份额仍在手里。");
        obj.CarrierId=null;
        foreach(var c in State.Creatures)
            if(c.Interaction.GroundIntakeSourceId==obj.Id)DetachNaturalIntake(c);
        foreach(var station in State.SupplyStations)
            if(station.OutletObjectId==obj.Id)station.OutletObjectId=null;
        return true;
    }
    public bool ReleaseSupply(string id,double? x=null,double? z=null)
    {
        var obj=State.RoomObjects.Find(o=>o.Id==id&&o.HeldByHand);
        if(obj==null)return false;
        if(x.HasValue&&z.HasValue)
        {
            if(!double.IsFinite(x.Value)||!double.IsFinite(z.Value)||Math.Abs(x.Value)>7.7||Math.Abs(z.Value)>7.7)return false;
            if(!WoodlandLayout.GroundFits(State,obj.RegionId,x.Value,z.Value,.2))return false;
            if(!ClearOfStations(obj.RegionId,x.Value,z.Value,.2))return false;
            if(!ShelterGroundFits(obj.RegionId,x.Value,z.Value,StationObjectRadius(obj.Kind)))return false;
            if(State.DetachedStemBroods.Any(s=>s.RegionId==obj.RegionId&&!s.Cleared&&s.Phase!=DetachedStemPhase.Born&&
                Distance(s.X,s.Z,x.Value,z.Value)<StemVesselRadius+.2))return false;
            if(State.Creatures.Any(c=>!c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&c.RegionId==obj.RegionId&&Distance(c.X,c.Z,x.Value,z.Value)<InteractionFootprint(c)+.2))return false;
            obj.X=x.Value;obj.Z=z.Value;obj.Height=RoomSupplySupport.Height(State,obj.RegionId,obj.X,obj.Z,obj.Kind);
        }
        // Cancel retains its original ground location. Holding itself never consumes a portion.
        obj.HeldByHand=false;
        obj.Height=RoomSupplySupport.Height(State,obj.RegionId,obj.X,obj.Z,obj.Kind);
        if(obj.Portions<=0)State.RoomObjects.Remove(obj);
        return true;
    }
    private RoomObject? OfferedSupply(InteractionInput input,Creature c)=>input.ObjectId==null?null:
        State.RoomObjects.Find(o=>o.Id==input.ObjectId&&o.HeldByHand&&!o.Stored&&o.Portions>0&&o.RegionId==c.RegionId&&
            (input.Action==InteractionAction.Drink?o.Kind==RoomObjectKind.Water:
             input.Action==InteractionAction.FeedSweet?o.Kind==RoomObjectKind.Sweet:o.Kind==RoomObjectKind.Meal));
}

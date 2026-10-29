namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private void SettleRoomSupplies()
    {
        foreach(var obj in State.RoomObjects)
            if(!obj.Stored&&!obj.HeldByHand&&obj.CarrierId==null&&obj.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water)
                obj.Height=RoomSupplySupport.Height(State,obj.RegionId,obj.X,obj.Z,obj.Kind);
    }
    public bool HoldSupply(string id)
    {
        if(!State.InteractionPlaytest||State.Settings.Pause||State.RoomObjects.Any(o=>o.HeldByHand))return false;
        var obj=State.RoomObjects.Find(o=>o.Id==id&&!o.Stored&&o.RegionId==State.ActiveRegionId);
        if(obj==null||obj.Kind is not (RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water))return false;
        if(obj.Portions<=0&&NaturalResources.Consumer(State,obj)==null)return false;
        if(obj.CarrierId is { } carrierId&&State.Creatures.Find(c=>c.Id==carrierId) is { } carrier)
        {
            carrier.Family.CareParcelId=null;carrier.Family.CareTargetId=null;
            carrier.Family.NextCareAt=State.WorldSeconds+12;
        }
        obj.CarrierId=null;
        obj.HeldByHand=true;
        foreach(var c in State.Creatures)
            if(c.Interaction.GroundIntakeSourceId==obj.Id)DetachNaturalIntake(c);
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
            if(State.Creatures.Any(c=>!c.Family.ColdStorage.Stored&&c.RegionId==obj.RegionId&&Distance(c.X,c.Z,x.Value,z.Value)<InteractionFootprint(c)+.2))return false;
            obj.X=x.Value;obj.Z=z.Value;obj.Height=RoomSupplySupport.Height(State,obj.RegionId,obj.X,obj.Z,obj.Kind);
        }
        // Cancel retains its original ground location. Holding itself never consumes a portion.
        obj.HeldByHand=false;
        if(obj.Portions<=0)State.RoomObjects.Remove(obj);
        return true;
    }
    private RoomObject? OfferedSupply(InteractionInput input,Creature c)=>input.ObjectId==null?null:
        State.RoomObjects.Find(o=>o.Id==input.ObjectId&&o.HeldByHand&&!o.Stored&&o.Portions>0&&o.RegionId==c.RegionId&&
            (input.Action==InteractionAction.Drink?o.Kind==RoomObjectKind.Water:
             input.Action==InteractionAction.FeedSweet?o.Kind==RoomObjectKind.Sweet:o.Kind==RoomObjectKind.Meal));
}

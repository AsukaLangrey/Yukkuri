namespace Yukkuri.Simulation;

public static class SupplyStationValidation
{
    public static void Validate(WorldState state)
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
        Require(state.SupplyStations!=null&&state.SupplyStationSequence>=0&&state.FoodStationStock is >=0 and <=99&&
            state.WaterStationStock is >=0 and <=99&&state.StationWaterPortions>=0,"供给器库存或序号无效");
        Require(state.DetachedStemBroods!=null&&state.DetachedStemBroods.All(s=>s!=null),"独立窝列表缺失或含空记录");
        var ids=state.RoomObjects.Select(o=>o.Id).Concat(state.Creatures.Select(c=>c.Id))
            .Concat(state.DetachedStemBroods.Select(s=>s.EventId)).ToHashSet(StringComparer.Ordinal);
        var outputs=new HashSet<string>(StringComparer.Ordinal);
        foreach(var s in state.SupplyStations!)
        {
            Require(s!=null&&s.RefillIntervalSeconds is 0 or 10 or 30 or 60 or 120&&double.IsFinite(s.NextRefillAt)&&s.NextRefillAt>=0,"供给器补充频率无效");
            Require(s!=null&&!string.IsNullOrWhiteSpace(s.Id)&&ids.Add(s.Id)&&Enum.IsDefined(s.Kind)&&
                state.Regions.Any(r=>r.Id==s.RegionId)&&double.IsFinite(s.X)&&double.IsFinite(s.Z)&&
                Math.Abs(s.X)<=8-WorldSimulation.SupplyStationRadius&&Math.Abs(s.Z)<=8-WorldSimulation.SupplyStationRadius&&
                Math.Abs(s.Z+WorldSimulation.SupplyStationOutletOffset)<=7.68&&s.OutputSequence>=0&&s.LastEvent!=null,
                "供给器身份或位置无效");
            int capacity=s!.Kind==SupplyStationKind.Food?WorldSimulation.FoodStationCapacity:WorldSimulation.WaterStationCapacity;
            int pending=0,portions=0;
            if(s.OutletObjectId!=null)
            {
                Require(outputs.Add(s.OutletObjectId)&&s.OutletObjectId.StartsWith(s.Id+"-out-",StringComparison.Ordinal)&&
                    long.TryParse(s.OutletObjectId.AsSpan(s.Id.Length+5),System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,out long sequence)&&sequence>0&&sequence<=s.OutputSequence,
                    "供给器出料身份无效");
                var o=state.RoomObjects.Find(o=>o.Id==s.OutletObjectId);
                // A consumed/collected object may disappear before the next world tick.
                // An existing owned outlet must still be the original physical portion.
                if(o!=null)
                {
                    Require(o.Kind==(s.Kind==SupplyStationKind.Food?RoomObjectKind.Meal:RoomObjectKind.Water)&&
                        o.RegionId==s.RegionId&&o.OwnerId==null&&o.CarrierId==null&&!o.Stored&&!o.HeldByHand&&
                        Math.Abs(o.X-s.X)<.001&&Math.Abs(o.Z-s.Z-WorldSimulation.SupplyStationOutletOffset)<.001,
                        "供给器出口实际份额归属无效");
                    portions=o.Portions;pending=state.Creatures.Count(c=>c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeSourceId==o.Id);
                    Require(portions+pending<=1,"供给器同一出口复制了份额");
                }
            }
            Require(s.StoredPortions>=0&&(long)s.StoredPortions+portions+pending<=capacity,"供给器容量无效");
        }
        Require(state.SupplyStations.Count(s=>s.HeldByHand)<=1&&(!state.SupplyStations.Any(s=>s.HeldByHand)||
            !state.Creatures.Any(c=>c.Interaction.Carried)&&!state.RoomObjects.Any(o=>o.HeldByHand)&&
            !state.DetachedStemBroods.Any(s=>s.HeldByHand)),"同一只手持有多个实际对象");
    }
}

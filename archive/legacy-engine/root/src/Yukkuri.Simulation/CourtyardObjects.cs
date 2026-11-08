namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public bool MoveLooseObject(string id,double x,double z)
    {
        var o=State.RoomObjects.Find(o=>o.Id==id&&!o.Stored&&o.RegionId==State.ActiveRegionId);
        if(o==null||State.Settings.Pause||!double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)>7.7||Math.Abs(z)>7.7||
            !WoodlandLayout.GroundFits(State,o.RegionId,x,z,.2)||!ClearOfStations(o.RegionId,x,z,.2)||
            State.Creatures.Any(c=>!c.Family.Memorialized&&!c.Family.ColdStorage.Stored&&c.RegionId==o.RegionId&&Distance(c.X,c.Z,x,z)<InteractionFootprint(c)+.2))return false;
        double distance=Distance(o.X,o.Z,x,z);int steps=Math.Max(1,(int)Math.Ceiling(distance/.08));
        double shelterRadius=o.Kind==RoomObjectKind.Cushion?1.3:StationObjectRadius(o.Kind);
        if(!ShelterObjectPathClear(o.RegionId,o.X,o.Z,x,z,shelterRadius))return false;
        double ox=o.X,oz=o.Z;
        for(int n=1;n<=steps;n++)
        {
            double nx=ox+(x-ox)*n/steps,nz=oz+(z-oz)*n/steps;
            if(!WoodlandLayout.GroundFits(State,o.RegionId,nx,nz,.2)||!ClearOfStations(o.RegionId,nx,nz,.2)||
                State.Creatures.Any(c=>c.RegionId==o.RegionId&&!c.Family.Memorialized&&!c.Family.ColdStorage.Stored&&Distance(c.X,c.Z,nx,nz)<InteractionFootprint(c)+.2)||
                State.DetachedStemBroods.Any(s=>s.RegionId==o.RegionId&&!s.Cleared&&Distance(s.X,s.Z,nx,nz)<StemVesselRadius+.2)||
                State.RoomObjects.Any(other=>other!=o&&!other.Stored&&other.RegionId==o.RegionId&&other.Kind is RoomObjectKind.Ball or RoomObjectKind.Cushion&&Distance(other.X,other.Z,nx,nz)<.5))break;
            o.X=nx;o.Z=nz;
        }
        o.VelocityX=o.VelocityZ=0;return true;
    }
    public bool MoveSupportedBodyOrFixture(string id,double x,double z)
    {
        var c=InteractionTarget(id);
        if(c==null||State.Settings.Pause||!double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)>7.5||Math.Abs(z)>7.5||
            c.Family.Memorialized||c.Family.ColdStorage.Stored)return false;
        if(c.Condition!=PhysicalCondition.Dead&&c.Interaction.Restraint==RestraintKind.None)return SlideAlongGround(id,x,z);
        double ox=c.X,oz=c.Z;int steps=Math.Max(1,(int)Math.Ceiling(Distance(ox,oz,x,z)/.08));
        for(int n=1;n<=steps;n++)
        {
            double nx=ox+(x-ox)*n/steps,nz=oz+(z-oz)*n/steps;
            if(!ShelterMovePathClear(c,nx,nz)||!CanOccupyRoom(c,nx,nz,c.Interaction.Restraint))break;
            c.X=nx;c.Z=nz;
        }
        c.TargetX=c.X;c.TargetZ=c.Z;return true;
    }
    public string RecycleCourtyardObject(string id)
    {
        if(State.Settings.Pause)return "暂停时不能收容。";
        if(PortableStatus(id) is { } motion&&motion.Held)return "先结束当前搬运，再收容对象。";
        if(DetachedStem(id) is { } brood&&!brood.Cleared&&brood.RegionId==State.ActiveRegionId)
        {
            ReleasePortableCarry(id,default,cancel:true);
            EnsureContainerRegion();brood.RegionId="courtyard-container";brood.HeldByHand=false;
            brood.LastEvent="整瓶移入收容箱，仍保留这一窝的实际生命与食水状态。";
            return "整瓶已收容，生命记录保留；没有折算成空瓶或退款，可从收容箱取回。";
        }
        if(Station(id) is { } station&&station.RegionId==State.ActiveRegionId)
        {
            if(StationOutletBusy(station))return "先等这一口完成，再收容器具。";
            int remaining=station.StoredPortions+StationOutletPortions(station);
            string contents=station.Kind==SupplyStationKind.Food?"food":"water",item=station.Kind==SupplyStationKind.Food?"food-station":"water-station";
            if(ShopQuantity(contents)>int.MaxValue-remaining||ShopQuantity(item)>=99)return "仓库放不下，器具和食水仍保留。";
            ReleasePortableCarry(id,default,cancel:true);
            if(StationOutlet(station) is { } outlet)State.RoomObjects.Remove(outlet);
            State.SupplyStations.Remove(station);ChangeShopQuantity(contents,remaining);ChangeShopQuantity(item,1);
            return "器具与剩余食水已回仓，未退钱。";
        }
        var obj=State.RoomObjects.Find(o=>o.Id==id&&!o.Stored&&o.RegionId==State.ActiveRegionId);
        if(obj!=null)
        {
            if(NaturalResources.Consumer(State,obj)!=null)return "先等它吃完这一口。";
            string? item=obj.Kind switch{RoomObjectKind.Meal or RoomObjectKind.Sweet=>"food",RoomObjectKind.Water=>"water",RoomObjectKind.Ball=>"ball",RoomObjectKind.Treasure=>"treasure",_=>null};
            int count=obj.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water?obj.Portions:1;
            if(item!=null&&ShopQuantity(item)>int.MaxValue-count)return "仓库已满，物品仍保留。";
            if(obj.CarrierId is { } carrierId&&State.Creatures.Find(c=>c.Id==carrierId) is { } carrier)
                CancelFamilyCare(carrier,reason:"食水移入仓库，原来的递送已停止。");
            foreach(var s in State.SupplyStations)if(s.OutletObjectId==id)s.OutletObjectId=null;
            // A treasure is personally remembered. Returning its warehouse token
            // must not erase its identity and turn the same item into a new gift.
            if(obj.Kind==RoomObjectKind.Treasure)obj.Stored=true;
            else State.RoomObjects.Remove(obj);
            if(item!=null)ChangeShopQuantity(item,count);
            if(PortableStatus(id) is { } removedMotion)State.PortableMotions.Remove(removedMotion);
            return item==null?"已收拾废弃物。":"物品已回仓，未退钱。";
        }
        var c=InteractionTarget(id);if(c==null||c.Family.Memorialized||c.Family.ColdStorage.Stored)return "这个对象已经不在小院。";
        ReleasePortableCarry(id,default,cancel:true);
        var fixture=InstalledItem(c.Interaction.Restraint);
        if(fixture!=null)ChangeShopQuantity(fixture,1);
        StopIncapacitatedActions(c);ClearCompanionship(c);ClearFamilyTargetsTo(c);DetachNaturalIntake(c);
        c.Interaction.Restraint=RestraintKind.None;c.Interaction.Carried=false;c.Interaction.Falling=false;c.Interaction.Lowering=false;
        c.Interaction.PersistentPressure=false;
        c.Interaction.Height=0;c.Interaction.VerticalVelocity=c.Interaction.VelocityX=c.Interaction.VelocityZ=0;
        EnsureContainerRegion();
        c.RegionId="courtyard-container";c.X=c.Z=c.TargetX=c.TargetZ=0;
        if(State.SelectedId==id){State.SelectedId=null;State.Mode=PlayMode.Management;}
        return c.Condition==PhysicalCondition.Dead?"遗体已收容，保留生命记录；没有墓碑或家族安定加成。":"已移入收容箱，身份与关系保留；没有退款，可从收容箱取回。";
    }
    private void EnsureContainerRegion()
    {
        if(!State.Regions.Any(r=>r.Id=="courtyard-container"))State.Regions.Add(new(){Id="courtyard-container",Name="收容箱",Kind=RegionKind.Sanctuary});
    }
    public string ReturnContainedBrood(string id)
    {
        var s=DetachedStem(id);if(s==null||s.RegionId!="courtyard-container"||State.Settings.Pause)return "当前不能取回。";
        for(double x=-6;x<=6;x+=1.5)for(double z=-6;z<=6;z+=1.5)
            if(CanPlaceDetachedStem(State.ActiveRegionId,x,z,id)){s.RegionId=State.ActiveRegionId;s.X=x;s.Z=z;return "已把同一窝取回小院空地。";}
        return "小院没有空位，仍在收容箱。";
    }
    public string ReturnContainedCreature(string id)
    {
        var c=State.Creatures.Find(c=>c.Id==id&&c.RegionId=="courtyard-container");
        if(c==null||State.Settings.Pause)return "当前不能取回。";
        string old=c.RegionId;c.RegionId=State.ActiveRegionId;
        for(double x=-6;x<=6;x+=1.5)for(double z=-6;z<=6;z+=1.5)
            if(CanOccupyRoom(c,x,z)){c.X=c.TargetX=x;c.Z=c.TargetZ=z;return "已取回小院空地。";}
        c.RegionId=old;return "小院没有空位，仍在收容箱。";
    }
    public string CleanSceneObject(string id)
    {
        if(State.Settings.Pause)return "先恢复时间。";
        var o=State.RoomObjects.Find(o=>o.Id==id&&!o.Stored&&o.RegionId==State.ActiveRegionId);
        if(o==null)return "这里没有可收拾的东西。";
        if(IsWaste(o.Kind)){CleanRoomWaste(id);return "已清理干净。";}
        if(o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet or RoomObjectKind.Water&&o.Portions==0&&NaturalResources.Consumer(State,o)==null)
        {State.RoomObjects.Remove(o);return "已收拾空容器。";}
        return "这件物品仍可使用；用抓取移入收容箱回仓。";
    }
}

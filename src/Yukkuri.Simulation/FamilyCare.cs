namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private bool CaregiverReady(Creature adult) => !adult.IsYoung && FamilyRules.WillSocialize(adult) && CanSelfMove(adult) &&
        adult.Family.Conflict.TargetId==null && adult.Family.FlightRemaining<=0 && adult.RetreatRemaining<=0 &&
        adult.Family.CompanionTargetId==null && !adult.Interaction.Resting && adult.Interaction.RestRemaining<=0 &&
        !adult.Interaction.FollowPlayer && adult.Interaction.WaitRemaining<=0 && adult.Interaction.Height<=.25 &&
        InteractionGestureTarget!=adult.Id && !NaturalResources.WantsFood(adult) && !NaturalResources.WantsWater(adult);

    private static bool IsCareParent(Creature adult,Creature child) => child.ParentId==adult.Id ||
        child.Family.MotherId==adult.Id || child.Family.FatherId==adult.Id;

    private bool CareTargetAvailable(Creature adult,Creature child) => child!=adult && child.IsYoung &&
        child.Condition!=PhysicalCondition.Dead && child.RegionId==adult.RegionId && !child.Family.ColdStorage.Stored &&
        !child.Interaction.Carried && !child.Interaction.Falling && child.Interaction.Height<=.25 &&
        child.Interaction.Restraint==RestraintKind.None && InteractionGestureTarget!=child.Id &&
        child.Family.FlightRemaining<=0 && child.RetreatRemaining<=0 && child.Family.Conflict.TargetId==null &&
        (!adult.Family.Peers.TryGetValue(child.Id,out var a)||a.Fear<40&&a.Resentment<35) &&
        (!child.Family.Peers.TryGetValue(adult.Id,out var b)||b.Fear<40&&b.Resentment<35) &&
        FamilyVisible(adult,child,6) && HasGroundSightLine(adult,child.X,child.Z);

    private void CancelFamilyCare(Creature adult,double cooldown=12)
    {
        DropCareParcel(adult);adult.Family.CareTargetId=null;adult.Family.CareApproachSeconds=0;
        adult.Family.CareBestDistance=100;adult.Family.NextCareAt=Math.Max(adult.Family.NextCareAt,State.WorldSeconds+cooldown);
    }

    public string ArrangeCare(string caregiverId,string childId)
    {
        var adult=State.Creatures.Find(c=>c.Id==caregiverId);var child=State.Creatures.Find(c=>c.Id==childId);
        var error=FamilyCommandError(adult)??FamilyCommandError(child);
        if(error!=null)return error;
        if(adult!.Id==child!.Id||adult.IsYoung||!child.IsYoung)return "需要一只成熟油照看幼体。";
        if(!CaregiverReady(adult)||!CareTargetAvailable(adult,child))
            return "照护者现在没有余力，或幼体暂时无法接近。";
        bool parent=IsCareParent(adult,child);
        if(!parent&&!child.Family.CaregiverIds.Contains(adult.Id)&&
            (!adult.Family.Peers.TryGetValue(child.Id,out var relationship)||relationship.Affection<20||adult.Aggression>.65))
            return "它还没有愿意接纳这只幼体，先让双方有安全相处的机会。";
        if(adult.Family.CareTargetId==child.Id)return "已经在照看这只幼体，给实际靠近和递送留一点时间。";
        DropCareParcel(adult);adult.Family.CareTargetId=child.Id;adult.Family.CareApproachSeconds=0;adult.Family.CareBestDistance=100;
        return State.LastActionMessage="已提出照护安排。它会在自己有余力时靠近、保暖，并寻找实际食水送过去。";
    }

    private void TickYoungLife(Creature c,double dt)
    {
        if(!c.IsYoung||c.Condition==PhysicalCondition.Dead)return;
        var f=c.Family;
        if(!f.ColdStorage.Stored)
        {
            bool supported=c.Interaction.Restraint==RestraintKind.Wrap||HasCushionedRestSupport(c);
            bool warmAdult=State.Creatures.Any(a=>CaregiverReady(a)&&CareTargetAvailable(a,c)&&
                f.CaregiverIds.Contains(a.Id)&&FamilyInContact(a,c));
            f.Warmth=Math.Clamp(f.Warmth+dt*(supported||warmAdult ? .18 : -.025),0,100);
            if(f.Warmth<15)Hurt(c,dt*.025,"幼体持续失温",null);
            if(c.Condition==PhysicalCondition.Dead)return;
            if(c.Condition==PhysicalCondition.Active&&f.Warmth<35&&c.ReactionRemaining<=0)
                React(c,Expression.Calling,"冷冷……陪陪……","幼体缩着身体，寻找温暖和陪伴。",5);
        }
        // Legacy young residents keep their age semantics; newly born individuals
        // have a recorded birth and grow only through ordinary biological time.
        if(f.BirthEventId!=null&&c.Body.AgeSeconds>=FamilyRules.YoungSeconds&&c.Fullness>35&&c.Health>50&&f.Warmth>40)
        {c.IsYoung=false;f.LastEvent="已经长大，开始学习独立生活。";RecordInteraction(c,"grown",f.LastEvent,3);}
    }

    private void DropCareParcel(Creature adult)
    {
        var f=adult.Family;
        if(f.CareParcelId is { } id&&State.RoomObjects.Find(o=>o.Id==id&&o.CarrierId==adult.Id) is { } parcel)
        {
            parcel.CarrierId=null;
            if(!parcel.HeldByHand&&!parcel.Stored)
            {parcel.RegionId=adult.RegionId;parcel.X=adult.X;parcel.Z=adult.Z;parcel.Height=RoomSupplySupport.Height(State,parcel.RegionId,parcel.X,parcel.Z,parcel.Kind);}
        }
        f.CareParcelId=null;
    }

    private void SynchronizeCareParcels()
    {
        foreach(var adult in State.Creatures)
        {
            var f=adult.Family;
            if(f.CareParcelId==null&&f.CareTargetId==null)continue;
            var child=State.Creatures.Find(c=>c.Id==f.CareTargetId);
            if(child==null||!CaregiverReady(adult)||!CareTargetAvailable(adult,child))
            {CancelFamilyCare(adult);continue;}
            if(f.CareParcelId==null)continue;
            var parcel=State.RoomObjects.Find(o=>o.Id==f.CareParcelId);
            if(parcel==null||parcel.CarrierId!=adult.Id||parcel.HeldByHand||parcel.Stored||parcel.Portions!=1)
            {CancelFamilyCare(adult);continue;}
            parcel.RegionId=adult.RegionId;parcel.X=adult.X+Math.Sin(adult.Interaction.Facing)*.65;
            parcel.Z=adult.Z+Math.Cos(adult.Interaction.Facing)*.65;parcel.Height=.45;
        }
    }

    private bool TryFamilyCare(Creature adult,double dt)
    {
        var f=adult.Family;
        if(!CaregiverReady(adult))
        {if(f.CareTargetId!=null||f.CareParcelId!=null)CancelFamilyCare(adult);return false;}
        var child=State.Creatures.Find(c=>c.Id==f.CareTargetId&&CareTargetAvailable(adult,c));
        if(child==null)
        {
            DropCareParcel(adult);f.CareTargetId=null;
            if(State.WorldSeconds<f.NextCareAt)return false;
            child=State.Creatures.Where(c=>CareTargetAvailable(adult,c)&&
                (c.Family.CaregiverIds.Contains(adult.Id)||IsCareParent(adult,c)||adult.Aggression<=.25&&adult.Patience>=.65&&
                    f.Peers.TryGetValue(c.Id,out var known)&&known.Affection>=20&&known.Fear<25&&known.Resentment<25)&&
                Distance(adult.X,adult.Z,c.X,c.Z)<5&&FamilyVisible(adult,c,5)&&
                (c.Fullness<55||c.Interaction.Hydration<55||c.Family.Warmth<60))
                .OrderBy(c=>Math.Min(c.Fullness,Math.Min(c.Interaction.Hydration,c.Family.Warmth)))
                .ThenBy(c=>c.Id,StringComparer.Ordinal).FirstOrDefault();
            if(child==null)return false;
            f.CareTargetId=child.Id;f.CareBestDistance=100;f.CareApproachSeconds=0;
        }
        if(f.CareParcelId is { } parcelId&&State.RoomObjects.Find(o=>o.Id==parcelId&&o.CarrierId==adult.Id) is { } parcel)
        {
            double distance=Distance(adult.X,adult.Z,child.X,child.Z);
            if(!FamilyInContact(adult,child))
            {RoomMove(adult,child.X,child.Z,dt);adult.Activity="把食水送到宝宝旁边";return CareProgress(adult,distance,dt);}
            double dx=adult.X-child.X,dz=adult.Z-child.Z,d=Math.Max(.01,distance);
            var offered=new RoomObject{Id="care-placement-probe",Kind=parcel.Kind,RegionId=child.RegionId,
                X=child.X+dx/d*NaturalResources.Reach(child)*.9,Z=child.Z+dz/d*NaturalResources.Reach(child)*.9};
            offered.Height=RoomSupplySupport.Height(State,offered.RegionId,offered.X,offered.Z,offered.Kind);
            if(!HasGroundSightLine(adult,offered.X,offered.Z)||!CanReachRoomFood(child,offered))
            {CancelFamilyCare(adult);adult.Activity="这里不方便接食水，先放下再找办法";return false;}
            parcel.CarrierId=null;parcel.Height=offered.Height;parcel.X=offered.X;parcel.Z=offered.Z;
            f.CareParcelId=null;f.NextCareAt=State.WorldSeconds+8;f.CareTargetId=null;
            adult.Energy=Math.Max(0,adult.Energy-.5);RegisterFamilyCare(adult,child,5);
            adult.Activity="把一份食水放到幼体嘴边";
            React(adult,Expression.Content,"饭饭在这里，慢慢吃。","送来的份量已经放下，幼体需要自己接这一口。",4);
            return true;
        }
        bool canTake=child.Interaction.EatingRemaining<=0&&!child.Interaction.NaturalIntakePending;
        bool water=canTake&&child.Interaction.Hydration<55&&child.Interaction.Hydration<child.Fullness;
        bool hungry=canTake&&child.Fullness<55;
        if(water||hungry)
        {
            var source=State.RoomObjects.Where(o=>o.RegionId==adult.RegionId&&!o.Stored&&!o.HeldByHand&&o.CarrierId==null&&o.Portions>0&&
                (water?o.Kind==RoomObjectKind.Water:o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet)&&
                NaturalResources.Consumer(State,o)==null&&Distance(adult.X,adult.Z,o.X,o.Z)<=NaturalResources.SenseRange&&CanSeeResource(adult,o))
                .MinBy(o=>Distance(adult.X,adult.Z,o.X,o.Z));
            if(source!=null)
            {
                double distance=Distance(adult.X,adult.Z,source.X,source.Z);
                if(distance>NaturalResources.Reach(adult)||!CanReachRoomFood(adult,source))
                {
                    var target=(X:source.X,Z:source.Z);
                    if(distance<=NaturalResources.Reach(adult)&&TryResourceStandingPoint(adult,source,out var standing))target=standing;
                    RoomMove(adult,target.X,target.Z,dt);adult.Activity=water?"寻找能送给宝宝的水":"寻找能送给宝宝的食物";
                    return CareProgress(adult,Distance(adult.X,adult.Z,target.X,target.Z),dt);
                }
                string id=NewFamilyId("supply");source.Portions--;
                var portion=new RoomObject{Id=id,Kind=source.Kind,RegionId=adult.RegionId,OwnerId=adult.Id,CarrierId=adult.Id,
                    X=adult.X,Z=adult.Z,Height=.45,Portions=1,Contamination=source.Contamination};
                State.RoomObjects.Add(portion);f.CareParcelId=id;f.CareBestDistance=100;f.CareApproachSeconds=0;
                adult.Activity="取好一份，准备送给幼体";return true;
            }
        }
        double gap=Distance(adult.X,adult.Z,child.X,child.Z);
        if(!FamilyInContact(adult,child)){RoomMove(adult,child.X,child.Z,dt);adult.Activity="靠近需要陪伴的幼体";return CareProgress(adult,gap,dt);}
        RegisterFamilyCare(adult,child,dt);
        child.Family.Warmth=Clamp100(child.Family.Warmth+dt*.16);
        adult.Energy=Math.Max(0,adult.Energy-dt*.01);adult.Activity="留在幼体旁边照看";
        f.CareApproachSeconds+=dt;
        if(f.CareApproachSeconds>12){f.CareTargetId=null;f.NextCareAt=State.WorldSeconds+12;}
        return true;
    }

    private bool CareProgress(Creature adult,double distance,double dt)
    {
        var f=adult.Family;
        if(distance<f.CareBestDistance-.03){f.CareBestDistance=distance;f.CareApproachSeconds=0;}
        else f.CareApproachSeconds+=dt;
        if(f.CareApproachSeconds<10)return true;
        DropCareParcel(adult);f.CareTargetId=null;f.NextCareAt=State.WorldSeconds+20;
        adult.Activity="暂时过不去，停下来寻求帮助";return false;
    }

    private void RegisterFamilyCare(Creature adult,Creature child,double seconds)
    {
        if(!adult.Family.Peers.TryGetValue(child.Id,out var a))adult.Family.Peers[child.Id]=a=new();
        if(!child.Family.Peers.TryGetValue(adult.Id,out var b))child.Family.Peers[adult.Id]=b=new();
        a.CareSeconds=Math.Min(1000000,a.CareSeconds+seconds);b.CareSeconds=Math.Min(1000000,b.CareSeconds+seconds);
        a.Familiarity=Clamp100(a.Familiarity+seconds*.03);b.Familiarity=Clamp100(b.Familiarity+seconds*.03);
        a.Affection=Clamp100(a.Affection+seconds*.025);b.Affection=Clamp100(b.Affection+seconds*.035);
        if(a.CareSeconds>=60&&!child.Family.CaregiverIds.Contains(adult.Id))
        {child.Family.CaregiverIds.Add(adult.Id);child.Family.LastEvent=IsCareParent(adult,child)?
            $"与{adult.Name}逐渐建立了稳定照护关系。":$"经过持续照顾，把{adult.Name}当作了养亲；血缘记录仍然保留。";
            RecordInteraction(child,"caregiver-"+adult.Id,child.Family.LastEvent,2);}
    }
}

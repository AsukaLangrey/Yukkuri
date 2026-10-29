namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public string ArrangeCare(string caregiverId,string childId)
    {
        var adult=State.Creatures.Find(c=>c.Id==caregiverId);var child=State.Creatures.Find(c=>c.Id==childId);
        var error=FamilyCommandError(adult)??FamilyCommandError(child);
        if(error!=null)return error;
        if(adult!.Id==child!.Id||adult.IsYoung||!child.IsYoung)return "需要一只成熟油照看幼体。";
        if(adult.RegionId!=child.RegionId||!FamilyRules.WillSocialize(adult)||adult.Family.Conflict.TargetId!=null||child.Family.ColdStorage.Stored)
            return "照护者现在没有余力，或幼体暂时无法接近。";
        bool parent=child.Family.MotherId==adult.Id||child.Family.FatherId==adult.Id||child.ParentId==adult.Id;
        if(!parent&&(!adult.Family.Peers.TryGetValue(child.Id,out var relationship)||relationship.Affection<20||adult.Aggression>.65))
            return "它还没有愿意接纳这只幼体，先让双方有安全相处的机会。";
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
            bool warmAdult=State.Creatures.Any(a=>a.Id!=c.Id&&!a.IsYoung&&a.RegionId==c.RegionId&&FamilyRules.WillSocialize(a)&&
                f.CaregiverIds.Contains(a.Id)&&Distance(c.X,c.Z,a.X,a.Z)<1.9);
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
        {parcel.CarrierId=null;parcel.Height=0;if(!parcel.HeldByHand){parcel.X=adult.X;parcel.Z=adult.Z;}}
        f.CareParcelId=null;
    }

    private void SynchronizeCareParcels()
    {
        foreach(var adult in State.Creatures)
        {
            var f=adult.Family;
            if(f.CareParcelId==null)continue;
            var parcel=State.RoomObjects.Find(o=>o.Id==f.CareParcelId);
            var child=State.Creatures.Find(c=>c.Id==f.CareTargetId);
            if(parcel==null||parcel.HeldByHand||parcel.Stored||parcel.Portions<=0||child==null||child.Condition==PhysicalCondition.Dead||
                !CanSelfMove(adult)||!FamilyRules.WillSocialize(adult)||f.Conflict.TargetId!=null||child.RegionId!=adult.RegionId||InteractionGestureTarget==adult.Id)
            {DropCareParcel(adult);f.CareTargetId=null;f.NextCareAt=State.WorldSeconds+12;continue;}
            parcel.RegionId=adult.RegionId;parcel.X=adult.X+Math.Sin(adult.Interaction.Facing)*.65;
            parcel.Z=adult.Z+Math.Cos(adult.Interaction.Facing)*.65;parcel.Height=.45;
        }
    }

    private bool TryFamilyCare(Creature adult,double dt)
    {
        var f=adult.Family;
        if(adult.IsYoung||!FamilyRules.WillSocialize(adult)||!CanSelfMove(adult)||f.Conflict.TargetId!=null||f.FlightRemaining>0)
        {DropCareParcel(adult);f.CareTargetId=null;return false;}
        var child=State.Creatures.Find(c=>c.Id==f.CareTargetId&&c.IsYoung&&c.Condition!=PhysicalCondition.Dead&&c.RegionId==adult.RegionId&&!c.Family.ColdStorage.Stored);
        if(child==null)
        {
            DropCareParcel(adult);f.CareTargetId=null;
            if(State.WorldSeconds<f.NextCareAt)return false;
            child=State.Creatures.Where(c=>c.IsYoung&&c.Condition!=PhysicalCondition.Dead&&!c.Family.ColdStorage.Stored&&c.RegionId==adult.RegionId&&
                (c.Family.CaregiverIds.Contains(adult.Id)||c.ParentId==adult.Id||adult.Aggression<=.25&&adult.Patience>=.65&&
                    f.Peers.TryGetValue(c.Id,out var known)&&known.Affection>=20&&known.Fear<25&&known.Resentment<25)&&
                Distance(adult.X,adult.Z,c.X,c.Z)<5&&FamilyVisible(adult,c,5)&&
                (c.Fullness<55||c.Interaction.Hydration<55||c.Family.Warmth<60))
                .OrderBy(c=>Math.Min(c.Fullness,Math.Min(c.Interaction.Hydration,c.Family.Warmth))).FirstOrDefault();
            if(child==null)return false;
            f.CareTargetId=child.Id;f.CareBestDistance=100;f.CareApproachSeconds=0;
        }
        if(InteractionGestureTarget==child.Id||child.Interaction.Carried||child.Interaction.Falling||child.Interaction.Restraint!=RestraintKind.None)
        {DropCareParcel(adult);return false;}
        if(f.CareParcelId is { } parcelId&&State.RoomObjects.Find(o=>o.Id==parcelId&&o.CarrierId==adult.Id) is { } parcel)
        {
            double distance=Distance(adult.X,adult.Z,child.X,child.Z);
            if(distance>1.7)
            {RoomMove(adult,child.X,child.Z,dt);adult.Activity="把食水送到宝宝旁边";return CareProgress(adult,distance,dt);}
            double dx=adult.X-child.X,dz=adult.Z-child.Z,d=Math.Max(.01,distance);
            parcel.CarrierId=null;parcel.Height=0;parcel.X=child.X+dx/d*NaturalResources.Reach(child)*.9;parcel.Z=child.Z+dz/d*NaturalResources.Reach(child)*.9;
            f.CareParcelId=null;f.NextCareAt=State.WorldSeconds+8;f.CareTargetId=null;
            adult.Energy=Math.Max(0,adult.Energy-.5);RegisterFamilyCare(adult,child,5);
            adult.Activity="把一份食水放到幼体嘴边";
            React(adult,Expression.Content,"饭饭在这里，慢慢吃。","送来的份量已经放下，幼体需要自己接这一口。",4);
            return true;
        }
        bool water=child.Interaction.Hydration<55&&child.Interaction.Hydration<child.Fullness;
        bool hungry=child.Fullness<55;
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
                {RoomMove(adult,source.X,source.Z,dt);adult.Activity=water?"寻找能送给宝宝的水":"寻找能送给宝宝的食物";return CareProgress(adult,distance,dt);}
                string id=NewFamilyId("supply");source.Portions--;
                var portion=new RoomObject{Id=id,Kind=source.Kind,RegionId=adult.RegionId,OwnerId=adult.Id,CarrierId=adult.Id,
                    X=adult.X,Z=adult.Z,Height=.45,Portions=1,Contamination=source.Contamination};
                State.RoomObjects.Add(portion);f.CareParcelId=id;f.CareBestDistance=100;f.CareApproachSeconds=0;
                adult.Activity="取好一份，准备送给幼体";return true;
            }
        }
        double gap=Distance(adult.X,adult.Z,child.X,child.Z);
        if(gap>1.75){RoomMove(adult,child.X,child.Z,dt);adult.Activity="靠近需要陪伴的幼体";return CareProgress(adult,gap,dt);}
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
        a.Affection=Clamp100(a.Affection+seconds*.025);b.Affection=Clamp100(b.Affection+seconds*.035);
        if(a.CareSeconds>=60&&!child.Family.CaregiverIds.Contains(adult.Id))
        {child.Family.CaregiverIds.Add(adult.Id);child.Family.LastEvent=$"与{adult.Name}逐渐建立了照护关系。";RecordInteraction(child,"caregiver",child.Family.LastEvent,2);}
    }
}

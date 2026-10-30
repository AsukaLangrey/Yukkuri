namespace Yukkuri.Simulation;

public sealed partial class FamilyLife
{
    // Independent from death/pregnancy outcomes; absent in old saves means no record yet.
    public string CareLastEvent { get; set; } = "";
}

public sealed partial class WorldSimulation
{
    private void RecordFamilyCareStatus(Creature c,string text)
    {
        text=text.Length>512?text[..512]:text;
        c.Family.CareLastEvent=text;
        var pregnancy=c.Family.Pregnancy;
        if(c.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)&&pregnancy.Phase==PregnancyPhase.None&&
            (string.IsNullOrEmpty(pregnancy.Outcome)||c.Family.LastEvent!=pregnancy.Outcome))c.Family.LastEvent=text;
    }
    // Being unable to travel does not remove the ability to warm a child already
    // alongside. Transport checks CanSelfMove separately at every carrying step.
    private bool CaregiverBodyReady(Creature adult) => !adult.IsYoung && FamilyRules.WillSocialize(adult) &&
        adult.Family.Conflict.TargetId==null && adult.Family.FlightRemaining<=0 && adult.RetreatRemaining<=0 &&
        !adult.Interaction.Resting && adult.Interaction.RestRemaining<=0 &&
        !adult.Interaction.FollowPlayer && adult.Interaction.WaitRemaining<=0 && adult.Interaction.Height<=.05 && !adult.Interaction.Lowering &&
        InteractionGestureTarget!=adult.Id && !NaturalResources.WantsFood(adult) && !NaturalResources.WantsWater(adult);

    private bool CaregiverReady(Creature adult) => CaregiverBodyReady(adult)&&adult.Family.CompanionTargetId==null;

    // Sharing real body warmth with this child is compatible with talking to
    // this same child. It neither starts a delivery nor earns care relationship time.
    private bool CanShareFamilyWarmth(Creature adult,Creature child) => CaregiverBodyReady(adult)&&
        (adult.Family.CompanionTargetId==null||adult.Family.CompanionTargetId==child.Id&&child.Family.CompanionTargetId==adult.Id)&&
        CareTargetAvailable(adult,child)&&(child.Family.CaregiverIds.Contains(adult.Id)||IsCareParent(adult,child))&&
        FamilyInContact(adult,child);

    private static bool IsCareParent(Creature adult,Creature child) => child.ParentId==adult.Id ||
        child.Family.MotherId==adult.Id || child.Family.FatherId==adult.Id;

    private static bool NeedsAssistedIntake(Creature c) => !c.IsYoung &&
        (c.Condition==PhysicalCondition.Recovering||UsesGroundFoot(c)&&c.Interaction.FootIntegrity<=25) &&
        (c.Fullness<55||c.Interaction.Hydration<55);

    private static bool CareRelationshipSafe(Creature a,Creature b) =>
        (!a.Family.Peers.TryGetValue(b.Id,out var ab)||ab.Fear<40&&ab.Resentment<35) &&
        (!b.Family.Peers.TryGetValue(a.Id,out var ba)||ba.Fear<40&&ba.Resentment<35);

    private static bool AdultCareRelationship(Creature a,Creature b) => FamilyRules.IsCloseFamily(a,b) ||
        a.Family.PartnerIds.Contains(b.Id)&&b.Family.PartnerIds.Contains(a.Id) ||
        a.Family.CaregiverIds.Contains(b.Id)||b.Family.CaregiverIds.Contains(a.Id) ||
        a.Family.Peers.TryGetValue(b.Id,out var ab)&&ab.Affection>=35&&ab.Familiarity>=30&&
        b.Family.Peers.TryGetValue(a.Id,out var ba)&&ba.Affection>=35&&ba.Familiarity>=30;

    private bool CareTargetAvailable(Creature adult,Creature child) => child!=adult && (child.IsYoung||NeedsAssistedIntake(child)) &&
        child.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering && child.RegionId==adult.RegionId &&
        !child.Family.ColdStorage.Stored && !FamilyRules.UnderIntervention(child) && !child.Interaction.Water.InWater&&
        !child.Interaction.Carried && !child.Interaction.Falling && !child.Interaction.Lowering && !child.Interaction.Supine && child.Interaction.Height<=.05 &&
        child.Interaction.Restraint is RestraintKind.None or RestraintKind.Wrap && InteractionGestureTarget!=child.Id &&
        !child.Interaction.Physiology.Active && !child.Interaction.Injuries.SelfCare.Active &&
        child.Family.FlightRemaining<=0 && child.RetreatRemaining<=0 && child.Family.Conflict.TargetId==null &&
        child.Family.ChildThreat.TargetId==null && CareRelationshipSafe(adult,child) &&
        (child.IsYoung||AdultCareRelationship(adult,child)) &&
        FamilyVisible(adult,child,6) && HasGroundSightLine(adult,child.X,child.Z);

    private bool CanContinueFamilyCare(Creature adult,Creature recipient) => CaregiverReady(adult)&&CareTargetAvailable(adult,recipient)&&
        (CanSelfMove(adult)||recipient.IsYoung&&FamilyInContact(adult,recipient));

    private bool CanChooseAutonomousFamilyCare(Creature adult,Creature child) => CanContinueFamilyCare(adult,child)&&
        (!child.IsYoung||child.Family.CaregiverIds.Contains(adult.Id)||IsCareParent(adult,child)||adult.Aggression<=.25&&adult.Patience>=.65&&
            adult.Family.Peers.TryGetValue(child.Id,out var known)&&known.Affection>=20&&known.Fear<25&&known.Resentment<25)&&
        Distance(adult.X,adult.Z,child.X,child.Z)<5&&FamilyVisible(adult,child,5)&&
        (child.Fullness<55||child.Interaction.Hydration<55||child.IsYoung&&child.Family.Warmth<60);

    // This is also read by a paused management modal. Do not create bonds,
    // update messages or apply the command's Pause gate while explaining it.
    public string? FamilyCareReason(string caregiverId,string recipientId)
    {
        var adult=State.Creatures.Find(c=>c.Id==caregiverId);var recipient=State.Creatures.Find(c=>c.Id==recipientId);
        if(!State.InteractionPlaytest||adult==null||recipient==null||adult.RegionId!=State.ActiveRegionId||recipient.RegionId!=adult.RegionId)
            return "先选择当前小院里能看见彼此的两只油。";
        return FamilyCareReasonCore(adult,recipient);
    }

    private string? FamilyCareReasonCore(Creature adult,Creature recipient)
    {
        if(adult.RegionId!=recipient.RegionId)return "双方已经不在同一地区，递送需在同一区域重新安排。";
        if(adult==recipient||adult.IsYoung)return "需要另一只成熟油承担照护。";
        if(adult.Condition!=PhysicalCondition.Active)return "照护者自身还需要救治或恢复，请先照顾它或换一位家人。";
        if(adult.Family.ColdStorage.Stored)return "照护者正在冷藏，请先安全取出。";
        if(adult.Family.ColdStorage.RecoveryRemaining>0)return "照护者刚取出冷藏，先等身体复苏。";
        if(FamilyRules.UnderIntervention(adult))return "照护者正处于繁育干预中，先停止干预并照顾身体。";
        if(adult.Interaction.Injuries.SelfCare.Active)return "照护者正在照顾自身伤口，先完成眼前的护伤。";
        if(adult.Interaction.Carried||adult.Interaction.Falling||adult.Interaction.Lowering||adult.Interaction.Height>.05)
            return "照护者还未稳稳落地，先放稳再安排。";
        if(adult.Interaction.Restraint!=RestraintKind.None||adult.Interaction.Supine)
            return "照护者受到限制或正翻着身体，先解除限制并帮助它放稳。";
        if(adult.Pain>=25||adult.Fear>=55||adult.Interaction.Digestion.Symptomatic)
            return "照护者正在疼痛、害怕或肚子不适，先处理它自身的不适。";
        if(NaturalResources.WantsFood(adult))return "照护者自己还饿，先给它准备食物，或换另一位家人。";
        if(NaturalResources.WantsWater(adult))return "照护者自己还渴，先给它准备水，或换另一位家人。";
        if(adult.Energy<=25||adult.Interaction.Resting||adult.Interaction.RestRemaining>0)
            return "照护者需要休息，先让它休养或换另一位有余力的家人。";
        if(adult.Interaction.EatingRemaining>0)return "照护者正在吃喝，等它咽下当前一口再安排。";
        if(adult.Interaction.Physiology.Active)return "照护者正在排泄，等当前身体动作结束再安排。";
        if(adult.Family.CompanionTargetId!=null)return "照护者正在陪伴另一只油，先结束这段相处或选择另一位家人。";
        if(adult.Family.Conflict.TargetId!=null||adult.Family.ChildThreat.TargetId!=null||adult.Family.FlightRemaining>0||adult.RetreatRemaining>0)
            return "照护者正在冲突或躲避，先处理危险并让它安定下来。";
        if(InteractionGestureTarget==adult.Id)return "照护者正接受手部操作，先收手并结束当前操作。";
        if(adult.Interaction.ActiveCue!=null||adult.Interaction.FollowPlayer||adult.Interaction.WaitRemaining>0)
            return "照护者正在接受指令或教学，先结束当前指令。";
        if(!CaregiverReady(adult))return "照护者暂时没有余力，先照顾它的身体需要。";
        if(recipient.Condition is PhysicalCondition.Critical or PhysicalCondition.Dead)
            return recipient.Condition==PhysicalCondition.Critical?"它正处于濒危，需要先救治稳定，不能直接送入口。":"它已经离世，不能再接食水。";
        if(!recipient.IsYoung&&!NeedsAssistedIntake(recipient))return "成年对象目前能自行生活，或暂时不需要补充食水。";
        if(recipient.Family.ColdStorage.Stored)return "对象仍在冷藏中，先安全取出再安排食水。";
        if(FamilyRules.UnderIntervention(recipient))return "对象仍在繁育干预中，先停止干预并放稳身体。";
        if(recipient.Interaction.Lowering)return "对象正在放下，等放稳后再安排。";
        if(recipient.Interaction.Carried)return "对象还被手托着，先放稳再安排送食水。";
        if(recipient.Interaction.Falling)return "对象正在下落，先帮助它安全落稳。";
        if(recipient.Interaction.Injuries.SelfCare.Active)return "对象正在照顾自身伤口，等它结束护伤后再安排递送。";
        if(recipient.Interaction.Height>.05)return "对象还在高处，先安置到地面稳定支撑处。";
        if(recipient.Interaction.Restraint is not (RestraintKind.None or RestraintKind.Wrap)||recipient.Interaction.Supine)
            return "对象正在箱内、悬挂或其他受限姿势中，先解除限制并放稳；地面柔软包裹可以保留。";
        if(InteractionGestureTarget==recipient.Id||recipient.Interaction.Physiology.Active)
            return "对象正接受手部操作或正在排泄，先结束当前动作。";
        if(recipient.Family.FlightRemaining>0||recipient.RetreatRemaining>0||recipient.Family.Conflict.TargetId!=null||recipient.Family.ChildThreat.TargetId!=null)
            return "对象正在冲突或躲避，先处理危险再靠近照护。";
        if(!CareRelationshipSafe(adult,recipient))return "双方仍有害怕或怨恨，先安全相处缓解关系，或选择另一位家人。";
        if(!recipient.IsYoung&&!AdultCareRelationship(adult,recipient))return "双方还不够亲近，先安全相处，或选择已有信任关系的家人。";
        if(adult.Quarantine!=recipient.Quarantine)return "双方处在不同隔离范围，先按实际安置安排同处。";
        if(Distance(adult.X,adult.Z,recipient.X,recipient.Z)>6)return "双方相距太远，先让它们来到彼此可见的近处。";
        if(!HasGroundSightLine(adult,recipient.X,recipient.Z)||!FamilyVisible(adult,recipient,6))
            return "双方之间看不清或有遮挡，先腾开通路、调整安置位置。";
        if(!CareTargetAvailable(adult,recipient))return "对象暂时无法接受照护，先检查它的身体状态与安置。";
        if(!CanSelfMove(adult)&&(!recipient.IsYoung||!FamilyInContact(adult,recipient)))
            return "照护者现在走不开，只能照看已经贴近的幼体；送食水请选另一位能走动的家人。";
        if(recipient.IsYoung&&!IsCareParent(adult,recipient)&&!recipient.Family.CaregiverIds.Contains(adult.Id)&&
            (!adult.Family.Peers.TryGetValue(recipient.Id,out var relation)||relation.Affection<20||adult.Aggression>.65))
            return "它还没有愿意接纳这只幼体，先让双方有安全相处的机会。";
        return null;
    }

    private void CancelFamilyCare(Creature adult,double cooldown=12,string reason="这次照护已停止，先处理身体状态或眼前的需要。")
    {
        if(adult.Family.CareTargetId==null&&adult.Family.CareParcelId==null)
        {adult.Family.CareApproachSeconds=0;adult.Family.CareBestDistance=100;return;}
        var recipient=State.Creatures.Find(c=>c.Id==adult.Family.CareTargetId);
        DropCareParcel(adult);adult.Family.CareTargetId=null;adult.Family.CareApproachSeconds=0;
        adult.Family.CareBestDistance=100;adult.Family.NextCareAt=Math.Max(adult.Family.NextCareAt,State.WorldSeconds+cooldown);
        RecordFamilyCareStatus(adult,reason);
        if(recipient!=null)RecordFamilyCareStatus(recipient,$"{adult.Name}的照护已中断：{reason}");
        if(adult.Condition==PhysicalCondition.Active)adult.Activity=reason;
    }

    public string ArrangeCare(string caregiverId,string childId)
    {
        var adult=State.Creatures.Find(c=>c.Id==caregiverId);var child=State.Creatures.Find(c=>c.Id==childId);
        var error=FamilyCommandError(adult)??FamilyCommandError(child);
        if(error!=null)return error;
        error=FamilyCareReason(caregiverId,childId);if(error!=null)return error;
        if(adult!.Family.CareTargetId==child!.Id)return "已经在照护这个对象，给实际靠近和递送留一点时间。";
        CancelFamilyCare(adult,reason:"照护已改派给另一位对象；旧递送已中断，如有已取份额，已经留在身旁。");
        adult.Family.CareTargetId=child!.Id;adult.Family.CareApproachSeconds=0;adult.Family.CareBestDistance=100;
        RecordFamilyCareStatus(adult,!CanSelfMove(adult)?"留在已贴近的幼体旁照看；身体走不开，无法搬运食水。":
            child.IsYoung?"已安排照看幼体，会在有余力时靠近保暖并寻找实际食水。":"已安排为行动不便的亲近对象送食水，需要实际取到并送达。");
        return State.LastActionMessage=adult.Family.CareLastEvent;
    }

    private void TickYoungLife(Creature c,double dt)
    {
        if(!c.IsYoung||c.Condition==PhysicalCondition.Dead)return;
        var f=c.Family;
        if(!f.ColdStorage.Stored)
        {
            bool supported=c.Interaction.Restraint==RestraintKind.Wrap||HasCushionedRestSupport(c);
            bool warmAdult=State.Creatures.Any(a=>CanShareFamilyWarmth(a,c));
            f.Warmth=Math.Clamp(f.Warmth+dt*(supported||warmAdult ? .18 : -.025),0,100);
            if(f.Warmth<15)Hurt(c,dt*.025,"幼体持续失温",null);
            if(c.Condition==PhysicalCondition.Dead)return;
            if(c.Condition==PhysicalCondition.Active&&f.Warmth<35&&c.ReactionRemaining<=0)
                React(c,Expression.Calling,"冷冷……陪陪……","幼体缩着身体，寻找温暖和陪伴。",5);
        }
        // Birth provenance is optional in old saves, but real body age is not.
        // Growing up must not fabricate a birth event or reset age/role/history.
        if(c.Body.AgeSeconds>=FamilyRules.YoungSeconds&&c.Fullness>35&&c.Health>50&&f.Warmth>40)
        {c.IsYoung=false;f.LastEvent="已经长大，开始学习独立生活。";RecordInteraction(c,"grown",f.LastEvent,3);}
    }

    private void DropCareParcel(Creature adult)
    {
        var f=adult.Family;
        if(f.CareParcelId is { } id&&State.RoomObjects.Find(o=>o.Id==id&&o.CarrierId==adult.Id) is { } parcel)
        {
            parcel.CarrierId=null;
            if(!parcel.HeldByHand&&!parcel.Stored)
            {
                // Crossing regions interrupts delivery. Do not teleport its
                // last physically tracked portion into the carrier's new region.
                if(parcel.RegionId==adult.RegionId){parcel.X=adult.X;parcel.Z=adult.Z;}
                parcel.Height=RoomSupplySupport.Height(State,parcel.RegionId,parcel.X,parcel.Z,parcel.Kind);
            }
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
            if(child==null||!CanContinueFamilyCare(adult,child)||f.CareParcelId!=null&&!CanSelfMove(adult))
            {CancelFamilyCare(adult,reason:child==null?"照护对象已不在这里，这次递送已停止。":
                FamilyCareReasonCore(adult,child)??"照护者已经不能搬运，原来的一份食水已留在身旁。");continue;}
            if(f.CareParcelId==null)continue;
            var parcel=State.RoomObjects.Find(o=>o.Id==f.CareParcelId);
            if(parcel==null||parcel.CarrierId!=adult.Id||parcel.HeldByHand||parcel.Stored||parcel.Portions!=1)
            {CancelFamilyCare(adult,reason:"原来搬运的一份食水已被拿走或改变，这次递送已停止。");continue;}
            if(parcel.RegionId!=adult.RegionId)
            {CancelFamilyCare(adult,reason:"照护者已离开原地区，递送已中断；已取份额留在原处。");continue;}
            parcel.RegionId=adult.RegionId;parcel.X=adult.X+Math.Sin(adult.Interaction.Facing)*.65;
            parcel.Z=adult.Z+Math.Cos(adult.Interaction.Facing)*.65;parcel.Height=.45;
        }
    }

    private bool TryFamilyCare(Creature adult,double dt)
    {
        var f=adult.Family;
        if(!CaregiverReady(adult))
        {
            var previous=State.Creatures.Find(c=>c.Id==f.CareTargetId);
            if(f.CareTargetId!=null||f.CareParcelId!=null)CancelFamilyCare(adult,reason:previous==null?
                "照护对象已不在这里，这次递送已停止。":FamilyCareReasonCore(adult,previous)??"照护者已没有余力，这次递送先停止。");
            return false;
        }
        var child=State.Creatures.Find(c=>c.Id==f.CareTargetId&&CanContinueFamilyCare(adult,c));
        if(child==null)
        {
            if(f.CareTargetId!=null||f.CareParcelId!=null)
                CancelFamilyCare(adult,reason:"对象已经无法继续接受这次照护，递送已中断；如有已取份额，已经留在身旁。");
            if(State.WorldSeconds<f.NextCareAt)return false;
            child=State.Creatures.Where(c=>CanChooseAutonomousFamilyCare(adult,c))
                .OrderBy(c=>Math.Min(c.Fullness,Math.Min(c.Interaction.Hydration,c.IsYoung?c.Family.Warmth:100)))
                .ThenBy(c=>c.Id,StringComparer.Ordinal).FirstOrDefault();
            if(child==null)return false;
            f.CareTargetId=child.Id;f.CareBestDistance=100;f.CareApproachSeconds=0;
            RecordFamilyCareStatus(adult,$"正在留意{child.Name}的食水与照护需要。");
        }
        // A stationary caregiver can share warmth at real contact only. It must
        // not reserve even a nearby portion and later teleport it to a child.
        if(!CanSelfMove(adult))
        {
            if(f.CareParcelId!=null)
            {CancelFamilyCare(adult,reason:"照护者已经不能搬运，原来的一份食水已留在身旁。");return false;}
            if(!child.IsYoung||!FamilyInContact(adult,child))
            {CancelFamilyCare(adult,reason:"身体走不开，无法赶到对象身旁照护。");return false;}
            return StayBesideChild(adult,child,dt,stationary:true);
        }
        if(f.CareParcelId is { } parcelId&&State.RoomObjects.Find(o=>o.Id==parcelId&&o.CarrierId==adult.Id) is { } parcel)
        {
            double distance=Distance(adult.X,adult.Z,child.X,child.Z);
            if(!FamilyInContact(adult,child))
            {RoomMove(adult,child.X,child.Z,dt);RecordFamilyCareStatus(adult,adult.Activity=$"正在把一份食水送到{child.Name}身旁。");return CareProgress(adult,distance,dt);}
            double dx=adult.X-child.X,dz=adult.Z-child.Z,d=Math.Max(.01,distance);
            var offered=new RoomObject{Id="care-placement-probe",Kind=parcel.Kind,RegionId=child.RegionId,
                X=child.X+dx/d*NaturalResources.Reach(child)*.9,Z=child.Z+dz/d*NaturalResources.Reach(child)*.9};
            offered.Height=RoomSupplySupport.Height(State,offered.RegionId,offered.X,offered.Z,offered.Kind);
            if(!HasGroundSightLine(adult,offered.X,offered.Z)||!CanReachRoomFood(child,offered))
            {CancelFamilyCare(adult,reason:"这里不方便接食水，原来的一份已放下，需要换个安全落点。");return false;}
            parcel.CarrierId=null;parcel.Height=offered.Height;parcel.X=offered.X;parcel.Z=offered.Z;
            f.CareParcelId=null;f.NextCareAt=State.WorldSeconds+8;f.CareTargetId=null;
            f.CareApproachSeconds=0;f.CareBestDistance=100;
            adult.Energy=Math.Max(0,adult.Energy-.5);RegisterFamilyCare(adult,child,5);
            RecordFamilyCareStatus(adult,adult.Activity=$"已把一份食水放到{child.Name}嘴边，等待它实际取用。");
            RecordFamilyCareStatus(child,$"{adult.Name}送来的一份食水已经放在可达处，还需要自己接这一口。");
            React(adult,Expression.Content,NaturalResources.IsWater(parcel.Kind)?"水水在这里，慢慢喝。":"饭饭在这里，慢慢吃。",
                "送来的份量已经放下，对方需要自己接这一口。",4);
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
                    RoomMove(adult,target.X,target.Z,dt);RecordFamilyCareStatus(adult,adult.Activity=water?$"正在寻找能送给{child.Name}的水。":$"正在寻找能送给{child.Name}的食物。");
                    return CareProgress(adult,Distance(adult.X,adult.Z,target.X,target.Z),dt);
                }
                string id=NewFamilyId("supply");source.Portions--;
                var portion=new RoomObject{Id=id,Kind=source.Kind,RegionId=adult.RegionId,OwnerId=adult.Id,CarrierId=adult.Id,
                    X=adult.X,Z=adult.Z,Height=.45,Portions=1,Contamination=source.Contamination};
                State.RoomObjects.Add(portion);f.CareParcelId=id;f.CareBestDistance=100;f.CareApproachSeconds=0;
                RecordFamilyCareStatus(adult,adult.Activity=$"已取好一份，准备送给{child.Name}。");return true;
            }
            if(!child.IsYoung){CancelFamilyCare(adult,reason:"附近没有可取用的合适食水，这次递送先停下；准备物资后可再安排。");return false;}
            RecordFamilyCareStatus(adult,"附近没有可取用的合适食水，先去幼体旁陪护，不能凭空补充食水。");
        }
        if(!child.IsYoung)
        {CancelFamilyCare(adult,reason:canTake?"对象暂时不需要继续送食水。":"对象还在咽下当前一口，暂缓下一次递送。");return false;}
        double gap=Distance(adult.X,adult.Z,child.X,child.Z);
        if(!FamilyInContact(adult,child)){RoomMove(adult,child.X,child.Z,dt);adult.Activity="靠近需要陪伴的幼体";return CareProgress(adult,gap,dt);}
        return StayBesideChild(adult,child,dt,stationary:false);
    }

    private bool StayBesideChild(Creature adult,Creature child,double dt,bool stationary)
    {
        var f=adult.Family;
        RegisterFamilyCare(adult,child,dt);
        child.Family.Warmth=Clamp100(child.Family.Warmth+dt*.16);
        adult.Energy=Math.Max(0,adult.Energy-dt*.01);
        RecordFamilyCareStatus(adult,adult.Activity=stationary?"留在已贴近的幼体旁保暖；身体走不开，无法搬运食水。":"留在幼体旁边照看。");
        f.CareApproachSeconds+=dt;
        if(f.CareApproachSeconds>12)
        {f.CareTargetId=null;f.NextCareAt=State.WorldSeconds+12;f.CareApproachSeconds=0;f.CareBestDistance=100;
            RecordFamilyCareStatus(adult,"这一段近旁陪护已完成，先歇一会儿再留意需要。");}
        return true;
    }

    private bool CareProgress(Creature adult,double distance,double dt)
    {
        var f=adult.Family;
        if(distance<f.CareBestDistance-.03){f.CareBestDistance=distance;f.CareApproachSeconds=0;}
        else f.CareApproachSeconds+=dt;
        if(f.CareApproachSeconds<10)return true;
        CancelFamilyCare(adult,20,"暂时过不去，递送已停下；如有取出的份额，已经留在身旁。");return false;
    }

    private void RegisterFamilyCare(Creature adult,Creature child,double seconds)
    {
        // Food already delivered and real shared warmth still help the body.
        // CareSeconds is evidence for a trusted caregiver, not the physical
        // warmth clock: a recent aggressor cannot buy that evidence or affection
        // while the same object's harm memory still prevents safe company.
        if(!PeerCompanyMemoryAllows(adult,child))return;
        if(!adult.Family.Peers.TryGetValue(child.Id,out var a))adult.Family.Peers[child.Id]=a=new();
        if(!child.Family.Peers.TryGetValue(adult.Id,out var b))child.Family.Peers[adult.Id]=b=new();
        a.CareSeconds=Math.Min(1000000,a.CareSeconds+seconds);b.CareSeconds=Math.Min(1000000,b.CareSeconds+seconds);
        a.Familiarity=Clamp100(a.Familiarity+seconds*.03);b.Familiarity=Clamp100(b.Familiarity+seconds*.03);
        a.Affection=Clamp100(a.Affection+seconds*.025);b.Affection=Clamp100(b.Affection+seconds*.035);
        if(child.IsYoung&&a.CareSeconds>=60&&!child.Family.CaregiverIds.Contains(adult.Id))
        {child.Family.CaregiverIds.Add(adult.Id);child.Family.LastEvent=IsCareParent(adult,child)?
            $"与{adult.Name}逐渐建立了稳定照护关系。":$"经过持续照顾，把{adult.Name}当作了养亲；血缘记录仍然保留。";
            RecordInteraction(child,"caregiver-"+adult.Id,child.Family.LastEvent,2);}
    }
}

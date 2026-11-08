namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private bool ShelterNeedsPriority(Creature c)=>NaturalResources.WantsFood(c)||NaturalResources.WantsWater(c)||c.Pain>=40||c.Energy<22||
        HasPriorityWaterDuty(c)||c.Interaction.EatingRemaining>0||c.Interaction.ActiveCue!=null||c.Interaction.FollowPlayer||
        c.Family.CompanionTargetId!=null||FamilyRules.UnderIntervention(c)||
        c.Family.Pregnancy.Phase==PregnancyPhase.Preparing||c.Family.Conflict.TargetId!=null||c.Family.ChildThreat.TargetId!=null||
        c.Family.Pregnancy.Phase==PregnancyPhase.Gestating&&c.Family.Pregnancy.Elapsed>=FamilyRules.GestationSeconds(c.Family.Pregnancy.Method)||
        c.Personality.HungerTargetId!=null||WaterConcernTarget(c)!=null;
    public string? ShelterEntryReason(Creature c,string shelterId)
    {
        var s=Shelter(shelterId);
        if(!State.InteractionPlaytest||!State.Creatures.Contains(c)||s==null||c.RegionId!=s.RegionId)return "住居与个体不在同一区域。";
        if(!CanSelfMove(c)||c.Interaction.Rolling||c.Interaction.Lowering||c.Interaction.Height>.05||
            c.Family.ColdStorage.RecoveryRemaining>0||c.Quarantine||c.IsIntruder)return "当前身体姿势或状态不能自行走进门。";
        if(AttachedStem(c)!=null)return "附着的外茎伸出身体，当前门洞不足以安全通过。";
        if(!s.DoorOpen)return "门帘关着，先寻找其他开门的小家。";
        if(InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy||ShelterNeedsPriority(c))return "先完成眼前的身体需要、实际互动或家庭事务。";
        double radius=ShelterPassageRadius(c);
        if(radius>ShelterGeometry.DoorWidth*.5-.04||radius>ShelterGeometry.InteriorWidth*.5-.04||
            ShelterBodyEnvelope.Height(c)>ShelterGeometry.DoorHeight-.03)return "当前身体与饰物包络不能安全通过这道门。";
        if(s.GuestId!=null&&s.GuestId!=c.Id&&ValidShelterGuest(s)!=null||PhysicalShelterGuest(s) is { } other&&other!=c)
            return "这间单住户小屋已经有人或身体占着。";
        return null;
    }
    private void ReleaseShelterGuest(ShelterState s,string message)
    {s.GuestId=null;s.Phase=ShelterVisitPhase.Empty;s.ExitRequested=false;s.BlockedSeconds=0;s.LastEvent=message;}
    private void SynchronizeShelters()
    {
        foreach(var s in State.Shelters)
        {
            var c=ValidShelterGuest(s);
            if(s.GuestId!=null&&c==null)
                ReleaseShelterGuest(s,"原来的自主入住已结束；实际身体仍按当前位置占位。");
            if(c!=null)
            {
                bool inOrNear=ShelterBodyPresent(c,s);
                if(inOrNear&&(!CanSelfMove(c)||ShelterNeedsPriority(c)))s.ExitRequested=true;
                if(s.Phase is ShelterVisitPhase.Inside or ShelterVisitPhase.Exiting&&
                    !inOrNear&&c.Z-s.Z>ShelterGeometry.OuterDepth*.5)
                {c.Interaction.ShelterSearchAfter=Math.Max(c.Interaction.ShelterSearchAfter,State.WorldSeconds+20);ReleaseShelterGuest(s,"它已经实际离开门口，住居空出了。");continue;}
                if(s.Phase==ShelterVisitPhase.Approaching&&ShelterNeedsPriority(c))
                {ReleaseShelterGuest(s,"它先去处理食水、伤痛或家庭事务，这次入住预约结束。");continue;}
            }
            // Human placement can put a body inside, but no location is changed
            // to make that ownership true. A carried/dead body remains physical
            // occupancy without creating a new autonomous visit.
            if(s.GuestId==null&&PhysicalShelterGuest(s) is { } physical&&physical.Condition!=PhysicalCondition.Dead&&
                !physical.Interaction.Carried&&!physical.Interaction.Falling&&
                !State.Shelters.Any(other=>other!=s&&other.GuestId==physical.Id))
            {s.GuestId=physical.Id;s.Phase=ShelterFullyInside(physical,s)?ShelterVisitPhase.Inside:ShelterVisitPhase.Exiting;
                s.ExitRequested=s.Phase==ShelterVisitPhase.Exiting;s.LastEvent="身体确实在屋内，出门仍须经过真实门洞。";}
        }
    }
    private string? ShelterExitBlockReason(ShelterState s,Creature c)
    {
        if(!ShelterBodyPresent(c,s)||!s.ExitRequested&&s.Phase!=ShelterVisitPhase.Exiting)return null;
        if(!CanSelfMove(c)||c.Interaction.Rolling||c.Interaction.Height>.05)return "它在屋内走不开，需要实际护理或从开门处低托救出。";
        if(!s.DoorOpen)return "出口门帘关着，它正在等人类打开。";
        double radius=ShelterPassageRadius(c);
        if(radius>ShelterGeometry.DoorWidth*.5-.04||ShelterBodyEnvelope.Height(c)>ShelterGeometry.DoorHeight-.03)
            return "身体或饰物当前无法通过门洞，需要先处理实际姿态或饰物。";
        double nextZ=Math.Min(s.Z+ShelterGeometry.OutsideZ(radius),c.Z+.10);
        if(!CanOccupyRoom(c,c.X,nextZ))return "门口或通路被实际身体、物件挡住，它在等待让路。";
        return null;
    }
    private void ShelterWait(ShelterState s,Creature c,double dt,string reason)
    {
        s.BlockedSeconds=Math.Min(600,s.BlockedSeconds+dt);s.LastEvent=reason;c.Activity=reason;
        if(s.ExitRequested&&s.BlockedSeconds>=1&&State.WorldSeconds-s.LastHelpAt>=12)
        {
            s.LastHelpAt=State.WorldSeconds;
            Feedback(c,"shelter-help",Expression.Calling,reason,"help-look",false);
        }
    }
    private bool MoveShelterStep(ShelterState s,Creature c,double x,double z,double dt)
    {
        // Door traversal follows an aligned route. A generic avoidance turn can
        // move sideways, trigger a retreat to the room centre on the next tick,
        // then repeat forever while each retreat clears the blocked timer.
        // Keep avoidance for the outdoor approach; wait on the actual doorway
        // route until its obstruction is removed.
        double before=Distance(c.X,c.Z,x,z);RoomMove(c,x,z,dt,slide:s.Phase==ShelterVisitPhase.Approaching);
        if(Distance(c.X,c.Z,x,z)<before-.0001){s.BlockedSeconds=0;return true;}
        ShelterWait(s,c,dt,"门口或通路被挡住，等一等再走。");return false;
    }
    // Existing visits route urgent needs through the actual doorway first.
    // They do not complete care, intake, teaching or breeding on their behalf.
    private bool TryActiveShelter(Creature c,double dt)
    {
        var s=State.Shelters.Find(s=>s.GuestId==c.Id&&ValidShelterGuest(s)==c);if(s==null)return false;
        bool inside=ShelterBodyPresent(c,s);
        if(s.Phase==ShelterVisitPhase.Approaching&&ShelterEntryReason(c,s.Id)!=null)
        {ReleaseShelterGuest(s,"眼前事务优先，这次入住预约已结束。");return false;}
        if(inside&&(ShelterNeedsPriority(c)||!CanSelfMove(c)||
            EmotionalReadiness.Settled(c)&&c.Fear<25&&c.Interaction.Emotion.Alarm<8))s.ExitRequested=true;
        if(c.Interaction.EatingRemaining>0||c.Interaction.Physiology.Active||FamilyRules.UnderIntervention(c))return false;
        // A blocked exit must not suppress food already within real mouth reach.
        if(inside&&(NaturalResources.WantsFood(c)||NaturalResources.WantsWater(c))&&TryEatNearby(c))return true;
        if(s.ExitRequested)
        {
            s.Phase=ShelterVisitPhase.Exiting;
            if(ShelterExitBlockReason(s,c) is { } reason&&(!CanSelfMove(c)||!s.DoorOpen||
                ShelterPassageRadius(c)>ShelterGeometry.DoorWidth*.5-.04||ShelterBodyEnvelope.Height(c)>ShelterGeometry.DoorHeight-.03))
            {
                if(c.Energy<22||c.Interaction.RestRemaining>0)c.Interaction.RestOnGroundUntil=State.WorldSeconds+2;
                ShelterWait(s,c,dt,reason);return true;
            }
            double radius=ShelterPassageRadius(c),outside=s.Z+ShelterGeometry.OutsideZ(radius);
            if(c.Z-s.Z>=ShelterGeometry.OuterDepth*.5+radius+.04)
            {c.Interaction.ShelterSearchAfter=Math.Max(c.Interaction.ShelterSearchAfter,State.WorldSeconds+20);ReleaseShelterGuest(s,"它已完整走出门槛，住居空出了。");return false;}
            // First align inside the room, then traverse the door on its centerline.
            double targetZ=Math.Abs(c.X-s.X)>.025?Math.Min(c.Z,s.Z):outside;
            MoveShelterStep(s,c,s.X,targetZ,dt);c.Activity="沿实际门口慢慢出去";return true;
        }
        if(s.Phase==ShelterVisitPhase.Inside)
        {c.TargetX=c.X;c.TargetZ=c.Z;c.Activity="留在小屋里观察，刚才的不安仍然记得";return true;}
        if(!CanSelfMove(c)){ShelterWait(s,c,dt,"身体暂时走不开，这次进门先停下。");return false;}
        double doorZ=s.Z+ShelterGeometry.OutsideZ(ShelterPassageRadius(c));
        if(s.Phase==ShelterVisitPhase.Approaching)
        {
            if(Distance(c.X,c.Z,s.X,doorZ)<.035)s.Phase=ShelterVisitPhase.Entering;
            else
            {
                double targetX=s.X,targetZ=doorZ;
                // Side/rear arrivals first go around the outside corner. Do not
                // drive a diagonal chord through the wall or invent a teleport.
                if(!ShelterMovePathClear(c,targetX,targetZ))
                {
                    double clearance=ShelterGeometry.OuterWidth*.5+ShelterPassageRadius(c)+.15;
                    targetX=s.X+(c.X>=s.X?1:-1)*clearance;
                    targetZ=c.Z<s.Z&&Math.Abs(c.X-s.X)<clearance-.03?c.Z:doorZ;
                }
                MoveShelterStep(s,c,targetX,targetZ,dt);c.Activity="走向小屋外的门前空地";
                if(s.BlockedSeconds>=12){c.Interaction.ShelterSearchAfter=State.WorldSeconds+20;ReleaseShelterGuest(s,"暂时走不到门口，先让出预约。");}
                return true;
            }
        }
        if(!s.DoorOpen)
        {
            ShelterWait(s,c,dt,"门还关着，停在门外等待。");
            if(!inside&&s.BlockedSeconds>=12){c.Interaction.ShelterSearchAfter=State.WorldSeconds+20;ReleaseShelterGuest(s,"门口关闭，这次入住预约结束。");}
            return true;
        }
        if(ShelterFullyInside(c,s)&&Distance(c.X,c.Z,s.X,s.Z)<.04)
        {s.Phase=ShelterVisitPhase.Inside;s.BlockedSeconds=0;s.LastEvent="它沿门槛完整走进屋内，仍保留自己的情绪。";return true;}
        MoveShelterStep(s,c,s.X,s.Z,dt);c.Activity="从门口慢慢进入小屋";return true;
    }
    private bool TrySeekShelter(Creature c,double dt)
    {
        if(c.Interaction.ShelterSearchAfter>State.WorldSeconds||c.Fear<35&&c.Interaction.Emotion.Alarm<12||
            State.Shelters.Any(s=>s.GuestId==c.Id))return false;
        var shelter=State.Shelters.Where(s=>s.RegionId==c.RegionId&&ShelterEntryReason(c,s.Id)==null&&
            Distance(c.X,c.Z,s.X,s.Z)<=5&&HasGroundSightLine(c,s.X,s.Z+ShelterGeometry.OutsideZ(ShelterPassageRadius(c))))
            .OrderBy(s=>Distance(c.X,c.Z,s.X,s.Z)).ThenBy(s=>s.Id,StringComparer.Ordinal).FirstOrDefault();
        if(shelter==null)return false;
        shelter.GuestId=c.Id;shelter.Phase=ShelterVisitPhase.Approaching;shelter.ExitRequested=false;shelter.BlockedSeconds=0;
        shelter.LastEvent=$"{c.Name}有些不安，决定先走到门口看看。";return TryActiveShelter(c,dt);
    }
}

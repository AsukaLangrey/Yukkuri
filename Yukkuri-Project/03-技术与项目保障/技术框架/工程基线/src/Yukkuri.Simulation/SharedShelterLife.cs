namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private static readonly (double X,double Z)[] SharedBeds=[(-1.08,-1.08),(1.08,-1.08),(0,0),(-1.08,1.08),(1.08,1.08)];
    public static string ShelterComfort(int count)=>count switch {0=>"空屋",<=2=>"最舒适",3=>"适用",4=>"拥挤",_=>"极度拥挤"};
    private bool HousingNight=>CourtyardCalendar.At(State.WorldSeconds).IsNight;
    private bool LiveHouseBody(Creature c,ShelterState s)=>c.RegionId==s.RegionId&&c.Condition!=PhysicalCondition.Dead&&
        !c.Family.Memorialized&&!c.Family.ColdStorage.Stored&&!c.Interaction.Carried&&!c.Interaction.Falling;
    private IEnumerable<Creature> SharedOccupants(ShelterState s)=>State.Creatures.Where(c=>LiveHouseBody(c,s)&&ShelterFullyInside(c,s));
    private int SharedShelterUsed(ShelterState s,Creature? except=null)=>s.Residents.Select(v=>v.CreatureId)
        .Concat(State.Creatures.Where(c=>ShelterBodyPresent(c,s)).Select(c=>c.Id)).Where(id=>id!=except?.Id).Distinct().Count();
    private ShelterState? SharedHome(Creature c)=>State.Shelters.Find(s=>s.Capacity==5&&s.Residents.Any(v=>v.CreatureId==c.Id));
    private bool NightUrgent(Creature c)=>NeedsOutdoorToilet(c)||CourtyardSurvival.Urgent(c,sleeping:true)||c.Pain>=40||
        HasPriorityWaterDuty(c)||c.Interaction.EatingRemaining>0||c.Interaction.ActiveCue!=null||c.Interaction.FollowPlayer||
        FamilyRules.UnderIntervention(c)||c.Family.Pregnancy.Phase==PregnancyPhase.Preparing||
        c.Family.Conflict.TargetId!=null||c.Family.ChildThreat.TargetId!=null||c.Personality.HungerTargetId!=null||WaterConcernTarget(c)!=null||
        c.Family.Pregnancy.Phase==PregnancyPhase.Gestating&&c.Family.Pregnancy.Elapsed>=FamilyRules.GestationSeconds(c.Family.Pregnancy.Method);
    private string? SharedShelterEntryReason(Creature c,ShelterState s)
    {
        if(!State.InteractionPlaytest||!State.Creatures.Contains(c)||c.RegionId!=s.RegionId)return "小屋与个体不在同一区域。";
        if(!CanSelfMove(c)||c.Interaction.Rolling||c.Interaction.Lowering||c.Interaction.Height>.05||
            c.Family.ColdStorage.RecoveryRemaining>0||c.Quarantine||c.IsIntruder)return "当前身体状态不能自行进屋。";
        if(NightUrgent(c)||InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy)return "先处理食水、伤痛、互动或紧急家庭事务。";
        if(ShelterTransitHeight(c,s)>ShelterGeometry.DoorHeight-.03||ShelterPassageRadius(c)>ShelterGeometry.DoorWidth*.5*ShelterGeometry.Scale(s)-.04)return "身体或饰物暂时无法安全通过门口。";
        if(SharedHome(c) is { } other&&other!=s)return "已经在前往另一间小屋。";
        if(SharedShelterUsed(s,c)>=s.Capacity)return "小屋已满（最多5只，包含正在进屋的油）。";
        return null;
    }
    private void SynchronizeSharedShelters()
    {
        foreach(var s in State.Shelters.Where(s=>s.Capacity==5))
        {
            s.Residents.RemoveAll(v=>!State.Creatures.Any(c=>c.Id==v.CreatureId&&LiveHouseBody(c,s)));
            foreach(var c in SharedOccupants(s).ToArray())
            {
                if(SharedHome(c)!=null||s.Residents.Count>=5)continue;
                int slot=Enumerable.Range(0,5).Except(s.Residents.Select(v=>v.Slot)).MinBy(n=>Distance(c.X,c.Z,s.X+SharedBeds[n].X,s.Z+SharedBeds[n].Z));
                s.Residents.Add(new(){CreatureId=c.Id,Slot=slot,Phase=ShelterVisitPhase.Inside});
            }
        }
    }
    private ShelterState? SleepingHome(Creature c)=>State.Shelters.Find(s=>LiveHouseBody(c,s)&&ShelterFullyInside(c,s)&&
        (s.Capacity==5?s.Residents.Any(v=>v.CreatureId==c.Id&&v.Phase==ShelterVisitPhase.Inside&&!v.ExitRequested):
        s.GuestId==c.Id&&s.Phase==ShelterVisitPhase.Inside&&!s.ExitRequested));
    private bool IsNightHouseSleep(Creature c)=>ScheduledRest(c)&&SleepingHome(c)!=null&&
        !NightUrgent(c)&&c.Fear<60&&c.Pain<40&&
        !c.Interaction.Physiology.Active&&!c.Interaction.Supine&&c.Interaction.Restraint==RestraintKind.None&&
        c.Interaction.Height<.05&&!(InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy);
    private double NightRestRate(Creature c)=>SleepingHome(c) is { } s?SharedOccupants(s).Count() switch {<=2=>.7,3=>.55,4=>.35,_=>.2}:.4;
    private void TickNightHousing(Creature c,double dt)
    {
        var i=c.Interaction;
        if(c.Family.ColdStorage.Stored||c.Family.Memorialized)return;
        if(!HousingNight)
        {i.NightExposureSeconds=0;i.NightHousingDiscomfort=Math.Max(0,i.NightHousingDiscomfort-dt*.12);return;}
        var home=State.Shelters.FirstOrDefault(s=>LiveHouseBody(c,s)&&ShelterFullyInside(c,s));
        if(home!=null)
        {
            i.NightExposureSeconds=0;
            int count=State.Creatures.Count(o=>LiveHouseBody(o,home)&&ShelterFullyInside(o,home));
            double target=count switch {<=3=>0,4=>12,_=>22};
            i.NightHousingDiscomfort+=Math.Clamp(target-i.NightHousingDiscomfort,-dt*.4,dt*.2);
        }
        else
        {
            i.NightExposureSeconds=Math.Min(600,i.NightExposureSeconds+dt);
            if(i.NightExposureSeconds>15)i.NightHousingDiscomfort=Math.Min(30,i.NightHousingDiscomfort+dt*.18);
        }
    }
    public string NightHousingStatus(Creature c)
    {
        if(!HousingNight)return c.Interaction.NightHousingDiscomfort>1?"昨夜没住好，幸福感正在恢复。":"";
        if(SharedHome(c) is { } s)
        {
            var v=s.Residents.Single(v=>v.CreatureId==c.Id);
            if(ShelterFullyInside(c,s))return $"夜间住宿：{SharedOccupants(s).Count()}/5 · {ShelterComfort(SharedOccupants(s).Count())}"+(c.Interaction.Resting?" · 睡觉中":"");
            return v.ExitRequested?"正在离开小屋处理需要。":"天黑了，正在寻找小屋里的睡觉位置。";
        }
        if(State.Shelters.Any(s=>s.Capacity==1&&LiveHouseBody(c,s)&&ShelterFullyInside(c,s)))return "夜间住宿：已在旧式小家内。";
        return c.Interaction.NightExposureSeconds>15?"夜晚没有住进小屋，幸福感逐渐下降。":"天黑了，希望找一间小屋睡觉。";
    }
    private bool TrySeekSharedShelter(Creature c,double dt)
    {
        if((!ScheduledRest(c)&&!c.Family.SeekingWarmth)||c.Interaction.ShelterSearchAfter>State.WorldSeconds||SharedHome(c)!=null||
            State.Shelters.Any(s=>s.GuestId==c.Id))return false;
        var s=State.Shelters.Where(s=>s.Capacity==5&&SharedShelterEntryReason(c,s)==null)
            .OrderBy(s=>SharedShelterUsed(s)).ThenBy(s=>Distance(c.X,c.Z,s.X,s.Z)).FirstOrDefault();
        if(s==null){c.Interaction.ShelterSearchAfter=State.WorldSeconds+30;return false;}
        int slot=Enumerable.Range(0,5).First(n=>s.Residents.All(v=>v.Slot!=n));
        s.Residents.Add(new(){CreatureId=c.Id,Slot=slot});c.Interaction.Resting=false;
        if(HousingNight)c.Interaction.RestRemaining=0;
        s.LastEvent=c.Family.SeekingWarmth?$"{c.Name}身体发冷，想进屋保暖。":$"{c.Name}想找个屋内的位置睡觉。";
        return TryActiveSharedShelter(c,dt);
    }
    private bool TryActiveSharedShelter(Creature c,double dt)
    {
        var s=SharedHome(c);if(s==null)return false;
        var v=s.Residents.Single(v=>v.CreatureId==c.Id);
        bool inside=ShelterBodyPresent(c,s);
        if(!ScheduledRest(c)&&!c.Family.SeekingWarmth||NightUrgent(c))v.ExitRequested=true;
        if(v.ExitRequested&&!inside)
        {
            // Collision returns as soon as the whole body clears the doorway.
            // No parking/queuing task keeps a departed resident reserved.
            s.Residents.Remove(v);c.Interaction.ShelterSearchAfter=State.WorldSeconds+10;
            if(HasGroundBodyOverlap(c))c.Interaction.LandingSeparationPending=true;
            return false;
        }
        if(c.Interaction.Physiology.Active||c.Interaction.EatingRemaining>0||FamilyRules.UnderIntervention(c))return false;
        if(inside&&v.ExitRequested&&CourtyardSurvival.Urgent(c,sleeping:true)&&TryEatNearby(c))return true;
        if(inside&&v.Phase==ShelterVisitPhase.Inside&&!v.ExitRequested)
        {c.TargetX=c.X;c.TargetZ=c.Z;c.Activity=IsNightHouseSleep(c)?"在小屋里睡觉":"在小屋里休息";return true;}
        if(!CanSelfMove(c))
        {
            c.Activity=!s.DoorOpen?"门帘关着，等人打开后出门":"暂时走不开，需要照护";
            if(IsNightHouseSleep(c))c.Activity="在小屋里睡觉";
            v.BlockedSeconds=Math.Min(600,v.BlockedSeconds+dt);s.LastEvent=c.Activity;return true;
        }
        double scale=ShelterGeometry.Scale(s);
        double front=s.Z+ShelterGeometry.OuterDepth*.5*scale+ShelterPassageRadius(c)+.15;
        double entryX=s.X+(v.Slot==3?-.85:v.Slot==4?.85:0);
        double tx=entryX,tz=front;
        if(v.ExitRequested)
        {
            v.Phase=ShelterVisitPhase.Exiting;c.Interaction.Resting=false;
            s.DoorOpen=true;
            if(!s.DoorPassable){c.Activity="等门帘完全打开，再出门";return true;}
            v.VisualHidden=false;
            // Leave along the bed's own aisle before converging outside.
            double aisle=ShelterGeometry.DoorWidth*.5*scale-ShelterPassageRadius(c)-.06;
            tx=Math.Clamp(c.X,s.X-aisle,s.X+aisle);c.Activity="天亮或有事，沿门口走出小屋";
            if(Math.Abs(tx-c.X)>.02)tz=c.Z;
        }
        else if(v.Phase==ShelterVisitPhase.Approaching)
        {
            if(Distance(c.X,c.Z,entryX,front)<.08)v.Phase=ShelterVisitPhase.Entering;
            else if(!ShelterMovePathClear(c,tx,tz))
            {
                double clearance=ShelterGeometry.OuterWidth*.5*scale+ShelterPassageRadius(c)+.2;
                tx=s.X+(c.X>=s.X?1:-1)*clearance;
                tz=c.Z<s.Z&&Math.Abs(c.X-s.X)<clearance-.03?c.Z:front;
            }
            c.Activity=HousingNight?"天黑了，走向小屋门口":c.Family.SeekingWarmth?"身体发冷，走向小屋门口":"特别困了，进屋补一觉";
        }
        if(v.Phase==ShelterVisitPhase.Entering&&!v.ExitRequested)
        {
            s.DoorOpen=true;if(!s.DoorPassable){c.Activity="等门帘完全打开，再迈过门槛";return true;}
            var bed=SharedBeds[v.Slot];tx=s.X+bed.X;tz=s.Z+bed.Z;
            // Cross the open doorway before turning towards a bed.
            if(c.Z>s.Z+1.2){tx=entryX;tz=s.Z+1.15;}
            if(Distance(c.X,c.Z,s.X+bed.X,s.Z+bed.Z)<.08&&ShelterFullyInside(c,s))
            {v.Phase=ShelterVisitPhase.Inside;v.VisualHidden=true;v.BlockedSeconds=0;RecordObjectEvent(s.Id,$"{c.Name}入住小屋。",c.Id);s.LastEvent=$"{c.Name}走进小屋，找到了睡觉位置。";return true;}
            c.Activity="从门口走进小屋，寻找睡觉位置";
        }
        double before=Distance(c.X,c.Z,tx,tz);RoomMove(c,tx,tz,dt);
        if(Distance(c.X,c.Z,tx,tz)<before-.0001)v.BlockedSeconds=0;
        else v.BlockedSeconds=Math.Min(600,v.BlockedSeconds+dt);
        if(v.BlockedSeconds>=12)
        {
            s.LastEvent="门口或屋内通路受阻，请移开障碍。";c.Activity=s.LastEvent;
            if(!inside){s.Residents.Remove(v);c.Interaction.ShelterSearchAfter=State.WorldSeconds+10;}
        }
        return true;
    }
}

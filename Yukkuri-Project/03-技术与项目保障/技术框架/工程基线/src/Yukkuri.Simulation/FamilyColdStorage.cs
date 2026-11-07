namespace Yukkuri.Simulation;

public sealed class ColdStorageState
{
    public bool Stored { get; set; }
    public double Elapsed { get; set; }
    public double RecoveryRemaining { get; set; }
    public int Sessions { get; set; }
}

public sealed partial class WorldSimulation
{
    public const int ColdStorageCapacity=12;
    // Kept for source compatibility with older checks; stored biology no longer expires.
    public const double ColdStorageLimitSeconds=600;
    public static double BiologicalTimeScale(Creature c)=>c.Family.ColdStorage.Stored?0:1;
    public static double BiologicalDelta(Creature c,double dt)=>dt*BiologicalTimeScale(c);

    public string SetColdStorage(string id,bool stored)
    {
        var c=State.Creatures.Find(x=>x.Id==id);
        var error=!stored&&c?.Family.ColdStorage.Stored==true?
            (State.Settings.Pause?"先恢复时间。":null):FamilyCommandError(c,allowDead:true);
        if(error!=null)return error;
        var cold=c!.Family.ColdStorage;var i=c.Interaction;
        if(cold.Stored==stored)return stored?"它已经在冷藏中。":"它已经在小院里。";
        if(stored)
        {
            if(c.Family.Memorialized)return "已经安置的个体不能再次收纳。";
            if(WaterEnvironment.IsInWater(State,c))return "先用救援用品带回岸上，冷藏不能代替水域救援。";
            if(State.Mode==PlayMode.CloseUp)return "先回到小院，再安排冷藏。";
            if(i.Carried||i.Falling||i.Lowering||i.Restraint!=RestraintKind.None||i.Height>.05||HasInteractionGesture&&InteractionGestureTarget==id)
                return "先放稳并结束接触、解除装置，再安排冷藏。";
            if(State.Creatures.Count(x=>x.Family.ColdStorage.Stored)>=ColdStorageCapacity)return "回收箱12个位置已满，对象仍留在庭院。";
            if(i.EatingRemaining>0)return "先等它咽下这一口。";
            if(FamilyRules.UnderIntervention(c))CancelIntervention(c,"安排冷藏时干预已停止；已用材料和关系后果保留。");
            CancelWildSelfCare(c,"进入冷藏，自行护伤已中断；尚未获得护理效果。");
            CancelFamilyCare(c,reason:"进入冷藏，递送已中断；已取份额留在身旁。");DetachNaturalIntake(c);ClearCompanionship(c);
            EndHumanCompany(c);
            i.ActiveCue=null;i.FollowPlayer=false;i.Resting=false;i.RestRemaining=0;
            i.ReactionPose="idle";i.ReactionPoseRemaining=0;i.PendingConversation=null;
            i.VelocityX=0;i.VelocityZ=0;i.Physiology.Excreting=ExcretionKind.None;i.Physiology.Remaining=0;
            cold.Stored=true;cold.Elapsed=0;cold.RecoveryRemaining=0;cold.Sessions=Math.Min(int.MaxValue-1,cold.Sessions)+1;
            ClearFamilyTargetsTo(c);
            c.TargetX=c.X;c.TargetZ=c.Z;c.Activity="冷藏保存中";c.Speech="";c.ReactionRemaining=0;
            RecordInteraction(c,"cold-storage","进入回收箱，生理时间暂停，身份与关系保留。",2);
            return State.LastActionMessage="已放入回收箱，生理时间暂停。可从新油分类的回收箱管理。";
        }
        string oldRegion=c.RegionId;c.RegionId=State.ActiveRegionId;
        if(!TryColdReturnPoint(c,out var point))
        {c.RegionId=oldRegion;return "庭院没有合适的干燥空位，仍在回收箱。";}
        cold.Stored=false;c.X=point.X;c.Z=point.Z;c.TargetX=c.X;c.TargetZ=c.Z;
        cold.RecoveryRemaining=0;
        c.Activity=c.Condition==PhysicalCondition.Dead?"遗体已取出":"已返回庭院";
        RecordInteraction(c,"cold-return","同一个体从回收箱返回庭院，原有身体状态保留。",2);
        return State.LastActionMessage="已返回庭院，回收箱位置已释放。";
    }

    private bool TryColdReturnPoint(Creature c,out (double X,double Z) point)
    {
        point=(c.X,c.Z);
        if(CanOccupyRoom(c,c.X,c.Z))return true;
        for(double radius=.4;radius<=14;radius+=.4)
            for(int angle=0;angle<32;angle++)
            {
                double x=c.X+Math.Cos(angle*Math.Tau/32)*radius,z=c.Z+Math.Sin(angle*Math.Tau/32)*radius;
                if(!CanOccupyRoom(c,x,z))continue;
                point=(x,z);return true;
            }
        return false;
    }

    private void TickColdStorage(double dt)
    {
        foreach(var c in State.Creatures)
        {
            var cold=c.Family.ColdStorage;
            if(cold.Stored)
            {
                cold.Elapsed=Math.Min(1000000,cold.Elapsed+dt);
                c.Activity="回收箱保存中 · 生理时间暂停";
            }
            else cold.RecoveryRemaining=Math.Max(0,cold.RecoveryRemaining-dt);
        }
    }
}

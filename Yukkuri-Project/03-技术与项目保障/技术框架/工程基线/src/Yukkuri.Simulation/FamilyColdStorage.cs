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
    // Finite preservation, never a reset of a rescue window. Tuning is a draft.
    public const double ColdStorageLimitSeconds=600;
    public static double BiologicalTimeScale(Creature c)=>c.Family.ColdStorage.Stored&&
        c.Family.ColdStorage.Elapsed<ColdStorageLimitSeconds?.1:1;
    public static double BiologicalDelta(Creature c,double dt)
    {
        double slowed=c.Family.ColdStorage.Stored?Math.Clamp(ColdStorageLimitSeconds-c.Family.ColdStorage.Elapsed,0,dt):0;
        return slowed*.1+(dt-slowed);
    }

    public string SetColdStorage(string id,bool stored)
    {
        var c=State.Creatures.Find(x=>x.Id==id);var error=FamilyCommandError(c,allowDead:!stored);
        if(error!=null)return error;
        var cold=c!.Family.ColdStorage;var i=c.Interaction;
        if(cold.Stored==stored)return stored?"它已经在冷藏中。":"它已经在小院里。";
        if(stored)
        {
            if(State.InteractionPlaytest&&ShopQuantity("freezer")<=0)return "先在商店购买冷藏箱。";
            if(WaterEnvironment.IsInWater(State,c))return "先用救援用品带回岸上，冷藏不能代替水域救援。";
            if(State.Mode==PlayMode.CloseUp)return "先回到小院，再安排冷藏。";
            if(cold.RecoveryRemaining>0)return "取出后还需要恢复，不能立即反复冷藏。";
            if(i.Carried||i.Falling||i.Lowering||i.Restraint!=RestraintKind.None||i.Height>.05||HasInteractionGesture&&InteractionGestureTarget==id)
                return "先放稳并结束接触、解除装置，再安排冷藏。";
            if(State.Creatures.Count(x=>x.Family.ColdStorage.Stored)>=4)return "目前四个保存位置已占满，先妥善安置取出的个体。";
            if(i.EatingRemaining>0)return "先等它咽下这一口。";
            if(FamilyRules.UnderIntervention(c))CancelIntervention(c,"安排冷藏时干预已停止；已用材料和关系后果保留。");
            CancelWildSelfCare(c,"进入冷藏，自行护伤已中断；尚未获得护理效果。");
            CancelFamilyCare(c,reason:"进入冷藏，递送已中断；已取份额留在身旁。");DetachNaturalIntake(c);ClearCompanionship(c);
            EndHumanCompany(c);
            i.ActiveCue=null;i.FollowPlayer=false;i.Resting=false;i.RestRemaining=0;
            i.ReactionPose="idle";i.ReactionPoseRemaining=0;i.PendingConversation=null;
            i.VelocityX=0;i.VelocityZ=0;i.Physiology.Excreting=ExcretionKind.None;i.Physiology.Remaining=0;
            cold.Stored=true;cold.Elapsed=0;cold.Sessions=Math.Min(int.MaxValue-1,cold.Sessions)+1;
            ClearFamilyTargetsTo(c);
            c.TargetX=c.X;c.TargetZ=c.Z;c.Activity="冷藏保存中";c.Speech="";c.ReactionRemaining=0;
            RecordInteraction(c,"cold-storage","进入有限冷藏，生命过程十倍延缓，仍有保存期限。",2);
            return State.LastActionMessage="已放入冷藏。相关过程按正常速度的十分之一推进，需要及时取出。";
        }
        if(!TryColdReturnPoint(c,out var point))return "小院没有合适的干燥空位，先腾出位置；冷藏不会因此无限延长期限。";
        cold.Stored=false;c.X=point.X;c.Z=point.Z;c.TargetX=c.X;c.TargetZ=c.Z;
        cold.RecoveryRemaining=c.Condition==PhysicalCondition.Dead?0:30;
        // Once per exit, after storage has ended. Loading never rerolls it.
        if(c.Condition!=PhysicalCondition.Dead&&cold.Elapsed>300&&Next()<.12+Math.Max(0,40-c.Health)*.008)
            Hurt(c,4+Math.Max(0,cold.Elapsed-ColdStorageLimitSeconds)*.02,"冷藏后的复苏负担",null);
        if(c.Condition==PhysicalCondition.Dead)
        {
            cold.RecoveryRemaining=0;
            RecordInteraction(c,"cold-return","遗体已从冷藏取出，保存位置已释放。",2);
            return State.LastActionMessage="遗体已经取出，冷藏位置已释放。";
        }
        c.Activity="取出冷藏，慢慢恢复";
        RecordInteraction(c,"cold-return","从冷藏取出，已有伤势仍在，需要观察复苏与照护。",2);
        return State.LastActionMessage="已经取出，先观察身体并提供适用照护。原有伤势和期限没有重置。";
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
                if(cold.Elapsed>=ColdStorageLimitSeconds&&c.RegionId==State.ActiveRegionId)
                    SetColdStorage(c.Id,false);
                if(cold.Stored)c.Activity=cold.Elapsed>=ColdStorageLimitSeconds?"冷藏已超时，需腾出空位取出":"冷藏保存中";
            }
            else cold.RecoveryRemaining=Math.Max(0,cold.RecoveryRemaining-dt);
        }
    }
}

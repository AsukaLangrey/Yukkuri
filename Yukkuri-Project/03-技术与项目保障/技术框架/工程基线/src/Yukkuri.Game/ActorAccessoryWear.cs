using System;
using Yukkuri.Simulation;

public static class ActorAccessoryWear
{
    public static float DirtOpacity(AccessoryState item)=>(float)Math.Clamp(item.Dirt/30,0,1);
    public static float TearOpacity(AccessoryState item,int index)=>(float)Math.Clamp(((index==0?90:50)-item.Integrity)/30,0,1);
    public static float StitchOpacity(AccessoryState item,int index)
    {
        if(item.RepairProgress<=0)return 0;
        double repaired=item.RepairProgress*.75;
        double baseline=Math.Clamp(item.Integrity-repaired,0,100);
        double fraction=Math.Clamp(repaired/Math.Max(1,100-baseline),0,1);
        int stitchCount=baseline<50?8:4;
        return index>=stitchCount?0:(float)Math.Clamp(fraction*stitchCount-index,0,1);
    }
}

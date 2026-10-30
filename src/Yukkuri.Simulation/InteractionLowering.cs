using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    [JsonIgnore] public bool Lowering { get; internal set; }
}

public sealed partial class WorldSimulation
{
    private void AdvanceSupportedLowering(Creature c,double dt)
    {
        var i=c.Interaction;
        if(!i.Lowering)return;
        if(!i.Carried||i.Falling||i.Restraint!=RestraintKind.None){i.Lowering=false;return;}
        double scale=c.IsYoung?.73:c.IsElder?1.03:1;
        i.Height=Math.Max(0,i.Height-dt*1.2*scale);
        if(i.Height>0)return;
        i.Lowering=false;i.Carried=false;
        if(WaterEnvironment.IsInWater(State,c)){EnterWater(c,true);return;}
        if(i.ReactionSituation=="lowering"&&c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering)
            Feedback(c,"down",Expression.Shy,CanSelfMove(c)?"已经由手托着放到地面，可以观察它自己活动。":"已经稳稳放到地面，原有伤势仍影响走动。","settle",false);
    }
}

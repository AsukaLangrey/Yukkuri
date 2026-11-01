using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private bool _waterRescueToolArmed;
    private void ArmWaterRescueTool()
    {
        CancelGesture();_toolArmed=false;_waterRescueToolArmed=true;
        _message=Sim.State.WaterRescueTools>0?
            $"已拿出救援用品，剩余 {Sim.State.WaterRescueTools} 份。点击落水的油，尝试救到岸上。":
            $"救援用品已经用完。先在用品面板购买，每份 {WaterEnvironment.RescueToolPrice} 元。";
        RefreshRoomUi();
    }

    private (string Hint,bool Candidate) WaterRescueHint(Contact? contact)
    {
        // Mirror the command's public prerequisites, not its private bank search.
        // A candidate may still fail because no safe bank position is available.
        if(Sim.State.Settings.Pause)return ("暂停中 · 先继续时间再救助",false);
        if(contact==null)return ("救援用品 · 对准落水的油",false);
        if(contact.TargetKind!=InteractionTargetKind.Body)
            return ("救援用品请对准落水油的身体",false);
        var c=Sim.State.InteractionPlaytest?
            Sim.State.Creatures.Find(c=>c.Id==contact.Id&&c.RegionId==Sim.State.ActiveRegionId):null;
        if(c==null||c.Condition==PhysicalCondition.Dead)
            return ("当前没有可以救助的落水油",false);
        if(!WaterEnvironment.IsInWater(Sim.State,c))return ("它现在没有落水",false);
        if(Sim.State.WaterRescueTools<=0)return ("救援用品已用完 · 先购买用品",false);
        if(c.Interaction.Restraint!=RestraintKind.None)return ("先解除固定装置，再尝试救起",false);
        return ("点击尝试救起 · 成功消耗 1 份救援用品",true);
    }
}
